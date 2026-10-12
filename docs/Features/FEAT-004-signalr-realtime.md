Implementá `Docs/Features/FEAT-004-signalr-realtime.md`.

Antes de modificar código:

1. Leé `AGENTS.md`.
2. Leé `Docs/Memory/ProjectStatus.md`.
3. Leé completo `Docs/Adrs/ADR-002.md`.
4. Revisá `Docs/Adrs/ADR-003.md` porque FEAT-004 se conecta al procesamiento ya implementado.
5. Leé completa la feature FEAT-004.
6. Inspeccioná la implementación actual resultante de FEAT-001, FEAT-002 y FEAT-003.
7. Revisá especialmente cómo termina actualmente el caso de uso de procesamiento de mensajes entrantes y dónde ocurre el commit.

Implementá únicamente el alcance definido en FEAT-004.

Objetivo principal:

```text
RabbitMQ Consumer
→ Application
→ PostgreSQL
→ Commit exitoso
→ SignalR
→ clientes conectados
```

PostgreSQL debe seguir siendo la fuente de verdad.

SignalR se utiliza solamente para notificar cambios ocurridos.

No implementes todavía:
- listado de conversaciones;
- historial por API;
- mark-as-read;
- envío de mensajes salientes;
- frontend completo del inbox.

## Regla principal

Nunca emitir un evento SignalR antes de que la transacción de base de datos haya finalizado correctamente.

El flujo correcto debe ser:

```text
Procesar mensaje
→ Persistir
→ Commit
→ Construir DTO realtime
→ Emitir SignalR
→ ACK RabbitMQ
```

Revisá cuidadosamente cómo se relaciona esto con el ACK actual de RabbitMQ.

La emisión SignalR NO debe romper la consistencia de persistencia.

Si la base de datos falla:
- no emitir SignalR;
- no ACKear como éxito.

Si SignalR falla después del commit:
- no revertir la persistencia;
- no convertir SignalR en fuente de verdad;
- manejar el error de forma explícita y documentada.

Evitá que un fallo de notificación genere duplicados de dominio al reintentar innecesariamente el mensaje ya persistido.

## Clean Architecture

Mantener las dependencias actuales.

Application no debe depender de SignalR ni ASP.NET Core.

Definir una abstracción de tiempo real en Application, por ejemplo:

```csharp
public interface IRealtimeNotifier
{
    Task NotifyMessageReceivedAsync(
        MessageReceivedRealtimeDto message,
        CancellationToken cancellationToken);

    Task NotifyConversationUpdatedAsync(
        ConversationUpdatedRealtimeDto conversation,
        CancellationToken cancellationToken);
}
```

Los nombres pueden adaptarse a las convenciones existentes.

Infrastructure o Api debe contener la implementación concreta mediante SignalR.

No usar `IHubContext` directamente dentro de Application.

No introducir dependencias ASP.NET Core en Domain/Application.

## SignalR Hub

Crear un hub simple, por ejemplo:

```text
/hubs/conversations
```

El Hub no debe contener lógica de negocio.

Debe servir como punto de conexión para el frontend.

Registrar correctamente el endpoint en la API.

No implementar métodos bidireccionales innecesarios desde el cliente si no son requeridos.

## Eventos

Definir eventos mínimos y claros.

Como mínimo evaluar:

```text
MessageReceived
ConversationUpdated
```

Evitar emitir múltiples eventos redundantes si un solo contrato puede resolver correctamente la actualización.

La decisión debe priorizar simplicidad y claridad.

### MessageReceived

Debe incluir únicamente la información necesaria para agregar el mensaje al historial en el frontend.

Ejemplo conceptual:

```json
{
  "conversationId": "...",
  "message": {
    "id": "...",
    "direction": "Inbound",
    "text": "Hola",
    "sentAt": "...",
    "isRead": false
  }
}
```

### ConversationUpdated

Debe incluir datos suficientes para actualizar el sidebar sin volver a consultar toda la API.

Ejemplo conceptual:

```json
{
  "conversationId": "...",
  "contactId": "...",
  "displayName": "Juan Pérez",
  "username": "juanp",
  "lastMessageText": "Hola",
  "lastMessageAt": "...",
  "unreadCount": 2
}
```

