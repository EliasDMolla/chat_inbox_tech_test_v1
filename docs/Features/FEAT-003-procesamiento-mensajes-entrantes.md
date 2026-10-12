Implementá `Docs/Features/FEAT-003-procesamiento-mensajes-entrantes.md`.

Antes de modificar código:

1. Leé `AGENTS.md`.
2. Leé `Docs/Memory/ProjectStatus.md`.
3. Leé completos:
   - `Docs/Adrs/ADR-001.md`
   - `Docs/Adrs/ADR-003.md`
4. Leé completa la feature FEAT-003.
5. Inspeccioná la implementación actual resultante de FEAT-001 y FEAT-002.
6. Revisá especialmente el contrato de mensaje entrante ya publicado por RabbitMQ para reutilizarlo y no duplicar modelos.

Implementá únicamente el alcance definido en FEAT-003.

Objetivo principal:

```text
RabbitMQ
→ Consumer
→ Validación / idempotencia
→ Contact
→ Conversation
→ Message
→ PostgreSQL
→ Commit
→ ACK
```

Todavía no implementes SignalR funcional, endpoints de conversaciones, mark-as-read ni envío de mensajes salientes, salvo que la feature lo indique explícitamente.

Prestá especial atención a los siguientes puntos.

## Clean Architecture

- Domain no debe depender de EF Core, RabbitMQ, ASP.NET Core ni Infrastructure.
- Application debe contener los casos de uso y abstracciones necesarias.
- Infrastructure debe implementar EF Core, PostgreSQL y el consumer RabbitMQ.
- No pongas lógica de negocio en el consumer.
- El consumer debe deserializar, delegar y resolver ACK/NACK según el resultado.

## Modelo de dominio

Crear únicamente las entidades necesarias:

- Contact
- Conversation
- Message

Mantener el dominio simple y coherente con el alcance.

Conversation debe poder representar:

- Contacto asociado.
- Cantidad de mensajes no leídos.
- Último mensaje.
- Fecha/hora del último mensaje.

Message debe contemplar como mínimo:

- Id interno.
- ConversationId.
- TelegramMessageId.
- TelegramUpdateId.
- Dirección Inbound/Outbound.
- Texto.
- Estado leído/no leído.
- Fecha del mensaje.
- Fecha de creación.

No agregar entidades innecesarias.

## Persistencia

Usar PostgreSQL con EF Core.

Crear mappings/configuraciones explícitas cuando aporten claridad.

Agregar índices y restricciones para proteger:

- TelegramUpdateId.
- Telegram identifiers relevantes.
- Relaciones Contact → Conversation.
- Conversation → Messages.

Crear la migración inicial necesaria para estas entidades.

No hardcodear connection strings.

## Idempotencia

Este punto es crítico.

Telegram/RabbitMQ pueden ocasionar reentregas.

No confiar únicamente en una consulta previa del estilo:

```csharp
if (!exists)
{
    insert
}
```

La base de datos debe ser la última barrera de consistencia.

Aplicar una restricción UNIQUE sobre `TelegramUpdateId` o una clave equivalente suficientemente fuerte.

El flujo debe tolerar que dos consumidores intenten procesar el mismo update casi al mismo tiempo.

Un update duplicado debe terminar en un estado válido y NO debe:

- Crear dos mensajes.
- Incrementar dos veces el contador de no leídos.
- Crear contactos duplicados.
- Crear conversaciones duplicadas.
- Considerarse un error fatal si ya fue procesado correctamente.

## Concurrencia

Revisar cuidadosamente el caso de dos mensajes diferentes que llegan casi al mismo tiempo para la misma conversación.

Evitar perder incrementos de:

```text
UnreadCount
```

y evitar que `LastMessage` o `LastMessageAt` queden con información vieja.

Preferir garantías transaccionales y de base de datos antes de introducir locks distribuidos.

No agregar Redis ni mecanismos complejos de locking.

## Transacción

El procesamiento de un mensaje entrante debe comportarse como una única operación lógica:

```text
Encontrar/crear contacto
→ Encontrar/crear conversación
→ Crear mensaje
→ Incrementar unread
→ Actualizar last message
→ Commit
```

No permitir estados parciales como:

```text
mensaje insertado
+
contador no actualizado
```

o:

```text
conversación creada
+
mensaje perdido
```

## RabbitMQ Consumer

Implementar un consumer para:

```text
telegram.incoming.messages
```

Debe:

1. Consumir el contrato creado en FEAT-002.
2. Deserializar el mensaje.
3. Delegar a Application.
4. Hacer ACK únicamente después de procesamiento + commit exitosos.
5. No hacer ACK antes de persistir.
6. Resolver fallos de manera coherente con ADR-003.
7. Evitar requeue infinito.
8. Permitir que mensajes poison terminen en la DLQ configurada.

No modificar innecesariamente la topología ya creada en FEAT-002.

## ACK / NACK

El flujo correcto debe ser conceptualmente:

```text
Consume
→ Process
→ DB transaction
→ Commit
→ ACK
```

Ante un fallo transitorio o de procesamiento:

