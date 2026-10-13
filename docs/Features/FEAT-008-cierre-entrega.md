Implementá `Docs/Features/FEAT-008-cierre-entrega.md`.

Antes de modificar cualquier archivo:

1. Leé `AGENTS.md`.
2. Leé `Docs/Memory/ProjectStatus.md`.
3. Leé todos los ADRs:
   - `Docs/Adrs/ADR-001.md`
   - `Docs/Adrs/ADR-002.md`
   - `Docs/Adrs/ADR-003.md`
4. Revisá todas las features FEAT-001 a FEAT-007.
5. Inspeccioná el repositorio completo.
6. Revisá el README actual, `.env.example`, `docker-compose.yml`, C4 y documentación existente.

Esta feature NO debe agregar funcionalidades nuevas.

El objetivo es dejar el repositorio listo para ser entregado públicamente como prueba técnica.

# Objetivo principal

Realizar una auditoría integral de:

- cumplimiento funcional;
- arquitectura;
- calidad de código;
- Docker;
- seguridad;
- documentación;
- C4;
- ADRs;
- trabajo con IA;
- pruebas;
- experiencia de ejecución para el evaluador.

Al finalizar, el proyecto debe poder ser clonado, configurado y ejecutado siguiendo solamente el README.

# 1. Auditoría de requisitos funcionales

Verificar uno por uno los requisitos originales.

Confirmar que la aplicación permite:

1. Recibir mensajes de texto desde Telegram.
2. Recibirlos mediante webhook público.
3. Procesarlos mediante RabbitMQ.
4. Persistir contactos, conversaciones y mensajes.
5. Ver listado de contactos/conversaciones.
6. Mostrar nombre o username.
7. Mostrar cantidad de mensajes no leídos.
8. Mostrar preview del último mensaje.
9. Abrir una conversación.
10. Ver historial completo.
11. Marcar mensajes como leídos.
12. Responder desde Angular.
13. Enviar la respuesta al usuario mediante Telegram.
14. Recibir actualizaciones realtime sin recargar la página.

Registrar cualquier requisito que no esté completamente cubierto.

No declarar el proyecto listo si falta alguno obligatorio.

# 2. Auditoría de alcance

Confirmar que no se haya agregado complejidad innecesaria.

No deberían existir funcionalidades fuera de alcance como:

- autenticación;
- roles;
- usuarios operadores;
- multimedia;
- paginación;
- búsqueda;
- edición de mensajes;
- borrado;
- typing indicators;
- presencia;
- Redis;
- Kafka;
- Kubernetes;
- microservicios.

No eliminar una implementación útil solo por simplificar, pero evitar scope innecesario.

# 3. Clean Architecture

Revisar dependencias entre proyectos.

Confirmar:

- Domain no depende de Infrastructure.
- Domain no depende de EF Core.
- Domain no depende de Telegram.
- Domain no depende de RabbitMQ.
- Domain no depende de SignalR.
- Application no depende de Infrastructure.
- Application no depende de ASP.NET Core.
- Application no depende de SDKs concretos externos.
- Infrastructure implementa integraciones externas.
- Api funciona como composition root y capa HTTP/realtime.

Revisar controllers.

Deben ser delgados y delegar en Application.

No mover código por estética si la separación actual ya es correcta.

# 4. Revisión de dominio

Revisar:

- Contact
- Conversation
- Message

Confirmar que las reglas principales estén correctamente modeladas:

- inbound incrementa unread;
- outbound no incrementa unread;
- mark-as-read deja unread consistente;
- last message se actualiza correctamente;
- timestamps son UTC;
- duplicados no modifican estado.

Revisar nombres y responsabilidades.

# 5. Idempotencia

Revisar la implementación completa de idempotencia.

Confirmar que:

- `TelegramUpdateId` tiene protección UNIQUE;
- un update duplicado no crea otro Message;
- no incrementa UnreadCount;
- no emite SignalR duplicado;
- se ACKea correctamente;
- la base de datos es la última barrera de consistencia.

