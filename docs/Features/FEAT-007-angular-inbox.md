Implementá `Docs/Features/FEAT-007-angular-inbox.md`.

Antes de modificar código:

1. Leé `AGENTS.md`.
2. Leé `Docs/Memory/ProjectStatus.md`.
3. Leé:
   - `Docs/Adrs/ADR-002.md`
4. Revisá los contratos REST y realtime implementados en FEAT-004, FEAT-005 y FEAT-006.
5. Leé completa FEAT-007.
6. Inspeccioná la aplicación Angular creada en FEAT-001 antes de modificarla.

Implementá únicamente el alcance definido en FEAT-007.

Objetivo principal:

Crear la bandeja conversacional Angular funcional utilizando las APIs y eventos SignalR existentes.

Flujo esperado:

Carga inicial:
Angular
→ GET /api/conversations
→ seleccionar conversación
→ GET /api/conversations/{id}/messages

Tiempo real:
SignalR
→ MessageReceived
→ MessageSent
→ ConversationUpdated
→ MessagesRead
→ actualizar estado local

Lectura:
Abrir conversación
→ POST /api/conversations/{id}/read

Respuesta:
Escribir texto
→ POST /api/conversations/{id}/messages
→ Telegram
→ respuesta API / SignalR
→ actualizar UI

No implementar nuevas reglas de negocio en frontend.

## Diseño

Crear una interfaz de bandeja de dos paneles:

Panel izquierdo:
- listado de conversaciones;
- nombre o username;
- preview del último mensaje;
- fecha/hora;
- badge de mensajes no leídos.

Panel central:
- encabezado de conversación;
- historial;
- mensajes inbound/outbound diferenciados;
- composer inferior;
- botón enviar.

No se requiere responsive.

El diseño debe ser limpio, profesional y visualmente inspirado en EncodeLabs sin copiarlo pixel por pixel.

Priorizar:
- jerarquía visual;
- espacios consistentes;
- buen contraste;
- estados claros;
- lectura cómoda.

Evitar animaciones innecesarias.

## Arquitectura Angular

Usar standalone components.

Mantener una estructura simple, por ejemplo:

src/app/
├── core/
│   ├── models/
│   └── services/
├── features/
│   └── inbox/
│       ├── components/
│       ├── pages/
│       ├── models/
│       └── services/
└── shared/

No agregar NgRx.

Usar Angular Signals o estado simple mediante servicios.

No sobrearquitecturar.

## Estado

Mantener como mínimo:

- conversations
- selectedConversationId
- messages
- conversationsLoading
- messagesLoading
- sendingMessage
- realtimeConnectionState
- error

Derivar estados cuando sea posible en lugar de duplicarlos.

Evitar múltiples fuentes de verdad dentro del frontend.

## API service

Crear servicios tipados para consumir:

GET /api/conversations

GET /api/conversations/{conversationId}/messages

POST /api/conversations/{conversationId}/read

POST /api/conversations/{conversationId}/messages

No usar `any`.

Crear interfaces/types explícitos para requests y responses.

Centralizar `apiUrl` utilizando la configuración existente.

No hardcodear localhost en componentes.

## Carga inicial

Al abrir la aplicación:

1. Cargar conversaciones.
2. Mostrar estado loading.
3. Si no hay conversaciones, mostrar empty state.
4. No seleccionar automáticamente una conversación salvo que la UX definida lo justifique.

Si se decide seleccionar automáticamente la primera conversación, hacerlo explícitamente y de forma consistente.

## Sidebar

Cada conversación debe mostrar:

- DisplayName.
- Username si existe y aporta información.
- LastMessageText.
- LastMessageAt.
- UnreadCount.

Ordenar según el orden ya entregado por backend.

No reordenar arbitrariamente si no es necesario.

UnreadCount > 0 debe mostrarse claramente.

La conversación seleccionada debe distinguirse visualmente.

## Abrir conversación

Al seleccionar una conversación:

1. Actualizar `selectedConversationId`.
2. Cargar historial.
3. Mostrar loading del panel central.
4. Ejecutar mark-as-read cuando corresponda.
5. Actualizar localmente el unread de forma consistente con la respuesta/backend/realtime.

Evitar race conditions si el usuario cambia rápidamente entre conversaciones.

Si una request anterior termina después de seleccionar otra conversación, no debe pisar el estado de la conversación actual.

Usar cancelación, switchMap o una estrategia equivalente si resulta apropiada.

## Historial

Mostrar mensajes ordenados cronológicamente.

Diferenciar claramente:

Inbound
Outbound

Mostrar al menos:
- texto;
- hora.

No exponer IDs técnicos.

Agregar auto-scroll al último mensaje cuando:
- se abre una conversación;
- llega un nuevo mensaje para la conversación seleccionada;
- se envía un mensaje.

No romper la posición del usuario innecesariamente si en algún momento se encuentra revisando mensajes anteriores.

Dado que no hay paginación y el alcance es pequeño, mantener la implementación simple.

## Composer

