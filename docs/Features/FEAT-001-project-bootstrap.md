# FEAT-001: Bootstrap inicial del proyecto

## Objetivo

Crear la estructura base de la solución para la prueba técnica de bandeja conversacional, dejando preparados los proyectos backend, frontend, testing e infraestructura mínima para continuar con las siguientes features.

Esta feature NO debe implementar todavía la lógica funcional de Telegram, RabbitMQ, SignalR ni conversaciones.

El objetivo es dejar una base limpia, compilable y coherente con `AGENTS.md`.

---

## Documentación a respetar

Antes de implementar:

- Leer `AGENTS.md`.
- Leer `Docs/Adrs/ADR-001.md`.
- Leer `Docs/Adrs/ADR-002.md`.
- Leer `Docs/Adrs/ADR-003.md`.

No cambiar decisiones arquitectónicas definidas en esos documentos.

---

## Alcance

Crear:

- Solución .NET 10.
- Proyectos de Clean Architecture.
- Proyecto de tests.
- Aplicación Angular.
- Configuración base de PostgreSQL.
- Configuración base de RabbitMQ.
- Configuración base de Docker Compose.
- `.gitignore`.
- Configuración inicial de variables de entorno.
- Health check básico del backend.
- Estructura inicial de documentación.

No implementar aún casos de uso funcionales.

---

# Backend

## Solución

Crear una solución:

```text
EncodeLabs.Inbox.sln
```

Dentro de:

```text
src/
```

crear:

```text
EncodeLabs.Inbox.Domain
EncodeLabs.Inbox.Application
EncodeLabs.Inbox.Infrastructure
EncodeLabs.Inbox.Api
```

Dentro de:

```text
tests/
```

crear:

```text
EncodeLabs.Inbox.Domain.Tests
```

---

## Tipo de proyecto

### Domain

```text
Class Library
```

Debe contener únicamente código de dominio.

No debe tener referencias a Infrastructure, API ni frameworks de infraestructura.

---

### Application

```text
Class Library
```

Debe depender de:

```text
Domain
```

Debe contener posteriormente:

- Casos de uso.
- DTOs.
- Interfaces de infraestructura.
- Validaciones.
- Contratos.

No implementar lógica funcional todavía.

---

### Infrastructure

```text
Class Library
```

Debe depender de:

```text
Application
Domain
```

Será responsable posteriormente de:

- Entity Framework Core.
- PostgreSQL.
- RabbitMQ.
- Telegram Bot API.
- Implementaciones externas.
- Persistencia.

---

### API

```text
ASP.NET Core Web API
```

Debe depender de:

```text
Application
Infrastructure
```

Debe contener:

- Configuración HTTP.
- Dependency Injection.
- Controllers.
- SignalR Hubs posteriormente.
- Health checks.

No colocar lógica de negocio en `Program.cs` ni en controllers.

---

# Referencias esperadas

Las referencias entre proyectos deben quedar:

```text
Domain
   ↑
Application
   ↑
Infrastructure

Application
   ↑
Api

Infrastructure
   ↑
Api
```

Más concretamente:

```text
Application → Domain
Infrastructure → Application
Infrastructure → Domain
Api → Application
Api → Infrastructure
Tests → Domain
```

No crear dependencias inversas.

---

# PostgreSQL

Agregar soporte para PostgreSQL utilizando:

```text
Entity Framework Core
Npgsql.EntityFrameworkCore.PostgreSQL
```

Crear una infraestructura mínima para futura persistencia.

Por ejemplo:

```text
Infrastructure/
└── Persistence/
    └── AppDbContext.cs
```

No crear aún entidades finales ni migraciones de negocio.

El `DbContext` puede quedar preparado inicialmente sin entidades.

Registrar el DbContext mediante Dependency Injection.

La connection string debe obtenerse desde configuración.

Ejemplo esperado:

```text
ConnectionStrings__Postgres
```

No hardcodear credenciales.

---

# RabbitMQ

Agregar el cliente oficial de RabbitMQ para .NET.

Preparar únicamente la configuración base.

Crear opciones/configuración para:

```text
Host
Port
Username
Password
```

No implementar todavía:

