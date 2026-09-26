---

description: "Task list template for feature implementation"
---

# Tasks: Solución Web de Control de Acceso Empresarial

> **Regenerado íntegramente (Sesión 2026-09-14)**: la versión anterior de este documento (158 tareas) quedó
> obsoleta por la extensa evolución del dominio ocurrida después de generarla: integración de `ux-ui.md`,
> RF-066 (la credencial gatilla la evaluación de acceso), RF-067 a RF-069 (consultas agregadas, aún sin
> contrato), RF-070 (vigencia temporal de credencial), RF-071 (fechas obligatorias en toda asociación
> temporal de persona), RF-072 (contención temporal jerárquica) y RF-073 (renovación de
> `AsignaciónPersonaCompañía`). Este documento se generó desde cero a partir de los artefactos vigentes:
> `spec.md` (RF-001 a RF-073, CS-001 a CS-035, Decisiones Pendientes #1 a #10), `plan.md`, `data-model.md`,
> `research.md` (§1-§26), `contracts/` y `quickstart.md`. Verificación de consistencia cruzada realizada
> antes de generar: sin contradicciones detectadas (ver informe de cierre al final de este documento).
>
> **Decisión de secuenciación explícita (no es un cambio de prioridad de negocio)**: `AsignaciónCredencial`
> (entidad, configuración EF Core, migración y trigger de no-solapamiento) se construye en **US5**, no en
> **US9**, porque RF-066 hace que el paso 6 del algoritmo de 14 pasos de `EvaluadorDeAcceso` (US8, P1)
> consulte esa entidad de forma incondicional — ya no es un enriquecimiento diferible. Esto es el mismo
> patrón ya usado para mover `Compañía` a Foundational en la generación anterior (una historia posterior
> necesitaba una entidad antes de que su propia fase la crease). La prioridad P2 de Historia 9 en spec.md
> **no cambia**: US9 sigue entregando únicamente la capacidad de negocio incremental (asignar/devolver/
> eliminar credencial vía API+UI); la Decisión Pendiente #7 (¿debe subir Historia 9 a P1?) permanece
> explícitamente sin resolver, sin que esta reorganización la prejuzgue — ver informe de cierre.

**Input**: Design documents from `/specs/001-control-acceso-empresarial/`

**Prerequisites**: [plan.md](./plan.md) (required), [spec.md](./spec.md) (required for user stories),
[research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/),
[quickstart.md](./quickstart.md)

**Tests**: Incluidas. El Principio VII de la Constitución (NO NEGOCIABLE) exige pruebas automatizadas para
seguridad, autorización por compañías, reglas temporales, jerarquías, permisos, credenciales, revocación
automática y auditoría; CS-008 exige pruebas automatizadas para toda historia P1 antes de liberar.

**Organization**: Las tareas están agrupadas por historia de usuario (spec.md) para permitir implementación
y prueba independientes de cada una.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede ejecutarse en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: Historia de usuario a la que pertenece la tarea (US1..US10)
- Cada descripción incluye la ruta de archivo exacta

## Stack tecnológico vigente (fuente: plan.md, research.md — sin cambios en esta regeneración)

C# / .NET 10 LTS · ASP.NET Core 10 Web API · Entity Framework Core 10 (proveedor
`Microsoft.EntityFrameworkCore.SqlServer`) · SQL Server · LINQ · EF Core Migrations (incluidos triggers
`AFTER INSERT, UPDATE` vía SQL crudo para no-solapamiento — research.md §5) · ASP.NET Core Authentication
(JWT Bearer) + Authorization Policies (`CompaniaScopeAuthorizationHandler`) · Options Pattern ·
`Microsoft.AspNetCore.OpenApi` nativo · `ProblemDetails` RFC 7807/9457 · `Microsoft.Extensions.Logging` ·
ASP.NET Core Health Checks · `rowversion` para concurrencia optimista · value conversions de enums a
`nvarchar` · Docker (`mcr.microsoft.com/mssql/server`) · xUnit + FluentAssertions + `Testcontainers.MsSql` ·
React 18 + Vite, TanStack Query, React Hook Form + Zod, React Router, sistema de diseño propio conforme a
`ux-ui.md` (design tokens, componentes base y de dominio, WCAG 2.2 AA) · Vitest + React Testing Library +
Playwright.

## Path Conventions (de plan.md)

- Backend en capas: `backend/src/EnterpriseAccessControl.{Domain|Application|Infrastructure|Api}/...`
- Backend pruebas: `backend/tests/EnterpriseAccessControl.{UnitTests|IntegrationTests|ContractTests}/...`
- Frontend: `frontend/src/{app|components|features}/...`, `frontend/tests/{unit|e2e}/...`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Inicialización del proyecto y estructura base (backend en capas + frontend) sobre el stack
vigente, conforme a Project Structure de [plan.md](./plan.md).

- [X] T001 Crear la solución backend con los cuatro proyectos en capas
      (`EnterpriseAccessControl.Domain`, `.Application`, `.Infrastructure`, `.Api`) y los tres proyectos de
      prueba (`.UnitTests`, `.IntegrationTests`, `.ContractTests`) en `backend/`, todos apuntando a
      **.NET 10 LTS**, con las referencias de proyecto correctas (Api → Infrastructure → Application →
      Domain; el Dominio no referencia EF Core ni ningún paquete de SQL Server — research.md §15)
- [X] T002 Crear el proyecto frontend con Vite + React 18 + TypeScript en `frontend/`, con las carpetas
      `src/app/`, `src/components/`, `src/features/`, `src/lib/`, `tests/unit/`, `tests/e2e/`
- [X] T003 [P] Agregar dependencias NuGet al backend: `Microsoft.EntityFrameworkCore`,
      `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`,
      `FluentValidation.AspNetCore`, `NodaTime`, `Microsoft.AspNetCore.Identity` (solo `PasswordHasher<T>`),
      `Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.Authentication.JwtBearer`,
      `AspNetCore.HealthChecks.SqlServer`, `xunit`, `FluentAssertions`, `Testcontainers.MsSql` en los
      `.csproj` correspondientes bajo `backend/`
- [X] T004 [P] Agregar dependencias npm al frontend: `react-router-dom`, `@tanstack/react-query`,
      `react-hook-form`, `zod`, `axios`, `vitest`, `@testing-library/react`, `@playwright/test` en
      `frontend/package.json`
- [X] T005 [P] Configurar linting y formato backend (`.editorconfig`, analizadores Roslyn) en `backend/`
- [X] T006 [P] Configurar linting y formato frontend (ESLint + Prettier) en `frontend/.eslintrc.cjs` y
      `frontend/.prettierrc`
- [X] T007 Configurar `docker-compose.yml` en la raíz del repositorio con SQL Server
      (`mcr.microsoft.com/mssql/server:2022-latest`, `ACCEPT_EULA=Y`) — ver [quickstart.md](./quickstart.md)
      paso 1; sin extensiones de servidor que habilitar manualmente
- [X] T008 [P] Configurar gestión de variables de entorno y Options Pattern base (cadena de conexión SQL
      Server, secreto/emisor/audiencia JWT, zona horaria empresarial) en
      `backend/src/EnterpriseAccessControl.Api/appsettings.json`,
      `appsettings.Development.json` y `frontend/.env.example`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Infraestructura núcleo que TODAS las historias de usuario requieren: auditoría automática
(Principio III), generación de UUID (Principio II), acceso a alcance de compañías (Principio I), detección
de ciclos (Principio V), `ProblemDetails`, distinción Authorization-vs-motor-de-dominio (research.md §18), y
la entidad base `Compañía` — ya con su clasificación `TipoCompañía` — requerida por `AlcanceUsuarioCompañía`
de US1 antes de que US2 construya su mantenimiento completo.

**⚠️ CRITICAL**: Ninguna historia de usuario puede comenzar hasta completar esta fase.

- [X] T009 [P] Crear abstracciones comunes de dominio: interfaz `IAuditable` (CreatedAt/UpdatedAt/
      CreatedById/UpdatedById), enum `Estado` (ACTIVO/INACTIVO) y generador de UUID v7
      (`Guid.CreateVersion7()`) en `backend/src/EnterpriseAccessControl.Domain/Common/` — el Dominio no
      referencia EF Core ni tipos de SQL Server (research.md §15)
- [X] T010 Crear el esqueleto de `AppDbContext` (proveedor SQL Server) y el `SaveChangesInterceptor` de
      auditoría (estampa `CreatedAt/UpdatedAt/CreatedById/UpdatedById` usando el usuario autenticado de la
      request, nunca aceptado del cliente — research.md §6) en
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/AppDbContext.cs` y
      `backend/src/EnterpriseAccessControl.Infrastructure/Auditing/AuditSaveChangesInterceptor.cs`
- [X] T011 Configurar el proveedor `Microsoft.EntityFrameworkCore.SqlServer`, generar la migración inicial
      vacía, y establecer la convención de value conversions para enums de negocio a `nvarchar`
      (`HasConversion<string>()`, research.md §17) en
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/` y
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/ModelConventions.cs`
- [X] T012 [P] Crear la entidad base `Compañía` (Id, Nombre, TipoDocumentoId, NumeroDocumento,
      `TipoCompañía` enum PRINCIPAL_MANDANTE/CONTRATISTA — RF-042, Estado, `RowVersion` + auditoría) con su
      configuración `IEntityTypeConfiguration<Compania>` y migración en
      `backend/src/EnterpriseAccessControl.Domain/Entities/Compania.cs` y
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Configurations/CompaniaConfiguration.cs`
      — necesaria como FK de `AlcanceUsuarioCompañía` (US1) y de `RelaciónContratistaPrincipal`/
      `ÁreaAcceso` (US2/US6) antes de que esas historias agreguen sus servicios/controladores completos
- [X] T013 [P] Implementar `IAlcanceCompaniaAccessor` e `IUsuarioActualAccessor` de ámbito de request
      (resuelven el alcance de compañías y el usuario autenticado desde los claims del JWT — research.md §3)
      en `backend/src/EnterpriseAccessControl.Infrastructure/Security/`
- [X] T014 [P] Configurar autenticación JWT Bearer y el `CompaniaScopeAuthorizationHandler` +
      `CompaniaScopeRequirement` de ASP.NET Core Authorization (gate de alcance administrativo de
      **primera línea**, a nivel de endpoint, distinto del motor de evaluación de acceso de dominio que
      vive en `EvaluadorDeAcceso` — research.md §18) en
      `backend/src/EnterpriseAccessControl.Api/Program.cs` y
      `backend/src/EnterpriseAccessControl.Infrastructure/Security/CompaniaScopeAuthorizationHandler.cs`
- [X] T015 [P] Implementar middleware de `ProblemDetails` (`AddProblemDetails()` + traducción de
      excepciones de validación de dominio al formato RFC 7807/9457 con la extensión `codigo` — research.md
      §21) en `backend/src/EnterpriseAccessControl.Api/Middleware/ExceptionHandlingMiddleware.cs`
- [X] T016 [P] Implementar helper de paginación genérico (`PaginaResponse<T>`, `pagina`, `tamañoPagina`)
      reutilizado por todos los endpoints de listado en
      `backend/src/EnterpriseAccessControl.Application/Common/PaginaResponse.cs`
- [X] T017 [P] Implementar helper de recorrido jerárquico vía CTE recursivo de SQL Server (LINQ traducido o
      `FromSqlRaw` si la traducción resulta insuficiente) con dos responsabilidades reutilizables por US2 y
      US6: (a) detección de ciclos (RF-038, Principio V) y (b) resolución del nodo raíz de un nodo dado
      (usado por US2 para resolver la Compañía Principal propietaria de una `UnidadOrganizativa` no raíz
      consultando `CompañíaPrincipalUnidadOrganizativaRaiz` sobre esa raíz — research.md §4) en
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/JerarquiaCicloValidator.cs`
- [X] T018 [P] Implementar servicio de reloj empresarial basado en NodaTime que resuelve conversiones UTC ↔
      `America/Lima` (research.md §5, usado por evaluación de acceso en US8) en
      `backend/src/EnterpriseAccessControl.Infrastructure/Security/RelojEmpresarial.cs`
- [X] T019 [P] Implementar la convención de índice clúster para entidades de histórico de alto volumen de
      inserción: PK `Id` como `NONCLUSTERED` + índice `CLUSTERED` sobre `CreatedAt`, aplicada a
      `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`,
      `AsignaciónCredencial`, `RelaciónContratistaPrincipal`, `HistorialContraseña` (research.md §20) en
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Configurations/ClusteredOnCreatedAtConvention.cs`
- [X] T020 [P] Implementar la convención de concurrencia optimista (`RowVersion` vía `[Timestamp]`/
      `.IsRowVersion()`, mapeado a `rowversion` de SQL Server) aplicada a `Usuario`,
      `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`, `AsignaciónCredencial`,
      `PermisoAcceso`, `RelaciónContratistaPrincipal`, y su traducción a `409 Conflict` con `ProblemDetails`
      ante `DbUpdateConcurrencyException` (research.md §16) en
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/RowVersionConvention.cs` y el
      middleware de T015
- [X] T021 [P] Configurar el shell de la aplicación frontend: enrutamiento, `QueryClientProvider` de
      TanStack Query, cliente HTTP con inyección de header de autenticación y manejo de errores
      `ProblemDetails` en `frontend/src/app/`
- [X] T022 [P] Implementar el componente de árbol accesible (patrón ARIA `treeview`, navegable por teclado,
      con desambiguación de nombres iguales y breadcrumb — RF-036, ux-ui.md §14, reutilizado por US2, US6)
      en `frontend/src/components/Tree/`
- [X] T023 [P] Configurar el fixture base de `Testcontainers.MsSql` para pruebas de integración (levanta una
      instancia real de SQL Server por corrida — imprescindible para verificar los triggers de
      no-solapamiento, que no tienen representación en el modelo de EF Core — research.md §11) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Fixtures/SqlServerFixture.cs`
- [X] T024 [P] Configurar la generación nativa de OpenAPI (`AddOpenApi()`/`MapOpenApi()` de
      `Microsoft.AspNetCore.OpenApi`, documento en `/openapi/v1.json`) alineada a los grupos de
      `contracts/*.yaml`, como base para las pruebas de contrato de cada historia en
      `backend/src/EnterpriseAccessControl.Api/Program.cs`
- [X] T025 [P] Configurar ASP.NET Core Health Checks con `AspNetCore.HealthChecks.SqlServer`, exponiendo
      `GET /health/live` (liveness, sin dependencias) y `GET /health/ready` (readiness, incluye
      conectividad a SQL Server), ambos sin autenticación (research.md §19) en
      `backend/src/EnterpriseAccessControl.Api/Program.cs`

**Checkpoint**: Infraestructura lista — las historias de usuario pueden comenzar.

---

## Phase 3: User Story 1 - Inicio de sesión y alcance de gestión (Priority: P1) 🎯 MVP

**Goal**: Un usuario ACTIVO con credenciales válidas inicia sesión y obtiene su alcance de compañías; todas
sus operaciones quedan limitadas a ese alcance (Historia 1, RF-001 a RF-005, RF-050).

**Independent Test**: `POST /api/auth/login` con un usuario ACTIVO de prueba devuelve un token y
`alcanceCompanias` no vacío; el mismo intento con un usuario INACTIVO/BLOQUEADO devuelve `403` con
`ProblemDetails`; un usuario con alcance limitado a las compañías A/B recibe `404` al consultar cualquier
recurso de la compañía C.

### Tests for User Story 1 ⚠️

- [X] T026 [P] [US1] Prueba de contrato para los endpoints de `contracts/auth.yaml` (verifica que las
      respuestas de error usan el esquema `ProblemDetails`) en
      `backend/tests/EnterpriseAccessControl.ContractTests/AuthContractTests.cs`
- [X] T027 [P] [US1] Prueba de contrato para los endpoints de `contracts/users.yaml` en
      `backend/tests/EnterpriseAccessControl.ContractTests/UsersContractTests.cs`
- [X] T028 [P] [US1] Prueba de integración de login: éxito, usuario INACTIVO, usuario BLOQUEADO, y
      `requiereCambioPassword` fuerza cambio (Historia 1, criterios 1–3) contra SQL Server real en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/LoginTests.cs`
- [X] T029 [P] [US1] Prueba de integración de política de contraseña: bloqueo tras N intentos fallidos,
      expiración periódica, rechazo de reutilización vía `HistorialContraseña` (research.md §2) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/PasswordPolicyTests.cs`
- [X] T030 [P] [US1] Prueba unitaria del validador de política de contraseña (longitud, complejidad), sin
      base de datos, en
      `backend/tests/EnterpriseAccessControl.UnitTests/Auth/PasswordPolicyValidatorTests.cs`
- [X] T031 [P] [US1] Prueba de integración: el alcance administrativo (`AlcanceUsuarioCompañía`) es
      independiente de la relación operacional Persona→Compañía→UnidadOrganizativa (RF-050) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/AlcanceIndependienteTests.cs`

### Implementation for User Story 1

- [X] T032 [P] [US1] Crear entidad `Usuario` (Correo, PasswordHash, Estado, RequiereCambioPassword,
      IntentosFallidosConsecutivos, FechaUltimoCambioPassword, `RowVersion` + auditoría) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/Usuario.cs`
- [X] T033 [P] [US1] Crear entidad `HistorialContraseña` (UsuarioId, PasswordHash) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/HistorialContrasena.cs`
- [X] T034 [P] [US1] Crear entidad `AlcanceUsuarioCompañía` (UsuarioId, CompañíaId, único compuesto) con FK a
      la entidad `Compañía` creada en T012 en
      `backend/src/EnterpriseAccessControl.Domain/Entities/AlcanceUsuarioCompania.cs`
- [X] T035 [US1] Configurar EF Core (`IEntityTypeConfiguration<T>`: índice único en Correo, único compuesto
      en AlcanceUsuarioCompañía) y generar migración de SQL Server para
      Usuario/HistorialContraseña/AlcanceUsuarioCompañía (depende de T032-T034) en
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Configurations/UsuarioConfiguration.cs`
- [X] T036 [US1] Implementar `AutenticacionService` (valida credenciales con `PasswordHasher<T>`, cuenta
      intentos fallidos, aplica bloqueo/expiración según research.md §2, umbrales vía Options Pattern) en
      `backend/src/EnterpriseAccessControl.Application/Auth/AutenticacionService.cs`
- [X] T037 [US1] Implementar `JwtTokenService` (emite JWT con claim de `alcanceCompanias`, configuración vía
      `IOptions<JwtOptions>`) en `backend/src/EnterpriseAccessControl.Infrastructure/Security/JwtTokenService.cs`
- [X] T038 [US1] Implementar `AuthController` (`POST /api/auth/login`, `POST /api/auth/cambiar-password`,
      `GET /api/auth/sesion`; errores 401/403 como `ProblemDetails`) en
      `backend/src/EnterpriseAccessControl.Api/Controllers/AuthController.cs`
- [X] T039 [US1] Implementar `UsuarioService` (crear, listar, actualizar, desbloquear, gestionar
      `alcance-companias` — sin ninguna dependencia hacia entidades operacionales de Persona, RF-050) y
      `UsuariosController` conforme a `contracts/users.yaml`, protegido por
      `[Authorize(Policy = "CompaniaScope")]` (T014) en
      `backend/src/EnterpriseAccessControl.Application/Auth/UsuarioService.cs` y
      `backend/src/EnterpriseAccessControl.Api/Controllers/UsuariosController.cs`
- [X] T040 [US1] Agregar validadores FluentValidation para `CrearUsuarioRequest` y
      `CambiarPasswordRequest` que apliquen la política de contraseña (longitud mínima 10, mayúscula,
      minúscula, dígito) en `backend/src/EnterpriseAccessControl.Application/Auth/Validators/`
- [X] T041 [P] [US1] Frontend: página de login, almacenamiento de sesión y guard de rutas protegidas en
      `frontend/src/features/auth/`
- [X] T042 [P] [US1] Frontend: pantallas de mantenimiento de usuarios (listar, crear, editar, desbloquear,
      asignar alcance de compañías) en `frontend/src/features/users/`

**Checkpoint**: Login funcional con alcance de compañías aplicado; base de autenticación lista para que las
demás historias protejan sus endpoints.

---

## Phase 4: User Story 2 - Compañías (Principal/Contratista), sus relaciones y unidades organizativas (Priority: P1)

**Goal**: Clasificar cada compañía como PRINCIPAL_MANDANTE o CONTRATISTA (RF-042), declarar relaciones
Contratista↔Principal con vigencia temporal (RF-051), y administrar las unidades organizativas jerárquicas
de una Compañía Principal, aisladas de las de cualquier otra Principal, sin introducir ninguna FK directa
`UnidadOrganizativa → Compañía` (Historia 2, RF-006 a RF-008, RF-038, RF-042 a RF-045, RF-049, RF-051).

**Independent Test**: Crear dos compañías `PRINCIPAL_MANDANTE` (P1 y P2) y una `CONTRATISTA`; declarar
relaciones vigentes simultáneas de la Contratista hacia P1 y P2; crear una jerarquía de 3 unidades
organizativas bajo P1; verificar que `GET /api/unidades-organizativas/arbol?companiaPrincipalId=P1` refleja
la jerarquía y que la misma consulta con `companiaPrincipalId=P2` devuelve un árbol vacío (RF-043); intentar
crear una unidad raíz asociada a la Contratista y verificar rechazo `400` (RF-045).

### Tests for User Story 2 ⚠️

- [X] T043 [P] [US2] Prueba de contrato para `contracts/companies.yaml` (incluye `tipoCompania` y
      `relaciones-principales`/`finalizar`) en
      `backend/tests/EnterpriseAccessControl.ContractTests/CompaniasContractTests.cs`
- [X] T044 [P] [US2] Prueba de contrato para `contracts/org-units.yaml` (incluye `companiaPrincipalId`
      obligatorio en creación de raíz y en `/arbol`) en
      `backend/tests/EnterpriseAccessControl.ContractTests/UnidadesOrganizativasContractTests.cs`
- [X] T045 [P] [US2] Prueba de integración: creación de compañía, clasificación `TipoCompañía`
      PRINCIPAL_MANDANTE/CONTRATISTA, filtrado por tipo, y filtrado de listados/consultas por alcance del
      usuario (Historia 1 criterio 4 revalidado) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Companies/CompaniasTests.cs`
- [X] T046 [P] [US2] Prueba de integración: unidad raíz asociada a una Compañía Principal, unidad hija,
      rechazo de ciclos al crear/mover (RF-038, Principio V) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/OrgUnits/UnidadesOrganizativasTests.cs`
- [X] T047 [P] [US2] Prueba de integración: aislamiento multi-Principal — dos Compañías Principales con
      árboles de unidades organizativas separados; el árbol de una no incluye nodos de la otra (RF-043,
      CS-011) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/OrgUnits/AislamientoMultiPrincipalTests.cs`
- [X] T048 [P] [US2] Prueba de integración: rechazo de unidad raíz asociada a una compañía `CONTRATISTA`
      (RF-045, Historia 2 criterio 5) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/OrgUnits/UnidadRaizContratistaRechazadaTests.cs`
- [X] T049 [P] [US2] Prueba de integración: rechazo de mover una unidad bajo un padre perteneciente al árbol
      de otra Compañía Principal (RF-045) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/OrgUnits/MoverEntrePrincipalesRechazadoTests.cs`
- [X] T050 [P] [US2] Prueba de integración: `RelaciónContratistaPrincipal` — creación, relaciones vigentes
      simultáneas de una misma Contratista hacia varias Principales (CS-012), rechazo de solapamiento en el
      mismo par (trigger de SQL Server, research.md §5), y finalización explícita. `fechaHoraFin` sigue
      siendo opcional para esta entidad — no está vinculada a una persona, RF-071 no le aplica — en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Companies/RelacionContratistaPrincipalTests.cs`
- [X] T051 [P] [US2] Prueba unitaria del helper de detección de ciclos y resolución de raíz (T017) con
      grafos en memoria, sin base de datos, en
      `backend/tests/EnterpriseAccessControl.UnitTests/Hierarchy/JerarquiaCicloValidatorTests.cs`

### Implementation for User Story 2

- [X] T052 [US2] Crear entidad `UnidadOrganizativa` (Nombre, UnidadSuperiorId autorreferenciado, Estado +
      auditoría; **sin ninguna columna ni FK hacia Compañía** — RF-044) con su configuración
      `IEntityTypeConfiguration<T>` y migración en
      `backend/src/EnterpriseAccessControl.Domain/Entities/UnidadOrganizativa.cs`
