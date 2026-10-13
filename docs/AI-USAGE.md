# Uso de IA

## Herramientas utilizadas

- **Codex:** asistencia para implementar features, inspeccionar cambios, ejecutar validaciones y realizar la auditoría final.

## Rol de IA y agentes

El trabajo se dividió por features documentadas. La IA ayudó a interpretar requisitos, proponer cambios acotados, implementar código y tests, revisar límites de Clean Architecture y automatizar verificaciones. Las decisiones de alcance, trade-offs y aceptación se mantuvieron explícitas en ADRs, features y memoria del proyecto; no se trató la salida del modelo como evidencia suficiente por sí sola.

## Prompts representativos

- “Leé `AGENTS.md`, los ADRs y la feature antes de modificar código; implementá únicamente su alcance y actualizá `ProjectStatus`.”
- “Validá idempotencia por `TelegramUpdateId` y que incoming, read y outbound compartan el advisory lock `chat:{TelegramChatId}`.”
- “Auditá el repositorio completo para entrega: requisitos, arquitectura, Docker desde cero, tests, seguridad, C4 y documentación.”

## Validación

El código asistido fue revisado mediante inspección del diff, builds, tests backend y frontend, `dotnet format`, auditoría de dependencias, validación de Compose y pruebas manuales sobre Docker. También se verificaron arquitectura, migraciones, topología/ACK/DLQ de RabbitMQ, concurrencia, idempotencia, orden commit/realtime, reconexión y ausencia de secretos versionados. Las pruebas que requieren interacción humana con Telegram se registran de forma separada y no se presentan como ejecutadas si no pudieron completarse.