- Publishers.
- Consumers.
- Exchanges.
- Queues.
- Retry.
- Dead Letter Queue.

Eso se implementará en una feature posterior.

La aplicación debe poder arrancar aunque todavía no exista procesamiento de mensajes.

---

# SignalR

Agregar soporte para SignalR en el backend.

Registrar SignalR en Dependency Injection.

No crear todavía lógica de negocio ni eventos reales.

Puede crearse una estructura vacía:

```text
Api/
└── Hubs/
```

No implementar todavía el hub funcional si no es necesario.

---

# Logging

Configurar logging estructurado.

Preferentemente usar:

```text
Serilog
```

Configurar al menos salida por consola.

Evitar configuración excesivamente compleja.

Nunca loggear secretos.

---

# Health Check

Crear endpoint:

```http
GET /health
```

Debe devolver un resultado exitoso cuando la API está levantada.

Si es sencillo de implementar sin agregar complejidad innecesaria, agregar checks para:

- PostgreSQL
- RabbitMQ

Si todavía no están disponibles al momento del arranque, manejarlo correctamente mediante Docker health checks o configuración de dependencias.

No bloquear innecesariamente el inicio del backend.

---

# Frontend

Crear aplicación Angular dentro de:

```text
web/
```

Nombre sugerido:

```text
encodelabs-inbox
```

Usar:

- Última versión estable de Angular.
- Standalone Components.
- Routing.
- SCSS.

No agregar NgRx.

No agregar librerías innecesarias.

---

## Estructura inicial

Crear una estructura simple:

```text
src/app/
├── core/
│   ├── models/
│   └── services/
│
├── features/
│   └── inbox/
│       ├── components/
│       ├── pages/
│       ├── models/
│       └── services/
│
└── shared/
```

No crear componentes vacíos sin utilidad solamente para llenar carpetas.

Crear únicamente lo necesario para dejar la estructura preparada.

---

## Página inicial

Crear una página base del inbox.

Por ahora debe ser solamente una pantalla mínima que permita comprobar que Angular funciona.

Ejemplo:

```text
EncodeLabs Inbox
Bandeja conversacional
```

No implementar todavía:

- Lista de conversaciones.
- Historial.
- SignalR.
- Envío de mensajes.

---

# Comunicación frontend/backend

Configurar el frontend para poder consumir la API posteriormente.

Crear configuración de environment o equivalente para:

```text
apiUrl
```

No hardcodear múltiples URLs a lo largo del frontend.

---

# Docker

Crear:

```text
docker-compose.yml
```

con los siguientes servicios:

```text
postgres
rabbitmq
api
frontend
ngrok
```

---

## PostgreSQL

Usar una imagen oficial estable.

Configurar mediante variables provenientes de `.env`.

Crear volumen persistente.

Agregar health check.

---

## RabbitMQ

Usar:

```text
rabbitmq:management
```

Exponer:

```text
5672
15672
```

Configurar usuario y password mediante `.env`.

Agregar health check.

---

## API

Crear `Dockerfile` para el backend.

Debe:

- Restaurar dependencias.
- Compilar.
- Publicar.
- Ejecutar la API.

No ejecutar en modo development dentro del contenedor final.

Exponer un puerto interno consistente, por ejemplo:

```text
8080
```

---

## Frontend

Crear `Dockerfile` para Angular.

Usar multi-stage build.

Build:

```text
Node
```

Runtime:

```text
nginx
```

El contenedor final no debe ejecutar `ng serve`.

Exponer:

```text
80
```

y mapearlo a un puerto local documentado.

---

## ngrok

Agregar servicio ngrok.

Debe leer:

```text
NGROK_AUTHTOKEN
```

desde `.env`.

Debe exponer públicamente el backend.

No registrar todavía el webhook en Telegram en esta feature.

Sí dejar ngrok funcionando y accesible para que una feature posterior pueda consultar su API local.

Exponer si es necesario:

```text
4040
```

para la API/UI de ngrok.

---

# Variables de entorno

Actualizar:

```text
.env.example
```

con todas las variables necesarias.

Ejemplo:

```env
TELEGRAM_BOT_TOKEN=
NGROK_AUTHTOKEN=

POSTGRES_DB=encodelabs_inbox
POSTGRES_USER=postgres
POSTGRES_PASSWORD=

RABBITMQ_DEFAULT_USER=guest
RABBITMQ_DEFAULT_PASS=

ConnectionStrings__Postgres=
RabbitMq__Host=
RabbitMq__Port=
RabbitMq__Username=
RabbitMq__Password=
```

Ajustar nombres si Docker Compose requiere otros valores.

Evitar duplicación innecesaria.

Nunca agregar valores reales.

---

# Git

Crear o actualizar `.gitignore`.

Debe excluir al menos:

```text
.env
bin/
obj/
node_modules/
dist/
.vs/
.vscode/
```

No ignorar:

```text
.env.example
```

---

# Docker Compose

El objetivo es que el proyecto pueda iniciarse con:

```bash
docker compose up --build
```

después de completar `.env`.

El arranque debe contemplar que:

- PostgreSQL puede tardar en estar disponible.
- RabbitMQ puede tardar en estar disponible.
- ngrok puede tardar en crear el túnel.

Usar health checks y dependencias cuando tenga sentido.

Evitar `sleep` arbitrarios si existe una alternativa mejor.

---

# Puertos sugeridos

Mantener una configuración simple y documentada.

Ejemplo:

```text
Frontend       http://localhost:4200
API            http://localhost:8080
RabbitMQ UI    http://localhost:15672
ngrok UI       http://localhost:4040
PostgreSQL     localhost:5432
```

Si se utilizan otros puertos, documentarlos.

---

# README

Actualizar el `README.md` únicamente con información correspondiente al bootstrap.

Incluir:

## Estado actual

Indicar claramente que esta feature crea la infraestructura inicial y que el flujo funcional de Telegram todavía no está implementado.

## Requisitos

Por ejemplo:

- Docker
- Docker Compose
- Token de ngrok para levantar el túnel

## Inicio

```bash
cp .env.example .env
docker compose up --build
```

## URLs

Documentar:

- Frontend
- API
- `/health`
- RabbitMQ Management
- ngrok UI

No documentar funcionalidades que todavía no existen.

---

# Validaciones

Al finalizar ejecutar:

## Backend

```bash
dotnet restore
dotnet build
dotnet test
```

Todos deben finalizar correctamente.

---

## Frontend

Ejecutar:

```bash
npm install
npm run build
```

Debe compilar sin errores.

---

## Docker

Ejecutar:

```bash
docker compose config
```

y validar que la configuración sea correcta.

Si el entorno permite ejecutar Docker:

```bash
docker compose up --build
```

y validar los servicios.

---

# Criterios de aceptación

La feature queda completa cuando:

- Existe `EncodeLabs.Inbox.sln`.
- Existen los cuatro proyectos backend.
- Las dependencias respetan Clean Architecture.
- Existe el proyecto de tests.
- Backend compila.
- Tests ejecutan correctamente.
- Existe la aplicación Angular.
- Angular compila.
- PostgreSQL está configurado.
- RabbitMQ está configurado.
- SignalR está registrado.
- Existe logging estructurado.
- Existe `/health`.
- Existe `docker-compose.yml`.
- Existen Dockerfiles para API y frontend.
- ngrok está incluido en Docker Compose.
- `.env.example` contiene las variables necesarias sin secretos.
- `.env` está ignorado.
- `docker compose config` funciona.
- README refleja el estado real del proyecto.

---

# Fuera de alcance

NO implementar todavía:

- Registro automático del webhook de Telegram.
- Endpoint funcional del webhook.
- Procesamiento de mensajes de Telegram.
- Publisher RabbitMQ.
- Consumer RabbitMQ.
- Dead Letter Queue.
- Contactos.
- Conversaciones.
- Mensajes.
- SignalR funcional.
- Bandeja conversacional.
- Envío de mensajes.
- Marcado como leído.

Estas funcionalidades serán implementadas en features posteriores.

---

# Resultado esperado

Al terminar debe existir una base limpia, compilable y ejecutable sobre la que se puedan desarrollar las siguientes features sin necesidad de reestructurar la solución.

No sobrearquitecturar.

No anticipar funcionalidades que todavía no fueron solicitadas.

Respetar `AGENTS.md` y los ADRs existentes.
