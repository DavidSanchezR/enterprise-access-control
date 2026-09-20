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

- [ ] T169 [P] [US1] Crear enum `RolAdministrativo` con exactamente dos valores (`GLOBAL_ADMINISTRATOR`, `COMPANY_ADMINISTRATOR`) como enum cerrado del dominio, con value conversion a `nvarchar` (research.md §17, RF-074) en `backend/src/EnterpriseAccessControl.Domain/Enums/RolAdministrativo.cs`
- [ ] T170 [US1] Crear entidad `AsignacionRolAdministrativo` (`UsuarioId`, `Rol`, `CompaniaId` nullable, `FechaHoraInicio`, `FechaHoraFin` **NOT NULL**, `RowVersion`, auditoría heredada) y su `IEntityTypeConfiguration` (RF-074, RF-075) en `backend/src/EnterpriseAccessControl.Domain/Entities/AsignacionRolAdministrativo.cs`
- [ ] T171 [US1] Migración EF Core que crea la tabla `AsignacionRolAdministrativo`, el `CHECK` de la regla fundamental (`Rol = GLOBAL_ADMINISTRATOR` ⇒ `CompaniaId IS NULL`; `Rol = COMPANY_ADMINISTRATOR` ⇒ `CompaniaId IS NOT NULL`) y el índice clúster sobre `CreatedAt` conforme research.md §20 (RF-074) en `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/`
- [ ] T172 [US1] Agregar en la misma migración el **trigger SQL Server de no-solapamiento** particionado por `(UsuarioId, CompaniaId)` y filtrado a filas `Rol = COMPANY_ADMINISTRATOR`, siguiendo la plantilla de research.md §5; `GLOBAL_ADMINISTRATOR` queda exento (RF-075) en `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/`
- [ ] T173 [US1] Eliminar la entidad `AlcanceUsuarioCompania`, su `DbSet`, su configuración y su tabla mediante una migración de esquema — sin transformar ni conservar sus filas: el proyecto es greenfield, sin datos de producción ni de desarrollo que deban preservarse. Queda reemplazada por `AsignacionRolAdministrativo` (RF-074) en `backend/src/EnterpriseAccessControl.Domain/Entities/AlcanceUsuarioCompania.cs` y `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/`
- [ ] T174 [P] [US2] Agregar el campo `ZonaHorariaIana` (string, identificador IANA) a la entidad `Compania`, con migración y valor obligatorio cuando `TipoCompania = PRINCIPAL_MANDANTE` (RF-080) en `backend/src/EnterpriseAccessControl.Domain/Entities/Compania.cs` y `backend/src/EnterpriseAccessControl.Infrastructure/Persistence/Migrations/`

**Checkpoint**: esquema listo — el modelo RBAC y la zona horaria por Principal existen en base de datos.

---

## Phase 15: Dominio y reglas de negocio

**Purpose**: Reglas que no dependen de la capa de autorización ni de la API.

