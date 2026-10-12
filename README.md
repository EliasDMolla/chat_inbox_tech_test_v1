# EncodeLabs Inbox

Prueba técnica de una bandeja conversacional para Telegram.

## Estado actual

FEAT-005 agrega la API de conversaciones e historial y el marcado transaccional de mensajes como leídos. El flujo entrante continúa publicando en RabbitMQ, persistiendo en PostgreSQL y notificando mediante SignalR después del commit.

Los updates repetidos son idempotentes mediante `TelegramUpdateId`; no duplican mensajes, no incrementan nuevamente los no leídos y no emiten eventos SignalR. PostgreSQL es la fuente de verdad y la bandeja Angular todavía no está implementada.

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

## Persistencia e idempotencia

El esquema inicial contiene:

- `contacts`, único por `TelegramUserId`.
- `conversations`, única por `TelegramChatId` y asociada a un contacto; mantiene `UnreadCount`, `LastMessage` y `LastMessageAt`.
- `messages`, con índices únicos para `TelegramUpdateId` y `(ConversationId, TelegramMessageId)`.

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

## Tiempo real con SignalR

El Hub `/hubs/conversations` notifica a `Clients.All` estos eventos, siempre en este orden para cada mensaje nuevo:

1. `MessageReceived`, con `conversationId` y un mensaje compuesto por `id`, `direction`, `text`, `sentAt` e `isRead`.
2. `ConversationUpdated`, con `conversationId`, `contactId`, `displayName`, `username`, `lastMessageText`, `lastMessageAt` y `unreadCount`.

Un marcado como leído que produce cambios emite, después del commit:

1. `MessagesRead`, con `conversationId` y los IDs afectados para actualizar el historial abierto.
2. `ConversationUpdated`, con `unreadCount = 0` para actualizar el sidebar.

Para probarlo, iniciar el stack, conectar un cliente SignalR al Hub y registrar ambos handlers. Luego enviar un texto al bot o publicar un `IncomingTelegramMessage` válido en `telegram.incoming.messages`. El cliente debe recibir los dos eventos después del commit; al republicar exactamente el mismo `UpdateId`, no debe recibir ninguno.

PostgreSQL es la fuente de verdad. SignalR no almacena ni reproduce eventos: luego de una desconexión, el cliente debe recuperar el sidebar con `GET /api/conversations` y el historial seleccionado con `GET /api/conversations/{conversationId}/messages`. Si una notificación falla después del commit, el error se registra sin revertir el estado persistido. No se implementa Outbox en el alcance actual, por lo que una notificación puede perderse y un fallo entre dos eventos puede producir una entrega parcial.

El origen permitido por CORS se configura en `Cors:AllowedOrigins` y por defecto incluye `http://localhost:4200`. En esta etapa no hay autenticación, grupos, scopes multioperador ni cliente SignalR integrado en Angular.

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
