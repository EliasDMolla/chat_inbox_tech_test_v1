Implementá `Docs/Features/FEAT-006-envio-mensajes-telegram.md`.

Antes de modificar código:

1. Leé `AGENTS.md`.
2. Leé `Docs/Memory/ProjectStatus.md`.
3. Leé:
   - `Docs/Adrs/ADR-001.md`
   - `Docs/Adrs/ADR-002.md`
   - `Docs/Adrs/ADR-003.md`
4. Leé completa FEAT-006.
5. Inspeccioná la implementación actual de FEAT-001 a FEAT-005.
6. Revisá especialmente:
   - Contact
   - Conversation
   - Message
   - estrategia de advisory locks
   - IRealtimeNotifier
   - ConversationUpdated
   - MessageReceived
   - endpoints existentes
   - configuración de Telegram ya utilizada para el webhook

Implementá únicamente el alcance definido en FEAT-006.

Objetivo principal:

POST /api/conversations/{conversationId}/messages
→ Application
→ Telegram Bot API
→ Telegram confirma envío
→ Persistir Message Outbound
→ Actualizar Conversation
→ Commit
→ SignalR
→ respuesta HTTP

No utilizar RabbitMQ para mensajes salientes.

## Endpoint

Implementar:

POST /api/conversations/{conversationId}/messages

Request mínimo:

{
  "text": "Hola, ¿cómo estás?"
}

Validar:
- texto requerido;
- no whitespace;
- longitud razonable compatible con Telegram.

Si Conversation no existe:
→ 404

Si el texto es inválido:
→ 400

Si Telegram rechaza o falla el envío:
→ respuesta HTTP de error coherente.

No devolver éxito si Telegram no recibió correctamente el mensaje.

## Clean Architecture

Application no debe depender de la librería de Telegram.

Definir una abstracción similar a:

public interface ITelegramMessageSender
{
    Task<TelegramSendResult> SendTextMessageAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken);
}

El nombre puede adaptarse a las convenciones actuales.

Infrastructure debe implementar esta abstracción utilizando Telegram Bot API.

No usar tipos de Telegram dentro de Domain/Application salvo modelos propios.

## Flujo de envío

El flujo esperado es:

1. Recibir conversationId y texto.
2. Obtener Conversation y Contact necesarios.
3. Validar que exista TelegramChatId.
4. Adquirir el mismo advisory lock utilizado para operaciones concurrentes del chat.
5. Enviar mensaje a Telegram.
6. Confirmar respuesta exitosa de Telegram.
7. Crear Message con Direction = Outbound.
8. Persistir TelegramMessageId devuelto por Telegram si corresponde.
9. Actualizar LastMessageText.
10. Actualizar LastMessageAt.
11. NO incrementar UnreadCount.
12. Commit.
13. Emitir SignalR.
14. Responder éxito.

No persistir el mensaje como exitosamente enviado antes de que Telegram confirme el envío.

## Orden Telegram / base de datos

Para esta prueba priorizar:

Telegram Send
→ DB Commit

Esto implica un trade-off:

Telegram podría aceptar el mensaje y luego PostgreSQL podría fallar.

Documentar explícitamente esta limitación.

No implementar Distributed Transaction ni Outbox/Inbox adicional para resolverla.

La alternativa de guardar primero y enviar después requeriría modelar estados de entrega, retries y reconciliación, lo cual está fuera del alcance actual.

Registrar esta decisión en README o documentación si corresponde.

## Concurrencia

Reutilizar la estrategia existente de advisory lock por chat.

El envío saliente debe coexistir correctamente con:

- mensajes entrantes;
- mark-as-read;
- otros mensajes salientes.

El lock debe evitar que LastMessage/LastMessageAt queden inconsistentes por operaciones concurrentes del mismo chat.

No bloquear conversaciones diferentes entre sí.

## Dominio

Agregar comportamiento de dominio solamente si aporta valor.

Un mensaje saliente:

- Direction = Outbound.
- IsRead debe tener semántica coherente; normalmente un mensaje del operador no debe contarse como unread.
- No incrementa Conversation.UnreadCount.
- Actualiza LastMessage.
- Actualiza LastMessageAt.

No introducir estados complejos de delivery salvo necesidad explícita.

## Fecha del mensaje

Preferir la fecha devuelta por Telegram si está disponible y representa correctamente el momento de envío.

Mantener almacenamiento en UTC.

No utilizar `DateTime.Now`.

## Identificadores Telegram

Guardar `TelegramMessageId` retornado por Telegram.

El `TelegramUpdateId` aplica a mensajes entrantes y no debe inventarse para mensajes salientes si Telegram no lo provee en ese flujo.

Revisar las restricciones existentes para que soporten correctamente mensajes Outbound.

Si el esquema actual requiere cambios, crear una migración mínima y coherente.

No debilitar las garantías de idempotencia de mensajes entrantes.

## SignalR

Después del commit exitoso emitir realtime.

Evaluar reutilizar:

MessageSent
ConversationUpdated

o, si la arquitectura actual tiene un contrato genérico claro, reutilizar un evento de mensaje con Direction = Outbound.

Preferencia:
- mantener eventos explícitos y fáciles de entender;
- evitar duplicar contratos casi idénticos sin necesidad.

El frontend futuro debe poder agregar inmediatamente el mensaje saliente.

ConversationUpdated debe reflejar:

- nuevo LastMessageText;
- nuevo LastMessageAt;
- UnreadCount sin incrementar.

Nunca emitir SignalR antes del commit.

Si SignalR falla después del commit:
- loggear;
- no revertir DB;
- mantener éxito HTTP;
- PostgreSQL sigue siendo fuente de verdad.

## Fallos Telegram

Distinguir fallos esperables.

Ejemplos:
- bot bloqueado por usuario;
- chat inexistente;
- token/configuración inválida;
- timeout;
- error HTTP Telegram.

No filtrar detalles sensibles al cliente.

Loggear información suficiente para diagnóstico.

Nunca loggear BotToken.

No persistir `Message` como exitosamente enviado ante fallo Telegram.

## Idempotencia de envío HTTP

No agregar un sistema complejo de idempotency keys salvo que esté definido en la feature.

Como mínimo, prevenir doble submit en frontend posteriormente.

Si se decide incorporar un request id simple, justificarlo.

No sobrearquitecturar.

## API response

Devolver un DTO del mensaje creado.

Ejemplo conceptual:

{
  "id": "...",
  "conversationId": "...",
  "direction": "Outbound",
  "text": "Hola",
  "sentAt": "...",
  "isRead": true
}

No exponer:
- tokens;
- datos internos de infraestructura;
- Telegram identifiers innecesarios.

## Telegram sender

Reutilizar la configuración existente:

TELEGRAM_BOT_TOKEN

No crear múltiples clientes Telegram si la librería permite reutilización segura.

Registrar la implementación mediante DI.

Respetar CancellationToken.

## Tests

Agregar tests de valor real.

Como mínimo:

1. Conversation inexistente → NotFound.
2. Texto vacío → validación.
3. Telegram exitoso → Message Outbound persistido.
4. Telegram exitoso → UnreadCount no cambia.
5. Telegram exitoso → LastMessage se actualiza.
6. Telegram exitoso → LastMessageAt se actualiza.
7. Telegram falla → no se persiste Message.
8. Telegram falla → no se emite SignalR.
9. Persistencia exitosa → SignalR después del commit.
10. SignalR falla después del commit → mensaje sigue persistido y la operación HTTP se considera exitosa.
11. Verificar concurrencia básica con el lock existente si resulta razonable.

No testear internals de la librería Telegram.

Mockear la abstracción ITelegramMessageSender en tests de Application.

## Validación manual sin Telegram real

Si no existen credenciales Telegram, implementar una estrategia de test que NO altere el comportamiento productivo.