Realizar prueba manual de duplicado si Docker está disponible.

# 6. Concurrencia

Revisar las operaciones:

- incoming;
- mark-as-read;
- outbound.

Confirmar que utilizan exactamente la misma estrategia de advisory lock por chat.

Verificar que la clave lógica usada para:

`chat:{TelegramChatId}`

sea consistente en todos los flujos.

Buscar posibles diferencias de formato que puedan provocar locks distintos.

Confirmar:

- conversaciones diferentes pueden procesarse en paralelo;
- operaciones del mismo chat se serializan;
- no se pierde UnreadCount;
- LastMessage no queda desactualizado.

# 7. RabbitMQ

Auditar:

- exchange;
- queue;
- routing key;
- DLQ;
- durability;
- persistent messages;
- ACK;
- NACK;
- retry;
- requeue.

Confirmar que no existe loop infinito de requeue.

Revisar la estrategia de un retry antes de DLQ.

Confirmar que un payload inválido termina correctamente en DLQ.

Documentar la topología real.

# 8. PostgreSQL

Revisar esquema y migraciones.

Confirmar:

- Contact único por TelegramUserId;
- Conversation correctamente relacionada;
- Message correctamente relacionada;
- TelegramUpdateId único cuando aplica;
- protección de TelegramMessageId;
- índices por Conversation/fecha;
- foreign keys correctas.

No agregar índices sin justificación.

# 9. Migraciones

Probar una base vacía.

Confirmar que al ejecutar:

`docker compose up --build`

la base llega correctamente al esquema final.

La aplicación automática de migraciones al iniciar puede mantenerse por simplicidad del challenge.

Documentar en README que en producción podría ejecutarse como etapa separada del deployment.

# 10. SignalR

Revisar:

- `/hubs/conversations`
- MessageReceived
- MessageSent
- ConversationUpdated
- MessagesRead

Confirmar:

- eventos posteriores al commit;
- duplicados no generan eventos;
- SignalR no es fuente de verdad;
- fallos realtime no revierten PostgreSQL;
- frontend rehidrata después de reconexión.

Documentar explícitamente que SignalR es best effort.

# 11. Telegram outbound

Revisar el flujo:

Angular
→ API
→ Telegram
→ PostgreSQL
→ SignalR

Confirmar:

- RabbitMQ no participa;
- Telegram debe confirmar antes de persistir como enviado;
- fallos Telegram no crean Message exitoso;
- Outbound no modifica unread;
- TelegramMessageId se persiste;
- token nunca se loggea.

Mantener documentado el trade-off:

Telegram puede aceptar el mensaje y luego fallar PostgreSQL.

No implementar Outbox, retries persistentes ni reconciliación en esta feature.

# 12. Frontend

Auditar la UI completa.

Verificar:

- sidebar;
- conversaciones;
- unread;
- preview;
- selección;
- historial;
- inbound/outbound;
- composer;
- errores;
- empty states;
- loading;
- realtime;
- reconnect.

Revisar especialmente:

- no duplicación HTTP + SignalR;
- una única conexión SignalR;
- cancelación de requests al cambiar rápido de conversación;
- rehidratación al reconectar;
- no hardcodear API URLs;
- no utilizar `any` innecesariamente.

No rediseñar todo si la interfaz ya es prolija.

# 13. Estilo visual

Revisar que sea:

- simple;
- profesional;
- agradable;
- consistente;
- cercano al estilo tecnológico de EncodeLabs.

No es necesario copiar pixel-perfect.

No agregar animaciones innecesarias.

# 14. Seguridad

Buscar en todo el repositorio posibles secretos.

Confirmar que no estén versionados:

- TELEGRAM_BOT_TOKEN real;
- NGROK_AUTHTOKEN real;
- PostgreSQL password real;
- RabbitMQ password real;
- connection strings con secretos;
- `.env`.

