# EncodeLabs Inbox

Prueba técnica de una bandeja conversacional para Telegram.

## Estado actual

FEAT-002 implementa la recepción de mensajes de texto de Telegram en `POST /api/telegram/webhook`, su validación mínima y publicación asíncrona en RabbitMQ. Los updates no soportados se aceptan sin publicar y el webhook se registra automáticamente con la URL HTTPS de ngrok.

El consumer, la persistencia, SignalR funcional y la bandeja conversacional todavía no están implementados.

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

Al iniciar, la API espera el túnel HTTPS de ngrok y registra automáticamente `{URL_PUBLICA}/api/telegram/webhook` mediante `setWebhook`. Los reintentos son limitados y un fallo queda registrado sin detener la API.

## URLs

- Frontend: http://localhost:4200
- API: http://localhost:8080
- Health check: http://localhost:8080/health
- RabbitMQ Management: http://localhost:15672
- ngrok UI/API: http://localhost:4040

## Verificación de FEAT-002

1. Confirmar en http://localhost:4040 que ngrok creó un túnel HTTPS hacia la API.
2. Revisar los logs de `api` y confirmar el registro del webhook y la declaración de la topología RabbitMQ.
3. En http://localhost:15672, verificar el exchange `telegram.incoming`, la cola `telegram.incoming.messages` y la DLQ `telegram.incoming.messages.dlq`.
4. Enviar un mensaje de texto al bot. Debe quedar pendiente en `telegram.incoming.messages`, porque el consumer corresponde a FEAT-003.
5. Enviar un sticker o una imagen. No debe agregarse un mensaje a esa cola.

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