Agregar:
- textarea o input adecuado;
- botón enviar.

Reglas:
- no permitir texto vacío;
- trim antes de enviar según semántica del backend;
- respetar máximo de 4096 caracteres;
- mostrar estado de envío;
- impedir doble submit;
- Enter puede enviar;
- Shift+Enter puede crear salto de línea si se usa textarea.

No permitir que el usuario envíe durante un request pendiente si eso genera duplicados.

## Envío

Al enviar:

POST /api/conversations/{conversationId}/messages

Manejar:
- éxito;
- 400;
- 404;
- 502;
- 503;
- fallo de red.

No mostrar errores técnicos crudos.

Ejemplos de UX:

502:
"No se pudo enviar el mensaje a Telegram."

503:
"Telegram no está disponible temporalmente."

No asumir que todos los errores HTTP tienen el mismo significado.

## Estrategia UI al enviar

Evitar duplicar mensajes entre:
- respuesta HTTP;
- evento SignalR `MessageSent`.

Elegir una estrategia explícita.

Preferencia:

Usar la respuesta HTTP para confirmar/mostrar inmediatamente el mensaje y hacer que el handler SignalR sea idempotente por `message.id`.

Alternativamente, depender únicamente de SignalR si se puede garantizar una UX correcta.

En cualquier caso:

UN mensaje enviado
→ UN mensaje visible

Nunca duplicarlo por recibir tanto response como SignalR.

Implementar deduplicación sencilla por `message.id`.

## SignalR

Crear un servicio dedicado.

Conectarse a:

/hubs/conversations

Escuchar:

- MessageReceived
- MessageSent
- ConversationUpdated
- MessagesRead

Usar el cliente oficial de SignalR.

Configurar reconexión automática.

No mezclar toda la lógica SignalR dentro de componentes.

El servicio realtime debe exponer eventos/estado al store/service del inbox.

## Realtime: MessageReceived

Cuando llega:

Si corresponde a conversación seleccionada:
- agregar mensaje si todavía no existe;
- actualizar visualmente;
- ejecutar lógica de lectura si corresponde;
- hacer auto-scroll.

Si corresponde a otra conversación:
- no cargar su historial;
- dejar que ConversationUpdated actualice preview/unread.

Evitar mensajes duplicados.

## Realtime: MessageSent

Si corresponde a la conversación seleccionada:
- agregar únicamente si no existe por `id`.

Actualizar historial sin reload completo.

## Realtime: ConversationUpdated

Actualizar únicamente la conversación correspondiente.

No volver a ejecutar GET /api/conversations por cada evento.

Actualizar:
- lastMessageText;
- lastMessageAt;
- unreadCount;
- demás campos presentes.

Reposicionar la conversación en el sidebar si su actividad la convierte en la más reciente.

Mantener orden por LastMessageAt DESC.

## Realtime: MessagesRead

Actualizar el estado de los mensajes correspondientes cuando sea necesario.

Si el backend envía IDs:
- localizar esos mensajes;
- actualizar IsRead.

Actualizar unread local de la conversación.

No recargar todo el historial.

## Reconexión SignalR

Configurar automatic reconnect.

Mostrar estado discreto si se pierde realtime.

Al reconectarse:

PostgreSQL es la fuente de verdad.

Rehidratar como mínimo:
- conversaciones;
- historial de la conversación actualmente abierta si existe.

No asumir que SignalR reproducirá eventos perdidos.

Esto es importante y debe quedar explícito en la implementación.

## Mark as read

Al abrir una conversación con unread > 0:

POST /api/conversations/{id}/read

No bloquear toda la UI mientras se procesa.

Evitar ejecutar el endpoint repetidamente sin necesidad.

Si llega un nuevo mensaje mientras la conversación está abierta, mantener comportamiento coherente.

No inventar lógica que contradiga la semántica backend.

## Errores

Mostrar errores de forma simple y visible.

Contemplar:
- error cargando conversaciones;
- error cargando historial;
- error marcando como leído;
- error enviando mensaje;
- SignalR desconectado.

No usar `alert()` salvo necesidad extrema.

Preferir mensajes inline/toast/simple feedback visual.

No agregar una librería grande únicamente para notifications.

## Empty states

Agregar estados claros para:

### Sin conversaciones

"No hay conversaciones todavía."

### Sin conversación seleccionada

"Seleccioná una conversación para ver los mensajes."

### Conversación sin mensajes

"No hay mensajes en esta conversación."

Aunque normalmente no ocurra, manejarlo correctamente.

## Loading

Evitar una única pantalla global de loading.

Distinguir:
- sidebar loading;
- historial loading;
- sending.

Usar indicadores simples.

## Estilo

Inspirarse en EncodeLabs.

Preferir:
- fondo oscuro o neutro;
- paneles limpios;
- acentos tecnológicos;
- bordes sutiles;
- tipografía clara;
- badges visibles;
- bubbles simples.

No copiar assets o contenido protegido del sitio.

No agregar efectos visuales pesados.

