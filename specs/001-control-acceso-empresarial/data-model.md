# Fase 1 — Modelo de Datos: Control de Acceso Empresarial

> **Nota de revisión (Sesión 2026-09-14, corrección Compañía Principal/Contratista)**: este documento fue
> revisado para incorporar la clasificación PRINCIPAL_MANDANTE/CONTRATISTA de `Compañía` (RF-042), el
> aislamiento de `UnidadOrganizativa` y `ÁreaAcceso` por Compañía Principal (RF-043 a RF-046), y la entidad
> de enlace `CompañíaPrincipalUnidadOrganizativaRaiz`. Ningún RF ni entidad previamente válidos fue
> eliminado; la sección "Nota de alcance" que antes dejaba pendiente esta decisión bajo `UnidadOrganizativa`
> queda resuelta y reemplazada más abajo.
>
> **Nota de revisión (Sesión 2026-09-14, corrección Contexto Operativo)**: segunda revisión. Se agregan las
> entidades `RelaciónContratistaPrincipal` y `ContextoOperativoPersonaPrincipal` (RF-051, RF-052). Se
> modifica `AsignaciónPersonaUnidadOrganizativa` (agrega `ContextoOperativoId`, rescopea su exclusividad de
> `PersonaId` a `ContextoOperativoId` — RF-015, RF-055). Se modifica `AsignaciónCredencial` (retira
> `CódigoFisico`, agrega `CompañíaPrincipalId`, rescopea su exclusividad de `PersonaId` a
> `(PersonaId, CompañíaPrincipalId)` — RF-056, RF-057, RF-058). Ver research.md §12 (decisión revertida) y
> §13 (nuevas entidades) para el razonamiento completo.
>
> **Nota de revisión (Sesión 2026-09-14, corrección Revocación Automática)**: tercera revisión. No se
> agregan entidades nuevas. Se agregan campos `Estado`/`MotivoFin` a `AsignaciónPersonaCompañía`,
> `ContextoOperativoPersonaPrincipal` y `AsignaciónPersonaUnidadOrganizativa`; se agrega
> `RevocadoPorPertenenciaId` (FK → `AsignaciónPersonaCompañía`) a estas tres más `AsignaciónCredencial`; se
> agrega el valor `REVOCADA` al `Estado` de `AsignaciónCredencial`. El cierre de una
> `AsignaciónPersonaCompañía` ahora dispara una revocación en cascada real (escritura), no solo la
> re-validación dinámica de la evaluación de acceso (que se conserva como defensa adicional). Ver
> research.md §14 para el diseño completo.
>
> **Nota de revisión (Sesión 2026-09-14, corrección Modelo de Cardinalidad Definitivo)**: cuarta revisión,
> de redacción únicamente — **ningún campo, entidad ni restricción cambia**. Se corrige la nota de
> `ContextoOperativoPersonaPrincipal` sobre revocación en cascada, que estaba redactada de forma ambigua y
> podía sugerir que RF-014 limitaba el número de contextos simultáneos de una persona. RF-014 solo limita
> `AsignaciónPersonaCompañía`; `ContextoOperativoPersonaPrincipal` sigue permitiendo, sin límite, un contexto
> vigente por cada Compañía Principal distinta (RF-052, CS-013, CS-030) — la restricción de exclusividad
> `(PersonaId, CompañíaPrincipalId)` ya documentada nunca cambió y siempre lo permitió *(esta nota se
> escribió bajo el diseño de PostgreSQL — `EXCLUDE USING gist`; ver la nota de revisión siguiente para el
> mecanismo vigente en SQL Server, que preserva exactamente la misma clave de partición y garantía)*.
>
> **Nota de revisión (Sesión 2026-09-14, decisión arquitectónica — Stack Tecnológico Oficial)**: quinta
> revisión. Motor de base de datos ratificado como SQL Server (reemplaza PostgreSQL, que era un supuesto de
> `spec.md`, nunca una decisión ratificada). Cambios mecánicos en este documento: (a) todas las columnas de
> fecha/hora pasan de `timestamptz` a `datetime2(3)`; (b) las 5 restricciones `EXCLUDE USING gist` (Postgres,
> sin equivalente en SQL Server — contradicción real, no traducible 1:1) se reemplazan por un trigger
> `AFTER INSERT, UPDATE` por tabla, con la misma clave de partición documentada en cada entidad (research.md
> §5 tiene el detalle completo y la plantilla SQL del trigger). Ninguna entidad, campo de negocio,
> cardinalidad ni regla de validación cambia — el cambio es exclusivamente de mecanismo de persistencia.
>
> **Nota de revisión (Sesión 2026-09-14, auditoría de consistencia RF-066 — vigencia temporal de
> `AsignaciónCredencial`)**: sexta revisión. Corrige `AsignaciónCredencial.FechaHoraFin`, que estaba
> incorrectamente descrito como "`null` mientras esté `ASIGNADO`" — esa restricción no correspondía a
> ninguna regla de negocio y hacía inalcanzable el escenario que RF-066/RF-070 (spec.md) exigen cubrir (una
> credencial `ASIGNADO` temporalmente expirada). `FechaHoraFin` ahora puede tener valor (incluso futuro)
> mientras `Estado = ASIGNADO`; la vigencia efectiva para autorización se determina siempre comparando
> `FechaHoraInicio`/`FechaHoraFin` contra la fecha evaluada, nunca por `Estado` en aislamiento — mismo
> patrón ya vigente para las otras tres entidades revocables (RF-063). También se corrige la frase de
> "Validaciones clave" de esta entidad, que aún afirmaba que la credencial "nunca" determina el acceso por
> sí misma — contradecía RF-066 (Sesión anterior) y había quedado sin actualizar en este documento. Ningún
> campo nuevo, ninguna entidad nueva, ninguna migración estructural — solo corrección de la descripción de
> un campo ya existente y de una frase ya superada. Ver research.md §24 (nueva) y spec.md, Clarificaciones,
> para el razonamiento completo. ~~**Observación sin resolver, fuera de alcance de esta revisión**: las
> otras tres entidades revocables... no se evaluó si tienen la misma necesidad — ver spec.md, Decisiones
> Pendientes #8.~~ **[RESUELTA Y SUPERADA — ver la revisión siguiente]**
>
> **Nota de revisión (Sesión 2026-09-14, "vigencia temporal jerárquica")**: séptima revisión, la de mayor
> alcance sobre esta sección. Cierra la Decisión Pendiente #8 (arriba) con una decisión más amplia de lo que
> esa pregunta original planteaba: `FechaHoraFin` deja de ser nullable — con "`null` = vigencia indefinida"
> como opción válida — para **las seis entidades** temporales vinculadas a una persona, no solo para las
> cuatro de la cascada de RF-061. Cambian: `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`,
> `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial` (revierte parcialmente la sexta revisión: ya
> no basta con "puede tener valor mientras ASIGNADO", ahora es estrictamente obligatorio, sin `null`),
> `AsignaciónTipoPersona`, y `PermisoAcceso.FechaHoraFinVigencia` (para los tres alcances — hallazgo
> adicional: RF-021 ya exigía esto desde el spec original, nunca reflejado correctamente aquí). Se agrega la
> regla de **contención temporal** (RF-072, nueva): `ContextoOperativoPersonaPrincipal`,
> `AsignaciónPersonaUnidadOrganizativa` y `AsignaciónCredencial` — las tres que RF-061 revoca por
> dependencia — DEBEN quedar contenidas dentro de la vigencia de la `AsignaciónPersonaCompañía` vigente al
> momento de su creación; `AsignaciónTipoPersona` y `PermisoAcceso` quedan explícitamente **fuera** de la
> contención (el modelo de dominio no las establece como dependientes de la pertenencia). RF-048 se
> reescribe: su afirmación de que "no existe validación cruzada" queda reemplazada. Ninguna entidad nueva,
> ninguna cardinalidad ni regla de revocación/cascada cambia (RF-061 a RF-065 intactas). Ver spec.md
> Clarifications, research.md §7 (paso 6 simplificado), §24 (corregido) y §25 (nueva, diseño de la
> contención) para el detalle completo. ~~**Decisión pendiente nueva, no resuelta aquí**: si
> `AsignaciónPersonaCompañía.FechaHoraFin` puede extenderse hacia adelante tras su creación (renovación) —
> ver spec.md, Decisiones Pendientes #9.~~ **[RESUELTA — ver la revisión siguiente]**
>
> **Nota de revisión (Sesión 2026-09-14, "renovación de AsignaciónPersonaCompañía")**: octava revisión.
> Cierra la Decisión Pendiente #9 (arriba): se agrega una operación explícita de **renovación** (RF-073) a
> la sección `AsignaciónPersonaCompañía` — extiende `FechaHoraFin` hacia una fecha posterior, solo si
> `Estado = ACTIVA`, sin tocar `FechaHoraInicio`/`Estado`/`MotivoFin`, sin crear ni modificar ninguna
> asociación dependiente existente, y sin disparar la cascada de RF-061. Es la única operación que puede
> extender `FechaHoraFin`; el cierre (reemplazo/cese) sigue siendo la única que puede acortarla — ambas
> siguen siendo mecanismos distintos, sin superposición. Ninguna entidad nueva, ninguna migración
> estructural, ningún campo nuevo. Ver research.md §26 (nuevo) para el rationale completo. ~~**Nueva
> decisión pendiente, no resuelta aquí**: si la renovación aplica también a una pertenencia ya expirada
> dinámicamente (`Estado = ACTIVA` pero `FechaHoraFin` ya pasada) — spec.md, Decisiones Pendientes #10.~~
> **[RESUELTA — ver la revisión siguiente]**
>
> **Nota de revisión (Sesión 2026-09-14, "cierre Decisión Pendiente #10")**: novena revisión, acotada.
> Cierra la Decisión Pendiente #10: la renovación (RF-073) exige, además de `Estado = ACTIVA`, que la
> pertenencia siga **vigente dinámicamente** en el momento de renovar (`fecha actual <= FechaHoraFin` ya
> declarada) — nunca puede usarse para puentear retroactivamente un vacío temporal ya transcurrido. Una
> pertenencia `ACTIVA` pero dinámicamente expirada no es renovable, igual que una `FINALIZADA`; ambos casos
> requieren una nueva `AsignaciónPersonaCompañía`, ya soportado sin cambios (no hay solapamiento posible).
> Se amplía el punto (a) de la regla de renovación en la sección `AsignaciónPersonaCompañía`. Ningún campo,
> entidad ni migración nueva.
>
> **Nota de revisión (Sesión 2026-09-20, cierre de Etapa 1 — Decisiones D1 a D9)**: décima revisión. Cierra
> las 16 preguntas de la matriz de auditoría de cierre de Etapa 1 (research.md, sección "Cierre de Etapa 1 —
> Decisiones D1 a D9"). Cambios a este documento: `AlcanceUsuarioCompañía` queda **reemplazada** por
> [`AsignaciónRolAdministrativo`](#asignaciónroladministrativo) (D1 — modelo RBAC con `Rol`
> `GLOBAL_ADMINISTRATOR`/`COMPANY_ADMINISTRATOR`, alcance `GLOBAL`/`COMPANY`, vigencia obligatoria); se agrega
> el campo `ZonaHorariaIana` a [`Compañía`](#compañía) (D5); se agrega una validación de dependientes al
> cambiar `TipoCompañía` en la misma sección (D6). D2 (bootstrap), D3 (aislamiento por alcance), D4
> (inactivación de Compañía) y D7 (interfaz de Historia 5) no requieren ningún cambio de modelo de datos —
> ver research.md §28-30 y §33. D8 y D9 no requieren cambio alguno en este documento.

Convenciones aplicadas a todas las entidades (no repetidas por entidad):

- **PK**: `Id` (`Guid`, generado en Aplicación vía `Guid.CreateVersion7()` — ver research.md §1), inmutable,
  no editable ni visible en pantallas normales (Principio II, RF-013).
- **Auditoría**: `CreatedAt` (`datetime2(3)`), `UpdatedAt` (`datetime2(3)`), `CreatedById` (`Guid` → `Usuario`),
  `UpdatedById` (`Guid` → `Usuario`). Estampados exclusivamente por el `SaveChangesInterceptor` (research.md
  §6); nunca presentes en DTOs de entrada ni editables en UI (Principio III, RF-026, RF-027).
- **Fechas/horas**: persistidas en UTC (`datetime2(3)`); conversión a `America/Lima` solo en evaluación de
  bloques horarios y presentación (Principio IV).
- Salvo indicación contraria, todos los campos de negocio listados son obligatorios (RF-028).

## Índice de entidades

1. [Usuario](#usuario)
2. [HistorialContraseña](#historialcontraseña)
3. [AsignaciónRolAdministrativo](#asignaciónroladministrativo)
4. [Compañía](#compañía)
5. [RelaciónContratistaPrincipal](#relacióncontratistaprincipal)
6. [UnidadOrganizativa](#unidadorganizativa)
7. [CompañíaPrincipalUnidadOrganizativaRaiz](#companiaprincipalunidadorganizativaraiz)
8. [TipoPersona](#tipopersona)
9. [Persona](#persona)
10. [AsignaciónTipoPersona](#asignacióntipopersona)
11. [AsignaciónPersonaCompañía](#asignaciónpersonacompañía)
12. [ContextoOperativoPersonaPrincipal](#contextooperativopersonaprincipal)
13. [AsignaciónPersonaUnidadOrganizativa](#asignaciónpersonaunidadorganizativa)
14. [ÁreaAcceso](#áreaacceso)
15. [ÁreaAccesoTipoPersona](#áreaaccesotipopersona)
16. [PermisoAcceso](#permisoacceso)
17. [BloqueHorarioPermiso](#bloquehorariopermiso)
18. [TipoCredencial](#tipocredencial)
19. [AsignaciónCredencial](#asignacióncredencial)
20. [TipoDocumento](#tipodocumento)
21. [TipoSangre](#tiposangre)
22. [Género](#género)

---

## Usuario

Usuario autorizado a iniciar sesión y administrar dentro de su alcance de compañías (Historia 1).

| Campo | Tipo | Reglas |
|---|---|---|
| Correo | string(256) | Único (case-insensitive); formato de correo válido |
| PasswordHash | string | Nunca texto plano (RF-003); producido por `PasswordHasher<T>` |
| Estado | enum: `ACTIVO`, `INACTIVO`, `BLOQUEADO` | RF-002 |
| RequiereCambioPassword | bool | Fuerza cambio en el siguiente login (Historia 1, criterio 3) |
| IntentosFallidosConsecutivos | int | Se resetea a 0 en login exitoso; ≥ umbral configurado ⇒ `Estado = BLOQUEADO` |
| FechaUltimoCambioPassword | datetime2(3) | Usado para calcular expiración (research.md §2) |

**Relaciones**: 1—N `AsignaciónRolAdministrativo`; 1—N `HistorialContraseña`; referenciado por
`CreatedById`/`UpdatedById` de toda entidad auditable (nullable — el `Usuario` creado por la rutina de
bootstrap, D2, tiene `CreatedById = NULL`: creado por el sistema, no por otro `Usuario`).

**Transiciones de estado**: `ACTIVO ⇄ INACTIVO` (administrativo) · `ACTIVO → BLOQUEADO` (intentos fallidos,
automático) · `BLOQUEADO → ACTIVO` (desbloqueo administrativo). Un usuario `INACTIVO` o `BLOQUEADO` no puede
iniciar sesión (Historia 1, criterio 2).

**Validaciones clave**: login exitoso requiere `Estado = ACTIVO`; toda operación protegida requiere sesión
válida (RF-034).

## HistorialContraseña

Histórico de hashes de contraseña por usuario, para impedir reutilización (research.md §2, RF-003).

| Campo | Tipo | Reglas |
|---|---|---|
| UsuarioId | Guid (FK → Usuario) | — |
| PasswordHash | string | No texto plano |

**Relaciones**: N—1 `Usuario`. `CreatedAt` (heredado de auditoría) representa la fecha del cambio.

**Validaciones clave**: al establecer una nueva contraseña, se rechaza si coincide con cualquiera de las
últimas N (configurable, valor de trabajo = 5) entradas de este historial para el mismo `UsuarioId`.

## AsignaciónRolAdministrativo

**Reemplaza a `AlcanceUsuarioCompañía`** (Sesión 2026-09-20, cierre de Etapa 1, D1 — modelo RBAC de
administración de usuarios; research.md §27). Representa la asignación temporal y auditable de un rol
administrativo a un `Usuario`, con alcance `GLOBAL` (todo el sistema) o `COMPANY` (una compañía específica).
Distinta, y conceptualmente separada, de la relación operacional Persona→Compañía→UnidadOrganizativa del
dominio de control de acceso físico (RF-050, sin cambios): que un `Usuario` tenga una asignación
administrativa con una compañía no implica ninguna pertenencia empresarial de ninguna `Persona`.

| Campo | Tipo | Reglas |
|---|---|---|
| UsuarioId | Guid (FK → Usuario) | — |
| Rol | enum: `GLOBAL_ADMINISTRATOR`, `COMPANY_ADMINISTRATOR` | Catálogo **cerrado** — agregar un rol nuevo exige modificar el modelo de autorización, no es un dato maestro versionado (mismo patrón que `EstadoUsuario`/`TipoCompañía`) |
| CompañíaId | Guid? (FK → Compañía, nullable) | **Regla fundamental**: `NULL` si y solo si `Rol = GLOBAL_ADMINISTRATOR`; obligatoria y válida si `Rol = COMPANY_ADMINISTRATOR` |
| FechaHoraInicio | datetime2(3) | Obligatoria, sin excepción |
| FechaHoraFin | datetime2(3) | **NOT NULL** desde la creación — mismo patrón que RF-071, aplicado aquí por primera vez a una entidad ligada a `Usuario` en vez de a `Persona`. **Única excepción documentada**: la asignación `GLOBAL_ADMINISTRATOR` creada por la rutina de bootstrap (D2, research.md §28) usa `FechaHoraFin = MAX_VALIDITY_DATE` (`2999-12-31T23:59:59Z`) — excepción explícita y acotada exclusivamente a esa asignación, nunca generalizable a otra fila de esta entidad ni a ninguna entidad ligada a `Persona` |

**Relaciones**: N—1 `Usuario`; N—1 `Compañía` (solo cuando `Rol = COMPANY_ADMINISTRATOR`).

**Restricción de base de datos**: trigger `AFTER INSERT, UPDATE` particionado por `(UsuarioId, CompañíaId)`,
aplicado **únicamente** a filas `Rol = COMPANY_ADMINISTRATOR` — impide asignaciones `COMPANY_ADMINISTRATOR`
solapadas para el mismo par (Usuario, Compañía); secuenciales sin solapamiento sí se permiten. Un usuario
puede tener varias asignaciones `COMPANY_ADMINISTRATOR` vigentes simultáneas si son de compañías distintas.
`GLOBAL_ADMINISTRATOR` queda **exento** de esta partición (no tiene `CompañíaId`); pueden coexistir varias
asignaciones `GLOBAL_ADMINISTRATOR`, de uno o varios usuarios, sin restricción de solapamiento entre ellas.

**Renovación**: sigue las mismas reglas ya establecidas para `AsignaciónPersonaCompañía` (RF-073): solo
extiende `FechaHoraFin` hacia una fecha posterior, solo mientras la asignación siga vigente dinámicamente
(`fecha actual <= FechaHoraFin` ya declarada) — nunca puentea un vacío temporal ya transcurrido.

**Validaciones clave**: toda consulta/operación administrativa DEBE resolver el alcance efectivo del usuario
autenticado a partir de sus asignaciones vigentes de esta entidad (research.md §27, §29) — `GLOBAL_ADMINISTRATOR`
vigente ⇒ alcance sobre todas las compañías; en caso contrario, alcance limitado a las `CompañíaId` de sus
asignaciones `COMPANY_ADMINISTRATOR` vigentes. Un `COMPANY_ADMINISTRATOR` NO puede crear, asignar ni elevar
ninguna asignación de rol fuera de su propio nivel y compañía (no puede asignar `GLOBAL_ADMINISTRATOR`, no
puede elevar su propio rol, no puede asignar `COMPANY_ADMINISTRATOR` para una compañía distinta de la suya);
sí puede crear nuevas asignaciones `COMPANY_ADMINISTRATOR` para su propia compañía. Crear o modificar
cualquier asignación de rol fuera de esos límites requiere un `GLOBAL_ADMINISTRATOR` vigente, o el mecanismo
de bootstrap (D2) para la primera asignación del sistema.

## Compañía

| Campo | Tipo | Reglas |
|---|---|---|
| Nombre | string(200) | RF-006 |
| TipoDocumentoId | Guid (FK → TipoDocumento) | Documento de identificación de la compañía (p. ej. RUC) |
| NumeroDocumento | string(20) | — |
| TipoCompañía | enum: `PRINCIPAL_MANDANTE`, `CONTRATISTA` | RF-042. Clasifica la compañía; determina si puede
  poseer unidades organizativas propias (solo `PRINCIPAL_MANDANTE`, vía la entidad de enlace de raíz) y
  áreas de acceso propias (RF-045, RF-046) |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-006, RF-032. Consultado dinámicamente por `EvaluadorDeAcceso`
  desde la Sesión 2026-09-20 (D4, research.md §30): una Compañía `INACTIVO` deniega el acceso de forma
  inmediata y reversible en la evaluación, sin cascada de escritura sobre ningún dependiente |
| ZonaHorariaIana | string | **Nuevo (Sesión 2026-09-20, D5, research.md §31)**. Identificador IANA (p. ej.
  `America/Lima`). Obligatorio y validado (zona reconocida) cuando `TipoCompañía = PRINCIPAL_MANDANTE`; sin
  uso funcional para `CONTRATISTA` (no poseen áreas, contextos ni bloques horarios propios). Rige la
  interpretación/presentación de fechas y la evaluación de bloques horarios de los permisos de las áreas de
  esta Principal. Cambiarla nunca reinterpreta instantes UTC ya persistidos; sí cambia la representación
  local en consultas/presentaciones futuras (sin versionado histórico de la zona) |

**Relaciones**: 1—N `AsignaciónRolAdministrativo` (cuando `Rol = COMPANY_ADMINISTRATOR`); 1—N
`AsignaciónPersonaCompañía`; 1—N `PermisoAcceso` (cuando `Alcance = COMPAÑÍA`); si
`TipoCompañía = PRINCIPAL_MANDANTE`: 0—N `CompañíaPrincipalUnidadOrganizativaRaiz`
(sus árboles de unidades organizativas), 1—N `ÁreaAcceso` (sus áreas de acceso), 0—N
`RelaciónContratistaPrincipal` (como Principal) y 0—N `ContextoOperativoPersonaPrincipal`; si
`TipoCompañía = CONTRATISTA`: 0—N `RelaciónContratistaPrincipal` (como Contratista).

**Validaciones clave**: único `(TipoDocumentoId, NumeroDocumento)` (consistente con la regla de unicidad de
documento aplicada a `Persona`, RF-041). `Estado = INACTIVO` ⇒ no puede recibir nuevas asignaciones activas de
persona ni nuevos permisos activos (RF-032, Historia 2 criterio 6). El sistema admite múltiples compañías con
`TipoCompañía = PRINCIPAL_MANDANTE` simultáneamente; no debe asumirse una única Principal global (RF-043).

**Cambio de `TipoCompañía` (Sesión 2026-09-20, D6, research.md §32)**: rechazado con `409 Conflict`
(`CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS`) si la compañía tiene alguna dependencia incompatible con el tipo
destino: áreas de acceso propias, raíces de unidad organizativa, `RelaciónContratistaPrincipal` vigentes
(como Contratista o como Principal — simétrico en ambas direcciones), contextos operativos, o credenciales.
Nunca se resuelven automáticamente en cascada; deben cerrarse/resolverse explícitamente antes del cambio.

## RelaciónContratistaPrincipal

Relación explícita y vigente entre una Compañía CONTRATISTA y una Compañía PRINCIPAL_MANDANTE (RF-051,
Historia 2). Una misma Contratista puede tener relaciones vigentes simultáneas con varias Principales
distintas, y una misma Principal puede tener relaciones vigentes simultáneas con varias Contratistas
distintas. Determina qué Principales puede seleccionar el usuario al abrir un `ContextoOperativoPersonaPrincipal`
para una persona de esa Contratista (Historia 5, Caso B).

| Campo | Tipo | Reglas |
|---|---|---|
| CompañíaContratistaId | Guid (FK → Compañía) | DEBE referenciar una compañía con `TipoCompañía = CONTRATISTA` |
| CompañíaPrincipalId | Guid (FK → Compañía) | DEBE referenciar una compañía con `TipoCompañía = PRINCIPAL_MANDANTE` |
| FechaHoraInicio | datetime2(3) | Normalizada a 00:00 del día de inicio (RF-016, mismo patrón que asignaciones de persona) |
| FechaHoraFin | datetime2(3)? (nullable) | Normalizada a 23:59 del último día cuando se fija; `null` mientras esté vigente |

**Relaciones**: N—1 `Compañía` (como Contratista); N—1 `Compañía` (como Principal).

**Restricción de base de datos**: trigger `AFTER INSERT, UPDATE` particionado por
`(CompañíaContratistaId, CompañíaPrincipalId)` — impide solapamiento entre relaciones del mismo par
Contratista-Principal, pero permite relaciones simultáneas de la misma Contratista con Principales distintas
(research.md §5, §13; plantilla SQL del trigger en research.md §5).

**Validaciones clave**: rechazada si `CompañíaContratistaId` no es `CONTRATISTA` o `CompañíaPrincipalId` no
es `PRINCIPAL_MANDANTE` (RF-051). Períodos inválidos (`FechaHoraFin ≤ FechaHoraInicio`) se rechazan
(RF-039). Las operaciones de mantenimiento sobre esta relación quedan limitadas al alcance de compañías del
usuario autenticado, evaluado contra `CompañíaPrincipalId` (RF-060) — gestionar la relación es una decisión
del lado de la Principal.

## UnidadOrganizativa

Jerarquía padre-hijo que representa **exclusivamente** la estructura organizativa de una Compañía Principal
(RF-007, RF-045). **Esta entidad NO tiene ninguna columna ni relación (FK) directa hacia `Compañía`** — es
una restricción explícita de negocio (RF-044). Su pertenencia a una Compañía Principal se resuelve
indirectamente mediante la entidad de enlace [`CompañíaPrincipalUnidadOrganizativaRaiz`](#companiaprincipalunidadorganizativaraiz),
descrita en la siguiente sección — ver research.md §4 para el razonamiento completo y las alternativas
descartadas.

| Campo | Tipo | Reglas |
|---|---|---|
| Nombre | string(200) | RF-007 |
| UnidadSuperiorId | Guid? (FK → UnidadOrganizativa, nullable) | `null` ⇒ nodo raíz (Historia 2, criterio 1) |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-007, RF-032 |

**Relaciones**: autorreferencia N—1 (`UnidadSuperiorId`) / 1—N (hijos); 1—N `AsignaciónPersonaUnidadOrganizativa`;
1—N `PermisoAcceso` (cuando `Alcance = UNIDAD_ORGANIZATIVA`); **ninguna** relación directa hacia `Compañía`
(RF-044) — un nodo raíz participa como `UnidadOrganizativaRaízId` en, como máximo, una fila de
`CompañíaPrincipalUnidadOrganizativaRaiz`.

**Validaciones clave**: `UnidadSuperiorId` no puede crear un ciclo directo ni indirecto (RF-038, Principio V,
research.md §4) — validado vía CTE recursivo antes de confirmar creación/reubicación. `Estado = INACTIVO` ⇒
no puede recibir nuevas asignaciones activas (RF-032). Toda Compañía Contratista NO puede tener unidades
organizativas propias (RF-045): esto se aplica indirectamente, ya que solo se permite crear una fila en
`CompañíaPrincipalUnidadOrganizativaRaiz` cuando `Compañía.TipoCompañía = PRINCIPAL_MANDANTE`.

## CompañíaPrincipalUnidadOrganizativaRaiz

Entidad de enlace que vincula **únicamente** los nodos raíz de `UnidadOrganizativa`
(`UnidadSuperiorId IS NULL`) con la Compañía Principal que los posee (RF-045). Resuelve el aislamiento entre
los árboles organizativos de distintas Compañías Principales sin introducir una relación directa en
`UnidadOrganizativa` (RF-044) — decisión tomada explícitamente para reconciliar RF-043 (múltiples
Principales, cada una con su propio contexto organizacional) con la restricción de negocio de no modelar
`UnidadOrganizativa → Compañía` (research.md §4).

| Campo | Tipo | Reglas |
|---|---|---|
| CompañíaId | Guid (FK → Compañía) | DEBE referenciar una compañía con `TipoCompañía = PRINCIPAL_MANDANTE` |
| UnidadOrganizativaRaízId | Guid (FK → UnidadOrganizativa) | DEBE referenciar un nodo con `UnidadSuperiorId = null`; único (una raíz pertenece a una sola Principal) |

**Relaciones**: N—1 `Compañía` (solo `PRINCIPAL_MANDANTE`); 1—1 `UnidadOrganizativa` (solo nodos raíz).

**Validaciones clave**: rechazado si `Compañía.TipoCompañía = CONTRATISTA` (RF-045, Historia 2 criterio 5);
rechazado si `UnidadOrganizativaRaízId` no es un nodo raíz; único por `UnidadOrganizativaRaízId` (una unidad
raíz pertenece exactamente a una Compañía Principal). La Compañía Principal propietaria de un nodo **no
raíz** se resuelve en tiempo de consulta recorriendo `UnidadSuperiorId` hasta la raíz y consultando esta
tabla — no se almacena de forma denormalizada en cada fila de `UnidadOrganizativa` (research.md §4).

## TipoPersona

Catálogo de perfiles de persona (p. ej. Trabajador, Visitante, Proveedor) — RF-010.

| Campo | Tipo | Reglas |
|---|---|---|
| Nombre | string(100) | Único |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-032 |

**Relaciones**: 1—N `AsignaciónTipoPersona`; 1—N `ÁreaAccesoTipoPersona`.

## Persona

| Campo | Tipo | Reglas |
|---|---|---|
| Nombres | string(150) | RF-012 |
| Apellidos | string(150) | RF-012 |
| FechaNacimiento | date | RF-012 |
| TipoDocumentoId | Guid (FK → TipoDocumento) | RF-012 |
| NumeroDocumento | string(20) | RF-012 |
| GéneroId | Guid (FK → Género) | RF-012 |
| CorreoElectronico | string(256) | Formato de correo válido |
| TipoSangreId | Guid (FK → TipoSangre) | RF-012 |
| ContactoEmergencia | string(150) | Nombre del contacto |
| NumeroEmergencia | string(30) | Teléfono del contacto |

**Relaciones**: 1—N `AsignaciónTipoPersona`; 1—N `AsignaciónPersonaCompañía`; 1—N
`AsignaciónPersonaUnidadOrganizativa`; 1—N `AsignaciónCredencial`; referenciada por `PermisoAcceso.PersonaId`
cuando `Alcance = PERSONA`.

**Validaciones clave**: único `(TipoDocumentoId, NumeroDocumento)` globalmente — RF-041, Historia 4. `Id` no
se muestra en interfaces gráficas normales (RF-013).

## AsignaciónTipoPersona

Histórico de perfiles vigentes de una persona; a diferencia de compañía/unidad, admite múltiples perfiles
simultáneos activos (RF-011: "uno o varios") — sin restricción de exclusividad mutua.

| Campo | Tipo | Reglas |
|---|---|---|
| PersonaId | Guid (FK → Persona) | — |
| TipoPersonaId | Guid (FK → TipoPersona) | Debe estar `ACTIVO` al momento de asignar (RF-032) |
| FechaHoraInicio | datetime2(3) | — |
| FechaHoraFin | datetime2(3) (**NOT NULL**) | Fecha/hora real y conocida desde la creación; ya no admite `null` ni fecha centinela (RF-071, Sesión 2026-09-14 "vigencia temporal jerárquica") — no existe vigencia indefinida para esta entidad. **No** sujeta a contención respecto a `AsignaciónPersonaCompañía` (RF-072) — el modelo de dominio no la establece como dependiente de la pertenencia (RF-011: perfiles múltiples sin exclusividad) |
| Estado | enum: `ACTIVO`, `INACTIVO` | Baja lógica sin eliminar el histórico |

**Relaciones**: N—1 `Persona`; N—1 `TipoPersona`.

**Validaciones clave**: `FechaHoraFin` debe ser posterior a `FechaHoraInicio` (RF-039). No se exige
exclusividad entre asignaciones de distinto `TipoPersonaId` para la misma persona; sí se rechaza duplicar el
mismo `(PersonaId, TipoPersonaId)` con rangos solapados.

## AsignaciónPersonaCompañía

Histórico de pertenencia de una persona a una compañía; como máximo una asignación activa a la vez
(Clarifications, RF-014). El cierre de la asignación vigente dispara la revocación automática en cascada de
los contextos operativos, asignaciones de unidad organizativa y credenciales que dependen de ella (RF-061,
Historia 5 "Revocación automática por cese de pertenencia").

| Campo | Tipo | Reglas |
|---|---|---|
| PersonaId | Guid (FK → Persona) | — |
| CompañíaId | Guid (FK → Compañía) | Debe estar `ACTIVO` al momento de asignar (RF-032) |
| FechaHoraInicio | datetime2(3) | Normalizada a 00:00 del día de inicio (RF-016); nunca se modifica tras la creación (RF-063) |
| FechaHoraFin | datetime2(3) (**NOT NULL**) | Normalizada a 23:59 del último día de vigencia (RF-016); fecha/hora real y conocida **obligatoria desde la creación** (RF-071, Sesión 2026-09-14 "vigencia temporal jerárquica") — ya no admite `null` ni fecha centinela; no existe pertenencia de vigencia indefinida. Es también la fecha efectiva de revocación en cascada cuando se cierra anticipadamente (RF-064); un cierre (reemplazo o cese) solo puede **acortarla**; una **renovación** explícita (RF-073, ver más abajo) es la única operación que puede **extenderla**, y solo mientras `Estado = ACTIVA` |
| Estado | enum: `ACTIVA`, `FINALIZADA` | RF-063. Administrativo/informativo — NO determina vigencia por sí solo (ver Validaciones clave) |
| MotivoFin | enum: `CESE_PERTENENCIA`, `REEMPLAZO_ASIGNACION` (nullable) | Poblado solo cuando `Estado = FINALIZADA` (RF-063) |

**Relaciones**: N—1 `Persona`; N—1 `Compañía`; es la asociación temporal raíz de la que dependen
temporalmente (contención, RF-072) `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`
y `AsignaciónCredencial`.

**Restricción de base de datos**: trigger `AFTER INSERT, UPDATE` particionado por `PersonaId` — impide
solapamiento físico entre asignaciones de la misma persona (research.md §5).

**Validaciones clave**: al crear una nueva asignación cuya `FechaHoraInicio` sea anterior o igual a la
`FechaHoraFin` ya declarada de la asignación activa previa de la misma persona (esto es, cuando de otro modo
se produciría solapamiento), el caso de uso cierra automáticamente esa asignación previa en la misma
transacción — ajusta su `FechaHoraFin` a 23:59 del día anterior al nuevo inicio (sin extenderla si ya era
anterior a ese valor), fija `Estado = FINALIZADA`, `MotivoFin = REEMPLAZO_ASIGNACION` (Historia 5). Si la
nueva asignación inicia después de que la `FechaHoraFin` ya declarada de la anterior haya pasado (sin
solapamiento posible), la anterior no se modifica: conserva su propia `FechaHoraFin` real tal como fue
declarada, con `Estado` sin cambios (RF-071 no exige, y este documento no asume, que `Estado` se actualice
por el mero vencimiento — ver Historia 5 regla 7). Un cierre explícito (sin reemplazo, antes del vencimiento
natural) usa `MotivoFin = CESE_PERTENENCIA`. **Ambos casos de cierre anticipado disparan la cascada de
revocación de RF-061** (research.md §14.1) — la vigencia efectiva de esta entidad y de sus dependientes se
determina siempre por `FechaHoraInicio`/`FechaHoraFin`, nunca por `Estado` en aislamiento (research.md
§14.2). Períodos inválidos (`FechaHoraFin ≤ FechaHoraInicio`) se rechazan (RF-039). `CompañíaId` puede
referenciar indistintamente una compañía `PRINCIPAL_MANDANTE` o `CONTRATISTA` (RF-047) — el histórico de
compañía de una persona no está restringido a un tipo de compañía. Esta entidad representa únicamente la
relación laboral/contractual de la persona (a qué empresa pertenece); NO representa para qué Compañía
Principal trabaja/accede — ver `ContextoOperativoPersonaPrincipal` a continuación (RF-048, Historia 5).

**Renovación (RF-073, Sesión 2026-09-14 "renovación de AsignaciónPersonaCompañía")**: una tercera operación,
distinta de la creación de una nueva asignación (reemplazo automático) y del cierre explícito (cese), que
**extiende** `FechaHoraFin` hacia una fecha posterior a la ya vigente, sin crear ningún registro nuevo.
Reglas: (a) solo aplica si `Estado = ACTIVA` **y** la pertenencia sigue **vigente dinámicamente** en el
momento de renovar (`fecha actual <= FechaHoraFin` ya declarada) — una asignación `FINALIZADA`, o una
`ACTIVA` cuya `FechaHoraFin` ya pasó sin cierre administrativo (expiración dinámica), NO es renovable en
ningún caso: requiere una nueva `AsignaciónPersonaCompañía` (Sesión 2026-09-14 "cierre Decisión Pendiente
#10" — la renovación nunca puentea retroactivamente un vacío temporal ya transcurrido); (b) la nueva
`FechaHoraFin` DEBE ser estrictamente posterior a la ya vigente (rechazada si es igual o anterior);
(c) `FechaHoraInicio` no se toca; (d) `Estado` y `MotivoFin` no cambian; (e) NO crea, modifica ni extiende
ninguna `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` ni `AsignaciónCredencial`
existente — estas conservan su propia `FechaHoraFin` sin cambios; el único efecto es ampliar el techo
temporal que RF-072 permitirá validar para asociaciones creadas *después* de la renovación; (f) no dispara
la cascada de RF-061 (no es un cierre); (g) queda registrada mediante `UpdatedAt`/`UpdatedById`
(RF-026/RF-027), sin campo ni entidad de auditoría adicional.

## ContextoOperativoPersonaPrincipal

Relación operativa vigente entre una Persona y una Compañía Principal (RF-052, Historia 5) — **distinta e
independiente** del histórico de compañía de pertenencia (`AsignaciónPersonaCompañía`). Es el contenedor
lógico de la asignación de unidad organizativa de la persona para esa Principal. Una persona puede tener
ningún, uno o varios contextos operativos vigentes **simultáneamente**, cada uno con una Compañía Principal
distinta (CS-013).

| Campo | Tipo | Reglas |
|---|---|---|
| PersonaId | Guid (FK → Persona) | — |
| CompañíaPrincipalId | Guid (FK → Compañía) | DEBE referenciar una compañía con `TipoCompañía = PRINCIPAL_MANDANTE` |
| FechaHoraInicio | datetime2(3) | Normalizada a 00:00 del día de inicio (RF-016); nunca se modifica tras la creación (RF-063) |
| FechaHoraFin | datetime2(3) (**NOT NULL**) | Normalizada a 23:59 del último día; fecha/hora real y conocida **obligatoria desde la creación** (RF-071) — ya no admite `null` ni fecha centinela. DEBE estar contenida dentro de la vigencia de la `AsignaciónPersonaCompañía` vigente de la persona al crear el contexto (RF-072, contención — ver Validaciones clave) |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-063. Administrativo/informativo — NO determina vigencia por sí solo (ver Validaciones clave) |
| MotivoFin | enum: `REVOCACION_CESE_PERTENENCIA`, `REEMPLAZO_ASIGNACION`, `CIERRE_MANUAL` (nullable) | Poblado solo cuando `Estado = INACTIVO` (RF-063) |
| RevocadoPorPertenenciaId | Guid? (FK → AsignaciónPersonaCompañía, nullable) | Poblado solo si `MotivoFin = REVOCACION_CESE_PERTENENCIA` (RF-063, research.md §14.2) |

**Relaciones**: N—1 `Persona`; N—1 `Compañía` (solo `PRINCIPAL_MANDANTE`); N—1 opcional
`AsignaciónPersonaCompañía` (vía `RevocadoPorPertenenciaId`; también la referencia de contención temporal —
RF-072); 1—N `AsignaciónPersonaUnidadOrganizativa` (vía `ContextoOperativoId`).

**Restricción de base de datos**: trigger `AFTER INSERT, UPDATE` particionado por
`(PersonaId, CompañíaPrincipalId)` — impide solapamiento de contextos del mismo par persona-Principal, pero
permite contextos simultáneos de la misma persona con Principales distintas (research.md §5, §13).

**Validaciones clave** (RF-053, RF-054, research.md §13 — la persona DEBE tener una `AsignaciónPersonaCompañía`
vigente para poder abrir un contexto; ver nota de alcance en research.md sobre esta inferencia de diseño):
- Si la compañía vigente de la persona es `PRINCIPAL_MANDANTE`: `CompañíaPrincipalId` DEBE ser exactamente
  esa misma compañía — el sistema la fija automáticamente, sin permitir seleccionar otra (RF-053, CS-020,
  Historia 5 Caso A).
- Si la compañía vigente de la persona es `CONTRATISTA`: DEBE existir una `RelaciónContratistaPrincipal`
  vigente entre esa Contratista y `CompañíaPrincipalId` en el momento de la asignación (RF-054, CS-019,
  Historia 5 Caso B).
- **Contención temporal (RF-072, nuevo — Sesión 2026-09-14 "vigencia temporal jerárquica")**:
  `FechaHoraInicio >= FechaHoraInicio` de la `AsignaciónPersonaCompañía` vigente, y `FechaHoraFin <=
  FechaHoraFin` de esa misma pertenencia. Se valida en Aplicación al crear el contexto (no es un `CHECK` de
  SQL Server porque cruza tablas); no impide que el contexto termine antes que la pertenencia.
- Períodos inválidos (`FechaHoraFin ≤ FechaHoraInicio`) se rechazan (RF-039).

**Exclusividad por par confirmada (auditoría de consistencia, Sesión 2026-09-14)**: la exclusividad estricta
por `(PersonaId, CompañíaPrincipalId)` es una decisión final, no provisional — ningún caso de negocio
establecido requiere más de un contexto simultáneo con la misma Principal (research.md §13.1).

**Revocación automática por cese de pertenencia (RF-061, corrección Revocación Automática, Sesión
2026-09-14 — reemplaza la nota anterior de esta sección)**: cuando se cierra la `AsignaciónPersonaCompañía`
vigente de la persona, el mismo caso de uso, en la misma transacción, cierra en cascada **todos** los
contextos operativos vigentes de esa persona en ese momento (que pueden ser varios simultáneos, uno por
Compañía Principal — CS-013, CS-030; no hay ningún límite de RF-014 sobre esta entidad, solo sobre
`AsignaciónPersonaCompañía`): fija `FechaHoraFin` (sin extender una fecha ya fijada antes),
`Estado = INACTIVO`, `MotivoFin = REVOCACION_CESE_PERTENENCIA` y `RevocadoPorPertenenciaId` apuntando a la
pertenencia que se cerró (research.md §14.1, §14.4). El registro nunca se elimina ni se modifica su
`FechaHoraInicio`. Como `AsignaciónPersonaCompañía` solo admite una asignación activa a la vez (RF-014),
todo contexto abierto en ese momento —sea uno o varios— dependía necesariamente de esa misma pertenencia (no
puede existir un contexto que dependiera de otra pertenencia también vigente al mismo tiempo), por lo que la
cascada los alcanza a todos; una pertenencia distinta y posterior de la misma persona abre contextos propios
no afectados (RF-062). Además de esta escritura, la evaluación
de acceso sigue re-validando dinámicamente la legitimidad de cada contexto en cada evaluación (research.md
§7 paso 5) como defensa adicional, no como sustituto (RF-065).

## AsignaciónPersonaUnidadOrganizativa

Asignación de unidad organizativa de una persona, registrada **dentro de** un `ContextoOperativoPersonaPrincipal`
(RF-015, RF-048, RF-055, Historia 5).

| Campo | Tipo | Reglas |
|---|---|---|
| PersonaId | Guid (FK → Persona) | Denormalizado por conveniencia de consulta; DEBE coincidir con `ContextoOperativoId.PersonaId` (validado en Aplicación) |
| ContextoOperativoId | Guid (FK → ContextoOperativoPersonaPrincipal) | Obligatoria; determina la Compañía Principal de esta asignación |
| UnidadOrganizativaId | Guid (FK → UnidadOrganizativa) | Debe estar `ACTIVO` al momento de asignar; su Compañía Principal propietaria (vía `CompañíaPrincipalUnidadOrganizativaRaiz`) DEBE coincidir con `ContextoOperativoId.CompañíaPrincipalId` (RF-055, validado en Aplicación) |
| FechaHoraInicio | datetime2(3) | Normalizada a 00:00 (RF-016); nunca se modifica tras la creación (RF-063) |
| FechaHoraFin | datetime2(3) (**NOT NULL**) | Normalizada a 23:59 del último día; fecha/hora real y conocida **obligatoria desde la creación** (RF-071) — ya no admite `null` ni fecha centinela. DEBE estar contenida dentro de la vigencia de la `AsignaciónPersonaCompañía` vigente de la persona al crear la asignación (RF-072, contención — ver Validaciones clave) |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-063. Administrativo/informativo — NO determina vigencia por sí solo |
| MotivoFin | enum: `REVOCACION_CESE_PERTENENCIA`, `REEMPLAZO_ASIGNACION`, `CIERRE_MANUAL` (nullable) | Poblado solo cuando `Estado = INACTIVO` (RF-063) |
| RevocadoPorPertenenciaId | Guid? (FK → AsignaciónPersonaCompañía, nullable) | Poblado solo si `MotivoFin = REVOCACION_CESE_PERTENENCIA` (research.md §14.2) |

**Relaciones**: N—1 `Persona` (denormalizada); N—1 `ContextoOperativoPersonaPrincipal`; N—1
`UnidadOrganizativa`; N—1 indirecta `AsignaciónPersonaCompañía` (vía `ContextoOperativoId.PersonaId`, para
contención temporal — RF-072).

**Restricción de base de datos**: trigger `AFTER INSERT, UPDATE` particionado por `ContextoOperativoId` —
impide solapamiento **dentro del mismo contexto operativo**, pero permite asignaciones de unidad
organizativa simultáneas en contextos operativos distintos de la misma persona (RF-015 corregido, CS-014;
research.md §5, §13).

**Validaciones clave (RF-048, RF-055, RF-072)**: el histórico de compañía de pertenencia
(`AsignaciónPersonaCompañía`) y esta asignación quedan conectadas mediante las reglas de apertura del
`ContextoOperativoPersonaPrincipal` (RF-053/RF-054) que esta asignación referencia obligatoriamente, **y**
mediante la contención temporal explícita de RF-072 (**[REEMPLAZADA — ver Sesión 2026-09-14 "vigencia
temporal jerárquica"]** ~~no hay validación cruzada directa~~ SÍ existe validación cruzada temporal: su
`FechaHoraInicio`/`FechaHoraFin` DEBE estar contenida dentro de la vigencia de la `AsignaciónPersonaCompañía`
vigente de la persona en el momento de crear la asignación). Esta asignación, junto con el contexto
operativo que la contiene, es la relación operacional explícita, histórica y temporal entre una persona (en
particular, el personal de una Compañía Contratista) y la Compañía Principal propietaria de la unidad; no
existe acceso implícito a otras Principales fuera de esta asignación (RF-048, Historia 5). Las operaciones
de asignación quedan limitadas al alcance de compañías del usuario que las realiza (RF-049, RF-060).

**Revocación automática (RF-061)**: cuando la cascada de cierre de `AsignaciónPersonaCompañía` revoca el
`ContextoOperativoPersonaPrincipal` que contiene esta asignación (ver esa sección), esta asignación se
revoca en la misma transacción: `FechaHoraFin` (sin extender una fecha ya fijada antes), `Estado =
INACTIVO`, `MotivoFin = REVOCACION_CESE_PERTENENCIA`, `RevocadoPorPertenenciaId` apuntando a la misma
pertenencia. El registro se conserva íntegro (research.md §14.1).

## ÁreaAcceso

Árbol de áreas físicas de acceso (Historia 6, RF-009). A diferencia de `UnidadOrganizativa`, no existe
restricción de negocio contra una relación directa hacia `Compañía`; por eso `ÁreaAcceso` sí incorpora una FK
directa `CompañíaPrincipalId` (RF-046, research.md §4).

| Campo | Tipo | Reglas |
|---|---|---|
| Nombre | string(200) | — |
| ÁreaSuperiorId | Guid? (FK → ÁreaAcceso, nullable) | `null` ⇒ nodo raíz |
| CompañíaPrincipalId | Guid (FK → Compañía) | RF-046. DEBE referenciar una compañía con `TipoCompañía = PRINCIPAL_MANDANTE`; en un área hija, DEBE coincidir con el `CompañíaPrincipalId` de su área padre |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-032 |

**Relaciones**: autorreferencia N—1/1—N; N—1 `Compañía` (solo `PRINCIPAL_MANDANTE`); 1—N
`ÁreaAccesoTipoPersona`; 1—N `PermisoAcceso`.

**Validaciones clave**: sin ciclos, directos ni indirectos (RF-038, Principio V, research.md §4). Un área
`INACTIVA` no participa en evaluaciones de acceso concedido (Historia 8, paso 4). `CompañíaPrincipalId` debe
apuntar a una compañía `PRINCIPAL_MANDANTE` (rechazado si es `CONTRATISTA`, Historia 6 criterio de
aceptación adicional); al crear o mover un área hija, su `CompañíaPrincipalId` se fija igual al de su área
padre (no editable independientemente) — validado en el mismo paso que la detección de ciclos. Las
operaciones de mantenimiento sobre un área quedan limitadas al alcance de compañías del usuario autenticado,
evaluado contra su `CompañíaPrincipalId` (RF-049).

## ÁreaAccesoTipoPersona

Tipos de persona autorizados por área (Historia 7, RF-019).

| Campo | Tipo | Reglas |
|---|---|---|
| ÁreaAccesoId | Guid (FK → ÁreaAcceso) | — |
| TipoPersonaId | Guid (FK → TipoPersona) | — |

**Relaciones**: N—1 `ÁreaAcceso`; N—1 `TipoPersona`. Único `(ÁreaAccesoId, TipoPersonaId)`.

## PermisoAcceso

Permiso de acceso a un área, con alcance PERSONA / UNIDAD_ORGANIZATIVA / COMPAÑÍA (Historia 8, RF-020,
RF-021).

| Campo | Tipo | Reglas |
|---|---|---|
| ÁreaAccesoId | Guid (FK → ÁreaAcceso) | — |
| Alcance | enum: `PERSONA`, `UNIDAD_ORGANIZATIVA`, `COMPAÑÍA` | RF-020 |
| PersonaId | Guid? (FK → Persona, nullable) | Obligatorio y único-no-nulo si `Alcance = PERSONA` |
| UnidadOrganizativaId | Guid? (FK → UnidadOrganizativa, nullable) | Obligatorio si `Alcance = UNIDAD_ORGANIZATIVA` |
| CompañíaId | Guid? (FK → Compañía, nullable) | Obligatorio si `Alcance = COMPAÑÍA` |
| FechaHoraInicioVigencia | datetime2(3) | RF-021 |
| FechaHoraFinVigencia | datetime2(3) (**NOT NULL**) | RF-021 ya exigía "inicio **y** fin de vigencia" desde el spec original — este campo se documentaba incorrectamente como nullable, contradiciéndolo; se corrige aquí (RF-071, Sesión 2026-09-14 "vigencia temporal jerárquica", hallazgo adicional). Aplica a **los tres alcances** (PERSONA, UNIDAD_ORGANIZATIVA, COMPAÑÍA), no solo PERSONA. **No** sujeto a contención respecto a `AsignaciónPersonaCompañía` (RF-072) — `PermisoAcceso` no está en la lista de entidades revocadas por RF-061; es configuración evaluada independientemente del histórico de pertenencia (Historia 8) |
| Estado | enum: `ACTIVO`, `INACTIVO` | Baja lógica |

**Relaciones**: N—1 `ÁreaAcceso`; N—1 opcional `Persona` / `UnidadOrganizativa` / `Compañía` según `Alcance`;
1—N `BloqueHorarioPermiso`.

**Restricción de base de datos**: `CHECK` que garantiza que exactamente una de `PersonaId`,
`UnidadOrganizativaId`, `CompañíaId` sea no nula, y que coincida con `Alcance`.

**Validaciones clave**: `FechaHoraFinVigencia` posterior a `FechaHoraInicioVigencia` (RF-039).
Evaluado según el flujo de 14 pasos de research.md §7 (que determina primero la Compañía Principal
propietaria del área y exige un `ContextoOperativoPersonaPrincipal` vigente antes de evaluar cualquier
permiso — RF-059), con precedencia definitiva PERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA cuando varios
niveles aplican simultáneamente al mismo área y horario (RF-025). Cuando `Alcance = COMPAÑÍA`, `CompañíaId`
puede referenciar una compañía `PRINCIPAL_MANDANTE` o `CONTRATISTA` indistintamente (research.md §12) —
p. ej. un permiso que otorga acceso a todo el personal de una Compañía Contratista sobre un área de la
Compañía Principal a la que presta servicios es un caso válido (Historia 8), condicionado siempre a que la
persona tenga contexto operativo vigente con esa Principal. Las operaciones de creación/actualización de un
permiso quedan limitadas al alcance de compañías del usuario autenticado, evaluado contra el
`CompañíaPrincipalId` del `ÁreaAcceso` referenciado (RF-049).

## BloqueHorarioPermiso

Bloques horarios por día de semana dentro de un permiso (Historia 8, RF-022).

| Campo | Tipo | Reglas |
|---|---|---|
| PermisoAccesoId | Guid (FK → PermisoAcceso) | — |
| DíaSemana | enum: `LUNES`…`DOMINGO` | — |
| HoraInicio | time (local, interpretada en `America/Lima`) | — |
| HoraFin | time (local, interpretada en `America/Lima`) | Debe ser posterior a `HoraInicio` |

**Relaciones**: N—1 `PermisoAcceso`.

**Validaciones clave**: `HoraFin > HoraInicio` (RF-039); dentro del mismo `(PermisoAccesoId, DíaSemana)` los
bloques no pueden solaparse entre sí. La evaluación de acceso convierte la fecha/hora UTC evaluada a
`America/Lima` antes de comparar contra estos bloques (research.md §5, §7).

## TipoCredencial

Representa el **tipo/diseño visual** de la credencial que utilizará la persona (p. ej. "Credencial
Contratista", "Credencial Corporativo", "Credencial Visitante", "Credencial Proveedor", "Credencial
Temporal") — **nunca** una tecnología de identificación física (RFID, QR, NFC, código de barras) ni un
identificador físico (RF-058, corrección Sesión 2026-09-14). La impresión física y el diseño gráfico quedan
fuera del alcance del sistema.

| Campo | Tipo | Reglas |
|---|---|---|
| Nombre | string(100) | RF-017 |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-017, RF-032 |

**Relaciones**: 1—N `AsignaciónCredencial`.

## AsignaciónCredencial

Histórico de credenciales/fotocheck asignadas a una persona, **dentro del contexto de una Compañía
Principal** (Historia 9, RF-018, RF-056, RF-057).

| Campo | Tipo | Reglas |
|---|---|---|
| PersonaId | Guid (FK → Persona) | — |
| CompañíaPrincipalId | Guid (FK → Compañía) | RF-056. DEBE referenciar una compañía con `TipoCompañía = PRINCIPAL_MANDANTE` |
| TipoCredencialId | Guid (FK → TipoCredencial) | Debe estar `ACTIVO` al asignar (RF-032) |
| FechaHoraInicio | datetime2(3) | Nunca se modifica tras la creación (RF-063) |
| FechaHoraFin | datetime2(3) (**NOT NULL**) | Fin de vigencia; fecha/hora real y conocida **obligatoria desde la creación** (RF-071, Sesión 2026-09-14 "vigencia temporal jerárquica" — ya no admite `null` ni fecha centinela; supera la redacción de la sesión anterior, que solo permitía valor futuro opcional). DEBE estar contenida dentro de la vigencia de la `AsignaciónPersonaCompañía` vigente de la persona al crear la credencial (RF-072, contención). Al cerrarse administrativamente (`DEVUELTO`/`ELIMINADO`/`REVOCADA`) se ajusta al valor efectivo de cierre, sin extenderla más allá de lo ya declarado (mismo patrón que las demás entidades revocables) |
| Estado | enum: `ASIGNADO`, `DEVUELTO`, `ELIMINADO`, `REVOCADA` | Historia 9. `REVOCADA` es nuevo (RF-061) — distinto de `DEVUELTO` (devolución física voluntaria) y `ELIMINADO` (baja lógica administrativa): el acceso fue invalidado por una causa ajena a la credencial (cese de la pertenencia que la sustentaba). Administrativo/informativo: **NO** se transiciona automáticamente por el mero vencimiento de `FechaHoraFin` (RF-070) — solo una acción administrativa explícita (devolución, baja lógica, cascada de revocación) cambia `Estado` |
| RevocadoPorPertenenciaId | Guid? (FK → AsignaciónPersonaCompañía, nullable) | Poblado solo si `Estado = REVOCADA` (research.md §14.2) |

**Relaciones**: N—1 `Persona`; N—1 `Compañía` (solo `PRINCIPAL_MANDANTE`); N—1 `TipoCredencial`; N—1 opcional
`AsignaciónPersonaCompañía` (vía `RevocadoPorPertenenciaId`).

**Restricción de base de datos**: trigger `AFTER INSERT, UPDATE` particionado por
`(PersonaId, CompañíaPrincipalId)`, evaluado solo entre filas con `Estado = ASIGNADO`, para impedir dos
credenciales simultáneamente `ASIGNADO` que se solapen **para la misma persona y la misma Compañía
Principal** (RF-057) — una persona SÍ puede tener credenciales `ASIGNADO` simultáneas para Compañías
Principales distintas (CS-016, CS-017; research.md §5, §13).

**Sin cierre automático de la credencial previa (Sesión 2026-09-15, decisión A)**: a diferencia de
`AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y
`RelaciónContratistaPrincipal`, asignar una credencial **nunca** cierra, finaliza, devuelve, elimina ni
revoca una credencial anterior. Si la persona ya tiene una credencial `ASIGNADO` para la misma Compañía
Principal cuyo período se solapa con el de la nueva, la asignación se rechaza con 409
(`SOLAPAMIENTO_VIGENCIA`, RF-057) y la existente queda intacta — la aplicación lo valida antes de escribir y
el trigger es la última línea de defensa (research.md §5). `DEVUELTO`, `ELIMINADO` y `REVOCADA` solo se
producen, respectivamente, por devolución física, baja administrativa y la cascada de RF-061; no existe un
estado ni motivo de "reemplazo" (research.md §23).

**Validaciones clave (RF-056, RF-072)**: DEBE existir un `ContextoOperativoPersonaPrincipal` vigente entre
la persona y `CompañíaPrincipalId` en el momento de la asignación (misma naturaleza de validación que para
`AsignaciónPersonaUnidadOrganizativa`, sin una FK directa al contexto — research.md §13). La ventana
`FechaHoraInicio`/`FechaHoraFin` DEBE estar contenida dentro de la vigencia de la `AsignaciónPersonaCompañía`
vigente de la persona (RF-072, contención — Sesión 2026-09-14 "vigencia temporal jerárquica"). Una
credencial emitida en el contexto de una Principal no satisface reglas de contexto o autorización de otra
Principal (CS-024). *(Corregido — Sesión 2026-09-14, integración `ux-ui.md`)*: ~~la credencial es un
elemento complementario de identificación, nunca el mecanismo que por sí solo determina el permiso de
acceso (Historia 8, Historia 9).~~ La credencial **vigente** (RF-070: `Estado = ASIGNADO` y dentro de su
ventana `FechaHoraInicio`/`FechaHoraFin`) es condición necesaria, no suficiente, para conceder acceso — su
ausencia deniega por defecto (RF-066, research.md §7 paso 6).

**Vigencia efectiva vs. `Estado`**: igual que en las demás entidades revocables (RF-063), `Estado` es
administrativo/informativo; la vigencia efectiva para autorización se determina siempre comparando
`FechaHoraInicio`/`FechaHoraFin` contra la fecha evaluada, nunca por `Estado` en aislamiento (RF-070). Una
fila `Estado = ASIGNADO` cuya `FechaHoraFin` ya pasó está temporalmente expirada para efectos de RF-066,
sin que ello cambie su `Estado`.

**Transiciones de estado**: `ASIGNADO → DEVUELTO` · `ASIGNADO → ELIMINADO` (baja lógica, no elimina el
registro histórico — Historia 9: "Eliminar significa baja lógica, no eliminación física del histórico") ·
`ASIGNADO → REVOCADA` (automática, ver abajo). `DEVUELTO`, `ELIMINADO` y `REVOCADA` son terminales. El mero
vencimiento de `FechaHoraFin` **NO** es una transición de estado — `Estado` permanece `ASIGNADO` hasta que
una acción administrativa explícita lo cambie (RF-070).

**Revocación automática (RF-061)**: cuando se cierra la `AsignaciónPersonaCompañía` vigente de la persona,
toda credencial en `Estado = ASIGNADO` cuyo `CompañíaPrincipalId` corresponda a un contexto operativo
revocado por esa misma cascada pasa a `Estado = REVOCADA` en la misma transacción: `FechaHoraFin` (sin
extender una fecha ya fijada antes) y `RevocadoPorPertenenciaId` apuntando a la pertenencia que se cerró. El
registro se conserva íntegro (research.md §14.1).

## TipoDocumento

Catálogo maestro versionado (RF-031). Semilla inicial para Perú: DNI, Carné de Extranjería, Pasaporte, RUC
(para `Compañía`).

| Campo | Tipo | Reglas |
|---|---|---|
| Nombre | string(100) | Único |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-032 |

## TipoSangre

Catálogo maestro versionado. Semilla inicial: O+, O-, A+, A-, B+, B-, AB+, AB-.

| Campo | Tipo | Reglas |
|---|---|---|
| Nombre | string(10) | Único |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-032 |

## Género

Catálogo maestro versionado. Semilla inicial: Masculino, Femenino.

| Campo | Tipo | Reglas |
|---|---|---|
| Nombre | string(50) | Único |
| Estado | enum: `ACTIVO`, `INACTIVO` | RF-032 |

---

## Diagrama de relaciones (resumen textual)

```text
Usuario 1──N AlcanceUsuarioCompañía N──1 Compañía  (alcance ADMINISTRATIVO — independiente de lo operacional, RF-050)
Usuario 1──N HistorialContraseña

Compañía [TipoCompañía: PRINCIPAL_MANDANTE | CONTRATISTA]
Compañía(Contratista) N──N Compañía(Principal)  vía  RelaciónContratistaPrincipal  (vigencia; RF-051, CS-012)
Compañía 1──N AsignaciónPersonaCompañía N──1 Persona   (cualquier TipoCompañía — RF-047; relación LABORAL;
  Estado/MotivoFin — RF-063; su cierre dispara la cascada de revocación — RF-061, research.md §14)

UnidadOrganizativa (self N──1 padre)                    ── SIN relación directa hacia Compañía (RF-044) ──
  └─ solo nodo raíz ── 1──1 ── CompañíaPrincipalUnidadOrganizativaRaiz ── N──1 ── Compañía [PRINCIPAL_MANDANTE]
TipoPersona 1──N AsignaciónTipoPersona N──1 Persona

Persona 1──N ContextoOperativoPersonaPrincipal N──1 Compañía [PRINCIPAL_MANDANTE]   (relación OPERATIVA,
  independiente de AsignaciónPersonaCompañía — RF-048/RF-052; varios contextos simultáneos por persona, uno
  por Principal — CS-013; Estado/MotivoFin/RevocadoPorPertenenciaId — RF-061/RF-063)
  └─(N──1, opcional)── AsignaciónPersonaCompañía   vía RevocadoPorPertenenciaId (solo si fue revocado en cascada)
ContextoOperativoPersonaPrincipal 1──N AsignaciónPersonaUnidadOrganizativa N──1 UnidadOrganizativa
  (exclusividad rescopeada a ContextoOperativoId, no a PersonaId — RF-015/RF-055, CS-014; Estado/MotivoFin/
  RevocadoPorPertenenciaId — RF-061/RF-063)
ContextoOperativoPersonaPrincipal 1──N AsignaciónCredencial N──1 TipoCredencial
  (exclusividad rescopeada a (PersonaId, CompañíaPrincipalId) — RF-056/RF-057, CS-016/CS-017; TipoCredencial
  = diseño visual, no tecnología física — RF-058; Estado ahora incluye REVOCADA — RF-061)

Revocación automática (RF-061 a RF-065, research.md §14): cierre de AsignaciónPersonaCompañía ──cascada──>
  ContextoOperativoPersonaPrincipal ──cascada──> {AsignaciónPersonaUnidadOrganizativa, AsignaciónCredencial}
  (misma transacción, FechaHoraFin propagada, Estado/MotivoFin/RevocadoPorPertenenciaId poblados; nunca
  elimina registros ni modifica FechaHoraInicio; no afecta pertenencias/contextos distintos de la persona)

ÁreaAcceso (self N──1 padre, hijo hereda CompañíaPrincipalId del padre)
ÁreaAcceso N──1 Compañía [PRINCIPAL_MANDANTE únicamente] (RF-046)
ÁreaAcceso 1──N ÁreaAccesoTipoPersona N──1 TipoPersona
ÁreaAcceso 1──N PermisoAcceso ──(Alcance)──> {Persona | UnidadOrganizativa | Compañía (cualquier TipoCompañía)}
  ── evaluado solo si existe ContextoOperativoPersonaPrincipal vigente con la Principal del área (RF-059) ──
PermisoAcceso 1──N BloqueHorarioPermiso

TipoDocumento 1──N Persona / Compañía
TipoSangre 1──N Persona
Género 1──N Persona
```