- [ ] T175 [US1] Implementar `AsignacionRolAdministrativoService` con las operaciones crear, listar, finalizar y renovar: valida la regla fundamental de `CompaniaId`, exige `FechaHoraInicio`/`FechaHoraFin` reales, rechaza solapamientos y aplica las reglas de renovación de RF-073 (solo hacia adelante y solo mientras siga vigente dinámicamente) — RF-074, RF-075 — en `backend/src/EnterpriseAccessControl.Application/Auth/AsignacionRolAdministrativoService.cs`
- [ ] T176 [P] [US2] Implementar `DependenciasTipoCompaniaValidator` que cuenta las cinco categorías de dependencia incompatible (áreas de acceso, raíces de unidad organizativa, `RelacionContratistaPrincipal` vigentes en **ambas** direcciones, contextos operativos, credenciales) sin inspeccionar `AsignacionPersonaCompania` ni personas (RF-081) en `backend/src/EnterpriseAccessControl.Application/Companies/DependenciasTipoCompaniaValidator.cs`
- [ ] T177 [US2] Integrar ese validador en `CompaniaService.ActualizarAsync` para rechazar el cambio de `TipoCompania` con `409 CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS` y detalle accionable, sin cascada ni modificación automática de dependientes (RF-081, RF-033) en `backend/src/EnterpriseAccessControl.Application/Companies/CompaniaService.cs`
- [ ] T178 [US8] Ampliar `EvaluadorDeAcceso` con el **paso 5** (Compañía Principal propietaria del área con `Estado = ACTIVO`) y con la verificación de `Estado = ACTIVO` de la compañía de pertenencia vigente dentro del **paso 6**, denegando con `COMPANIA_INACTIVA` sin escribir nada y de forma reversible (RF-079, research.md §7 y §30) en `backend/src/EnterpriseAccessControl.Application/Permissions/`
- [ ] T179 [US8] Rediseñar `IRelojEmpresarial` para recibir la Compañía Principal cuya zona debe resolverse, y `RelojEmpresarial` para obtener `Compania.ZonaHorariaIana` con respaldo en `ZonaHoraria:TimeZoneId`; `IRelojSistema` (`UtcNow`) no cambia (RF-080, research.md §31) en `backend/src/EnterpriseAccessControl.Domain/Common/IRelojEmpresarial.cs` y `backend/src/EnterpriseAccessControl.Infrastructure/Security/RelojEmpresarial.cs`
- [ ] T180 [US8] Actualizar el **paso 13** de `EvaluadorDeAcceso` para evaluar día de semana y bloque horario en la zona de la Principal determinada en el paso 4, y exponer esa zona en `evaluadoEnZonaHoraria` (RF-080) en `backend/src/EnterpriseAccessControl.Application/Permissions/`
- [ ] T181 [US2] Validar el identificador IANA de `ZonaHorariaIana` **por request** al crear o actualizar una compañía (hoy solo se valida una vez al arrancar), rechazando zonas no reconocidas (RF-080) en `backend/src/EnterpriseAccessControl.Application/Companies/`

**Checkpoint**: reglas de dominio completas — evaluables por prueba unitaria sin API ni autorización.

---

## Phase 16: Autorización, RBAC y Resource Ownership

**Purpose**: Cierra F-01 y F-02, el defecto crítico que impedía congelar el baseline.

