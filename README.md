# EncodeLabs Inbox

Bandeja conversacional para recibir mensajes de texto de Telegram, gestionarlos desde una interfaz web y responder al contacto. PostgreSQL conserva el estado; RabbitMQ desacopla la recepción y SignalR actualiza la UI en tiempo real.

## Arquitectura

La solución usa Clean Architecture (`Domain`, `Application`, `Infrastructure`, `Api`) y dos flujos deliberadamente distintos:

```text
Incoming: Telegram → ngrok → API/Webhook → RabbitMQ → Consumer → PostgreSQL → SignalR → Angular
Outbound: Angular → API → Telegram Bot API → PostgreSQL → SignalR → Angular
```

RabbitMQ declara el exchange durable `telegram.incoming`, la routing key `telegram.message.text`, la cola durable `telegram.incoming.messages` y la DLQ `telegram.incoming.messages.dlq`. Un fallo inesperado se reintenta una vez; un payload inválido o un segundo fallo termina en DLQ. El ACK ocurre después del procesamiento exitoso.

## Requisitos previos

- Docker con Docker Compose.
- Un bot creado con [BotFather](https://t.me/BotFather) y su token.
- Una cuenta de [ngrok](https://dashboard.ngrok.com/get-started/your-authtoken) y su authtoken.

No hace falta instalar .NET, Node, PostgreSQL, RabbitMQ ni ngrok localmente.

## Configuración

Crear la configuración local:

```bash
cp .env.example .env
```

En PowerShell puede usarse `Copy-Item .env.example .env`. Completar en `.env`:

- `TELEGRAM_BOT_TOKEN`: crear el bot con `/newbot` en BotFather y copiar el token.
- `NGROK_AUTHTOKEN`: copiar el authtoken del dashboard de ngrok.
- `POSTGRES_PASSWORD` y `RABBITMQ_DEFAULT_PASS`: definir contraseñas locales.
- `POSTGRES_DB`, `POSTGRES_USER` y `RABBITMQ_DEFAULT_USER`: pueden conservar sus valores de ejemplo.

`.env` está ignorado por Git y no debe versionarse.

## Ejecución

```bash
docker compose up --build
```

La API espera PostgreSQL y RabbitMQ, aplica migraciones, inicia el consumer y obtiene la URL HTTPS de ngrok para registrar automáticamente `/api/telegram/webhook` mediante `setWebhook`.

URLs locales:

- Frontend: http://localhost:4200
- API y health: http://localhost:8080 y http://localhost:8080/health
- SignalR: http://localhost:8080/hubs/conversations
- RabbitMQ Management: http://localhost:15672
- ngrok inspector: http://localhost:4040

## Prueba punta a punta

1. Levantar el stack y esperar el mensaje de registro exitoso del webhook en los logs de `api`.
2. Abrir http://localhost:4200.
3. Enviar un mensaje de texto al bot desde Telegram.
4. Verificar que la conversación aparezca sin refrescar y muestre preview y unread.
5. Abrirla; el historial debe cargar y el unread pasar a cero.
6. Responder desde el composer y confirmar que el mensaje llegue a Telegram una sola vez.
7. Enviar otro texto al bot y comprobar la actualización realtime.

PostgreSQL es la fuente de verdad. SignalR es best effort y no reproduce eventos: después de reconectar, Angular rehidrata el sidebar y el historial abierto desde la API.

## Supuestos y decisiones

- Existe un único operador; por eso SignalR usa `Clients.All` y no hay autenticación ni grupos.
- Cada contacto tiene una conversación; se soportan únicamente mensajes de texto.
- No hay búsqueda ni paginación por el volumen esperado de la prueba.
- Los updates inbound se deduplican por `TelegramUpdateId`, con una restricción única como última barrera.
- Incoming, lectura y outbound usan el mismo advisory lock `chat:{TelegramChatId}`.
- El outbound es síncrono y no pasa por RabbitMQ. Telegram confirma antes de persistir; si PostgreSQL falla después, el mensaje puede haber llegado sin quedar registrado. No se agregó Outbox ni reconciliación.
- Las migraciones se aplican al iniciar por simplicidad. En producción convendría ejecutarlas como una etapa separada del deployment.

## Tests

Para validación local con los SDK instalados:

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes

cd web
npm install
npm audit --omit=dev
npm run build
npm test
```

También puede validarse Compose con `docker compose config`.

## Limitaciones

No incluye autenticación, múltiples operadores, responsive mobile, multimedia, búsqueda, paginación, edición/eliminación, delivery receipts ni retries persistentes. SignalR no usa Outbox y puede perder una notificación durante una caída; la rehidratación posterior recupera el estado persistido.

**Caché.** No se incorpora porque PostgreSQL y SignalR son suficientes para este alcance. A mayor volumen, Redis podría cachear resúmenes o metadata de lectura frecuente, asumiendo el costo de invalidación; con varias instancias también podría actuar como backplane de SignalR.

**Multimedia.** Los binarios no deberían circular por RabbitMQ. El broker transportaría metadata y referencias; un worker descargaría el archivo a object storage (S3, Azure Blob o MinIO) y PostgreSQL conservaría metadata y ubicación.

## Documentación

- [Índice documental](Docs/Index.md)
- [C4 nivel 1](Docs/C4/C4-Level-1.svg) y [C4 nivel 2](Docs/C4/C4-Level-2.svg)
- [ADRs](Docs/ADRs/ADR-001.md)
- [Uso de IA](Docs/AI-USAGE.md)
- [Estado final](Docs/Memory/ProjectStatus.md)