- [X] T053 [US2] Crear entidad de enlace `CompañíaPrincipalUnidadOrganizativaRaiz` (CompañíaId,
      UnidadOrganizativaRaízId único) con validación de aplicación de que `CompañíaId` referencia una
      compañía `PRINCIPAL_MANDANTE` (RF-045) y que `UnidadOrganizativaRaízId` referencia un nodo con
      `UnidadSuperiorId = null`, configuración EF Core y migración (depende de T052) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/CompaniaPrincipalUnidadOrganizativaRaiz.cs`
- [X] T054 [US2] Crear entidad `RelaciónContratistaPrincipal` (CompañíaContratistaId, CompañíaPrincipalId,
      FechaHoraInicio, FechaHoraFin **nullable** — no está vinculada a una persona, RF-071 no le aplica,
      `null` sigue significando vigencia abierta, `RowVersion` + auditoría) con validación de que
      `CompañíaContratistaId` es `CONTRATISTA` y `CompañíaPrincipalId` es `PRINCIPAL_MANDANTE` (RF-051), y
      migración que agrega el **trigger SQL Server `AFTER INSERT, UPDATE`** de no-solapamiento particionado
      por `(CompañíaContratistaId, CompañíaPrincipalId)` (research.md §5, plantilla SQL incluida) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/RelacionContratistaPrincipal.cs` y
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/*_AddRelacionContratistaPrincipalTrigger.cs`
- [X] T055 [US2] Implementar `CompaniaService` (CRUD sobre la entidad `Compañía` ya creada en T012,
      filtrado por alcance y por `TipoCompañía`, único `(TipoDocumentoId, NumeroDocumento)`) en
      `backend/src/EnterpriseAccessControl.Application/Companies/CompaniaService.cs`
- [X] T056 [US2] Implementar `UnidadOrganizativaService` (CRUD; al crear un nodo raíz valida
      `companiaPrincipalId` obligatorio y `TipoCompañía = PRINCIPAL_MANDANTE`, y crea la fila de enlace de
      T053; al crear un nodo hijo hereda la Compañía Principal de su padre; árbol anidado filtrado por
      `companiaPrincipalId`; mover valida ausencia de ciclo vía T017 y que el nuevo padre pertenezca al
      mismo árbol/Principal — RF-045) en
      `backend/src/EnterpriseAccessControl.Application/OrgUnits/UnidadOrganizativaService.cs`
- [X] T057 [US2] Implementar `RelacionContratistaPrincipalService` (crear con cierre automático de la
      relación previa del mismo par, listar por Contratista, finalizar explícitamente) en
      `backend/src/EnterpriseAccessControl.Application/Companies/RelacionContratistaPrincipalService.cs`
- [X] T058 [US2] Implementar `CompaniasController` conforme a `contracts/companies.yaml` (incluye filtro por
      `tipoCompania` y los endpoints `relaciones-principales`/`relaciones-principales/{id}/finalizar`),
      protegido por la política `CompaniaScope` (T014) en
      `backend/src/EnterpriseAccessControl.Api/Controllers/CompaniasController.cs`
- [X] T059 [US2] Implementar `UnidadesOrganizativasController` (`companiaPrincipalId` obligatorio en
      `GET` lista/`/arbol`; incluido en el request de creación de raíz; `/mover` valida mismo árbol) conforme
      a `contracts/org-units.yaml` en
      `backend/src/EnterpriseAccessControl.Api/Controllers/UnidadesOrganizativasController.cs`
- [X] T060 [P] [US2] Frontend: pantallas de mantenimiento de compañías, con selector de `TipoCompañía`
      (PRINCIPAL_MANDANTE/CONTRATISTA) en `frontend/src/features/companies/`
- [X] T061 [P] [US2] Frontend: selector de Compañía Principal seguido del árbol de unidades organizativas
      de esa Principal (reutiliza el componente Tree de T022), con crear/mover nodos en
      `frontend/src/features/org-units/`
- [X] T062 [P] [US2] Frontend: pantalla de relaciones Contratista↔Principal (listar, crear, finalizar) en
      `frontend/src/features/companies/RelacionesContratistaPrincipal/`

**Checkpoint**: Compañías clasificadas Principal/Contratista con relaciones vigentes entre ellas; unidades
organizativas administrables como jerarquías sin ciclos, aisladas por Compañía Principal, sin ninguna FK
directa hacia `Compañía`.

---

## Phase 5: User Story 3 - Datos maestros (Priority: P1)

**Goal**: Mantenimiento de los catálogos Tipo de Documento, Tipo de Sangre, Género, Tipo de Persona y Tipo
de Credencial, con carga inicial versionada para Perú (Historia 3, RF-010, RF-017, RF-030, RF-031, RF-032).

**Independent Test**: Tras aplicar las migraciones, `GET /api/maestros/tipos-documento` devuelve DNI, Carné
de Extranjería, Pasaporte y RUC; crear un nuevo `TipoPersona`, marcarlo INACTIVO y verificar que queda
disponible para lectura pero no utilizable en nuevas asignaciones (validado indirectamente en US4/US5).

### Tests for User Story 3 ⚠️

- [X] T063 [P] [US3] Prueba de contrato para `contracts/masters.yaml` (5 grupos de rutas) en
      `backend/tests/EnterpriseAccessControl.ContractTests/MaestrosContractTests.cs`
- [X] T064 [P] [US3] Prueba de integración: CRUD y transición de estado ACTIVO/INACTIVO por catálogo contra
      SQL Server real en `backend/tests/EnterpriseAccessControl.IntegrationTests/Masters/MaestrosTests.cs`
- [X] T065 [P] [US3] Prueba de integración: la migración de semilla versionada carga los catálogos iniciales
      de Perú (RF-031) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Masters/SeedDataTests.cs`

### Implementation for User Story 3

- [X] T066 [P] [US3] Crear entidades `TipoDocumento`, `TipoSangre`, `Genero`, `TipoPersona`,
      `TipoCredencial` (Nombre, Estado + auditoría; `TipoCredencial` representa tipo/diseño visual, nunca
      tecnología física — RF-058) en `backend/src/EnterpriseAccessControl.Domain/Entities/`
- [X] T067 [US3] Configurar EF Core y generar migración de semilla versionada con los valores iniciales de
      Perú (DNI/Carné de Extranjería/Pasaporte/RUC; O+/O-/A+/A-/B+/B-/AB+/AB-; Masculino/Femenino —
      data-model.md, research.md §8) vía `migrationBuilder.InsertData` en
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/Seed/`
- [X] T068 [US3] Implementar `MasterDataService` genérico reutilizable (listar, crear, actualizar por
      catálogo) en `backend/src/EnterpriseAccessControl.Application/Masters/MasterDataService.cs`
- [X] T069 [US3] Implementar `MaestrosController` (5 grupos de rutas de `contracts/masters.yaml`) en
      `backend/src/EnterpriseAccessControl.Api/Controllers/MaestrosController.cs`
- [X] T070 [P] [US3] Frontend: pantallas genéricas de mantenimiento para los 5 catálogos en
      `frontend/src/features/masters/`

**Checkpoint**: Los 5 catálogos maestros están disponibles y precargados para Perú.

---

## Phase 6: User Story 4 - Personas (Priority: P1)

**Goal**: Registrar personas con datos personales, de identificación y contacto obligatorios, con ID
UID/UUID oculto e inmutable (Historia 4, RF-012, RF-013, RF-041).

**Independent Test**: Registrar una persona con todos los campos obligatorios; verificar que un segundo
registro con el mismo `(tipoDocumentoId, numeroDocumento)` recibe `409` con `ProblemDetails`; buscar la
persona por nombre/documento y confirmar que el `id` nunca aparece como campo editable.

### Tests for User Story 4 ⚠️

- [X] T071 [P] [US4] Prueba de contrato para los endpoints de Persona en `contracts/people.yaml` en
      `backend/tests/EnterpriseAccessControl.ContractTests/PersonasContractTests.cs`
- [X] T072 [P] [US4] Prueba de integración: registro con todos los campos obligatorios y rechazo de
      duplicado `(tipoDocumentoId, numeroDocumento)` (RF-041, índice único en SQL Server) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/PersonasTests.cs`
- [X] T073 [P] [US4] Prueba de integración: búsqueda de personas limitada al alcance de compañías del
      usuario (RF-035) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/PersonasBusquedaTests.cs`
- [X] T074 [US4] Prueba de rendimiento: búsqueda de personas p95 < 2s con ≥100,000 personas (CS-002) contra
      SQL Server en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/PersonasPerformanceTests.cs`

### Implementation for User Story 4

- [X] T075 [US4] Crear entidad `Persona` (todos los campos de Historia 4) con índice único compuesto
      `(TipoDocumentoId, NumeroDocumento)` en
      `backend/src/EnterpriseAccessControl.Domain/Entities/Persona.cs`
- [X] T076 [US4] Implementar `PersonaService` (crear, actualizar, buscar paginado, filtrado por alcance vía
      compañía vigente) en `backend/src/EnterpriseAccessControl.Application/People/PersonaService.cs`
- [X] T077 [US4] Implementar `PersonasController` (`GET` lista/{id}, `POST`, `PUT`) conforme a
      `contracts/people.yaml` en `backend/src/EnterpriseAccessControl.Api/Controllers/PersonasController.cs`
- [X] T078 [US4] Agregar validadores FluentValidation de `PersonaRequest` (campos obligatorios, formato de
      correo) en `backend/src/EnterpriseAccessControl.Application/People/Validators/PersonaRequestValidator.cs`
- [X] T079 [P] [US4] Frontend: formulario de registro y pantalla de búsqueda de personas en
      `frontend/src/features/people/`

**Checkpoint**: Personas registrables y consultables dentro del alcance del usuario.

---

## Phase 7: User Story 5 - Históricos, Contexto Operativo, Credencial y Revocación Automática (Priority: P1)

**Goal**: Conservar el histórico de compañía de pertenencia de una persona (RF-014), con `FechaHoraFin`
**obligatoria desde la creación** (RF-071 — ya no existe vigencia indefinida) y una operación explícita de
**renovación** que la extiende hacia adelante sin crear una nueva pertenencia (RF-073); administrar sus
contextos operativos —uno por cada Compañía Principal, simultáneos sin límite (RF-052, CS-030)— cada uno
con su propia unidad organizativa (RF-015, RF-055) y **credencial** (RF-056, RF-057) por Compañía Principal;
todas las asociaciones dependientes de la pertenencia **contenidas temporalmente dentro de su vigencia**
(RF-072); y revocar automáticamente en cascada transaccional los contextos, asignaciones de unidad
organizativa **y credenciales** que dependan de una pertenencia que finaliza (RF-061 a RF-065, Historia 5
"Revocación automática por cese de pertenencia"). **Incluye la entidad `AsignaciónCredencial`** (ver nota de
secuenciación al inicio de este documento: se construye aquí, no en US9, porque US8/P1 depende de ella
incondicionalmente vía RF-066) — la asignación/devolución/eliminación de credenciales como funcionalidad de
usuario sigue siendo P2, en US9.

**Independent Test**: Asignar una persona Contratista a una compañía con relaciones vigentes hacia dos
Principales, con `fechaHoraInicio`/`fechaHoraFin` reales; abrir contextos operativos con ambas
simultáneamente, cada uno con su propia unidad organizativa y credencial, todos contenidos dentro de la
vigencia de la pertenencia; cerrar la pertenencia de la persona a su Contratista; verificar que **ambos**
contextos, sus asignaciones de unidad organizativa y **ambas credenciales** quedan `INACTIVO`/`FINALIZADA`/
`REVOCADA` con `MotivoFin`/`RevocadoPorPertenenciaId` poblados, `FechaHoraInicio` sin modificar, y ningún
registro eliminado. Adicionalmente: renovar la pertenencia extiende su `FechaHoraFin` sin tocar ningún
contexto/UO/credencial existente; intentar crear un contexto con fechas fuera de la vigencia de la
pertenencia es rechazado con `409`.

### Tests for User Story 5 ⚠️

- [X] T080 [P] [US5] Prueba de contrato para los endpoints de historial, contexto operativo y renovación de
      `contracts/people.yaml` (`historial-companias`, `.../finalizar`, `.../renovar`, `contextos-operativos`,
      `.../unidad-organizativa`) en
      `backend/tests/EnterpriseAccessControl.ContractTests/HistorialPersonaContractTests.cs`
- [X] T081 [P] [US5] Prueba de contrato para `contracts/credentials.yaml` (incluye `companiaPrincipalId`,
      `fechaHoraFin` obligatoria, y el valor `REVOCADA` del estado) en
      `backend/tests/EnterpriseAccessControl.ContractTests/CredencialesContractTests.cs`
- [X] T082 [P] [US5] Prueba de integración: normalización 00:00/23:59, `fechaHoraFin` obligatoria desde la
      creación (RF-071, rechazo `400` si se omite), cierre automático de la asignación de compañía de
      pertenencia previa (sin extender su `FechaHoraFin` si ya estaba naturalmente vencida y sin
      solapamiento con la nueva), rechazo de solapamiento vía el trigger SQL Server particionado por
      `PersonaId` (RF-014, RF-016, RF-039, RF-071) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/HistorialPersonaTests.cs`
- [X] T083 [P] [US5] Prueba de integración: **renovación de pertenencia** (RF-073) —
      `POST .../historial-companias/{id}/renovar` extiende `FechaHoraFin` sin crear una nueva asignación,
      sin modificar `FechaHoraInicio`/`Estado`/`MotivoFin`, sin disparar la cascada de RF-061, y sin afectar
      ningún contexto/UO/credencial dependiente ya existente; rechazo `409` si la nueva fecha no es
      estrictamente posterior a la vigente, si `Estado = FINALIZADA`, o si la pertenencia ya expiró
      dinámicamente (`fecha actual > FechaHoraFin` vigente, aunque `Estado` siga `ACTIVA`) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/RenovacionPertenenciaTests.cs`
- [X] T084 [P] [US5] Prueba de integración: apertura de contexto operativo Caso A — persona de una
      Principal, `companiaPrincipalId` fijado automáticamente, rechazo de una Principal distinta (RF-053,
      CS-020) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/ContextoOperativoCasoATests.cs`
- [X] T085 [P] [US5] Prueba de integración: apertura de contexto operativo Caso B — persona de una
      Contratista, restringido a Principales con `RelaciónContratistaPrincipal` vigente, rechazo si no
      existe relación (RF-054, CS-019) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/ContextoOperativoCasoBTests.cs`
- [X] T086 [P] [US5] Prueba de integración: múltiples contextos operativos simultáneos para Principales
      distintas de la misma persona, sin límite superior (RF-052, CS-013, CS-030) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/ContextosMultiplesSimultaneosTests.cs`
- [X] T087 [P] [US5] Prueba de integración: asignación de unidad organizativa dentro de un contexto
      operativo, exclusividad por `ContextoOperativoId` (trigger SQL Server), simultaneidad entre contextos
      distintos de la misma persona (RF-015, RF-055, CS-014) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/AsignacionUnidadOrganizativaTests.cs`
- [X] T088 [P] [US5] Prueba de integración: **contención temporal (RF-072)** — crear un
      `ContextoOperativoPersonaPrincipal`, una `AsignaciónPersonaUnidadOrganizativa` o una
      `AsignaciónCredencial` con `fechaHoraInicio` anterior al inicio de la pertenencia, o `fechaHoraFin`
      posterior a su fin, es rechazado con `409` (`FUERA_DE_CONTENCION_TEMPORAL`); con fechas contenidas
      (incluida la igualdad exacta de `fechaHoraFin`) es aceptado (CS-034) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/ContencionTemporalTests.cs`
- [X] T089 [P] [US5] Prueba de integración: **fechas obligatorias (RF-071)** — crear un contexto operativo,
      una asignación de unidad organizativa, una credencial o un perfil (`AsignaciónTipoPersona`) sin
      `fechaHoraFin` es rechazado `400`; ninguna de estas entidades admite `null` ni fecha centinela en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/FechasObligatoriasTests.cs`
- [X] T090 [P] [US5] Prueba de integración: múltiples perfiles (`TipoPersona`) simultáneos permitidos sin
      exclusividad (RF-011) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/PerfilesPersonaTests.cs`
- [X] T091 [US5] Prueba de integración: reconstrucción del estado efectivo de una persona en una fecha/hora
      dada, incluyendo el array `contextosOperativosVigentes` (RF-037) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/EstadoEfectivoTests.cs`
- [X] T092 [US5] Prueba de integración: **cambio de compañía de pertenencia** — crear una nueva
      `AsignaciónPersonaCompañía` cierra automáticamente la anterior y dispara la revocación en cascada de
      todos los contextos operativos, asignaciones de unidad organizativa **y credenciales** dependientes de
      la pertenencia cerrada (CS-025, CS-027) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/RevocacionPorCambioCompaniaTests.cs`
- [X] T093 [US5] Prueba de integración: **cese explícito de pertenencia** —
      `POST /historial-companias/{id}/finalizar` dispara la misma cascada de revocación sin crear una nueva
      compañía de pertenencia (RF-061) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/CeseExplicitoPertenenciaTests.cs`
- [X] T094 [US5] Prueba de integración: **preservación del histórico** — la revocación en cascada nunca
      elimina registros ni modifica `FechaHoraInicio`; `Estado`, `MotivoFin` y `RevocadoPorPertenenciaId`
      quedan correctamente poblados y consultables para las tres entidades dependientes, incluida
      `AsignaciónCredencial` (CS-026, RF-063) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/PreservacionHistoricoTests.cs`
- [X] T095 [US5] Prueba de integración: **múltiples Principales simultáneos revocados juntos** — una
      persona con contextos, UO y credenciales simultáneos hacia Principal A y B (vía una única Contratista)
      pierde el acceso derivado de **ambas** al terminar esa única pertenencia (CS-027, Ejemplo 2 de
      negocio) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/RevocacionMultiPrincipalTests.cs`
- [X] T096 [US5] Prueba de integración: **aislamiento entre pertenencias** — una pertenencia posterior y
      distinta de la misma persona (tras cambiar de compañía) abre contextos, UO y credenciales nuevos y
      propios, no afectados por revocaciones previas (RF-062) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/AislamientoPertenenciasTests.cs`
- [X] T097 [US5] Prueba de integración: **fecha futura de finalización** — un cese registrado con
      `FechaHoraFin` futura propaga esa misma fecha de inmediato a los registros dependientes (sin proceso
      programado); estos permanecen genuinamente vigentes hasta esa fecha (CS-028, RF-064) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/RevocacionFechaFuturaTests.cs`
- [X] T098 [US5] Prueba de integración: asignar, devolver (baja lógica manual), rechazo de solapamiento
      entre asignaciones `ASIGNADO` de la misma persona para la misma Compañía Principal vía el trigger SQL
      Server particionado por `(PersonaId, CompañíaPrincipalId)`, y credenciales `ASIGNADO` simultáneas para
      Principales distintas (RF-018, RF-057, CS-016, CS-017) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/People/CredencialesAsignacionTests.cs`
- [X] T099 [P] [US5] Prueba unitaria de normalización de vigencia (00:00 inicio / 23:59 fin del último día)
      en `backend/tests/EnterpriseAccessControl.UnitTests/People/VigenciaNormalizationTests.cs`
- [X] T100 [P] [US5] Prueba unitaria de `RevocacionService` en aislamiento (repositorio simulado): verifica
      `FechaHoraFin = MIN(actual, efectiva del cese)` sin extender fechas ya fijadas, y el `Estado`/
      `MotivoFin`/`RevocadoPorPertenenciaId` correctos para las **tres** entidades dependientes (contexto,
      UO, credencial) en
      `backend/tests/EnterpriseAccessControl.UnitTests/People/RevocacionServiceTests.cs`
- [X] T101 [P] [US5] Prueba unitaria del validador de contención temporal (RF-072) en aislamiento: casos
      límite de igualdad exacta de fechas, inicio anterior, fin posterior en
      `backend/tests/EnterpriseAccessControl.UnitTests/People/ContencionTemporalValidatorTests.cs`

### Implementation for User Story 5

- [X] T102 [US5] Extender la entidad `AsignaciónPersonaCompañía` (ya creada; RF-014) con `Estado` (enum
      `ACTIVA`/`FINALIZADA`), `MotivoFin` (enum nullable `CESE_PERTENENCIA`/`REEMPLAZO_ASIGNACION`,
      research.md §14.2), y `FechaHoraFin` **NOT NULL** desde la creación (RF-071 — sin `null` ni fecha
      centinela), y migración que agrega el **trigger SQL Server** de no-solapamiento particionado por
      `PersonaId` en `backend/src/EnterpriseAccessControl.Domain/Entities/AsignacionPersonaCompania.cs`
- [X] T103 [P] [US5] Crear entidad `ContextoOperativoPersonaPrincipal` (PersonaId, CompañíaPrincipalId,
      FechaHoraInicio, `FechaHoraFin` **NOT NULL** — RF-071, `Estado` ACTIVO/INACTIVO, `MotivoFin` nullable
      REVOCACION_CESE_PERTENENCIA/REEMPLAZO_ASIGNACION/CIERRE_MANUAL, `RevocadoPorPertenenciaId` FK
      nullable a `AsignaciónPersonaCompañía`, `RowVersion` + auditoría) con migración que agrega el
      **trigger SQL Server** particionado por `(PersonaId, CompañíaPrincipalId)` en
      `backend/src/EnterpriseAccessControl.Domain/Entities/ContextoOperativoPersonaPrincipal.cs`
- [X] T104 [P] [US5] Crear entidad `AsignaciónPersonaUnidadOrganizativa` (PersonaId denormalizado,
      `ContextoOperativoId` FK obligatoria, UnidadOrganizativaId, FechaHoraInicio, `FechaHoraFin` **NOT
      NULL** — RF-071, `Estado`, `MotivoFin`, `RevocadoPorPertenenciaId`) con migración que agrega el
      **trigger SQL Server** particionado por `ContextoOperativoId` (RF-015, RF-055) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/AsignacionPersonaUnidadOrganizativa.cs`
- [X] T105 [P] [US5] Crear entidad `AsignaciónCredencial` (PersonaId, `CompañíaPrincipalId` FK obligatoria a
      `Compañía` PRINCIPAL_MANDANTE, TipoCredencialId — FK a `TipoCredencial` de US3, FechaHoraInicio,
      `FechaHoraFin` **NOT NULL** — RF-071, `Estado` ASIGNADO/DEVUELTO/ELIMINADO/REVOCADA,
      `RevocadoPorPertenenciaId` FK nullable a `AsignaciónPersonaCompañía`; ver nota de secuenciación al
      inicio de este documento) con migración que agrega el **trigger SQL Server** particionado por
      `(PersonaId, CompañíaPrincipalId)`, evaluado solo entre filas `Estado = ASIGNADO` (RF-056, RF-057) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/AsignacionCredencial.cs`
- [X] T106 [P] [US5] Crear entidad `AsignaciónTipoPersona` (PersonaId, TipoPersonaId, FechaHoraInicio,
      `FechaHoraFin` **NOT NULL** — RF-071, Estado; sin exclusividad — RF-011; **no** sujeta a contención,
      RF-072 no le aplica) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/AsignacionTipoPersona.cs`
- [X] T107 [US5] Implementar `ContencionTemporalValidator` (RF-072): dado un `PersonaId` y un rango
      `FechaHoraInicio`/`FechaHoraFin` propuesto, consulta la `AsignaciónPersonaCompañía` vigente de esa
      persona y valida `inicio_hija >= inicio_pertenencia AND fin_hija <= fin_pertenencia`; lanza una
      excepción de dominio traducible a `409 FUERA_DE_CONTENCION_TEMPORAL` si no se cumple. Reutilizado por
      `ContextoOperativoService`, `AsignacionUnidadOrganizativaService` (T112) y `CredencialService` (US9,
      T146) en `backend/src/EnterpriseAccessControl.Application/People/ContencionTemporalValidator.cs`
- [X] T108 [US5] Implementar `RevocacionService` (Application/People) — operación transaccional que, dado
      el cierre de una `AsignaciónPersonaCompañía`, revoca **todas** las tres entidades dependientes de la
      persona que dependan de ella y estén vigentes: `ContextoOperativoPersonaPrincipal`,
      `AsignaciónPersonaUnidadOrganizativa` **y `AsignaciónCredencial`** — fija `FechaHoraFin` (`MIN` con la
      existente, nunca la extiende), `Estado`, `MotivoFin`/`Estado=REVOCADA`, `RevocadoPorPertenenciaId`
      (research.md §14.1). Implementación completa en un solo paso — no requiere extensión posterior desde
      US9, porque las tres entidades dependientes ya existen en esta historia (depende de T102-T105) en
      `backend/src/EnterpriseAccessControl.Application/People/RevocacionService.cs`
- [X] T109 [US5] Implementar `HistorialPersonaService` con tres operaciones: (a) `CrearAsignacionAsync` —
      crea nueva asignación de compañía cerrando la anterior (sin extender su `FechaHoraFin` si no hay
      solapamiento posible) y disparando `RevocacionService`; (b) `FinalizarAsync` — cese explícito,
      también disparando `RevocacionService`; (c) `RenovarAsync` (RF-073) — extiende `FechaHoraFin` de la
      pertenencia vigente hacia una fecha estrictamente posterior, solo si `Estado = ACTIVA` **y** sigue
      vigente dinámicamente (`fecha actual <= FechaHoraFin` ya declarada); no toca `FechaHoraInicio`/
      `Estado`/`MotivoFin`, no crea ni modifica ninguna asociación dependiente, no dispara `RevocacionService`.
      Normaliza horas a 00:00/23:59 en las tres operaciones en
      `backend/src/EnterpriseAccessControl.Application/People/HistorialPersonaService.cs`
- [X] T110 [US5] Implementar `ContextoOperativoService` (abrir contexto validando Caso A — RF-053 — o Caso
      B — RF-054 —, y la contención temporal vía T107 — RF-072 —, listar; exige `AsignaciónPersonaCompañía`
      vigente como ancla, research.md §13) en
      `backend/src/EnterpriseAccessControl.Application/People/ContextoOperativoService.cs`
- [X] T111 [US5] Implementar `AsignacionUnidadOrganizativaService` (asignar UO dentro de un contexto
      operativo vigente, validando que la unidad pertenezca a la misma Compañía Principal del contexto —
      RF-055 — y la contención temporal vía T107 — RF-072; cierra automáticamente la anterior del mismo
      contexto) en
      `backend/src/EnterpriseAccessControl.Application/People/AsignacionUnidadOrganizativaService.cs`
- [X] T112 [US5] Implementar `EstadoEfectivoService` (resuelve compañía de pertenencia vigente y **todos**
      los contextos operativos vigentes con su unidad organizativa vigente en una fecha dada) en
      `backend/src/EnterpriseAccessControl.Application/People/EstadoEfectivoService.cs`
- [X] T113 [US5] Extender `PersonasController` con `/historial-companias`,
      `/historial-companias/{id}/finalizar`, `/historial-companias/{id}/renovar` (RF-073),
      `/contextos-operativos`, `/contextos-operativos/{id}/unidad-organizativa`, `/perfiles`,
      `/estado-efectivo` conforme a `contracts/people.yaml` en
      `backend/src/EnterpriseAccessControl.Api/Controllers/PersonasController.cs`
- [X] T114 [US5] Implementar `CredencialService` mínimo requerido por esta historia — solo `AsignarAsync`
      (valida contexto operativo vigente con la Principal indicada — RF-056 — y la contención temporal vía
      T107 — RF-072) — usado por las pruebas de la cascada de esta historia; las operaciones
      `DevolverAsync`/`EliminarAsync` y el `CredencialesController` completo se implementan en US9 (T145) en
      `backend/src/EnterpriseAccessControl.Application/Credentials/CredencialService.cs`
- [X] T115 [P] [US5] Frontend: pantallas de histórico de persona (línea de tiempo de compañía de
      pertenencia y de cada contexto operativo, con indicación visual de `Estado`/`MotivoFin`, y acción de
      renovar la pertenencia — RF-073) en `frontend/src/features/people/history/`
- [X] T116 [P] [US5] Frontend: pantalla de asignación de unidad organizativa con comportamiento Caso A
      (Principal fijada automáticamente) / Caso B (selector de Principal limitado a relaciones vigentes de
      la Contratista) en `frontend/src/features/people/AsignacionUnidadOrganizativa/`

**Checkpoint**: Históricos de compañía/contexto/unidad organizativa/credencial íntegros, sin solapamientos,
con fechas obligatorias y contención temporal validadas, con múltiples contextos simultáneos soportados sin
límite, con renovación de pertenencia funcional, y con revocación automática transaccional verificada
end-to-end para las tres entidades dependientes (contextos, UO y credenciales) en una sola implementación.

---

## Phase 8: User Story 6 - Árbol de áreas físicas de acceso (Priority: P1)

**Goal**: Crear y visualizar las áreas físicas de acceso como un árbol sin ciclos, perteneciendo cada una a
exactamente una Compañía Principal (Historia 6, RF-009, RF-038, RF-046, RF-049).

**Independent Test**: Crear una jerarquía de áreas raíz → hijo → nieto bajo una Compañía Principal P1;
`GET /api/areas-acceso/arbol?companiaPrincipalId=P1` refleja la jerarquía anidada; la misma consulta con
`companiaPrincipalId=P2` devuelve vacío; intentar crear un área raíz asociada a una compañía `CONTRATISTA`
devuelve `400`; mover la raíz bajo el nieto devuelve `409`.

### Tests for User Story 6 ⚠️

- [X] T117 [P] [US6] Prueba de contrato para los endpoints de árbol de `contracts/area-access.yaml`
      (incluye `companiaPrincipalId` obligatorio en creación de raíz y en `/arbol`) en
      `backend/tests/EnterpriseAccessControl.ContractTests/AreasAccesoContractTests.cs`
- [X] T118 [P] [US6] Prueba de integración: área raíz asociada a una Compañía Principal, área hija hereda
      `companiaPrincipalId`, rechazo de ciclos al crear/mover en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/AreaAccess/AreasAccesoTests.cs`
- [X] T119 [P] [US6] Prueba de integración: aislamiento multi-Principal — el árbol de áreas de una Compañía
      Principal no incluye nodos de otra (RF-043) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/AreaAccess/AislamientoAreasMultiPrincipalTests.cs`
- [X] T120 [P] [US6] Prueba de integración: rechazo de área raíz asociada a una compañía `CONTRATISTA`
      (RF-046) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/AreaAccess/AreaRaizContratistaRechazadaTests.cs`
- [X] T121 [P] [US6] Prueba de integración: rechazo de mover un área bajo un padre del árbol de otra
      Compañía Principal (RF-046) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/AreaAccess/MoverAreaEntrePrincipalesRechazadoTests.cs`

### Implementation for User Story 6

- [X] T122 [US6] Crear entidad `AreaAcceso` (Nombre, AreaSuperiorId autorreferenciado, `CompañíaPrincipalId`
      FK directa a `Compañía` con restricción `TipoCompañía = PRINCIPAL_MANDANTE` — RF-046, Estado +
      auditoría) en `backend/src/EnterpriseAccessControl.Domain/Entities/AreaAcceso.cs`
- [X] T123 [US6] Implementar `AreaAccesoService` (CRUD; al crear un área raíz valida `companiaPrincipalId`
      obligatorio y `TipoCompañía = PRINCIPAL_MANDANTE`; al crear un área hija hereda el
      `companiaPrincipalId` de su padre; árbol anidado filtrado por `companiaPrincipalId`; mover valida
      ausencia de ciclo vía T017 y que el nuevo padre pertenezca a la misma Compañía Principal) en
      `backend/src/EnterpriseAccessControl.Application/AreaAccess/AreaAccesoService.cs`
- [X] T124 [US6] Implementar `AreasAccesoController` (`companiaPrincipalId` obligatorio en `GET`
      lista/`/arbol`; incluido en el request de creación de raíz; `/mover` valida misma Compañía Principal)
      conforme a `contracts/area-access.yaml` en
      `backend/src/EnterpriseAccessControl.Api/Controllers/AreasAccesoController.cs`
- [X] T125 [P] [US6] Frontend: selector de Compañía Principal seguido del árbol de áreas de acceso de esa
      Principal (reutiliza el componente Tree de T022) en `frontend/src/features/area-access/`

**Checkpoint**: Áreas de acceso administrables como árbol sin ciclos, cada una perteneciente a exactamente
una Compañía Principal, aisladas de las áreas de cualquier otra Principal.

---

## Phase 9: User Story 7 - Tipos de persona autorizados por área (Priority: P1)

**Goal**: Indicar qué tipos de persona pueden acceder a cada área (Historia 7, RF-019).

**Independent Test**: Asociar Trabajador y Visitante a un área; `GET /api/areas-acceso/{id}/tipos-persona`
devuelve ambos; reemplazar el conjunto con `PUT` y confirmar que solo quedan los nuevos tipos indicados.

### Tests for User Story 7 ⚠️

- [X] T126 [P] [US7] Prueba de contrato para los endpoints `tipos-persona` de `contracts/area-access.yaml`
      en `backend/tests/EnterpriseAccessControl.ContractTests/AreaAccesoTipoPersonaContractTests.cs`
- [X] T127 [P] [US7] Prueba de integración: asociar y reemplazar tipos de persona permitidos por área
      (RF-019) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/AreaAccess/AreaAccesoTipoPersonaTests.cs`

### Implementation for User Story 7

- [X] T128 [US7] Crear entidad `AreaAccesoTipoPersona` (único `AreaAccesoId + TipoPersonaId`) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/AreaAccesoTipoPersona.cs`
- [X] T129 [US7] Extender `AreaAccesoService` con obtener/reemplazar tipos de persona autorizados en
      `backend/src/EnterpriseAccessControl.Application/AreaAccess/AreaAccesoService.cs`
- [X] T130 [US7] Extender `AreasAccesoController` con `GET`/`PUT /api/areas-acceso/{id}/tipos-persona` en
      `backend/src/EnterpriseAccessControl.Api/Controllers/AreasAccesoController.cs`
- [X] T131 [P] [US7] Frontend: selector de tipos de persona permitidos por área en
      `frontend/src/features/area-access/TiposPersonaPorArea/`

**Checkpoint**: Cada área declara explícitamente qué perfiles pueden acceder a ella.

---

## Phase 10: User Story 8 - Permisos de acceso con vigencia, horarios y credencial (Priority: P1)

**Goal**: Configurar permisos de acceso por persona, unidad organizativa o compañía, con vigencia
**obligatoria completa** (`fechaHoraInicioVigencia` y `fechaHoraFinVigencia`, ya exigido por RF-021 desde
el spec original, para los tres alcances — RF-071), evaluados dentro del contexto de la Compañía Principal
propietaria del área mediante el motor de dominio `EvaluadorDeAcceso` — explícitamente **no** un
`AuthorizationPolicy` de ASP.NET Core (research.md §18) — con precedencia PERSONA > UNIDAD_ORGANIZATIVA >
COMPAÑÍA, denegación por defecto, y un **paso de credencial vigente obligatorio** (RF-066, RF-070, RF-071:
14 pasos, no 13 — `Estado = ASIGNADO` **y** dentro de su ventana temporal) (Historia 8, RF-020 a RF-025,
RF-049, RF-059, RF-065, RF-066).

**Independent Test**: Crear un permiso de alcance PERSONA con vigencia y un bloque horario que cubra "ahora"
en `America/Lima`, para una persona con contexto operativo **y credencial** vigentes; `POST
/api/evaluacion-acceso` devuelve `CONCEDIDO`; sin credencial vigente para esa Principal, el mismo permiso
devuelve `DENEGADO` con `motivoDenegacion = SIN_CREDENCIAL_VIGENTE`; repetir fuera del bloque horario y
verificar `DENEGADO` con `motivoDenegacion = FUERA_DE_BLOQUE_HORARIO`; crear permisos simultáneos en los 3
niveles para la misma área y confirmar que gana el de nivel PERSONA (RF-025).

### Tests for User Story 8 ⚠️

- [X] T132 [P] [US8] Prueba de contrato para `contracts/permissions.yaml` (`fechaHoraFinVigencia`
      obligatoria para los tres alcances — RF-021, RF-071) en
      `backend/tests/EnterpriseAccessControl.ContractTests/PermisosContractTests.cs`
- [X] T133 [P] [US8] Prueba de contrato para `contracts/access-evaluation.yaml` (enum `MotivoDenegacion` con
      11 valores incluido `SIN_CREDENCIAL_VIGENTE`; descripción del flujo de 14 pasos) en
      `backend/tests/EnterpriseAccessControl.ContractTests/EvaluacionAccesoContractTests.cs`
- [X] T134 [P] [US8] Prueba de integración: creación de permiso con `fechaHoraFinVigencia` obligatoria
      (rechazo `400` si se omite, para los tres alcances), bloques horarios válidos e inválidos (fin ≤
      inicio, bloques solapados en el mismo día — RF-021, RF-022, RF-039, RF-071) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/PermisosTests.cs`
- [X] T135 [US8] Prueba de integración: flujo completo de evaluación de acceso (14 pasos de research.md
      §7) cubriendo cada motivo de denegación, incluido `SIN_CREDENCIAL_VIGENTE` (RF-023, RF-024, RF-059,
      RF-066) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/EvaluacionAccesoTests.cs`
- [X] T136 [US8] Prueba de integración: **gate de credencial (RF-066, RF-070)** — persona con contexto
      operativo vigente y permiso aplicable, pero sin `AsignacionCredencial` para la Principal evaluada
      (nunca asignada; `Estado = DEVUELTO`/`ELIMINADO`/`REVOCADA`; o `Estado = ASIGNADO` con `FechaHoraFin`
      ya pasada — temporalmente expirada) ⇒ `DENEGADO`, `motivoDenegacion = SIN_CREDENCIAL_VIGENTE`, en los
      cuatro casos, y confirmar que `Estado` de la credencial expirada **no** cambia por la sola evaluación
      en `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/GateCredencialTests.cs`
- [X] T137 [US8] Prueba de integración: precedencia PERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA con permisos
      aplicables simultáneamente en los 3 niveles (RF-025) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/PrecedenciaPermisosTests.cs`
- [X] T138 [P] [US8] Prueba unitaria de `EvaluadorDeAcceso` con cada paso aislado (mocks, sin base de
      datos ni `HttpContext`), cubriendo los 14 pasos: sin contexto operativo, relación Contratista-Principal
      vencida, sin credencial vigente (los cuatro casos de T136), área inactiva, perfil no autorizado,
      permiso fuera de vigencia, fuera de bloque horario en
      `backend/tests/EnterpriseAccessControl.UnitTests/Permissions/EvaluadorDeAccesoTests.cs`
- [X] T139 [US8] Prueba de rendimiento: evaluación de acceso p95 < 500ms (CS-003) contra SQL Server,
      incluido el paso de credencial en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/EvaluacionAccesoPerformanceTests.cs`
- [X] T140 [P] [US8] Prueba de integración: escenario Contratista→Principal — persona con compañía vigente
      CONTRATISTA, asignada a una unidad organizativa y con credencial de la Compañía Principal dueña del
      área, con permiso de alcance UNIDAD_ORGANIZATIVA, obtiene `CONCEDIDO` en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/EvaluacionAccesoContratistaTests.cs`
- [X] T141 [US8] Prueba de integración: **re-validación dinámica** (paso 5 del algoritmo) — una persona
      cambia de compañía de pertenencia sin que su `ContextoOperativoPersonaPrincipal` haya sido tocado por
      una revocación en cascada (p. ej. inconsistencia histórica simulada); la evaluación deniega igual
      porque re-deriva la legitimidad contra la compañía vigente en la fecha evaluada (RF-065, defensa
      adicional, no sustituye la cascada de US5) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/RevalidacionDinamicaTests.cs`

### Implementation for User Story 8

- [X] T142 [P] [US8] Crear entidad `PermisoAcceso` (Alcance, PersonaId/UnidadOrganizativaId/CompañíaId
      exclusivos —`CompañíaId` acepta cualquier `TipoCompañía`, research.md §12—, `FechaHoraInicioVigencia`,
      `FechaHoraFinVigencia` **NOT NULL** para los tres alcances (RF-021, RF-071 — corrige la
      documentación previa, que la trataba incorrectamente como opcional), Estado, `RowVersion` + `CHECK`
      de exclusividad por alcance; **no** sujeta a contención respecto a `AsignaciónPersonaCompañía` —
      RF-072 no le aplica) en `backend/src/EnterpriseAccessControl.Domain/Entities/PermisoAcceso.cs`
- [X] T143 [P] [US8] Crear entidad `BloqueHorarioPermiso` (DíaSemana, HoraInicio, HoraFin) en
      `backend/src/EnterpriseAccessControl.Domain/Entities/BloqueHorarioPermiso.cs`
- [X] T144 [US8] Implementar `PermisoAccesoService` (crear/actualizar permiso y bloques horarios, validar
      RF-039, validar alcance de compañías del usuario contra el `CompañíaPrincipalId` del área referenciada
      — RF-049, vía `CompaniaScopeAuthorizationHandler` de T014) en
      `backend/src/EnterpriseAccessControl.Application/Permissions/PermisoAccesoService.cs`
- [X] T145 [US8] Implementar `EvaluadorDeAcceso` como servicio de **dominio puro** (14 pasos ordenados de
      research.md §7, usa `RelojEmpresarial` de T018, denegación por defecto en cualquier paso ambiguo; NO
      depende de `HttpContext`/`ClaimsPrincipal`/ASP.NET Core Authorization — research.md §18). Paso 6
      (credencial vigente, RF-066/RF-070/RF-071) recibe como parámetro la `AsignacionCredencial` vigente ya
      resuelta por el orquestador de Aplicación (que la consulta desde `AsignaciónCredencial`, entidad
      creada en US5/T105) — el método de dominio en sí permanece puro, sin acceso directo a EF Core en
      `backend/src/EnterpriseAccessControl.Domain/Services/EvaluadorDeAcceso.cs`
- [X] T146 [US8] Implementar `PermisosController` conforme a `contracts/permissions.yaml` en
      `backend/src/EnterpriseAccessControl.Api/Controllers/PermisosController.cs`
- [X] T147 [US8] Implementar `EvaluacionAccesoController` + orquestador de Aplicación que carga persona,
      área, contexto operativo, `AsignacionCredencial` vigente (T105), perfil, UO y permisos, y los pasa al
      `EvaluadorDeAcceso` puro (T145) (`POST /api/evaluacion-acceso`, protegido por la política
      `CompaniaScope` para autorizar la propia consulta — paso 1 del algoritmo, RF-005) conforme a
      `contracts/access-evaluation.yaml` en
      `backend/src/EnterpriseAccessControl.Api/Controllers/EvaluacionAccesoController.cs`
- [X] T148 [P] [US8] Frontend: mantenimiento de permisos con selector semanal de bloques horarios y campos
      de vigencia obligatorios (inicio y fin) en `frontend/src/features/permissions/`
- [X] T149 [P] [US8] Frontend: formulario de prueba de evaluación de acceso (persona/área/fecha-hora →
      resultado, incluido el motivo `SIN_CREDENCIAL_VIGENTE`) en `frontend/src/features/access-evaluation/`

**Checkpoint**: Motor de evaluación de acceso completo de 14 pasos (incluido el gate de credencial), con
precedencia determinística, denegación por defecto, y soporte verificado para personal de compañías
contratistas accediendo a áreas de una Principal.

---

## Phase 11: User Story 9 - Mantenimiento de credencial/fotocheck por Compañía Principal (Priority: P2)

**Goal**: Completar la funcionalidad de usuario para credenciales — asignar, devolver y dar de baja lógica
— sobre la entidad `AsignaciónCredencial` ya construida en US5 (ver nota de secuenciación al inicio de este
documento): la entidad, su migración, su trigger de no-solapamiento y la revocación automática en cascada
ya existen y están probados desde US5; esta historia entrega el `CredencialesController` completo, las
operaciones `devolver`/`eliminar`, y la UI de mantenimiento (Historia 9, RF-017, RF-018, RF-056 a RF-058).

**Independent Test**: Asignar dos credenciales simultáneas a la misma persona para dos Principales
distintas (`POST /api/personas/{id}/credenciales`, `fechaHoraFin` obligatoria — ya cubierto por T098 de
US5); devolver una (`POST .../devolver`) y verificar `Estado = DEVUELTO`; dar de baja lógica la otra
(`DELETE`) y verificar `Estado = ELIMINADO`, registro conservado.

### Tests for User Story 9 ⚠️

- [X] T150 [P] [US9] Prueba de integración: `POST .../{id}/devolver` marca `Estado = DEVUELTO` y cierra la
      vigencia (rechazo `409` si la credencial no está en `ASIGNADO`) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Credentials/DevolverCredencialTests.cs`
- [X] T151 [P] [US9] Prueba de integración: `DELETE .../{id}` aplica baja lógica (`Estado = ELIMINADO`),
      registro histórico conservado íntegro en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Credentials/EliminarCredencialTests.cs`
- [X] T152 [P] [US9] Prueba de integración: `GET .../credenciales` lista el histórico ordenado por fecha de
      inicio descendente, filtrable por `companiaPrincipalId` en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Credentials/ListarCredencialesTests.cs`

### Implementation for User Story 9

- [X] T153 [US9] Extender `CredencialService` (creado en US5/T114) con `DevolverAsync` y `EliminarAsync`
      (baja lógica) en `backend/src/EnterpriseAccessControl.Application/Credentials/CredencialService.cs`
- [X] T154 [US9] Implementar `CredencialesController` completo (`GET` lista, `POST` asignar — ya cubierto
      funcionalmente desde US5 pero expuesto aquí como parte de la historia de usuario completa, `POST
      .../devolver`, `DELETE`) bajo `/api/personas/{personaId}/credenciales` conforme a
      `contracts/credentials.yaml` (el `POST` rechaza con `409` un solapamiento con otra credencial
      `ASIGNADO` de la misma persona y Principal, sin cerrar ni modificar la previa — Sesión 2026-09-15,
      decisión A) en
      `backend/src/EnterpriseAccessControl.Api/Controllers/CredencialesController.cs`
- [X] T155 [P] [US9] Frontend: histórico y asignación de credenciales por persona, agrupado por Compañía
      Principal, con acciones devolver/eliminar (un solapamiento se informa como rechazo; la credencial
      previa no se reemplaza — decisión A) en `frontend/src/features/credentials/`

**Checkpoint**: Mantenimiento completo de credenciales disponible vía API y UI; la regla fundamental de
revocación automática de Historia 5 (contextos + UO + credenciales) ya estaba completamente implementada y
probada desde US5 — esta historia no le agrega alcance, solo las operaciones de gestión directa.

---

## Phase 12: User Story 10 - Auditoría (Priority: P2)

**Goal**: Verificar que toda entidad persistente registra automáticamente Fecha de Registro, Fecha de
Última Actualización, ID Usuario Creación e ID Usuario Última Actualización, sin intervención manual
(Historia 10, RF-026, RF-027, CS-005). La implementación productiva ya existe desde la Fase 2
(`SaveChangesInterceptor`, T010); esta fase valida su cobertura end-to-end sobre entidades reales de las
historias ya construidas, incluidas las nuevas entidades de Contexto Operativo, credencial y la operación
de renovación.

**Independent Test**: Crear y luego actualizar una `Persona`, un `PermisoAcceso`, una
`ContextoOperativoPersonaPrincipal` y una `AsignaciónCredencial` a través de sus endpoints; renovar una
`AsignaciónPersonaCompañía`; confirmar en cada caso que `CreatedAt/UpdatedAt/CreatedById/UpdatedById` quedan
poblados correctamente y que ningún payload de entrada puede sobrescribirlos.

### Tests for User Story 10 ⚠️

- [X] T156 [P] [US10] Prueba de integración: estampado automático de auditoría en múltiples entidades
      (Persona, PermisoAcceso, ContextoOperativoPersonaPrincipal, AsignacionCredencial, Compañía,
      RelaciónContratistaPrincipal) a través de sus controladores, confirmando que los campos de auditoría
      no se aceptan desde el payload de entrada (CS-005) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Auditing/AuditoriaTests.cs`
- [X] T157 [US10] Prueba de integración: reconstrucción de la cadena de cambios (quién y cuándo) sobre una
      secuencia de actualizaciones a `Persona`, `PermisoAcceso` y `ContextoOperativoPersonaPrincipal`
      (incluida la revocación en cascada de US5, que estampa `UpdatedById` con el usuario que registró el
      cese, y la renovación de pertenencia de RF-073) en
      `backend/tests/EnterpriseAccessControl.IntegrationTests/Auditing/TrazabilidadTests.cs`
- [X] T158 [P] [US10] Prueba de contrato: ningún esquema de `contracts/*.yaml` expone
      `createdAt/updatedAt/createdById/updatedById` como campo editable en los `*Request` en
      `backend/tests/EnterpriseAccessControl.ContractTests/AuditFieldsNotExposedTests.cs`

**Checkpoint**: Cobertura de auditoría automática verificada end-to-end sobre entidades reales del sistema,
incluidas las de Contexto Operativo, credencial y renovación de pertenencia.

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: Validación final de extremo a extremo y endurecimiento aplicable a todas las historias.

- [X] T159 [P] Automatizar el escenario de validación de [quickstart.md](./quickstart.md) sección 5
      completa (CS-009, 12 pasos — incluye la asignación de credencial obligatoria para el gate de RF-066,
      agregada tras la corrección de vigencia temporal) como prueba end-to-end en
      `frontend/tests/e2e/cs009-quickstart.spec.ts`
- [X] T160 [P] Automatizar el escenario de validación de [quickstart.md](./quickstart.md) sección 6 (12
      pasos, multi-Principal Pedro García/Servicios ACME, incluidas fechas obligatorias y contención) como
      prueba end-to-end en `frontend/tests/e2e/multi-principal-quickstart.spec.ts`
- [X] T161 [P] Auditoría de accesibilidad del componente Tree (navegación completa por teclado, RF-036,
      WCAG 2.2 AA) en `frontend/tests/unit/Tree.a11y.test.tsx`
- [X] T162 [P] Prueba de snapshot: el documento OpenAPI nativo publicado por la API
      (`Microsoft.AspNetCore.OpenApi`, `/openapi/v1.json`) coincide con los esquemas de `contracts/*.yaml`
      (incluido el endpoint `/renovar` y el enum `MotivoDenegacion` de 11 valores) en
      `backend/tests/EnterpriseAccessControl.ContractTests/OpenApiSnapshotTests.cs`
- [X] T163 Revisión de endurecimiento de seguridad: confirmar que todo endpoint protegido exige
      autenticación y aplica la política `CompaniaScope` (T014), incluidos los endpoints de
      `UnidadOrganizativa`, `ÁreaAcceso`, `RelaciónContratistaPrincipal`, `ContextoOperativoPersonaPrincipal`
      y `/renovar` — CS-004, RF-049, RF-060 — en `backend/src/EnterpriseAccessControl.Api/Controllers/`
- [X] T164 Revisión de los 5 triggers SQL Server de no-solapamiento (`AsignaciónPersonaCompañía`,
      `RelaciónContratistaPrincipal`, `ContextoOperativoPersonaPrincipal`,
      `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial`): confirmar que todos están aplicados
      como parte de las migraciones de EF Core (no como scripts sueltos) y que
      `Testcontainers.MsSql` los ejecuta correctamente en `IntegrationTests` en
      `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/`
- [X] T165 Revisión de las columnas `NOT NULL` introducidas por RF-071 (`FechaHoraFin`/`FechaHoraFinVigencia`
      en las 6 entidades: `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`,
      `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial`, `AsignaciónTipoPersona`,
      `PermisoAcceso`) y de las 3 validaciones de contención temporal de RF-072 (`ContextoOperativoService`,
      `AsignacionUnidadOrganizativaService`, `CredencialService`) — confirmar que todas usan
      `ContencionTemporalValidator` (T107) sin lógica duplicada en
      `backend/src/EnterpriseAccessControl.Application/`
- [X] T166 Verificar que `GET /health/live` y `GET /health/ready` (T025) responden correctamente en el
      contenedor Docker compuesto (`docker-compose.yml`, T007), incluida la caída de SQL Server reflejada
      en `/health/ready`
- [X] T167 [P] Limpieza de código: eliminar duplicación entre `HistorialPersonaService`/`RevocacionService`
      (US5) y `CredencialService` (US5/US9) extrayendo lógica común de cierre de vigencia si aplica en
      `backend/src/EnterpriseAccessControl.Application/Common/`
- [X] T168 [P] Documentar despliegue (docker-compose de producción con SQL Server, variables de entorno
      requeridas, incluidas las de Options Pattern de T008) en `README.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias — puede iniciar de inmediato.
- **Foundational (Fase 2)**: depende de Setup — BLOQUEA todas las historias de usuario.
- **Historias de usuario (Fase 3+)**: todas dependen de Foundational.
  - Deben implementarse en orden de prioridad P1 (US1→US8) porque el modelo de datos tiene dependencias de
    FK hacia adelante entre historias: US5 usa entidades de US2/US3/US4 **y ahora incluye
    `AsignaciónCredencial` (normalmente de "Historia 9"), movida aquí por la dependencia estructural de
    US8/RF-066** (ver nota de secuenciación al inicio del documento); US6 usa `Compañía` con `TipoCompañía`
    (de Foundational/US2); US7/US8 usan entidades de US3/US4/US6; **US8 depende de US5 para
    `AsignaciónCredencial`, no de US9** (a diferencia de la generación anterior, donde esa dependencia iba
    hacia US9). La única dependencia "hacia atrás" restante (US1 necesita `Compañía`) se resolvió moviendo
    la entidad base `Compañía` a Foundational (T012).
  - US9 (P2) depende de US5 (T105, T114 — la entidad y el método `AsignarAsync` ya existen) y solo agrega
    `DevolverAsync`/`EliminarAsync`/UI. US10 (P2) depende de que las historias P1 que toca (Persona,
    Permisos, Contexto Operativo, Credencial) ya existan.
- **Polish (Fase final)**: depende de que todas las historias deseadas estén completas.

### User Story Dependencies

| Historia | Depende de (entidades) | Nota |
|---|---|---|
| US1 (P1) | Foundational (`Compañía` con `TipoCompañía` en T012) | MVP: login + alcance |
| US2 (P1) | Foundational | Compañía (CRUD completo) + UnidadOrganizativa + `RelaciónContratistaPrincipal` |
| US3 (P1) | Foundational | Catálogos maestros, independiente de US1/US2 |
| US4 (P1) | US3 (TipoDocumento, TipoSangre, Género) | Persona |
| US5 (P1) | US2 (Compañía, `RelaciónContratistaPrincipal`, UnidadOrganizativa), US3 (TipoPersona, **TipoCredencial**), US4 (Persona) | Históricos, Contexto Operativo, **credencial**, renovación, revocación completa (las 3 dependientes) |
| US6 (P1) | US2 (Compañía con `TipoCompañía` para validar `CompañíaPrincipalId`) | Árbol de áreas, aislado por Principal |
| US7 (P1) | US3 (TipoPersona), US6 (ÁreaAcceso) | Asociación área↔perfil |
| US8 (P1) | US4 (Persona), **US5** (estado efectivo, contexto operativo, **`AsignaciónCredencial`**), US6 (ÁreaAcceso), US7 (elegibilidad) | Evaluación de acceso de 14 pasos, incluye gate de credencial, escenario Contratista→Principal y re-validación dinámica |
| US9 (P2) | **US5** (T105 entidad, T114 `AsignarAsync`) | Completa devolver/eliminar/UI sobre la entidad ya construida — ya no depende de nada que US8 necesite |
| US10 (P2) | Foundational (interceptor ya activo) + entidades de US2/US4/US5/US8 para las pruebas | Validación de auditoría |

**Nota sobre el cambio de secuenciación respecto a la generación anterior**: la versión previa de 158
tareas construía `AsignaciónCredencial` en US9 (P2) y dividía `RevocacionService` en dos pasadas (T100 en
US5 para contexto+UO, T145 en US9 para extenderlo con credencial), porque en ese momento el dominio no
exigía credencial para conceder acceso. RF-066 (Sesión posterior) cambió eso: la credencial pasó a ser un
paso incondicional del algoritmo de evaluación (P1, US8). Mantener la entidad en US9 habría dejado a US8
—una historia P1— con una dependencia dura hacia una historia P2, rompiendo la regla de que las historias
P1 deben poder completarse sin depender de trabajo P2. Mover solo la entidad, su migración, su trigger y el
método `AsignarAsync` a US5 (que de todas formas ya necesitaba tocar `AsignaciónCredencial` para su propia
cascada de revocación) resuelve la dependencia sin adelantar ningún trabajo de **valor de usuario** de
Historia 9 (que sigue siendo P2: devolver, eliminar y la UI de mantenimiento). Esto no reabre la Decisión
Pendiente #7 de spec.md (¿debe subir Historia 9 completa a P1?) — esa pregunta sigue sin resolver y sigue
siendo una decisión de negocio, no de secuenciación técnica.

### Within Each User Story

- Pruebas de contrato/integración/unitarias antes de la implementación (deben fallar primero).
- Entidades de dominio antes que servicios de Aplicación.
- Servicios de Aplicación antes que controladores de Api.
- Backend (entidad → servicio → controlador) antes que la pantalla de frontend que lo consume.
- Historia completa y verificada (checkpoint) antes de iniciar la siguiente en el plan secuencial.

### Parallel Opportunities

- Todas las tareas [P] de Setup pueden ejecutarse en paralelo.
- Dentro de Foundational, T009, T012–T025 son paralelizables entre sí (T010 depende de T009; T011 depende
  de T010).
- US3 no depende de otras historias P1 y puede avanzar en paralelo con US2 una vez completado Foundational,
  si hay capacidad de equipo. US6 depende de US2 (para `CompañíaPrincipalId`), por lo que no es
  completamente paralelizable con ella.
- Dentro de cada historia, todas las pruebas marcadas [P] pueden lanzarse juntas, y las entidades de
  dominio marcadas [P] pueden crearse en paralelo antes de que el servicio que las combina las requiera.

---

## Parallel Example: User Story 5

```bash
# Lanzar juntas las entidades de dominio de la Historia 5 (todas [P], sin dependencias entre sí):
Task: "Crear entidad ContextoOperativoPersonaPrincipal en backend/src/.../Entities/ContextoOperativoPersonaPrincipal.cs"
Task: "Crear entidad AsignaciónPersonaUnidadOrganizativa en backend/src/.../Entities/AsignacionPersonaUnidadOrganizativa.cs"
Task: "Crear entidad AsignaciónCredencial en backend/src/.../Entities/AsignacionCredencial.cs"
Task: "Crear entidad AsignaciónTipoPersona en backend/src/.../Entities/AsignacionTipoPersona.cs"

# Lanzar juntas las pruebas de contención temporal y fechas obligatorias (RF-071/RF-072, todas [P]):
Task: "Integration test de contención temporal en backend/tests/.../People/ContencionTemporalTests.cs"
Task: "Integration test de fechas obligatorias en backend/tests/.../People/FechasObligatoriasTests.cs"
Task: "Integration test de renovación de pertenencia en backend/tests/.../People/RenovacionPertenenciaTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 solamente)

1. Completar Fase 1: Setup.
2. Completar Fase 2: Foundational (crítico — bloquea todas las historias).
3. Completar Fase 3: User Story 1.
4. **DETENER y VALIDAR**: probar Historia 1 de forma independiente (login + alcance).
5. Desplegar/demostrar si está listo.

### Incremental Delivery

1. Setup + Foundational → base lista (incluye SQL Server, `ProblemDetails`, OpenAPI nativo, Health Checks).
2. US1 → probar de forma independiente → demo (MVP funcional de autenticación).
3. US2 → probar de forma independiente → demo (compañías Principal/Contratista + relaciones + unidades
   organizativas aisladas por Principal).
4. US3 → probar de forma independiente → demo (catálogos, incluido `TipoCredencial`).
5. US4 → probar de forma independiente → demo (personas).
6. US5 → probar de forma independiente → demo (contexto operativo multi-Principal + credencial por
   Principal + renovación de pertenencia + revocación automática completa de las tres dependientes — el
   hito más sensible y ahora más completo de todo el dominio).
7. US6 → probar de forma independiente → demo (árbol de áreas por Compañía Principal).
8. US7 → probar de forma independiente → demo (elegibilidad por área).
9. US8 → probar de forma independiente → demo (permisos + evaluación de acceso de 14 pasos, incluye gate de
   credencial, escenario Contratista→Principal y re-validación dinámica — cierre del alcance P1, ya sin
   ninguna dependencia pendiente hacia P2).
10. US9 → probar de forma independiente → demo (devolver/eliminar credencial + UI completa; la entidad y
    su cascada ya funcionaban desde US5).
11. US10 → validar cobertura de auditoría end-to-end (P2).
12. Fase de Polish → validación completa de quickstart.md (CS-009 de 12 pasos + escenario multi-Principal
    de 12 pasos), triggers SQL Server, columnas `NOT NULL` de RF-071, validadores de contención de RF-072,
    health checks, y endurecimiento final.

### Parallel Team Strategy

Con varios desarrolladores:

1. El equipo completa Setup + Foundational en conjunto.
2. Una vez lista Foundational:
   - Desarrollador A: US1 → luego US5 (usa entidades de US2/US3/US4) — la historia más compleja, ahora
     incluye credencial y renovación
   - Desarrollador B: US2 (compañías + relaciones + unidades organizativas) → luego US7
   - Desarrollador C: US3 (incluido `TipoCredencial`) → luego US4 → luego US9 (requiere T105/T114 de US5
     completos antes de extender `CredencialService`)
   - Desarrollador D: US6 (requiere US2 completa) → luego US8 (requiere además US4/US5/US7 — **incluida
     `AsignaciónCredencial` de US5**, ya no de US9)
3. US8 (evaluación de acceso) es el punto de integración crítico: requiere US4, US5 (incluida
   `AsignaciónCredencial`), US6 y US7 completas — y **ya no** depende de ningún trabajo de US9. US9 solo
   necesita que US5 haya entregado T105/T114 para extender el servicio con devolver/eliminar.

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes.
- [Story] mapea cada tarea a su historia de usuario para trazabilidad.
- Cada historia debe ser completable y probable de forma independiente, salvo las dependencias de FK ya
  documentadas y el movimiento explícito y justificado de `AsignaciónCredencial` de US9 a US5 (ver nota de
  secuenciación al inicio del documento) — no evitable sin dejar a una historia P1 (US8) dependiendo
  estructuralmente de una historia P2 (US9), lo cual contradice el objetivo de independencia P1/P2 del
  propio framework de historias de usuario.
- Verificar que las pruebas fallan antes de implementar (Principio VII).
- Hacer commit después de cada tarea o grupo lógico.
- Detenerse en cada checkpoint para validar la historia de forma independiente.
- Evitar: tareas vagas, conflictos por archivo compartido, y dependencias cruzadas entre historias que no
  estén ya documentadas en la tabla de Dependencies.
- **Ninguna tarea de este documento hereda contenido de la versión anterior de 158 tareas sin revisar**: se
  verificó explícitamente que las 6 entidades afectadas por RF-071 (fechas obligatorias), las 3 por RF-072
  (contención temporal), la operación de RF-073 (renovación), y el paso 6 de RF-066/RF-070 (gate de
  credencial en el algoritmo de 14 pasos) están reflejadas en las tareas correspondientes — ver informe de
  cierre.
- **No se generaron tareas para RF-067 a RF-069** (consultas agregadas de auditoría/históricos/indicadores
  — Dashboard, Auditoría transversal, Históricos transversales de `ux-ui.md` §8/§20/§21): estos RF siguen
  sin contrato en `contracts/` (confirmado en la última auditoría de `/speckit-plan`), por lo que no hay
  ningún endpoint que mapear a una tarea de implementación — generar tareas para un contrato inexistente
  habría significado inventar la forma del endpoint, violando la instrucción de no inventar diseño no
  especificado.

---

# Cierre de Etapa 1 — Tareas posteriores a T168 (Sesión 2026-09-20)

**Origen**: decisiones D1 a D9 (matriz de 16 preguntas, todas cerradas), RF-074 a RF-081, CS-036 a CS-041, y
la formalización de la administración de usuarios en `ux-ui.md` §35 (UX-17 a UX-22).

**Regla de numeración**: estas tareas comienzan en **T169** y son estrictamente aditivas. **T001 a T168 no se
regeneran, no se renumeran y no se reinterpretan**: pertenecen al baseline histórico ya implementado y
validado. Ninguna tarea de este bloque repite trabajo ya cubierto por ellas.

**Fuera de alcance (Etapa 2)**: no se genera ninguna tarea para RF-067, RF-068, RF-069 ni CS-032 (consultas
transversales de auditoría, históricos e indicadores — Historia 10), diferidos explícitamente por la decisión
D8 y anotados `[DIFERIDA A ETAPA 2]` en `spec.md`.

**Correcciones documentales ya aplicadas**: las sesiones previas sincronizaron `spec.md`, `plan.md`,
`research.md`, `data-model.md`, `contracts/`, `quickstart.md`, `ux-ui.md` y `README.md`. No se generan tareas
para repetirlas; solo se incluye documentación cuando es consecuencia directa de implementar una tarea nueva.

---

## Phase 14: Modelo de datos y migraciones

**Purpose**: Estructuras de persistencia que bloquean todo lo demás de este bloque.

**⚠️ CRITICAL**: Ninguna tarea de las fases 15 a 22 puede comenzar hasta completar esta fase.

- [X] T169 [P] [US1] Crear enum `RolAdministrativo` con exactamente dos valores (`GLOBAL_ADMINISTRATOR`, `COMPANY_ADMINISTRATOR`) como enum cerrado del dominio, con value conversion a `nvarchar` (research.md §17, RF-074) en `backend/src/EnterpriseAccessControl.Domain/Enums/RolAdministrativo.cs`
- [X] T170 [US1] Crear entidad `AsignacionRolAdministrativo` (`UsuarioId`, `Rol`, `CompaniaId` nullable, `FechaHoraInicio`, `FechaHoraFin` **NOT NULL**, `RowVersion`, auditoría heredada) y su `IEntityTypeConfiguration` (RF-074, RF-075) en `backend/src/EnterpriseAccessControl.Domain/Entities/AsignacionRolAdministrativo.cs`
- [X] T171 [US1] Migración EF Core que crea la tabla `AsignacionRolAdministrativo`, el `CHECK` de la regla fundamental (`Rol = GLOBAL_ADMINISTRATOR` ⇒ `CompaniaId IS NULL`; `Rol = COMPANY_ADMINISTRATOR` ⇒ `CompaniaId IS NOT NULL`) y el índice clúster sobre `CreatedAt` conforme research.md §20 (RF-074) en `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/`
- [X] T172 [US1] Agregar en la misma migración el **trigger SQL Server de no-solapamiento** particionado por `(UsuarioId, CompaniaId)` y filtrado a filas `Rol = COMPANY_ADMINISTRATOR`, siguiendo la plantilla de research.md §5; `GLOBAL_ADMINISTRATOR` queda exento (RF-075) en `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/`
- [X] T173 [US1] Eliminar la entidad `AlcanceUsuarioCompania`, su `DbSet`, su configuración y su tabla mediante una migración de esquema — sin transformar ni conservar sus filas: el proyecto es greenfield, sin datos de producción ni de desarrollo que deban preservarse. Queda reemplazada por `AsignacionRolAdministrativo` (RF-074) en `backend/src/EnterpriseAccessControl.Domain/Entities/AlcanceUsuarioCompania.cs` y `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/`
- [X] T174 [P] [US2] Agregar el campo `ZonaHorariaIana` (string, identificador IANA) a la entidad `Compania`, con migración y valor obligatorio cuando `TipoCompania = PRINCIPAL_MANDANTE` (RF-080) en `backend/src/EnterpriseAccessControl.Domain/Entities/Compania.cs` y `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/`

**Checkpoint**: esquema listo — el modelo RBAC y la zona horaria por Principal existen en base de datos.

---

## Phase 15: Dominio y reglas de negocio

**Purpose**: Reglas que no dependen de la capa de autorización ni de la API.

- [X] T175 [US1] Implementar `AsignacionRolAdministrativoService` con las operaciones crear, listar, finalizar y renovar: valida la regla fundamental de `CompaniaId`, exige `FechaHoraInicio`/`FechaHoraFin` reales, rechaza solapamientos y aplica las reglas de renovación de RF-073 (solo hacia adelante y solo mientras siga vigente dinámicamente) — RF-074, RF-075 — en `backend/src/EnterpriseAccessControl.Application/Auth/AsignacionRolAdministrativoService.cs`
- [X] T176 [P] [US2] Implementar `DependenciasTipoCompaniaValidator` que cuenta las cinco categorías de dependencia incompatible (áreas de acceso, raíces de unidad organizativa, `RelacionContratistaPrincipal` vigentes en **ambas** direcciones, contextos operativos, credenciales) sin inspeccionar `AsignacionPersonaCompania` ni personas (RF-081) en `backend/src/EnterpriseAccessControl.Application/Companies/DependenciasTipoCompaniaValidator.cs`
- [X] T177 [US2] Integrar ese validador en `CompaniaService.ActualizarAsync` para rechazar el cambio de `TipoCompania` con `409 CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS` y detalle accionable, sin cascada ni modificación automática de dependientes (RF-081, RF-033) en `backend/src/EnterpriseAccessControl.Application/Companies/CompaniaService.cs`
- [X] T178 [US8] Ampliar `EvaluadorDeAcceso` con el **paso 5** (Compañía Principal propietaria del área con `Estado = ACTIVO`) y con la verificación de `Estado = ACTIVO` de la compañía de pertenencia vigente dentro del **paso 6**, denegando con `COMPANIA_INACTIVA` sin escribir nada y de forma reversible (RF-079, research.md §7 y §30) en `backend/src/EnterpriseAccessControl.Application/Permissions/`
- [X] T179 [US8] Rediseñar `IRelojEmpresarial` para recibir la Compañía Principal cuya zona debe resolverse, y `RelojEmpresarial` para obtener `Compania.ZonaHorariaIana` con respaldo en `ZonaHoraria:TimeZoneId`; `IRelojSistema` (`UtcNow`) no cambia (RF-080, research.md §31) en `backend/src/EnterpriseAccessControl.Domain/Common/IRelojEmpresarial.cs` y `backend/src/EnterpriseAccessControl.Infrastructure/Security/RelojEmpresarial.cs`
- [X] T180 [US8] Actualizar el **paso 13** de `EvaluadorDeAcceso` para evaluar día de semana y bloque horario en la zona de la Principal determinada en el paso 4, y exponer esa zona en `evaluadoEnZonaHoraria` (RF-080) en `backend/src/EnterpriseAccessControl.Application/Permissions/`
- [X] T181 [US2] Validar el identificador IANA de `ZonaHorariaIana` **por request** al crear o actualizar una compañía (hoy solo se valida una vez al arrancar), rechazando zonas no reconocidas (RF-080) en `backend/src/EnterpriseAccessControl.Application/Companies/`

**Checkpoint**: reglas de dominio completas — evaluables por prueba unitaria sin API ni autorización.

---

## Phase 16: Autorización, RBAC y Resource Ownership

**Purpose**: Cierra F-01 y F-02, el defecto crítico que impedía congelar el baseline.

- [X] T182 [US1] Redefinir `IAlcanceCompaniaAccessor`/`AlcanceCompaniaAccessor` para resolver el alcance efectivo desde las asignaciones vigentes: `GLOBAL_ADMINISTRATOR` ⇒ `EstaEnAlcance` verdadero para toda compañía sin enumerarlas; en caso contrario, membresía contra las compañías de sus asignaciones `COMPANY_ADMINISTRATOR` vigentes (RF-074, RF-077) en `backend/src/EnterpriseAccessControl.Infrastructure/Security/AlcanceCompaniaAccessor.cs`
- [X] T183 [US1] Redefinir `CompaniaScopeAuthorizationHandler` para exigir al menos una asignación de rol vigente en vez de `CompaniaIds.Count > 0`, que hoy denegaría a un `GLOBAL_ADMINISTRATOR` sin compañías enumeradas (RF-074, RF-077) en `backend/src/EnterpriseAccessControl.Infrastructure/Security/CompaniaScopeAuthorizationHandler.cs`
- [X] T184 [US1] Emitir en el JWT un claim de rol y, solo para `COMPANY_ADMINISTRATOR`, un claim por compañía asignada, reemplazando el esquema actual de "un claim por compañía" que no puede representar el alcance GLOBAL (RF-074) en `backend/src/EnterpriseAccessControl.Infrastructure/Security/JwtTokenService.cs` y `ClaimsPersonalizados.cs`
- [X] T185 [US1] Reescribir las siete operaciones de `UsuarioService` aplicando Resource Ownership y las restricciones de `COMPANY_ADMINISTRATOR`: filtrar listado y detalle por alcance, restringir creación al rol/compañía que el solicitante puede asignar, y reemplazar `ObtenerAlcanceAsync`/`ReemplazarAlcanceAsync` por operaciones sobre asignaciones individuales (RF-076, RF-077) en `backend/src/EnterpriseAccessControl.Application/Auth/UsuarioService.cs`
- [X] T186 [P] [US6] Aplicar Resource Ownership en `UnidadOrganizativaService` y `AreaAccesoService`, hoy sin ningún control de alcance pese a RF-049 (RF-077) en `backend/src/EnterpriseAccessControl.Application/OrgUnits/UnidadOrganizativaService.cs` y `backend/src/EnterpriseAccessControl.Application/AreaAccess/AreaAccesoService.cs`
- [X] T187 [P] [US5] Aplicar Resource Ownership en `AsignacionUnidadOrganizativaService`, `EstadoEfectivoService` y `RevocacionService`, hoy sin ningún control de alcance (RF-077, corrige F-02) en `backend/src/EnterpriseAccessControl.Application/People/`
- [X] T188 [US4] Implementar la regla de alcance de `Persona` por **unión** —compañía de pertenencia vigente **o** cualquier contexto operativo vigente con una Principal del alcance— en `PersonaService` y `HistorialPersonaService` (RF-077) en `backend/src/EnterpriseAccessControl.Application/People/`
- [X] T189 [US1] Auditar y completar la verificación de recurso concreto en los servicios que ya referencian el alcance parcialmente (`CompaniaService`, `CredencialService`, `ContextoOperativoService`, `RelacionContratistaPrincipalService`), asegurando lectura fuera de alcance ⇒ `404` sin revelar existencia (RF-077) en `backend/src/EnterpriseAccessControl.Application/`

**Checkpoint**: F-01 y F-02 cerrados — ningún usuario puede operar fuera de su alcance.

---

## Phase 17: Bootstrap del primer administrador

**Purpose**: Un despliegue desde cero queda operable sin intervención manual en base de datos.

- [X] T190 [P] [US1] Crear `BootstrapOptions` (`Bootstrap:AdminEmail` obligatorio y con formato de correo válido; `Bootstrap:AdminPassword` obligatorio) enlazado por Options Pattern con `ValidateOnStart()`, admitiendo `Bootstrap__AdminPassword` como variable de entorno y **sin valor por defecto versionado** (RF-078) en `backend/src/EnterpriseAccessControl.Application/Common/Options/BootstrapOptions.cs`
- [X] T191 [US1] Definir la constante `MAX_VALIDITY_DATE` (`2999-12-31T23:59:59Z`) usada **exclusivamente** por la asignación de bootstrap, sin exponerla como opción de configuración ni reutilizarla en ninguna otra asignación (RF-075, RF-078) en `backend/src/EnterpriseAccessControl.Domain/Common/`
- [X] T192 [US1] Implementar la rutina de arranque **idempotente** que, después de aplicar migraciones, crea el `Usuario` y su `AsignacionRolAdministrativo` `GLOBAL_ADMINISTRATOR` solo si no existe ninguna, validando la contraseña contra `PasswordPolicyValidator`, fijando `RequiereCambioPassword = true` y `FechaHoraFin = MAX_VALIDITY_DATE`, con `CreatedById`/`UpdatedById` nulos (creado por el sistema) — RF-078 — en `backend/src/EnterpriseAccessControl.Api/Program.cs` o un `IHostedService` dedicado
- [X] T193 [US1] Documentar las dos variables de bootstrap en el despliegue de producción como consecuencia directa de T190/T192, sin valor de respaldo committeado (RF-078) en `README.md` y `docker-compose.prod.yml`

**Checkpoint**: una base recién migrada arranca con un administrador operable y contraseña por cambiar.

---

## Phase 18: API y contratos

**Purpose**: Alinear el código con los contratos ya actualizados documentalmente.

- [X] T194 [US1] Reescribir `UsuariosController` conforme a `contracts/users.yaml` v2: crear usuario con rol y vigencia, `GET/POST /api/usuarios/{id}/roles`, `POST /api/usuarios/{id}/roles/{asignacionId}/finalizar`, y retirar `alcance-companias` (RF-074 a RF-077) en `backend/src/EnterpriseAccessControl.Api/Controllers/UsuariosController.cs`
- [X] T195 [P] [US1] Actualizar el flujo de login para devolver `rol` y `companiaIds` en lugar de `alcanceCompanias`, conforme a `contracts/auth.yaml` (RF-074) en `backend/src/EnterpriseAccessControl.Application/Auth/AutenticacionService.cs` y `backend/src/EnterpriseAccessControl.Api/Controllers/AuthController.cs`
- [X] T196 [P] [US2] Exponer `zonaHorariaIana` en crear y actualizar compañía, y el `409` de dependencias incompatibles, conforme a `contracts/companies.yaml` (RF-080, RF-081) en `backend/src/EnterpriseAccessControl.Api/Controllers/CompaniasController.cs`
- [X] T197 [P] [US8] Incorporar el motivo `COMPANIA_INACTIVA` al enum de respuesta y poblar `evaluadoEnZonaHoraria` con la zona de la Principal, conforme a `contracts/access-evaluation.yaml` (RF-079, RF-080) en `backend/src/EnterpriseAccessControl.Application/Permissions/EvaluacionAccesoDtos.cs`
- [X] T198 [P] Registrar los códigos de negocio nuevos (`CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS` y los de asignación de rol) en el catálogo de códigos de error, y declararlos en los contratos donde falten (RF-033, cierra F-11 para los códigos nuevos) en `backend/src/EnterpriseAccessControl.Application/Common/Errores/CodigosError.cs`

**Checkpoint**: el documento OpenAPI publicado vuelve a coincidir con `contracts/`.

---

## Phase 19: Frontend — Administración de usuarios (G7)

**Goal**: Operar RF-074 a RF-077 desde la interfaz, conforme a `ux-ui.md` §35.

**Independent Test**: un `COMPANY_ADMINISTRATOR` solo ve y opera usuarios de su compañía; un `GLOBAL_ADMINISTRATOR` opera sobre todas.

- [X] T199 [US1] Crear el cliente HTTP y los hooks de usuarios y asignaciones de rol (listar, crear, asignar, finalizar, renovar) en `frontend/src/features/users/api.ts` y `frontend/src/features/users/hooks.ts`
- [X] T200 [US1] Implementar el listado con columnas, filtros y aislamiento de alcance —sin revelar usuarios ajenos en resultados, totales ni paginación— (UX-22, `ux-ui.md` §35) en `frontend/src/features/users/UsuariosPage.tsx`
- [X] T201 [US1] Implementar el wizard de creación de usuario en cinco pasos (identidad, rol, compañía condicional, vigencia obligatoria, confirmación), ocultando el rol que el operador no puede asignar (UX-17) en `frontend/src/features/users/CrearUsuarioWizard.tsx`
- [X] T202 [US1] Implementar el detalle de usuario con tabs Resumen, Asignaciones e Histórico, distinguiendo estado de la asignación y vigencia efectiva por fechas (UX-18, `ux-ui.md` §16) en `frontend/src/features/users/UsuarioDetalle.tsx`
- [X] T203 [US1] Implementar la asignación de rol a un usuario existente, dejando explícito que agrega y no reemplaza asignaciones (UX-19) en `frontend/src/features/users/AsignarRolDialogo.tsx`
- [X] T204 [US1] Implementar finalizar y renovar una asignación, con confirmación para ambas y sin permitir editar rol ni compañía de una asignación existente (UX-20) en `frontend/src/features/users/`
- [X] T205 [P] [US1] Implementar los mensajes de error específicos de la tabla de `ux-ui.md` §35 (rol o compañía no permitidos, alcance insuficiente, correo duplicado, solapamiento, vigencia inválida, contraseña fuera de política), sin revelar datos fuera de alcance (UX-21) en `frontend/src/features/users/`
- [X] T206 [P] [US1] Añadir la entrada **Configuración → Usuarios y roles administrativos** a la navegación y sus rutas, con guard por rol (`ux-ui.md` §7 y §35) en `frontend/src/app/`

**Checkpoint**: G7 operable de extremo a extremo desde la interfaz.

---

## Phase 20: Frontend — Interfaz de Historia 5, Casos A y B

**Goal**: Completar el alcance funcional real de T116 dentro de Etapa 1 (decisión D7), sin convertirlo en funcionalidad de Etapa 2.

**Independent Test**: crear pertenencia, abrir contexto y asignar unidad organizativa y perfil, íntegramente desde la interfaz, produciendo las mismas relaciones de dominio que la API.

- [X] T207 [US5] Implementar el wizard de asignación con comportamiento Caso A (Principal fijada automáticamente, RF-053) y Caso B (selector de Principal limitado a relaciones vigentes de la Contratista, RF-054), invocando los hooks ya existentes `useCrearPertenencia` y `useAbrirContexto` en `frontend/src/features/people/AsignacionUnidadOrganizativa/`
- [X] T208 [US5] Reemplazar el selector plano de unidad organizativa por el componente `Tree` ya existente, mostrando exclusivamente el árbol de la Principal del contexto (CS-021) en `frontend/src/features/people/AsignacionUnidadOrganizativa/AsignacionUnidadOrganizativa.tsx` reutilizando `frontend/src/components/Tree/Tree.tsx`
- [X] T209 [P] [US5] Crear el cliente HTTP, los hooks y la interfaz de asociación de perfil (`AsignacionTipoPersona`) contra el endpoint `/personas/{id}/perfiles` ya existente, hoy sin ninguna pieza en el frontend en `frontend/src/features/people/`
- [X] T210 [P] [US5] Exponer desde `PersonaHistorialPage` los puntos de entrada a crear pertenencia y abrir contexto, hoy inalcanzables pese a tener hooks funcionales en `frontend/src/features/people/history/PersonaHistorialPage.tsx`
- [X] T211 [P] [US5] Interpretar y presentar las fechas de los formularios en la zona horaria de la Compañía Principal correspondiente, con respaldo global cuando no sea resoluble (RF-080) en `frontend/src/lib/`
- [X] T212 [P] [US5] Sustituir el UUID mostrado como respaldo cuando no resuelve el nombre de una unidad organizativa por un estado de carga o marcador, conforme RF-013, dentro del wizard de asignación de Historia 5 en `frontend/src/features/people/AsignacionUnidadOrganizativa/AsignacionUnidadOrganizativa.tsx`

**Checkpoint**: Historia 5 deja de ser operable solo por API.

---

## Phase 21: Pruebas

**Purpose**: Demostrar cada regla nueva. Ninguna prueba de T001 a T168 cubre automáticamente una regla de RF-074 a RF-081.

- [X] T213 [P] [US1] Pruebas unitarias del dominio de asignaciones de rol: regla fundamental de `CompaniaId`, vigencia obligatoria, rechazo de solapamiento, multiplicidad por compañías distintas y renovación conforme RF-073 en `backend/tests/EnterpriseAccessControl.UnitTests/Auth/`
- [X] T214 [US1] Pruebas de integración de RBAC y alcance que cubren **CS-036**: un `COMPANY_ADMINISTRATOR` no lista ni modifica usuarios de otra compañía, no se autoeleva, no crea `GLOBAL_ADMINISTRATOR`, y sí crea pares de su mismo nivel en su compañía en `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/`
- [X] T215 [US1] Pruebas de integración de aislamiento que cubren **CS-037**: toda lectura fuera de alcance responde `404` con identificador correcto y conocido, en usuarios, compañías, unidades organizativas, áreas, personas, contextos y credenciales en `backend/tests/EnterpriseAccessControl.IntegrationTests/`
- [X] T216 [US1] Prueba de integración del bootstrap que cubre **CS-038**: base vacía ⇒ se crea exactamente un `GLOBAL_ADMINISTRATOR` con cambio de contraseña pendiente; segundo arranque ⇒ no se crea otro; la contraseña sembrada cumple la política y `MAX_VALIDITY_DATE` no aparece en ninguna otra asignación en `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/`
- [X] T217 [P] [US8] Prueba de integración que cubre **CS-039**: inactivar la Principal deniega con `COMPANIA_INACTIVA` sin alterar contextos, credenciales ni permisos; reactivar restablece el acceso sin más intervención; repetir para la compañía de pertenencia de la persona en `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/`
- [X] T218 [P] [US8] Prueba de integración que cubre **CS-040**: dos Principales con zonas IANA distintas evalúan el mismo instante UTC contra sus propios bloques horarios; cambiar la zona de una no altera ningún instante persistido en `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/`
- [X] T219 [P] [US2] Prueba de integración que cubre **CS-041**: el cambio de `TipoCompania` se rechaza por cada una de las cinco categorías de dependencia y en ambas direcciones, y se acepta sin dependencias en `backend/tests/EnterpriseAccessControl.IntegrationTests/Companies/`
- [X] T220 [P] Actualizar las clases de prueba de contrato ya existentes (`UsersContractTests`, `AuthContractTests`, `CompaniasContractTests`, `EvaluacionAccesoContractTests`, `OpenApiSnapshotTests`) para reflejar los contratos vigentes de `users.yaml`, `auth.yaml`, `companies.yaml`, `access-evaluation.yaml` y `permissions.yaml` — no implica regenerarlas desde cero — cubriendo RF-074, RF-079, RF-080 y RF-081, incluido el snapshot del documento OpenAPI en `backend/tests/EnterpriseAccessControl.ContractTests/`
- [X] T221 [P] [US1] Pruebas de componente del módulo de usuarios (listado con aislamiento, wizard, asignación, finalizar/renovar, mensajes de error) en `frontend/tests/unit/`
- [X] T222 [P] [US5] Pruebas de componente del wizard de Historia 5 y del árbol de unidad organizativa, manteniendo en verde las existentes de `AsignacionUnidadOrganizativa` y `PersonaHistorialPage` en `frontend/tests/unit/`
- [X] T223 [US1] Prueba end-to-end que ejercita **UX-17 a UX-22** desde la interfaz en `frontend/tests/e2e/`
- [X] T224 [US5] Prueba end-to-end de los Casos A y B desde la interfaz, verificando las relaciones de dominio resultantes con `GET /api/personas/{id}/estado-efectivo` en `frontend/tests/e2e/`

**Checkpoint**: cada regla nueva tiene al menos una prueba que falla si se revierte.

---

## Phase 22: Cierre y validación final

- [X] T225 Ejecutar la regresión completa de las cinco suites (unitarias, contrato, integración, Vitest, Playwright) y confirmar que ninguna prueba del baseline histórico se rompe
- [X] T226 Ejecutar los seis escenarios de validación de `quickstart.md` §7 correspondientes a D1 a D7
- [X] T227 Actualizar en `README.md` el estado de implementación de las reglas nuevas, como consecuencia directa de las tareas anteriores, en `README.md`
- [X] T228 Repetir el gate de cierre de Etapa 1 contra el código corregido y registrar el resultado en `docs/auditorias/`

**Checkpoint**: baseline de Etapa 1 listo para congelar.

---

## Dependencies & Execution Order — bloque T169 a T228

### Orden de fases

- **Phase 14 (modelo/datos)** — sin dependencias dentro del bloque; **bloquea todo lo demás**.
- **Phase 15 (dominio)** — depende de Phase 14.
- **Phase 16 (autorización)** — depende de Phase 14 y de T175.
- **Phase 17 (bootstrap)** — depende de T170 (Phase 14, entidad `AsignacionRolAdministrativo`) y T175 (Phase 15, servicio de asignación de rol); no depende de Phase 16 (autorización/RBAC), porque la rutina de arranque crea el primer usuario y su asignación directamente vía persistencia, sin pasar por el pipeline de autorización HTTP, `CompaniaScopeAuthorizationHandler` ni los claims JWT.
- **Phase 18 (API/contratos)** — depende de Phase 15, 16 y 17.
- **Phase 19 y 20 (frontend)** — dependen de Phase 18; entre sí son independientes y pueden ir en paralelo.
- **Phase 21 (pruebas)** — cada prueba depende de la capacidad que verifica; T220 depende de Phase 18; T223 de Phase 19; T224 de Phase 20.
- **Phase 22 (cierre)** — depende de todo lo anterior.

### Dependencias puntuales

- T171 y T172 dependen de T170; T173 depende de T171.
- T177 depende de T176; T180 depende de T179; T181 depende de T174.
- T183, T184 y T185 dependen de T182; T185 depende además de T175.
- T192 depende de T170 y T175 (además de T190 y T191); no depende de ninguna tarea de Phase 16. T193 depende de T192.
- T194 depende de T185; T195 de T184; T196 de T174, T177 y T181; T197 de T178 y T180.
- T200 a T206 dependen de T199. T207 a T210 pueden avanzar en paralelo una vez disponible Phase 18.
- T214 a T219 dependen de sus capacidades respectivas; T225 depende de todas las de Phase 21.

### Oportunidades de paralelismo

- T169 y T174 en paralelo (archivos distintos).
- T186, T187 y T188 en paralelo entre sí (servicios distintos), una vez hecho T182.
- T195, T196, T197 y T198 en paralelo (controladores y catálogos distintos).
- T205, T206, T209, T210, T211 y T212 en paralelo.
- Todas las pruebas marcadas `[P]` de Phase 21 en paralelo.

---

## Trazabilidad de requisitos transversales (bloque histórico T001 a T168)

Estos requisitos son **transversales**: ninguna tarea los cita por su identificador porque no existe una
tarea que "los implemente" de forma aislada — se satisfacen en la estructura misma del sistema. Su cobertura
era hasta ahora solo inferible, lo que no es aceptable para un criterio de cierre. Esta tabla la hace
rastreable **sin modificar ninguna tarea histórica**: cita tareas ya existentes y ya completadas, y no
atribuye a ninguna una cobertura que no entregue.

| Requisito | Dónde queda cubierto | Tareas existentes |
|---|---|---|
| RF-002 (estados `ACTIVO`/`INACTIVO`/`BLOQUEADO` del usuario) | El campo `Estado` forma parte de la entidad `Usuario`; el login los distingue y la administración permite desbloquear | T032, T028, T039, T042 |
| RF-003 (historial de contraseñas; nunca en texto plano) | Entidad `HistorialContraseña` dedicada y `PasswordHash` en `Usuario`; la autenticación valida contra el hash | T032, T033, T036 |
| RF-028 (campos de negocio obligatorios salvo declaración explícita) | Validadores FluentValidation por cada petición de escritura, más la obligatoriedad declarada en el modelo de datos | T040, T078 |
| RF-029 (manejo de fecha **y** hora) | Todos los instantes se persisten en UTC con precisión de hora y se convierten solo al presentarlos; el reloj empresarial resuelve la zona | T018, T211 |
| RF-034 (toda operación protegida exige autorización) | Política `CompaniaScope` aplicada a los controladores, alimentada por el token; redefinida para RBAC en el bloque siguiente | T014, T037, T183, T184 |
| RF-040 (contrato de API por grupo funcional) | Documento OpenAPI generado y una prueba de contrato por cada grupo de `contracts/` | T024, T026, T027, T043, T044, T063, T071, T080, T081, T117, T126, T220, T235 |

---

## Trazabilidad RF/CS → tareas (bloque T169 a T228)

| Requisito | Tareas |
|---|---|
| RF-074 (catálogo cerrado de roles, entidad, regla fundamental) | T169, T170, T171, T173, T175, T182, T183, T184, T194, T195, T220 |
| RF-075 (vigencia obligatoria, no solapamiento, renovación) | T170, T172, T175, T191, T213 |
| RF-076 (restricciones del Company Administrator) | T185, T201, T203, T214 |
| RF-077 (alcance efectivo, Resource Ownership, 404 en lectura) | T182, T183, T185, T186, T187, T188, T189, T200, T215 |
| RF-078 (bootstrap) | T190, T191, T192, T193, T216 |
| RF-079 (Compañía INACTIVA en la evaluación) | T178, T197, T217, T220 |
| RF-080 (zona horaria por Principal) | T174, T179, T180, T181, T196, T197, T211, T218, T220 |
| RF-081 (dependencias que bloquean el cambio de tipo) | T176, T177, T196, T219, T220 |
| CS-036 | T214 |
| CS-037 | T215 (parcial: solo el recurso `Usuario`); cobertura efectiva en **T237, T238 y T242** |
| CS-038 | T216 |
| CS-039 | T217 |
| CS-040 | T218 |
| CS-041 | T219 |
| CS-021 (árbol en la asignación de UO, ya vigente) | T208, T222, T224 |
| UX-17 a UX-22 (`ux-ui.md` §35) | T199 a T206, T221, T223 |
| D7 / alcance real de T116 | T207 a T211, T222, T224 |

---

## Notas del bloque T169 a T228

- **T001 a T168 no se regeneraron, renumeraron ni modificaron**: este bloque es estrictamente aditivo y
  comienza en T169.
- **Sin tareas para RF-067, RF-068, RF-069 ni CS-032**: diferidos a Etapa 2 por la decisión D8 y anotados
  como tales en `spec.md`. Tampoco se generan tareas para Historia 10 en su acepción de consultas
  transversales; las tareas T156 a T158 del baseline histórico cubren la verificación del interceptor de
  auditoría automática, que es una capacidad distinta y ya completa.
- **T116 no se reabre ni se renumera**: la decisión D7 se materializa en tareas nuevas (T207 a T211) porque el
  alcance funcional que exige —wizard, creación de pertenencia y contexto desde la interfaz, árbol y perfil—
  excede lo que el texto original de T115 y T116 describía.
- **Eliminación de `AlcanceUsuarioCompania` (T173), sin migración de datos**: el proyecto es greenfield — no
  existe información de producción ni de desarrollo que deba preservarse; cualquier dato existente es de
  prueba y descartable. T173 elimina el modelo y su tabla directamente, sin transformar ni conservar filas.
  Los datos administrativos necesarios se regeneran mediante el arranque de RF-078 (T190–T192) y los
  mecanismos de prueba/seed correspondientes.
- **Documentación**: solo se incluyen T193 y T227, ambas consecuencia directa de implementar tareas de este
  bloque. Las correcciones documentales I4 a I8 y las dos posteriores ya se aplicaron y no se repiten.
- **Trazabilidad de CS-037 corregida (2026-09-21)**: la fila de la tabla anterior atribuía CS-037 a T215 sin
  matizar. T215 se ejecutó y su registro histórico **no se modifica**, pero la verificación que entregó
  alcanza únicamente al recurso `Usuario`; los otros seis recursos que CS-037 enumera quedaron cubiertos por
  T237, T238 y T242. La fila se anota para que refleje la cobertura efectiva sin reescribir la tarea ni
  borrar el registro de lo que sí hizo.

---

## Phase 23: D-1 — Renovación de asignación de rol administrativo

**Goal**: Exponer como operación HTTP la renovación que el dominio ya implementa, bajo los mismos controles de
autorización y alcance que crear o finalizar una asignación.

**Independent Test**: un `GLOBAL_ADMINISTRATOR` extiende la vigencia de una asignación vigente y obtiene la
misma asignación con nueva `fechaHoraFin`, sin registro adicional; un `COMPANY_ADMINISTRATOR` no puede
renovar fuera de su alcance ni una asignación `GLOBAL_ADMINISTRATOR`.

> **Nomenclatura**: `D-1`, `D-2` y `D-4` (con guion) son las **desviaciones de implementación** detectadas al
> terminar T169–T228 y auditadas con `/speckit-analyze`; no son las decisiones de negocio `D1`–`D9` (sin
> guion) del bloque anterior. `D-3` se cerró como implementación válida y `D-5` por configuración y
> documentación: ninguna de las dos genera tareas (research.md §34).

- [X] T229 [US1] Sustituir en la renovación de asignación de rol la `ReglaNegocioInvalidaException` (⇒ `400`) de `RENOVACION_NO_POSTERIOR` por `ConflictoEstadoException` (⇒ `409`), unificándola con `HistorialPersonaService` y con el contrato, y renombrar `RenovarAsignacionRolRequest.NuevaFechaHoraFin` a `FechaHoraFin` para que el JSON sea exactamente `fechaHoraFin`; sin alterar la regla de negocio (la nueva fecha sigue debiendo ser estrictamente posterior a la vigente) ni crear una segunda implementación de renovación (RF-075, research.md §34.1) en `backend/src/EnterpriseAccessControl.Application/Auth/AsignacionRolAdministrativoService.cs` y `backend/src/EnterpriseAccessControl.Application/Auth/AuthDtos.cs`
- [X] T230 [US1] Exponer `POST /api/usuarios/{id}/roles/{asignacionId}/renovar` con cuerpo `{ "fechaHoraFin": "<date-time>" }` y respuesta `204`, reutilizando `UsuarioService.RenovarRolAsync` —que ya aplica la autorización de RF-076 y el alcance de RF-077— sin ruta alternativa ni lógica nueva, y retirar el comentario que documenta que la renovación no está expuesta (RF-075, RF-076, RF-077; `contracts/users.yaml` v2.1.0, research.md §34.1) en `backend/src/EnterpriseAccessControl.Api/Controllers/UsuariosController.cs`
- [X] T231 [US1] Implementar el cliente `renovarRol` y el hook `useRenovarRol` contra el endpoint aprobado, invalidando la caché de usuarios igual que el resto de escrituras del módulo (RF-075, research.md §34.1) en `frontend/src/features/users/api.ts` y `frontend/src/features/users/hooks.ts`
- [X] T232 [US1] Incorporar la acción **Renovar** de una asignación de rol en el detalle de usuario, con ingreso de la nueva `fechaHoraFin`, confirmación explícita previa —igual que finalizar, cuya semántica no cambia— y mensajes propios para `400`, `403`, `404` y `409` (UX-20, `ux-ui.md` §35; quickstart.md §8.1) en `frontend/src/features/users/UsuarioDetalle.tsx` y `frontend/src/features/users/mensajesRol.ts`

**Checkpoint**: la renovación deja de existir solo en el dominio y es operable de extremo a extremo.

---

## Phase 24: D-4 — Búsqueda server-side de usuarios

**Goal**: Que buscar un usuario dentro del alcance autorizado cubra todo ese conjunto y no solo la página
cargada, resolviéndolo en el servidor antes de paginar.

**Independent Test**: con más usuarios que `tamañoPagina`, buscar un correo que cae en una página posterior lo
encuentra desde la página 1; un `COMPANY_ADMINISTRATOR` que busca el correo exacto de un usuario ajeno
obtiene el mismo resultado que ante un correo inexistente.

- [X] T233 [US1] Implementar el parámetro opcional `texto` del listado de usuarios: filtro server-side por coincidencia parcial sobre `Correo` con `EF.Functions.Like` (misma convención que `CompaniaService`), compuesto estrictamente en el orden alcance (RF-077) → `estado` → `texto` → `CountAsync` → `OrderBy(correo)` → `Skip`/`Take`, combinando `estado` y `texto` con AND y devolviendo `200` con `items: []` y `total: 0` cuando no hay coincidencias —nunca `404`, reservado a recurso fuera de alcance—, sin introducir requisitos nuevos de ordenamiento ni de rendimiento (RF-077, UX-22; `contracts/users.yaml` v2.1.0, research.md §34.3) en `backend/src/EnterpriseAccessControl.Application/Auth/UsuarioService.cs`, `backend/src/EnterpriseAccessControl.Application/Auth/AuthDtos.cs` y `backend/src/EnterpriseAccessControl.Api/Controllers/UsuariosController.cs`
- [X] T234 [US1] Retirar el filtrado local por correo sobre `consulta.data?.items` y pasar `texto` al hook y al cliente, sin duplicar el filtro en el frontend y conservando sin cambios el comportamiento actual de los filtros de rol, compañía y «solo con asignaciones vigentes» (UX-22, research.md §34.3) en `frontend/src/features/users/UsuariosPage.tsx` y `frontend/src/features/users/api.ts`

**Checkpoint**: la búsqueda de la interfaz y la del contrato describen el mismo conjunto.

---

## Phase 25: Contrato y pruebas del bloque delta

**Purpose**: Demostrar cada capacidad nueva y cerrar la brecha de regresión de D-2, que es exclusivamente de
cobertura: el control de alcance de los cinco servicios auditados ya está implementado y no se modifica.

- [X] T235 [P] [US1] Verificar la API contra `contracts/users.yaml` v2.1.0 —endpoint de renovación con sus cinco respuestas, parámetro `texto` del listado y composición alcance → filtros → total → paginación— actualizando las aserciones de `UsersContractTests` y el snapshot de `OpenApiSnapshotTests`, sin tocar contratos no relacionados (RF-075, RF-077, UX-22) en `backend/tests/EnterpriseAccessControl.ContractTests/`
- [X] T236 [P] [US1] Pruebas de integración de la renovación: caso exitoso (`204`, misma asignación con la nueva `fechaHoraFin` y sin registro adicional), `RENOVACION_NO_POSTERIOR` → `409`, `SOLAPAMIENTO_VIGENCIA` → `409`, `ASIGNACION_ROL_NO_VIGENTE` → `409`, fuera de alcance → `404` y no autorizado → `403`, distinguiendo los tres conflictos por `codigo` y verificando que el cuerpo aceptado usa `fechaHoraFin` (RF-075, RF-076, RF-077; quickstart.md §8.1) en `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/RolesAdministrativosTests.cs`
- [X] T237 [P] [US6] Pruebas de integración de alcance para `UnidadOrganizativaService` y `AreaAccesoService`, ejercitadas a través del stack de aplicación con un actor autenticado —no por invocación directa del servicio—: un `GLOBAL_ADMINISTRATOR` opera sobre recursos de dos Compañías Principales distintas en la misma prueba, y un `COMPANY_ADMINISTRATOR` de la Principal A recibe `404` sobre recursos conocidos y existentes de la Principal B (RF-077, CS-037; research.md §34.2, quickstart.md §8.2) en `backend/tests/EnterpriseAccessControl.IntegrationTests/OrgUnits/` y `backend/tests/EnterpriseAccessControl.IntegrationTests/AreaAccess/`
- [X] T238 [P] [US5] Completar la cobertura de alcance GLOBAL vs COMPANY para la asignación de unidad organizativa, el estado efectivo y la revocación en cascada —esta última a través de `POST /api/personas/{id}/historial-companias/{asignacionId}/finalizar`, porque `RevocacionService` no tiene endpoint propio ni control de alcance propio por diseño—, sin cambiar ninguna regla de negocio salvo que una prueba demuestre una discrepancia real con RF-077 (RF-077, CS-037; research.md §34.2, quickstart.md §8.2) en `backend/tests/EnterpriseAccessControl.IntegrationTests/People/`
- [X] T239 [P] [US1] Pruebas de integración de la búsqueda server-side: hallar por coincidencia parcial de correo a un usuario que no está en la primera página —lo que demuestra que la búsqueda precede a la paginación—, combinación de `texto` con `estado`, búsqueda sin coincidencias → `200` con `total: 0`, y aislamiento: un `COMPANY_ADMINISTRATOR` que busca el correo exacto y conocido de un usuario fuera de su alcance obtiene el mismo resultado que ante un correo inexistente, sin revelar su existencia (RF-077, UX-22; quickstart.md §8.3) en `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/BusquedaUsuariosTests.cs`
- [X] T240 [P] [US1] Pruebas de componente del módulo de usuarios para la renovación de rol y para la búsqueda server-side —parámetro `texto` enviado al cliente y manejo de las respuestas relevantes—, sin duplicar las pruebas ya existentes de listado, wizard y asignación (UX-20, UX-22) en `frontend/tests/unit/UsuariosPage.test.tsx`
- [X] T241 [US1] Prueba end-to-end sobre el flujo real de la aplicación: renovar una asignación con confirmación previa (UX-20) y localizar mediante búsqueda a un usuario que no está en la primera página, confirmando que se encuentra dentro del alcance autorizado sin recorrer la paginación (UX-20, UX-22; quickstart.md §8.1 y §8.3) en `frontend/tests/e2e/administracion-usuarios.spec.ts`

**Checkpoint**: cada capacidad nueva tiene una prueba que falla si se revierte, y CS-037 deja de estar
declarado sin verificar.

---

## Phase 26: Ampliación de CS-037 a los siete recursos declarados (cierre de C2)

**Purpose**: T237 y T238 cubren los **cinco servicios** que la auditoría de D-2 identificó, pero T215
declaró CS-037 sobre **siete tipos de recurso**. Esta fase cierra esa diferencia ampliando la cobertura
—no recortando el requisito— conforme a la decisión explícita sobre el hallazgo C2.

**Independent Test**: para cada uno de los siete recursos, un `COMPANY_ADMINISTRATOR` de la Principal A
recibe `404` sobre un recurso real y de identificador conocido de la Principal B, y un
`GLOBAL_ADMINISTRATOR` alcanza recursos de ambas Principales en la misma prueba.

- [X] T242 [US1] Pruebas de integración de CS-037 para los recursos que T237 y T238 no cubren —**usuarios**, **compañías**, **personas**, **contextos operativos** y **credenciales**—, ejercitadas por HTTP con actor autenticado sobre la superficie de autorización real de cada uno (`GET /api/usuarios/{id}`, `GET /api/companias/{id}`, `GET /api/personas/{id}`, `GET /api/personas/{id}/contextos-operativos` y `GET|POST|DELETE /api/personas/{id}/credenciales[/{id}...]`), verificando en cada caso `404` con identificador conocido y válido, acceso permitido al recurso propio y alcance del `GLOBAL_ADMINISTRATOR` sobre dos Principales distintas; cierra el hallazgo C2 sin reducir el alcance de CS-037 ni modificar T215 (RF-077, CS-037, Principio VII; research.md §34.2) en `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/AislamientoRecursosTests.cs`

**Checkpoint**: CS-037 queda verificado sobre los siete recursos que declara, no sobre cinco.

---

## Dependencies & Execution Order — bloque T229 a T241

### Orden por decisión

- **D-1 (Phase 23)** — cadena estricta: T229 → T230 → T231 → T232. T229 precede a T230 porque el contrato ya
  publicado declara `fechaHoraFin` y `409`; exponer la ruta antes de corregir el DTO y la excepción
  publicaría un endpoint que contradice su propio contrato.
- **D-4 (Phase 24)** — T233 → T234.
- **D-2 (T237, T238)** — **sin dependencias dentro del bloque**: el código que verifican ya existe y no se
  modifica. Puede ser lo primero que se ejecute.
- **D-1 y D-4 son independientes entre sí**: sus únicas coincidencias son de archivo
  (`UsuariosController.cs` en T230 y T233; `api.ts` en T231 y T234), que obligan a secuencia dentro del
  archivo, no dependencia lógica entre decisiones.

### Dependencias puntuales

- T235 depende de T230 **y** de T233: verifica en la misma pasada la ruta de renovación y el parámetro de
  búsqueda.
- T236 depende de T230; T239 depende de T233.
- T240 y T241 dependen de T232 y de T234 (ambas capacidades ya operables desde la interfaz).
- T237 y T238 no dependen de ninguna tarea de este bloque ni de ninguna anterior.

### Oportunidades de paralelismo

- T237 y T238 en paralelo entre sí y con todo lo demás, desde el inicio.
- T235, T236, T239 y T240 en paralelo una vez satisfechas sus dependencias (suites y archivos distintos).
- T241 al final: es el único que ejercita simultáneamente las dos capacidades desde la interfaz.

---

## Trazabilidad RF/CS → tareas (bloque T229 a T241)

| Desviación | Requisito / UX | Artefacto de diseño | Tareas | Evidencia de prueba |
|---|---|---|---|---|
| D-1 (renovación) | RF-075, RF-076, RF-077 | research.md §34.1, `contracts/users.yaml` v2.1.0, data-model.md, quickstart.md §8.1 | T229, T230, T231, T232 | T235, T236, T240, T241 |
| D-2 (alcance GLOBAL vs COMPANY) | RF-077, CS-037 | research.md §34.2, quickstart.md §8.2 | T237, T238 | T237, T238 |
| C2 (CS-037 sobre los siete recursos) | RF-077, CS-037 | research.md §34.2, quickstart.md §8.2 | T242 | T242 |
| D-4 (búsqueda server-side) | RF-077, UX-22 | research.md §34.3, `contracts/users.yaml` v2.1.0, quickstart.md §8.3 | T233, T234 | T235, T239, T240, T241 |

| Requisito | Tareas del bloque |
|---|---|
| RF-075 (renovación como operación expuesta) | T229, T230, T231, T232, T235, T236 |
| RF-076 (autorización de quien renueva) | T230, T236 |
| RF-077 (alcance, búsqueda antes de paginar, `404` sin revelar existencia) | T230, T233, T234, T235, T236, T237, T238, T239 |
| UX-20 (finalizar y renovar con confirmación) | T232, T240, T241 |
| UX-22 (buscar dentro del alcance autorizado) | T233, T234, T235, T239, T240, T241 |
| CS-037 (aislamiento verificado en los siete recursos) | T237, T238, T242 |

---

## Notas del bloque T229 a T241

- **T001 a T228 no se regeneraron, renumeraron, reabrieron ni modificaron**: este bloque es estrictamente
  aditivo y comienza en T229. Las 228 tareas anteriores conservan su estado `[X]` y su redacción original.
- **Por qué tareas nuevas y no ampliación de T194, T199, T204, T220, T221 o T223**: su cierre fue verificado
  por el gate de T228; reabrirlas haría que `[X]` significara dos cosas distintas y volvería falso un
  registro de auditoría ya emitido. Es el mismo criterio ya aplicado a T116 en el bloque anterior. La
  trazabilidad se preserva citando la tarea antecesora aquí, sin modificarla: T230 completa T194; T231
  completa T199; T232 completa T204 y T205; T233 amplía T185 y T194; T234 amplía T200; T235 amplía T220;
  T236 amplía T213 y T214; T237 cubre T186; T238 cubre T187; T239 amplía T215; T240 amplía T221; T241 amplía
  T223.
- **T199 y T204 sí nombraban la renovación** y se cerraron sin ella porque `contracts/users.yaml` v2.0.0 no
  declaraba la ruta y publicarla habría roto las pruebas de contrato —limitación registrada en su momento
  como nota explícita en `UsuariosController.cs`—. Se completaron hasta donde el contrato permitía; el resto
  queda desbloqueado solo ahora, con v2.1.0.
- **D-2 no genera tareas de producción**: el control de alcance de `UnidadOrganizativaService`,
  `AreaAccesoService`, `AsignacionUnidadOrganizativaService`, `EstadoEfectivoService` y `RevocacionService` ya
  está implementado y verificado por lectura de código. T237 y T238 solo cierran la brecha de regresión que
  exige el Principio VII, sin tocar la lógica que verifican.
- **D-5 no genera ninguna tarea**: quedó cerrada por configuración y documentación
  (`docker-compose.yml` sin contraseña por defecto, `README.md` y `quickstart.md` sincronizados), sin
  modificar RF-078, cuyo texto ya era inequívoco.
- **Sin tareas de Etapa 2**: RF-067, RF-068, RF-069, CS-032 e Historia 10 en su acepción de consultas
  transversales siguen diferidos y fuera de este bloque, igual que en el bloque T169 a T228.
- **`OpenApiSnapshotTests` estuvo en rojo entre la publicación del contrato v2.1.0 y la implementación de
  T230**, con `POST /api/usuarios/{id}/roles/{asignacionId}/renovar: no existe en la API`: recorre cada
  operación declarada en `contracts/*.yaml` y exige que exista. Era el estado esperado en ese intervalo —el
  mismo patrón ya vivido con T194 a T198—. **Cerrado**: con T230 implementada, las 246 pruebas de contrato
  pasan, y T235 añadió además la aserción del parámetro `texto`, que el snapshot no compara.
- **T232 no requirió cambios en `mensajesRol.ts`**: T205 ya había añadido los mensajes de
  `RENOVACION_NO_POSTERIOR`, `ASIGNACION_ROL_NO_VIGENTE`, `SOLAPAMIENTO_VIGENCIA`, `ROL_NO_AUTORIZADO` y
  `RECURSO_NO_ENCONTRADO` cuando construyó la tabla de errores de `ux-ui.md` §35. La renovación los reutiliza
  tal cual; no se duplicó ninguno.
- **T242 es la única tarea nueva de esta ampliación** y existe porque T237/T238 cubren los cinco servicios
  que la auditoría de D-2 nombró, mientras que CS-037 declara siete recursos. El hallazgo C2 se cerró
  ampliando la cobertura, no recortando el criterio ni reescribiendo T215.

---

# POST-BASELINE — VF-007

> **Bloque de evolución post-Baseline.** El Baseline de Etapa 1 (T001–T242) está cerrado y **no se
> reabre**. Numerar este bloque a partir de T243 solo continúa la secuencia del archivo, como hicieron los
> bloques delta anteriores; no reinterpreta ni modifica ninguna tarea previa.
>
> **Origen**: hallazgo VF-007 (`docs/functional-validation/post-baseline-validation.md` §9), formalizado como
> cambio de requisito post-Baseline en spec.md (Sesión 2026-09-25): **RF-082**, **CS-042** y **CS-043**.
> Diseño: plan.md ("Plan post-Baseline VF-007", paquetes WP-1 a WP-13), research.md §35, data-model.md
> (undécima revisión), `contracts/people.yaml` y `contracts/permissions.yaml` v1.1.0, quickstart.md §9.
>
> **Decisiones de VF-007** (no son las `D1`–`D9` del cierre de Etapa 1 ni las `D-1`–`D-5`):
> **D1** contención completa (`inicio_hija >= inicio_pertenencia` Y `fin_hija <= fin_pertenencia`, igualdad
> válida), con la pertenencia vigente por fechas en el instante de la operación como referencia. **D2**
> `PermisoAcceso` solo con `Alcance = PERSONA`. **D3** sin cambios en la cascada (RF-061 a RF-065). **D4** se
> valida cuando el registro resultante queda `ACTIVO` y se crea, cambia sus fechas o pasa de `INACTIVO` a
> `ACTIVO`. No se valida al cambiar solo bloques, al desactivar ni al consultar. Errores:
> `400 SIN_PERTENENCIA_VIGENTE` y `409 FUERA_DE_CONTENCION_TEMPORAL`.
>
> **Fuera de alcance de este bloque**: migración o corrección de datos existentes (RF-082 no es retroactiva);
> cualquier cambio en `RevocacionService`, `ReglasRevocacion`, `EvaluadorDeAcceso` o
> `EvaluacionAccesoService`; permisos `UNIDAD_ORGANIZATIVA`/`COMPANIA`; VF-004, VF-008 y VF-009; Etapa 2.
>
> **Estado de partida**: `OpenApiSnapshotTests` está en rojo (2/42) desde `/speckit-plan` porque los
> contratos v1.1.0 declaran respuestas que la API aún no publica: `POST /api/personas/{id}/perfiles`
> 400/409 y `POST`/`PUT /api/permisos` 409. T247 y T248 lo devuelven a verde (research.md §35.4).
>
> **Frontera de alcance (hallazgo C1 de `/speckit-analyze`, research.md §35.6)**: en los permisos PERSONA, el
> alcance histórico del actor sobre la persona (`PersonaService.ExigirAlcanceHistoricoAsync`, `404
> RECURSO_NO_ENCONTRADO`) se evalúa **antes** de consultar su pertenencia, para que
> `SIN_PERTENENCIA_VIGENTE` y `FUERA_DE_CONTENCION_TEMPORAL` no puedan revelar datos de una persona fuera
> de alcance (Principio I). Queda absorbido por T245, T246, T248 y T252, sin tareas nuevas. La fuga
> preexistente de `ValidarSujetoAsync` (existencia de la persona) queda fuera de este bloque
> (post-baseline-validation.md §9.9).

## Phase 27: VF-007 — Backend: contención en perfiles y permisos PERSONA

**Goal**: Aplicar RF-082 en el servidor reutilizando `ContencionTemporalValidator`, sin duplicar su lógica ni
tocar la cascada.

**Independent Test**: dar de alta un perfil o un permiso PERSONA fuera de la pertenencia devuelve `409`, y sin
pertenencia devuelve `400`. Un permiso UO/COMPANIA fuera de la pertenencia sigue devolviendo `201`. Un permiso
PERSONA existente fuera de contención se puede desactivar y cambiar de bloques, pero no reactivar.

- [X] T243 [US8] Crear la función estática pura que decide si una escritura de `PermisoAcceso` requiere contención (research.md §35.2): recibe `AlcancePermiso alcance`, `Estado? estadoPrevio` (`null` = alta), `Estado estadoResultante` y `bool fechasCambian`, y devuelve `true` solo si `alcance == PERSONA` y `estadoResultante == ACTIVO` y (`estadoPrevio is null` o `fechasCambian` o `estadoPrevio == INACTIVO`). Incluir en el mismo archivo la función pura `FechasCambian(inicioAlmacenado, finAlmacenado, inicioRecibido, finRecibido)`, que considera cambio una diferencia ≥ 1 ms en cualquiera de los dos instantes (research.md §35.3, columna `datetime2(3)`). Sin acceso a datos y sin lógica de contención propia (RF-082 D2/D4) en `backend/src/EnterpriseAccessControl.Application/Permissions/ReglaContencionPermiso.cs` (nuevo)
- [X] T244 [P] [US5] En `EstadoEfectivoService`, inyectar `ContencionTemporalValidator` por constructor (ya está registrado como scoped en `DependencyInjection.cs`, que no requiere cambios; verificarlo) y, en `AsignarPerfilAsync`, invocar `ValidarAsync(personaId, inicio, fin, ct)` justo después de `Vigencia.NormalizarRango` y **antes** de verificar que el `TipoPersona` esté activo, reutilizando sus excepciones (`400 SIN_PERTENENCIA_VIGENTE`, `409 FUERA_DE_CONTENCION_TEMPORAL`) y el mapeo existente a `ProblemDetails`, sin códigos nuevos. Actualizar el `<summary>` del método ("sin contención temporal") y el `<remarks>` de la entidad `AsignacionTipoPersona` ("Tampoco está sujeta a la contención de RF-072"), indicando que RF-082 la somete a contención pero no a la cascada. En el `<remarks>` de `ContencionTemporalValidator` sustituir solo el párrafo "`AsignaciónTipoPersona` queda fuera a propósito" por la referencia a RF-082, **sin cambiar ninguna línea de lógica** (RF-082 D1/D3; CS-042) en `backend/src/EnterpriseAccessControl.Application/People/EstadoEfectivoService.cs`, `backend/src/EnterpriseAccessControl.Domain/Entities/AsociacionesPersona.cs` y `backend/src/EnterpriseAccessControl.Application/People/ContencionTemporalValidator.cs`
- [X] T245 [US8] En `PermisoAccesoService`, inyectar por constructor `ContencionTemporalValidator` y `PersonaService` (ambos ya registrados como scoped en `DependencyInjection.cs`; no requiere cambios). En `CrearAsync`, después de `ValidarSujetoAsync`, que no cambia y conserva el `400` actual ante una persona inexistente, y antes de `db.PermisosAcceso.Add`: si `ReglaContencionPermiso` indica contención (alta: `estadoPrevio = null`, `estadoResultante = request.Estado`), **primero** invocar `PersonaService.ExigirAlcanceHistoricoAsync(request.PersonaId!.Value, ct)`, que produce `404 RECURSO_NO_ENCONTRADO` si la persona está fuera del alcance histórico del actor, y **solo después** `ContencionTemporalValidator.ValidarAsync(request.PersonaId!.Value, inicio, fin, ct)`. Así, una persona fuera de alcance nunca llega a producir `400 SIN_PERTENENCIA_VIGENTE` ni `409 FUERA_DE_CONTENCION_TEMPORAL` (Principio I; research.md §35.6, hallazgo C1). Reutilizar ese método sin crear lógica de autorización paralela. Los permisos `UNIDAD_ORGANIZATIVA` y `COMPANIA` no se validan nunca, y un alta PERSONA directamente `INACTIVO` tampoco: en ambos casos no se consulta la pertenencia ni se añade control de alcance (RF-082 D2/D4; CS-042) en `backend/src/EnterpriseAccessControl.Application/Permissions/PermisoAccesoService.cs` — depende de T243
- [X] T246 [US8] En `PermisoAccesoService.ActualizarAsync`, después de cargar el permiso y de la comprobación de alcance inmutable, y **antes de mutar ninguna propiedad**, capturar `estadoPrevio = permiso.Estado` y calcular `fechasCambian` con `ReglaContencionPermiso.FechasCambian` contra los valores almacenados. Si la regla indica contención, aplicar la misma frontera que T245 sobre el `PersonaId` almacenado: **primero** `PersonaService.ExigirAlcanceHistoricoAsync(permiso.PersonaId!.Value, ct)` (`404 RECURSO_NO_ENCONTRADO` si está fuera del alcance histórico del actor) y **solo después** `ValidarAsync(permiso.PersonaId!.Value, inicio, fin, ct)`, de modo que ni `SIN_PERTENENCIA_VIGENTE` ni `FUERA_DE_CONTENCION_TEMPORAL` puedan revelar datos de una persona fuera de alcance (Principio I; research.md §35.6, hallazgo C1). Solo cambiar bloques, desactivar (`ACTIVO` → `INACTIVO`) o actualizar un registro que queda `INACTIVO` no consulta la pertenencia, aunque la vigencia almacenada la exceda (registros anteriores a RF-082). No se modifica ninguna fecha que el cliente no haya cambiado. Actualizar el `<remarks>` de la entidad `PermisoAcceso` ("**No** está sujeto a la contención temporal de RF-072"), indicando RF-082 para alcance PERSONA y que la cascada no cambia (RF-082 D2/D3/D4; CS-043) en `backend/src/EnterpriseAccessControl.Application/Permissions/PermisoAccesoService.cs` y `backend/src/EnterpriseAccessControl.Domain/Entities/PermisoAcceso.cs` — depende de T243 y T245 (mismo archivo)
- [X] T247 [P] [US5] Declarar en `POST /api/personas/{id}/perfiles` las respuestas `400` y `409` con `ProducesResponseType`, junto al `201` existente, para que la API publique lo que declara `contracts/people.yaml` v1.1.0. Sin cambiar la firma ni los DTO (RF-082; research.md §35.4) en `backend/src/EnterpriseAccessControl.Api/Controllers/PersonasController.cs` — depende de T244
- [X] T248 [P] [US8] Declarar `409` con `ProducesResponseType` en `POST /api/permisos` y `PUT /api/permisos/{id}`, y conservar (o declarar si faltara) `404`, que ahora cubre también la persona fuera del alcance histórico del actor en permisos PERSONA (C1). Así la API publica lo que declara `contracts/permissions.yaml` v1.1.0: `400 SIN_PERTENENCIA_VIGENTE`, `404 RECURSO_NO_ENCONTRADO` y `409 FUERA_DE_CONTENCION_TEMPORAL`. Sin códigos nuevos, firmas ni DTO. Ambas acciones declaran hoy `404` por el área o el permiso fuera de alcance; verificar que se mantiene (RF-082; research.md §35.4 y §35.6) en `backend/src/EnterpriseAccessControl.Api/Controllers/PermisosController.cs` — depende de T245 y T246

**Checkpoint**: RF-082 queda aplicado en el servidor. El validador conserva su lógica; la cascada y la evaluación
de acceso no se tocan.

---

## Phase 28: VF-007 — Pruebas de backend

**Purpose**: Demostrar CS-042 y CS-043 con pruebas que fallen si se revierte RF-082, y adaptar las pruebas del
Baseline que verificaban la regla anterior o usaban fechas que la nueva regla rechaza.

- [X] T249 [P] [US8] Prueba unitaria parametrizada (`[Theory]`) de `ReglaContencionPermiso`: todas las combinaciones de alcance (PERSONA/UNIDAD_ORGANIZATIVA/COMPANIA) × estado previo (alta/`ACTIVO`/`INACTIVO`) × estado resultante (`ACTIVO`/`INACTIVO`) × fechas cambiadas (sí/no), con el resultado esperado de D4. Además, `FechasCambian`: igualdad exacta → `false`; diferencia submilisegundo → `false`; diferencia de 1 ms en el inicio o en el fin → `true` (RF-082 D2/D4; research.md §35.2–35.3) en `backend/tests/EnterpriseAccessControl.UnitTests/Permissions/ReglaContencionPermisoTests.cs` (nuevo) — depende de T243
- [X] T250 [US5] Invertir la prueba del Baseline que verificaba la exclusión: renombrar `El_perfil_NO_esta_sujeto_a_contencion_temporal` a un nombre que exprese la nueva regla y cambiar su aserción de `201` a `409` con `codigo = FUERA_DE_CONTENCION_TEMPORAL`, con un comentario que cite RF-082/VF-007 como cambio post-Baseline. **Ninguna otra prueba de este archivo** (contexto, unidad organizativa, credencial — T088) se modifica (RF-082; CS-042 c) en `backend/tests/EnterpriseAccessControl.IntegrationTests/People/ContencionTemporalTests.cs` — depende de T244
- [X] T251 [P] [US5] Pruebas de integración CS-042 para perfiles por HTTP (`EscenarioUs5` con `MontarConPertenenciaFijaAsync` o equivalente de fechas relativas): (a) fin anterior al de la pertenencia → `201`; (b) fin igual → `201`; (c) fin un día posterior → `409 FUERA_DE_CONTENCION_TEMPORAL`; (d) inicio un día anterior → `409`; (e) persona sin ninguna pertenencia → `400 SIN_PERTENENCIA_VIGENTE`; además, pertenencia `ACTIVA` cuya `FechaHoraFin` ya pasó → `400`, y dos perfiles distintos simultáneos dentro de la pertenencia → ambos `201` (RF-011 sin cambios). Se verifica el `codigo` del `ProblemDetails`, no solo el estado HTTP (RF-082; CS-042) en `backend/tests/EnterpriseAccessControl.IntegrationTests/People/ContencionPerfilesTests.cs` (nuevo) — depende de T244
- [X] T252 [P] [US8] Pruebas de integración CS-042 y del cambio de fechas para permisos por HTTP (`EscenarioPermisos`): con alcance PERSONA, casos (a)–(e) del alta con los mismos resultados que T251; alta PERSONA `INACTIVO` fuera de contención → `201`; alta `UNIDAD_ORGANIZATIVA` y alta `COMPANIA` con fechas fuera de la pertenencia de la persona → `201`; `PUT` de un permiso PERSONA vigente que cambia sus fechas a un rango contenido → `200`, a un rango que excede la pertenencia → `409`, y con la persona ya sin pertenencia → `400`; `PUT` de UO/COMPANIA con fechas fuera → `200`. **Aislamiento entre compañías (C1)**: un actor `COMPANY_ADMINISTRATOR` con alcance solo sobre la compañía A, administrador del área, intenta crear un permiso PERSONA `ACTIVO` y actualizar las fechas de uno existente para una persona cuyo único histórico es con la compañía B (sin contexto operativo con A). Resultado esperado en ambos: `404` con `codigo = RECURSO_NO_ENCONTRADO` en el `ProblemDetails`, verificando de forma explícita que **no** se devuelve `400 SIN_PERTENENCIA_VIGENTE` ni `409 FUERA_DE_CONTENCION_TEMPORAL`, tanto con fechas contenidas como con fechas que excederían la pertenencia de B, y que no se persiste ningún cambio (RF-082 D2/D4; CS-042; Principio I, research.md §35.6) en `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/ContencionPermisosPersonaTests.cs` (nuevo) — depende de T245 y T246
- [X] T253 [P] [US8] Pruebas de integración CS-043 sobre registros anteriores a RF-082: sembrar **directamente en BD** (patrón `ConDatosAsync` de `RevalidacionDinamicaTests`, sin pasar por los servicios) un `AsignacionTipoPersona` y un `PermisoAcceso` PERSONA `ACTIVO` cuyas fechas exceden la pertenencia, y un segundo permiso PERSONA `INACTIVO` fuera de contención. Verificar: (a) `GET /perfiles` y `GET /api/permisos/{id}` devuelven las fechas originales intactas; (b) `PUT` que solo cambia bloques, reenviando las fechas almacenadas (también con diferencia submilisegundo) → `200`, fechas intactas; (c) `PUT` a `INACTIVO` → `200`, también tras finalizar la pertenencia de la persona; (d) `PUT` del permiso `INACTIVO` a `ACTIVO` → `409` con pertenencia vigente y `400` sin ella; (e) `PUT` que cambia fechas a un rango aún fuera → `409` y a un rango contenido → `200`; (f) las filas sembradas conservan fechas y `Estado` idénticos tras ejecutar altas de otros perfiles/permisos de la misma persona (la regla no reescribe nada por sí misma) (RF-082 D4, no retroactividad; CS-043) en `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/RegistrosAnterioresRf082Tests.cs` (nuevo) — depende de T244 y T246
- [X] T254 [P] [US5] Pruebas de integración de la interacción con RF-073 y de la frontera de la cascada: (1) renovar la pertenencia (`POST .../historial-companias/{id}/renovar`) no cambia las fechas de perfiles ni permisos PERSONA existentes, y después un perfil nuevo y un permiso PERSONA nuevo con fin igual al nuevo fin → `201` (antes de renovar habría sido `409`); (2) finalizar la pertenencia (`POST .../finalizar`) deja perfiles y permisos PERSONA existentes con el mismo `Estado` y las mismas fechas (RF-061 a RF-065 sin cambios, D3), mientras contexto, unidad organizativa y credencial sí se revocan (RF-073, RF-082 D3; CS-035 aplicado a RF-082) en `backend/tests/EnterpriseAccessControl.IntegrationTests/People/Rf082RenovacionYCascadaTests.cs` (nuevo) — depende de T244 y T245
- [X] T255 [US5] Ajustar las pruebas del Baseline cuyas fechas quedan fuera de la pertenencia del escenario (desde hoy − 1 mes hasta hoy + 1 año), sin cambiar lo que verifican: en `El_mismo_perfil_puede_repetirse_en_periodos_distintos`, el primer periodo debe quedar dentro de la pertenencia; en `Los_perfiles_se_normalizan_a_dias_completos`, sustituir las fechas fijas 2026-09-01/2026-12-31 (que fallarían a partir del 2026-10-01) por fechas relativas contenidas. `Los_perfiles_no_se_revocan_al_cerrar_la_pertenencia` **no se modifica**: protege D3 (RF-082 D3) en `backend/tests/EnterpriseAccessControl.IntegrationTests/People/PerfilesPersonaTests.cs` — depende de T244
- [X] T256 [US8] Ajustar las pruebas del Baseline de permisos PERSONA con fechas fuera de la pertenencia del escenario, sin cambiar lo que verifican: `La_precedencia_no_rescata_un_permiso_de_persona_vencido` (inicio `Instante − 2 meses` → un inicio contenido que conserve el permiso vencido en el instante evaluado) y `Actualizar_cambia_vigencia_estado_y_reemplaza_los_bloques` (`nuevoFin = Instante + 1 año` → un fin contenido y distinto del original) (RF-082) en `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/PrecedenciaPermisosTests.cs` y `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/PermisosTests.cs` — depende de T245 y T246
- [X] T257 [US8] Actualizar la prueba estructural de dependencia del validador: mover `EstadoEfectivoService` y `PermisoAccesoService` de `NoSujetosAContencion` a `DependientesDeLaPertenencia` (o a una lista `SujetosAContencion` que incluya los cinco servicios), renombrar `Las_tres_dependientes_...` y `Perfiles_y_permisos_no_aplican_contencion_...` según RF-082, y conservar sin cambios `El_validador_es_el_unico_punto_que_emite_el_error_de_contencion` (garantiza que la lógica no se duplicó) (RF-072, RF-082 D1) en `backend/tests/EnterpriseAccessControl.IntegrationTests/Persistence/VigenciaObligatoriaYContencionTests.cs` — depende de T244 y T245

**Checkpoint**: CS-042 y CS-043 verificados por HTTP. Las pruebas del Baseline que se adaptan lo hacen solo en
fechas o en la aserción de exclusión, nunca en lo que protegen.

---

## Phase 29: VF-007 — Frontend

**Goal**: Que la interfaz explique los rechazos de RF-082 y oriente con límites de fecha, sin sustituir la regla
del servidor ni bloquear operaciones que RF-082 permite.

**Independent Test**: un alta de perfil o de permiso PERSONA rechazada por contención muestra un mensaje
específico, no genérico. Desactivar un permiso o cambiar solo sus bloques se envía sin ninguna comprobación
de pertenencia en el cliente.

- [X] T258 [US5] Añadir `SIN_PERTENENCIA_VIGENTE` al catálogo de códigos de error del frontend, junto a `FUERA_DE_CONTENCION_TEMPORAL`, que ya existe (RF-082) en `frontend/src/lib/problemDetails.ts`
- [X] T259 [US5] En `PerfilesPersona.tsx`: (1) obtener la pertenencia vigente con `useHistorialCompanias(personaId)` (`frontend/src/features/people/history/hooks.ts`, ya cacheado por React Query en el historial) y aplicar `min`/`max` a los campos **Desde**/**Hasta** a partir de su `fechaHoraInicio`/`fechaHoraFin` (fecha UTC); (2) si no hay pertenencia vigente, mostrar un aviso y deshabilitar **Asignar perfil** como prevención de una operación obviamente inválida (el servidor sigue siendo quien decide); (3) mensajes específicos para `SIN_PERTENENCIA_VIGENTE` y `FUERA_DE_CONTENCION_TEMPORAL`, siguiendo el patrón de `AsignacionUnidadOrganizativa.tsx`. **No** modificar el texto ni el manejo de `SOLAPAMIENTO_VIGENCIA` (VF-008, fuera de alcance) (RF-082; CS-042; research.md §35.5) en `frontend/src/features/people/perfiles/PerfilesPersona.tsx` — depende de T258 y T244
- [X] T260 [US8] En `PermisoFormulario.tsx`: (1) mensajes específicos para `SIN_PERTENENCIA_VIGENTE` (`400`) y `FUERA_DE_CONTENCION_TEMPORAL` (`409`) cuando el alcance es PERSONA, en lugar del `error.message` genérico; (2) límite opcional de fechas: al elegir la persona, consultar su historial de pertenencias y, si hay una vigente, calcular `max` desde el instante UTC de su `fechaHoraFin` convertido a la hora local del control `datetime-local`. Si la consulta falla (p. ej. `404`, porque el usuario administra el área pero no la persona), el formulario sigue funcionando sin límite; (3) **ninguna** validación de contención en el esquema `zod` ni en el envío: desactivar y cambiar solo bloques se envían siempre, y los alcances UO/COMPANIA nunca reciben límite. No cambiar la normalización de fechas de los permisos (VF-004, fuera de alcance) (RF-082 D2/D4; research.md §35.5) en `frontend/src/features/permissions/PermisoFormulario.tsx` — depende de T258 y T245/T246
- [X] T261 [P] [US5] Pruebas de componente de `PerfilesPersona`: límites `min`/`max` derivados de la pertenencia vigente simulada, aviso y botón deshabilitado sin pertenencia vigente, mensaje específico para cada código (`400 SIN_PERTENENCIA_VIGENTE`, `409 FUERA_DE_CONTENCION_TEMPORAL`) y que nunca se muestra un UUID (RF-013) (RF-082) en `frontend/tests/unit/PerfilesPersona.test.tsx` (nuevo) — depende de T259
- [X] T262 [P] [US8] Ampliar las pruebas de `PermisosPage`/`PermisoFormulario`: mensaje específico ante `409 FUERA_DE_CONTENCION_TEMPORAL` y `400 SIN_PERTENENCIA_VIGENTE` en alcance PERSONA; al editar, cambiar solo bloques o pasar a `INACTIVO` envía la petición sin consultar la pertenencia y conservando las fechas originales exactas (`conservarSiNoCambio`); el formulario funciona sin límite cuando la consulta de pertenencia responde `404`; alcance UO sin límite (RF-082 D2/D4) en `frontend/tests/unit/PermisosPage.test.tsx` — depende de T260