No enviar entidades EF Core directamente.

No enviar objetos de dominio completos.

Usar DTOs explícitos.

## Orden de eventos

Si se emiten dos eventos por un mismo mensaje:

```text
MessageReceived
ConversationUpdated
```

mantener un orden consistente.

Documentar el orden esperado.

No asumir que SignalR provee persistencia de eventos.

El frontend futuro debe poder recuperarse consultando PostgreSQL mediante la API.

## Integración con FEAT-003

Reutilizar el caso de uso existente.

No duplicar procesamiento.

No volver a consultar innecesariamente toda la conversación desde base de datos si el estado necesario ya está disponible luego del procesamiento.

Idealmente el caso de uso de FEAT-003 debe devolver un resultado explícito con la información necesaria para notificar.

Ejemplo conceptual:

```csharp
IncomingMessageProcessResult
{
    bool WasDuplicate,
    MessageDto? Message,
    ConversationSummaryDto? Conversation
}
```

Si actualmente devuelve `void`, evaluá el cambio mínimo necesario.

No acoplar la lógica del consumer a detalles internos de EF Core.

## Duplicados

Un TelegramUpdateId duplicado ya procesado no debe generar nuevamente eventos SignalR.

El comportamiento esperado es:

```text
Update duplicado
→ idempotencia detectada
→ no cambia PostgreSQL
→ no SignalR
→ ACK
```

Verificar explícitamente este caso.

## Fallos SignalR

Este punto requiere criterio.

La persistencia y la notificación realtime son dos operaciones diferentes.

PostgreSQL es la fuente de verdad.

Si SignalR falla después del commit:

- registrar el fallo;
- no eliminar el mensaje persistido;
- no modificar el estado del dominio para fingir que falló la recepción;
- evitar reintentar el procesamiento completo si eso puede duplicar eventos o efectos.

Mantener la implementación proporcional a la prueba.

NO implementar Outbox Pattern en esta feature salvo que exista una razón técnica fuerte y documentada.

Podés documentar Outbox como mejora futura.

## Clientes conectados

Para esta prueba no existe autenticación ni múltiples operadores.

Puede notificarse a:

```text
Clients.All
```

si es la opción más simple y coherente.

No crear grupos, tenants o usuarios SignalR artificiales.

Documentar que en un escenario multioperador/multitenant se utilizarían grupos o scopes.

## CORS

Verificar que Angular pueda conectarse al Hub.

Configurar CORS correctamente para el origen utilizado por el frontend.

No usar configuraciones excesivamente permisivas si puede evitarse.

Contemplar credenciales únicamente si SignalR las requiere según la configuración elegida.

## Reconexión

La lógica de reconexión del cliente Angular no pertenece necesariamente a esta feature si el frontend funcional aún está fuera de alcance.

Sin embargo, dejar el backend compatible con reconexiones normales.

No almacenar estado de cliente en memoria dentro del Hub.

## Logging

Agregar logging estructurado para eventos importantes.

Ejemplos:

```text
SignalR MessageReceived emitido
SignalR ConversationUpdated emitido
Notificación omitida por update duplicado
Error notificando SignalR
```

Contexto útil:

- TelegramUpdateId
- ConversationId
- MessageId

No loggear secretos.

No loggear el contenido completo del mensaje salvo necesidad concreta.

## Tests

Agregar tests con valor real.

Como mínimo cubrir:

- Un mensaje nuevo genera resultado notificable.
- Un duplicado no genera notificación.
- Si el procesamiento falla antes del commit, no se invoca realtime.
- El DTO de MessageReceived contiene los datos esperados.
- El DTO de ConversationUpdated refleja unread count y último mensaje correctos.

Si la implementación permite mockear `IRealtimeNotifier`, agregar un test de Application para verificar:

```text
nuevo mensaje → notifier llamado
duplicado → notifier NO llamado
```

No testear internals del framework SignalR.

No generar tests que solamente validen que Microsoft SignalR funciona.

## Frontend mínimo de prueba

Si es necesario para validar SignalR end-to-end, se permite agregar una integración mínima y temporal en Angular, siempre que no se convierta todavía en la bandeja funcional.