- No marcar el mensaje como procesado correctamente.
- Aplicar la política definida para retry/reject.
- Evitar loops infinitos.
- Permitir llegada a DLQ cuando corresponda.

Un duplicado ya procesado correctamente puede considerarse éxito idempotente y ser ACKeado.

## Logging

Usar logging estructurado.

Registrar contexto útil como:

- TelegramUpdateId
- TelegramMessageId
- TelegramChatId
- ConversationId
- ContactId

No registrar secretos.

No registrar el contenido completo del mensaje salvo que exista una razón concreta.

Distinguir en logs:

- Mensaje procesado correctamente.
- Mensaje duplicado ignorado idempotentemente.
- Error transitorio.
- Error enviado a DLQ.
- Error de persistencia.

## Tests

Agregar tests con valor real.

Como mínimo revisar casos como:

- Primer mensaje crea Contact, Conversation y Message.
- Primer mensaje incrementa UnreadCount a 1.
- Segundo mensaje entrante incrementa correctamente el contador.
- Mensaje saliente no aplica en esta feature.
- Update duplicado no crea un segundo Message.
- Update duplicado no incrementa dos veces UnreadCount.
- Texto inválido no deja estado parcial.
- La lógica de dominio mantiene correctamente LastMessage y LastMessageAt.

Si es posible, agregar al menos un test de integración focalizado en la restricción UNIQUE/idempotencia con PostgreSQL o una estrategia equivalente realista.

No generar tests artificiales de poco valor.

## Migraciones

Crear la migración inicial correspondiente a Contact, Conversation y Message.

Verificar que:

```bash
dotnet ef database update
```

pueda aplicarse en un entorno configurado.

No generar migraciones vacías o innecesarias.

## Fuera de alcance

NO implementar todavía:

- SignalR funcional.
- Hub con eventos reales.
- API de listado de conversaciones.
- API de historial.
- Mark as read.
- Envío de mensajes hacia Telegram.
- Frontend funcional.
- Autenticación.
- Paginación.

No anticipar FEAT-004.

## Validaciones al finalizar

Ejecutá:

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
```

Si la feature agrega migraciones, validá también que EF pueda descubrirlas correctamente.

Ejecutá:

```bash
docker compose config
```

Si Docker está disponible:

```bash
docker compose up --build
```

y validá el flujo:

```text
mensaje en telegram.incoming.messages
→ consumer
→ PostgreSQL
→ ACK
```

Verificá manualmente en base de datos:

- Contact creado.
- Conversation creada.
- Message creado.
- UnreadCount correcto.
- LastMessage correcto.
- LastMessageAt correcto.

Luego republicá exactamente el mismo mensaje y verificá:

```text
1 Contact
1 Conversation
1 Message
UnreadCount sin incremento adicional
```

También probá dos mensajes distintos para el mismo contacto y verificá:

```text
2 Messages
UnreadCount = 2
LastMessage = segundo mensaje
```

Si es posible, inspeccioná RabbitMQ para confirmar que los mensajes procesados desaparecen de la cola luego del ACK.

## README

Actualizá `README.md` únicamente con comportamiento real implementado.

Agregar, si corresponde:

- Modelo de persistencia.
- Cómo aplicar/verificar migraciones.
- Cómo probar el consumer.
- Cómo verificar idempotencia.
- Cómo inspeccionar RabbitMQ y PostgreSQL.

No documentar SignalR ni funcionalidades que todavía no existen.

## Memory

Actualizar obligatoriamente:

`Docs/Memory/ProjectStatus.md`

Agregar:

`FEAT-003 — Procesamiento y persistencia de mensajes entrantes`

Registrar brevemente:

- Fecha.
- Estado.
- Qué quedó implementado.
- Decisiones técnicas relevantes.
- Modelo de datos final.
- Restricciones/índices importantes.
- Estrategia de idempotencia.
- Estrategia ACK/NACK/DLQ.
- Validaciones ejecutadas.
- Resultado de pruebas manuales.
- Limitaciones pendientes.
- Próxima feature recomendada.

No copies todo el detalle de la feature en Memory.

## Revisión final

Antes de dar por terminada la tarea:

1. Revisá el diff completo.
2. Verificá que no haya dependencias incorrectas entre capas.
3. Revisá que Domain no tenga dependencias de infraestructura.
4. Revisá que la idempotencia dependa finalmente de una garantía de base de datos.
5. Revisá la concurrencia del unread count y LastMessage.
6. Revisá que ACK ocurra después del commit.
7. Revisá que no exista requeue infinito.
8. Revisá que no se hayan agregado funcionalidades fuera de alcance.
9. Revisá secretos y configuración.
10. Confirmá que Memory y README reflejen la implementación real.

En tu respuesta final informá:

- Qué implementaste.
- Modelo de dominio creado.
- Modelo de base de datos y migración.
- Estrategia de idempotencia.
- Estrategia de concurrencia.
- Estrategia ACK/NACK/DLQ.
- Archivos principales modificados.
- Tests ejecutados y resultados.
- Validaciones Docker/manuales realizadas.
- Problemas o limitaciones pendientes.
- Próxima feature lógica.

No des FEAT-003 por completada únicamente porque el código compile.