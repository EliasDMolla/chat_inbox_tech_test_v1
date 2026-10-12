# FEAT-002: Webhook de Telegram y publicación en RabbitMQ

## Objetivo

Implementar la recepción de updates de Telegram mediante webhook y publicar los mensajes de texto válidos en RabbitMQ para su procesamiento asíncrono posterior.

Esta feature debe cubrir únicamente:

```text
Telegram
→ Webhook HTTP
→ Validación mínima
→ RabbitMQ
→ Respuesta HTTP
```

Todavía NO debe implementar:

- Persistencia de contactos.
- Persistencia de conversaciones.
- Persistencia de mensajes.
- Consumer de RabbitMQ.
- SignalR funcional.
- Contadores de no leídos.
- Envío de respuestas hacia Telegram.

El objetivo es desacoplar correctamente la recepción del webhook del procesamiento posterior.

---

# Documentación obligatoria

Antes de implementar:

1. Leer `AGENTS.md`.
2. Leer `Docs/Memory/ProjectStatus.md`.
3. Leer `Docs/Adrs/ADR-003.md`.
4. Revisar `Docs/Adrs/ADR-001.md` y `Docs/Adrs/ADR-002.md` únicamente si alguna decisión de implementación pudiera afectarlos.
5. Inspeccionar la implementación existente de FEAT-001 antes de modificar archivos.

La implementación debe mantenerse alineada principalmente con ADR-003.

---

# Estado previo esperado

FEAT-001 debe haber dejado disponible:

- .NET 10.
- Clean Architecture.
- API ASP.NET Core.
- Infrastructure.
- RabbitMQ configurado a nivel de opciones.
- Docker Compose.
- ngrok.
- PostgreSQL.
- SignalR registrado.
- Serilog.
- `/health`.

No duplicar configuraciones existentes.

Reutilizar la infraestructura creada en FEAT-001.

---

# Flujo esperado

El flujo de esta feature debe ser:

```text
Usuario Telegram
      ↓
Telegram
      ↓
POST /api/telegram/webhook
      ↓
WebhookController
      ↓
Validación mínima
      ↓
Application
      ↓
IIncomingMessagePublisher
      ↓
RabbitMQ
      ↓
telegram.incoming.messages
      ↓
HTTP 200
```

El webhook NO debe ejecutar el procesamiento completo del mensaje.

---

# Endpoint

Crear:

```http
POST /api/telegram/webhook
```

Debe aceptar un update enviado por Telegram.

El endpoint debe:

1. Recibir el payload.
2. Detectar si contiene un mensaje soportado.
3. Ignorar correctamente updates fuera de alcance.
4. Publicar el update o mensaje normalizado en RabbitMQ.
5. Responder rápidamente a Telegram.

No acceder directamente a PostgreSQL desde este endpoint.

---

# Tipos de mensajes soportados

Para esta prueba solamente se soportan:

```text
Message
└── Text
```

Debe procesarse un update si contiene un mensaje de texto válido.

Ejemplo conceptual:

```text
update.Message != null
update.Message.Text != null
```

---

# Tipos de mensajes fuera de alcance

Ignorar sin error:

- Imágenes.
- Audio.
- Voice.
- Video.
- Stickers.
- Documentos.
- Animaciones.
- Ubicación.
- Contactos compartidos.
- Reacciones.
- Ediciones.
- Callbacks.
- Eventos de miembros del grupo.
- Cualquier update que no represente un mensaje de texto soportado.

Un update no soportado no debe producir un error 500.

Debe considerarse recibido correctamente y no publicarse en la cola de mensajes de texto.

---

# Modelo publicado en RabbitMQ

No publicar necesariamente el objeto completo de la librería de Telegram.

Crear un contrato propio y simple para desacoplar Infrastructure de Telegram.

Ejemplo conceptual:

```csharp
public sealed record IncomingTelegramMessage(
    long UpdateId,
    long MessageId,
    long ChatId,
    long TelegramUserId,
    string? Username,
    string? FirstName,
    string? LastName,
    string Text,
    DateTimeOffset SentAt);
```

El nombre final puede variar si existe una convención mejor en el proyecto.