Por ejemplo:
- conectar al Hub;
- registrar eventos en consola;
- mostrar un pequeño indicador de conexión.

Pero preferentemente no anticipar FEAT-007.

Si se agrega cualquier código frontend, debe quedar limpio y reutilizable para la futura bandeja.

## Docker

Verificar que la URL del Hub funcione correctamente desde el navegador cuando frontend y API están ejecutándose mediante Docker Compose.

Revisar:
- puertos;
- CORS;
- nginx/proxy si existe;
- configuración de API URL.

No hardcodear URLs internas de Docker en Angular.

## Validación manual

Si Docker está disponible:

1. Levantar:

```bash
docker compose up --build
```

2. Conectarse al Hub SignalR con un cliente de prueba o con una integración mínima del frontend.

3. Publicar un mensaje válido en:

```text
telegram.incoming.messages
```

o utilizar el webhook existente.

4. Verificar:

```text
RabbitMQ
→ Consumer
→ PostgreSQL
→ Commit
→ SignalR
```

5. Confirmar que se recibe:

```text
MessageReceived
ConversationUpdated
```

6. Verificar PostgreSQL:
- mensaje persistido;
- unread correcto;
- last message correcto.

7. Republicar exactamente el mismo update.

Verificar:

```text
DB sin cambios
+
ningún evento SignalR nuevo
```

8. Si es posible, simular desconexión del cliente.

Reconectar y documentar que SignalR no reproduce eventos históricos.

Esto es esperado porque PostgreSQL es la fuente de verdad.

## Validaciones obligatorias

Ejecutar:

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
```

Resultado esperado:

```text
0 errores
0 warnings relevantes
tests OK
```

Ejecutar:

```bash
docker compose config
```

Si Docker está disponible:

```bash
docker compose up --build
```

y validar manualmente el flujo realtime.

Si se modifica Angular:

```bash
npm install
npm run build
npm test
```

según los scripts disponibles.

## README

Actualizar únicamente lo correspondiente a FEAT-004.

Agregar:
- endpoint del Hub;
- eventos emitidos;
- cómo probar realtime;
- aclaración de que PostgreSQL es la fuente de verdad;
- comportamiento ante reconexión;
- limitaciones actuales.

No documentar todavía endpoints de conversaciones o envío saliente si no existen.

## Memory

Actualizar obligatoriamente:

`Docs/Memory/ProjectStatus.md`

Agregar:

`FEAT-004 — Notificaciones en tiempo real con SignalR`

Registrar brevemente:

- Fecha.
- Estado.
- Qué quedó implementado.
- Eventos definidos.
- DTOs realtime.
- Punto exacto donde se emiten respecto al commit.
- Comportamiento de duplicados.
- Decisión ante fallos SignalR.
- Validaciones ejecutadas.
- Prueba manual realizada.
- Limitaciones pendientes.
- Próxima feature recomendada.

Mantener Memory breve y operativo.

## Revisión final

Antes de dar por terminada FEAT-004:

1. Revisá el diff completo.
2. Confirmá que Application no referencia SignalR/ASP.NET Core.
3. Confirmá que el Hub no contiene lógica de negocio.
4. Confirmá que ningún evento se emite antes del commit.
5. Confirmá que updates duplicados no emiten eventos.
6. Confirmá que SignalR no se trata como fuente de verdad.
7. Revisá el comportamiento si SignalR falla después del commit.
8. Confirmá que no se introdujo Outbox u otra complejidad no necesaria.
9. Confirmá que no se implementaron features futuras.
10. Confirmá que README y Memory reflejan el comportamiento real.

En tu respuesta final informá:

- Qué implementaste.
- Qué Hub y endpoint quedaron disponibles.
- Qué eventos SignalR existen.
- Qué DTOs se utilizan.
- En qué punto del flujo se emiten.
- Cómo se comportan los duplicados.
- Qué ocurre si SignalR falla.
- Archivos principales modificados.
- Tests ejecutados y resultados.
- Validación Docker/end-to-end realizada.
- Limitaciones pendientes.
- Próxima feature recomendada.

No des FEAT-004 por completada únicamente porque compile.