Revisar `.gitignore`.

`.env.example` debe existir y no contener secretos.

No loggear tokens.

# 15. .env.example

Revisar que contenga exactamente las variables necesarias.

Debe ser claro qué debe completar el evaluador.

Como mínimo revisar:

TELEGRAM_BOT_TOKEN
NGROK_AUTHTOKEN
POSTGRES_DB
POSTGRES_USER
POSTGRES_PASSWORD
RABBITMQ_DEFAULT_USER
RABBITMQ_DEFAULT_PASS

y cualquier configuración adicional realmente usada.

Eliminar variables obsoletas.

# 16. Docker Compose

Esta es una validación crítica.

El flujo esperado para el evaluador debe ser:

1. clonar repo;
2. copiar `.env.example` a `.env`;
3. completar credenciales;
4. ejecutar:

`docker compose up --build`

No debe necesitar:

- .NET instalado;
- Node instalado;
- PostgreSQL local;
- RabbitMQ local;
- ngrok local.

Confirmar servicios:

- postgres;
- rabbitmq;
- api;
- frontend;
- ngrok.

Revisar:

- health checks;
- depends_on;
- networking;
- volúmenes;
- puertos;
- variables;
- nombres internos.

# 17. Prueba desde cero

Si Docker está disponible:

Eliminar stack anterior y volúmenes temporales del proyecto.

Luego ejecutar desde estado limpio:

`docker compose up --build`

Validar:

1. PostgreSQL healthy.
2. RabbitMQ healthy.
3. Migraciones aplicadas.
4. API healthy.
5. Frontend disponible.
6. ngrok disponible.
7. webhook registrado si hay credenciales.
8. `/health` responde 200.
9. UI carga correctamente.

No depender de datos anteriores.

# 18. Prueba real Telegram/ngrok

Si ahora existen credenciales válidas, realizar una prueba real.

Validar flujo completo:

Telegram real
→ ngrok
→ webhook
→ RabbitMQ
→ consumer
→ PostgreSQL
→ SignalR
→ Angular

Pasos:

1. Enviar mensaje de texto al bot.
2. Ver conversación aparecer sin refresh.
3. Confirmar unread.
4. Abrir conversación.
5. Confirmar unread = 0.
6. Responder desde Angular.
7. Confirmar recepción real en Telegram.
8. Enviar otro mensaje.
9. Confirmar realtime.

Si no hay credenciales válidas:

- no inventar resultados;
- documentar claramente que el flujo real no pudo probarse;
- mantener instrucciones completas para el evaluador.

# 19. Reconexión

Realizar prueba manual si es posible.

1. Abrir conversación.
2. Cortar temporalmente realtime/API.
3. Generar cambios.
4. Restaurar conexión.
5. Confirmar rehidratación desde API/PostgreSQL.

Verificar:

- sidebar;
- historial;
- unread.

# 20. Tests backend

Ejecutar:

`dotnet restore`
`dotnet build`
`dotnet test`
`dotnet format --verify-no-changes`

Esperado:

- 0 errores;
- 0 warnings relevantes;
- todos los tests OK.

No agregar tests solo para subir número.

# 21. Tests frontend

Ejecutar:

`npm install`
`npm audit --omit=dev`
`npm run build`
`npm test`

Esperado:

- build correcto;
- tests OK;
- sin vulnerabilidades de producción conocidas.

# 22. C4 Nivel 1

Crear o revisar:

`Docs/C4/C4-Level-1.svg`

o XML draw.io equivalente.

Mostrar únicamente:

- Usuario de Telegram.
- Operador.
- Bandeja Conversacional.
- Telegram Platform.

Relaciones de alto nivel.

No mostrar clases ni componentes internos.

# 23. C4 Nivel 2

Crear o revisar:

`Docs/C4/C4-Level-2.svg`

o XML draw.io.

Mostrar:

- Angular Web App.
- ASP.NET Core API.
- RabbitMQ.
- PostgreSQL.
- ngrok.
- Telegram Platform.

Representar correctamente:

Incoming:

Telegram
→ ngrok
→ API
→ RabbitMQ
→ consumer
→ PostgreSQL
→ SignalR
→ Angular

Outbound:

Angular
→ API
→ Telegram
→ PostgreSQL
→ SignalR

No representar tecnologías que no existen.

# 24. ADRs

Revisar:

- ADR-001 Base de datos.
- ADR-002 Tiempo real.
- ADR-003 Webhook/RabbitMQ.

Confirmar que coincidan con la implementación final.

Actualizar únicamente si alguna decisión real cambió.

Mantener máximo aproximado de una página por ADR.

# 25. AI-USAGE.md

Crear o completar:

`Docs/AI-USAGE.md`

Máximo una página.

Debe incluir:

## Herramientas utilizadas

Incluir únicamente herramientas realmente usadas.

Por ejemplo:

- ChatGPT
- Codex
- Claude Code

## Rol de IA/agentes

Explicar brevemente el flujo utilizado.

Por ejemplo:

- definición de features;
- implementación;
- revisión arquitectónica;
- QA;
- detección de problemas;
- generación de tests.

No exagerar autonomía.

## Prompts representativos

Incluir 2 o 3 prompts representativos.

No pegar prompts gigantes completos.

Elegir fragmentos que demuestren:

- lectura de AGENTS/ADRs;
- implementación por feature;
- validación de concurrencia/idempotencia;
- revisión final.

## Validación

Explicar que todo código generado fue revisado mediante:

- inspección de diff;
- builds;
- tests;
- Docker;
- pruebas manuales;
- validación de arquitectura;
- concurrencia;
- idempotencia.

El documento debe mostrar IA como herramienta de asistencia, no como reemplazo de criterio técnico.

# 26. README final

Revisar o reescribir README para que sea el punto de entrada del evaluador.

Debe incluir:

## Descripción

Qué problema resuelve.

## Arquitectura

Explicar brevemente ambos flujos.

Incoming:

Telegram
→ ngrok
→ API
→ RabbitMQ
→ PostgreSQL
→ SignalR
→ Angular

Outbound:

Angular
→ API
→ Telegram
→ PostgreSQL
→ SignalR

## Requisitos previos

Idealmente solamente:

- Docker;
- Docker Compose;
- Telegram bot token;
- ngrok authtoken.

## Configuración

Explicar:

`cp .env.example .env`

o equivalente en Windows.

Indicar qué variables deben completarse.

## BotFather

Explicar brevemente cómo crear bot y copiar token.

## ngrok

Explicar cómo obtener authtoken.

## Ejecución

`docker compose up --build`

## URLs

Documentar:

- frontend;
- API;
- health;
- RabbitMQ Management;
- ngrok UI si aplica.

## Cómo probar punta a punta

Pasos exactos:

1. levantar stack;
2. abrir frontend;
3. escribir al bot;
4. verificar conversación;
5. abrir;
6. responder;
7. verificar Telegram.

## Supuestos

Documentar como mínimo:

- un operador;
- un contacto tiene una conversación;
- solo texto;
- no auth;
- sin paginación;
- SignalR best effort;
- PostgreSQL fuente de verdad;
- outbound síncrono;
- migraciones automáticas por simplicidad del challenge;
- Clients.All por no existir múltiples operadores.

## Tests

Cómo correr backend/frontend.

## Documentación

Links a:

- ADRs;
- C4;
- AI-USAGE.

## Limitaciones

Transparentes y concretas.

README debe ser corto pero suficiente.

# 27. Preguntas bonus

Agregar al README o un documento pequeño.

## Caché

Responder en máximo un párrafo.

Idea:

No se incorpora caché en el alcance actual porque PostgreSQL y SignalR son suficientes. En escenarios de mayor volumen podría utilizarse Redis para resúmenes de conversaciones, metadata o datos de lectura frecuente, teniendo en cuenta la complejidad de invalidación. En despliegues con múltiples instancias también podría utilizarse como backplane de SignalR.