- [ ] T182 [US1] Redefinir `IAlcanceCompaniaAccessor`/`AlcanceCompaniaAccessor` para resolver el alcance efectivo desde las asignaciones vigentes: `GLOBAL_ADMINISTRATOR` ⇒ `EstaEnAlcance` verdadero para toda compañía sin enumerarlas; en caso contrario, membresía contra las compañías de sus asignaciones `COMPANY_ADMINISTRATOR` vigentes (RF-074, RF-077) en `backend/src/EnterpriseAccessControl.Infrastructure/Security/AlcanceCompaniaAccessor.cs`
- [ ] T183 [US1] Redefinir `CompaniaScopeAuthorizationHandler` para exigir al menos una asignación de rol vigente en vez de `CompaniaIds.Count > 0`, que hoy denegaría a un `GLOBAL_ADMINISTRATOR` sin compañías enumeradas (RF-074, RF-077) en `backend/src/EnterpriseAccessControl.Infrastructure/Security/CompaniaScopeAuthorizationHandler.cs`
- [ ] T184 [US1] Emitir en el JWT un claim de rol y, solo para `COMPANY_ADMINISTRATOR`, un claim por compañía asignada, reemplazando el esquema actual de "un claim por compañía" que no puede representar el alcance GLOBAL (RF-074) en `backend/src/EnterpriseAccessControl.Infrastructure/Security/JwtTokenService.cs` y `ClaimsPersonalizados.cs`
- [ ] T185 [US1] Reescribir las siete operaciones de `UsuarioService` aplicando Resource Ownership y las restricciones de `COMPANY_ADMINISTRATOR`: filtrar listado y detalle por alcance, restringir creación al rol/compañía que el solicitante puede asignar, y reemplazar `ObtenerAlcanceAsync`/`ReemplazarAlcanceAsync` por operaciones sobre asignaciones individuales (RF-076, RF-077) en `backend/src/EnterpriseAccessControl.Application/Auth/UsuarioService.cs`
- [ ] T186 [P] [US6] Aplicar Resource Ownership en `UnidadOrganizativaService` y `AreaAccesoService`, hoy sin ningún control de alcance pese a RF-049 (RF-077) en `backend/src/EnterpriseAccessControl.Application/OrgUnits/UnidadOrganizativaService.cs` y `backend/src/EnterpriseAccessControl.Application/AreaAccess/AreaAccesoService.cs`
- [ ] T187 [P] [US5] Aplicar Resource Ownership en `AsignacionUnidadOrganizativaService`, `EstadoEfectivoService` y `RevocacionService`, hoy sin ningún control de alcance (RF-077, corrige F-02) en `backend/src/EnterpriseAccessControl.Application/People/`
- [ ] T188 [US4] Implementar la regla de alcance de `Persona` por **unión** —compañía de pertenencia vigente **o** cualquier contexto operativo vigente con una Principal del alcance— en `PersonaService` y `HistorialPersonaService` (RF-077) en `backend/src/EnterpriseAccessControl.Application/People/`
- [ ] T189 [US1] Auditar y completar la verificación de recurso concreto en los servicios que ya referencian el alcance parcialmente (`CompaniaService`, `CredencialService`, `ContextoOperativoService`, `RelacionContratistaPrincipalService`), asegurando lectura fuera de alcance ⇒ `404` sin revelar existencia (RF-077) en `backend/src/EnterpriseAccessControl.Application/`

**Checkpoint**: F-01 y F-02 cerrados — ningún usuario puede operar fuera de su alcance.

---

## Phase 17: Bootstrap del primer administrador

**Purpose**: Un despliegue desde cero queda operable sin intervención manual en base de datos.

- [ ] T190 [P] [US1] Crear `BootstrapOptions` (`Bootstrap:AdminEmail` obligatorio y con formato de correo válido; `Bootstrap:AdminPassword` obligatorio) enlazado por Options Pattern con `ValidateOnStart()`, admitiendo `Bootstrap__AdminPassword` como variable de entorno y **sin valor por defecto versionado** (RF-078) en `backend/src/EnterpriseAccessControl.Application/Common/Options/BootstrapOptions.cs`
- [ ] T191 [US1] Definir la constante `MAX_VALIDITY_DATE` (`2999-12-31T23:59:59Z`) usada **exclusivamente** por la asignación de bootstrap, sin exponerla como opción de configuración ni reutilizarla en ninguna otra asignación (RF-075, RF-078) en `backend/src/EnterpriseAccessControl.Domain/Common/`
- [ ] T192 [US1] Implementar la rutina de arranque **idempotente** que, después de aplicar migraciones, crea el `Usuario` y su `AsignacionRolAdministrativo` `GLOBAL_ADMINISTRATOR` solo si no existe ninguna, validando la contraseña contra `PasswordPolicyValidator`, fijando `RequiereCambioPassword = true` y `FechaHoraFin = MAX_VALIDITY_DATE`, con `CreatedById`/`UpdatedById` nulos (creado por el sistema) — RF-078 — en `backend/src/EnterpriseAccessControl.Api/Program.cs` o un `IHostedService` dedicado
- [ ] T193 [US1] Documentar las dos variables de bootstrap en el despliegue de producción como consecuencia directa de T190/T192, sin valor de respaldo committeado (RF-078) en `README.md` y `docker-compose.prod.yml`

**Checkpoint**: una base recién migrada arranca con un administrador operable y contraseña por cambiar.

---

## Phase 18: API y contratos