**Checkpoint**: la interfaz refleja RF-082 sin convertirse en frontera de seguridad.

---

## Phase 30: VF-007 — Contrato, regresión y cierre

**Purpose**: Verificar que la API vuelve a coincidir con los contratos v1.1.0, que las tres entidades de RF-072
no cambian y que VF-007 queda registrado con evidencia.

- [X] T263 [P] Pruebas de contrato de v1.1.0: en `HistorialPersonaContractTests`, que `POST /api/personas/{id}/perfiles` publique `201`, `400` y `409`; en `PermisosContractTests`, que `POST /api/permisos` y `PUT /api/permisos/{id}` publiquen `409`; y que `OpenApiSnapshotTests` pase en verde sobre todos los contratos (0 fallos; hoy 2/42). Se mantiene `El_perfil_no_expone_campos_de_revocacion_en_cascada` (D3). Sin cambios de esquema (RF-082; research.md §35.4) en `backend/tests/EnterpriseAccessControl.ContractTests/HistorialPersonaContractTests.cs` y `backend/tests/EnterpriseAccessControl.ContractTests/PermisosContractTests.cs` — depende de T247 y T248
- [X] T264 **Regresión explícita de RF-072** para `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y `AsignaciónCredencial`: ejecutar sin modificarlas `ContencionTemporalValidatorTests` (T101), las pruebas de contención de contexto, unidad organizativa y credencial de `ContencionTemporalTests` (T088; solo la prueba del perfil cambia, en T250), `FechasObligatoriasTests` (T089) y las suites de contexto operativo, unidad organizativa, credenciales y revocación. Confirmar con `git diff` contra el commit de partida que no hay cambios en `ContextoOperativoService.cs`, `AsignacionUnidadOrganizativaService.cs`, `CredencialService.cs`, `RevocacionService.cs`, `ReglasRevocacion.cs`, `EvaluadorDeAcceso.cs` y `EvaluacionAccesoService.cs`, y que en `ContencionTemporalValidator.cs` solo cambian comentarios. **Sin migraciones ni cambios de esquema (G1)**: confirmar con `git status` y `git diff` que no hay archivos nuevos ni modificados en `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/` (incluido `AppDbContextModelSnapshot.cs`) ni en `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Configurations/`, porque RF-082 no es retroactiva y no requiere migración ni corrección masiva de datos (RF-072, RF-061 a RF-065, RF-082 D3) en `backend/tests/` (verificación, sin archivos nuevos) — depende de T243–T257
- [X] T265 Ejecutar las cinco suites (unitarias, integración con Testcontainers, contrato, Vitest y Playwright) sin regresión en T001–T242, y reproducir a mano los escenarios 1–6 de quickstart.md §9 registrando el resultado (RF-082; CS-042, CS-043) en `specs/001-control-acceso-empresarial/quickstart.md` §9 (validación) — depende de T258–T264
- [X] T266 Cierre documental de VF-007: en quickstart.md §9 sustituir la nota "Pendiente de implementación / `OpenApiSnapshotTests` en rojo" por el estado implementado y una tabla de cobertura automatizada (patrón de §8), y en `post-baseline-validation.md` §9.1 pasar VF-007 a **FIXED** con referencia a las tareas T243–T265 y a las pruebas que cubren CS-042/CS-043. VALIDATED queda para la validación funcional del usuario. Sin modificar spec.md ni ninguna tarea T001–T242 (RF-082) en `specs/001-control-acceso-empresarial/quickstart.md` y `docs/functional-validation/post-baseline-validation.md` — depende de T265

**Checkpoint**: VF-007 implementado, verificado y registrado. Baseline intacto.

---

## Dependencies & Execution Order — bloque T243 a T266 (POST-BASELINE — VF-007)

### Orden por fase

- **Phase 27 (backend)**: T243 → T245 → T246 (mismo archivo: `PermisoAccesoService.cs`) → T248. T244 → T247.
  La cadena de perfiles (T244, T247) y la de permisos (T243, T245, T246, T248) son independientes y pueden
  avanzar en paralelo.
- **Phase 28 (pruebas backend)**: cada prueba depende solo del servicio que ejercita. T249 solo de T243. Las
  de perfiles (T250, T251, T255) de T244. Las de permisos (T252, T256) de T245/T246. T253, T254 y T257 de
  ambas cadenas.
- **Phase 29 (frontend)**: T258 → {T259, T260} → {T261, T262}. Puede empezar en paralelo con la Phase 28 una
  vez cerrada la Phase 27.
- **Phase 30 (cierre)**: T263 tras T247/T248. T264 tras toda la Phase 28. T265 tras todo lo anterior. T266
  al final.

### Dependencias puntuales

- T245 y T246 no pueden marcarse `[P]` entre sí: comparten `PermisoAccesoService.cs`.
- T250 y T251 están en archivos distintos, pero ambas dependen de T244.
- `OpenApiSnapshotTests` sigue en rojo hasta que T247 **y** T248 estén hechas; T263 lo confirma en verde.

### Oportunidades de paralelismo

- T244 ∥ T243 (cadenas independientes) y T247 ∥ T248 (controladores distintos).
- T249, T251, T252, T253 y T254 en paralelo una vez satisfechas sus dependencias (archivos nuevos y distintos).
- T259 ∥ T260 y T261 ∥ T262 (componentes y suites distintos).

### Estrategia de implementación

1. **Incremento mínimo (MVP)**: perfiles → T244, T247, T250, T251, T255. Resuelve la observación original de
   VF-007 (el perfil ya no puede exceder la pertenencia).
2. **Permisos PERSONA**: T243, T245, T246, T248, T249, T252, T253, T256.
3. **Fronteras y estructura**: T254, T257, T263, T264.
4. **Interfaz**: T258–T262.
5. **Cierre**: T265, T266.

---

## Trazabilidad VF-007 → requisitos → tareas

```text
VF-007 (post-baseline-validation.md §9, decisiones D1–D4)
  -> RF-082 (amplía RF-072 hacia adelante; RF-061..RF-065 y RF-073 sin cambios)
       -> perfil (AsignaciónTipoPersona) ........ T244, T247, T258, T259
       -> permiso PERSONA (D2) + D4 ............. T243, T245, T246, T248, T252, T260
       -> aislamiento de alcance (C1) ........... T245, T246, T248, T252
  -> CS-042 (alta contenida / igual / posterior / inicio anterior / sin pertenencia; UO y COMPANIA fuera)
       -> T250, T251, T252, T261, T262
  -> CS-043 (registros anteriores: consulta, desactivación, solo bloques, reactivación y cambio de fechas
             rechazados fuera de contención, sin reescritura automática)
       -> T249, T253, T262
          [perfiles: desactivar, cambiar fechas y reactivar quedan pendientes de VF-008; ver nota U1]
  -> T243..T266
