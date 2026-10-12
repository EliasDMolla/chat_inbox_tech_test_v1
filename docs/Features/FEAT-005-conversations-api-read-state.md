Implementá `Docs/Features/FEAT-005-conversations-api-read-state.md`.

Antes de modificar código:

1. Leé `AGENTS.md`.
2. Leé `Docs/Memory/ProjectStatus.md`.
3. Leé:
   - `Docs/Adrs/ADR-001.md`
   - `Docs/Adrs/ADR-002.md`
4. Revisá ADR-003 únicamente para no romper el flujo existente.
5. Leé completa FEAT-005.
6. Inspeccioná la implementación actual de FEAT-001 a FEAT-004.
7. Revisá especialmente las entidades Contact, Conversation y Message, sus mappings EF Core y los DTOs realtime existentes.

Implementá únicamente el alcance definido en FEAT-005.

Objetivo principal:

PostgreSQL
→ Application queries
→ API
→ futuro cliente Angular

y:

Abrir conversación
→ MarkAsRead
→ PostgreSQL
→ Commit
→ SignalR ConversationUpdated / MessagesRead

PostgreSQL continúa siendo la fuente de verdad.

No implementes todavía:
- envío de mensajes salientes;
- Telegram Bot API para respuestas;
- UI completa Angular;
- autenticación;
- paginación.

## Endpoints requeridos

Implementar como mínimo:

GET /api/conversations

GET /api/conversations/{conversationId}/messages

POST /api/conversations/{conversationId}/read

Mantener controllers delgados.

La lógica de consultas y comandos debe quedar en Application.

## GET /api/conversations

Debe devolver el listado necesario para construir el sidebar.

Cada conversación debe incluir al menos:

- ConversationId
- ContactId
- DisplayName
- Username
- LastMessageText
- LastMessageAt
- UnreadCount

Ordenar por actividad más reciente:

LastMessageAt DESC

Definir un criterio estable de desempate si es necesario.

No devolver entidades EF Core directamente.

Usar DTOs explícitos.

Para DisplayName:

- preferir nombre disponible del contacto;
- usar username cuando no exista un nombre útil;
- definir un fallback razonable.

No introducir lógica excesivamente compleja.

## Performance del listado

Evitar N+1 queries.

La consulta debe resolverse eficientemente desde EF Core/PostgreSQL.

Usar `AsNoTracking()` para lecturas cuando corresponda.

No cargar el historial completo para construir el sidebar.

Seleccionar únicamente las columnas necesarias.

## GET /api/conversations/{conversationId}/messages

Debe devolver el historial completo de la conversación.

Paginación está explícitamente fuera de alcance.

Cada mensaje debe incluir al menos:

- Id
- ConversationId
- Direction
- Text
- IsRead
- SentAt

Orden:

SentAt ASC

Definir desempate estable cuando dos mensajes tengan el mismo timestamp.

No exponer:
- TelegramUpdateId
- detalles internos de idempotencia
- datos de infraestructura
- entidades EF

Si la conversación no existe, devolver una respuesta HTTP coherente, preferentemente 404.

## POST /api/conversations/{conversationId}/read

Debe marcar como leídos los mensajes entrantes pendientes de una conversación.

Comportamiento esperado:

Unread incoming messages
→ IsRead = true

Conversation.UnreadCount
→ 0

La operación debe ser idempotente.

Ejecutarla dos veces debe mantener:

UnreadCount = 0

sin producir errores ni cambios inconsistentes.

## Regla de lectura

Solamente marcar como leídos mensajes:

Direction = Inbound
AND
IsRead = false

No modificar artificialmente mensajes salientes.

Mantener Message y Conversation consistentes.

## Transacción

El marcado como leído debe comportarse como una única operación lógica:

Actualizar mensajes unread
+
Conversation.UnreadCount = 0
+
Commit

No permitir que quede:

Messages.IsRead = true
pero
Conversation.UnreadCount > 0

o viceversa.

Usar una transacción cuando la implementación requiera múltiples operaciones SQL.

## Concurrencia

Este punto es importante.

Considerar el escenario:

1. operador abre conversación;
2. comienza `MarkAsRead`;
3. llega un nuevo mensaje concurrentemente.

El resultado final no debe perder un mensaje no leído.

No hacer simplemente:

UPDATE messages SET IsRead = true
UPDATE conversation SET UnreadCount = 0

sin considerar la concurrencia con FEAT-003.

Reutilizar o adaptar la estrategia de serialización existente por chat/conversación si es apropiado.

No agregar locks distribuidos ni Redis.

La implementación debe garantizar que un mensaje nuevo concurrente quede:
- leído si pertenece correctamente al momento procesado, o
- unread si llegó después;

pero nunca perdido por una carrera.

Documentar brevemente la estrategia elegida.

## SignalR

Después de un MarkAsRead exitoso, notificar al frontend.

Evaluar eventos:

ConversationUpdated
MessagesRead

Si `ConversationUpdated` ya permite actualizar correctamente el unread count, evitar eventos redundantes salvo que `MessagesRead` aporte valor real al historial abierto.

Si se implementa `MessagesRead`, usar un payload pequeño, por ejemplo:

{
  "conversationId": "...",
  "messageIds": [...]
}

o una representación equivalente.

No enviar entidades completas.

Emitir SignalR únicamente después del commit.

Si SignalR falla después del commit:

- registrar error;
- no revertir el marcado como leído;
- devolver éxito HTTP si la operación persistente fue exitosa;
- PostgreSQL sigue siendo la fuente de verdad.

Mantener coherencia con FEAT-004.

## Application

Crear casos de uso claros, por ejemplo:

- GetConversations
- GetConversationMessages
- MarkConversationAsRead

Los nombres pueden adaptarse a la convención actual.

Application no debe depender de EF Core ni ASP.NET Core.

Definir abstracciones únicamente cuando aporten valor.

No crear un repository genérico artificial si actualmente EF Core está encapsulado de otra forma razonable.

## Infrastructure

Implementar las consultas EF Core de forma eficiente.

Revisar:

- `AsNoTracking()`
- proyecciones directas a DTO/projection
- índices existentes
- consultas sin N+1

Agregar índices solamente si existe una necesidad concreta.

No generar una migración únicamente por cambios que no alteren el esquema.

## API

Controllers delgados.

Responsabilidades:

- recibir parámetros;
- delegar a Application;
- devolver status codes.

Comportamientos esperados:

GET /api/conversations
→ 200

GET /api/conversations/{id}/messages
→ 200 o 404

POST /api/conversations/{id}/read
→ 200/204 o 404

Elegir una convención coherente y mantenerla.

No devolver siempre 200 con `success: false` para errores HTTP normales.

## DTOs

Mantener consistencia entre DTOs REST y DTOs realtime, pero no forzar reutilización si representan propósitos distintos.

Evitar duplicación innecesaria.

No filtrar propiedades internas por accidente.

## Tests

Agregar tests de valor real.

Como mínimo cubrir:

1. Listado de conversaciones ordenado por LastMessageAt DESC.
2. Conversation DTO contiene unread y preview correctos.
3. Historial devuelve mensajes en orden cronológico.
4. Historial de conversación inexistente devuelve NotFound/result equivalente.
5. MarkAsRead marca solamente mensajes entrantes unread.
6. MarkAsRead pone UnreadCount en 0.
7. MarkAsRead ejecutado dos veces es idempotente.
8. MarkAsRead emite realtime solamente después de persistencia exitosa.
9. Conversación inexistente no genera notificaciones.
10. Revisar mediante test focalizado la concurrencia MarkAsRead vs nuevo mensaje si la arquitectura actual permite probarlo razonablemente.

No testear internals de ASP.NET Core o EF sin valor.

## Validación manual

Si Docker está disponible:

1. Levantar el stack:

docker compose up --build

2. Generar dos conversaciones con varios mensajes utilizando el flujo existente.

3. Verificar:

GET /api/conversations

Debe devolver:
- ambas conversaciones;
- orden correcto;
- unread correcto;
- preview correcto.

4. Consultar:

GET /api/conversations/{id}/messages

Verificar historial completo y ordenado.

5. Ejecutar:

POST /api/conversations/{id}/read

6. Verificar en PostgreSQL:
- mensajes inbound correspondientes con IsRead = true;
- Conversation.UnreadCount = 0.

7. Ejecutar nuevamente el mismo endpoint.

Verificar que siga consistente.

8. Conectar un cliente SignalR y comprobar el evento realtime correspondiente.

9. Si es posible, probar llegada de un nuevo mensaje cerca de MarkAsRead y confirmar que no se pierde el unread.

## Validaciones obligatorias

Ejecutar:

dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes

Esperado:
- 0 errores
- 0 warnings relevantes
- todos los tests OK

Ejecutar:

docker compose config

Si Docker está disponible:

docker compose up --build

y realizar la prueba manual descrita.

No modificar Angular salvo que sea necesario para una validación mínima.

## README

Actualizar únicamente lo realmente implementado.

Agregar:
- endpoints disponibles;
- ejemplos breves de uso;
- semántica de mark-as-read;
- cómo recuperar estado después de una reconexión SignalR;
- aclaración de que PostgreSQL es la fuente de verdad.

No documentar todavía envío de mensajes salientes.

## Memory

Actualizar obligatoriamente:

`Docs/Memory/ProjectStatus.md`

Agregar:

`FEAT-005 — API de conversaciones, historial y lectura`

Registrar brevemente:

- Fecha.
- Estado.
- Endpoints implementados.
- DTOs principales.
- Estrategia de queries.
- Estrategia de concurrencia de MarkAsRead.
- Evento SignalR utilizado.
- Validaciones ejecutadas.
- Prueba manual.
- Limitaciones pendientes.
- Próxima feature recomendada.

## Fuera de alcance

No implementar todavía:

- POST para enviar mensajes a Telegram.
- Telegram Bot API saliente.
- Message delivery statuses.
- Angular Inbox funcional.
- Autenticación.
- Paginación.
- Búsqueda.
- Filtros complejos.

No anticipar FEAT-006.

## Revisión final

Antes de dar FEAT-005 por terminada:

1. Revisá el diff completo.
2. Confirmá que no existe N+1 en listado/historial.
3. Confirmá que no se exponen entidades EF.
4. Confirmá que REST no expone identificadores internos innecesarios de Telegram.
5. Confirmá que MarkAsRead es idempotente.
6. Revisá específicamente la carrera MarkAsRead vs IncomingMessage.
7. Confirmá que SignalR ocurre después del commit.
8. Confirmá que un fallo SignalR no revierte persistencia.
9. Confirmá que no se agregaron funcionalidades futuras.
10. Confirmá que README y Memory describen exactamente la implementación real.

En tu respuesta final informá:

- Endpoints implementados.
- Casos de uso creados.
- Consultas y estrategia para evitar N+1.
- Estrategia de MarkAsRead.
- Cómo resolviste concurrencia con mensajes entrantes.
- Eventos SignalR emitidos.
- Tests y resultados.
- Validaciones Docker/manuales.
- Archivos principales modificados.
- Limitaciones pendientes.
- Próxima feature recomendada.

No des FEAT-005 por terminada únicamente porque compile.