**Purpose**: Alinear el código con los contratos ya actualizados documentalmente.

- [ ] T194 [US1] Reescribir `UsuariosController` conforme a `contracts/users.yaml` v2: crear usuario con rol y vigencia, `GET/POST /api/usuarios/{id}/roles`, `POST /api/usuarios/{id}/roles/{asignacionId}/finalizar`, y retirar `alcance-companias` (RF-074 a RF-077) en `backend/src/EnterpriseAccessControl.Api/Controllers/UsuariosController.cs`
- [ ] T195 [P] [US1] Actualizar el flujo de login para devolver `rol` y `companiaIds` en lugar de `alcanceCompanias`, conforme a `contracts/auth.yaml` (RF-074) en `backend/src/EnterpriseAccessControl.Application/Auth/AutenticacionService.cs` y `backend/src/EnterpriseAccessControl.Api/Controllers/AuthController.cs`
- [ ] T196 [P] [US2] Exponer `zonaHorariaIana` en crear y actualizar compañía, y el `409` de dependencias incompatibles, conforme a `contracts/companies.yaml` (RF-080, RF-081) en `backend/src/EnterpriseAccessControl.Api/Controllers/CompaniasController.cs`
- [ ] T197 [P] [US8] Incorporar el motivo `COMPANIA_INACTIVA` al enum de respuesta y poblar `evaluadoEnZonaHoraria` con la zona de la Principal, conforme a `contracts/access-evaluation.yaml` (RF-079, RF-080) en `backend/src/EnterpriseAccessControl.Application/Permissions/EvaluacionAccesoDtos.cs`
- [ ] T198 [P] Registrar los códigos de negocio nuevos (`CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS` y los de asignación de rol) en el catálogo de códigos de error, y declararlos en los contratos donde falten (RF-033, cierra F-11 para los códigos nuevos) en `backend/src/EnterpriseAccessControl.Application/Common/Errores/CodigosError.cs`

**Checkpoint**: el documento OpenAPI publicado vuelve a coincidir con `contracts/`.

---

## Phase 19: Frontend — Administración de usuarios (G7)

**Goal**: Operar RF-074 a RF-077 desde la interfaz, conforme a `ux-ui.md` §35.

**Independent Test**: un `COMPANY_ADMINISTRATOR` solo ve y opera usuarios de su compañía; un `GLOBAL_ADMINISTRATOR` opera sobre todas.

- [ ] T199 [US1] Crear el cliente HTTP y los hooks de usuarios y asignaciones de rol (listar, crear, asignar, finalizar, renovar) en `frontend/src/features/users/api.ts` y `frontend/src/features/users/hooks.ts`
- [ ] T200 [US1] Implementar el listado con columnas, filtros y aislamiento de alcance —sin revelar usuarios ajenos en resultados, totales ni paginación— (UX-22, `ux-ui.md` §35) en `frontend/src/features/users/UsuariosPage.tsx`
- [ ] T201 [US1] Implementar el wizard de creación de usuario en cinco pasos (identidad, rol, compañía condicional, vigencia obligatoria, confirmación), ocultando el rol que el operador no puede asignar (UX-17) en `frontend/src/features/users/CrearUsuarioWizard.tsx`
- [ ] T202 [US1] Implementar el detalle de usuario con tabs Resumen, Asignaciones e Histórico, distinguiendo estado de la asignación y vigencia efectiva por fechas (UX-18, `ux-ui.md` §16) en `frontend/src/features/users/UsuarioDetalle.tsx`
- [ ] T203 [US1] Implementar la asignación de rol a un usuario existente, dejando explícito que agrega y no reemplaza asignaciones (UX-19) en `frontend/src/features/users/AsignarRolDialogo.tsx`
- [ ] T204 [US1] Implementar finalizar y renovar una asignación, con confirmación para ambas y sin permitir editar rol ni compañía de una asignación existente (UX-20) en `frontend/src/features/users/`
- [ ] T205 [P] [US1] Implementar los mensajes de error específicos de la tabla de `ux-ui.md` §35 (rol o compañía no permitidos, alcance insuficiente, correo duplicado, solapamiento, vigencia inválida, contraseña fuera de política), sin revelar datos fuera de alcance (UX-21) en `frontend/src/features/users/`
- [ ] T206 [P] [US1] Añadir la entrada **Configuración → Usuarios y roles administrativos** a la navegación y sus rutas, con guard por rol (`ux-ui.md` §7 y §35) en `frontend/src/app/`

