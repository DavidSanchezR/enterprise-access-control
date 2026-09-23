# Enterprise Access Control Platform

Solución web para administrar el acceso físico a instalaciones empresariales: jerarquías de compañías y
unidades organizativas, personas con históricos de pertenencia y contexto operativo, permisos con vigencia
y bloques horarios, credenciales/fotocheck, y evaluación de acceso con denegación por defecto.

> **Estado del repositorio (2026-09-23):** el **baseline de Etapa 1 está implementado por completo** — las
> 242 tareas de [`tasks.md`](specs/001-control-acceso-empresarial/tasks.md) (T001 a T242) están cerradas, el
> defecto crítico de autorización que bloqueaba el cierre (F-01/F-02) está corregido y verificado en el
> código, y el **gate de calidad de requisitos se cerró el 2026-09-21** con sus 45 ítems satisfechos
> ([`checklists/baseline-gate.md`](specs/001-control-acceso-empresarial/checklists/baseline-gate.md)).
>
> Las cinco suites se re-ejecutaron en este repositorio hoy: las cuatro de backend y frontend pasan
> íntegras; la de extremo a extremo pasa 8 de 9 desde una base de datos limpia, por un caso que depende del
> historial de ejecuciones anteriores y no de un defecto del producto
> ([detalle](#la-suite-e2e-no-es-reproducible-desde-una-base-limpia)). Corregir esa prueba es lo único
> pendiente que exige tocar código; el resto para congelar el baseline son actos de gobierno. Ver
> [Estado actual de las pruebas](#estado-actual-de-las-pruebas) y
> [Próximos pasos](#próximos-pasos-para-congelar-el-baseline).
>
> Las cifras y afirmaciones técnicas de este documento están verificadas contra el código, los archivos de
> proyecto y las dependencias instaladas, no contra la especificación. Donde ambas divergen, se indica
> explícitamente.

## Índice

1. [Objetivo](#objetivo)
2. [Contexto de negocio](#contexto-de-negocio)
3. [Capacidades principales](#capacidades-principales)
4. [Documentación de especificación (Spec Kit)](#documentación-de-especificación-spec-kit)
5. [Arquitectura](#arquitectura)
6. [Modelo de autorización](#modelo-de-autorización)
7. [Motor de evaluación de acceso](#motor-de-evaluación-de-acceso)
8. [Reglas temporales del dominio](#reglas-temporales-del-dominio)
9. [Principios de seguridad](#principios-de-seguridad)
10. [Stack tecnológico](#stack-tecnológico)
11. [Estructura del repositorio](#estructura-del-repositorio)
12. [Contrato de API](#contrato-de-api)
13. [Estrategia de testing](#estrategia-de-testing)
14. [Estado actual de las pruebas](#estado-actual-de-las-pruebas)
15. [Instrucciones de desarrollo](#instrucciones-de-desarrollo)
16. [Despliegue (producción)](#despliegue-producción)
17. [Estado actual del proyecto](#estado-actual-del-proyecto)
18. [Decisiones de la auditoría: todas resueltas](#decisiones-de-la-auditoría-todas-resueltas)
19. [Próximos pasos para congelar el baseline](#próximos-pasos-para-congelar-el-baseline)

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
  acceso) aislado del de cualquier otra Principal, y su propia zona horaria IANA con la que se interpretan
  los bloques horarios de sus áreas.
- Múltiples **Compañías Contratistas** que prestan servicios dentro de las instalaciones de una o varias
  Principales simultáneamente, mediante una relación explícita y con vigencia temporal
  (`RelaciónContratistaPrincipal`).

Una persona (típicamente de una Contratista) puede trabajar simultáneamente para varias Compañías
Principales, con unidad organizativa, permisos y credencial propios e independientes para cada una
(`ContextoOperativoPersonaPrincipal`). El histórico de compañía de pertenencia (el empleador) es
independiente de esos contextos operativos, y ninguna regla limita cuántas Principales puede atender una
misma persona a la vez. El cese de la pertenencia de una persona con su empleador dispara la **revocación
automática en cascada** de todos los contextos, asignaciones de unidad organizativa y credenciales que
dependían de ella, preservando el histórico íntegro.

El detalle funcional completo —**81 requisitos funcionales, 41 criterios de éxito y 15 sesiones de
clarificación de negocio** que corrigieron y afinaron el modelo original— vive en
[spec.md](specs/001-control-acceso-empresarial/spec.md).

> **Nota de nomenclatura.** `D1`–`D9` (sin guion) son las **decisiones de negocio** del cierre de Etapa 1.
> `D-1`–`D-5` (con guion) son las **desviaciones de implementación** detectadas al terminar T169–T228. La
> coincidencia de letra es accidental y sus contenidos no se corresponden; ambas series se rastrean por
> separado en `spec.md`.

## Capacidades principales

10 historias de usuario (8 de prioridad P1, 2 de prioridad P2):

| # | Historia | Prioridad | Estado |
|---|---|---|---|
| 1 | Inicio de sesión y alcance de gestión por compañía | P1 | Implementada (API + UI) |
| 2 | Compañías (Principal/Contratista), sus relaciones y unidades organizativas | P1 | Implementada (API + UI) |
| 3 | Datos maestros (tipo de documento, sangre, género, tipo de persona, tipo de credencial) | P1 | Implementada (API + UI) |
| 4 | Personas: registro de datos personales, de identificación y contacto | P1 | Implementada (API + UI) |
| 5 | Históricos de compañía, contexto operativo por Principal, unidad organizativa y perfil, con revocación automática | P1 | Implementada (API + UI) |
| 6 | Árbol de áreas físicas de acceso | P1 | Implementada (API + UI) |
| 7 | Tipos de persona autorizados por área | P1 | Implementada (API + UI) |
| 8 | Permisos de acceso con vigencia, bloques horarios y evaluación de acceso (motor de 15 pasos) | P1 | Implementada (API + UI) |
| 9 | Mantenimiento de credencial/fotocheck por Compañía Principal | P2 | Implementada (API + UI dentro del historial de la persona) |
| 10 | Auditoría automática por entidad (RF-026, RF-027) | P2 | Implementada (interceptor de EF Core) |
| 10 | Consultas transversales: auditoría agregada, históricos y dashboard (RF-067 a RF-069, CS-032) | P2 | **Diferida a Etapa 2** (decisión D8) |

Las historias 1 a 9 están implementadas y respaldadas por prueba automatizada, con interfaz incluida: la de
credenciales (Historia 9) vive dentro del historial de la persona, no como módulo propio.

Lo único que queda fuera del baseline son las **consultas transversales** de la Historia 10 (RF-067 a
RF-069): su necesidad funcional está declarada y anotada `[DIFERIDA A ETAPA 2]` en `spec.md`, pero no tiene
contrato de API, código ni pantalla. Eso es visible en la navegación de la SPA
([`AppShell.tsx`](frontend/src/app/AppShell.tsx)), que ofrece Compañías, Unidades organizativas, Áreas de
acceso, Permisos, Evaluación de acceso, Personas, Datos maestros y —bajo Configuración— Usuarios y roles
administrativos, **sin entradas de Históricos ni de Auditoría**; y la ruta `/` renderiza un marcador de
Dashboard pendiente en lugar de los indicadores agregados que describe `ux-ui.md` §8.

## Documentación de especificación (Spec Kit)

El proyecto se construyó con [Spec Kit](https://github.com/github/spec-kit) siguiendo el ciclo
`/speckit-constitution` → `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` →
`/speckit-implement` → `/speckit-analyze` → `/speckit-checklist`. Todos los artefactos viven bajo
[`specs/001-control-acceso-empresarial/`](specs/001-control-acceso-empresarial/) y son la fuente de verdad:
el código se deriva de ellos, no al revés.

| Artefacto | Contenido | Magnitud |
|---|---|---|
| [`spec.md`](specs/001-control-acceso-empresarial/spec.md) | Especificación funcional: historias, requisitos, entidades, criterios de éxito, supuestos y el historial completo de clarificaciones | 81 RF · 41 CS · 10 historias · 15 sesiones de clarificación · 10 decisiones pendientes, todas cerradas |
| [`plan.md`](specs/001-control-acceso-empresarial/plan.md) | Plan de implementación: Technical Context, Constitution Check y sus re-chequeos, estructura de código, plan de cierre de desviaciones | Stack ratificado · re-chequeos de constitución sin violaciones |
| [`research.md`](specs/001-control-acceso-empresarial/research.md) | Decisiones técnicas, cada una con sus alternativas descartadas y su justificación | 34 secciones |
| [`data-model.md`](specs/001-control-acceso-empresarial/data-model.md) | Entidades, campos, validaciones clave, índices y diagrama textual de relaciones | 22 entidades de dominio |
| [`ux-ui.md`](specs/001-control-acceso-empresarial/ux-ui.md) | Especificación de UX/UI: dirección visual "Enterprise Operations Console", design tokens, navegación, pantallas, flujos y accesibilidad | 35 secciones · UX-01 a UX-22 · 27 componentes base + 9 de dominio |
| [`quickstart.md`](specs/001-control-acceso-empresarial/quickstart.md) | Guía de validación end-to-end con escenarios manuales reproducibles | 9 secciones · CS-009, multi-Principal, D1–D9 y D-1/D-2/D-4 |
| [`tasks.md`](specs/001-control-acceso-empresarial/tasks.md) | Tareas de implementación por historia, con trazabilidad RF/CS → tarea → evidencia de prueba | 242 tareas (T001–T242), 242 completadas |
| [`contracts/`](specs/001-control-acceso-empresarial/contracts/) | Contratos OpenAPI por grupo funcional, verificados por las pruebas de contrato | 10 archivos · 47 rutas · 72 operaciones |
| [`checklists/baseline-gate.md`](specs/001-control-acceso-empresarial/checklists/baseline-gate.md) | Gate de calidad **de la redacción de los requisitos** previo a congelar el baseline, con su acta de cierre | 45 ítems · cerrado el 2026-09-21 |
| [`.specify/memory/constitution.md`](.specify/memory/constitution.md) | Constitución del proyecto: 7 principios no negociables y reglas de arquitectura e ingeniería | v1.1.1, ratificada 2026-09-14 |

Los artefactos se construyeron de forma **acumulativa y trazable**: ninguna sesión posterior renumeró,
reabrió ni reescribió tareas ya cerradas. Cuando una decisión de negocio invalidó a una anterior, la anterior
se conserva marcada como `SUPERADA`, `MATIZADA` o `REEMPLAZADA` junto al puntero a la que la sustituye, de
modo que la trazabilidad histórica no se pierde. El precio de esa política es que `spec.md` debe leerse
completo: una regla citada aisladamente puede estar superada por una sesión posterior.

Los informes de auditoría y de gate que acompañan al proceso están en
[`docs/auditorias/`](docs/auditorias/) (12 documentos HTML, 2 de ellos también en PDF).

## Arquitectura

Aplicación web con frontend y backend separados. El backend sigue una arquitectura en capas:

```
Dominio → Aplicación → Infraestructura → API
```

- **Dominio**: entidades, enums y reglas de negocio puras, sin dependencias externas.
- **Aplicación**: casos de uso por módulo (auth, maestros, compañías, unidades organizativas, personas,
  áreas de acceso, permisos, evaluación de acceso, credenciales), DTOs y validación con FluentValidation.
- **Infraestructura**: `DbContext` de EF Core sobre SQL Server, migraciones (incluidos los seis triggers
  SQL de no-solapamiento temporal), el interceptor de auditoría automática, y los mecanismos de seguridad
  (hashing de contraseñas, resolución del alcance administrativo, reloj empresarial por zona horaria).
- **API**: controladores ASP.NET Core, autenticación JWT, `ProblemDetails` para errores (RFC 7807/9457),
  health checks, composición de dependencias.

El frontend es una SPA React organizada por *features*, una por módulo de negocio (`auth`, `companies`,
`org-units`, `people`, `area-access`, `permissions`, `credentials`, `masters`, `users`,
`access-evaluation`). [ux-ui.md](specs/001-control-acceso-empresarial/ux-ui.md) define la dirección de
diseño "Enterprise Operations Console" y un catálogo de 27 componentes base + 9 de dominio como **objetivo
de especificación**; lo construido hoy es más acotado: una capa de *design tokens* en
[`src/index.css`](frontend/src/index.css) (paleta con modo claro/oscuro, tipografía `system-ui`, radios y
sombras), dos componentes compartidos —[`Tree`](frontend/src/components/Tree) (patrón ARIA `treeview`,
navegable por teclado) y [`Dialogo`](frontend/src/components/Dialogo.tsx)— y una hoja de estilos por
*feature*. La accesibilidad WCAG 2.2 AA es el objetivo declarado; hoy está verificada por prueba
automatizada sobre el componente `Tree`.

**Solo la API está contenedorizada.** [`backend/Dockerfile`](backend/Dockerfile) publica la API sobre
`mcr.microsoft.com/dotnet/aspnet:10.0` (imagen Debian, no *chiseled*: `Microsoft.Data.SqlClient` necesita
ICU) en el puerto 8080 y con usuario sin privilegios; su contexto de construcción es la raíz del repositorio
para compartir `.editorconfig` y `Directory.Build.props` con el build local. **No existe Dockerfile ni
servicio de compose para la SPA**: `npm run build` genera estáticos en `frontend/dist/` que debe servir un
proxy inverso propio, el mismo que reenvía `/api/` y `/health/` a la API para que ambos compartan origen
(la API no configura CORS). Ver [Despliegue (producción)](#despliegue-producción).

Detalle completo de las decisiones de arquitectura, incluidas las correcciones de dominio y sus
re-chequeos de constitución: [plan.md](specs/001-control-acceso-empresarial/plan.md) y
[research.md](specs/001-control-acceso-empresarial/research.md) (34 secciones técnicas).

## Modelo de autorización

El sistema separa explícitamente dos mecanismos de autorización (research.md §18):

1. **Autorización de plataforma** (ASP.NET Core Authentication/Authorization): valida la sesión (JWT
   Bearer) y aplica el **alcance administrativo** del usuario autenticado.
2. **Motor de evaluación de acceso de dominio** (Historia 8): decide si una *persona* puede entrar
   físicamente a un *área*. Es independiente del anterior y se describe en la
   [sección siguiente](#motor-de-evaluación-de-acceso).

### RBAC con catálogo cerrado de roles

El alcance administrativo se deriva de las `AsignaciónRolAdministrativo` **vigentes** del usuario
(RF-074 a RF-077). El catálogo de roles es **cerrado**: agregar un rol exige modificar el modelo de
autorización, no insertar una fila.

| Rol | Alcance | Regla fundamental |
|---|---|---|
| `GLOBAL_ADMINISTRATOR` | Todas las compañías | `CompañíaId` es nulo **si y solo si** el rol es GLOBAL |
| `COMPANY_ADMINISTRATOR` | Exactamente las compañías de sus asignaciones vigentes | `CompañíaId` obligatorio |

El alcance no es un atributo del usuario sino de cada asignación, y cada asignación tiene su propia vigencia
auditable, renovable y finalizable.

**Cómo viaja el alcance.** Al iniciar sesión, el rol vigente se emite como claim `rol` y —solo para
`COMPANY_ADMINISTRATOR`— una compañía por claim `alcance_compania`. Un `GLOBAL_ADMINISTRATOR` **no enumera
compañías** en su token: su alcance lo determina el claim de rol, lo que evita devolver una lista cerrada
que daría una falsa sensación de exhaustividad. La consecuencia operativa es que el alcance se resuelve
desde el token y no desde la base de datos en cada request: **cambiar las asignaciones de un usuario no
surte efecto hasta que vuelve a iniciar sesión** o su token expira (60 minutos por defecto,
`Jwt:AccessTokenMinutos`). Un token sin claim de rol —de un usuario sin asignación vigente— no tiene alcance
alguno.

**Denegación por defecto a nivel de plataforma.** [`Program.cs`](backend/src/EnterpriseAccessControl.Api/Program.cs)
declara `SetFallbackPolicy(RequireAuthenticatedUser)`: un endpoint que olvidara declarar su autorización
queda protegido igualmente en lugar de publicarse anónimo. Las únicas excepciones son explícitas
(`AllowAnonymous`): login, los dos health checks y, solo en `Development`, el documento OpenAPI.

### Reglas de aislamiento efectivas

- Un `COMPANY_ADMINISTRATOR` administra usuarios **solo de su propia compañía**; no puede reasignar fuera de
  su alcance, elevar su propio alcance, asignar `GLOBAL_ADMINISTRATOR` ni asignar `COMPANY_ADMINISTRATOR`
  para otra compañía (RF-076).
- Un usuario **sin ninguna asignación vigente** no tiene alcance alguno: toda operación administrativa
  protegida se rechaza por denegación por defecto. Es un caso distinto del `404` por recurso ajeno.
- **Poseer el identificador de un recurso no otorga autorización.** Una lectura fuera de alcance responde
  `404` y no revela la existencia del recurso, aun con el identificador correcto y conocido (CS-037,
  verificado sobre los **siete tipos de recurso** que el sistema expone con identificador propio: usuario,
  compañía, unidad organizativa, área de acceso, persona, contexto operativo y asignación de credencial).
- Una `Persona` no tiene una única compañía propietaria: está dentro del alcance si su **compañía de
  pertenencia vigente** lo está **o** si tiene al menos un **contexto operativo vigente** con una Principal
  del alcance (RF-077).
- Toda denegación por alcance es **libre de efectos**: la verificación precede a cualquier escritura, de
  modo que una operación que habría disparado la cascada de revocación no deja nada detrás.
- Las **búsquedas y filtros** se aplican sobre el conjunto ya restringido al alcance y **antes** de paginar;
  nunca amplían el alcance ni sirven como oráculo de enumeración.

> El defecto crítico F-01 (`UsuarioService` sin ningún control de alcance), que las auditorías del
> 2026-09-16 reprodujeron en ejecución y que bloqueaba el cierre del baseline, **está corregido**: las nueve
> operaciones del servicio pasan hoy por resolución de alcance y verificación del recurso concreto. Ver
> [Estado actual del proyecto](#estado-actual-del-proyecto).

## Motor de evaluación de acceso

Algoritmo de **15 pasos**, independiente de la autorización de plataforma, que determina si una persona
puede acceder físicamente a un área en una fecha/hora dada. Mantiene el principio de denegación por defecto:
cualquier paso sin resultado inequívoco produce `DENEGADO`. Vive en
[`Domain/Services/EvaluadorDeAcceso.cs`](backend/src/EnterpriseAccessControl.Domain/Services/EvaluadorDeAcceso.cs)
como lógica pura, con
[`EvaluacionAccesoService`](backend/src/EnterpriseAccessControl.Application/Permissions/EvaluacionAccesoService.cs)
recolectando de la base de datos únicamente los datos que cada paso necesita.

| Paso | Verificación |
|---|---|
| 1 | El usuario que consulta tiene la Compañía Principal del área en su alcance |
| 2–4 | Identificar persona, área y la Compañía Principal propietaria del área |
| 5 | Esa Compañía Principal está `ACTIVO` (RF-079) |
| 6 | La persona tiene un contexto operativo vigente con esa Principal, **legítimo** según su compañía de pertenencia vigente —que también debe estar `ACTIVO`— y, si es Contratista, con `RelaciónContratistaPrincipal` vigente |
| 7 | Existe una `AsignaciónCredencial` vigente para esa Principal: `Estado = ASIGNADO` **y** dentro de su rango de fechas (RF-066, RF-070) |
| 8 | El área está `ACTIVA` |
| 9 | Algún perfil vigente de la persona está autorizado en el área (RF-024) |
| 10 | Determinar su unidad organizativa vigente dentro de ese contexto operativo |
| 11 | Evaluar permisos aplicables en los tres niveles: PERSONA, UNIDAD_ORGANIZATIVA, COMPAÑÍA |
| 12 | Evaluar la vigencia de fechas de cada permiso aplicable |
| 13 | Evaluar día de semana y bloque horario **en la zona horaria IANA de esa Compañía Principal** (RF-080) |
| 14 | Resolver conflictos con precedencia `PERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA` |
| 15 | Conceder o denegar |

La denegación se comunica con uno de **12 motivos tipificados** (`MotivoDenegacion` en
`contracts/access-evaluation.yaml`): `PERSONA_NO_ENCONTRADA`, `AREA_NO_ENCONTRADA`,
`FUERA_DE_ALCANCE_USUARIO`, `SIN_CONTEXTO_OPERATIVO_VIGENTE`, `RELACION_CONTRATISTA_PRINCIPAL_VENCIDA`,
`SIN_CREDENCIAL_VIGENTE`, `COMPANIA_INACTIVA`, `AREA_INACTIVA`, `PERFIL_NO_AUTORIZADO_EN_AREA`,
`SIN_PERMISO_APLICABLE`, `PERMISO_FUERA_DE_VIGENCIA` y `FUERA_DE_BLOQUE_HORARIO`.

Dos propiedades que conviene no perder de vista:

- **La credencial es un gate, no un adorno.** Sin una credencial vigente para la Principal propietaria del
  área, el acceso se deniega antes de evaluar ningún permiso. Tenerla es condición necesaria, no suficiente.
- **Inactivar una compañía deniega por evaluación dinámica, no por cascada de escritura** (RF-079): no se
  modifica ningún registro dependiente, y reactivarla restablece el acceso sin intervención adicional.

Detalle paso a paso: `spec.md` Historia 8 y `research.md` §7.

## Reglas temporales del dominio

El Principio IV de la constitución (Integridad Temporal e Históricos) se concreta en un conjunto de reglas
que atraviesan todo el modelo y explican buena parte de su complejidad:

- **Vigencia obligatoria (RF-071).** Las seis asociaciones temporales vinculadas a una persona
  —`AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`,
  `AsignaciónCredencial`, `AsignaciónTipoPersona` y `PermisoAcceso`— nacen con `FechaHoraInicio` **y**
  `FechaHoraFin` reales. No existe "vigencia indefinida": ni `null` ni fecha centinela.
- **Contención (RF-072).** Las tres asociaciones que dependen de la pertenencia —contexto operativo, unidad
  organizativa y credencial— deben estar temporalmente contenidas dentro de la vigencia de esa pertenencia.
  Pueden terminar antes; nunca empezar antes ni terminar después.
- **No-solapamiento por trigger.** **Seis** triggers `AFTER INSERT, UPDATE` en SQL Server garantizan que no
  se solapen períodos incompatibles, cada uno con su propia clave de partición:
  `trg_AsignacionPersonaCompania_NoSolapamiento`, `trg_ContextoOperativoPersonaPrincipal_NoSolapamiento`,
  `trg_AsignacionPersonaUnidadOrganizativa_NoSolapamiento`, `trg_AsignacionCredencial_NoSolapamiento`,
  `trg_RelacionContratistaPrincipal_NoSolapamiento` y `trg_AsignacionRolAdministrativo_NoSolapamiento`
  (este último añadido con el RBAC de D1). Es el equivalente idiomático del `EXCLUDE USING gist` de
  PostgreSQL, que SQL Server no tiene (research.md §5). El mecanismo no tiene representación en el modelo
  de EF Core —solo se declara con `HasTrigger` para que EF no use `OUTPUT` en las escrituras—, y por eso
  las pruebas de integración corren contra SQL Server real y no contra un proveedor en memoria.
- **Vigencia ≠ estado.** `Estado` es administrativo/informativo. La vigencia efectiva se determina siempre
  comparando fechas, nunca por `Estado` de forma aislada: una credencial `ASIGNADO` cuya fecha de fin ya
  pasó **no** concede acceso, y el sistema no la transiciona automáticamente por el mero paso del tiempo.
- **Revocación en cascada (RF-061 a RF-065).** Cerrar una pertenencia cierra en la misma operación los
  contextos, unidades organizativas y credenciales que dependían de ella, fijando `FechaHoraFin`, `Estado` y
  `MotivoFin`, sin eliminar nada ni tocar `FechaHoraInicio`. La re-validación dinámica de la evaluación de
  acceso permanece como defensa en profundidad.
- **Renovación (RF-073, RF-075).** `FechaHoraFin` puede extenderse hacia adelante mediante una operación
  explícita de renovación, que no crea un registro nuevo, no mueve `FechaHoraInicio` y no renueva las
  asociaciones dependientes. Solo es renovable lo que sigue vigente **dinámicamente**: una vigencia ya
  expirada no se puede "puentear" retroactivamente.
- **UTC siempre, zona local solo para presentar y para evaluar horarios.** Los timestamps se persisten en
  UTC; la zona IANA de cada Compañía Principal convierte entre ese instante y la hora local. Cambiar la zona
  no reinterpreta instantes ya persistidos.

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

Los siete principios están verificados como cumplidos (`PASS`) en el re-chequeo de constitución de
`plan.md`. La violación activa del **Principio I** que las auditorías del 2026-09-16 registraron en el
perímetro de administración de usuarios quedó cerrada con la implementación del RBAC y del Resource
Ownership (T169–T228, T229–T242), y su cobertura de regresión está verificada sobre los siete recursos que
CS-037 enumera.

## Stack tecnológico

Versiones tomadas de los archivos de proyecto y de las dependencias instaladas, no de la especificación —
`plan.md` declara "TypeScript 5.6+", pero lo instalado es TypeScript 6.

**Backend** (`net10.0`, `LangVersion 13.0`, `Nullable` y `TreatWarningsAsErrors` activados en
[`Directory.Build.props`](backend/Directory.Build.props))

| Componente | Versión | Nota |
|---|---|---|
| .NET / ASP.NET Core Web API | `net10.0` (SDK 10.0.401) | C# 13 fijado explícitamente |
| Entity Framework Core + provider SQL Server + Design | 10.0.12 | Migrations como mecanismo de evolución de esquema |
| SQL Server | `mcr.microsoft.com/mssql/server:2022-latest` | `datetime2(3)` en UTC, CTE recursivos, `rowversion`, triggers de no-solapamiento |
| FluentValidation (+ `DependencyInjectionExtensions`) | 12.1.1 | Ejecutada por `ValidacionAutomaticaFilter`, no por auto-validación |
| NodaTime | 3.3.4 | Zona IANA por Compañía Principal, con respaldo global configurable |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.12 | `ClockSkew = 0`: la expiración del token es exacta |
| `Microsoft.AspNetCore.OpenApi` | 10.0.12 | Generador nativo, **no** Swashbuckle; expuesto solo en `Development` |
| `System.IdentityModel.Tokens.Jwt` | 8.22.0 | Emisión del token |
| `AspNetCore.HealthChecks.SqlServer` | 9.0.0 | `/health/live` y `/health/ready` |

`PasswordHasher<T>` (ASP.NET Core Identity) se usa solo como algoritmo de hashing, sin su modelo de usuario;
llega por el `FrameworkReference` de `Microsoft.AspNetCore.App`. Los errores viajan como `ProblemDetails`
(RFC 7807/9457) con un campo `codigo` estable en `extensions`.

Dos detalles de persistencia que conviene conocer antes de tocar el esquema:

- `rowversion` (concurrencia optimista) está habilitado solo en las entidades con escritura concurrente
  real; un conflicto se traduce a `409` con `ProblemDetails`, no a una excepción de infraestructura.
- El índice clúster sobre `CreatedAt` —con la PK `Id` como `NONCLUSTERED`— se aplica hoy a **dos**
  entidades: `HistorialContraseña` y `AsignaciónRolAdministrativo`. El comentario de
  `ConfiguracionExtensions.ConClusterPorCreatedAt` afirma que cubre "las 6 entidades de histórico de alto
  volumen"; eso no coincide con el modelo actual y es una corrección pendiente en el código, no en este
  documento.

**Frontend**

| Componente | Versión instalada |
|---|---|
| React / React DOM | 18.3.1 |
| TypeScript | 6.0.3 |
| Vite | 8.3.0 |
| TanStack Query (`@tanstack/react-query`) | 5.x — estado de servidor |
| React Hook Form + Zod (`@hookform/resolvers`) | 7.x / 4.x — formularios y validación de cliente |
| React Router (`react-router-dom`) | 7.x |
| Axios | 1.x — cliente HTTP, normalizado a `ProblemDetails` en [`lib/apiClient.ts`](frontend/src/lib/apiClient.ts) |

Sin librería de componentes de terceros: los *design tokens*, el `Tree` accesible y el `Dialogo` son propios
(ver [Arquitectura](#arquitectura)).

**Testing**

- Backend: xUnit 2.9.3 + FluentAssertions 8.11.0 + `Microsoft.AspNetCore.Mvc.Testing` 10.0.12;
  `Testcontainers.MsSql` 4.15.0 para integración contra SQL Server real —imprescindible para los triggers
  de no-solapamiento, que no tienen representación en el modelo de EF Core—; `YamlDotNet` 18.1.0 en las
  pruebas de contrato para leer los `contracts/*.yaml` y compararlos con el OpenAPI generado.
- Frontend: Vitest 5.0.0 + React Testing Library 16.x (`jsdom`) y Playwright 1.63 para end-to-end.

**Infraestructura**

- Docker / Docker Compose v2: SQL Server oficial + API contenedorizada. La SPA **no** tiene imagen ni
  servicio de compose (ver [Arquitectura](#arquitectura)).
- Sin proveedor de identidad externo, sin mensajería/colas, sin caché distribuido, sin microservicios —
  decisión arquitectónica explícita mientras ningún requisito lo justifique (constitución, Reglas de
  Arquitectura e Ingeniería).
- Sin pipeline de integración continua: no existe `.github/workflows`.

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
├── .env.example                                   # VITE_API_PROXY_TARGET y variables de E2E
├── src/
│   ├── index.css                                  # Design tokens (claro/oscuro), tipografía system-ui
│   ├── features/                                  # auth, companies, org-units, people, area-access,
│   │                                              # permissions, credentials, masters, users,
│   │                                              # access-evaluation (cada una con su .css)
│   ├── components/                                # Compartidos: Tree (ARIA treeview) y Dialogo
│   ├── app/                                       # App, AppShell, RutaProtegida, queryClient
│   └── lib/                                       # apiClient (Axios), problemDetails, fechas
└── tests/
    ├── unit/                                      # Vitest + React Testing Library (18 archivos)
    └── e2e/                                       # Playwright (5 archivos, 9 casos)

specs/001-control-acceso-empresarial/
├── spec.md              # Especificación funcional (81 RF, 41 CS, 15 sesiones de clarificación)
├── plan.md              # Plan de implementación y re-chequeos de constitución
├── research.md          # 34 secciones de decisiones técnicas
├── data-model.md        # 22 entidades de dominio
├── ux-ui.md             # Especificación de UX/UI (35 secciones, UX-01 a UX-22)
├── quickstart.md        # Guía de validación end-to-end (9 secciones)
├── tasks.md             # 242 tareas de implementación, organizadas por historia
├── checklists/          # Gate de calidad de requisitos (45 ítems, cerrado el 2026-09-21)
└── contracts/           # 10 contratos OpenAPI (47 rutas, 72 operaciones)

backend/Dockerfile       # Única imagen del repositorio (la API); la SPA no se contenedoriza
docs/auditorias/         # 12 informes de auditoría y de gate (HTML; 2 también en PDF)
scripts/                 # verificar-health-docker.sh (health checks sobre el compose de desarrollo)
.specify/                # Constitución, plantillas, scripts PowerShell y workflows de Spec Kit
docker-compose.yml       # Entorno de desarrollo local (SQL Server + API)
docker-compose.prod.yml  # Despliegue de producción (SQL Server + API, sin secretos por defecto)
```

No hay `.github/workflows`: las suites se ejecutan localmente.

## Contrato de API

La API expone **72 operaciones sobre 47 rutas**, declaradas en 10 contratos OpenAPI y verificadas una por
una por las pruebas de contrato, incluido un snapshot que compara el documento generado por
`Microsoft.AspNetCore.OpenApi` contra los archivos publicados.

| Contrato | Versión | Rutas | Operaciones | Cobertura |
|---|---|---|---|---|
| [`auth.yaml`](specs/001-control-acceso-empresarial/contracts/auth.yaml) | 1.0.0 | 3 | 3 | Login, cambio de contraseña, sesión |
| [`users.yaml`](specs/001-control-acceso-empresarial/contracts/users.yaml) | **2.1.0** | 6 | 9 | Usuarios, asignaciones de rol, finalizar/renovar, búsqueda server-side |
| [`masters.yaml`](specs/001-control-acceso-empresarial/contracts/masters.yaml) | 1.0.0 | 10 | 15 | Tipo de documento, sangre, género, tipo de persona, tipo de credencial |
| [`companies.yaml`](specs/001-control-acceso-empresarial/contracts/companies.yaml) | 1.0.0 | 4 | 7 | Compañías y relaciones Contratista↔Principal |
| [`org-units.yaml`](specs/001-control-acceso-empresarial/contracts/org-units.yaml) | 1.0.0 | 4 | 6 | Árbol de unidades organizativas |
| [`people.yaml`](specs/001-control-acceso-empresarial/contracts/people.yaml) | 1.0.0 | 9 | 15 | Personas, históricos, contextos operativos, perfiles, estado efectivo |
| [`area-access.yaml`](specs/001-control-acceso-empresarial/contracts/area-access.yaml) | 1.0.0 | 5 | 8 | Árbol de áreas y tipos de persona autorizados |
| [`permissions.yaml`](specs/001-control-acceso-empresarial/contracts/permissions.yaml) | 1.0.0 | 2 | 4 | Permisos y bloques horarios |
| [`credentials.yaml`](specs/001-control-acceso-empresarial/contracts/credentials.yaml) | 1.0.0 | 3 | 4 | Asignación e histórico de credenciales por Principal |
| [`access-evaluation.yaml`](specs/001-control-acceso-empresarial/contracts/access-evaluation.yaml) | 1.0.0 | 1 | 1 | Evaluación de acceso (motor de 15 pasos) |

La paridad es exacta y comprobable: los 10 controladores de
[`Api/Controllers/`](backend/src/EnterpriseAccessControl.Api/Controllers) declaran **72 atributos
`[HttpGet|Post|Put|Patch|Delete]`**, uno por operación del contrato, y cada contrato corresponde a un único
controlador con el mismo recuento. El documento OpenAPI generado se publica en `/openapi/v1.json` **solo en
`Development`**; `OpenApiSnapshotTests` lo compara contra `contracts/*.yaml` y falla si divergen.

Los errores usan `ProblemDetails` (RFC 7807/9457) con un campo `codigo` estable en `extensions`, para que
el cliente decida sin parsear el `detail` destinado a humanos. Los enumerados viajan como su literal de
texto (`ACTIVO`, `PRINCIPAL_MANDANTE`, …) —el mismo valor que se persiste en `nvarchar` y que declaran los
contratos— y no como enteros, para que un reordenamiento de miembros no cambie en silencio el significado
de los datos ya emitidos.

## Estrategia de testing

Conforme al Principio VII (no negociable), cada historia de usuario P1 cuenta con pruebas automatizadas
independientes, con cobertura explícita de denegación por defecto, ciclos jerárquicos, solapamientos
temporales inválidos y fuga de datos entre compañías:

- **Unitarias** (xUnit + FluentAssertions): reglas de dominio y casos de uso de Aplicación, sin base de
  datos.
- **Integración** (Testcontainers.MsSql): contra una instancia real de SQL Server — necesarias para
  verificar los triggers de no-solapamiento temporal, que no tienen representación en el modelo de EF Core
  y no pueden probarse con un proveedor en memoria.
- **Contrato**: verifican que la API (incluido el formato `ProblemDetails` y el documento OpenAPI generado)
  coincide con los 10 archivos publicados en `contracts/`.
- **Frontend unitario/componentes** (Vitest + React Testing Library): incluye accesibilidad WCAG 2.2 AA del
  componente `Tree`.
- **End-to-end** (Playwright): 9 casos en 5 archivos, contra la API real y con base de datos dedicada
  (`EnterpriseAccessControl_E2E`). Corren en serie (`workers: 1`) porque los escenarios amplían el alcance
  del mismo usuario y dos escrituras concurrentes perderían una de las compañías. Su
  [preparación global](frontend/tests/e2e/soporte/preparacion-global.ts) levanta SQL Server, aplica **las
  migraciones reales**, arranca la API y luego inserta por SQL dos usuarios de prueba —un
  `GLOBAL_ADMINISTRATOR` y un `COMPANY_ADMINISTRATOR` de otra compañía, que es lo que permite contrastar el
  aislamiento—, con su contraseña hasheada en formato Identity V3 desde Node. Esas filas son preparación de
  pruebas, no una semilla del producto, y viven solo en esa base.

No hay todavía un pipeline de integración continua configurado en el repositorio (sin `.github/workflows`);
las suites se ejecutan localmente según [Instrucciones de desarrollo](#instrucciones-de-desarrollo).
Automatizarlas en CI es uno de los [próximos pasos](#próximos-pasos-para-congelar-el-baseline).

## Estado actual de las pruebas

Ejecutadas en este repositorio el **2026-09-23**, con Docker activo:

| Suite | Resultado | Antes (auditoría 2026-09-20) |
|---|---|---|
| `dotnet build` (solución completa) | ✅ 0 errores, 0 advertencias | 0 / 0 |
| Backend — Unitarias (`EnterpriseAccessControl.UnitTests`) | ✅ 101/101 | 101/101 |
| Backend — Integración (`EnterpriseAccessControl.IntegrationTests`) | ✅ 534/534 | 511/511 |
| Backend — Contrato (`EnterpriseAccessControl.ContractTests`) | ✅ 246/246 | 244/244 |
| Frontend — `npm run build` (`tsc -b && vite build`) | ✅ sin errores | OK |
| Frontend — Vitest (`npm run test`) | ✅ 160/160 (18 archivos) | 149/149 |
| Frontend — E2E (Playwright) | ⚠️ 8/9 sobre base limpia · 9/9 sobre base acumulada | 8/8 |

El crecimiento respecto del gate del 2026-09-20 corresponde a las tareas T229–T242, que añadieron cobertura
de la renovación de asignaciones de rol, de la búsqueda server-side y del aislamiento por alcance sobre los
siete recursos de CS-037.

### La suite E2E no es reproducible desde una base limpia

Un caso —`UX-22: la búsqueda encuentra a un usuario que no está en la primera página`— **depende del
historial de ejecuciones anteriores**. Crea un usuario con correo `zzz.e2e.busqueda.<timestamp>@…`,
deliberadamente al final del orden alfabético, y luego afirma que *no* aparece en la página visible antes de
buscarlo. Pero el listado pagina de 20 en 20 y la prueba **no crea los usuarios que harían falta para
forzar una segunda página**: se apoya en que la base ya los tenga acumulados, como reconoce su propio
comentario ("la base acumula usuarios entre ejecuciones"). Sobre `EnterpriseAccessControl_E2E` recién
creada —con 5 usuarios— el objetivo cae en la página 1 y la aserción falla.

El comportamiento se reprodujo de forma determinista partiendo de una base recién creada y repitiendo la
misma suite sin cambiar una línea de código. Cada corrida deja 4 usuarios nuevos, y el caso pasa a verde
exactamente cuando el total supera el tamaño de página:

| Corrida | Usuarios en la base al empezar | Resultado |
|---|---|---|
| 1 | 5 | 8/9 — UX-22 falla |
| 2 | 9 | 8/9 — UX-22 falla |
| 3 | 13 | 8/9 — UX-22 falla |
| 4 | 17 (→ 21 tras crear el suyo) | **9/9 — UX-22 pasa** |

Es un defecto de aislamiento de la prueba, no del producto: la búsqueda server-side que pretende verificar
está implementada y cubierta además por `BusquedaUsuariosTests` (integración, T239), que sí construye su
propio conjunto de datos y pasa siempre. Arreglarlo requiere que la prueba siembre los usuarios que
necesita, o que fije un `tamañoPagina` pequeño, en lugar de heredar el estado de corridas previas.

### Dos trampas del entorno local

Ambas producen fallos que parecen defectos del producto y no lo son:

1. **Un servidor de Vite ya corriendo en el puerto 5173.** `playwright.config.ts` usa
   `reuseExistingServer: !process.env.CI`, así que reutiliza el que encuentre — incluido un `npm run dev`
   anterior cuyo proxy apunta al destino por defecto (`http://localhost:5290`, el perfil de `dotnet run`)
   en lugar de a la API contenedorizada. El resultado es un `502` en `/api/**` que la SPA muestra como
   "Correo o contraseña incorrectos". Detener ese servidor antes de `npm run test:e2e` evita el falso
   negativo.
2. **`MSSQL_DATABASE` puede no llegar a Docker Compose.** La preparación global lo pasa por el entorno del
   proceso hijo; si no se propaga, la API se levanta contra la base de **desarrollo** y no encuentra los
   usuarios que la preparación insertó en `EnterpriseAccessControl_E2E`, con el mismo síntoma de
   credenciales inválidas. Exportar `MSSQL_DATABASE=EnterpriseAccessControl_E2E` en la shell antes de
   lanzar la suite lo resuelve.

Sin estas dos precauciones la suite falla 7 de 9 en el inicio de sesión, y el síntoma no distingue un
problema de entorno de uno de producto. Es el mismo fallo silencioso que afecta al desarrollo normal: ver
la nota sobre el proxy en [Instrucciones de desarrollo](#instrucciones-de-desarrollo).

**Importante:** que todas las suites estén en verde no significa que el sistema esté libre de defectos. Las
auditorías del 2026-09-16 reprodujeron en ejecución una escalada de privilegios y fugas de datos entre
compañías que **ninguna prueba automatizada de entonces cubría** — eran huecos de cobertura, no regresiones.
Esa lección es la razón por la que T237, T238, T239 y T242 existen: cada capacidad nueva tiene hoy una
prueba que falla si se revierte.

## Instrucciones de desarrollo

Prerrequisitos: .NET 10 SDK (verificado con 10.0.401), Node.js 20+ (verificado con 24.15.0 y npm 11.12.1),
Docker Desktop o motor compatible (verificado con Docker 29.8), y la herramienta `dotnet-ef`
(`dotnet tool install --global dotnet-ef`).

Antes del primer arranque, define `BOOTSTRAP_ADMIN_PASSWORD` con la contraseña del administrador inicial
(RF-078): la API no arranca sin ella —ni en desarrollo ni en producción— y `docker-compose.yml` no declara
ningún valor por defecto, para que el secreto nunca quede versionado en el repositorio. La forma más simple
es un archivo `.env` local junto a `docker-compose.yml` (ya excluido por `.gitignore`), que Docker Compose
lee automáticamente:

```bash
echo 'BOOTSTRAP_ADMIN_PASSWORD=<elige-una-contraseña-que-cumpla-la-política-de-contraseñas>' > .env
```

**El orden importa: las migraciones van antes que la API.** La API no las aplica al arrancar (ni en
desarrollo), y su rutina de arranque (RF-078) consulta la base de datos durante el `StartAsync` del host: si
el esquema no existe, el proceso **falla y el contenedor termina** con `Cannot open database` (SQL error
4060) en lugar de quedar levantado en un estado degradado. La secuencia correcta es levantar SQL Server,
migrar desde el host y solo entonces arrancar la API:

```bash
# 1. SQL Server, y esperar a que esté sano
docker compose up -d --build --wait sqlserver

# 2. Esquema (usa la cadena de appsettings.Development.json → localhost,1433)
dotnet ef database update \
  --project backend/src/EnterpriseAccessControl.Infrastructure \
  --startup-project backend/src/EnterpriseAccessControl.Api

# 3. API
docker compose up -d --build api

# 4. Verificación
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8080/health/ready   # 200

# 5. Frontend
cd frontend
npm install
npm run dev     # http://localhost:5173, proxy /api → VITE_API_PROXY_TARGET
```

**El proxy del servidor de desarrollo apunta por defecto a `http://localhost:5290`** (el perfil de
`dotnet run`), donde no hay nada escuchando si levantaste la API con Docker. Para consumir la API
contenedorizada del paso 3, copia [`frontend/.env.example`](frontend/.env.example) a `frontend/.env` y
define `VITE_API_PROXY_TARGET=http://localhost:8080`; exportar la variable en la shell antes de
`npm run dev` también funciona y tiene precedencia.

> Si no lo haces, **cada llamada a `/api` devuelve `502`** y la pantalla de acceso muestra "Correo o
> contraseña incorrectos" aunque las credenciales sean correctas: `LoginPage` traduce a ese texto
> cualquier error que no sea `403`, así que un fallo de infraestructura es indistinguible de uno de
> credenciales. La pista que lo delata es que `Usuario.IntentosFallidosConsecutivos` **no se incrementa**
> en la base de datos: la petición nunca llegó a la API.

Guía paso a paso, incluidos los escenarios de validación funcional completos (CS-009, el escenario
multi-Principal de Pedro García/Servicios ACME, los seis escenarios de las decisiones D1–D9 y los tres del
cierre de desviaciones): [quickstart.md](specs/001-control-acceso-empresarial/quickstart.md).

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
apuntar a la base de desarrollo. Antes de lanzarlo, **detén cualquier `npm run dev` que esté ocupando el
puerto 5173** y exporta `MSSQL_DATABASE=EnterpriseAccessControl_E2E`; ambas omisiones producen fallos de
login que parecen defectos del producto y no lo son — ver la nota de
[Estado actual de las pruebas](#estado-actual-de-las-pruebas).

## Despliegue (producción)

El despliegue de producción usa [docker-compose.prod.yml](docker-compose.prod.yml), que se levanta con
`docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build --wait`. A diferencia del
compose de desarrollo, **ningún secreto tiene valor por defecto**: si falta una variable obligatoria,
compose se niega a arrancar.

1. **Requisitos**: host Linux con Docker Compose v2, licencia de SQL Server (la edición Developer no se
   permite en producción, por eso `MSSQL_PID` no tiene valor por defecto), .NET 10 SDK + `dotnet-ef` para
   aplicar migraciones, Node.js para compilar la SPA, proxy inverso propio con TLS.
2. **Variables de entorno obligatorias**: `MSSQL_SA_PASSWORD`, `MSSQL_PID`,
   `SQLSERVER_CONNECTION_STRING`, `JWT_SIGNING_KEY` (≥32 caracteres), `BOOTSTRAP_ADMIN_EMAIL` y
   `BOOTSTRAP_ADMIN_PASSWORD` (RF-078; la contraseña **nunca** se versiona y debe cumplir la política
   vigente). Deben definirse en un `.env.prod` fuera de control de versiones o en el gestor de secretos del
   host. Opcionales con valor por defecto: `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_ACCESS_TOKEN_MINUTOS` (60),
   `ZONA_HORARIA_TIME_ZONE_ID` (`America/Lima`, la zona global de respaldo de RF-080) y los `PASSWORD_*`.
3. **Exposición de puertos**: tanto SQL Server como la API publican **solo en `127.0.0.1`**. El tráfico
   externo entra exclusivamente por el proxy inverso, que termina TLS y sirve la SPA en el mismo origen.
4. **Política de contraseñas** (`PASSWORD_*`): longitud mínima 10, exige mayúscula, minúscula y dígito,
   bloqueo a los 5 intentos fallidos, expiración a los 90 días e historial de 5 contraseñas no
   reutilizables. Estos valores quedaron **ratificados como definitivos** por la decisión de negocio #1,
   cerrada el 2026-09-20, sin umbrales especiales para ningún usuario y sin mecanismo adicional de
   recuperación. *(Nota de desincronización: los comentarios de `docker-compose.prod.yml` todavía los
   califican de "PROVISIONALES pendientes de la Decisión #1"; ese texto quedó obsoleto con D9 y es una
   corrección pendiente en el archivo, no en este documento.)*
5. **Migraciones**: la API **no las aplica al arrancar** y **falla si el esquema no existe** (ver
   [Instrucciones de desarrollo](#instrucciones-de-desarrollo)); ejecuta `dotnet ef database update` contra
   SQL Server **antes** de levantar la API. El repositorio incluye 12 migraciones de esquema más una
   migración de semilla versionada con los catálogos maestros de Perú (RF-031).
6. **Primer usuario administrador**: en el primer arranque, si no existe ninguna asignación
   `GLOBAL_ADMINISTRATOR` vigente, la API crea el primer administrador tomando su correo de
   `Bootstrap:AdminEmail` y su contraseña de `Bootstrap:AdminPassword` (variable de entorno
   `Bootstrap__AdminPassword` donde no haya gestor de secretos). La contraseña **nunca** se versiona, debe
   cumplir la política de contraseñas vigente y su cambio es obligatorio en el primer inicio de sesión. La
   rutina es idempotente: reiniciar la API no crea un segundo administrador. **Ambas variables son
   obligatorias**: sin ellas la API no arranca, en lugar de levantar con un administrador adivinable
   (RF-078, decisión D2; cierre de la desviación D-5).
7. **SPA y proxy inverso**: `npm run build` genera `frontend/dist/`. **No hay imagen ni servicio de compose
   para la SPA**: el proxy inverso debe servir esos estáticos, reenviar `/api/` y `/health/` a la API, y
   devolver `index.html` para las rutas de cliente. La API no configura CORS, así que la SPA debe
   consumirla en el mismo origen.
8. **Verificación**: `GET /health/live` responde sin tocar dependencias (el proceso está vivo) y
   `GET /health/ready` incluye conectividad con SQL Server (`503` si no responde), de modo que una
   readiness fallida no provoque que el orquestador reinicie la API.
   [scripts/verificar-health-docker.sh](scripts/verificar-health-docker.sh) automatiza esa comprobación
   sobre el compose de desarrollo, incluida la caída y recuperación de SQL Server.

## Estado actual del proyecto

**Etapa 1 implementada y con su gate de requisitos cerrado.** Las dos auditorías del 2026-09-16
([`gate-cierre-etapa1-2026-09-16.html`](docs/auditorias/gate-cierre-etapa1-2026-09-16.html) y
[`auditoria-final-cierre-2026-09-16.html`](docs/auditorias/auditoria-final-cierre-2026-09-16.html))
concluyeron **"No congelable" / "No cerrada"**, verificando en ejecución y no solo en código. Todo lo que
bloqueaba ese veredicto está resuelto: las nueve decisiones de negocio se cerraron el 2026-09-20, su
implementación (T169–T228) se verificó en ejecución el mismo día
([`gate-cierre-etapa1-2026-09-20.html`](docs/auditorias/gate-cierre-etapa1-2026-09-20.html)), las cinco
desviaciones que ese gate dejó abiertas se cerraron con T229–T242, y el gate de calidad de requisitos se
cerró el 2026-09-21.

**1. Implementado y validado**

- Las 8 historias P1 y la Historia 9, con API y con interfaz. El motor de evaluación de acceso opera sus
  **15 pasos completos**, incluida la verificación de `Compañía.Estado` de los pasos 5 y 6 (RF-079).
- Modelo de datos completo: 22 entidades de dominio, 12 migraciones EF Core más la migración de semilla de
  catálogos maestros para Perú, **6 triggers SQL** de no-solapamiento, reglas temporales (vigencias
  obligatorias, contención, renovación), cascada de revocación automática, auditoría automática por
  interceptor y aislamiento de datos entre Compañías Principales.
- 242/242 tareas de `tasks.md` completadas, con trazabilidad RF/CS → tarea → evidencia de prueba.
- Los **72 endpoints** de los 10 contratos OpenAPI existen y responden: la paridad contrato↔código está
  verificada por las 246 pruebas de contrato, incluido el snapshot del documento OpenAPI generado.

**2. Cierre de Etapa 1 — decisiones D1 a D9 (T169 a T228)**

Formalizadas en `spec.md` (RF-074 a RF-081, CS-036 a CS-041) y construidas:

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

**3. Cierre de las desviaciones de implementación (T229 a T242)**

El gate del 2026-09-20 detectó cinco desviaciones entre lo construido y la fuente de verdad. Las cinco están
cerradas:

| # | Desviación | Cierre |
|---|---|---|
| D-1 | La renovación de una asignación de rol existía en el dominio pero no tenía endpoint, porque el contrato no la declaraba | `contracts/users.yaml` v2.1.0 declara `POST /api/usuarios/{id}/roles/{asignacionId}/renovar` → `204`, con cuerpo `{ "fechaHoraFin": … }`. Extiende la vigencia sin crear un registro nuevo y sin tocar rol, compañía ni fecha de inicio; exige fecha estrictamente posterior y asignación aún vigente. Operable desde el detalle de usuario con confirmación explícita (RF-075, RF-076, RF-077; UX-20) |
| D-2 | Dos tareas describían un defecto que ya no existía: los cinco servicios auditados sí delegaban el control de alcance | Cerrada como implementación válida, sin cambios de especificación. La brecha real era de **cobertura de regresión**, no de lógica: T237 y T238 la cierran |
| D-3 | La migración se generó después del código, invirtiendo el orden declarado en las tareas | Cerrada como implementación válida: `dotnet ef migrations add` exige que la solución compile. El contenido de la migración es el que las tareas describían |
| D-4 | La búsqueda por correo del listado de usuarios filtraba solo la página ya cargada | `GET /api/usuarios` acepta `texto`, que filtra por coincidencia parcial de correo sobre **todo el conjunto dentro del alcance autorizado y antes de paginar**. Sin coincidencias responde `200` con página vacía, nunca `404`: un correo ajeno y uno inexistente son indistinguibles (RF-077, UX-22) |
| D-5 | `docker-compose.yml` (desarrollo) traía una contraseña de arranque por defecto | Eliminada. `BOOTSTRAP_ADMIN_PASSWORD` es obligatoria también en desarrollo; compose falla explícitamente si no se provee |

Además, el hallazgo C2 —CS-037 declaraba siete recursos pero solo cinco estaban verificados— se cerró
**ampliando la cobertura, no recortando el criterio** (T242): para cada uno de los siete recursos, un
`COMPANY_ADMINISTRATOR` de la Principal A recibe `404` sobre un recurso real y de identificador conocido de
la Principal B, y un `GLOBAL_ADMINISTRATOR` alcanza recursos de ambas Principales en la misma prueba.

**4. Correcciones de la auditoría ya aplicadas**

- **Crítico (F-01), corregido**: `UsuarioService` aplica Resource Ownership en sus nueve operaciones. Un
  `COMPANY_ADMINISTRATOR` no lista ni modifica usuarios de otra compañía, no se autoeleva y no puede crear
  asignaciones `GLOBAL_ADMINISTRATOR`; una lectura fuera de alcance responde `404` (CS-036, CS-037).
- **F-02, corregido**: el alcance efectivo se deriva del rol vigente, de modo que el control alcanza por
  igual a los servicios que lo delegaban en `CompaniaService` y a los de histórico de personas, cuyo
  alcance sobre una `Persona` se resuelve ahora por unión de pertenencia y contexto operativo (RF-077).
- Una compañía `INACTIVA` deniega el acceso de forma dinámica y reversible, sin cascada de escritura
  (RF-079).

**5. Etapa 2 / futuro**

- **Consultas transversales** (RF-067 a RF-069, CS-032): auditoría agregada filtrable, históricos
  transversales e indicadores operativos del Dashboard. Diferidas explícitamente por la decisión D8, sin
  tareas ni código en este cierre. La ruta `/` del frontend muestra un marcador de Dashboard pendiente.
- **Revocación en cascada al finalizar una `RelaciónContratistaPrincipal`**: sigue abierta como decisión de
  negocio. Hoy ese caso está protegido únicamente por la re-validación dinámica de la evaluación de acceso
  (RF-059/RF-065), sin cascada de escritura equivalente a RF-061.
- **Integración continua**: no hay `.github/workflows`; las cinco suites se ejecutan localmente.

## Decisiones de la auditoría: todas resueltas

La auditoría de cierre agrupó 23 hallazgos en 9 decisiones (D1–D9) y una matriz de **16 preguntas de
negocio**. Las 16 quedaron respondidas el 2026-09-20, las nueve decisiones están formalizadas en `spec.md`
(RF-074 a RF-081, CS-036 a CS-041) y **las diez Decisiones Pendientes del spec están cerradas**.

| Decisión | Tema | Resolución |
|---|---|---|
| D1 | Modelo de administración de usuarios | RBAC con catálogo cerrado de dos roles, alcance GLOBAL o por compañía, asignaciones con vigencia auditable (RF-074 a RF-077) |
| D2 | Alta del primer administrador | Arranque automático idempotente con credenciales por configuración segura y cambio forzado en el primer login (RF-078) |
| D3 | Aislamiento por alcance | Cadena de autorización con verificación del recurso concreto; una `Persona` está en alcance por su compañía de pertenencia o por un contexto operativo vigente (RF-077) |
| D4 | Inactivación de una Compañía | Denegación por evaluación dinámica, sin cascada de escritura, reversible (RF-079; cierra la Decisión Pendiente #6 para este caso) |
| D5 | Calendario de vigencias | Zona horaria IANA por Compañía Principal, con zona global de respaldo; UTC siempre persistido (RF-080) |
| D6 | Reclasificar el tipo de una Compañía | Rechazo si hay dependencias incompatibles, simétrico en ambas direcciones, sin cascada (RF-081) |
| D7 | Interfaz de Historia 5 | Se corrige y completa dentro de Etapa 1, no se traslada |
| D8 | Consultas transversales (RF-067 a RF-069, CS-032) | Diferidas a Etapa 2, anotadas en `spec.md`, sin tareas de baseline |
| D9 | Heredadas #1, #3 y #7 | Política de contraseñas actual ratificada; sin retención ni purga en el baseline; Historia 9 se mantiene en P2 |

El gate de calidad de requisitos del 2026-09-21 revisó los 45 ítems de
[`baseline-gate.md`](specs/001-control-acceso-empresarial/checklists/baseline-gate.md) y encontró 14
hallazgos, **todos documentales**: la implementación era correcta pero la especificación no la obligaba. Se
corrigieron sin tocar código de producción — entre otros, CS-037 pasó de "cualquier recurso" a una
enumeración cerrada de siete, RF-077 ganó reglas explícitas de orden (filtros → total → paginación),
semántica de coincidencia parcial, resultado vacío y ausencia de efectos al denegar, y `spec.md` incorporó
la nota que desambigua `D1`–`D9` de `D-1`–`D-5`.

Detalle de cada decisión, sus alternativas y su análisis de impacto:
[`decisiones-etapa1-2026-09-16.html`](docs/auditorias/decisiones-etapa1-2026-09-16.html) (narrativo),
[`clasificacion-hallazgos-2026-09-16.html`](docs/auditorias/clasificacion-hallazgos-2026-09-16.html)
(tabular, con los 23 hallazgos F-01 a F-23 y la matriz),
[`analisis-consistencia-2026-09-20.html`](docs/auditorias/analisis-consistencia-2026-09-20.html) y
[`auditoria-decision-d2-d5-2026-09-20.html`](docs/auditorias/auditoria-decision-d2-d5-2026-09-20.html).
**Las páginas del 16/09 conservan a propósito el estado previo a las respuestas**, como registro histórico.

## Próximos pasos para congelar el baseline

De la secuencia acordada en la auditoría de cierre, los cinco primeros pasos están completados:

1. ~~**Sesión de decisiones de negocio** — responder las 16 preguntas pendientes.~~ **Completado el
   2026-09-20**: las 16 respondidas y las nueve decisiones formalizadas en `spec.md` (RF-074 a RF-081).
2. ~~**Corregir el perímetro de autorización** (F-01, F-02) aplicando el modelo de administración de
   usuarios (D1) y el aislamiento por alcance (D3), con pruebas de integración por operación.~~
   **Completado** (T169–T228, T237–T239, T242).
3. ~~**Correcciones técnicas que no requieren decisión.**~~ **Completado** dentro del mismo bloque.
4. ~~**Sincronizar la documentación** con las decisiones tomadas y las correcciones aplicadas.~~
   **Completado el 2026-09-21** con el gate de calidad de requisitos: 45/45 ítems satisfechos y 14
   correcciones documentales aplicadas.
5. ~~**Regresión completa** de las cinco suites.~~ **Completada** el 2026-09-20 y re-ejecutada en este
   repositorio el 2026-09-23 (ver [Estado actual de las pruebas](#estado-actual-de-las-pruebas)).

Queda pendiente. Salvo el primer punto, es trabajo de gobierno más que de construcción:

6. **Hacer reproducible la suite E2E desde una base limpia.** `UX-22: la búsqueda encuentra a un usuario
   que no está en la primera página` solo pasa si la base acumuló más de 20 usuarios en corridas previas
   (ver [Estado actual de las pruebas](#estado-actual-de-las-pruebas)). Es el único punto de esta lista que
   exige tocar código, y bloquea el paso 9: una prueba que depende del historial no sirve en CI, donde cada
   ejecución arranca en limpio. ← **el más urgente**
7. **Repetir formalmente el gate de cierre** contra el código con T229–T242 incluidas, y registrar el acta
   en `docs/auditorias/`. El último gate de ejecución registrado (`gate-cierre-etapa1-2026-09-20.html`)
   cubre T169–T228; el bloque posterior está implementado y probado, pero su acta de gate no se ha emitido.
8. **Congelar la línea base de Etapa 1** (tag/rama de release) una vez aceptada esa acta.
9. **Configurar integración continua** para que las cinco suites dejen de depender de ejecuciones locales.
10. **Abrir formalmente la Etapa 2**: consultas transversales (RF-067 a RF-069, CS-032) y la decisión de
    negocio todavía abierta sobre la cascada al finalizar una `RelaciónContratistaPrincipal`.

Correcciones menores detectadas al homologar este documento con el código, ninguna funcional:

- `docker-compose.prod.yml` califica la política de contraseñas de "PROVISIONAL pendiente de la Decisión
  #1", que D9 cerró el 2026-09-20.
- El comentario de `ConfiguracionExtensions.ConClusterPorCreatedAt` dice cubrir "las 6 entidades de
  histórico de alto volumen"; el modelo la aplica a 2.
- `plan.md` (Constitution Check, Principio I), `data-model.md` (diagrama de relaciones) y `research.md` §3
  todavía nombran `AlcanceUsuarioCompañía` sin marca de reemplazo, aunque D1 la sustituyó por
  `AsignaciónRolAdministrativo`.
- `plan.md` declara "TypeScript 5.6+"; lo instalado es TypeScript 6.
