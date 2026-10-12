## Proyecto

Prueba técnica: bandeja conversacional para Telegram.

Flujo principal:

```text
Telegram → Webhook → RabbitMQ → Consumer → PostgreSQL → SignalR → Angular
Angular → API → Telegram Bot API
```

## Stack

- Backend: .NET 10, ASP.NET Core, Entity Framework Core
- Frontend: última versión estable de Angular
- Base de datos: PostgreSQL
- Mensajería: RabbitMQ
- Tiempo real: SignalR
- Infraestructura: Docker Compose + ngrok

## Arquitectura

Usar Clean Architecture:

```text
Domain
Application
Infrastructure
Api
```

Las dependencias deben apuntar hacia adentro.

El proyecto Domain no debe depender de:

- Entity Framework Core
- RabbitMQ
- SignalR
- Librerías de Telegram
- ASP.NET Core
- Infraestructura

Mantener los controllers livianos.

La lógica de negocio debe estar en Application y/o Domain.

No sobrearquitecturar la solución.

Evitar salvo que exista una justificación concreta:

- Microservicios
- Event Sourcing
- CQRS completo
- Redis
- Kafka
- Kubernetes
- NgRx

## Mensajes entrantes

Flujo obligatorio:

```text
Telegram
→ Webhook
→ RabbitMQ
→ Consumer
→ Persistencia
→ Commit
→ SignalR
```

Reglas:

- El webhook debe realizar solamente validaciones mínimas y publicar el mensaje en RabbitMQ.
- No realizar lógica de negocio completa dentro del webhook.
- El ACK de RabbitMQ debe ocurrir únicamente después de procesar correctamente el mensaje.
- El procesamiento debe ser idempotente utilizando `update_id` de Telegram y/o identificadores propios del mensaje.
- No emitir eventos de SignalR antes de que la persistencia haya finalizado correctamente.
- Ignorar los tipos de mensajes de Telegram que estén fuera de alcance.

## Mensajes salientes

Flujo obligatorio:

```text
Angular
→ API
→ Telegram Bot API
→ Persistir mensaje saliente
→ Actualizar conversación
→ SignalR
```

Reglas:

- Los mensajes salientes no necesitan pasar por RabbitMQ.
- Si Telegram falla al enviar un mensaje, no guardarlo como enviado correctamente.
- No emitir eventos de éxito antes de completar la persistencia.

## Dominio

Entidades principales:

- Contact
- Conversation
- Message

Direcciones posibles de un mensaje:

- Inbound
- Outbound

La conversación mantiene:

- Cantidad de mensajes no leídos
- Último mensaje
- Fecha y hora del último mensaje

Al abrir una conversación se deben marcar como leídos los mensajes entrantes pendientes.

## Frontend

Layout principal:

- Panel lateral con conversaciones
- Panel central con conversación seleccionada
- Campo para enviar mensajes

Usar servicios simples y Angular Signals para manejar estado cuando sea apropiado.

SignalR debe actualizar la interfaz sin necesidad de recargar toda la página.

Contemplar:

- Estados de carga
- Estados vacíos
- Manejo de errores
- Reconexión de SignalR
- Botón de enviar deshabilitado si el mensaje está vacío
- Prevención de doble envío
- Indicadores de mensajes no leídos

## Base de datos

PostgreSQL es la fuente de verdad del sistema.

Reglas:

- Utilizar fechas en UTC.
- Crear índices y restricciones para identificadores de Telegram.
- La persistencia de un mensaje entrante y la actualización de la conversación deben mantenerse consistentes.
- Los updates duplicados de Telegram no deben generar mensajes duplicados.
- Los updates duplicados no deben incrementar dos veces el contador de no leídos.

## Idempotencia

Telegram puede reenviar un mismo update.

El sistema debe contemplar este escenario.

Utilizar `TelegramUpdateId` o un identificador equivalente con una restricción única en base de datos.

Un update duplicado no debe:

- Crear nuevamente el mensaje.
- Incrementar nuevamente los mensajes no leídos.
- Emitir eventos duplicados por SignalR.
- Dejar la conversación en un estado inconsistente.

La base de datos debe ser la última barrera de protección contra duplicados.

## RabbitMQ

RabbitMQ se utiliza para procesar los mensajes entrantes de Telegram.

Flujo:

```text
Webhook
→ Exchange
→ Queue
→ Consumer
```

Mantener una topología simple.

Evitar crear colas o exchanges adicionales que no aporten valor al alcance de la prueba.

Contemplar Dead Letter Queue para mensajes que no puedan procesarse correctamente.

Evitar ciclos infinitos de requeue.

## SignalR

SignalR se utiliza para notificar al frontend los cambios en tiempo real.

Posibles eventos:

```text
MessageReceived
MessageSent
ConversationUpdated
MessagesRead
```

Enviar DTOs y no entidades de Entity Framework directamente.

Mantener los payloads pequeños.

PostgreSQL sigue siendo la fuente de verdad.

SignalR solamente notifica cambios.

## Documentación

La implementación debe mantenerse alineada con:

```text
Docs/Adrs/ADR-001.md
Docs/Adrs/ADR-002.md
Docs/Adrs/ADR-003.md
```

Si una decisión arquitectónica cambia, actualizar primero o junto con el cambio el ADR correspondiente.

Las especificaciones de funcionalidades se encuentran en:

```text
Docs/Features/
```

El estado del proyecto puede mantenerse en:

```text
Docs/Memory/
```

No duplicar documentación innecesariamente.

## Flujo de trabajo

Antes de modificar código:

1. Leer la feature correspondiente.
2. Inspeccionar la implementación existente.
3. Revisar los ADRs relacionados.
4. Entender el flujo actual.
5. Planificar el cambio mínimo necesario.
6. Implementar.
7. Compilar.
8. Ejecutar los tests relevantes.
9. Revisar posibles regresiones.
10. Actualizar documentación o estado del proyecto si corresponde.

No reescribir código funcional sin una razón concreta.

No realizar refactors no relacionados con la tarea actual.

## Calidad de código

Priorizar:

- Nombres claros
- Métodos pequeños
- Responsabilidades bien separadas
- APIs asíncronas
- `CancellationToken`
- Logging estructurado
- DTOs en los límites de la API
- SOLID aplicado de manera pragmática
- Manejo explícito de errores
- Código fácil de leer y explicar

Evitar:

- God services
- Lógica de negocio en controllers
- Interfaces innecesarias
- Abstracciones excesivas
- Herencia innecesaria
- Efectos secundarios ocultos
- Código difícil de mantener
- Secretos hardcodeados

## Controllers

Los controllers deben ser delgados.

Deben encargarse principalmente de:

- Recibir requests.
- Validar entrada básica.
- Delegar hacia Application.
- Traducir resultados a respuestas HTTP.

No colocar reglas de negocio directamente en los controllers.

## Application

La capa Application debe coordinar los casos de uso.

Ejemplos:

- Procesar mensaje entrante.
- Obtener conversaciones.
- Obtener historial.
- Marcar conversación como leída.
- Enviar mensaje a Telegram.

Debe depender de abstracciones para servicios externos.

## Infrastructure

Infrastructure contiene implementaciones relacionadas con:

- Entity Framework Core
- PostgreSQL
- RabbitMQ
- Telegram Bot API
- SignalR
- Integraciones externas

No colocar reglas de negocio importantes dentro de Infrastructure.

## Transacciones

Operaciones que afectan simultáneamente mensajes y conversaciones deben mantenerse consistentes.

Por ejemplo:

```text
Crear mensaje
+
Actualizar último mensaje
+
Incrementar no leídos
```

deben ejecutarse como una única operación lógica.

Evitar estados como:

```text
Mensaje guardado
Conversación no actualizada
Contador incorrecto
```

## Concurrencia

Asumir que pueden llegar dos mensajes de Telegram muy cercanos en el tiempo.

Revisar especialmente:

- Incrementos de `UnreadCount`
- Actualización de `LastMessage`
- Actualización de `LastMessageAt`
- Procesamiento duplicado
- Condiciones de carrera

Preferir garantías de base de datos antes de introducir mecanismos complejos de locking.

## Telegram

El webhook debe recibir únicamente el update y publicarlo en RabbitMQ.

No debe encargarse directamente de:

- Crear contactos.
- Crear conversaciones.
- Guardar mensajes.
- Actualizar contadores.
- Emitir SignalR.

El procesamiento real debe realizarse en el consumer.

## ngrok

ngrok debe exponer públicamente el webhook.

Al levantar la solución, el sistema debe intentar:

```text
Levantar backend
→ Esperar ngrok
→ Obtener URL pública
→ Construir URL del webhook
→ Registrar webhook en Telegram
```

No requerir configuración manual del webhook por parte del evaluador.

## Docker Compose

La solución debe poder ejecutarse con:

```bash
docker compose up --build
```

después de completar `.env`.

Servicios esperados:

- PostgreSQL
- RabbitMQ
- API
- Frontend Angular
- ngrok

El evaluator no debería tener que ejecutar pasos manuales no documentados.

## Seguridad

Nunca guardar ni loggear:

- Token del bot de Telegram
- Token de ngrok
- Password de PostgreSQL
- Password de RabbitMQ
- Otras credenciales

`.env` nunca debe subirse al repositorio.

Mantener `.env.example` sin valores reales.

## Tests

Los tests son opcionales pero deben priorizar comportamiento del dominio.

Casos importantes:

- Un mensaje entrante incrementa los no leídos.
- Marcar una conversación como leída resetea el contador.
- Un mensaje saliente no incrementa los no leídos.
- No se permiten mensajes de texto vacíos.
- Un update duplicado no genera dos mensajes.

No generar grandes cantidades de tests sin valor.

## Scope

No implementar salvo que sea explícitamente solicitado:

- Autenticación
- Autorización
- Gestión de operadores
- Roles
- Imágenes
- Videos
- Audios
- Archivos
- Stickers
- Edición de mensajes
- Eliminación de mensajes
- Búsqueda
- Paginación
- Redis
- Microservicios

Mantenerse dentro del alcance de la prueba técnica.

## Criterio de finalización

Una feature se considera terminada cuando:

- El backend compila.
- El frontend compila si fue modificado.
- Los tests relevantes pasan.
- No se rompe funcionalidad existente.
- La arquitectura sigue siendo consistente.
- No se introducen credenciales.
- El comportamiento solicitado funciona de punta a punta.

## Regla final

Cuando existan dos posibles implementaciones, preferir la que sea:

1. Correcta.
2. Simple de explicar.
3. Fácil de probar.
4. Coherente con los ADRs.
5. Proporcional al alcance de la prueba.

No optimizar por complejidad arquitectónica.

Optimizar por claridad, mantenibilidad y corrección.