**Checkpoint**: G7 operable de extremo a extremo desde la interfaz.

---

## Phase 20: Frontend — Interfaz de Historia 5, Casos A y B

**Goal**: Completar el alcance funcional real de T116 dentro de Etapa 1 (decisión D7), sin convertirlo en funcionalidad de Etapa 2.

**Independent Test**: crear pertenencia, abrir contexto y asignar unidad organizativa y perfil, íntegramente desde la interfaz, produciendo las mismas relaciones de dominio que la API.

- [ ] T207 [US5] Implementar el wizard de asignación con comportamiento Caso A (Principal fijada automáticamente, RF-053) y Caso B (selector de Principal limitado a relaciones vigentes de la Contratista, RF-054), invocando los hooks ya existentes `useCrearPertenencia` y `useAbrirContexto` en `frontend/src/features/people/AsignacionUnidadOrganizativa/`
- [ ] T208 [US5] Reemplazar el selector plano de unidad organizativa por el componente `Tree` ya existente, mostrando exclusivamente el árbol de la Principal del contexto (CS-021) en `frontend/src/features/people/AsignacionUnidadOrganizativa/AsignacionUnidadOrganizativa.tsx` reutilizando `frontend/src/components/Tree/Tree.tsx`
- [ ] T209 [P] [US5] Crear el cliente HTTP, los hooks y la interfaz de asociación de perfil (`AsignacionTipoPersona`) contra el endpoint `/personas/{id}/perfiles` ya existente, hoy sin ninguna pieza en el frontend en `frontend/src/features/people/`
- [ ] T210 [P] [US5] Exponer desde `PersonaHistorialPage` los puntos de entrada a crear pertenencia y abrir contexto, hoy inalcanzables pese a tener hooks funcionales en `frontend/src/features/people/history/PersonaHistorialPage.tsx`
- [ ] T211 [P] [US5] Interpretar y presentar las fechas de los formularios en la zona horaria de la Compañía Principal correspondiente, con respaldo global cuando no sea resoluble (RF-080) en `frontend/src/lib/`
- [ ] T212 [P] [US5] Sustituir el UUID mostrado como respaldo cuando no resuelve el nombre de una unidad organizativa por un estado de carga o marcador, conforme RF-013, dentro del wizard de asignación de Historia 5 en `frontend/src/features/people/AsignacionUnidadOrganizativa/AsignacionUnidadOrganizativa.tsx`

**Checkpoint**: Historia 5 deja de ser operable solo por API.

---

## Phase 21: Pruebas

**Purpose**: Demostrar cada regla nueva. Ninguna prueba de T001 a T168 cubre automáticamente una regla de RF-074 a RF-081.

