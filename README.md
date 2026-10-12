# EncodeLabs Inbox

Prueba técnica de una bandeja conversacional para Telegram.

## Estado actual

FEAT-003 implementa el procesamiento completo de mensajes de texto entrantes: el webhook publica en RabbitMQ, un consumer crea o actualiza `Contact`, `Conversation` y `Message` en PostgreSQL dentro de una transacción y recién entonces confirma el mensaje al broker.

Los updates repetidos son idempotentes mediante `TelegramUpdateId`; no duplican mensajes ni incrementan nuevamente los no leídos. SignalR funcional y la bandeja conversacional todavía no están implementados.

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