El contrato debe contener únicamente información necesaria para procesar posteriormente:

- Telegram Update ID.
- Telegram Message ID.
- Chat ID.
- Telegram User ID.
- Username.
- First name.
- Last name.
- Texto.
- Fecha/hora original del mensaje.

No agregar información que no se utilizará.

---

# Separación de responsabilidades

## API

El controller debe encargarse únicamente de:

- Recibir HTTP.
- Delegar el procesamiento mínimo.
- Devolver una respuesta adecuada.

No debe contener:

- Configuración de RabbitMQ.
- Serialización manual compleja.
- Lógica de negocio.
- Creación de contactos.
- Acceso a EF Core.

---

## Application

Application debe definir los contratos necesarios para desacoplar la infraestructura.

Por ejemplo:

```text
IIncomingMessagePublisher
```

Posible contrato:

```csharp
Task PublishAsync(
    IncomingTelegramMessage message,
    CancellationToken cancellationToken);
```

No acoplar Application al cliente oficial de RabbitMQ.

No utilizar tipos de RabbitMQ dentro de Application.

---

## Infrastructure

Infrastructure debe implementar la publicación RabbitMQ.

Por ejemplo:

```text
RabbitMqIncomingMessagePublisher
```

Responsabilidades:

- Conectarse a RabbitMQ.
- Declarar o utilizar la topología necesaria.
- Serializar el mensaje.
- Publicarlo.
- Manejar correctamente recursos y conexiones.
- Emitir logging estructurado.

No colocar decisiones de dominio dentro de esta implementación.

---

# RabbitMQ

La topología debe respetar ADR-003.

## Exchange

Usar:

```text
telegram.incoming
```

Tipo recomendado:

```text
direct
```

Debe ser durable.

---

## Queue

Usar:

```text
telegram.incoming.messages
```

Debe ser durable.

---

## Routing key

Usar:

```text
telegram.message.text
```

---

# Dead Letter Queue

Preparar la topología de Dead Letter Queue definida por ADR-003.

Crear:

```text
telegram.incoming.messages.dlq
```

La cola principal debe quedar preparada para enviar mensajes fallidos hacia una dead-letter topology.

Si es necesario crear un exchange específico para DLQ, mantener nombres claros y consistentes.

Ejemplo posible:

```text
telegram.incoming.dlx
telegram.incoming.messages.dlq
```

No implementar todavía el retry del consumer porque el consumer corresponde a una feature posterior.

Sí dejar preparada correctamente la topología.

---

# Durabilidad

Para la cola de mensajes entrantes utilizar configuración durable.

Los mensajes publicados deben utilizar persistencia/delivery mode apropiado cuando el cliente RabbitMQ utilizado lo permita.

La intención es evitar perder mensajes ante un reinicio normal del broker.

No asumir que RabbitMQ es almacenamiento permanente.

---

# Conexión RabbitMQ

Reutilizar la configuración existente de FEAT-001.

Valores esperados:

```text
Host
Port
Username
Password
```

No hardcodear:

```text
localhost
guest
passwords
ports
```

cuando exista configuración disponible.

Dentro de Docker, utilizar el hostname correspondiente al servicio:

```text
rabbitmq
```

---

# Gestión de conexiones

No abrir una nueva conexión RabbitMQ completa para cada request HTTP si puede evitarse.

Diseñar la implementación para reutilizar apropiadamente la conexión.

Respetar las recomendaciones del cliente RabbitMQ utilizado y su modelo actual de channels/connections.

Mantener la implementación simple.

No crear un connection manager complejo sin necesidad.

---

# Fallos al publicar

Si el mensaje es soportado pero no puede publicarse en RabbitMQ, no responder como si se hubiera procesado exitosamente.

El fallo debe:

- Generar logging estructurado.
- Propagarse hasta una respuesta HTTP apropiada.
- No ocultarse silenciosamente.

No implementar todavía persistencia alternativa.

No implementar fallback a base de datos.

RabbitMQ forma parte obligatoria del flujo de entrada.

---

# Respuesta a Telegram

Cuando el mensaje haya sido correctamente publicado, devolver una respuesta HTTP exitosa.

