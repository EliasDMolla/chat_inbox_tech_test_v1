# EncodeLabs Inbox

Prueba técnica de una bandeja conversacional para Telegram.

## Estado actual

FEAT-007 incorpora la bandeja Angular de dos paneles sobre las APIs existentes. Permite listar conversaciones, abrir el historial, marcar mensajes como leídos y responder por Telegram. Los cambios confirmados se reflejan mediante SignalR sin recargar el sidebar ni el historial completo.

Los updates repetidos son idempotentes mediante `TelegramUpdateId`; no duplican mensajes, no incrementan nuevamente los no leídos y no emiten eventos SignalR. PostgreSQL sigue siendo la fuente de verdad: tras una reconexión, Angular vuelve a consultar conversaciones y el historial abierto para recuperar cualquier evento perdido.

## Requisitos

- Docker con Docker Compose.
- Bot de Telegram creado con [BotFather](https://t.me/BotFather) y su token.
- Token de ngrok para crear el túnel público.
- Para desarrollo local sin Docker: .NET SDK 10 y una versión de Node compatible con Angular 22 (`22.22.3`, `24.15.0` o superior dentro de esas líneas soportadas).

## Inicio

1. Crear el archivo local de variables:

```bash
cp .env.example .env
```

2. Completar `TELEGRAM_BOT_TOKEN`, `NGROK_AUTHTOKEN`, `POSTGRES_PASSWORD` y `RABBITMQ_DEFAULT_PASS` en `.env`. No subir este archivo al repositorio.
3. Iniciar la solución:

```bash
docker compose up --build
```

Al iniciar, la API aplica las migraciones de PostgreSQL, declara la topología RabbitMQ, inicia el consumer y registra automáticamente `{URL_PUBLICA}/api/telegram/webhook` mediante `setWebhook`. Los reintentos son limitados y un fallo de registro queda informado sin detener la API.

## URLs

- Frontend: http://localhost:4200
- API: http://localhost:8080
- Health check: http://localhost:8080/health
- Hub SignalR: http://localhost:8080/hubs/conversations
- RabbitMQ Management: http://localhost:15672
- ngrok UI/API: http://localhost:4040

## Uso de la bandeja Angular

Abrir http://localhost:4200 después de iniciar Compose. El sidebar carga las conversaciones en el orden entregado por la API y muestra último mensaje, fecha y cantidad de no leídos. No se selecciona una conversación automáticamente: al elegir una se carga su historial y se marcan como leídos los inbound pendientes sin bloquear el resto de la interfaz.

El composer acepta texto de hasta 4096 caracteres. `Enter` envía, `Shift+Enter` agrega una línea y el botón permanece deshabilitado para texto vacío o mientras hay un envío pendiente. La respuesta HTTP muestra el outbound inmediatamente; si también llega `MessageSent`, se deduplica por ID.

La interfaz muestra por separado los estados de carga del sidebar, historial y envío, además de empty states y errores legibles. Un indicador en el panel central informa el estado de SignalR. Si la conexión se recupera, el cliente rehidrata el sidebar y la conversación abierta desde la API porque SignalR no reproduce eventos históricos.

Sin `TELEGRAM_BOT_TOKEN`, el listado, historial y lectura siguen disponibles, pero el envío devuelve `503` y la UI muestra `Telegram no está disponible temporalmente.` Un rechazo explícito de Telegram (`502`) muestra `No se pudo enviar el mensaje a Telegram.`

## Persistencia e idempotencia

El esquema inicial contiene:

- `contacts`, único por `TelegramUserId`.
- `conversations`, única por `TelegramChatId` y asociada a un contacto; mantiene `UnreadCount`, `LastMessage` y `LastMessageAt`.
- `messages`, con índices únicos para `TelegramUpdateId` y `(ConversationId, TelegramMessageId)`. `TelegramUpdateId` es obligatorio conceptualmente para inbound y nullable para outbound, porque Telegram no devuelve un update ID al usar `sendMessage`.

La migración se aplica automáticamente al iniciar la API. También puede inspeccionarse o aplicarse manualmente con una connection string configurada:

```bash
dotnet ef migrations list --project src/EncodeLabs.Inbox.Infrastructure --startup-project src/EncodeLabs.Inbox.Api
dotnet ef database update --project src/EncodeLabs.Inbox.Infrastructure --startup-project src/EncodeLabs.Inbox.Api
```

## Verificación de FEAT-003

1. Confirmar en http://localhost:4040 que ngrok creó un túnel HTTPS hacia la API.
2. Revisar los logs de `api` y confirmar la migración, la declaración de la topología y el inicio del consumer.
3. En http://localhost:15672, verificar el exchange `telegram.incoming`, la cola `telegram.incoming.messages` y la DLQ `telegram.incoming.messages.dlq`.
4. Enviar un mensaje de texto al bot. Debe desaparecer de la cola principal después del ACK y crear un contacto, una conversación y un mensaje.
5. Republicar exactamente el mismo update. Debe seguir existiendo un solo mensaje y `UnreadCount` no debe volver a incrementarse.
6. Enviar otro texto para el mismo chat. Debe existir un segundo mensaje, `UnreadCount` debe valer `2` y `LastMessage` debe contener el segundo texto.
7. Publicar un payload inválido directamente en la cola para verificar que sea rechazado hacia `telegram.incoming.messages.dlq`.

Para inspeccionar PostgreSQL desde el contenedor:

```bash
docker compose exec postgres sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

Si RabbitMQ no puede aceptar un mensaje de texto soportado, el endpoint devuelve error; no informa un éxito falso a Telegram.

## API de conversaciones

- `GET /api/conversations`: devuelve el sidebar ordenado por actividad descendente.
- `GET /api/conversations/{conversationId}/messages`: devuelve el historial cronológico completo o `404` si la conversación no existe.
- `POST /api/conversations/{conversationId}/read`: marca únicamente mensajes inbound pendientes, pone `UnreadCount` en cero y devuelve `204`; devuelve `404` para una conversación inexistente.

Ejemplos:

```bash
curl http://localhost:8080/api/conversations
curl http://localhost:8080/api/conversations/CONVERSATION_ID/messages
curl -X POST http://localhost:8080/api/conversations/CONVERSATION_ID/read
```

El marcado como leído es idempotente. Mensajes y contador se actualizan dentro de una transacción protegida por el mismo advisory lock por chat que utiliza el procesamiento entrante. Esto evita perder un unread si llega un mensaje mientras se abre la conversación.

## Envío de mensajes a Telegram

`POST /api/conversations/{conversationId}/messages` acepta un texto no vacío de hasta 4096 caracteres:

```bash
curl -X POST http://localhost:8080/api/conversations/CONVERSATION_ID/messages \
  -H "Content-Type: application/json" \
  -d '{"text":"Respuesta desde EncodeLabs Inbox"}'
```

El flujo exacto es: obtener la conversación, adquirir el advisory lock `chat:{TelegramChatId}`, llamar a Telegram Bot API, recibir la confirmación, persistir un `Message` con dirección `Outbound`, actualizar `LastMessage`/`LastMessageAt`, hacer commit y emitir `MessageSent` seguido de `ConversationUpdated`. El mensaje outbound queda leído y no incrementa `UnreadCount`.

Un texto inválido devuelve `400`, una conversación inexistente `404`, un rechazo de Telegram `502` y una indisponibilidad o falta de configuración `503`. Los errores enviados al cliente no incluyen detalles sensibles y el bot token nunca se registra en logs. Si Telegram falla, no se crea el mensaje ni se emite SignalR.

Existe un trade-off deliberado entre sistemas: Telegram puede aceptar el mensaje y PostgreSQL fallar después. En ese caso el usuario recibe el texto, pero la bandeja no lo persiste. Resolverlo requeriría estados de entrega, retries y reconciliación; no se incorporan transacciones distribuidas ni Outbox para este alcance.

## Tiempo real con SignalR

El Hub `/hubs/conversations` notifica a `Clients.All` estos eventos, siempre en este orden para cada mensaje nuevo:

1. `MessageReceived`, con `conversationId` y un mensaje compuesto por `id`, `direction`, `text`, `sentAt` e `isRead`.
2. `ConversationUpdated`, con `conversationId`, `contactId`, `displayName`, `username`, `lastMessageText`, `lastMessageAt` y `unreadCount`.

Para un mensaje saliente confirmado y persistido emite `MessageSent` con el mismo contrato compacto de mensaje y luego `ConversationUpdated`; el contador unread conserva su valor anterior.

Un marcado como leído que produce cambios emite, después del commit:

1. `MessagesRead`, con `conversationId` y los IDs afectados para actualizar el historial abierto.
2. `ConversationUpdated`, con `unreadCount = 0` para actualizar el sidebar.

Para probarlo, iniciar el stack, conectar un cliente SignalR al Hub y registrar ambos handlers. Luego enviar un texto al bot o publicar un `IncomingTelegramMessage` válido en `telegram.incoming.messages`. El cliente debe recibir los dos eventos después del commit; al republicar exactamente el mismo `UpdateId`, no debe recibir ninguno.

PostgreSQL es la fuente de verdad. SignalR no almacena ni reproduce eventos: luego de una desconexión, el cliente debe recuperar el sidebar con `GET /api/conversations` y el historial seleccionado con `GET /api/conversations/{conversationId}/messages`. Si una notificación falla después del commit, el error se registra sin revertir el estado persistido. No se implementa Outbox en el alcance actual, por lo que una notificación puede perderse y un fallo entre dos eventos puede producir una entrega parcial.

El origen permitido por CORS se configura en `Cors:AllowedOrigins` y por defecto incluye `http://localhost:4200`. La aplicación Angular mantiene una sola conexión SignalR con reconexión automática. No hay autenticación, grupos ni scopes multioperador.

## Prueba end-to-end de la bandeja

1. Ejecutar `docker compose up --build` y abrir http://localhost:4200.
2. Enviar un texto al bot o publicar un `IncomingTelegramMessage` válido en RabbitMQ. Verificar que la conversación aparezca o suba al primer lugar, actualice preview/unread y muestre el mensaje sin refresh.
3. Abrir la conversación. Verificar que se cargue el historial y que el badge unread pase a cero.
4. Responder desde el composer. Con token real debe aparecer un único outbound y llegar a Telegram; sin token debe verse el error `503` y no debe agregarse un mensaje falso.
5. Detener temporalmente la API. Verificar el indicador de desconexión, volver a iniciar la API y confirmar que la reconexión rehidrata conversaciones e historial desde PostgreSQL.

El alcance no incluye responsive/mobile, paginación, multimedia, búsqueda, autenticación, edición/eliminación ni estados de entrega.

## Estructura

```text
src/
  EncodeLabs.Inbox.Domain
  EncodeLabs.Inbox.Application
  EncodeLabs.Inbox.Infrastructure
  EncodeLabs.Inbox.Api
tests/
  EncodeLabs.Inbox.Domain.Tests
web/
```

Las dependencias del backend apuntan hacia el dominio, de acuerdo con Clean Architecture y los ADRs del proyecto.

Para simplificar la ejecución de la prueba técnica, la API aplica automáticamente
las migraciones pendientes al iniciar. En un entorno productivo se evaluaría ejecutar
las migraciones como una etapa independiente del deployment.
