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

## FEAT-003 — Procesamiento y persistencia de mensajes entrantes

- **Fecha:** 2026-10-12.
- **Estado:** completada.
- **Implementado:** entidades `Contact`, `Conversation` y `Message`; caso de uso de procesamiento; persistencia transaccional EF Core/PostgreSQL; migración inicial; consumer de `telegram.incoming.messages` con ACK posterior al commit y aplicación automática de migraciones al iniciar.
- **Decisiones técnicas:** el dominio mantiene unread y último mensaje; Infrastructure serializa escrituras por usuario/chat mediante advisory locks transaccionales de PostgreSQL; `TelegramUpdateId` único es la última barrera de idempotencia; un duplicado se considera éxito; payloads inválidos van directo a DLQ y fallos inesperados tienen un único requeue antes de DLQ.
- **Modelo e índices:** contactos únicos por `TelegramUserId`; conversaciones únicas por `TelegramChatId` y `ContactId`; mensajes únicos por `TelegramUpdateId` y por `(ConversationId, TelegramMessageId)`, con índice adicional por conversación/fecha.
- **Módulos principales:** entidades en Domain, `Application/IncomingMessages`, persistencia/migraciones y consumer en Infrastructure, registro de dependencias, tests, `README.md`.
- **Validaciones:** restore y build .NET sin warnings/errores; 16/16 tests; formato; migración descubierta por EF; Compose válido; imágenes API/frontend construidas; revisión de paquetes sin vulnerabilidades conocidas.
- **Prueba manual:** stack Docker saludable con credenciales efímeras; tres publicaciones (dos updates distintos y una repetición) produjeron 1 contacto, 1 conversación, 2 mensajes, `UnreadCount = 2` y último mensaje correcto; cola principal quedó vacía tras ACK; el duplicado fue ignorado; un payload inválido terminó en DLQ; restricción única verificada directamente en PostgreSQL.
- **Limitaciones pendientes:** no se probó Telegram/ngrok real por falta de tokens locales. SignalR, consultas de conversaciones/historial, mark-as-read y mensajes salientes continúan fuera de alcance. La prueba local requiere completar las contraseñas vacías del `.env`.
- **Próxima feature recomendada:** FEAT-004, incorporar notificaciones SignalR posteriores al commit sin cambiar PostgreSQL como fuente de verdad.

## FEAT-004 — Notificaciones en tiempo real con SignalR

- **Fecha:** 2026-10-12.
- **Estado:** completada.
- **Implementado:** Hub `/hubs/conversations`, CORS configurable para Angular, abstracción `IRealtimeNotifier` en Application e implementación SignalR en Api. El caso de uso emite a `Clients.All` después del commit y antes del ACK de RabbitMQ.
- **Eventos y DTOs:** `MessageReceived` usa `MessageReceivedRealtimeDto`/`RealtimeMessageDto`; luego `ConversationUpdated` usa `ConversationUpdatedRealtimeDto`. Los payloads contienen solamente el mensaje y el resumen necesarios para actualizar una futura UI.
- **Duplicados y fallos:** un `TelegramUpdateId` duplicado no modifica PostgreSQL ni notifica y se ACKea. Si SignalR falla luego del commit, se registra el error y se ACKea para no reprocesar dominio persistido; no se revierte la transacción ni se implementa Outbox.
- **Validaciones:** restore/build .NET sin warnings/errores, 20/20 tests, formato sin cambios y `docker compose config` válido. Build Docker de API/frontend y stack aislado saludables.
- **Prueba manual:** un cliente SignalR real recibió `MessageReceived` seguido de `ConversationUpdated`; PostgreSQL quedó con 2 mensajes, `UnreadCount = 2` y el último mensaje correcto. Al republicar ambos updates no hubo eventos ni cambios duplicados. Una conexión nueva no recibió eventos históricos, como se esperaba.
- **Limitaciones pendientes:** SignalR es best effort; un fallo posterior al commit puede perder una notificación o dejar entrega parcial entre los dos eventos. No hay Outbox, autenticación, grupos ni cliente Angular. Telegram/ngrok real no se probó por falta de tokens locales.
- **Próxima feature recomendada:** API de consultas de conversaciones e historial y caso de uso mark-as-read, para que el cliente pueda hidratarse y recuperarse de reconexiones desde PostgreSQL.

## FEAT-005 — API de conversaciones, historial y lectura

- **Fecha:** 2026-10-12.
- **Estado:** completada.
- **Endpoints:** `GET /api/conversations`, `GET /api/conversations/{conversationId}/messages` y `POST /api/conversations/{conversationId}/read`, con `404` coherente y `204` idempotente para lectura.
- **DTOs y queries:** `ConversationSummaryDto` y `ConversationMessageDto`; proyecciones EF Core `AsNoTracking`, orden estable y una consulta sin N+1 para el sidebar. El historial usa una verificación de existencia y una consulta proyectada, sin exponer IDs internos de Telegram.
- **Lectura y concurrencia:** mensajes inbound unread y `Conversation.UnreadCount` se actualizan en una transacción. Se reutiliza el advisory lock `chat:{TelegramChatId}` de FEAT-003; según quién obtenga el lock, el mensaje concurrente queda correctamente leído o unread, sin perder el contador.
- **Realtime:** después del commit se emite `MessagesRead` y luego `ConversationUpdated`. Una segunda lectura sin cambios no renotifica. Los fallos SignalR se registran pero no revierten PostgreSQL ni cambian el éxito HTTP.
- **Validaciones:** restore/build sin warnings ni errores, 31/31 tests, formato sin cambios, Compose válido y build Docker de API/frontend correcto.
- **Prueba manual:** tres conversaciones creadas por RabbitMQ; sidebar e historiales verificados por HTTP; 404, lectura e idempotencia confirmados; un cliente SignalR recibió ambos eventos en orden. PostgreSQL confirmó inbound leídos, outbound sintético sin modificar y contador en cero. En una carrera controlada, la lectura ganó el lock y el mensaje posterior quedó unread, con `UnreadCount = 1` igual al único mensaje unread.
- **Limitaciones pendientes:** sin paginación, autenticación, búsqueda, UI Angular ni envío saliente. Realtime continúa siendo best effort y no usa Outbox. Telegram/ngrok real no se probó por falta de tokens locales.
- **Próxima feature recomendada:** envío de mensajes salientes mediante Telegram Bot API, con persistencia y notificaciones posteriores al éxito.
