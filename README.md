# Enterprise Access Control Platform

Solución web para administrar el acceso físico a instalaciones empresariales: jerarquías de compañías y
unidades organizativas, personas con históricos de pertenencia y contexto operativo, permisos con vigencia
y bloques horarios, credenciales/fotocheck, y evaluación de acceso con denegación por defecto.

> **Estado del repositorio (2026-09-19):** esta es la primera subida a GitHub. El proyecto corresponde al
> cierre de la **Etapa 1** de la rama de funcionalidad
> [`001-control-acceso-empresarial`](specs/001-control-acceso-empresarial/) — implementada y con sus
> dominios de negocio validados por prueba automatizada — **pero el baseline todavía no está congelado**.
> Dos auditorías independientes (2026-09-16) concluyeron que el perímetro de autorización tiene un defecto
> crítico. Las 16 preguntas de negocio que ese gate dejó abiertas **ya fueron respondidas** (2026-09-20) y
> están formalizadas en `spec.md`, pero su implementación todavía no se ha construido. Ver [Estado actual del
> proyecto](#estado-actual-del-proyecto) y [Decisiones de la auditoría](#decisiones-de-la-auditoría-todas-resueltas)
> antes de asumir que cualquier parte de esto es definitiva.

## Índice

1. [Objetivo](#objetivo)
2. [Contexto de negocio](#contexto-de-negocio)
3. [Capacidades principales](#capacidades-principales)
4. [Arquitectura](#arquitectura)
5. [Modelo de autorización](#modelo-de-autorización)
6. [Principios de seguridad](#principios-de-seguridad)
7. [Stack tecnológico](#stack-tecnológico)
8. [Estructura del repositorio](#estructura-del-repositorio)
9. [Estrategia de testing](#estrategia-de-testing)
10. [Estado actual de las pruebas](#estado-actual-de-las-pruebas)
11. [Instrucciones de desarrollo](#instrucciones-de-desarrollo)
12. [Configuración y ejecución (producción)](#configuración-y-ejecución-producción)
13. [Estado actual del proyecto](#estado-actual-del-proyecto)
14. [Decisiones de la auditoría: todas resueltas](#decisiones-de-la-auditoría-todas-resueltas)
15. [Próximos pasos para cerrar el baseline](#próximos-pasos-para-cerrar-el-baseline)

## Objetivo

Construir una plataforma que centralice, con validación 100% server-side y denegación por defecto, quién
puede acceder físicamente a qué área de una instalación, en qué horario y bajo qué credencial — reemplazando
procesos manuales o dispersos de control de acceso con un modelo de datos explícito, auditable y con
históricos temporales íntegros.

## Contexto de negocio

La solución está orientada principalmente a empresas mineras y otros sectores industriales de gran escala,
donde coexisten:

- Una o varias **Compañías Principales/Mandantes**: la empresa propietaria de la operación que solicita el
  control de acceso. Cada una tiene su propio contexto organizacional (unidades organizativas y áreas de
  acceso) aislado del de cualquier otra Principal.
- Múltiples **Compañías Contratistas** que prestan servicios dentro de las instalaciones de una o varias
  Principales simultáneamente, mediante una relación explícita y con vigencia temporal
  (`RelaciónContratistaPrincipal`).

Una persona (típicamente de una Contratista) puede trabajar simultáneamente para varias Compañías
Principales, con unidad organizativa, permisos y credencial propios e independientes para cada una
(`ContextoOperativoPersonaPrincipal`). El histórico de compañía de pertenencia (el empleador) es
independiente de esos contextos operativos. El cese de la pertenencia de una persona con su empleador
dispara la **revocación automática en cascada** de todos los contextos, asignaciones de unidad organizativa
y credenciales que dependían de ella, preservando el histórico íntegro.

El detalle funcional completo, incluidas 12 sesiones de clarificación de negocio que corrigieron y afinaron
el modelo original, vive en [spec.md](specs/001-control-acceso-empresarial/spec.md).

## Capacidades principales

10 historias de usuario (8 de prioridad P1, 2 de prioridad P2):

| # | Historia | Prioridad |
|---|---|---|
| 1 | Inicio de sesión y alcance de gestión por compañía | P1 |
| 2 | Compañías (Principal/Contratista), sus relaciones y unidades organizativas | P1 |
| 3 | Datos maestros (tipo de documento, sangre, género, tipo de persona, tipo de credencial) | P1 |
| 4 | Personas: registro de datos personales, de identificación y contacto | P1 |
| 5 | Históricos de compañía, contexto operativo por Principal, unidad organizativa y perfil, con revocación automática | P1 |
| 6 | Árbol de áreas físicas de acceso | P1 |
| 7 | Tipos de persona autorizados por área | P1 |
| 8 | Permisos de acceso con vigencia, bloques horarios y evaluación de acceso (motor de 15 pasos) | P1 |
| 9 | Mantenimiento de credencial/fotocheck por Compañía Principal | P2 |
| 10 | Auditoría y consultas transversales (histórico, indicadores) | P2 |

Las historias 1 a 8 están implementadas y respaldadas por prueba automatizada. La Historia 9 tiene backend
completo pero **sin interfaz de usuario** (ver [Estado actual](#estado-actual-del-proyecto)). La Historia 10
(auditoría/consultas transversales) tiene su necesidad funcional declarada en `spec.md` (RF-067 a RF-069)
pero **sin contrato de API, código ni pantalla**, y quedó **diferida a la Etapa 2** por la decisión D8 — está
anotada como tal en `spec.md` (ver [Decisiones de la auditoría](#decisiones-de-la-auditoría-todas-resueltas)).

## Arquitectura

Aplicación web con frontend y backend separados. El backend sigue una arquitectura en capas:

```
Dominio → Aplicación → Infraestructura → API
```

- **Dominio**: entidades, enums y reglas de negocio puras, sin dependencias externas.
- **Aplicación**: casos de uso por módulo (auth, maestros, compañías, unidades organizativas, personas,
  áreas de acceso, permisos, evaluación de acceso, credenciales), DTOs y validación con FluentValidation.
- **Infraestructura**: `DbContext` de EF Core sobre SQL Server, migraciones (incluidos los triggers SQL de
  no-solapamiento temporal), el interceptor de auditoría automática, y los mecanismos de seguridad
  (hashing de contraseñas, alcance de compañías).
- **API**: controladores ASP.NET Core, autenticación JWT, `ProblemDetails` para errores (RFC 7807/9457),
  health checks, composición de dependencias.

El frontend es una SPA React organizada por *features* (uno por módulo de negocio, más `dashboard`,
`historicos` y `audit` para las vistas transversales de la Historia 10), con un sistema de diseño propio
("Enterprise Operations Console") documentado en [ux-ui.md](specs/001-control-acceso-empresarial/ux-ui.md):
16 pantallas/flujos críticos, accesibilidad WCAG 2.2 AA, y componentes accesibles por teclado para los
árboles jerárquicos (patrón ARIA `treeview`).

La API y la SPA se despliegan como contenedores Docker independientes, detrás de un proxy inverso propio que
sirve la SPA como estáticos y reenvía `/api/` y `/health/` a la API (mismo origen, sin CORS). Ver
[Configuración y ejecución](#configuración-y-ejecución-producción).

Detalle completo de las decisiones de arquitectura, incluidas las correcciones de dominio y sus
re-chequeos de constitución: [plan.md](specs/001-control-acceso-empresarial/plan.md) y
[research.md](specs/001-control-acceso-empresarial/research.md) (26 secciones técnicas).

## Modelo de autorización

El sistema separa explícitamente dos mecanismos de autorización (research.md §18):

1. **Autorización de plataforma** (ASP.NET Core Authentication/Authorization): valida la sesión (JWT
   Bearer) y aplica el **alcance administrativo** del usuario autenticado, derivado de sus
   `AsignaciónRolAdministrativo` vigentes — rol `GLOBAL_ADMINISTRATOR` (todas las compañías) o
   `COMPANY_ADMINISTRATOR` (las compañías de sus asignaciones). El alcance viaja dentro del token; cambiarlo
   exige volver a iniciar sesión.
2. **Motor de evaluación de acceso de dominio** (Historia 8): un algoritmo de **15 pasos**, independiente de
   la autorización de plataforma, que determina si una persona puede acceder físicamente a un área en una
   fecha/hora dada. Determina primero la Compañía Principal propietaria del área y exige que esté `ACTIVA`;
   luego un contexto operativo vigente con esa Principal —con su compañía de pertenencia también `ACTIVA`, y
   una relación Contratista↔Principal vigente cuando aplica—, una credencial vigente para esa misma
   Principal, que el área esté activa, elegibilidad de perfil en el área, y finalmente el permiso aplicable
   con mayor precedencia (`PERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA`) dentro de su vigencia y de su bloque
   horario, evaluado en la zona horaria de esa Compañía Principal. Cualquier paso no satisfecho deniega el
   acceso por defecto. El detalle paso a paso está en `spec.md` Historia 8 y `research.md` §7.

**Defecto crítico confirmado, no resuelto todavía:** el mecanismo (1) — el alcance de compañías sobre la
propia administración de usuarios — **no está aplicado hoy en el código**. `UsuarioService` no filtra
ninguna de sus 7 operaciones por alcance, lo que permite en ejecución que cualquier usuario autenticado se
conceda compañías ajenas, liste todos los usuarios del sistema y modifique el alcance de terceros. Este es
el motivo por el que el baseline de Etapa 1 no puede congelarse todavía; ver
[Estado actual del proyecto](#estado-actual-del-proyecto).

## Principios de seguridad

El proyecto se gobierna por una [Constitución](.specify/memory/constitution.md) versionada (v1.1.1) con
siete principios fundamentales, no negociables salvo enmienda explícita:

| Principio | Resumen |
|---|---|
| I. Seguridad Server-Side, Denegación por Defecto y Autorización por Compañías | Toda decisión de acceso se evalúa en el servidor; el cliente nunca es frontera de seguridad; sin match válido, se deniega. |
| II. Identificadores Únicos Autogenerados (UID/UUID) | Toda entidad persistente usa un `Guid` generado por el sistema, nunca aceptado del cliente ni derivado de datos de negocio. |
| III. Auditoría Automática y Trazabilidad | Toda entidad persistente registra automáticamente quién y cuándo la creó/modificó, sin intervención del cliente. |
| IV. Integridad Temporal e Históricos | Vigencias con inicio/fin explícitos, sin solapamientos inválidos; los históricos nunca se eliminan físicamente. |
| V. Jerarquías sin Ciclos | Las jerarquías de unidades organizativas y áreas de acceso se validan server-side para impedir ciclos. |
| VI. Modelado Explícito del Dominio | Personas, compañías, jerarquías, perfiles y credenciales se modelan como entidades y relaciones explícitas, nunca como campos libres. |
| VII. Pruebas Automatizadas Obligatorias (NO NEGOCIABLE) | Toda funcionalidad de seguridad, autorización, reglas temporales, jerarquías y auditoría requiere prueba automatizada antes de darse por completa. |

Los principios II a VII están verificados como cumplidos (`PASS`) en el re-chequeo de constitución de
`plan.md`. El **Principio I tiene una violación activa y confirmada** en el perímetro de administración de
usuarios (ver [Modelo de autorización](#modelo-de-autorización)); el resto de la superficie del sistema
(evaluación de acceso, jerarquías, históricos) sí lo cumple.

## Stack tecnológico

**Backend**

- .NET 10 LTS + C# 13, ASP.NET Core 10 Web API
- Entity Framework Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`) + Migrations
- SQL Server (motor de base de datos oficial; columnas `datetime2(3)` en UTC)
- FluentValidation, NodaTime (zonas horarias IANA por Compañía Principal para los bloques horarios, con una
  zona global de respaldo configurable)
- ASP.NET Core Identity (`PasswordHasher<T>`) solo para hashing de contraseñas
- ASP.NET Core Authentication (JWT Bearer) + Authorization (Policies) para el alcance administrativo
- `Microsoft.AspNetCore.OpenApi` nativo (documento OpenAPI, solo en `Development`)
- `ProblemDetails` (RFC 7807/9457) como formato de error HTTP
- ASP.NET Core Health Checks (`AspNetCore.HealthChecks.SqlServer`)

**Frontend**

- React 18 + TypeScript 5.6+ / Vite
- TanStack Query (`@tanstack/react-query`) para estado de servidor
- React Hook Form + Zod para formularios y validación de cliente
- React Router para enrutamiento de la SPA
- Axios como cliente HTTP

**Testing**

- Backend: xUnit + FluentAssertions + `Testcontainers.MsSql` (integración contra SQL Server real —
  imprescindible para el trigger de no-solapamiento, que no tiene representación en el modelo de EF Core) +
  pruebas de contrato contra el OpenAPI generado
- Frontend: Vitest + React Testing Library (unitario/componentes) + Playwright (end-to-end)

**Infraestructura**

- Docker / Docker Compose v2 (SQL Server oficial `mcr.microsoft.com/mssql/server` + API contenedorizada)
- Sin proveedor de identidad externo, sin mensajería/colas, sin caché distribuido, sin microservicios —
  decisión arquitectónica explícita mientras ningún requisito lo justifique (constitución, Reglas de
  Arquitectura e Ingeniería).

## Estructura del repositorio

```text
backend/
├── src/
│   ├── EnterpriseAccessControl.Domain/            # Entidades, enums, reglas de dominio puras
│   ├── EnterpriseAccessControl.Application/       # Casos de uso, DTOs, validación (por módulo)
│   ├── EnterpriseAccessControl.Infrastructure/    # DbContext, migraciones EF Core, auditoría, seguridad
│   └── EnterpriseAccessControl.Api/               # Controllers, JWT, ProblemDetails, health checks
└── tests/
    ├── EnterpriseAccessControl.UnitTests/         # Dominio + Aplicación, sin base de datos
    ├── EnterpriseAccessControl.IntegrationTests/  # Testcontainers.MsSql
    └── EnterpriseAccessControl.ContractTests/     # Verifica la API contra contracts/*.yaml

frontend/
├── src/
│   ├── features/                                  # Un módulo por historia de negocio
│   ├── components/                                # Sistema de diseño (ux-ui.md)
│   ├── app/                                        # Enrutamiento, providers, guards de sesión
│   └── lib/                                        # Cliente HTTP tipado, utilidades de zona horaria
└── tests/
    ├── unit/                                       # Vitest + React Testing Library
    └── e2e/                                        # Playwright

specs/001-control-acceso-empresarial/
├── spec.md            # Especificación funcional (73 RF, 35 criterios de éxito, historial de clarificaciones)
├── plan.md             # Plan de implementación y re-chequeos de constitución
├── research.md         # 26 secciones de decisiones técnicas
├── data-model.md       # 22 entidades de dominio
├── ux-ui.md            # Especificación de UX/UI (16 pantallas, sistema de diseño)
├── quickstart.md        # Guía de validación end-to-end
├── tasks.md             # 168 tareas de implementación, organizadas por historia
└── contracts/           # 10 contratos OpenAPI (uno por grupo funcional)

docs/auditorias/         # Auditorías de cierre de Etapa 1 (ver Estado actual y Decisiones pendientes)
scripts/                 # Scripts de verificación (health checks de Docker)
.specify/                # Constitución del proyecto y plantillas de Spec Kit
docker-compose.yml        # Entorno de desarrollo local
docker-compose.prod.yml   # Despliegue de producción
```

## Estrategia de testing

Conforme al Principio VII (no negociable), cada historia de usuario P1 cuenta con pruebas automatizadas
independientes, con cobertura explícita de denegación por defecto, ciclos jerárquicos, solapamientos
temporales inválidos y fuga de datos entre compañías:

- **Unitarias** (xUnit + FluentAssertions): reglas de dominio y casos de uso de Aplicación, sin base de
  datos.
- **Integración** (Testcontainers.MsSql): contra una instancia real de SQL Server — necesarias para
  verificar los triggers de no-solapamiento temporal, que no tienen representación en el modelo de EF Core
  y no pueden probarse con un proveedor en memoria.
- **Contrato**: verifican que la API (incluido el formato `ProblemDetails`) coincide con los 10 archivos
  OpenAPI publicados en `contracts/`.
- **Frontend unitario/componentes** (Vitest + React Testing Library): incluye accesibilidad WCAG 2.2 AA.
- **End-to-end** (Playwright): flujos P1 completos contra la API real, con base de datos dedicada
  (`EnterpriseAccessControl_E2E`).

No hay todavía un pipeline de integración continua configurado en el repositorio (sin `.github/workflows`);
las suites se ejecutan localmente según [Instrucciones de desarrollo](#instrucciones-de-desarrollo).

## Estado actual de las pruebas

Verificado directamente en este repositorio el 2026-09-19 (build y suites rápidas, sin requerir Docker):

| Suite | Resultado |
|---|---|
| `dotnet build` (solución completa) | ✅ 0 errores, 0 advertencias |
| Backend — Unitarias (`EnterpriseAccessControl.UnitTests`) | ✅ 92/92 |
| Frontend — `npm run build` (`tsc -b && vite build`) | ✅ sin errores |
| Frontend — Vitest (`npm run test`) | ✅ 141/141 (16 archivos) |

Las suites que requieren Docker/SQL Server real (integración, contrato) y Playwright (end-to-end) no se
re-ejecutaron en esta verificación. Según la última auditoría de cierre de Etapa 1
([`auditoria-final-cierre-2026-09-16.html`](docs/auditorias/auditoria-final-cierre-2026-09-16.html),
2026-09-16) estaban en:

| Suite | Resultado (auditoría 2026-09-16) |
|---|---|
| Backend — Integración (`EnterpriseAccessControl.IntegrationTests`) | 485/485 |
| Backend — Contrato (`EnterpriseAccessControl.ContractTests`) | 242/242 |
| Frontend — E2E (Playwright) | 3/3 |

Ningún archivo de `specs/`, `backend/` ni `frontend/` se modificó entre esa auditoría y esta verificación, por
lo que esos números siguen siendo representativos, pero no fueron re-confirmados en ejecución hoy.

**Importante:** que todas las suites estén en verde no significa que el sistema esté libre de defectos. La
misma auditoría reprodujo en ejecución una escalada de privilegios y fugas de datos entre compañías (ver
abajo) que **ninguna prueba automatizada existente cubre todavía** — son huecos de cobertura, no
regresiones.

## Instrucciones de desarrollo

Prerrequisitos: .NET 10 SDK, Node.js 20+ (validado con Node.js 24), Docker Desktop (o motor compatible),
herramienta `dotnet-ef` (`dotnet tool install --global dotnet-ef`).

Antes del primer arranque, define `BOOTSTRAP_ADMIN_PASSWORD` con la contraseña del administrador inicial
(RF-078): la API no arranca sin ella —ni en desarrollo ni en producción— y `docker-compose.yml` no declara
ningún valor por defecto, para que el secreto nunca quede versionado en el repositorio. La forma más simple es
un archivo `.env` local junto a `docker-compose.yml` (ya excluido por `.gitignore`), que Docker Compose lee
automáticamente:

```bash
echo 'BOOTSTRAP_ADMIN_PASSWORD=<elige-una-contraseña-que-cumpla-la-política-de-contraseñas>' > .env
```

```bash
# Base de datos y API (contenedorizadas)
docker compose up -d --build

# Frontend
cd frontend
npm install
npm run dev
```

Guía paso a paso, incluidos dos escenarios de validación funcional completos (CS-009 y el escenario
multi-Principal de Pedro García/Servicios ACME): [quickstart.md](specs/001-control-acceso-empresarial/quickstart.md).

Pruebas:

```bash
cd backend
dotnet test tests/EnterpriseAccessControl.UnitTests
dotnet test tests/EnterpriseAccessControl.IntegrationTests   # requiere Docker (Testcontainers)
dotnet test tests/EnterpriseAccessControl.ContractTests

cd ../frontend
npm run test        # Vitest
npm run test:e2e    # Playwright — requiere Docker, dotnet-ef y `npx playwright install chromium`
```

`npm run test:e2e` levanta una base de datos dedicada `EnterpriseAccessControl_E2E` reutilizando los
contenedores de desarrollo; tras ejecutarlo, correr `docker compose up -d` de nuevo para que la API vuelva a
apuntar a la base de desarrollo.

## Configuración y ejecución (producción)

El despliegue de producción usa [docker-compose.prod.yml](docker-compose.prod.yml). Resumen (detalle
completo, incluidas todas las variables de entorno, en el propio archivo y en `quickstart.md`):

1. **Requisitos**: host Linux con Docker Compose v2, licencia de SQL Server (la edición Developer no se
   permite en producción), .NET 10 SDK + `dotnet-ef` para aplicar migraciones, Node.js para compilar la SPA,
   proxy inverso propio con TLS.
2. **Variables de entorno obligatorias**: `MSSQL_SA_PASSWORD`, `MSSQL_PID`,
   `SQLSERVER_CONNECTION_STRING`, `JWT_SIGNING_KEY` (≥32 caracteres), `BOOTSTRAP_ADMIN_EMAIL` y
   `BOOTSTRAP_ADMIN_PASSWORD` (RF-078; la contraseña **nunca** se versiona y debe cumplir la política
   vigente). Deben definirse en un `.env.prod` fuera de control de versiones o en el gestor de secretos del
   host; si falta alguna, compose no arranca y la API tampoco.
3. **Política de contraseñas** (`PASSWORD_*`): configurable por variable de entorno y **aprobada como
   definitiva** — la Decisión de negocio #1 se cerró el 2026-09-20 ratificando los valores actualmente
   configurados como autoridad única del baseline, sin umbrales especiales para ningún usuario y sin
   mecanismo adicional de recuperación.
4. **Migraciones**: la API **no las aplica al arrancar**; se ejecutan manualmente con `dotnet ef database
   update` apuntando a SQL Server antes de levantar la API.
5. **Primer usuario administrador**: la especificación define un **arranque automático** (RF-078, decisión
   D2 cerrada el 2026-09-20). En el primer arranque, si no existe ninguna asignación `GLOBAL_ADMINISTRATOR`,
   la API crea el primer administrador tomando su correo de `Bootstrap:AdminEmail` y su contraseña de
   `Bootstrap:AdminPassword` (variable de entorno `Bootstrap__AdminPassword` donde no haya gestor de
   secretos). La contraseña **nunca** se versiona en el repositorio, debe cumplir la política de contraseñas
   vigente y su cambio es obligatorio en el primer inicio de sesión. La rutina es idempotente: reiniciar la
   API no crea un segundo administrador. **Ambas variables son obligatorias**: sin ellas la API no arranca,
   en lugar de levantar con un administrador adivinable.
6. **SPA y proxy inverso**: `npm run build` genera `frontend/dist/`; el proxy debe servir esos estáticos,
   reenviar `/api/` y `/health/` a la API, y devolver `index.html` para rutas de cliente. La API no configura
   CORS: la SPA debe consumirla en el mismo origen.
7. **Verificación**: `GET /health/live` (proceso vivo) y `GET /health/ready` (conecta con SQL Server;
   `503` si no responde). [scripts/verificar-health-docker.sh](scripts/verificar-health-docker.sh) automatiza
   esta verificación sobre el compose de desarrollo.

## Estado actual del proyecto

**Etapa 1 implementada, incluido el cierre del baseline.** Dos auditorías independientes
([`gate-cierre-etapa1-2026-09-16.html`](docs/auditorias/gate-cierre-etapa1-2026-09-16.html) y
[`auditoria-final-cierre-2026-09-16.html`](docs/auditorias/auditoria-final-cierre-2026-09-16.html))
concluyeron el 2026-09-16 **"No congelable" / "No cerrada"**, verificando en ejecución y no solo en código.
Las nueve decisiones que lo bloqueaban se cerraron el 2026-09-20 y las tareas T169 a T228 que las
implementan están construidas; el gate de cierre debe repetirse contra este código para congelar el
baseline formalmente.

**1. Implementado y validado**

- Las 8 historias P1 (login/alcance, compañías/relaciones, maestros, personas, históricos/contexto
  operativo/revocación automática, árbol de áreas, elegibilidad por tipo de persona, motor de evaluación de
  acceso — **los 15 pasos completos** desde la sesión de implementación del 2026-09-20, incluida la
  verificación de `Compañía.Estado` en los pasos 5 y 6 (RF-079)).
- Modelo de datos completo (22 entidades, 11 migraciones EF Core aplicadas), reglas temporales
  (contención, no-solapamiento vía trigger SQL, vigencias obligatorias), cascada de revocación automática,
  auditoría automática por interceptor, aislamiento de datos entre Compañías Principales.
- 168/168 tareas de `tasks.md` completadas y verificadas contra el repositorio, salvo dos excepciones
  documentales (ver "Correcciones pendientes").
- Los 45 endpoints de los 10 contratos OpenAPI existen y responden en el código — paridad contrato↔código
  verificada por la auditoría de cierre del 2026-09-16, no re-confirmada en esta subida.

**2. Cierre de Etapa 1 — implementado en la sesión del 2026-09-20** (tareas T169 a T228 de `tasks.md`)

Las decisiones de negocio que bloqueaban el baseline se cerraron el 2026-09-20, se formalizaron en `spec.md`
(RF-074 a RF-081, CS-036 a CS-041) y se construyeron a continuación:

- Modelo de administración de usuarios: RBAC con roles `GLOBAL_ADMINISTRATOR` y `COMPANY_ADMINISTRATOR`,
  alcance por rol y asignaciones con vigencia auditable — entidad `AsignacionRolAdministrativo`, que
  reemplaza al antiguo `AlcanceUsuarioCompañía` (RF-074 a RF-077).
- Alta automática del primer administrador en un despliegue nuevo, idempotente y por configuración
  segura (RF-078).
- Calendario de vigencias: zona horaria IANA por Compañía Principal, con zona global de respaldo (RF-080).
- Estado de la compañía como condición dinámica de la evaluación de acceso (RF-079) y dependencias que
  bloquean el cambio de `TipoCompañía` (RF-081).
- Interfaz de administración de usuarios y roles (UX-17 a UX-22) e interfaz completa de Historia 5
  —wizard de pertenencia y contexto, árbol de unidad organizativa, perfiles—, que antes solo era
  operable vía API.
- Las decisiones heredadas de `spec.md` (#1 política de contraseñas, #3 retención legal, #6 inactivación de
  compañía, #7 prioridad de Historia 9) quedaron **todas cerradas**; ninguna requirió trabajo técnico salvo
  #6, absorbida por RF-079.

**3. Correcciones de la auditoría ya aplicadas**

- **Crítico (F-01), corregido**: `UsuarioService` aplica Resource Ownership en todas sus operaciones. Un
  `COMPANY_ADMINISTRATOR` no lista ni modifica usuarios de otra compañía, no se autoeleva y no puede crear
  asignaciones `GLOBAL_ADMINISTRATOR`; una lectura fuera de alcance responde `404` (CS-036, CS-037).
- **F-02, corregido**: el alcance efectivo se deriva del rol vigente, de modo que el control alcanza por
  igual a los servicios que lo delegaban en `CompaniaService` y a los de histórico de personas, cuyo
  alcance sobre una `Persona` se resuelve ahora por unión de pertenencia y contexto operativo (RF-077).
- Una compañía `INACTIVA` deniega el acceso de forma dinámica y reversible, sin cascada de escritura
  (RF-079).

**4. Stage 2 / futuro**

- Historia 10 (auditoría y consultas transversales, RF-067 a RF-069, CS-032): diferida explícitamente a
  Etapa 2 por la decisión D8. No se generó ninguna tarea ni código para ella en este cierre.

## Decisiones de la auditoría: todas resueltas

La auditoría de cierre agrupó 23 hallazgos en 9 decisiones (D1–D9) y una matriz de **16 preguntas de
negocio**. **Las 16 quedaron respondidas el 2026-09-20** y las nueve decisiones están formalizadas en
`spec.md` (RF-074 a RF-081, CS-036 a CS-041, y las diez Decisiones Pendientes cerradas). Ninguna sigue
bloqueando el baseline: lo que resta es implementación, no decisión.

| Decisión | Tema | Resolución |
|---|---|---|
| D1 | Modelo de administración de usuarios | RBAC con catálogo cerrado de dos roles, alcance GLOBAL o por compañía, asignaciones con vigencia auditable (RF-074 a RF-077) |
| D2 | Alta del primer administrador | Arranque automático idempotente con credenciales por configuración segura y cambio forzado en el primer login (RF-078) |
| D3 | Aislamiento por alcance | Cadena de autorización con verificación del recurso concreto; una `Persona` está en alcance por su compañía de pertenencia o por un contexto operativo vigente (RF-077) |
| D4 | Inactivación de una Compañía | Denegación por evaluación dinámica, sin cascada de escritura, reversible (RF-079; cierra la Decisión #6) |
| D5 | Calendario de vigencias | Zona horaria IANA por Compañía Principal, con zona global de respaldo; UTC siempre persistido (RF-080) |
| D6 | Reclasificar el tipo de una Compañía | Rechazo si hay dependencias incompatibles, simétrico en ambas direcciones, sin cascada (RF-081) |
| D7 | Interfaz de Historia 5 | Se corrige y completa dentro de Etapa 1, no se traslada |
| D8 | Consultas transversales (RF-067 a RF-069, CS-032) | Diferidas a Etapa 2, anotadas en `spec.md`, sin tareas de baseline |
| D9 | Heredadas #1, #3 y #7 | Política de contraseñas actual ratificada; sin retención ni purga en el baseline; Historia 9 se mantiene en P2 |

Detalle de cada decisión, sus alternativas y su análisis de impacto:
[`decisiones-etapa1-2026-09-16.html`](docs/auditorias/decisiones-etapa1-2026-09-16.html) (narrativo),
[`clasificacion-hallazgos-2026-09-16.html`](docs/auditorias/clasificacion-hallazgos-2026-09-16.html)
(tabular, con los 23 hallazgos F-01 a F-23 y la matriz) y
[`analisis-consistencia-2026-09-20.html`](docs/auditorias/analisis-consistencia-2026-09-20.html) (análisis de
consistencia posterior). **Las dos páginas del 16/09 conservan a propósito el estado previo a las respuestas**,
como registro histórico.

## Próximos pasos para cerrar el baseline

Secuencia acordada en la auditoría de cierre para llegar a un baseline estable de Etapa 1:

1. ~~**Sesión de decisiones de negocio** — responder las 16 preguntas pendientes.~~ **Completado el
   2026-09-20**: las 16 respondidas, las nueve decisiones formalizadas en `spec.md` (RF-074 a RF-081) y el
   diseño técnico en `plan.md`/`research.md`/`data-model.md`/`contracts/`.
2. **Corregir el perímetro de autorización** (F-01, F-02) aplicando el modelo de administración de
   usuarios ya decidido (D1) y el aislamiento por alcance (D3), con pruebas de integración nuevas por
   operación. ← **siguiente paso**
3. **Correcciones técnicas que no requieren decisión** (F-05, F-06, F-10, F-12, F-14, F-22, visualización de
   fechas de F-08, entre otras).
4. **Sincronizar la documentación** (`spec.md`, `research.md`, `data-model.md`, `contracts/`, `ux-ui.md`,
   `tasks.md`) con las decisiones tomadas y las correcciones aplicadas.
5. **Regresión completa** de las cinco suites de prueba y repetición del gate de cierre.
6. **Congelar la línea base de Etapa 1** y abrir formalmente la Etapa 2 con lo que se haya decidido
   trasladar (Historia 10, y lo que corresponda de Historia 5).

No se ha ejecutado ningún paso de esta secuencia todavía: este README documenta el punto de partida, no un
avance sobre él.