Cuando el update sea válido pero no corresponda a un mensaje soportado, también devolver éxito sin publicar.

La respuesta debe ser rápida.

No esperar procesamiento de negocio posterior.

---

# Logging

Agregar logging estructurado.

Ejemplos de información útil:

```text
TelegramUpdateId
TelegramMessageId
TelegramChatId
TelegramUserId
```

Ejemplos de eventos:

```text
Telegram update recibido
Update ignorado por tipo no soportado
Mensaje publicado en RabbitMQ
Error publicando en RabbitMQ
```

No loggear:

- Token del bot.
- Token de ngrok.
- Passwords.
- Texto completo del mensaje si no es necesario.

Se puede loggear longitud del mensaje o identificadores en lugar del contenido.

---

# Validación

La validación debe ser mínima.

Verificar como mínimo:

- Existe mensaje.
- Existe texto.
- El texto no es vacío.
- Existe chat.
- Existe usuario remitente cuando corresponda.

No introducir FluentValidation si no aporta valor para este DTO de infraestructura.

---

# Telegram Bot Token

Esta feature puede agregar la configuración necesaria para Telegram:

```text
Telegram__BotToken
```

o nombre equivalente según la convención existente.

Debe obtenerse desde:

```text
TELEGRAM_BOT_TOKEN
```

mediante configuración de Docker/.NET.

No hardcodear el token.

---

# Registro automático del webhook

Esta feature debe implementar también el registro automático del webhook de Telegram al levantar la solución.

El requisito de la prueba técnica especifica que el webhook debe registrarse automáticamente utilizando la URL pública de ngrok.

---

# Flujo de inicialización del webhook

Implementar un Hosted Service o mecanismo equivalente:

```text
API inicia
   ↓
Esperar disponibilidad de ngrok
   ↓
Consultar API de ngrok
   ↓
Obtener URL pública HTTPS
   ↓
Construir URL
   ↓
Registrar webhook en Telegram
```

Webhook final esperado:

```text
{NGROK_PUBLIC_URL}/api/telegram/webhook
```

---

# API de ngrok

Consultar la API local de ngrok disponible desde Docker.

Ejemplo conceptual:

```text
http://ngrok:4040/api/tunnels
```

No asumir inmediatamente que el túnel existe.

Implementar reintentos limitados con backoff razonable.

No utilizar un `Thread.Sleep` bloqueante.

Utilizar operaciones async.

---

# Registro en Telegram

Utilizar el Bot API o la librería oficial/configurada para ejecutar:

```text
setWebhook
```

Registrar únicamente URLs HTTPS.

Loggear algo equivalente a:

```text
Webhook de Telegram registrado correctamente en {WebhookUrl}
```

No loggear el token.

---

# Reintentos de inicialización

Docker Compose no garantiza que ngrok esté listo inmediatamente cuando inicia la API.

El registro debe tolerar temporalmente:

- ngrok aún no disponible.
- ngrok sin túnel todavía.
- fallo temporal de red.
- Telegram temporalmente no disponible.

Implementar una cantidad limitada de intentos.

Ejemplo conceptual:

```text
intentos: 10
backoff: 2-5 segundos
```

No es obligatorio utilizar esos valores exactos.

Si finalmente no puede configurarse el webhook:

- Loggear error claramente.
- Evitar ocultarlo.
- Decidir de forma explícita si la API continúa levantada.

Preferencia para esta prueba:

```text
API continúa levantada
+
error visible en logs
```

porque facilita diagnosticar configuración incorrecta sin tumbar toda la aplicación.

---

# Idempotencia del registro del webhook

Registrar el mismo webhook múltiples veces no debe generar un problema funcional.

Puede consultarse el webhook actual antes de cambiarlo si la librería lo facilita, pero no introducir complejidad innecesaria.

El objetivo es garantizar que después de:

```bash
docker compose up --build
```

Telegram conozca automáticamente la URL pública actual de ngrok.

---

# Seguridad opcional del webhook

Si la API de Telegram soportada permite utilizar un secret token para verificar el origen del webhook, puede implementarse únicamente si no complica excesivamente esta feature.

En ese caso:

- Debe configurarse desde `.env`.
- Debe validarse mediante el header correspondiente.
- Debe documentarse.

No es obligatorio para completar FEAT-002.

Si se agrega, documentar la decisión.

---

# Docker Compose

Actualizar Compose únicamente donde sea necesario para permitir:

- API → RabbitMQ.
- API → ngrok.
- API → Telegram externo.
- ngrok → API.

Confirmar que ngrok expone el puerto del backend dentro de la red de Docker.

Evitar depender de:

```text
localhost
```

para comunicación entre containers.

---

# .env.example

Agregar o verificar:

```env
TELEGRAM_BOT_TOKEN=
NGROK_AUTHTOKEN=
```

Agregar únicamente nuevas variables realmente utilizadas.

Si se implementa secret token:

```env
TELEGRAM_WEBHOOK_SECRET=
```

No agregar valores reales.

---

# Health checks

No convertir `/health` en un chequeo del Telegram Bot API.

Mantener los health checks existentes salvo que exista una necesidad concreta.

RabbitMQ puede formar parte del health check si ya está implementado o si agregarlo es trivial y coherente con FEAT-001.

No bloquear `/health` por la disponibilidad externa de Telegram.

---

# Tests

Agregar tests únicamente donde aporten valor.

## Tests recomendados

### Mapping

Dado un update válido de Telegram:

```text
Update
→ IncomingTelegramMessage
```

debe mapear correctamente:

- UpdateId.
- MessageId.
- ChatId.
- UserId.
- Username.
- Nombre.
- Texto.
- Fecha.

---

### Update sin texto

Un update que no contiene texto:

```text
Sticker
Photo
Video
etc.
```

no debe publicarse.

---

### Texto vacío

Un mensaje vacío no debe publicarse.

---

### Controller

Si existe una abstracción fácilmente mockeable:

- Un mensaje válido delega una única publicación.
- Un update fuera de alcance devuelve éxito sin publicar.

No testear internals de RabbitMQ.

No generar tests artificiales de poco valor.

---

# No implementar todavía

Queda expresamente fuera de alcance de FEAT-002:

- Consumer RabbitMQ funcional.
- Persistencia del mensaje.
- Entidades Contact.
- Entidad Conversation.
- Entidad Message.
- EF migrations de negocio.
- SignalR events.
- Pantalla funcional de conversaciones.
- Envío de respuestas a Telegram.
- Unread count.
- Mark as read.

No anticipar FEAT-003.

---

# Validaciones obligatorias

Al finalizar ejecutar:

## Backend

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
```

El resultado esperado:

```text
0 errores
0 warnings relevantes
tests OK
```

---

## Frontend

Si no se modifica frontend, no realizar cambios innecesarios.

Aun así verificar que los cambios de configuración global no lo hayan roto si corresponde.

---

## Docker Compose

Ejecutar:

```bash
docker compose config
```

Si Docker daemon está disponible:

```bash
docker compose up --build
```

Validar:

- PostgreSQL inicia.
- RabbitMQ inicia.
- API inicia.
- ngrok inicia.
- Angular inicia.
- ngrok crea un túnel.
- API obtiene la URL.
- API registra el webhook.
- RabbitMQ contiene la topología esperada.

---

# Validación manual end-to-end de FEAT-002

Si Docker y las credenciales están disponibles:

1. Levantar:

```bash
docker compose up --build
```

2. Verificar logs:

```text
Webhook Telegram registrado
```

3. Abrir RabbitMQ Management:

```text
http://localhost:15672
```

4. Confirmar existencia de:

```text
telegram.incoming.messages
telegram.incoming.messages.dlq
```

5. Enviar desde Telegram:

```text
Hola desde Telegram
```

6. Verificar que:

```text
telegram.incoming.messages
```

reciba el mensaje.

Como todavía no existe consumer, el mensaje debe permanecer en la cola.

Eso es correcto para FEAT-002.

7. Enviar un sticker o imagen.

Verificar que no se publique como mensaje de texto.

---

# README

Actualizar únicamente las partes afectadas.

Agregar:

- Configuración del Bot de Telegram mediante BotFather.
- `TELEGRAM_BOT_TOKEN`.
- Comportamiento automático del webhook.
- Cómo verificar ngrok.
- Cómo verificar RabbitMQ.
- Cómo probar FEAT-002 manualmente.

No documentar todavía funcionalidades de conversaciones que no existen.

---

# Memory

Actualizar obligatoriamente:

```text
Docs/Memory/ProjectStatus.md
```

Agregar una sección:

```text
FEAT-002 — Telegram Webhook
```

Registrar:

- Fecha.
- Estado.
- Qué quedó implementado.
- Decisiones técnicas relevantes.
- Módulos principales afectados.
- Validaciones ejecutadas.
- Resultado de pruebas manuales.
- Limitaciones pendientes.
- Próxima feature recomendada.

Mantener Memory breve y operativo.

No duplicar todo el contenido de esta feature.

---

# Criterios de aceptación

FEAT-002 queda completa cuando:

- Existe `POST /api/telegram/webhook`.
- Recibe updates válidos de Telegram.
- Ignora correctamente updates fuera de alcance.
- Existe un contrato interno para mensajes entrantes.
- Application no depende de RabbitMQ.
- Infrastructure implementa el publisher.
- Existe exchange `telegram.incoming`.
- Existe queue `telegram.incoming.messages`.
- Existe routing key `telegram.message.text`.
- Existe DLQ.
- La topología es durable.
- Los mensajes se publican de forma persistente cuando corresponda.
- Los errores de RabbitMQ no se ocultan.
- El webhook responde rápido sin ejecutar procesamiento de negocio.
- El webhook se registra automáticamente mediante la URL pública de ngrok.
- La API tolera que ngrok tarde en iniciar.
- No existen credenciales hardcodeadas.
- Backend compila.
- Tests pasan.
- `docker compose config` es válido.
- README refleja el comportamiento real.
- `Docs/Memory/ProjectStatus.md` está actualizado.

---

# Resultado esperado

Al finalizar FEAT-002 debe poder demostrarse:

```text
Telegram
   ↓
ngrok
   ↓
POST /api/telegram/webhook
   ↓
ASP.NET Core
   ↓
RabbitMQ
   ↓
telegram.incoming.messages
```

Todavía no debe existir procesamiento del mensaje después de RabbitMQ.

Ese procesamiento corresponde a FEAT-003.

Mantener la solución simple, comprobable y coherente con ADR-003.
```

Y para **ejecutarla con el agente**, usaría este prompt:

```text
Implementá `Docs/Features/FEAT-002-telegram-webhook.md`.

Antes de modificar código:

1. Leé `AGENTS.md`.
2. Leé `Docs/Memory/ProjectStatus.md`.
3. Leé completa `Docs/Adrs/ADR-003.md`.
4. Leé completa la feature.
5. Inspeccioná la implementación actual resultante de FEAT-001.

Implementá únicamente el alcance de FEAT-002.

No anticipes FEAT-003 ni agregues persistencia de conversaciones, mensajes, contactos o procesamiento del consumer.

Prestá especial atención a:
- separación Clean Architecture;
- desacople entre Telegram, Application y RabbitMQ;
- reutilización correcta de conexiones RabbitMQ;
- topología durable;
- DLQ;
- manejo de errores;
- startup asíncrono de ngrok;
- registro automático del webhook;
- ausencia de secretos hardcodeados.

Al finalizar:

1. Ejecutá todas las validaciones definidas por la feature.
2. Corregí errores y warnings relevantes.
3. Si Docker está disponible, probá el flujo real Telegram → ngrok → API → RabbitMQ.
4. Actualizá README únicamente con comportamiento realmente implementado.
5. Actualizá obligatoriamente `Docs/Memory/ProjectStatus.md`.
6. Revisá el diff completo antes de dar por terminada la tarea.

En tu respuesta final informá:
- qué implementaste;
- arquitectura resultante;
- archivos principales modificados;
- tests y validaciones ejecutadas;
- resultado de Docker/end-to-end;
- decisiones técnicas tomadas;
- problemas o limitaciones pendientes;
- cuál sería la siguiente feature lógica.

No des la feature por completada únicamente porque el código compile.