```

| Requisito / decisión | Diseño | Implementación | Evidencia |
|---|---|---|---|
| RF-082, D1 (contención completa, validador único) | research.md §35.1 | T244, T245, T246 | T251, T252, T257, T264 |
| RF-082, D2 (solo PERSONA) y D4 (operaciones), permiso | research.md §35.2–35.3; permissions.yaml v1.1.0 | T243, T245, T246, T248, T260 | T249, T252, T253, T262 |
| C1: aislamiento de alcance antes de la contención (Principio I) | research.md §35.6; permissions.yaml v1.1.0 (`404`) | T245, T246, T248 | T252 |
| RF-082, D3 (sin cascada) | plan.md, frontera de la cascada | — (no se toca) | T254, T255 (prueba conservada), T263, T264 |
| CS-042 | quickstart.md §9.1–9.2 | T244, T245 | T250, T251, T252, T261, T262 |
| CS-043 | quickstart.md §9.3 | T246 | T249, T253, T262 (perfiles: parcial, pendiente de VF-008) |
| RF-073 (renovación) | research.md §35 | — | T254 |
| RF-072 (tres entidades originales) y ausencia de migraciones | research.md §25 | — | T264 |
| Contratos v1.1.0 / OpenAPI | research.md §35.4 | T247, T248 | T263 |

### Trazabilidad plan.md (WP-1 a WP-13) ↔ tareas

| WP (plan.md) | Trabajo en plan.md | Tareas |
|---|---|---|
| WP-1 | Contención en `AsignarPerfilAsync`; 400/409 en `PersonasController`; comentarios | T244, T247 |
| WP-2 | Predicado D4, `CrearAsync`/`ActualizarAsync` de permisos, 409 en `PermisosController` (+ frontera de alcance C1) | T243, T245, T246, T248 |
| WP-3 | Prueba unitaria del predicado D4 | T249 |
| WP-4 | Integración CS-042, perfiles | T251 |
| WP-5 | Integración CS-042, permisos (+ aislamiento C1) | T252 |
| WP-6 | Integración CS-043 | T253 |
| WP-7 | Integración RF-073 y frontera de cascada | T254 |
| WP-8 | Ajuste de pruebas existentes que verificaban la regla anterior | T250, T255, T256, T257 |
| WP-9 | Contrato y snapshot OpenAPI | T263 |
| WP-10 | Frontend de perfiles y `problemDetails.ts` | T258, T259 |
| WP-11 | Frontend de permisos | T260 |
| WP-12 | Vitest | T261, T262 |
| WP-13 | Cierre: suites, quickstart §9, registro VF-007; regresión y comprobación de `git diff` de la sección "Validación y cierre" | T264, T265, T266 |

Recorrido inverso: T243 → WP-2; T244 → WP-1; T245–T246 → WP-2; T247 → WP-1; T248 → WP-2; T249 → WP-3;
T250 → WP-8; T251 → WP-4; T252 → WP-5; T253 → WP-6; T254 → WP-7; T255–T257 → WP-8; T258–T259 → WP-10;
T260 → WP-11; T261–T262 → WP-12; T263 → WP-9; T264–T266 → WP-13. Ninguna tarea queda sin WP y ningún WP
queda sin tarea.

---

## Notas del bloque T243 a T266 (POST-BASELINE — VF-007)

- **Cobertura parcial de CS-043 para perfiles (hallazgo U1)**: RF-082 y CS-043 (b)(c) describen desactivar,
  cambiar las fechas y reactivar un perfil (`AsignaciónTipoPersona`), pero hoy los perfiles solo exponen
  `POST` y `GET /api/personas/{id}/perfiles`: esas operaciones no existen y pertenecen al hallazgo VF-008.
  VF-007 **no** crea endpoints de perfiles ni amplía T243–T266 para cubrirlas. En perfiles, CS-043 queda
  cubierto parcialmente: consulta del registro anterior sin alteración (T253 a) y no reescritura automática
  (T253 f). Las partes de desactivación, cambio de fechas y reactivación de perfiles se verificarán cuando
  VF-008 implemente esas operaciones, que deberán respetar RF-082 D4. Para permisos PERSONA, CS-043 queda
  cubierto por completo (T249, T253, T262).
- **Desviación documentada en T256 (implementación)**: `PermisosTests.Actualizar_cambia_vigencia_estado_y_reemplaza_los_bloques`
  **no se modificó**. Esa prueba deja el permiso en `INACTIVO`, y según D4 desactivar nunca consulta la
  pertenencia: su `nuevoFin = Instante + 1 año` no rompe con RF-082. Se verificó en verde sin cambios.
  Tocarla solo habría modificado sin necesidad una prueba del Baseline. T256 se limitó a
  `PrecedenciaPermisosTests.La_precedencia_no_rescata_un_permiso_de_persona_vencido`, cuyo permiso sí nace
  `ACTIVO` con un inicio anterior a la pertenencia.
- **Prueba del Baseline ajustada que el plan no había identificado (detectada en T264)**:
  `EvaluacionAccesoTests.Sin_ningun_permiso_aplicable_deniega` creaba un permiso PERSONA `ACTIVO` para
  "otra persona" sembrada sin pertenencia. Con RF-082 eso devuelve, como corresponde,
  `400 SIN_PERTENENCIA_VIGENTE`. Se le dio a esa persona una pertenencia que contiene la vigencia del
  permiso, sin cambiar lo que la prueba verifica (un permiso de otra persona no se aplica). Es el mismo tipo
  de ajuste de precondición que T255/T256, sin tarea nueva.
- **T265, cómo se cumplió**: las cinco suites en verde (unitarias 141, integración 567, contrato 250, Vitest
  172, Playwright 9) sobre la API reconstruida (`docker compose up -d --build --no-deps api`). Después de los
  E2E, el contenedor se devolvió a su configuración original, verificada por hash. Los escenarios de
  quickstart.md §9 se verificaron mediante sus equivalentes automatizados por HTTP contra la API y SQL Server
  reales (tabla de quickstart.md §9), sin reproducirlos a mano en la base de validación del usuario, para
  no escribir en ella datos de prueba. El recorrido manual en la interfaz queda para la validación funcional
  (VALIDATED).
- **Montaje E2E ajustado (detectado en T265)**: `frontend/tests/e2e/soporte/seccion5.ts`, compartido por
  `cs009-quickstart` y `multi-principal-quickstart`, creaba el perfil antes que la pertenencia. Con RF-082
  esa alta devuelve `400 SIN_PERTENENCIA_VIGENTE`. Solo se invirtió el orden (pertenencia → perfil), con
  las mismas fechas. quickstart.md §5/§6 lleva la anotación post-Baseline correspondiente.
- **Correcciones de `/speckit-analyze` absorbidas sin tareas nuevas**: C1 (frontera de alcance) en T245,
  T246, T248 y T252; I1 en la cabecera del bloque; G1 en T264; I3 en la tabla WP ↔ tareas. I2 y T1 son
  correcciones de texto en spec.md, data-model.md y `contracts/permissions.yaml`. No se creó T267.

- **T001 a T242 no se regeneraron, renumeraron, reabrieron ni modificaron.** Este bloque es estrictamente
  aditivo y está marcado como post-Baseline. Ninguna casilla `[X]` anterior cambia de estado.
- **Por qué se cambian pruebas creadas por tareas cerradas** (T250, T255, T256, T257 tocan archivos de T088,
  T090, T165 y de Historia 8): esas pruebas verificaban la regla del Baseline (exclusión de RF-072) o usaban
  fechas que RF-082 ahora rechaza. Se cambia el código de prueba, no la tarea histórica: T088, T090 y T165
  siguen cerradas y describiendo lo que hicieron en su momento. Es el mismo criterio de los bloques delta
  anteriores.
- **Sin tareas de migración, corrección masiva ni reescritura de datos**: RF-082 no es retroactiva (CS-043).
- **Sin tareas sobre la cascada o la evaluación de acceso** (D3): T254 y T264 verifican que no cambian.
- **Sin cambios de DI**: `ContencionTemporalValidator`, `EstadoEfectivoService` y `PermisoAccesoService` ya
  están registrados como scoped. T244 solo verifica que el contenedor resuelve el nuevo parámetro.
- **Sin prueba E2E nueva**: el plan no la exige y las pruebas de integración por HTTP y de componente cubren
  CS-042/CS-043. Los E2E existentes (`multi-principal-quickstart.spec.ts`) crean el perfil con las fechas
  de la pertenencia y el permiso con alcance UO, así que siguen siendo compatibles. T265 lo confirma.

---

# POST-BASELINE — VF-005

> **Bloque de evolución post-Baseline.** Continúa la numeración después de T266 sin reabrir el Baseline
> (T001–T242) ni el bloque de VF-007 (T243–T266).
>
> **Origen**: hallazgo VF-005 (`docs/functional-validation/post-baseline-validation.md` §13). Es un **gap
> de UX** de severidad baja: el buscador de persona del formulario de Nuevo Permiso (alcance PERSONA) no
> orienta sobre qué introducir. No cambia ningún requisito, contrato, API ni comportamiento de búsqueda:
> `GET /api/personas?texto=` sigue buscando por nombres, apellidos o número de documento (RF-035,
> `contracts/people.yaml`). La etiqueta "Buscar persona" se conserva (ux-ui.md §23: labels persistentes, no
> depender de placeholders).

## Phase 31: VF-005 — Placeholder del buscador de persona

**Goal**: Que el campo "Buscar persona" muestre el placeholder aprobado sin alterar la etiqueta ni la
búsqueda.

**Independent Test**: en Nuevo permiso con alcance PERSONA, el campo con etiqueta "Buscar persona" muestra
exactamente "Ingrese su nro. de documento", y escribir en él sigue enviando el texto a `buscarPersonas`.

- [X] T267 [US8] Añadir `placeholder="Ingrese su nro. de documento"` al input `permiso-buscar-persona`, conservando la etiqueta `<label htmlFor="permiso-buscar-persona">Buscar persona</label>` y sin tocar estado, handlers, `usePersonas`, parámetros de búsqueda, endpoint ni lógica de selección (VF-005; UX-07; ux-ui.md §23) en `frontend/src/features/permissions/PermisoFormulario.tsx`
- [X] T268 [US8] Pruebas de componente del buscador: con alcance PERSONA en Nuevo permiso, el campo se localiza por su etiqueta "Buscar persona" y tiene exactamente el placeholder "Ingrese su nro. de documento"; escribir en él llama a `buscarPersonas` con ese `texto` (el criterio no cambia, RF-035); con otro alcance el buscador no aparece. Sin cambios en el mock ni en el contrato de `buscarPersonas` (VF-005) en `frontend/tests/unit/PermisosPage.test.tsx` — depende de T267
- [X] T269 Validar el frontend (tests relevantes, Vitest completo, typecheck y ESLint, separando los problemas preexistentes no relacionados) y cerrar documentalmente VF-005 en `docs/functional-validation/post-baseline-validation.md` (ANALYZING → FIXED → VALIDATED → CLOSED) — depende de T267 y T268

**Checkpoint**: VF-005 cerrado, sin cambios de backend, API, contratos, requisitos ni comportamiento.

### Notas del bloque T267 a T269 (POST-BASELINE — VF-005)

- **T001–T266 no se modificaron, renumeraron ni reabrieron.** El bloque es aditivo.
- **Sin tareas de backend, contrato, integración ni E2E**: el cambio es un atributo de presentación, y el
  snapshot OpenAPI y las pruebas de `PersonaService` no se ven afectados.
- **El placeholder orienta hacia el criterio más preciso (documento), pero no restringe la búsqueda**:
  nombres y apellidos siguen siendo válidos.

---

# POST-BASELINE — VF-010

> **Bloque de evolución post-Baseline.** Continúa la numeración después de T269 sin reabrir el Baseline
> (T001–T242) ni los bloques de VF-007 (T243–T266) y VF-005 (T267–T269).
>
> **Origen**: hallazgo VF-010 (`docs/functional-validation/post-baseline-validation.md` §14). Es un
> **defecto de implementación visual**, de severidad baja y sin impacto funcional. La regla global
> `input, select { width: 100% }` de `index.css` estira los `<input type="radio">` del paso "Rol"
> (`CamposAsignacion`, UX-17 y UX-19) y anula el diseño en línea que el propio componente declara con
> `.opcion-radio` (flex, `align-items: center`, `gap`). La corrección se acota a `.opcion-radio`, sin tocar
> `index.css`, el TSX, la lógica de roles, la autorización ni los contratos.

## Phase 32: VF-010 — Alineación de los radios de rol

**Goal**: Que los radios de rol se muestren con su tamaño nativo y alineados con su texto en una sola línea.

**Independent Test**: en el paso "Rol" del alta de usuario, cada radio mide su ancho nativo (no el de la
etiqueta) y los dos radios comparten la misma posición horizontal.

- [X] T270 [US1] Añadir `.opcion-radio input { width: auto; }` junto a `.opcion-radio`, para anular solo en ese componente el `width: 100%` global, sin modificar `index.css`, el marcado de `CamposAsignacion.tsx`, `rolesAsignables` ni los handlers (VF-010; UX-17, UX-19; ux-ui.md §35 paso 2) en `frontend/src/features/users/users.css`
- [X] T271 [US1] Ampliar el paso UX-17 del E2E con una comprobación de geometría de los radios de rol: el ancho de `boundingBox()` de cada radio es como máximo 24 px y ambos comparten la misma `x`. jsdom no calcula maquetación, así que solo Playwright puede detectar esta regresión visual. Sin cambiar el flujo existente del paso (VF-010; UX-17) en `frontend/tests/e2e/administracion-usuarios.spec.ts` — depende de T270
- [X] T272 Validar (Vitest, typecheck, ESLint separando los problemas preexistentes no relacionados, y el E2E de administración de usuarios con la API restaurada después a su configuración original) y cerrar documentalmente VF-010 en `docs/functional-validation/post-baseline-validation.md` (ANALYZING → FIXED → VALIDATED → CLOSED) — depende de T270 y T271

**Checkpoint**: VF-010 cerrado, sin cambios de backend, API, contratos, requisitos ni comportamiento.

### Notas del bloque T270 a T272 (POST-BASELINE — VF-010)

- **T001–T269 no se modificaron, renumeraron ni reabrieron.** El bloque es aditivo.
- **Por qué no se corrige en `index.css`**: una regla global para radios y casillas cambiaría el aspecto de
  otras pantallas que VF-010 no cubre. La corrección acotada reutiliza el recurso que el proyecto ya usa
  para inputs concretos (`width: auto`).
- **Alcance**: `CamposAsignacion` es compartido por el alta de usuario (UX-17) y la asignación de rol
  (UX-19), así que ambos se corrigen con la misma regla. No se toca ningún otro componente.

---

# POST-BASELINE — VF-011

> **Bloque de evolución post-Baseline.** Continúa la numeración después de T272 sin reabrir el Baseline
> (T001–T242) ni los bloques de VF-007 (T243–T266), VF-005 (T267–T269) y VF-010 (T270–T272).
>
> **Origen**: hallazgo VF-011 (`docs/functional-validation/post-baseline-validation.md` §15). Es un
> **defecto de implementación** de severidad baja con impacto solo de presentación. El paso "Confirmación"
> del alta de usuario muestra el UUID de la compañía (`asignacion.companiaId`), incumpliendo RF-013 ("IDs …
> ocultos en la interfaz") y ux-ui.md §35 paso 5 ("Mostrar: Correo → Rol → Compañía …"). El nombre ya está
> disponible en la consulta `useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })`, que comparte caché con
> el selector del paso "Compañía" y con `UsuariosPage`. No cambia backend, API, contratos, requisitos,
> modelo de datos, autorización ni el cuerpo del alta.

## Phase 33: VF-011 — Nombre de la compañía en la confirmación del alta de usuario

**Goal**: Que la confirmación del alta muestre el nombre de la compañía y nunca su identificador.

**Independent Test**: al crear un administrador de compañía, la pantalla de confirmación muestra el nombre
de la compañía elegida y no su UUID, y el alta sigue enviando el mismo `companiaId`.

- [X] T273 [US1] En la fila "Compañía" del paso "Confirmación", mostrar el nombre resolviendo `asignacion.companiaId` contra `useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })` (mismo filtro que el selector, para compartir caché y no añadir llamadas HTTP), con un respaldo textual que nunca sea el UUID (RF-013). Sin tocar `BorradorAsignacion`, `CamposAsignacion`, la validación ni el cuerpo del `POST` (VF-011; UX-17; ux-ui.md §35 paso 5) en `frontend/src/features/users/CrearUsuarioWizard.tsx`
- [X] T274 [US1] Prueba de componente: un `GLOBAL_ADMINISTRATOR` crea un "Administrador de compañía" eligiendo "Minera Propia"; la confirmación muestra "Minera Propia" y no su UUID, y el alta envía ese mismo `companiaId`. Si la compañía no puede resolverse, se muestra el respaldo y nunca el UUID (VF-011; RF-013) en `frontend/tests/unit/UsuariosPage.test.tsx` — depende de T273
- [X] T275 Validar (pruebas relevantes, Vitest completo, typecheck y ESLint separando los problemas preexistentes no relacionados) y cerrar documentalmente VF-011 en `docs/functional-validation/post-baseline-validation.md` (ANALYZING → FIXED → VALIDATED → CLOSED) — depende de T273 y T274

**Checkpoint**: VF-011 cerrado, sin cambios de backend, API, contratos, requisitos ni comportamiento.

### Notas del bloque T273 a T275 (POST-BASELINE — VF-011)

- **T001–T272 no se modificaron, renumeraron ni reabrieron.** El bloque es aditivo.
- **Por qué no se guarda el nombre en el borrador**: obligaría a cambiar `BorradorAsignacion` y
  `CamposAsignacion`, compartidos con la asignación de rol (UX-19), y duplicaría un dato que la caché ya
  contiene.
- **Sin E2E**: jsdom verifica el texto renderizado, así que una prueba de componente basta.
- **Fuera de alcance**: `UsuarioDetalle.tsx` también muestra `companiaId` sin resolver en sus asignaciones.
  No forma parte del texto de VF-011 y se registra como observación independiente (registro §15), sin
  tareas en este bloque.

---

# POST-BASELINE — VF-011 (extensión)

> **Extensión del mismo hallazgo VF-011, no un hallazgo nuevo (no hay VF-012).** La observación que el bloque
> T273–T275 dejó fuera de alcance (registro §15.8) se incorpora a VF-011 por decisión aprobada: es el mismo
> incumplimiento de RF-013, un UUID de compañía visible en la interfaz. T273–T275 no se modifican.
>
> **Corrección de premisa**: `GET /api/usuarios/{id}/roles` (`AsignacionRolAdministrativoDto`, contrato
> `AsignacionRolAdministrativo`) devuelve solo `companiaId`, no el nombre. El nombre se toma de
> `useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })`, la consulta que `UsuariosPage` (única vía de apertura
> de `UsuarioDetalle`) ya mantiene activa: se sirve desde caché, sin llamada HTTP adicional. No se modifican
> backend ni contratos. Las compañías históricas inactivas o fuera del alcance actual se muestran como
> "Compañía no disponible", sin consulta adicional sin filtro de estado (RF-077).

## Phase 34: VF-011 (extensión) — Nombre de la compañía en el detalle de usuario

**Goal**: Que las pestañas Asignaciones e Histórico del detalle de usuario muestren el nombre de la compañía
y nunca su identificador.

**Independent Test**: en el detalle de un usuario, la tabla de asignaciones y el histórico muestran "Minera
Propia" y no su UUID. Una compañía no resoluble muestra "Compañía no disponible" y el alcance global conserva
su texto.

- [X] T276 [US1] En la pestaña Asignaciones (columna Compañía) y en el Histórico, resolver `asignacion.companiaId` contra `useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })` con el mismo patrón que T273: nombre si se resuelve, "Compañía no disponible" (`.sin-resolver`) si no, nunca el UUID. El alcance global conserva "Todas (alcance global)" y "alcance global". Sin tocar `AsignacionRol`, `useRolesUsuario`, los diálogos de renovar y finalizar, los payloads, el backend ni los contratos (VF-011 extensión; RF-013; UX-18) en `frontend/src/features/users/UsuarioDetalle.tsx`
- [X] T277 [US1] Pruebas de componente: simular `listarCompanias` en `beforeEach` (el componente ahora consulta compañías) y comprobar que la tabla de Asignaciones y el Histórico muestran "Minera Propia" y no su UUID, que una compañía no resoluble muestra "Compañía no disponible" sin UUID y que el alcance global conserva su texto (VF-011 extensión; RF-013) en `frontend/tests/unit/UsuarioDetalle.test.tsx` — depende de T276
- [X] T278 Validar (pruebas relevantes, prueba negativa sin la corrección, Vitest completo, typecheck y ESLint separando los problemas preexistentes no relacionados) y documentar la extensión en `docs/functional-validation/post-baseline-validation.md` §15, devolviendo VF-011 a CLOSED — depende de T276 y T277

**Checkpoint**: VF-011 vuelve a CLOSED con la extensión, sin cambios de backend, API, contratos, requisitos ni comportamiento.

### Notas del bloque T276 a T278 (POST-BASELINE — VF-011, extensión)

- **T001–T275 no se modificaron, renumeraron ni reabrieron.** El bloque es aditivo; la nota "Fuera de
  alcance" del bloque T273–T275 se conserva como registro histórico de ese cierre.
- **Sin cambios en los diálogos de renovar y finalizar**: allí `companiaId` solo decide el texto ("en esa
  compañía" o "con alcance global") y nunca muestra el UUID.

---

# POST-BASELINE — VF-001

> **Bloque de evolución post-Baseline.** Continúa la numeración después de T278 sin reabrir el Baseline
> (T001–T242) ni los bloques de VF-007, VF-005, VF-010 y VF-011 (T243–T278).
>
> **Origen**: hallazgo VF-001 (`docs/functional-validation/post-baseline-validation.md` §16). Es un **defecto
> de implementación frontend** de severidad alta. En "Nueva compañía" y "Editar compañía", "Tipo de
> documento" es un campo de texto donde hay que escribir el UUID del maestro. Eso incumple RF-013 ("IDs …
> ocultos en la interfaz") y el Principio II, y no trata el campo como la referencia al catálogo que declara
> data-model (`TipoDocumentoId → TipoDocumento`). El backend y el contrato (`CompaniaRequest.tipoDocumentoId`,
> UUID) ya funcionan con el ID y no se modifican. Se reutiliza el patrón de `PersonaFormulario`.

## Phase 35: VF-001 — Selector de tipo de documento en el formulario de compañía

**Goal**: Elegir el tipo de documento por su nombre desde el catálogo, sin ver ni escribir su UUID, y seguir
enviando el ID.

**Independent Test**: en Nueva compañía, "Tipo de documento" es un selector con los nombres de los tipos
activos y el alta envía el ID del elegido. En Editar compañía aparece preseleccionado el tipo actual (si está
activo) con su nombre.

- [X] T279 [US2] Sustituir el `<input type="text">` de "Tipo de documento" por un `<select>` alimentado con `useMaestro('tipos-documento', 'ACTIVO')` (opción inicial "Seleccione…", `value={tipo.id}`, texto `{tipo.nombre}`), cambiar la validación a `z.string().min(1, 'Seleccione el tipo de documento.')` y retirar la ayuda "Identificador del maestro de tipos de documento.". En edición se conserva `tipoDocumentoId` como valor del formulario; un tipo inactivo o no resoluble no recibe opción especial y exige elegir uno activo para guardar, como en `PersonaFormulario` (RF-032). Sin cambios de backend, contratos ni payload (VF-001; RF-013; Principio II) en `frontend/src/features/companies/CompaniaFormulario.tsx`
- [X] T280 [US2] Pruebas de componente: simular `listarMaestro`; ajustar las dos pruebas existentes que escribían el UUID para que elijan el tipo por su nombre; añadir que el selector ofrece nombres y no UUIDs, que el alta envía el ID elegido, que la edición preselecciona el tipo actual mostrando su nombre y lo envía sin cambios, y que un tipo actual no disponible exige elegir uno para guardar (VF-001; RF-013) en `frontend/tests/unit/CompaniasPage.test.tsx` — depende de T279
- [X] T281 Validar (pruebas relevantes, prueba negativa sin la corrección, Vitest completo, typecheck y ESLint separando los problemas preexistentes no relacionados) y cerrar documentalmente VF-001 en `docs/functional-validation/post-baseline-validation.md` (ANALYZING → FIXED → VALIDATED → CLOSED) — depende de T279 y T280

**Checkpoint**: VF-001 cerrado, sin cambios de backend, API, contratos, requisitos, modelo de datos ni autorización.

### Notas del bloque T279 a T281 (POST-BASELINE — VF-001)

- **T001–T278 no se modificaron, renumeraron ni reabrieron.** El bloque es aditivo.
- **Fuera de alcance por decisión aprobada**: `CompaniaService` no comprueba que el tipo de documento exista y
  esté activo (a diferencia de `PersonaService`), y `Compania.TipoDocumentoId` no tiene FK en base de datos. No
  se implementa en VF-001 ni se abre un hallazgo nuevo en este bloque.

---

# POST-BASELINE — VF-002

> **Bloque de evolución post-Baseline.** Continúa la numeración después de T281 sin reabrir el Baseline
> (T001–T242) ni los bloques anteriores (T243–T281).
>
> **Origen**: hallazgo VF-002 (`docs/functional-validation/post-baseline-validation.md` §17). Es un **defecto
> de implementación frontend** de severidad alta. La página de Unidades Organizativas ya muestra un árbol (el
> componente compartido `Tree`, patrón ARIA `treeview`) con la jerarquía anidada que entrega
> `GET /api/unidades-organizativas/arbol`. Sin embargo, con el ratón no se puede expandir ni contraer: el clic
> solo selecciona, el indicador `▸`/`▾` es decorativo y la expansión solo funciona con el teclado. Eso incumple
> **RF-036** ("Los árboles DEBEN permitir expandir, contraer y seleccionar nodos") y ux-ui.md §14. Se corrige en
> `Tree.tsx`, sin librerías nuevas ni cambios de backend, contratos, modelo o autorización.

## Phase 36: VF-002 — Expandir y contraer el árbol con el ratón

**Goal**: Que el usuario con ratón pueda expandir y contraer ramas desde el indicador, conservando la
selección con el clic en el nombre y toda la navegación por teclado.

**Independent Test**: en el árbol de unidades organizativas, un clic en `▸` muestra los hijos y un clic en
`▾` los oculta, sin cambiar la selección. Un clic en el nombre selecciona el nodo sin expandirlo.

- [X] T282 Hacer interactivo el indicador de expansión de los nodos con hijos: un clic alterna expandido/contraído (con `stopPropagation`, de modo que no seleccione), y el clic en el nombre sigue solo seleccionando. El indicador conserva `aria-hidden` y queda fuera del orden de tabulación, porque el teclado ya cubre la expansión con las flechas. Las hojas (`•`) no tienen acción. El estado inicial sigue contraído, sin props nuevas ni auto-expansión. Sin cambios en la API pública del componente, en la navegación por teclado ni en la accesibilidad existente (VF-002; RF-036; ux-ui.md §14) en `frontend/src/components/Tree/Tree.tsx`
- [X] T283 Pruebas: en el `Tree`, el clic en el indicador expande y contrae (incluidos varios niveles) sin seleccionar, el clic en el nombre selecciona sin expandir, las hojas no expanden y el estado inicial sigue contraído; en `UnidadesOrganizativasPage`, el hijo de la jerarquía solo se ve tras expandir su raíz con el ratón y la selección habilita las acciones existentes. Las pruebas de teclado existentes no se modifican (VF-002; RF-036) en `frontend/tests/unit/Tree.test.tsx` y `frontend/tests/unit/UnidadesOrganizativasPage.test.tsx` — depende de T282
- [X] T284 Validar (pruebas relevantes, prueba negativa sin la corrección, Vitest completo, typecheck y ESLint separando los problemas preexistentes no relacionados) y cerrar documentalmente VF-002 en `docs/functional-validation/post-baseline-validation.md` (ANALYZING → FIXED → VALIDATED → CLOSED) — depende de T282 y T283

**Checkpoint**: VF-002 cerrado, sin cambios de backend, API, contratos, modelo, autorización ni dependencias.

### Notas del bloque T282 a T284 (POST-BASELINE — VF-002)

- **T001–T281 no se modificaron, renumeraron ni reabrieron.** El bloque es aditivo.
- **Componente compartido**: `Tree` también lo usan Áreas de acceso (`AreasAccesoPage`) y la asignación de unidad
  organizativa (`AsignacionUnidadOrganizativa`), que heredan el cambio. Por decisión aprobada, **VF-003
  permanece como hallazgo independiente** y no se cierra por este bloque.
- **Fuera de alcance por decisión aprobada**: búsqueda y breadcrumb del árbol (ux-ui.md §14). No se implementan
  ni se abre un hallazgo nuevo; quedan como observación pendiente en el registro.

---

# POST-BASELINE — VF-003

> **Bloque de evolución post-Baseline.** Continúa la numeración después de T284 sin reabrir el Baseline
> (T001–T242) ni los bloques anteriores (T243–T284).
>
> **Origen**: hallazgo VF-003 (`docs/functional-validation/post-baseline-validation.md` §18). Es un **defecto
> de implementación frontend** con **la misma causa raíz que VF-002**: el árbol de Áreas de acceso
> (`AreasAccesoPage`, T125) usa el componente compartido `Tree`, cuyo indicador `▸`/`▾` no permitía expandir ni
> contraer con el ratón (RF-036). **La corrección de código ya se hizo en T282** (VF-002). Este bloque **no
> modifica código de producción**: aporta la evidencia específica de Áreas de acceso y cierra VF-003 como
> hallazgo independiente. Sin cambios de `Tree.tsx`, `AreasAccesoPage.tsx`, backend, contratos, modelo ni
> dependencias.

## Phase 37: VF-003 — Evidencia del árbol de Áreas de acceso

**Goal**: Demostrar en la propia pantalla de Áreas de acceso que el árbol se recorre con el ratón y que la
selección de un área sigue gobernando sus acciones y su panel de tipos de persona.

**Independent Test**: en Áreas de acceso, `▸` despliega la raíz y muestra el hijo, `▾` la vuelve a contraer,
se alcanza un segundo nivel, el clic en el nombre selecciona sin expandir, y seleccionar un área hija habilita
crear y mover y muestra el panel de tipos de persona de esa área.

- [X] T285 [US6] Pruebas de página específicas de Áreas de acceso: el árbol empieza contraído; el clic en `▸` expande la raíz y muestra el hijo; el clic en `▾` la contrae; se recorre un segundo nivel; el clic en el nombre selecciona sin expandir; y seleccionar un área hija habilita "Crear bajo la seleccionada" y "Mover" y muestra el panel de tipos de persona de esa área (RF-019). Las pruebas de teclado existentes no se modifican (VF-003; RF-009; RF-036) en `frontend/tests/unit/AreasAccesoPage.test.tsx`
- [X] T286 Validar (pruebas de `AreasAccesoPage`, `Tree` y de Áreas de acceso relacionadas, prueba negativa con el `Tree` anterior a T282, Vitest completo, typecheck y ESLint de los archivos afectados) y cerrar documentalmente VF-003 en `docs/functional-validation/post-baseline-validation.md` (ANALYZING → FIXED → VALIDATED → CLOSED), registrando que la corrección de código fue T282 — depende de T285

**Checkpoint**: VF-003 cerrado con evidencia propia, sin cambios de código de producción.

### Notas del bloque T285 a T286 (POST-BASELINE — VF-003)

- **T001–T284 no se modificaron, renumeraron ni reabrieron.** El bloque es aditivo.
- **Por qué no hay tarea de corrección de código**: la causa raíz (indicador de expansión decorativo en el `Tree`
  compartido) se eliminó en T282. VF-003 se mantuvo independiente por decisión aprobada y se cierra con su propia
  evidencia, no por arrastre de VF-002.
- **Fuera de alcance**: búsqueda y breadcrumb (ux-ui.md §14, observación ya registrada en VF-002 §17.8);
  auto-expansión del primer nivel (descartada); presentación jerárquica del selector "Nueva área superior".

---

# POST-BASELINE — VF-004

> **Bloque de evolución post-Baseline.** Continúa la numeración después de T286 sin reabrir el Baseline
> (T001–T242) ni los bloques anteriores (T243–T286).
>
> **Origen**: hallazgo VF-004 (`docs/functional-validation/post-baseline-validation.md` §19), formalizado como
> cambio de requisito post-Baseline en spec.md (Sesión 2026-09-25 VF-004): **RF-083**, **CS-044** a **CS-047**,
> y matices de Historia 8, RF-021, RF-029, RF-080 y RF-082. Diseño: plan.md ("Plan post-Baseline VF-004",
> paquetes WP-1 a WP-12), research.md §36, data-model.md (duodécima revisión), `contracts/permissions.yaml`
> **v2.0.0**, quickstart.md §10 y ux-ui.md §18.
>
> **Decisiones de VF-004** (F-1 a F-7; no son las series `D1`–`D9`, `D-1`–`D-5` ni D1–D4 de VF-007):
> **F-1** inicio = primer instante válido del día local; fin = primer instante válido del día siguiente − 1 ms;
> se persiste en UTC y la evaluación sigue siendo `inicio <= instante < fin`. **F-2** para `PermisoAcceso`, la
> contención de RF-082 se compara por fecha civil (RF-072 no cambia). **F-3** la petición usa `format: date`;
> contrato incompatible v2.0.0, sin doble versión. **F-4** los permisos existentes con hora no se migran ni se
> reinterpretan. **F-5** solo fechas si la vigencia es de días completos; en otro caso, fecha y hora. **F-6**
> cada extremo se edita por separado (fecha sin cambios → instante conservado). **F-7** cambiar la zona no
> modifica instantes.
>
> **Estado de partida**: desde `/speckit-plan`, `OpenApiSnapshotTests` (2 casos de `permissions.yaml`) y
> `PermisosContractTests` (2 casos) están en rojo, porque el contrato v2.0.0 declara una forma que la API aún no
> publica. T302 los devuelve a verde.
>
> **Fuera de alcance de este bloque** (no se toca): `EvaluadorDeAcceso`, `PermisoAcceso.EstaVigenteEn` (solo
> comentarios), `EvaluacionAccesoService`, `Vigencia` (normalización general al día UTC; registro §19.6),
> `ContencionTemporalValidator.ValidarAsync`, RF-022 y `BloqueHorarioPermiso` (incluido el tramo
> 23:59–24:00), la cascada (`RevocacionService`, `ReglasRevocacion`), RF-072 y D1–D4 de VF-007, migraciones,
> esquema de base de datos, scripts de conversión de datos, y los servicios de pertenencias, contextos, unidades,
> credenciales y perfiles.
>
> **Orden de autorización obligatorio** (Principio I, research.md §35.6 y §36.4): (1) alcance del actor sobre
> el área (`404`); (2) sujeto; (3) alcance histórico del actor sobre la persona (`404`), solo si hay contención;
> (4) contención por fecha civil; (5) escritura. La zona de la Principal, las fechas de contención y la
> pertenencia **nunca** se obtienen de un recurso fuera del alcance del actor: un recurso fuera de alcance sigue
> produciendo el `404` del Baseline.

## Phase 38: VF-004 — Backend: conversión diaria, contención por fecha civil y contrato

**Goal**: Disponer de la conversión fecha civil ↔ instante UTC con tzdb, de la contención por fecha civil y de
los DTO de la v2.0.0, sin tocar la evaluación ni RF-072.

**Independent Test**: las pruebas unitarias de T294–T296 pasan: Lima, Santiago con el día sin 00:00 y el de 25
horas, conservación y normalización por extremo, y contención con igualdad válida en ambos extremos.

- [X] T287 [US8] Crear la clase estática pura `VigenciaDiariaPermiso` (research.md §36.1, §36.3; RF-083 (a), (d), (f); F-1, F-6), sin acceso a datos ni reloj. Operaciones: • `InicioUtc(DateOnly fecha, DateTimeZone zona)`: `zona.AtStartOfDay(LocalDate.FromDateOnly(fecha)).ToInstant().ToDateTimeUtc()`. • `FinUtc(DateOnly fecha, DateTimeZone zona)`: `AtStartOfDay` del día siguiente menos `Duration.FromMilliseconds(1)`. • `FechaCivil(DateTime instanteUtc, DateTimeZone zona)`: fecha local de `Instant.FromDateTimeUtc(...)` en la zona, como `DateOnly`. • `EsDiaCompleto(DateTime inicioUtc, DateTime finUtc, DateTimeZone zona)`: `true` solo si ambos instantes coinciden exactamente con `InicioUtc`/`FinUtc` de sus propias fechas civiles. • `ResolverExtremo(DateTime almacenadoUtc, DateOnly solicitada, DateTimeZone zona, bool esFin)`: devuelve `(DateTime Instante, bool Cambia)`, que conserva el instante almacenado si `FechaCivil(almacenado) == solicitada` y, si no, lo normaliza con `InicioUtc` o `FinUtc`. • `Zona(string zonaEfectiva)`: `DateTimeZoneProviders.Tzdb[zonaEfectiva]`. Si `AtStartOfDay` lanza `SkippedTimeException` (la zona omite el día entero), traducirla a `ReglaNegocioInvalidaException(CodigosError.ValidacionEntrada, "La fecha {fecha} no existe en la zona horaria de la Compañía Principal del área.")`. **Prohibido** calcular límites de día o cambios de horario con aritmética de `DateTime`/`TimeSpan` o con `Vigencia.NormalizarInicio/NormalizarFin` (normalizan el día UTC). Archivo: `backend/src/EnterpriseAccessControl.Application/Permissions/VigenciaDiariaPermiso.cs` (nuevo). Prueba: T294.
- [X] T288 [P] [US8] En `ContencionTemporalValidator`, añadir la variante por fecha civil (research.md §36.4; RF-082 matizado, RF-083 (c); F-2): • `ValidarFechasCivilesAsync(Guid personaId, DateOnly inicio, DateOnly fin, CancellationToken ct)`: obtiene la pertenencia con el `ObtenerPertenenciaVigenteAsync` existente, **sin modificarlo**, y delega en la comprobación pura. • `static ValidarFechasCiviles(AsignacionPersonaCompania pertenencia, DateOnly inicio, DateOnly fin)`: fecha declarada = `DateOnly.FromDateTime(pertenencia.FechaHoraInicio)` y `DateOnly.FromDateTime(pertenencia.FechaHoraFin)`, sus componentes UTC. **Nunca** convierte esos instantes a otra zona. `inicio < inicioDeclarado` → `409 FUERA_DE_CONTENCION_TEMPORAL`; `fin > finDeclarado` → `409`. La igualdad es válida en ambos extremos. Mismos códigos y textos que `Validar`. `ValidarAsync`, `Validar` y `ObtenerPertenenciaVigenteAsync` no cambian una sola línea de lógica: siguen siendo los de RF-072 y perfiles. En el `<remarks>` de la clase, añadir un párrafo sobre la variante de VF-004. Archivo: `backend/src/EnterpriseAccessControl.Application/People/ContencionTemporalValidator.cs`. Prueba: T295.
- [X] T289 [US8] DTOs y validador de la v2.0.0 (research.md §36.5; `contracts/permissions.yaml` v2.0.0; F-3): • `PermisoAccesoRequest`: sustituir `DateTime FechaHoraInicioVigencia`/`FechaHoraFinVigencia` por `DateOnly FechaInicioVigencia`/`FechaFinVigencia`, que se serializan como `fechaInicioVigencia`/`fechaFinVigencia` con `format: date`. • `PermisoAccesoDto`: conservar `DateTime FechaHoraInicioVigencia`/`FechaHoraFinVigencia` (instantes UTC efectivos) y añadir `DateOnly FechaInicioVigencia`, `DateOnly FechaFinVigencia`, `bool VigenciaEnDiasCompletos` y `string ZonaHorariaIana`. • `PermisoAccesoRequestValidator`: `NotEqual(default(DateOnly))` con `WithName("fechaInicioVigencia")`/`WithName("fechaFinVigencia")` y los mensajes de obligatoriedad actuales (RF-021, RF-071). Actualizar el `<remarks>`. Un cuerpo con los campos antiguos `fechaHora*` deja las fechas en `default` y se rechaza con `400`, que nombra los campos nuevos. Sin doble versión ni aceptación de los campos antiguos. Rompe la compilación del servicio y de las pruebas hasta T290–T293 y T297: hacerlo en el mismo tramo. Archivos: `backend/src/EnterpriseAccessControl.Application/Permissions/PermisoAccesoDtos.cs` y `backend/src/EnterpriseAccessControl.Application/Permissions/Validators/PermisosRequestValidators.cs`. Pruebas: T299 y T302.

**Checkpoint**: piezas puras y contrato de C# listos. La evaluación, RF-072 y la cascada siguen sin cambios.

---

## Phase 39: VF-004 — `PermisoAccesoService`: zona, creación, actualización y lecturas

**Goal**: Aplicar RF-083 en el servidor manteniendo el orden de autorización y sin reinterpretar datos
existentes.

**Independent Test**: un alta de 25/09–30/09 en un área de Lima persiste `2026-09-25T05:00:00.000Z` –
`2026-10-01T04:59:59.999Z`. Un permiso sembrado con hora conserva sus instantes al cambiar solo bloques o
estado. Un área fuera de alcance devuelve `404` sin leer la zona.

- [X] T290 [US8] Zona efectiva de la Principal del área, solo dentro del alcance (research.md §36.2; RF-080, RF-083; Principio I): • Inyectar `IRelojEmpresarial` en `PermisoAccesoService` (ya es singleton en `DependencyInjection.cs`; verificar que no requiere cambios). • Cambiar `ExigirAreaEnAlcanceAsync` para que, en la **misma** consulta que hoy confirma el alcance (`AreasAcceso` filtrada por `alcance.EsGlobal || companias.Contains(a.CompaniaPrincipalId)`), proyecte `Compania.ZonaHorariaIana` de la Principal del área y devuelva `VigenciaDiariaPermiso.Zona(reloj.ZonaEfectiva(zona))`. Si no es visible, conserva el `404` actual y no lee ninguna zona. • Añadir `ZonaDelAreaAsync(Guid areaAccesoId)`, que solo se invoca con el `AreaAccesoId` de un permiso ya obtenido por `ObtenerEnAlcanceAsync`. • Añadir `ZonasDeAsync(IReadOnlyCollection<Guid> areaIds)` por lote para las lecturas (patrón de `BloquesDeAsync`), aplicada solo a permisos ya filtrados por `EnAlcance`. Así ninguna zona se obtiene de un recurso fuera de alcance. Archivo: `backend/src/EnterpriseAccessControl.Application/Permissions/PermisoAccesoService.cs`. Depende de T287. Prueba: T300 (área fuera de alcance → `404`).
- [X] T291 [US8] Creación (`CrearAsync`) según RF-083 (research.md §36.1, §36.4; RF-082; F-1, F-2). Orden exacto: (1) `ValidarVigencia` por fechas: pura, antes del alcance como hoy; `FechaFinVigencia < FechaInicioVigencia` → `400 PERIODO_INVALIDO`; la igualdad es válida. (2) `ValidarBloques` (sin cambios). (3) `ExigirAreaEnAlcanceAsync` → zona (T290). (4) `ValidarSujetoAsync` (sin cambios). (5) Conversión con `VigenciaDiariaPermiso.InicioUtc`/`FinUtc`. (6) Si `ReglaContencionPermiso.RequiereContencion(alcance, null, estado, true)`: `ContenerEnPertenenciaAsync(personaId, fechaInicio, fechaFin, inicioUtc, finUtc, ct)`, que llama **primero** a `personas.ExigirAlcanceHistoricoAsync` (`404`) y **después** a `contencion.ValidarFechasCivilesAsync(personaId, fechaInicio, fechaFin, ct)` con las fechas de la petición. El método recibe las fechas civiles de inicio y fin **y** los instantes UTC ya resueltos de esos extremos (paso 5). La implementación normal usa solo las fechas civiles para la contención por día civil (RF-082, F-2). Los instantes forman parte de los datos ya resueltos y se pasan para que la regresión dirigida de T310 (b) pueda sustituir únicamente la llamada por `ValidarAsync(personaId, inicioUtc, finUtc, ct)`. No cambian la regla funcional de VF-004. (7) Escritura. UO y COMPANIA, o un alta directamente `INACTIVO`, no consultan la pertenencia. Archivo: `backend/src/EnterpriseAccessControl.Application/Permissions/PermisoAccesoService.cs`. Depende de T288–T290. Pruebas: T299 y T300.
- [X] T292 [US8] Actualización (`ActualizarAsync`) por extremo (research.md §36.3; RF-083 (d), (g); F-4, F-6; D4 sin cambios). Orden: (1) `ValidarVigencia` y `ValidarBloques`. (2) `ObtenerEnAlcanceAsync` (`404`) y la comprobación de alcance inmutable (sin cambios). (3) Zona por `ZonaDelAreaAsync(permiso.AreaAccesoId)`. (4) `VigenciaDiariaPermiso.ResolverExtremo` para el inicio y para el fin, cada uno contra su instante almacenado. (5) **Validación obligatoria de los instantes resueltos**: si `FinUtc <= InicioUtc` → `400 PERIODO_INVALIDO` (RF-039, data-model.md `PermisoAcceso`). Con un extremo antiguo conservado, dos fechas válidas pueden dar instantes vacíos o invertidos, y no hay CHECK en base de datos que lo impida. Se valida **antes** de la contención y de persistir, sin mutar nada. (6) `fechasCambian = inicio.Cambia || fin.Cambia`. (7) `RequiereContencion(permiso.Alcance, permiso.Estado, request.Estado, fechasCambian)`, sin cambios; si aplica, `ContenerEnPertenenciaAsync(permiso.PersonaId!.Value, fechaInicio, fechaFin, inicioUtc, finUtc, ct)` (alcance histórico → fecha civil), **antes** de mutar. Recibe las fechas civiles de la petición **y** los instantes UTC ya resueltos en el paso 4, ya sean conservados o normalizados. Igual que en T291, la contención usa solo las fechas civiles, y los instantes quedan disponibles para la regresión dirigida de T310 (b), sin cambiar la regla funcional. (8) Asignar **solo** el extremo que cambia; nunca reescribir el que no cambió. (9) Estado y bloques como hoy. Eliminar `ReglaContencionPermiso.FechasCambian` y su `Difieren`, que quedan sin uso (la tolerancia de 1 ms de research.md §35.3 no aplica a fechas civiles), y actualizar el `<remarks>` de `ReglaContencionPermiso`. `RequiereContencion` no cambia. Archivos: `backend/src/EnterpriseAccessControl.Application/Permissions/PermisoAccesoService.cs` y `backend/src/EnterpriseAccessControl.Application/Permissions/ReglaContencionPermiso.cs`. Depende de T291 (mismo archivo). Pruebas: T296 y T301.
- [X] T293 [US8] Lecturas y presentación (research.md §36.5–36.6; RF-083 (f), (h); F-5, F-7): • `AMapa` recibe la zona de cada permiso y rellena `FechaInicioVigencia = FechaCivil(inicio)`, `FechaFinVigencia = FechaCivil(fin)`, `VigenciaEnDiasCompletos = EsDiaCompleto(inicio, fin, zona)` y `ZonaHorariaIana = zona.Id`. • `ListarAsync` usa `ZonasDeAsync` sobre la página ya filtrada; `ObtenerAsync`, `CrearAsync` y `ActualizarAsync` usan la zona ya resuelta. • Nada de esto se persiste: se recalcula en cada lectura, así que un cambio de zona no modifica filas. • Actualizar el `<summary>`/`<remarks>` de `PermisoAccesoService` y el de las propiedades de vigencia de `PermisoAcceso`, indicando que los instantes se calculan desde fechas civiles (RF-083). `EstaVigenteEn` no cambia su lógica. Archivos: `backend/src/EnterpriseAccessControl.Application/Permissions/PermisoAccesoService.cs` y `backend/src/EnterpriseAccessControl.Domain/Entities/PermisoAcceso.cs` (solo comentarios). Depende de T292. Pruebas: T299 y T301.

**Checkpoint**: RF-083 aplicado en el servidor; la API publica la v2.0.0. La evaluación, la cascada y RF-072 no
cambian.

---

## Phase 40: VF-004 — Pruebas de backend (unitarias e integración)

**Purpose**: Pruebas nuevas que fallen si se revierte RF-083, y adaptar las existentes solo donde cambia la
forma de la petición o la expectativa.

- [X] T294 [P] [US8] Pruebas unitarias nuevas de `VigenciaDiariaPermiso` con zonas tzdb reales (research.md §36.1; CS-044, CS-045; F-1, F-6): • **America/Lima**: `2026-09-25` → `2026-09-25T05:00:00.000Z`; fin `2026-09-30` → `2026-10-01T04:59:59.999Z`. • **America/Santiago, día sin 00:00**: `InicioUtc` equivale a las 01:00 locales y el día dura 23 h (`FinUtc − InicioUtc + 1 ms`). • **America/Santiago, día de 25 horas**: el día dura 25 h y el fin del día anterior es `InicioUtc − 1 ms`. Ambas fechas de transición de Santiago se localizan con `zona.GetZoneIntervals(...)` en la propia prueba, con una aserción previa que garantiza la premisa (hueco o repetición). No se codifican offsets a mano. • **Instantes antiguos**: `FechaCivil` de `2026-09-25T08:00Z` y `2026-09-30T17:00Z` en Lima → 25/09 y 30/09. • **`EsDiaCompleto`**: `true` para una vigencia normalizada; `false` para la antigua y para la normalizada en Lima evaluada en Santiago (F-7). • **`ResolverExtremo`**: conserva con la misma fecha y normaliza con otra, para inicio y fin. • **Día omitido**: `Pacific/Apia`, `2011-12-30` → `ReglaNegocioInvalidaException` `VALIDACION_ENTRADA`. Archivo: `backend/tests/EnterpriseAccessControl.UnitTests/Permissions/VigenciaDiariaPermisoTests.cs` (nuevo). Depende de T287.
- [X] T295 [P] [US8] Pruebas unitarias nuevas de `ContencionTemporalValidator.ValidarFechasCiviles` (research.md §36.4; F-2), con una pertenencia construida con `Vigencia.NormalizarRango` del 01/08/2026 al 31/07/2027: • inicio 01/08/2026 y fin 31/07/2027 → válido (igualdad en ambos extremos); • fin 01/08/2027 → `FUERA_DE_CONTENCION_TEMPORAL`; • inicio 31/07/2026 → `FUERA_DE_CONTENCION_TEMPORAL`. Se eliminó el antiguo cuarto caso ("un fin cuyo instante en Lima supera al de la pertenencia"): la función solo recibe fechas civiles y ese caso no distinguía nada, porque era idéntico al primero. La garantía de que dos rangos con la misma fecha civil no se rechazan por la diferencia de instantes UTC queda **explícitamente en T300** (fin = último día en Lima; inicio = primer día en `Asia/Tokyo`), en el punto de llamada del servicio, que es donde T310 (b) introduce la regresión. `ContencionTemporalValidatorTests` (T101) no se modifica. Archivo: `backend/tests/EnterpriseAccessControl.UnitTests/People/ContencionFechaCivilPermisoTests.cs` (nuevo). Depende de T288.
- [X] T296 [P] [US8] Adaptar `ReglaContencionPermisoTests`: eliminar solo los casos de `FechasCambian` (tolerancia de 1 ms), que desaparece en T292, y mantener sin cambios la matriz de `RequiereContencion` (D4). Archivo: `backend/tests/EnterpriseAccessControl.UnitTests/Permissions/ReglaContencionPermisoTests.cs`. Depende de T292.
- [X] T297 [US8] Adaptar la forma de las peticiones de permisos en las pruebas de integración: • `EscenarioPermisos.Peticion` pasa a `DateOnly? inicio`/`DateOnly? fin`. Por defecto, la fecha civil en la zona de `PrincipalA` de `Instante.AddMonths(-1)` y de `Instante.AddMonths(6)`. • Añadir en el escenario los helpers `FechaCivil(DateTime)` (zona de la Principal) y `FechaDeclarada(DateTime)` (componentes UTC de un instante de pertenencia). • Actualizar las llamadas de `ContencionPermisosPersonaTests` y `Rf082RenovacionYCascadaTests` con `FechaDeclarada` de la pertenencia, **nunca** convirtiéndola a Lima. • Actualizar también `PrecedenciaPermisosTests`, `GateCredencialTests`, `RevalidacionDinamicaTests`, `EvaluacionAccesoContratistaTests`, `EvaluacionAccesoTests`, `PermisosTests`, `RegistrosAnterioresRf082Tests` y cualquier otra llamada a `Peticion` o cuerpo JSON de `/api/permisos`, sin cambiar lo que verifica cada prueba. Archivos: `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/*.cs` y `backend/tests/EnterpriseAccessControl.IntegrationTests/People/Rf082RenovacionYCascadaTests.cs`. Depende de T289.
- [X] T298 [US8] Adaptar las tres pruebas cuya **expectativa** cambia con RF-083 (plan.md, Regresión VF-004): • `PermisosTests`: la petición con inicio = fin deja de ser `PERIODO_INVALIDO`. Se sustituye por fin anterior a inicio y se añade el caso de un solo día → `201`. El caso de campo ausente nombra `fechaFinVigencia`. • `EvaluacionAccesoTests`: el permiso "vencido" con fin `Instante − 1 h` cae el mismo día en Lima; se usa como fin la fecha civil del día anterior a `Instante`. • `RegistrosAnterioresRf082Tests`: el caso de tolerancia `AddTicks(±5000)` se sustituye por "reenviar las mismas fechas civiles conserva los instantes"; los demás casos envían fechas civiles. Archivos: `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/PermisosTests.cs`, `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/EvaluacionAccesoTests.cs` y `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/RegistrosAnterioresRf082Tests.cs`. Depende de T293 y T297.
- [X] T299 [P] [US8] Integración nueva de CS-044 y CS-045 por HTTP con SQL Server real (Testcontainers). **Fechas relativas obligatorias**: ninguna fecha absoluta como dependencia temporal del escenario. Todas se derivan de `EscenarioPermisos.Instante` (D = fecha civil en Lima de `Instante`, más desplazamientos en días). 25/09–30/09/2026 queda solo como ejemplo documental de spec.md CS-044. Conversión esperada en `America/Lima` (UTC−5 fijo, sin cambio de horario): fecha de inicio D → `D 05:00:00.000Z`; fecha de fin D → `(D+1) 04:59:59.999Z`. • **CS-044**: • un alta de D a D+5 en un área de Lima → `201`, y la fila persiste exactamente `D 05:00:00.000Z` / `(D+6) 04:59:59.999Z`; • la respuesta trae las fechas civiles D y D+5, `vigenciaEnDiasCompletos = true` y `zonaHorariaIana = America/Lima`; • con bloques que cubren la hora evaluada (p. ej. 00:00–23:59 del día de semana correspondiente), la evaluación el día D+5 a las 23:00 de Lima (`(D+6) 04:00Z`) → `CONCEDIDO` y el D+6 a las 00:30 (`(D+6) 05:30Z`) → `PERMISO_FUERA_DE_VIGENCIA`; dentro de la vigencia y sin bloque → `FUERA_DE_BLOQUE_HORARIO`. Pertenencia, contexto y credencial del escenario cubren esos instantes. • **CS-045**: • permiso de un solo día → `201`, con vigencia de 00:00 a 23:59:59.999 locales; • fin anterior a inicio → `400 PERIODO_INVALIDO`; • **contrato antiguo**: un cuerpo con `fechaHoraInicioVigencia`/`fechaHoraFinVigencia` → `400` cuyos errores nombran `fechaInicioVigencia` y `fechaFinVigencia`; • fecha con hora o mal formada → `400`. • **Cambio de horario de extremo a extremo**: con una Principal del escenario en `America/Santiago`, un alta de alcance `UNIDAD_ORGANIZATIVA` o `COMPANIA` (sin dependencia de la pertenencia) que empieza el próximo día sin 00:00 posterior a `Instante`, localizado con tzdb (`GetZoneIntervals`), persiste en UTC las 01:00 locales de ese día. • **UO y COMPANIA**: con fechas fuera de la pertenencia, `201` sin contención. Archivo: `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/VigenciaDiariaPermisosTests.cs` (nuevo). Depende de T293 y T297.
- [X] T300 [P] [US8] Integración nueva de RF-082 por fecha civil y aislamiento (research.md §36.2, §36.4; F-2; Principio I), con una persona con pertenencia del día D1 al D2 (fechas relativas a `Instante`) y un área de Lima: • fin del permiso = D2 → `201`, aunque su instante UTC (`(D2+1) 04:59:59.999Z`) supere al de la pertenencia (`D2 23:59:59.999Z`); se verifica en la fila; • fin = D2 + 1 → `409 FUERA_DE_CONTENCION_TEMPORAL`; • inicio = D1 → `201`; inicio = D1 − 1 → `409`; • **zona al este de UTC**: con un área de una Principal en `Asia/Tokyo` (UTC+9, sin cambio de horario) y la misma persona (el alta no exige contexto operativo; basta con que el actor administre el área), inicio = D1 → `201`, aunque su instante UTC (`(D1−1) 15:00:00.000Z`) sea anterior al de la pertenencia (`D1 00:00:00.000Z`); se verifica en la fila. Es el caso que distingue fecha civil de instante en el extremo inicial, y lo usa T310 (b); • persona sin pertenencia → `400 SIN_PERTENENCIA_VIGENTE`; • **aislamiento**: un `COMPANY_ADMINISTRATOR` sobre una persona fuera de su alcance histórico → `404 RECURSO_NO_ENCONTRADO` con cualquier fecha, nunca `400`/`409`; • área fuera del alcance del actor → `404`, en alta y en actualización; • en todos los rechazos no se escribe ninguna fila. Archivo: `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/ContencionPermisoFechaCivilTests.cs` (nuevo). Depende de T293 y T297.
- [X] T301 [P] [US8] Integración nueva de permisos históricos, edición por extremo y cambio de zona (CS-046, CS-047; RF-083 (d), (g), (h); F-4, F-6, F-7). **Fechas relativas obligatorias**: todas se derivan de `EscenarioPermisos.Instante`, con Da y Db fechas relativas (p. ej. Da = D − 5 y Db = Da + 5, siendo D la fecha civil en Lima de `Instante`). El escenario **garantiza** que Da − 1, Da, Db y Db + 5 están dentro de la pertenencia vigente de la persona (inicio declarado ≤ Da − 1 y Db + 5 ≤ fin declarado), porque los `PUT` que cambian una fecha de un permiso PERSONA `ACTIVO` validan la contención y deben aceptarse por fecha y no rechazarse por quedar accidentalmente fuera de la pertenencia. Si la pertenencia por defecto no lo cumple, se crea con fechas que lo cumplan antes de sembrar. El ejemplo `2026-09-25T08:00Z – 2026-09-30T17:00Z` de CS-046 queda solo como referencia documental, y se **mantienen los desplazamientos históricos 08:00Z/17:00Z**. Sembrar **directamente en BD**, igual que en `RegistrosAnterioresRf082Tests`, un permiso PERSONA `Da 08:00:00.000Z` – `Db 17:00:00.000Z` en un área de Lima (03:00 y 12:00 locales): • `GET` → instantes idénticos, fechas civiles Da y Db y `vigenciaEnDiasCompletos = false`; • `PUT` que reenvía las mismas fechas y cambia solo bloques → ambos instantes intactos al milisegundo; • `PUT` que cambia solo el estado → intactos, y a `INACTIVO` sin consultar la pertenencia (D4); • `PUT` que cambia solo la fecha de fin a Db + 5 → inicio intacto y fin `(Db+6) 04:59:59.999Z`; • `PUT` que cambia solo la fecha de inicio a Da − 1 (dentro de la pertenencia) → fin intacto e inicio `(Da−1) 05:00:00.000Z`; • **instantes invertidos por edición de un extremo (U1)**: con **`Dx = Da + 2`**, fecha relativa al escenario que el escenario garantiza dentro de la pertenencia vigente (igual que Da − 1, Da, Db y Db + 5), sembrar otro permiso antiguo con fin exactamente `Dx 05:00:00.000Z` (00:00 locales del día Dx, así que su fecha civil de fin es Dx) e inicio anterior (p. ej. `Da 08:00:00.000Z`). Un `PUT` con `fechaInicioVigencia = Dx` y `fechaFinVigencia = Dx` pasa la validación de fechas, conserva el fin y normaliza el inicio a `Dx 05:00:00.000Z`, con lo que `FinUtc <= InicioUtc` → `400 PERIODO_INVALIDO`. El permiso existente no cambia (instantes, estado y bloques idénticos en BD) y la operación no consulta ni modifica la pertenencia, porque la validación ocurre antes de la contención; • **CS-047, cambio de zona**: (1) crear explícitamente por `POST /api/permisos` un permiso nuevo en el área de Lima con fechas relativas dentro de la pertenencia, y comprobar que se normaliza (`vigenciaEnDiasCompletos = true`, límites `05:00:00.000Z`/`04:59:59.999Z`); (2) guardar los instantes de todas las filas de `PermisoAcceso` del escenario (este permiso nuevo y los antiguos sembrados); (3) cambiar la zona de la Principal a `America/Santiago` por la API de compañías; (4) verificar que ninguna fila cambia (instantes idénticos al milisegundo) y que, en la lectura, las fechas civiles y `vigenciaEnDiasCompletos` se recalculan con la zona nueva: el permiso nuevo pasa a `false`, porque `05:00Z` no es el inicio de un día en Santiago, y `zonaHorariaIana = America/Santiago`. Archivo: `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/PermisosHistoricosVigenciaDiariaTests.cs` (nuevo). Depende de T293 y T297.

**Checkpoint**: CS-044 a CS-047, el último día de la pertenencia, el día siguiente rechazado, el aislamiento
`404`, el contrato antiguo `400`, el permiso de un día, el cambio de horario, los permisos históricos y la
edición por extremo quedan cubiertos.

---

## Phase 41: VF-004 — Contrato y OpenAPI

- [X] T302 [US8] Pruebas de contrato de la v2.0.0 y trazabilidad `permissions.yaml` ↔ DTO ↔ validador (research.md §36.5; F-3): • `PermisosContractTests`: • `PermisoAccesoRequest` exige `fechaInicioVigencia` y `fechaFinVigencia` con `format: date` en el documento publicado por la API, y ya no declara `fechaHoraInicioVigencia`/`fechaHoraFinVigencia`; • `PermisoAcceso` declara los instantes `date-time`, las fechas `date`, `vigenciaEnDiasCompletos` y `zonaHorariaIana`; • `fechaHoraFinVigencia` sigue sin ser anulable; • adaptar `La_vigencia_completa_es_obligatoria_para_los_tres_alcances` a los nombres nuevos. • `OpenApiSnapshotTests`: en verde sobre todos los contratos (0 fallos). • Verificar sin cambios que `PermisosController` ya declara `400`, `404` y `409` en `POST`/`PUT`. Archivo: `backend/tests/EnterpriseAccessControl.ContractTests/PermisosContractTests.cs`. Depende de T289 y T293.

---

## Phase 42: VF-004 — Frontend date-only

**Goal**: Formularios y listado de permisos con fechas civiles; permisos históricos con fecha y hora en la zona
de la Principal.

**Independent Test**: en Vitest, el formulario envía `fechaInicioVigencia`/`fechaFinVigencia` como
`AAAA-MM-DD`, el listado muestra solo fechas o fecha y hora según `vigenciaEnDiasCompletos`, y la edición de un
permiso con hora muestra la nota de conservación.

- [X] T303 [P] [US8] Añadir dos helpers sin conversión de zona (research.md §36.7): • `fechaDeclarada(iso: string): string`, que devuelve los 10 primeros caracteres de un instante de vigencia diaria normalizada al día UTC (pertenencias; registro §19.6); • `formatearFecha(fecha: string): string`, que formatea `AAAA-MM-DD` como `dd/mm/aaaa` sin construir un `Date` (evita el desplazamiento de zona). `aValorLocal`, `aIsoUtc`, `formatearFechaHora` y `aValorLocalEnZona` no cambian. Pruebas en `frontend/tests/unit/fechas.test.ts` (nuevo). Archivo: `frontend/src/lib/fechas.ts`.
- [X] T304 [US8] Tipos de la v2.0.0 en `permissions/api.ts`: • `PermisoAccesoRequest` con `fechaInicioVigencia` y `fechaFinVigencia` (`AAAA-MM-DD`), sin los campos `fechaHora*`; • `PermisoAcceso` con `fechaHoraInicioVigencia` y `fechaHoraFinVigencia` (instantes), `fechaInicioVigencia`, `fechaFinVigencia`, `vigenciaEnDiasCompletos` y `zonaHorariaIana`; • comentarios con referencia a `contracts/permissions.yaml` v2.0.0. Archivo: `frontend/src/features/permissions/api.ts`. Depende de T289 (contrato).
- [X] T305 [US8] Formulario de permisos con fechas civiles (research.md §36.7; ux-ui.md §18; RF-083; F-5, F-6): • inputs `type="date"` con las mismas etiquetas "Inicio de vigencia" y "Fin de vigencia"; • zod: ambas obligatorias y fin ≥ inicio (comparación de `AAAA-MM-DD`; igualdad válida), con el mensaje en `fechaFinVigencia`; • en edición, valores iniciales = `edicion.fechaInicioVigencia` y `edicion.fechaFinVigencia`; se envían tal cual y se elimina `conservarSiNoCambio` (el servidor decide); • si `edicion.vigenciaEnDiasCompletos === false`, nota informativa con `formatearFechaHora(fechaHora*, edicion.zonaHorariaIana)` y el aviso de que conservar una fecha conserva su hora; • límites de RF-082: `min`/`max` = `fechaDeclarada(vigente.fechaHoraInicio/Fin)`, y la ayuda muestra la pertenencia con `formatearFecha`; • se mantienen los mensajes de `SIN_PERTENENCIA_VIGENTE` y `FUERA_DE_CONTENCION_TEMPORAL` y el funcionamiento sin límites si la pertenencia no es visible (research.md §35.5). Archivo: `frontend/src/features/permissions/PermisoFormulario.tsx`. Depende de T303 y T304. Prueba: T307.
- [X] T306 [US8] Listado de permisos (research.md §36.6; F-5, F-7; RF-080): • si `vigenciaEnDiasCompletos`, `formatearFecha(fechaInicioVigencia)` – `formatearFecha(fechaFinVigencia)`; • si no, `formatearFechaHora(fechaHoraInicioVigencia, zonaHorariaIana)` – `formatearFechaHora(fechaHoraFinVigencia, zonaHorariaIana)`. Sustituye los `new Date(...).toLocaleString()` actuales, que usan la zona del navegador. Archivo: `frontend/src/features/permissions/PermisosPage.tsx`. Depende de T303 y T304. Prueba: T307.
- [X] T307 [US8] Vitest de permisos. Adaptar los casos existentes a la v2.0.0: valores `AAAA-MM-DD`, petición con los campos nuevos y aserción de que no se envían `fechaHora*`; límites RF-082 por fecha declarada. Añadir casos: • listado de un permiso de días completos → solo fechas; • permiso histórico → fecha y hora en `America/Lima` aunque el navegador tenga otra zona; • edición de un permiso histórico → nota con sus instantes; • edición sin cambios → reenvía las mismas fechas; • permiso de un día → se envía; • fin anterior a inicio → error y sin petición; • `min`/`max` = fechas declaradas de la pertenencia. Archivos: `frontend/tests/unit/PermisosPage.test.tsx` y `frontend/tests/unit/fechas.test.ts`. Depende de T305 y T306.

---

## Phase 43: VF-004 — E2E

- [X] T308 [US8] E2E con la v2.0.0 (research.md §36.10): • en `soporte/tiempo.ts`, `inicio = referencia − 2 días`, y añadir a `Reloj` `fechaInicioPermiso = inicio.slice(0, 10)` y `fechaFinPermiso = fin.slice(0, 10)` (fechas declaradas de la pertenencia del escenario), con un comentario sobre por qué el `inicio` anterior fallaba entre las 22:00 y las 23:59 de Lima; • `soporte/seccion5.ts` y `multi-principal-quickstart.spec.ts` envían `fechaInicioVigencia`/`fechaFinVigencia` en `POST /api/permisos`; • ejecutar Playwright sobre el contenedor `eac-api` reconstruido (`docker compose up -d --build --no-deps api`), comprobar la equivalencia de configuración por hash **sin imprimir secretos** y devolver el contenedor a su configuración original al terminar; • `administracion-usuarios.spec.ts` (vigencia de un rol, no de un permiso) no cambia. Archivos: `frontend/tests/e2e/soporte/tiempo.ts`, `frontend/tests/e2e/soporte/seccion5.ts` y `frontend/tests/e2e/multi-principal-quickstart.spec.ts`. Depende de T293 y T305.

---

## Phase 44: VF-004 — Regresión, datos existentes, documentación y cierre

- [X] T309 **Regresión y datos existentes** (plan.md, Regresión y Datos existentes VF-004; F-4; RF-072; D1–D4): • con `git diff` contra el commit de partida, confirmar que no cambian: • `EvaluadorDeAcceso.cs`, `EvaluacionAccesoService.cs`, `BloqueHorarioPermiso.cs`, `Vigencia.cs`, `RevocacionService.cs`, `ReglasRevocacion.cs` y los servicios de pertenencia, contexto, unidad, credencial y perfiles; • la lógica de `PermisoAcceso.EstaVigenteEn` (solo comentarios); • `ContencionTemporalValidator.ValidarAsync`, `Validar` y `ObtenerPertenenciaVigenteAsync`; • con `git status` y `git diff`, confirmar que no hay archivos nuevos ni modificados en `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/` (incluido `AppDbContextModelSnapshot.cs`) ni en `Persistence/Configurations/`, y que no existe ningún script de conversión de datos; • ejecutar sin modificarlas las suites de RF-072 (`ContencionTemporalValidatorTests`, `ContencionTemporalTests`), perfiles, evaluación (`EvaluadorDeAccesoTests`), revocación y bloques; • confirmar con la evidencia de T301 que los permisos históricos conservan sus instantes, que editar bloques o estado no altera la vigencia y que editar un extremo no modifica el otro. Verificación sin archivos nuevos, en `backend/`. Depende de T287–T302.
- [X] T310 **Regresión dirigida (prueba negativa) sobre la implementación ya hecha**. **No** se restaura la versión completa de `PermisoAccesoService.cs` anterior a VF-004: no compila con los DTO `DateOnly` de T289, así que daría un error de compilación en vez de una demostración. En su lugar se introduce **temporalmente**, de **una en una**, cada regresión conceptual de RF-083 y se comprueba que las pruebas la detectan. Antes de empezar, guardar en el scratchpad copias y hashes de `VigenciaDiariaPermiso.cs`, `ContencionTemporalValidator.cs` y `PermisoAccesoService.cs`. (a) **Día UTC en vez del día local**: `InicioUtc`/`FinUtc` calculados con `Vigencia.NormalizarInicio`/`Vigencia.NormalizarFin` → deben fallar los casos de Lima y Santiago de T294 y la persistencia exacta de CS-044 en T299; restaurar. (b) **Contención por instantes en el punto de llamada**: la regresión se introduce en `PermisoAccesoService.ContenerEnPertenenciaAsync`, **no** dentro de `ValidarFechasCiviles`, que solo recibe fechas civiles y no puede comparar instantes. La implementación correcta llama a `contencion.ValidarFechasCivilesAsync(personaId, fechaInicio, fechaFin, ct)`; durante la regresión se sustituye temporalmente esa llamada por `contencion.ValidarAsync(personaId, inicioUtc, finUtc, ct)` con los instantes ya convertidos en la zona efectiva del área correspondiente (Lima o Tokio según el caso: cada caso usa la zona de su propia área). Esos instantes ya llegan a `ContenerEnPertenenciaAsync` como parámetros (T291, T292), así que la regresión cambia **solo esa llamada**, sin tocar la firma del método. Deben fallar los dos casos de T300 que distinguen la comparación por fecha civil de la comparación por instante: "fin = último día de la pertenencia → `201`" en el área de **Lima** (UTC−5: el fin del permiso, `(D2+1) 04:59:59.999Z`, supera al de la pertenencia, `D2 23:59:59.999Z`, y la regresión devuelve `409`), e "inicio = primer día de la pertenencia → `201`" en el área de **`Asia/Tokyo`** (UTC+9: el inicio del permiso, `(D1−1) 15:00:00.000Z`, es anterior al de la pertenencia, `D1 00:00:00.000Z`, y la regresión devuelve `409`). En Lima el caso del inicio no distingue ambas lógicas, porque `D1 05:00Z` no es anterior a `D1 00:00Z`. Por eso se usa una zona al este de UTC. Restaurar. (c) **Sin conservación por extremo**: `ResolverExtremo` normaliza siempre → deben fallar los casos de conservación de T294 y los de T301 (solo bloques o estado, cambiar un solo extremo); restaurar. (d) **Restauración exacta**: verificar por hash y con `git diff` frente a las copias que `VigenciaDiariaPermiso.cs`, `ContencionTemporalValidator.cs` y **`PermisoAccesoService.cs`** son idénticos a la implementación correcta, sin rastro de ningún cambio temporal, y volver a ejecutar en verde las suites afectadas (unitarias de T294–T296 e integración de T299–T301). Registrar qué pruebas falló cada alteración: demuestra que las pruebas detectan **individualmente** las tres regresiones. Sin archivos nuevos en el repositorio. Depende de T309.
- [X] T311 Ejecutar las suites completas: unitarias, integración con Testcontainers, contrato (incluido `OpenApiSnapshotTests` en verde), Vitest, typecheck, ESLint de los archivos afectados (informando aparte los problemas previos no relacionados) y Playwright (T308). Registrar los totales, sin regresión en T001–T286. Depende de T310.
- [X] T312 **Cierre documental de VF-004**: • en quickstart.md §10, sustituir la presentación de "escenarios pendientes" por el estado implementado y una tabla de cobertura automatizada (patrón de §9); • verificar que research.md §36, data-model.md, `contracts/permissions.yaml` v2.0.0, ux-ui.md §18 y spec.md RF-083 coinciden con lo implementado, registrando cualquier desviación como nota de este bloque; • en `post-baseline-validation.md` §19.1, pasar VF-004 a **FIXED** (T287–T311) y a **VALIDATED** con la evidencia técnica de T309–T311; • pasar a **CLOSED** solo con la validación funcional del usuario en la interfaz; si aún no existe, dejar VF-004 en VALIDATED y registrarlo; • confirmar que el diff de tasks.md se limita a este bloque: T001–T286 sin cambios (líneas 1–2065 idénticas byte a byte a las de partida) y ninguna tarea posterior a T312; • confirmar que no hay cambios fuera del alcance aprobado. Archivos: `specs/001-control-acceso-empresarial/quickstart.md`, `docs/functional-validation/post-baseline-validation.md` y `specs/001-control-acceso-empresarial/tasks.md` (solo casillas de este bloque). Depende de T311.

**Checkpoint**: VF-004 implementado, verificado y registrado. Baseline y bloques anteriores intactos.

---

## Dependencies & Execution Order — bloque T287 a T312 (POST-BASELINE — VF-004)

### Orden por fase

- **Phase 38**: T287 ∥ T288 (archivos distintos). T289 rompe la compilación hasta T293 y T297: hacerlo en el
  mismo tramo que la Phase 39.
- **Phase 39**: T290 → T291 → T292 → T293. Todas comparten `PermisoAccesoService.cs`, así que ninguna es `[P]`
  entre sí.
- **Phase 40**:
  - T294 tras T287; T295 tras T288; T296 tras T292;
  - T297 tras T289 y T298 tras T297 y T293;
  - T299, T300 y T301 (archivos nuevos y distintos) tras T293 y T297, en paralelo entre sí.
- **Phase 41**: T302 tras T289 y T293.
- **Phase 42**: T303 ∥ T304; T305 y T306 después de ambas; T307 al final. Puede avanzar en paralelo con la
  Phase 40 porque el contrato ya está fijado.
- **Phase 43**: T308 tras T293 y T305.
- **Phase 44**: T309 → T310 → T311 → T312.

### Oportunidades de paralelismo

- T287 ∥ T288; T294 ∥ T295 ∥ T296; T299 ∥ T300 ∥ T301; T303 ∥ T304.
- Frontend (Phase 42) ∥ pruebas de backend (Phase 40), una vez terminada la Phase 39.

### Estrategia de implementación

1. **Núcleo** (MVP técnico): T287, T288, T294, T295. Conversión con tzdb y contención por fecha civil,
   verificables de forma aislada.
2. **Servidor v2.0.0**: T289–T293, T296–T298 y T302. La API publica el contrato nuevo y el snapshot vuelve a
   verde.
3. **Evidencia de negocio**: T299–T301 (CS-044 a CS-047, RF-082 por fecha civil, aislamiento, históricos).
4. **Interfaz**: T303–T307.
5. **E2E y cierre**: T308–T312.

---

## Trazabilidad VF-004 → requisitos → tareas

```text
VF-004 (post-baseline-validation.md §19; decisiones F-1..F-7)
  -> RF-083 (nuevo) + matices de Historia 8, RF-021, RF-029, RF-080, RF-082
       -> (a) conversión F-1, cambio de horario ...... T287, T290, T291 | T294, T299
       -> (b) evaluación sin cambios ............... T309 | T299
       -> (c) contención por fecha civil F-2 ....... T288, T291, T292, T305 | T295, T300, T307
       -> (d) edición por extremo F-6 .............. T287, T292 | T294, T301
       -> (e) contrato v2.0.0 F-3 .................. T289, T304, T308 | T299, T302
       -> (f) presentación F-5 ..................... T293, T305, T306 | T299, T301, T307
       -> (g) datos existentes F-4 ................. T292, T309 | T301
       -> (h) cambio de zona F-7 ................... T290, T293 | T294, T301
  -> CS-044 -> T299 | CS-045 -> T299 | CS-046 -> T301 | CS-047 -> T301
  -> T287..T312
```

| Clarify | Plan (WP) | Diseño | Tareas | Evidencia |
|---|---|---|---|---|
| F-1 | WP-1, WP-4 | research.md §36.1–36.2 | T287, T290, T291 | T294, T299 |
| F-2 | WP-2, WP-4, WP-9 | research.md §36.4 | T288, T291, T292, T305 | T295, T300, T307 |
| F-3 | WP-3, WP-8, WP-9, WP-11 | research.md §36.5; `permissions.yaml` v2.0.0 | T289, T304, T308 | T299, T302 |
| F-4 | WP-4, WP-6 | research.md §36.8 | T292, T309 | T301, T309 |
| F-5 | WP-4, WP-9, WP-10 | research.md §36.6–36.7; ux-ui.md §18 | T293, T305, T306 | T301, T307 |
| F-6 | WP-1, WP-4 | research.md §36.3 | T287, T292 | T294, T296, T301 |
| F-7 | WP-4, WP-6 | research.md §36.2, §36.8 | T290, T293 | T294, T301 |
| Aislamiento (Principio I) | WP-4, WP-6 | research.md §36.2, §36.4 | T290, T291, T292 | T300 |
| Regresión y cierre | WP-7, WP-12 | plan.md, Regresión VF-004 | T297, T298, T309–T312 | T309–T311 |

Trazabilidad del contrato: `contracts/permissions.yaml` v2.0.0 → `PermisoAccesoDtos.cs` y
`PermisosRequestValidators.cs` (T289) → `PermisoAccesoService.AMapa` (T293) → `frontend/src/features/permissions/api.ts`
(T304) → `PermisosContractTests` y `OpenApiSnapshotTests` (T302) → helpers E2E (T308).

## Notas del bloque T287 a T312 (POST-BASELINE — VF-004)

- **T001–T286 no se modificaron, renumeraron ni reabrieron.** El bloque es estrictamente aditivo.
- **Por qué se cambian pruebas creadas por tareas cerradas** (T297, T298 tocan archivos de Historia 8 y de
  VF-007): cambia la forma de la petición (v2.0.0) o una expectativa que RF-083 modifica (inicio = fin ya es
  válido; un fin una hora antes cae el mismo día). Se cambia el código de prueba, no la tarea histórica.
- **Sin migraciones, scripts de conversión ni reinterpretación de datos** (F-4): T301 y T309 lo verifican.
- **Sin cambios en la evaluación, la cascada, `Vigencia`, RF-022 ni RF-072**: T309 lo verifica con `git diff`.
- **Sin cambios de DI**: `IRelojEmpresarial`, `ContencionTemporalValidator` y `PersonaService` ya están
  registrados. T290 solo verifica que el contenedor resuelve el nuevo parámetro.
- **`PermisosController` no cambia**: ya declara `400`, `404` y `409`. T302 lo verifica.
- **Código de una tarea cerrada que se retira (I2)**: T292 retira `ReglaContencionPermiso.FechasCambian`
  (introducido por T243); T243 permanece cerrada y no se modifica. Igual que con las pruebas, se cambia el
  código, no la tarea histórica.
- **Correcciones de `/speckit-analyze` absorbidas sin tareas nuevas**: F1 en T310 (regresión dirigida en lugar
  de restaurar el servicio anterior, incompatible con los DTO `DateOnly`); U1 en T292 (`FinUtc > InicioUtc`
  tras resolver los extremos → `400 PERIODO_INVALIDO`) y en T301 (caso de instantes invertidos); U2 en T299 y
  T301 (fechas relativas a `Instante`); I1 en spec.md RF-083 (a); I2 en esta nota; I3 en research.md §35.3 y
  §35.5. Numeración T287–T312 sin cambios.
- **Correcciones del segundo `/speckit-analyze`, sin tareas nuevas**: U3 en T310 (b), cuya regresión pasa al
  punto de llamada de `PermisoAccesoService.ContenerEnPertenenciaAsync` (`ValidarAsync` por instantes en lugar
  de `ValidarFechasCivilesAsync`), con `PermisoAccesoService.cs` incluido en las copias, los hashes y la
  restauración; en T295, que elimina el cuarto caso no discriminante; y en T300, que añade el caso de inicio en
  `Asia/Tokyo`, necesario porque en Lima el extremo inicial no distingue fecha civil de instante. U4 en T301
  (Da − 1, Da, Db y Db + 5 dentro de la pertenencia; `POST` explícito del permiso normalizado para CS-047). I4
  en plan.md (WP-4 y "Validación y cierre"). I5 en quickstart.md §10.
- **Correcciones del tercer `/speckit-analyze`, sin tareas nuevas**: L1 en T310 (b), donde los instantes son los
  de la zona efectiva del área de cada caso (Lima o Tokio); L2 en T291 y T292, donde `ContenerEnPertenenciaAsync`
  recibe las fechas civiles y los instantes UTC ya resueltos, usa solo las fechas y deja los instantes para que
  T310 (b) cambie únicamente la llamada; L3 en T301, con `Dx = Da + 2` dentro de la pertenencia.

### Notas de implementación del bloque T287 a T312 (T312)

- **Evidencia final**: unitarias 155/155, integración 593/593, contrato 252/252 (incluido
  `OpenApiSnapshotTests`), Vitest 203/203, Playwright 9/9. Typecheck (`tsconfig.app.json` y
  `tsconfig.node.json`) y ESLint de los archivos de VF-004 sin problemas. El detalle y la regresión dirigida
  están en `post-baseline-validation.md` §19.1.
- **Datos de montaje en T299 y T300**: el usuario del escenario solo administra la Contratista y las Principales
  A y B. Para los casos de `America/Santiago` (T299) y `Asia/Tokyo` (T300), la zona de la Principal B se cambia
  directamente en la base de datos como dato de montaje, sin crear una Principal fuera de su alcance. El cambio de
  zona que **se prueba** (CS-047, T301) sí pasa por la API de compañías, como exige la tarea.
- **T310, ejecución inválida descartada**: la primera medición de la alteración (a) usó `--no-build` después de
  compilar solo el proyecto de integración, así que las pruebas unitarias corrieron con el binario correcto y
  pasaron. Se repitió compilando, y los resultados registrados en §19.1 son los de esa segunda ejecución.
- **T311 y Playwright**: los E2E se ejecutaron en T308 sobre el código definitivo. Después no cambió ningún
  archivo de `backend/src` ni de `frontend`, como verifican los hashes de T310. El contenedor `eac-api` quedó
  reconstruido con el código de VF-004, se devolvió a su configuración original (hash del entorno igual al
  inicial, `Database=EnterpriseAccessControl`) y se dejó parado, junto con `eac-sqlserver`, como estaba antes.
- **Formato**: `frontend/src/lib/fechas.ts` ya no cumplía Prettier antes de VF-004 (diferencia en todo el
  archivo por los finales de línea); se dejó así para no mezclar un reformateo completo con el cambio.
  `PermisoFormulario.tsx` y `PermisosPage.test.tsx` se formatearon, y solo cambiaron líneas nuevas.
- **ESLint preexistente, ajeno a VF-004**: 1 error y 4 advertencias en `PertenenciaContextoWizard.tsx` y
  `CamposAsignacion.tsx`, los mismos que en los hallazgos anteriores.
- **Contrato (T302)**: el modelo `ContratoOpenApi` de las pruebas no expone `format`. Por eso la trazabilidad
  compara propiedades y obligatorios contra `permissions.yaml`, y los formatos (`date`, `date-time`,
  `boolean`, `string`) contra el documento OpenAPI que publica la API.
- **Sin desviaciones funcionales**: la implementación coincide con spec.md RF-083, research.md §36, data-model.md
  (duodécima revisión), `contracts/permissions.yaml` v2.0.0 y ux-ui.md §18. No hizo falta modificar la
  especificación, el plan ni la definición de las tareas durante la implementación.
