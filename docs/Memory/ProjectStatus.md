# Estado del proyecto

## FEAT-001 — Bootstrap inicial

- **Fecha:** 2026-10-12.
- **Estado:** completada.
- **Implementado:** solución .NET 10 con proyectos Domain, Application, Infrastructure y Api; tests de dominio; Angular 22 standalone con routing y página inicial; configuración base de EF Core/PostgreSQL, RabbitMQ, SignalR, Serilog y `/health`; Dockerfiles y Compose para PostgreSQL, RabbitMQ, API, frontend y ngrok.
- **Decisiones técnicas:** composición de dependencias de persistencia y mensajería centralizada en Infrastructure; health check básico desacoplado de servicios externos; frontend servido por nginx; URL de API centralizada en `environment.ts`; sin lógica funcional ni migraciones anticipadas.
- **Módulos principales:** `src/`, `tests/`, `web/`, `docker-compose.yml`, `.env.example`, `README.md`.
- **Validaciones:** `dotnet restore`, `dotnet build` (0 warnings/errores), `dotnet test` (1/1), `dotnet format --verify-no-changes`, `npm install` (0 vulnerabilidades), build Angular, tests Angular (2/2), `docker compose config`, revisión de dependencias vulnerables y respuesta `200 Healthy` de `/health`.
- **Limitaciones pendientes:** no existe aún flujo funcional de Telegram/RabbitMQ/SignalR ni modelo de conversaciones. No se ejecutó `docker compose up --build` porque el Docker daemon local no estaba disponible; la configuración de Compose sí quedó validada. El Node local 22.14 no satisface Angular 22, por lo que las validaciones se ejecutaron con Node 24.15 y el Dockerfile usa Node 24.
- **Próxima feature recomendada:** FEAT-002, endpoint de webhook de Telegram con validación mínima y publicación en RabbitMQ, según el índice y ADR-003.

## FEAT-002 — Telegram Webhook

- **Fecha:** 2026-10-12.
- **Estado:** completada.
- **Implementado:** `POST /api/telegram/webhook` con validación mínima de mensajes de texto; contrato normalizado y caso de uso en Application; publicación persistente en RabbitMQ; exchange, cola principal y DLQ durables; registro automático del webhook con la URL HTTPS de ngrok y reintentos limitados.
- **Decisiones técnicas:** Application no referencia RabbitMQ ni una librería de Telegram; Infrastructure reutiliza una conexión RabbitMQ y usa un channel por publicación; la topología se declara al iniciar y también idempotentemente antes de publicar; los fallos finales de ngrok/Telegram quedan en logs sin detener la API y nunca se registra el bot token.
- **Módulos principales:** `EncodeLabs.Inbox.Application/Telegram`, controller de webhook en Api, mensajería y registro de webhook en Infrastructure, Compose, tests de Application y `README.md`.
- **Validaciones:** restore y build .NET (0 warnings/errores), 6 tests (5 Application + 1 arquitectura), `dotnet format --verify-no-changes`, `docker compose config`, build Docker de API y Angular, health/API/frontend 200, topología inspeccionada en RabbitMQ, mensaje de texto publicado una vez y update no soportado ignorado.
- **Prueba manual:** stack levantado con credenciales efímeras; PostgreSQL y RabbitMQ saludables, API y frontend disponibles; la cola principal quedó con 1 mensaje y la DLQ con 0 luego de probar texto + sticker simulado.
- **Limitaciones pendientes:** no se validó Telegram → ngrok real porque el `.env` local no contiene tokens/contraseñas; ngrok salió por falta de credenciales. Consumer, persistencia, idempotencia en base de datos y SignalR siguen fuera de alcance de esta feature.
- **Próxima feature recomendada:** FEAT-003, consumer RabbitMQ para procesar y persistir mensajes entrantes de forma idempotente antes de notificar por SignalR.