- [ ] T213 [P] [US1] Pruebas unitarias del dominio de asignaciones de rol: regla fundamental de `CompaniaId`, vigencia obligatoria, rechazo de solapamiento, multiplicidad por compañías distintas y renovación conforme RF-073 en `backend/tests/EnterpriseAccessControl.UnitTests/Auth/`
- [ ] T214 [US1] Pruebas de integración de RBAC y alcance que cubren **CS-036**: un `COMPANY_ADMINISTRATOR` no lista ni modifica usuarios de otra compañía, no se autoeleva, no crea `GLOBAL_ADMINISTRATOR`, y sí crea pares de su mismo nivel en su compañía en `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/`
- [ ] T215 [US1] Pruebas de integración de aislamiento que cubren **CS-037**: toda lectura fuera de alcance responde `404` con identificador correcto y conocido, en usuarios, compañías, unidades organizativas, áreas, personas, contextos y credenciales en `backend/tests/EnterpriseAccessControl.IntegrationTests/`
- [ ] T216 [US1] Prueba de integración del bootstrap que cubre **CS-038**: base vacía ⇒ se crea exactamente un `GLOBAL_ADMINISTRATOR` con cambio de contraseña pendiente; segundo arranque ⇒ no se crea otro; la contraseña sembrada cumple la política y `MAX_VALIDITY_DATE` no aparece en ninguna otra asignación en `backend/tests/EnterpriseAccessControl.IntegrationTests/Auth/`
- [ ] T217 [P] [US8] Prueba de integración que cubre **CS-039**: inactivar la Principal deniega con `COMPANIA_INACTIVA` sin alterar contextos, credenciales ni permisos; reactivar restablece el acceso sin más intervención; repetir para la compañía de pertenencia de la persona en `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/`
- [ ] T218 [P] [US8] Prueba de integración que cubre **CS-040**: dos Principales con zonas IANA distintas evalúan el mismo instante UTC contra sus propios bloques horarios; cambiar la zona de una no altera ningún instante persistido en `backend/tests/EnterpriseAccessControl.IntegrationTests/Permissions/`
- [ ] T219 [P] [US2] Prueba de integración que cubre **CS-041**: el cambio de `TipoCompania` se rechaza por cada una de las cinco categorías de dependencia y en ambas direcciones, y se acepta sin dependencias en `backend/tests/EnterpriseAccessControl.IntegrationTests/Companies/`
- [ ] T220 [P] Actualizar las clases de prueba de contrato ya existentes (`UsersContractTests`, `AuthContractTests`, `CompaniasContractTests`, `EvaluacionAccesoContractTests`, `OpenApiSnapshotTests`) para reflejar los contratos vigentes de `users.yaml`, `auth.yaml`, `companies.yaml`, `access-evaluation.yaml` y `permissions.yaml` — no implica regenerarlas desde cero — cubriendo RF-074, RF-079, RF-080 y RF-081, incluido el snapshot del documento OpenAPI en `backend/tests/EnterpriseAccessControl.ContractTests/`
- [ ] T221 [P] [US1] Pruebas de componente del módulo de usuarios (listado con aislamiento, wizard, asignación, finalizar/renovar, mensajes de error) en `frontend/tests/unit/`
- [ ] T222 [P] [US5] Pruebas de componente del wizard de Historia 5 y del árbol de unidad organizativa, manteniendo en verde las existentes de `AsignacionUnidadOrganizativa` y `PersonaHistorialPage` en `frontend/tests/unit/`
- [ ] T223 [US1] Prueba end-to-end que ejercita **UX-17 a UX-22** desde la interfaz en `frontend/tests/e2e/`
- [ ] T224 [US5] Prueba end-to-end de los Casos A y B desde la interfaz, verificando las relaciones de dominio resultantes con `GET /api/personas/{id}/estado-efectivo` en `frontend/tests/e2e/`

**Checkpoint**: cada regla nueva tiene al menos una prueba que falla si se revierte.

---

## Phase 22: Cierre y validación final

- [ ] T225 Ejecutar la regresión completa de las cinco suites (unitarias, contrato, integración, Vitest, Playwright) y confirmar que ninguna prueba del baseline histórico se rompe
- [ ] T226 Ejecutar los seis escenarios de validación de `quickstart.md` §7 correspondientes a D1 a D7
- [ ] T227 Actualizar en `README.md` el estado de implementación de las reglas nuevas, como consecuencia directa de las tareas anteriores, en `README.md`
- [ ] T228 Repetir el gate de cierre de Etapa 1 contra el código corregido y registrar el resultado en `docs/auditorias/`

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
| CS-037 | T215 |
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