## Accesibilidad básica

Agregar:
- labels/aria-label donde sean necesarios;
- botones accesibles;
- focus visible;
- contraste razonable;
- estados disabled reales.

No hace falta auditoría WCAG completa.

## Performance

No hacer:
- reload completo del sidebar ante cada SignalR event;
- reload completo del historial por cada nuevo mensaje;
- suscripciones duplicadas;
- múltiples conexiones SignalR.

Mantener una única conexión realtime para la aplicación.

Desuscribir/limpiar recursos correctamente.

## Backend

No modificar backend salvo:
- corrección mínima necesaria para compatibilidad;
- CORS;
- contratos claramente incompletos.

Si es necesario modificar backend:
1. justificarlo;
2. mantener compatibilidad;
3. agregar/actualizar tests;
4. documentarlo en Memory.

No agregar features backend nuevas fuera del alcance.

## Tests Angular

Agregar tests de valor.

Como mínimo:

1. Sidebar renderiza conversaciones.
2. Badge unread se muestra correctamente.
3. Seleccionar conversación carga historial.
4. Inbound/outbound usan representación distinta.
5. Composer no envía texto vacío.
6. Sending evita doble submit.
7. MessageReceived agrega mensaje una sola vez.
8. MessageSent no duplica mensaje ya agregado por HTTP.
9. ConversationUpdated actualiza preview/unread y orden.
10. Reconexión dispara rehidratación o comportamiento equivalente.

No testear detalles internos de Angular sin valor.

## Validaciones

Ejecutar:

npm install
npm run build
npm test

Resultado:
- build correcto;
- tests OK;
- sin vulnerabilidades relevantes de producción.

Ejecutar también backend para garantizar que no se rompió integración:

dotnet build
dotnet test

y:

docker compose config

## Prueba manual end-to-end

Si Docker está disponible:

1. Levantar:

docker compose up --build

2. Abrir frontend.

3. Verificar carga de conversaciones existentes.

4. Seleccionar conversación.

5. Verificar historial.

6. Verificar que unread se marque como leído.

7. Publicar un mensaje inbound mediante el flujo existente.

8. Verificar:
- aparece sin refresh;
- sidebar se actualiza;
- conversación sube de posición;
- unread funciona correctamente.

9. Enviar mensaje desde el composer.

Si no existe token Telegram:
- verificar comportamiento 503 de forma visual.
- no introducir fake runtime.

Si existe Telegram real:
- confirmar entrega real en Telegram.

10. Cortar temporalmente SignalR/API si es posible.

11. Verificar indicador de desconexión.

12. Restaurar conexión.

13. Verificar rehidratación desde API y consistencia con PostgreSQL.

## README

Actualizar:
- URL del frontend;
- flujo de uso de la bandeja;
- comportamiento realtime;
- recuperación después de reconexión;
- errores esperables sin token Telegram;
- pasos de prueba end-to-end.

No inventar capacidades no implementadas.

## Memory

Actualizar obligatoriamente:

`Docs/Memory/ProjectStatus.md`

Agregar:

`FEAT-007 — Bandeja Angular`

Registrar brevemente:

- Fecha.
- Estado.
- Componentes principales.
- Estrategia de estado.
- Servicios API.
- Servicio SignalR.
- Estrategia de deduplicación.
- Mark-as-read.
- Reconexión/rehidratación.
- Manejo de errores.
- Tests.
- Validaciones manuales.
- Limitaciones.
- Próxima feature recomendada.

## Fuera de alcance

No implementar:
- autenticación;
- responsive/mobile;
- paginación;
- multimedia;
- búsqueda;
- filtros avanzados;
- edición/eliminación;
- typing indicators;
- presencia;
- múltiples operadores;
- delivery receipts.

## Revisión final

Antes de cerrar FEAT-007:

1. Revisá el diff completo.
2. Confirmá que no existe lógica de negocio duplicada del backend.
3. Confirmá una sola conexión SignalR.
4. Confirmá que HTTP + SignalR no duplican mensajes.
5. Confirmá que reconnect rehidrata desde API.
6. Confirmá que ConversationUpdated no provoca reload global innecesario.
7. Confirmá que cambio rápido de conversación no produce race visual.
8. Confirmá manejo correcto de 400/404/502/503.
9. Confirmá que no hay URLs hardcodeadas.
10. Confirmá build/tests frontend y backend.
11. Confirmá README y Memory.
12. Confirmá que no se agregaron features fuera de alcance.

En la respuesta final informá:

- Qué componentes creaste.
- Cómo manejás estado.
- Cómo consumís APIs.
- Cómo funciona SignalR.
- Cómo evitás duplicados HTTP/SignalR.
- Cómo manejás reconexión.
- Cómo funciona mark-as-read.
- Cómo manejás errores.
- Tests y resultados.
- Validación Docker/end-to-end.
- Cambios backend si existieron.
- Limitaciones pendientes.
- Próximo paso recomendado.

No des FEAT-007 por completada únicamente porque la interfaz se vea bien.