Podés:
- utilizar un fake/mock en tests;
- verificar el endpoint a nivel Application.

No agregar un fake runtime activado silenciosamente en producción.

## Validación manual con Telegram real

Si existen credenciales:

1. Levantar stack:

docker compose up --build

2. Generar una Conversation mediante un mensaje entrante real.

3. Ejecutar:

POST /api/conversations/{conversationId}/messages

{
  "text": "Respuesta desde EncodeLabs Inbox"
}

4. Confirmar que el usuario recibe el mensaje en Telegram.

5. Confirmar PostgreSQL:
- Message Outbound creado;
- TelegramMessageId guardado;
- UnreadCount sin incremento;
- LastMessage actualizado.

6. Confirmar SignalR:
- evento de mensaje saliente;
- ConversationUpdated.

7. Probar texto inválido.

8. Si es posible, probar un fallo de Telegram y confirmar que no exista Message persistido.

## Migraciones

Si el modelo actual necesita modificar columnas/restricciones para soportar mensajes salientes, crear una migración nueva.

No modificar manualmente la migración inicial ya aplicada salvo una razón muy fuerte.

Mantener historial de migraciones correcto.

## README

Actualizar únicamente lo implementado.

Agregar:
- endpoint de envío;
- ejemplo de request;
- flujo de envío;
- comportamiento ante fallo de Telegram;
- trade-off Telegram exitoso + fallo DB;
- realtime asociado.

No documentar todavía UI Angular completa.

## Memory

Actualizar obligatoriamente:

`Docs/Memory/ProjectStatus.md`

Agregar:

`FEAT-006 — Envío de mensajes salientes a Telegram`

Registrar:

- Fecha.
- Estado.
- Endpoint.
- Abstracción Telegram utilizada.
- Flujo Telegram → DB → SignalR.
- Estrategia de concurrencia.
- Comportamiento de UnreadCount.
- Manejo de fallos.
- Trade-off de consistencia externa.
- Eventos realtime.
- Tests y validaciones.
- Prueba manual.
- Limitaciones.
- Próxima feature recomendada.

## Validaciones obligatorias

Ejecutar:

dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes

Esperado:
- 0 errores;
- 0 warnings relevantes;
- tests OK.

Ejecutar:

docker compose config

Si Docker está disponible:

docker compose up --build

y verificar servicios.

Si no se modifica frontend, evitar cambios innecesarios en Angular.

## Fuera de alcance

No implementar:

- cola RabbitMQ outbound;
- retry persistente de mensajes salientes;
- delivery receipts;
- estados Pending/Sent/Failed complejos;
- Outbox Pattern;
- frontend completo;
- autenticación;
- adjuntos;
- multimedia.

No anticipar FEAT-007.

## Revisión final

Antes de dar FEAT-006 por terminada:

1. Revisá el diff completo.
2. Confirmá que Application no depende de Telegram SDK.
3. Confirmá que no se usa RabbitMQ para outbound.
4. Confirmá que Telegram debe devolver éxito antes de persistir como enviado.
5. Confirmá que Outbound no incrementa UnreadCount.
6. Confirmá que LastMessage queda consistente.
7. Revisá concurrencia con incoming/read/outbound.
8. Confirmá SignalR posterior al commit.
9. Confirmá que fallos Telegram no crean mensajes exitosos.
10. Confirmá que no se exponen secretos.
11. Confirmá que README y Memory describen el comportamiento real.
12. Confirmá que no se implementaron features futuras.

En la respuesta final informá:

- Qué implementaste.
- Endpoint creado.
- Abstracción e implementación Telegram.
- Orden exacto del flujo de envío.
- Modelo persistido.
- Estrategia de concurrencia.
- Manejo de errores Telegram.
- Trade-off entre Telegram y PostgreSQL.
- Eventos SignalR.
- Tests y resultados.
- Prueba Docker/manual.
- Limitaciones pendientes.
- Próxima feature recomendada.

No des FEAT-006 por terminada únicamente porque compile.