Adaptar al proyecto real.

## Multimedia

Responder en máximo un párrafo.

Idea:

No transportar archivos binarios directamente por RabbitMQ. El broker debería transportar metadata y referencias. Un worker podría descargar el archivo y almacenarlo en object storage como S3, Azure Blob o MinIO, mientras PostgreSQL guarda metadata y referencias.

No implementar multimedia.

# 28. Docs/Index.md

Actualizar `Docs/Index.md`.

Debe servir como mapa documental.

Incluir enlaces a:

- ADRs;
- C4;
- Features;
- AI-USAGE;
- Memory.

Mantenerlo corto.

# 29. ProjectStatus

Actualizar obligatoriamente:

`Docs/Memory/ProjectStatus.md`

Agregar:

`FEAT-008 — Cierre y entrega`

Registrar:

- Fecha.
- Estado.
- Auditoría final.
- Tests backend.
- Tests frontend.
- Resultado Docker desde cero.
- Resultado Telegram/ngrok real.
- Resultado idempotencia.
- Resultado reconexión.
- Estado C4.
- Estado ADRs.
- Estado AI-USAGE.
- Secret scan.
- Limitaciones finales.

Solo si todos los requisitos obligatorios están cumplidos:

`Estado final: listo para entrega.`

Si falta alguna validación obligatoria, indicarlo explícitamente.

# 30. Git / repositorio público

Revisar que no existan:

- `.env`;
- `bin/`;
- `obj/`;
- `node_modules/`;
- `dist/`;
- dumps;
- logs;
- archivos temporales;
- credenciales.

Revisar nombres de archivos y carpetas.

Confirmar que `.env.example` sí esté versionado.

# 31. Revisión como evaluador

Evaluar el repositorio con los criterios originales.

## Visión de negocio — 15%

¿Resuelve exactamente el problema?

¿Hay scope innecesario?

## Arquitectura — 20%

¿Clean Architecture está realmente aplicada?

¿C4 coincide con código?

## Toma de decisiones — 20%

¿ADRs muestran alternativas y trade-offs reales?

## Trabajo con IA — 15%

¿AI-USAGE muestra un uso controlado y validado?

## Calidad de código — 20%

¿El código es legible, consistente y pragmático?

## Funcionamiento e interfaz — 10%

¿El flujo principal funciona y la UI es clara?

Anotar cualquier punto que pueda restar puntaje y corregirlo si está dentro del alcance.

# No hacer

Durante FEAT-008:

- no agregar nuevas features;
- no cambiar arquitectura sin motivo;
- no incorporar nuevas tecnologías;
- no meter Redis;
- no agregar autenticación;
- no hacer refactors masivos;
- no rediseñar completamente Angular;
- no optimizar prematuramente.

Esta feature es de cierre.

# Validaciones finales obligatorias

Ejecutar:

Backend:

`dotnet restore`
`dotnet build`
`dotnet test`
`dotnet format --verify-no-changes`

Frontend:

`npm install`
`npm audit --omit=dev`
`npm run build`
`npm test`

Docker:

`docker compose config`

Si Docker está disponible:

`docker compose up --build`

y realizar pruebas de integración.

# Respuesta final del agente

Al finalizar informá:

- resultado de auditoría;
- cambios realizados;
- cantidad y resultado de tests backend;
- cantidad y resultado de tests frontend;
- resultado Docker desde cero;
- resultado Telegram/ngrok real;
- resultado de idempotencia;
- resultado de reconexión;
- estado README;
- estado C4;
- estado ADRs;
- estado AI-USAGE;
- resultado de revisión de secretos;
- limitaciones restantes;
- si el repositorio está o no listo para entrega.

No declares el proyecto listo para entrega si todavía existe algún requisito obligatorio incumplido.