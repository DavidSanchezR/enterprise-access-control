# Fase 0 — Investigación: Control de Acceso Empresarial

Todas las incógnitas del Technical Context del plan quedan resueltas en este documento. La lista de
"Decisiones Pendientes" del spec (política de contraseñas, identificador físico de credencial, requisitos
legales de retención) no bloquea la planificación técnica: se documentan como decisiones de trabajo con
rationale explícito, marcadas para confirmación de negocio antes de producción.

> **Nota de revisión (Sesión 2026-09-15, decisión A — sin cierre automático de credencial previa)**: §5
> atribuía de forma genérica a toda entidad particionada el cierre automático de la asignación activa
> anterior, lo que arrastraba ese comportamiento a `AsignaciónCredencial` y lo reflejaba
> `contracts/credentials.yaml`. Negocio decidió que no aplica a credenciales: el solapamiento se rechaza
> con 409 y la credencial previa no se toca. Se acota §5; §23 y §24 no cambian y quedan confirmados. Ninguna
> otra entidad ni regla se modifica.
>
> **Nota de revisión (Sesión 2026-09-14, corrección Compañía Principal/Contratista)**: esta fase 0 fue
> revisada tras incorporar la clasificación PRINCIPAL_MANDANTE/CONTRATISTA de Compañía y el aislamiento de
> `UnidadOrganizativa`/`ÁreaAcceso` por Compañía Principal (spec.md, Clarifications). Se agregó la decisión
> §12; las decisiones §3 y §4 se ampliaron con las implicancias de autorización y jerarquía que introduce el
> nuevo modelo. Ninguna decisión previa fue invalidada.
>
> **Nota de revisión (Sesión 2026-09-14, corrección Contexto Operativo)**: segunda revisión de esta fase 0.
> Se **reversa** la decisión de §12 (que rechazaba una entidad `RelaciónContratistaPrincipal`) — negocio
> confirmó explícitamente que dicha entidad SÍ es necesaria, junto con una nueva entidad
> `ContextoOperativoPersonaPrincipal` (nueva §13). Se rescopan las restricciones de exclusividad de §5 para
> `AsignaciónPersonaUnidadOrganizativa` (ahora por contexto operativo, no por persona globalmente) y de
> `AsignaciónCredencial` (ahora por par persona+Compañía Principal, no por persona globalmente). Se retira
> la decisión de trabajo de §9 (`CódigoFisico`), sin reemplazo. Se reescribe §7 con el algoritmo de
> evaluación de acceso de 13 pasos, fusionado con los pasos previamente vigentes que la nueva lista de
> negocio no repetía pero tampoco contradecía.
>
> **Nota de revisión (Sesión 2026-09-14, auditoría final de consistencia — `/speckit-clarify`)**: tercera
> revisión, de alcance mínimo. Cierra las dos Decisiones Pendientes dejadas abiertas por §13 (cambio de
> compañía de pertenencia, y exclusividad del contexto operativo), ambas resueltas por inferencia directa
> de reglas ya establecidas sin abrir ninguna decisión previamente cerrada. §7 (paso 5) se amplía para
> re-validar en vivo la legitimidad del contexto operativo según la compañía de pertenencia vigente en el
> momento de cada evaluación (RF-061) — ver §13 para el detalle completo.
>
> **Nota de revisión (Sesión 2026-09-14, corrección Revocación Automática)**: cuarta revisión. **Reversa**
> la conclusión de la sesión anterior de que la sola re-validación dinámica bastaba — negocio corrigió que
> la revocación automática en cascada (con escritura real de estado) es obligatoria, no opcional. Se agrega
> §14 con el diseño completo del mecanismo de revocación (campos nuevos en tres entidades, alcance,
> temporalidad, y su relación con la re-validación dinámica que §7/§13 ya establecían, la cual se conserva
> como defensa adicional, no se elimina). §7 (paso 5) se referencia sin cambios de fondo: sigue siendo
> válido como red de seguridad. No hay ninguna sección de este documento que no haya sido revisada al menos
> una vez; dos decisiones (§12 y ahora esta) han sido explícitamente revertidas por corrección de negocio —
> ambas documentadas con su razonamiento, no silenciadas.
>
> **Nota de revisión (Sesión 2026-09-14, corrección Modelo de Cardinalidad Definitivo)**: quinta revisión,
> de redacción, no de diseño — **ninguna decisión de negocio se revierte esta vez**. Negocio detectó que
> §14.4 (y las RF-014/RF-062 de spec.md) estaban redactadas de forma ambigua, susceptible de malinterpretar
> que RF-014 (máximo una `AsignaciónPersonaCompañía` activa) limitaba también el número de
> `ContextoOperativoPersonaPrincipal` simultáneos — algo que RF-052 siempre permitió sin límite (uno por
> Principal) y que el mecanismo de cascada de §14 siempre implementó correctamente (revocando **todos** los
> contextos abiertos, que pueden ser varios). Se reescribe §14.4 para eliminar la ambigüedad. Ningún campo,
> entidad, restricción de base de datos ni comportamiento cambia en esta sesión.
>
> **Nota de revisión (Sesión 2026-09-14, decisión arquitectónica — Stack Tecnológico Oficial)**: sexta
> revisión. El usuario fijó explícitamente el stack como decisión arquitectónica no negociable: .NET 10 +
> C# + ASP.NET Core 10 + Entity Framework Core 10 + **SQL Server** + LINQ (reemplaza PostgreSQL, que hasta
> ahora era un supuesto de `spec.md`, nunca una decisión ratificada). Esto **invalida el mecanismo concreto**
> de §5 (`EXCLUDE USING gist` no existe en SQL Server — contradicción real, no traducible 1:1; se reescribe
> §5 con el equivalente idiomático de SQL Server: un trigger `AFTER INSERT, UPDATE` por tabla particionada).
> §1 y §4 se ajustan de terminología (sin cambio de decisión de fondo: UUID v7 generado en app y CTE
> recursivo son ambos independientes del motor). §10 cambia de Swashbuckle a `Microsoft.AspNetCore.OpenApi`
> nativo de .NET 10 (nunca fue una decisión del usuario; se alinea con preferir capacidades estándar). §11
> cambia `Testcontainers.PostgreSql` por `Testcontainers.MsSql`. Se agregan las secciones §15 a §21
> (motor de base de datos, concurrencia optimista, value conversions, clustering de PK GUID, distinción
> Authorization de plataforma vs. motor de evaluación de dominio, health checks, y `ProblemDetails` — este
> último reemplaza el `ErrorResponse` propio de los 10 contratos, segunda contradicción real detectada).
> Ninguna regla de negocio ni entidad del dominio cambia — ver plan.md para el Technical Context actualizado.
>
> **Nota de revisión (Sesión 2026-09-14, integración `ux-ui.md` — `/speckit-clarify`)**: séptima revisión.
> La incorporación de `ux-ui.md` como entrada formal de UX/UI (ver plan.md) expuso una contradicción real
> entre su §16/§19 (la credencial gatilla la denegación de acceso) y el texto entonces vigente de Historia 9
> de spec.md ("la credencial nunca es el mecanismo que por sí solo determina el permiso de acceso") y de
> este documento (§7, que nunca consultaba `AsignaciónCredencial`). Negocio resolvió explícitamente a favor
> de `ux-ui.md`: la credencial **sí** gatilla la denegación (nuevo RF-066). §7 se reescribe con un paso
> nuevo (antiguo 13 pasos → 14), y `contracts/access-evaluation.yaml` agrega `SIN_CREDENCIAL_VIGENTE` al
> enum `MotivoDenegacion`. Se agrega §22 documentando la decisión de exponer consultas agregadas/
> transversales de auditoría, históricos e indicadores operativos (RF-067 a RF-069, nuevos) mediante
> endpoints dedicados server-side — resuelto por aplicación directa del Principio I (Seguridad Server-Side),
> no por negocio interactivo. Se confirma, sin cambios de modelo, que `AsignaciónCredencial` no necesita un
> campo `MotivoFin` propio (su `Estado` ya es específico por causa) — ver spec.md, Clarifications, para el
> razonamiento completo de las tres decisiones. Ninguna regla de cardinalidad, aislamiento entre Principales
> o revocación automática (RF-014, RF-052, RF-061 a RF-065) cambia.
>
> **Nota de revisión (Sesión 2026-09-14, auditoría de consistencia RF-066 — octava revisión)**: una
> auditoría dedicada sobre RF-066 encontró que `data-model.md` restringía `AsignaciónCredencial.FechaHoraFin`
> a `null` mientras `Estado = ASIGNADO` — una regla no derivada de ningún requisito de negocio que volvía
> inalcanzable el propio escenario que el paso 6 de §7 debía cubrir (una credencial `ASIGNADO` temporalmente
> expirada). Negocio confirmó la corrección explícitamente (RF-070, nuevo en spec.md): `FechaHoraFin` puede
> tener valor, incluso futuro, mientras `Estado = ASIGNADO`; la vigencia efectiva se determina siempre
> comparando fechas, nunca por `Estado` en aislamiento — mismo principio ya vigente para las otras tres
> entidades revocables (RF-063), ahora extendido explícitamente a `AsignaciónCredencial`. §7 (paso 6) se
> reescribe para expresar la conjunción completa (Estado + ventana temporal) en vez de solo `Estado`. Se
> agrega §24 con el detalle completo. No se encontró ninguna otra regla, RF, contrato o decisión que
> dependiera de la restricción eliminada; el trigger de no-solapamiento (§5) ya usaba la plantilla genérica
> `ISNULL(FechaHoraFin, '9999-12-31')` y nunca dependió de que `FechaHoraFin` fuera `null` durante
> `ASIGNADO`, por lo que no requiere ningún cambio. Ninguna regla de cardinalidad, aislamiento entre
> Principales o revocación automática cambia.
>
> **Nota de revisión (Sesión 2026-09-14, "vigencia temporal jerárquica" — novena revisión)**: la de mayor
> alcance sobre el modelado temporal desde el diseño original. **Reversa parcialmente** la octava revisión:
> `FechaHoraFin = null` como "vigencia indefinida" deja de ser válido, no solo para `AsignaciónCredencial`
> sino para las seis asociaciones temporales vinculadas a una persona (RF-071) —
> `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`,
> `AsignaciónCredencial`, `AsignaciónTipoPersona`, y `PermisoAcceso` (los tres alcances — hallazgo adicional:
> RF-021 ya lo exigía desde el spec original, nunca implementado correctamente aquí ni en `data-model.md`).
> Se agrega la regla de **contención temporal** (RF-072, §25 nueva): las tres asociaciones que la cascada de
> RF-061 revoca por dependencia (`ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`,
> `AsignaciónCredencial`) deben quedar temporalmente contenidas dentro de la vigencia de la
> `AsignaciónPersonaCompañía` vigente al momento de su creación — `AsignaciónTipoPersona` y `PermisoAcceso`
> quedan explícitamente excluidos de la contención, por no ser dependientes de la pertenencia según el
> modelo de dominio ya vigente (RF-011, RF-061). §7 (paso 6), §24 y el mecanismo de búsqueda de la cascada
> (§14.1) se corrigen para eliminar toda rama `FechaHoraFin IS NULL`, ya innecesaria. Ninguna cardinalidad,
> aislamiento entre Principales ni regla de revocación/cascada (RF-061 a RF-065) cambia — solo la
> obligatoriedad y contención de las fechas. Ver spec.md Clarifications para el detalle de las cuatro
> decisiones y el hallazgo de RF-021.
>
> **Nota de revisión (Sesión 2026-09-14, "renovación de AsignaciónPersonaCompañía" — décima revisión)**:
> cierra la Decisión Pendiente #9 (abierta por la revisión anterior). Se agrega §26: una tercera operación
> de escritura sobre `AsignaciónPersonaCompañía` — **renovación** — que extiende `FechaHoraFin` hacia
> adelante sin crear una nueva pertenencia, sin disparar la cascada de RF-061, y sin tocar
> `FechaHoraInicio`/`Estado`/`MotivoFin` ni ninguna asociación dependiente ya existente; solo aplica si
> `Estado = ACTIVA`. Es la contraparte exacta e inversa del cierre (`finalizar`, que acorta y dispara
> cascada). Ninguna entidad, columna ni migración nueva; nuevo endpoint dedicado en `contracts/people.yaml`.
> ~~**Nueva decisión pendiente, no resuelta aquí**: si la renovación aplica también a una pertenencia ya
> expirada dinámicamente — spec.md, Decisiones Pendientes #10.~~ **[RESUELTA — ver la revisión siguiente]**
>
> **Nota de revisión (Sesión 2026-09-14, "cierre Decisión Pendiente #10" — undécima revisión)**: acotada.
> Cierra la Decisión Pendiente #10: la renovación exige, además de `Estado = ACTIVA`, que la pertenencia
> siga vigente dinámicamente al momento de renovar (`fecha actual <= FechaHoraFin` ya declarada) — nunca
> puede usarse para puentear retroactivamente un vacío temporal ya transcurrido. §26 se amplía con esta
> restricción y su rationale. Ningún campo, entidad ni migración nueva.

## 1. Generación de UUID

> **Revisado (Sesión 2026-09-14, Stack Tecnológico Oficial)**: motor de base de datos confirmado como SQL
> Server. La decisión de fondo (generar en Aplicación, UUID v7) no cambia; se ajusta la terminología
> Postgres-específica y se añade la consideración de clustering de SQL Server (detallada en §20, ya que
> `uniqueidentifier` se compara con un orden de bytes distinto al de PostgreSQL).

- **Decision**: Los PKs son `Guid`, generados en la capa de Aplicación/Infraestructura mediante
  `Guid.CreateVersion7()` (UUID v7, disponible en .NET 9+), asignados antes del `SaveChanges` (no
  autogenerados por la base de datos). Se mapean a `uniqueidentifier` en SQL Server vía EF Core.
- **Rationale**: UUID v7 conserva monotonía temporal aproximada, lo que reduce la fragmentación de índices
  frente a UUID v4 aleatorio, mientras sigue cumpliendo el Principio II (no derivado de atributos de negocio,
  no editable, no reutilizable). Generarlo en la app (no `NEWID()`/`NEWSEQUENTIALID()` de SQL Server) evita
  depender de una función específica del motor y mantiene el ID disponible antes del INSERT para uso en
  lógica de aplicación (por ejemplo, relacionar `Persona` con sus históricos en la misma transacción). Ver
  §20 para el tratamiento específico de SQL Server sobre el índice clúster de estas columnas.
- **Alternatives considered**: `NEWSEQUENTIALID()` de SQL Server como default de columna (descartado: ata la
  generación de ID al motor de base de datos, no disponible antes del INSERT en el código de aplicación, y
  es específico de SQL Server — perdería portabilidad y el ID no estaría disponible para relacionar entidades
  en memoria antes de `SaveChanges`); UUID v4 vía `Guid.NewGuid()` (descartado: peor localidad de índice a la
  escala de 100,000+ personas de CS-002).

## 2. Autenticación y hashing de contraseñas

- **Decision**: Usar únicamente el componente `PasswordHasher<TUser>` de ASP.NET Core Identity (PBKDF2 con
  parámetros administrados por el framework) como servicio de hashing, sin adoptar el modelo de usuario ni
  las tablas completas de Identity. `Usuario` y `HistorialContraseña` son entidades propias del dominio.
  Autenticación basada en JWT de corta duración (access token) para la SPA, sin MFA (confirmado en
  Clarifications).
- **Rationale**: Reutiliza una implementación revisada y mantenida por Microsoft (cumple RF-003 "las
  contraseñas no pueden almacenarse en texto plano") sin arrastrar el esquema de tablas de ASP.NET Identity,
  que no encaja con el modelo de `Usuario` propio del dominio (estados ACTIVO/INACTIVO/BLOQUEADO, alcance de
  compañías). JWT evita mantener estado de sesión en servidor para una SPA desacoplada.
- **Alternatives considered**: BCrypt.Net / Argon2 vía librería de terceros (descartado: `PasswordHasher<T>`
  ya cumple el estándar y evita una dependencia adicional sin beneficio claro en este dominio); ASP.NET Core
  Identity completo (descartado: su modelo de usuario/roles no se alinea con `AlcanceUsuarioCompañía` ni con
  los estados de negocio requeridos); cookies de sesión con estado en servidor (descartado: complica escalar
  la API horizontalmente sin beneficio adicional para este caso de uso).
- **Política de contraseñas (decisión de trabajo, pendiente de confirmación de negocio)**: longitud mínima 10
  caracteres con al menos una mayúscula, una minúscula y un dígito; bloqueo tras 5 intentos fallidos
  consecutivos (`Usuario.Estado = BLOQUEADO`, requiere desbloqueo administrativo); expiración cada 90 días
  (`RequiereCambioPassword = true` al vencer, fuerza cambio en el siguiente login exitoso, RF-003 vía
  Historia 1 criterio 3); `HistorialContraseña` impide reutilizar cualquiera de las últimas 5 contraseñas.
  Estos umbrales deben quedar configurables (no hardcodeados) para ajuste sin despliegue de código.
- **Mecanismo de configuración (Sesión 2026-09-14, Stack Tecnológico Oficial)**: los umbrales de política de
  contraseña, la configuración de JWT (emisor, audiencia, clave de firma, tiempo de vida del token) y la
  zona horaria **global de respaldo** del sistema (por defecto `America/Lima`; la zona operativa de cada
  Compañía Principal vive en `Compañía.ZonaHorariaIana`, no en configuración — RF-080, §31) se exponen
  mediante el **Options Pattern** de ASP.NET Core
  (`IOptions<PasswordPolicyOptions>`, `IOptions<JwtOptions>`), enlazados desde `appsettings.json`/variables de
  entorno y validados al inicio con `ValidateOnStart()` + `IValidateOptions<T>` (o `DataAnnotations` sobre la
  clase de opciones) para fallar rápido si falta configuración obligatoria, en vez de fallar en el primer
  login. Es el mecanismo estándar de ASP.NET Core para exactamente este caso de uso (configuración
  fuertemente tipada, sin variables mágicas ni acceso directo a `IConfiguration` disperso en el código).

## 3. Alcance de compañías (autorización)

- **Decision**: `AlcanceUsuarioCompañía` se carga en el claim del JWT (lista de `CompañíaId`) al emitir el
  token; un `IAlcanceCompañiaAccessor` de request-scope expone ese conjunto a la capa de Aplicación. Todo
  repositorio/consulta que involucre datos filtrables por compañía aplica un filtro de consulta global de EF
  Core (`HasQueryFilter`) basado en ese accessor, y cada caso de uso valida explícitamente el alcance antes de
  escribir (defensa en profundidad: filtro de lectura + validación de escritura).
- **Rationale**: Cumple el Principio I: ningún dato fuera del alcance es alcanzable ni siquiera conociendo el
  ID del recurso, porque el filtro global actúa a nivel de consulta EF Core independientemente del punto de
  entrada. Duplicar la verificación en el caso de uso protege operaciones de escritura que no pasan por una
  consulta filtrada (p. ej. validar el `CompañíaId` de un payload de creación).
- **Alternatives considered**: Verificación manual por endpoint sin filtro global (descartado: alto riesgo de
  omisión en nuevos endpoints, no auditable de forma centralizada); autorización basada en políticas de
  ASP.NET Core (`IAuthorizationHandler`) sin filtro de consulta (descartado como único mecanismo: no impide
  fugas si un desarrollador olvida decorar un endpoint nuevo).
- **Extensión (corrección Compañía Principal/Contratista, RF-049)**: `UnidadOrganizativa` y `ÁreaAcceso` no
  tienen alcance de compañía propio de forma directa (`UnidadOrganizativa` no tiene ninguna FK hacia
  `Compañía` — RF-044; ver research.md §4). El filtro de alcance para estas dos entidades se resuelve así:
  - `ÁreaAcceso`: filtro directo por su columna `CompañíaPrincipalId` (igual mecanismo que Compañía/Persona).
  - `UnidadOrganizativa`: el filtro global de EF Core resuelve, para cada nodo, su Compañía Principal
    propietaria mediante un `JOIN` contra `CompañíaPrincipalUnidadOrganizativaRaiz` sobre el nodo raíz del
    árbol al que pertenece (calculado con el mismo CTE recursivo del helper de detección de ciclos —
    research.md §4). Esto evita duplicar el alcance en cada fila sin dejar de aplicar el Principio I.

## 4. Jerarquías y prevención de ciclos

- **Decision**: `UnidadOrganizativa` y `ÁreaAcceso` se modelan como listas de adyacencia (`ParentId` nullable
  autorreferenciado). Antes de insertar o reubicar un nodo, un servicio de dominio ejecuta una consulta CTE
  recursiva que recorre los ancestros del nodo destino; si el nodo que se intenta mover aparece entre esos
  ancestros (o es el mismo), la operación se rechaza con error explícito antes de confirmar.
- **Rationale**: La lista de adyacencia es el modelo más simple que satisface los requisitos (visualizar como
  árbol, expandir/contraer, evitar ciclos) sin la complejidad de mantenimiento de una tabla de clausura
  (closure table) o nested sets, que solo se justifican para lecturas jerárquicas masivas de solo lectura con
  reescrituras poco frecuentes — no es el patrón de uso aquí (edición frecuente de estructura organizativa).
  SQL Server soporta CTEs recursivos de forma nativa (`WITH cte AS (... UNION ALL ...)`, con `OPTION
  (MAXRECURSION n)` configurable si el árbol excede el límite por defecto de 100 niveles) y eficiente para
  árboles del tamaño esperado; EF Core 10 puede expresar el recorrido como una consulta LINQ traducida a CTE
  recursivo, o el equipo puede optar por SQL crudo (`FromSqlRaw`) si la traducción LINQ resulta insuficiente
  — decisión de implementación, no de diseño.
- **Alternatives considered**: Closure table (descartado: coste de mantenimiento en cada reubicación no se
  justifica al no requerir consultas de "todos los descendientes" a muy alta frecuencia); nested sets
  (descartado: reescritura costosa de rangos en cada inserción/movimiento, inadecuado para árboles editados
  con frecuencia).
- **Extensión (corrección Compañía Principal/Contratista)**: `UnidadOrganizativa` NO tiene ninguna columna ni
  FK directa hacia `Compañía` (restricción explícita de negocio — RF-044). Dado que ahora el sistema soporta
  varias Compañías Principales, cada una con su propio árbol de unidades organizativas aislado (RF-043,
  RF-045), la pertenencia se resuelve mediante una entidad de enlace independiente,
  `CompañíaPrincipalUnidadOrganizativaRaiz` (`CompañíaId`, `UnidadOrganizativaRaízId`), que vincula
  **únicamente** los nodos raíz (`UnidadSuperiorId IS NULL`) con su Compañía Principal propietaria. Un nodo
  no raíz hereda su Compañía Principal recorriendo `UnidadSuperiorId` hasta encontrar la raíz y consultando
  esa tabla de enlace — el mismo recorrido CTE recursivo ya usado para detectar ciclos se reutiliza para
  resolver "raíz de este nodo" en O(profundidad del árbol). Esto permite:
  1. Aislar completamente los árboles de distintas Compañías Principales sin tocar el esquema de
     `UnidadOrganizativa`.
  2. Validar en creación/reubicación que un nodo raíz solo se enlace a una Compañía `PRINCIPAL_MANDANTE`
     (nunca `CONTRATISTA` — RF-045) mediante un `CHECK`/validación de aplicación sobre la tabla de enlace.
  3. Aplicar el filtro de alcance de compañías (research.md §3) sin introducir la relación directa que el
     negocio prohibió explícitamente.
  `ÁreaAcceso`, en cambio, sí recibe una columna `CompañíaPrincipalId` directa (no hay restricción de negocio
  en contra para esta entidad — RF-046), validada para que cada área hija copie el valor de su área padre.
- **Alternatives considered (extensión)**: agregar la FK directamente en `UnidadOrganizativa` (más simple y
  con mejor rendimiento de consulta, pero descartado por violar explícitamente la restricción de negocio de
  no introducir una relación directa `UnidadOrganizativa → Compañía`); mantener un único árbol global sin
  ningún mecanismo de aislamiento (descartado: viola el Principio I, ya que ningún filtro de alcance podría
  aplicarse a `UnidadOrganizativa` y cualquier usuario autenticado vería/editaría las unidades organizativas
  de cualquier Compañía Principal).

## 5. Modelado temporal e históricos sin solapamiento

> **Revisado (Sesión 2026-09-14, Stack Tecnológico Oficial) — CONTRADICCIÓN RESUELTA**: la versión anterior
> de esta sección usaba `EXCLUDE USING gist` (extensión `btree_gist`), una restricción declarativa exclusiva
> de PostgreSQL sin equivalente en SQL Server. SQL Server no ofrece ninguna sintaxis nativa de "constraint de
> exclusión" ni un tipo `range` con operador de solapamiento (`&&`). Esto no es una diferencia cosmética de
> sintaxis — es una capacidad que SQL Server no tiene — por lo que se reemplaza por el patrón idiomático
> equivalente en SQL Server: un **trigger `AFTER INSERT, UPDATE`** por tabla particionada que verifica
> ausencia de solapamiento dentro de la misma partición y aborta la transacción (`THROW`/`ROLLBACK`) si lo
> detecta. El resto de la decisión (qué particiona cada entidad, cierre automático de la asignación previa,
> conversión de zona horaria) no cambia.

- **Decision**: Todas las columnas de fecha/hora se persisten como `datetime2(3)` en UTC (mayor precisión y
  rango que `datetime`; sin las ambigüedades históricas de `smalldatetime`/`datetime`). Las tablas de
  histórico con exclusividad usan un **trigger `AFTER INSERT, UPDATE`** que impide solapamientos a nivel de
  base de datos, con una **clave de partición distinta según la entidad** (corrección Contexto Operativo,
  ver §13):
  - `AsignaciónPersonaCompañía`: partición `PersonaId` — como máximo una compañía de pertenencia activa por
    persona a la vez (RF-014, sin cambios).
  - `RelaciónContratistaPrincipal`: partición `(CompañíaContratistaId, CompañíaPrincipalId)` — como máximo
    una relación activa por par Contratista-Principal, pero relaciones simultáneas con Principales distintas
    (RF-051).
  - `ContextoOperativoPersonaPrincipal`: partición `(PersonaId, CompañíaPrincipalId)` — como máximo un
    contexto activo por par persona-Principal, pero contextos simultáneos con Principales distintas
    (RF-052).
  - `AsignaciónPersonaUnidadOrganizativa`: partición `ContextoOperativoId` (no `PersonaId`) — como máximo
    una unidad organizativa activa por contexto operativo, pero asignaciones simultáneas en contextos
    distintos de la misma persona (RF-015, RF-055).
  - `AsignaciónCredencial` (cuando `Estado = ASIGNADO`): partición `(PersonaId, CompañíaPrincipalId)` —
    como máximo una credencial ASIGNADA activa por par persona-Principal, pero credenciales ASIGNADAS
    simultáneas en Principales distintas (RF-057).

  Forma general del trigger (una versión parametrizada por tabla/clave de partición/filtro de estado):

  ```sql
  CREATE TRIGGER trg_<Tabla>_NoSolapamiento
  ON <Tabla>
  AFTER INSERT, UPDATE
  AS
  BEGIN
      SET NOCOUNT ON;
      IF EXISTS (
          SELECT 1
          FROM inserted i
          JOIN <Tabla> t
              ON t.<ClavePartición> = i.<ClavePartición>   -- una o más columnas, según la entidad
             AND t.Id <> i.Id
             AND t.FechaHoraInicio < ISNULL(i.FechaHoraFin, '9999-12-31')
             AND i.FechaHoraInicio < ISNULL(t.FechaHoraFin, '9999-12-31')
             -- + filtro adicional de Estado cuando aplica (p. ej. AsignaciónCredencial: t.Estado = 'ASIGNADO')
      )
      BEGIN
          THROW 50001, 'Solapamiento de vigencia detectado para la misma partición.', 1;
      END
  END;
  ```

  Antes de insertar una nueva asignación vigente de `AsignaciónPersonaCompañía`,
  `RelaciónContratistaPrincipal`, `ContextoOperativoPersonaPrincipal` o
  `AsignaciónPersonaUnidadOrganizativa`, el caso de uso de Aplicación cierra automáticamente (fija
  `FechaHoraFin`) la asignación activa anterior dentro de la misma partición y transacción, en línea con
  Historia 5. **`AsignaciónCredencial` queda expresamente fuera de ese cierre automático** (Sesión
  2026-09-15, decisión A): una nueva asignación nunca cierra, finaliza, devuelve, elimina ni revoca la
  credencial `ASIGNADO` previa; si su período se solapa con el de otra credencial `ASIGNADO` de la misma
  persona y Compañía Principal, la operación se rechaza con 409 (RF-057) y la existente no se modifica. Sus
  estados terminales conservan una única causa cada uno — `DEVUELTO` (devolución física), `ELIMINADO` (baja
  administrativa), `REVOCADA` (exclusivamente la cascada de §14) — y no existe un estado ni motivo de
  "reemplazo" (§23, §24). En todos los casos el trigger es la última línea de defensa, no el mecanismo
  primario de UX (la aplicación valida primero y devuelve 409 con `ProblemDetails` legible; el trigger solo
  debería dispararse ante una condición de carrera genuina). La conversión de UTC a hora local ocurre
  únicamente en la evaluación de bloques horarios (Historia 8) y en la capa de presentación, y usa la zona
  horaria **de la Compañía Principal correspondiente** (`Compañía.ZonaHorariaIana`, NodaTime
  `DateTimeZoneProviders.Tzdb`), con la zona global de respaldo cuando el registro no es resoluble a una única
  Principal — RF-080, §31. *(Antes de la Sesión 2026-09-20 esta frase fijaba `America/Lima` para todo el
  sistema.)*
- **Rationale**: Un trigger a nivel de base de datos es la última línea de defensa contra condiciones de
  carrera (dos requests concurrentes) que la validación de aplicación por sí sola no puede garantizar,
  preservando exactamente la garantía que exigía el Principio IV con `EXCLUDE USING gist` en PostgreSQL —
  el mecanismo cambia, la garantía (invariante respaldado por la base de datos, no solo por la aplicación) no
  cambia. EF Core 10 gestiona el trigger como una migración SQL cruda (`migrationBuilder.Sql(...)`) dentro de
  `Up()`/`Down()`; no se modela como parte del modelo de EF Core (los triggers no tienen representación
  Fluent API nativa), pero sí se versiona junto con el resto del esquema.
- **Alternatives considered**: Restricción `CHECK` (descartada: SQL Server, igual que la mayoría de motores
  relacionales, no permite que un `CHECK` consulte otras filas — solo valida la fila que se está escribiendo,
  insuficiente para detectar solapamiento contra filas ya existentes); tablas temporales de sistema
  (`SYSTEM_VERSIONING`, `PERIOD FOR SYSTEM_TIME`) (descartadas: resuelven un problema distinto — versionado
  automático de UPDATE/DELETE gestionado por el motor — no vigencias de negocio elegidas explícitamente por
  el usuario con fechas arbitrarias, pasadas o futuras); validar solapamiento solo en la capa de aplicación
  con un `SELECT` previo, sin trigger (descartado como único mecanismo: vulnerable a condiciones de carrera
  bajo escrituras concurrentes — el motivo original para exigir un mecanismo de base de datos sigue vigente
  independientemente del motor); índice único filtrado (`CREATE UNIQUE INDEX ... WHERE FechaHoraFin IS
  NULL`) como único mecanismo (descartado: solo evita dos filas simultáneamente "abiertas" en la misma
  partición, pero no detecta solapamiento entre una fila con `FechaHoraFin` futura ya cerrada y una nueva fila
  cuyo rango se cruce con ella — cobertura parcial, insuficiente por sí sola; sigue siendo útil como
  optimización adicional de lectura, no como sustituto del trigger); `DateTime`/`DateTimeOffset` con
  `TimeZoneInfo` de .NET para reglas de horario (descartado frente a NodaTime: peor ergonomía y mayor riesgo
  de errores para aritmética de zonas horarias e intervalos, aunque Perú no observa horario de verano
  actualmente); mantener la partición `PersonaId` sola para
  `AsignaciónPersonaUnidadOrganizativa`/`AsignaciónCredencial` (descartado tras la corrección Contexto
  Operativo: impediría exactamente la simultaneidad multi-Principal que negocio exige — CS-013 a CS-017).

## 6. Auditoría automática

- **Decision**: Un `SaveChangesInterceptor` de EF Core intercepta `SavingChanges`, recorre las entidades
  rastreadas que implementan una interfaz común `IAuditable` (`CreatedAt`, `UpdatedAt`, `CreatedById`,
  `UpdatedById`) y estampa los valores usando la fecha UTC del servidor y el `UsuarioId` resuelto desde el
  contexto de autenticación de la request en curso. Los DTOs de entrada de la API nunca incluyen estos campos.
- **Rationale**: Centralizar el estampado en un interceptor garantiza cobertura 100% (CS-005) sin depender de
  que cada caso de uso lo invoque manualmente, cumpliendo el Principio III de forma estructural.
- **Alternatives considered**: Estampado manual en cada servicio de aplicación (descartado: alto riesgo de
  omisión en nuevas entidades); triggers de base de datos (descartado: no puede resolver de forma fiable
  "qué usuario autenticado" hizo el cambio sin pasar ese contexto explícitamente, lo que anula la ventaja
  frente al interceptor de EF Core).

## 7. Evaluación de acceso (Historia 8)

> **Revisado (Sesión 2026-09-14, corrección Contexto Operativo)**: reemplaza el algoritmo de 9 pasos de la
> versión anterior. Negocio entregó una lista de 13 pasos centrada en el nuevo modelo de Contexto Operativo
> (RF-059), que no repetía explícitamente dos verificaciones de la lista anterior — autorización de la
> propia consulta por alcance (RF-005) y elegibilidad de perfil/área (RF-024, Historia 7) — pero tampoco las
> contradecía ni las declaró fuera de alcance. Ambas siguen siendo requisitos vigentes del spec (RF-005,
> RF-024, Historia 7), por lo que se fusionaron en el algoritmo final en lugar de eliminarlas. Ver también
> spec.md Historia 8, nota de consolidación.
>
> **Revisado de nuevo (Sesión 2026-09-14, integración `ux-ui.md`)**: se agrega el paso 6 (credencial
> vigente), llevando el total de 13 a 14 pasos — ver RF-066 y spec.md Clarifications para el razonamiento
> completo de por qué se resolvió a favor de que la credencial sí gatille la denegación.
>
> **Revisado de nuevo (Sesión 2026-09-20, cierre de Etapa 1 — D4 y D5)**: se inserta el paso 5 (Compañía
> Principal `ACTIVO`, RF-079), llevando el total de 14 a **15 pasos**; el antiguo paso 5 (contexto operativo)
> pasa a ser el 6 y absorbe la verificación de que la compañía de pertenencia vigente de la persona también
> esté `ACTIVO`; y el paso de bloque horario (antiguo 12, ahora 13) deja de usar `America/Lima` de forma fija
> para resolver la zona de la Compañía Principal propietaria del área (RF-080). Los pasos 1 a 4 conservan su
> número; todos los posteriores se desplazan en uno. **Las referencias a números de paso escritas antes de
> esta sesión** —en las notas de revisión de este archivo, en las sesiones previas de spec.md y en los
> contratos— **deben leerse contra esa correspondencia**: antiguo 5 → 6 (contexto operativo), 6 → 7
> (credencial), 7 → 8 (área activa), 8 → 9 (perfil), 9 → 10 (unidad organizativa), 10 → 11 (permisos),
> 11 → 12 (vigencia), 12 → 13 (bloque horario), 13 → 14 (precedencia), 14 → 15 (conceder/denegar).

- **Decision**: Un servicio de dominio `EvaluadorDeAcceso` implementa, en orden, los siguientes 15 pasos:
  1. Verificar que el usuario autenticado que solicita la evaluación tenga, en su
     `AlcanceUsuarioCompañía`, la Compañía Principal que se determinará en el paso 4 (RF-005, RF-049;
     preservado de la lista anterior).
  2. Identificar a la persona evaluada.
  3. Identificar el `ÁreaAcceso` evaluada.
  4. Determinar la Compañía Principal propietaria del área vía `ÁreaAcceso.CompañíaPrincipalId`.
  5. Verificar que esa Compañía Principal tenga `Estado = ACTIVO` (RF-079, §30). Si está `INACTIVO` ⇒
     DENEGADO (`COMPANIA_INACTIVA`), sin evaluar el resto. Comprobación dinámica de solo lectura: no escribe
     ni modifica ningún registro dependiente, por lo que reactivar la compañía restablece el acceso sin
     ninguna otra operación.
  6. Verificar que exista un `ContextoOperativoPersonaPrincipal` vigente entre la persona y esa Principal en
     la fecha evaluada, **y que esa relación siga siendo legítima según la compañía de pertenencia vigente
     de la persona en esa misma fecha** (RF-061, §13 — auditoría de consistencia, cierra Decisión Pendiente
     #4): si esa compañía vigente es exactamente la Principal evaluada, la legitimidad es automática
     (RF-053); si es `CONTRATISTA`, verificar que la `RelaciónContratistaPrincipal` correspondiente esté
     vigente en esa misma fecha (RF-054, RF-059); en cualquier otro caso (p. ej. la persona cambió de
     compañía de pertenencia a una Contratista sin relación vigente con esta Principal, o a una Principal
     distinta) el contexto se considera no legítimo. Sin contexto vigente o sin legitimidad vigente ⇒
     DENEGADO (`SIN_CONTEXTO_OPERATIVO_VIGENTE`), sin evaluar el resto de los pasos. Esta re-validación es
     dinámica (recalculada en cada evaluación) y **no** cierra ni modifica el registro de
     `ContextoOperativoPersonaPrincipal` en sí. **Además** (Sesión 2026-09-20, D4, RF-079): la compañía de
     pertenencia vigente identificada en este mismo paso DEBE tener `Estado = ACTIVO`; si está `INACTIVO` ⇒
     DENEGADO con el mismo motivo `COMPANIA_INACTIVA` del paso 5 — también dinámico, sin escritura y
     reversible. Se agrupa aquí, y no como paso propio, porque este paso ya resuelve esa compañía para
     determinar la legitimidad del contexto.
  7. Verificar que exista una `AsignaciónCredencial` **vigente** para la persona y la Compañía Principal
     determinada en el paso 4, en la fecha evaluada (RF-066, RF-070, RF-071 — Sesión 2026-09-14, integración
     `ux-ui.md` y correcciones posteriores). "Vigente" es la conjunción de **ambas** condiciones, evaluadas
     dinámicamente en cada llamada: (a) `Estado = ASIGNADO`, y (b) `FechaHoraInicio <= fecha evaluada <=
     FechaHoraFin` (`FechaHoraFin` es obligatoria desde RF-071 — ya no existe la rama `IS NULL`). Sin una
     credencial que cumpla ambas (nunca asignada; en Estado `DEVUELTO`, `ELIMINADO` o `REVOCADA`; o
     `ASIGNADO` pero con `FechaHoraFin` ya pasado — temporalmente expirada) ⇒ DENEGADO
     (`SIN_CREDENCIAL_VIGENTE`), sin evaluar
     el resto de los pasos. El componente (a) reutiliza un campo ya escrito por la cascada de revocación
     (§14) sin necesitar ninguna entidad, columna ni migración nueva; el componente (b) es una comparación
     de fechas pura, igual que en cualquier otra entidad con vigencia temporal (§5) — ninguno de los dos
     escribe nada. Una credencial `ASIGNADO` cuya `FechaHoraFin` ya pasó **no** se transiciona
     automáticamente a otro `Estado` por este chequeo ni por el mero paso del tiempo (§24).
  8. Verificar que el área esté `ACTIVA`.
  9. Verificar que algún perfil (`TipoPersona`) vigente de la persona esté autorizado en el área (RF-024,
     Historia 7; preservado de la lista anterior).
  10. Determinar la unidad organizativa vigente de la persona dentro de ese contexto operativo (vía
      `AsignaciónPersonaUnidadOrganizativa` particionada por `ContextoOperativoId` — §5, §13).
  11. Recolectar permisos aplicables en los tres niveles: PERSONA (directo), UNIDAD_ORGANIZATIVA (la del
      paso 10) y COMPAÑÍA (la compañía de pertenencia vigente de la persona, sea Principal o Contratista —
      research.md §12).
  12. Filtrar por vigencia del permiso en la fecha evaluada.
  13. Filtrar por día de semana y bloque horario en la **zona horaria de la Compañía Principal propietaria
      del área** determinada en el paso 4 (`Compañía.ZonaHorariaIana`, RF-080, §31); si no fuera resoluble a
      una Principal, en la zona global de respaldo (`ZonaHoraria:TimeZoneId`). *(Antes de la Sesión
      2026-09-20 este paso usaba `America/Lima` de forma fija para todas las compañías.)*
  14. Si hay permisos aplicables en más de un nivel, aplicar precedencia PERSONA > UNIDAD_ORGANIZATIVA >
      COMPAÑÍA.
  15. Conceder o denegar. Ante cualquier paso sin resultado inequívoco, el resultado es DENEGADO
      (Principio I).

  El resultado y sus factores determinantes se pueden loguear para auditoría, sin bloquear la respuesta p95
  requerida por CS-003.
- **Rationale**: Un único servicio de dominio con pasos ordenados y explícitos permite pruebas unitarias
  aisladas por cada corte (compañía inactiva, sin contexto operativo, relación Contratista-Principal vencida,
  sin credencial vigente, área inactiva, perfil no autorizado, permiso vencido, fuera de bloque horario,
  conflicto de precedencia) tal como exige el Principio VII. Determinar la Principal en el paso 4 **antes** de
  tocar cualquier permiso (paso 11) garantiza que un permiso de la Principal B nunca pueda satisfacer una
  evaluación sobre un área de la Principal A (CS-018), porque los permisos ni siquiera se consultan hasta
  que el contexto operativo y la credencial con la Principal correcta fueron confirmados. Colocar el paso 7
  (credencial) inmediatamente después del paso 6 (contexto operativo) agrupa todas las verificaciones de
  legitimidad/identidad de la persona frente a esa Principal antes de entrar a evaluar elegibilidad de
  perfil/área y permisos — evita, por ejemplo, calcular la unidad organizativa vigente (paso 10) para una
  persona que de todas formas será denegada por falta de credencial. El paso 5 (Compañía Principal `ACTIVO`)
  se coloca inmediatamente después de determinar la Principal porque es el corte más barato de todos: una
  sola lectura de estado que evita resolver contexto, credencial, perfil y permisos de una compañía que no
  puede conceder acceso en absoluto.
- **Alternatives considered**: Reglas de precedencia implícitas por orden de consulta SQL (`ORDER BY` +
  `LIMIT 1`) sin servicio de dominio explícito (descartado: dificulta probar unitariamente cada corte y oculta
  la lógica de negocio en la capa de infraestructura); omitir el paso 1 (autorización de la propia consulta)
  y el paso 9 (elegibilidad de perfil/área) por no estar en la lista original de 13 pasos de negocio
  (descartado: ambos siguen siendo RF vigentes — RF-005 y RF-024 — que la lista de negocio no derogó
  explícitamente); colocar el paso de credencial al final del algoritmo, justo antes de conceder/
  denegar (descartado: dispersaría las verificaciones de legitimidad de identidad entre el principio y el
  final del algoritmo sin beneficio, y obligaría a calcular unidad organizativa y permisos — pasos 10 a 14 —
  para personas que de todas formas serán denegadas por falta de credencial, sin ganancia de claridad ni de
  rendimiento); plegar la verificación de `Compañía.Estado` dentro de los pasos 4 y 6 sin numerarla
  (descartado: la deja invisible en un algoritmo cuyo valor principal es que cada corte sea nombrable y
  probable por separado).

## 8. Semilla de datos maestros versionada (Perú)

- **Decision**: Los catálogos iniciales (`TipoDocumento`, `TipoSangre`, `Género`, `TipoPersona`,
  `TipoCredencial`) se cargan mediante migraciones de EF Core dedicadas (`Migrations/Seed/*`), cada una
  identificada por versión y aplicada una sola vez como parte del pipeline de migraciones estándar (no scripts
  SQL sueltos ejecutados manualmente). Los valores iniciales para Perú (p. ej. tipos de documento DNI, Carné
  de Extranjería, Pasaporte, RUC para compañía; tipos de sangre O+/O-/A+/A-/B+/B-/AB+/AB-; géneros
  Masculino/Femenino) quedan documentados en `data-model.md`.
- **Rationale**: Migraciones versionadas garantizan que el catálogo evolucione junto con el esquema, sea
  reproducible en cualquier entorno (incluidos Testcontainers de integración) y auditable en control de
  versiones, cumpliendo RF-031 y el Principio VI ("catálogos controlados y versionados").
- **Alternatives considered**: Script SQL manual ejecutado por el equipo de operaciones (descartado: no
  reproducible automáticamente en pruebas de integración ni en nuevos entornos); seed vía `HasData` de EF Core
  directamente en `OnModelCreating` (descartado: acopla el catálogo de datos al modelo y genera migraciones
  gigantes y difíciles de versionar incrementalmente frente a migraciones de seed dedicadas).

## 9. Identificador físico de credencial — RETIRADO (Sesión 2026-09-14, corrección Contexto Operativo)

- **Decision**: Se retira, sin reemplazo, el campo de trabajo `CódigoFisico` que una revisión anterior había
  incorporado en `AsignaciónCredencial`. `TipoCredencial` representa exclusivamente el tipo/diseño visual de
  la credencial (RF-058); ningún identificador físico, código de tarjeta, ni tecnología de identificación
  (RFID/QR/NFC/código de barras) se modela en esta fase.
- **Rationale**: Negocio corrigió explícitamente la interpretación de `TipoCredencial` y prohibió incorporar
  cualquier identificador físico adicional salvo que exista una decisión de negocio explícita que lo
  requiera (spec.md, Historia 9, Sesión 2026-09-14). El campo `CódigoFisico` era una "decisión de trabajo"
  propia (no una decisión de negocio confirmada) que llenaba el hueco de "Decisiones Pendientes" #2 del
  spec original; esa decisión pendiente queda ahora resuelta explícitamente como NO en spec.md.
- **Alternatives considered**: Mantener `CódigoFisico` como campo opcional no utilizado por ninguna regla de
  negocio (descartado: mantendría en el modelo un campo que negocio pidió explícitamente no introducir,
  violando la corrección recibida); agregar el campo de todos modos "por si acaso" (descartado: viola la
  guía de no modelar especulativamente más allá de lo solicitado).

## 10. Contrato de API y consistencia con el frontend

> **Revisado (Sesión 2026-09-14, Stack Tecnológico Oficial)**: reemplaza Swashbuckle (de terceros, nunca fue
> una decisión pedida por el usuario) por `Microsoft.AspNetCore.OpenApi`, el generador de documentos OpenAPI
> nativo de ASP.NET Core (disponible desde .NET 9, con soporte ampliado en .NET 10) — alineado con la
> directriz explícita de preferir capacidades estándar de la plataforma. El resto de la decisión no cambia.

- **Decision**: El contrato se documenta como OpenAPI 3.0.3 en `contracts/*.yaml`, un archivo por grupo
  funcional (auth, masters, companies, org-units, people, area-access, permissions, access-evaluation,
  credentials), generado y validado contra la implementación real de ASP.NET Core
  (`Microsoft.AspNetCore.OpenApi` vía `AddOpenApi()`/`MapOpenApi()`) en `ContractTests`. El cliente
  TypeScript del frontend se genera o se tipa manualmente a partir de estos esquemas para evitar divergencia.
- **Rationale**: Cumple RF-040 (contrato API explícito para los 7 grupos) y la regla de ingeniería de
  "consistencia entre API e interfaz"; separar por grupo funcional mantiene cada archivo legible y alineado
  1:1 con los namespaces de `Application/` y los `features/` del frontend. Usar el generador nativo evita una
  dependencia de terceros para una capacidad que la plataforma ya cubre (consistente con la directriz de no
  incorporar piezas no solicitadas — sección 8 de la decisión de stack).
- **Alternatives considered**: Un único archivo OpenAPI monolítico (descartado: dificulta la revisión y el
  mantenimiento incremental por módulo); documentación de contrato ad-hoc en Markdown sin esquema formal
  (descartado: no permite generación automática de pruebas de contrato ni de cliente tipado); Swashbuckle
  (descartado tras la decisión de stack: dependencia de terceros redundante frente al generador nativo de
  ASP.NET Core 10, sin capacidad adicional que el proyecto necesite).

## 11. Estrategia de pruebas automatizadas

> **Revisado (Sesión 2026-09-14, Stack Tecnológico Oficial)**: `Testcontainers.PostgreSql` reemplazado por
> `Testcontainers.MsSql` (imagen `mcr.microsoft.com/mssql/server` sobre Linux, consistente con el despliegue
> en Docker ya asumido). El resto de la decisión no cambia — sigue siendo indispensable una instancia real
> del motor, ahora por verificar el trigger de no-solapamiento de §5 en vez de `EXCLUDE`.

- **Decision**: `UnitTests` cubre reglas de dominio puras (precedencia de permisos, validación de bloques
  horarios, detección de ciclos en memoria) sin base de datos. `IntegrationTests` usa `Testcontainers.MsSql`
  para levantar una instancia real de SQL Server por corrida y verificar el trigger de no-solapamiento (§5),
  filtros de alcance de compañía, y auditoría end-to-end contra el `DbContext` real. `ContractTests` valida
  que cada endpoint implementado cumple el esquema publicado en `contracts/` (incluido el formato
  `ProblemDetails` de error — §21). En el frontend, Playwright cubre los flujos críticos P1 (login, jerarquía
  de 3 niveles + permiso horario de CS-009, búsqueda de personas dentro de alcance).
- **Rationale**: Satisface el Principio VII (NO NEGOCIABLE) exigiendo cobertura explícita de denegación por
  defecto, ciclos, solapamientos y fuga entre compañías en el nivel de prueba donde cada uno se detecta mejor
  (unitario para reglas puras, integración para restricciones de base de datos y filtros, e2e para el flujo de
  usuario completo). Los triggers de SQL Server (§5) no tienen representación en el modelo de EF Core y por
  tanto **no pueden verificarse con un proveedor en memoria** (`UseInMemoryDatabase`) ni con SQLite — deben
  probarse contra el motor real, reforzando la necesidad de `Testcontainers.MsSql`.
- **Alternatives considered**: Mockear la base de datos en pruebas de integración (descartado explícitamente:
  el trigger de no-solapamiento y los filtros de consulta global de EF Core solo se verifican de forma fiable
  contra un motor SQL Server real); `UseInMemoryDatabase` de EF Core (descartado: no ejecuta triggers, no
  aplica restricciones de unicidad/clave foránea con la misma semántica que SQL Server, y puede ocultar
  errores de traducción LINQ→SQL específicos del proveedor); SQL Server LocalDB (descartado como mecanismo de
  CI: solo disponible en Windows, incompatible con pipelines Linux/contenedores — válido únicamente como
  atajo de desarrollo local opcional, nunca como fuente de verdad de `IntegrationTests`).

## 12. Clasificación de Compañía y relación operacional Contratista↔Principal

> **DECISIÓN REVERTIDA (Sesión 2026-09-14, corrección Contexto Operativo)**: la versión original de esta
> sección rechazaba explícitamente una entidad `RelaciónContratistaPrincipal`. Negocio corrigió esta
> decisión: dicha entidad SÍ es necesaria y obligatoria. Se documenta abajo la decisión revertida (tachada,
> para trazabilidad) y la decisión vigente.

- ~~**Decision (retirada)**: No se modela una entidad separada de "relación contractual" entre una Compañía
  Contratista y una o varias Compañías Principales a nivel de compañía. La relación operacional queda
  capturada exclusivamente mediante el histórico de unidad organizativa de la persona
  (`AsignaciónPersonaUnidadOrganizativa`).~~
- **Decision (vigente)**: `Compañía` incorpora un campo obligatorio `TipoCompañía` (enum:
  `PRINCIPAL_MANDANTE`, `CONTRATISTA`) — sin cambios respecto a la decisión original. Adicionalmente, SÍ se
  modela una entidad explícita `RelaciónContratistaPrincipal` a nivel de compañía (ver §13), y una entidad
  explícita `ContextoOperativoPersonaPrincipal` a nivel de persona (ver §13) que contiene la asignación de
  unidad organizativa. El histórico de compañía de pertenencia (`AsignaciónPersonaCompañía`, RF-014,
  RF-047) sigue siendo independiente de estas dos nuevas entidades y de las asignaciones que dependen de
  ellas.
- **Rationale de la reversión**: negocio identificó que "Persona → Compañía → Principal" (la cadena que
  sustentaba la decisión original) es insuficiente para representar el contexto operativo real: una persona
  de una Contratista puede trabajar simultáneamente para varias Principales, y sin una relación explícita
  Contratista↔Principal no hay forma de restringir ni de consultar de forma eficiente "para qué Principales
  puede trabajar el personal de esta Contratista" (necesario para CS-019 y para poblar el selector de
  Principal de la pantalla de asignación — spec.md Historia 5, Caso B). Derivarlo transitivamente desde
  asignaciones de unidad organizativa ya existentes (como proponía la decisión original) no permite
  **restringir** qué Principales son válidas antes de que exista ninguna asignación — es decir, no puede
  servir de validación previa, solo de consulta posterior, lo cual es insuficiente para RF-054/CS-019.
- **Alternatives considered (revisión)**: mantener la decisión original y derivar la relación
  Contratista↔Principal únicamente a partir de asignaciones de unidad organizativa existentes (descartado:
  no permite validar/restringir antes de la primera asignación, ver Rationale); modelar la relación como un
  campo `PrincipalId` único dentro de `Compañía` Contratista (descartado explícitamente por negocio: una
  Contratista puede prestar servicios a varias Principales simultáneamente — sección 3 de la corrección).
  Se mantiene sin cambios la decisión de no restringir `PermisoAcceso.CompañíaId` (alcance COMPAÑÍA) a solo
  compañías `PRINCIPAL_MANDANTE` — sigue siendo un caso de uso legítimo (research.md §7, paso 11, donde se
  recolectan los permisos de nivel COMPAÑÍA; la referencia anterior apuntaba al paso 9, que resuelve la unidad
  organizativa — corregido en la Sesión 2026-09-20).

## 13. Contexto Operativo Persona↔Principal y Relación Contratista↔Principal (Sesión 2026-09-14)

- **Decision**: Se agregan dos entidades nuevas:
  1. **`RelaciónContratistaPrincipal`** (`CompañíaContratistaId`, `CompañíaPrincipalId`,
     `FechaHoraInicio`, `FechaHoraFin`): declara que una Compañía Contratista presta servicios a una
     Compañía Principal durante una vigencia determinada. `CompañíaContratistaId` DEBE referenciar una
     compañía `CONTRATISTA`; `CompañíaPrincipalId` DEBE referenciar una compañía `PRINCIPAL_MANDANTE`.
     Partición de exclusividad: `(CompañíaContratistaId, CompañíaPrincipalId)` — ver §5.
  2. **`ContextoOperativoPersonaPrincipal`** (`PersonaId`, `CompañíaPrincipalId`, `FechaHoraInicio`,
     `FechaHoraFin`): declara que una persona tiene una relación operativa vigente con una Compañía
     Principal. Partición de exclusividad: `(PersonaId, CompañíaPrincipalId)` — ver §5. Es el contenedor
     lógico de la asignación de unidad organizativa de esa persona para esa Principal
     (`AsignaciónPersonaUnidadOrganizativa.ContextoOperativoId`, FK obligatoria).

  Validaciones de creación de un `ContextoOperativoPersonaPrincipal`:
  - La persona DEBE tener una `AsignaciónPersonaCompañía` vigente en ese momento (decisión de diseño no
    solicitada explícitamente por negocio, pero necesaria como ancla para las dos reglas siguientes — ver
    nota de alcance abajo).
  - Si esa compañía vigente es `PRINCIPAL_MANDANTE`: `CompañíaPrincipalId` DEBE ser exactamente esa misma
    compañía (RF-053, CS-020); cualquier otro valor se rechaza.
  - Si esa compañía vigente es `CONTRATISTA`: DEBE existir una `RelaciónContratistaPrincipal` vigente entre
    esa Contratista y `CompañíaPrincipalId` en ese momento (RF-054, CS-019); en caso contrario se rechaza.

  `AsignaciónCredencial` (§5, §9) se modifica para incluir `CompañíaPrincipalId` (FK obligatoria a
  `Compañía`, debe ser `PRINCIPAL_MANDANTE`) en lugar del retirado `CódigoFisico`; se valida que exista un
  `ContextoOperativoPersonaPrincipal` vigente entre la persona y esa Principal en el momento de la
  asignación (misma naturaleza de validación que para la unidad organizativa, pero sin FK directa al
  contexto — ver Alternatives).
- **Rationale**: Satisface directamente CS-012 a CS-024. Usar una FK real (`ContextoOperativoId`) en
  `AsignaciónPersonaUnidadOrganizativa` (en vez de solo validar en tiempo de escritura) permite que la
  restricción `EXCLUDE` de §5 se aplique sobre una columna real almacenada, evitando depender de un trigger
  o columna calculada para partir la exclusión por "unidad organizativa dentro de este contexto". Para
  `AsignaciónCredencial`, en cambio, `CompañíaPrincipalId` ya es, por sí solo, una columna real utilizable
  directamente en la partición de exclusión (RF-057) sin necesitar la FK al contexto — se usa el campo tal
  como lo especifica negocio (spec.md, Historia 9, sección 12 de la corrección) en vez de introducir una FK
  adicional no solicitada.
- **Nota de alcance (decisión de diseño no explícitamente solicitada)**: la exigencia de que la persona
  tenga una `AsignaciónPersonaCompañía` vigente para poder abrir un `ContextoOperativoPersonaPrincipal` es
  una inferencia de diseño (necesaria para poder aplicar RF-053/RF-054, que dependen de conocer el
  `TipoCompañía` vigente de la persona), no una regla dictada literalmente por negocio. Se documenta aquí
  para que quede sujeta a confirmación si negocio tuviera un caso de uso distinto (p. ej. abrir un contexto
  operativo antes de registrar la compañía de pertenencia).
- **Alternatives considered**: agregar `ContextoOperativoId` también como FK obligatoria en
  `AsignaciónCredencial` en lugar de `CompañíaPrincipalId` directo (descartado: negocio especificó
  literalmente el campo `CompaniaPrincipalId` para esta entidad — spec.md Historia 9 sección 12 — y no hay
  necesidad técnica de la FK indirecta ya que `CompañíaPrincipalId` es suficiente para particionar la
  exclusión); cerrar automáticamente en cascada los contextos operativos, asignaciones de unidad
  organizativa y credenciales de una persona cuando cambia su compañía de pertenencia (descartado, ver
  resolución abajo).

### 13.1 Resolución de Decisiones Pendientes #4 y #5 (Sesión 2026-09-14, auditoría final de consistencia)

- **Decisión Pendiente #4 — cambio de compañía de pertenencia — RESUELTA**: NO se cierra nada en cascada.
  `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`, `PermisoAcceso` y
  `AsignaciónCredencial` permanecen íntegros como históricos, sin ninguna escritura automática disparada por
  un cambio en `AsignaciónPersonaCompañía`. En su lugar, la re-validación de legitimidad ya prevista para
  personas de una Contratista (§7 paso 5, antes de esta sesión) se **generaliza simétricamente** también a
  personas empleadas directamente por una Principal (RF-061): en cada evaluación de acceso, el sistema
  recalcula, usando la compañía vigente de la persona **en ese momento**, si su contexto operativo sigue
  siendo legítimo. Un cambio de empleador no cierra el contexto, pero si el nuevo empleador no sostiene la
  legitimidad (no es la misma Principal, o es una Contratista sin relación vigente con ella), la evaluación
  deniega igual que si el contexto no existiera. Esto resuelve el riesgo de seguridad identificado (un
  contexto automático que sobrevive indefinidamente a la salida de la persona de la Principal) sin requerir
  cascada de escritura alguna — coherente con el principio de auditoría/histórico íntegro de la Constitución
  (Principio IV: "NO DEBE realizarse eliminación física de historial") y con el patrón de denegación por
  defecto ya establecido (Principio I). No se requieren cambios de esquema: ninguna entidad nueva, ninguna
  columna nueva; el cambio es puramente de lógica de evaluación.
- **Decisión Pendiente #5 — exclusividad del contexto operativo — RESUELTA**: se confirma la exclusividad
  estricta por par `(PersonaId, CompañíaPrincipalId)` ya definida en la §13 original. Ninguna regla de
  negocio de ninguna de las dos sesiones de corrección describe, ni siquiera insinúa, un escenario de dos
  contextos simultáneos con la misma Principal — todos los ejemplos de simultaneidad (Pedro García,
  CS-013 a CS-017) son consistentemente entre Principales **distintas**. Mantener la exclusividad estricta
  es, por tanto, la lectura más fiel de las reglas ya establecidas, no una restricción adicional impuesta.
  Si en el futuro negocio identifica un caso real de múltiples roles simultáneos con una misma Principal
  (p. ej. dos nombramientos distintos), la extensión natural sería agregar un discriminador de rol al par de
  partición (`PersonaId, CompañíaPrincipalId, RolId`) — pero eso constituiría un requisito nuevo, no una
  corrección de lo ya especificado, y queda fuera de alcance mientras no se solicite.

## 14. Revocación automática por cese de pertenencia (Sesión 2026-09-14, corrección Revocación Automática)

> **Reversión de §13.1**: la resolución previa de la Decisión Pendiente #4 ("ningún registro se cierra en
> cascada; solo re-validación dinámica") queda **reemplazada**. Negocio corrigió que la revocación en
> cascada, con escritura real de estado, es obligatoria — la re-validación dinámica se conserva como defensa
> adicional (research.md §7 paso 5, sin cambios de fondo), pero deja de ser el único mecanismo.

### 14.1 Mecanismo: escritura en cascada, no solo lectura dinámica

- **Decision**: Cuando se cierra (fija `FechaHoraFin`) la `AsignaciónPersonaCompañía` vigente de una
  persona — por cese explícito o porque una nueva asignación la reemplaza automáticamente (mecánica ya
  existente desde Historia 5) —, el mismo caso de uso de Aplicación, en la misma transacción, ejecuta una
  cascada de escritura sobre:
  1. Todo `ContextoOperativoPersonaPrincipal` de esa persona con `FechaHoraFin` posterior a la nueva fecha
     efectiva del cese (desde la Sesión 2026-09-14 "vigencia temporal jerárquica", `FechaHoraFin` es
     obligatoria — RF-071 — por lo que esta condición ya no incluye una rama `IS NULL`).
  2. Todo `AsignaciónPersonaUnidadOrganizativa` cuyo `ContextoOperativoId` apunte a uno de los contextos
     recién revocados y que también esté vigente.
  3. Toda `AsignaciónCredencial` de esa persona en `Estado = ASIGNADO` cuyo `CompañíaPrincipalId` coincida
     con uno de los contextos recién revocados.

  Para cada fila afectada: `FechaHoraFin = MIN(FechaHoraFin actual, FechaHoraFin efectiva del cese)` (nunca
  se extiende una fecha ya fijada antes), `Estado` pasa a su valor de cierre correspondiente,
  `MotivoFin` se fija según la causa, y se agrega una FK `RevocadoPorPertenenciaId` apuntando a la
  `AsignaciónPersonaCompañía` que originó la cascada. `FechaHoraInicio` nunca se toca.
- **Rationale**: Una escritura real (no solo una relectura en tiempo de evaluación) es lo que el negocio
  pidió explícitamente ("no solamente una condición visual de la UI"): permite auditar de forma directa y
  performante "qué fue revocado y por qué" sin tener que re-derivar el estado en cada consulta, y deja un
  registro explícito incluso para vistas que no pasan por el motor de evaluación de acceso (p. ej. un
  listado administrativo de credenciales). Ejecutar la cascada en la MISMA transacción que el cierre de la
  pertenencia (en vez de un job asíncrono) es más simple, evita ventanas de inconsistencia, y es viable
  porque el volumen por persona es pequeño (a lo sumo unos pocos contextos simultáneos por persona, RF-052).
- **Alternatives considered**: cascada vía job en segundo plano/cola de eventos (descartado: introduce
  ventana de inconsistencia entre el cierre de la pertenencia y la revocación de sus dependientes, y
  complejidad operativa —reintentos, orden de procesamiento— no justificada por el volumen); trigger de
  base de datos (descartado por el mismo motivo ya usado en research.md §6 para auditoría: no puede resolver
  de forma fiable metadatos de aplicación como `MotivoFin` sin pasar ese contexto explícitamente, y esta
  cascada además necesita ejecutar la misma lógica de "no extender FechaHoraFin" que ya vive en la capa de
  aplicación); depender solo de la re-validación dinámica sin ninguna escritura (la decisión previa,
  descartada explícitamente por negocio).

### 14.2 Campos nuevos: `Estado`, `MotivoFin`, `RevocadoPorPertenenciaId`

- **Decision**: Se agregan campos a cuatro entidades:
  - `AsignaciónPersonaCompañía`: `Estado` (enum `ACTIVA`, `FINALIZADA`), `MotivoFin` (enum nullable:
    `CESE_PERTENENCIA`, `REEMPLAZO_ASIGNACION`).
  - `ContextoOperativoPersonaPrincipal` y `AsignaciónPersonaUnidadOrganizativa`: `Estado` (enum `ACTIVO`,
    `INACTIVO`), `MotivoFin` (enum nullable: `REVOCACION_CESE_PERTENENCIA`, `REEMPLAZO_ASIGNACION`,
    `CIERRE_MANUAL`), `RevocadoPorPertenenciaId` (Guid nullable, FK → `AsignaciónPersonaCompañía`; solo se
    puebla cuando `MotivoFin = REVOCACION_CESE_PERTENENCIA`).
  - `AsignaciónCredencial`: se agrega el valor `REVOCADA` al `Estado` ya existente (`ASIGNADO`, `DEVUELTO`,
    `ELIMINADO`, `REVOCADA`) y el campo `RevocadoPorPertenenciaId` (mismo patrón). No se agrega `MotivoFin`
    aquí: el propio valor `REVOCADA` del `Estado` ya existente cumple esa función sin duplicar información.
- **Rationale — por qué `Estado` es administrativo/informativo y NO la fuente de verdad para autorización**:
  en todas estas entidades, "¿está vigente ahora?" para efectos de conceder acceso se sigue determinando
  **exclusivamente** comparando `FechaHoraInicio`/`FechaHoraFin` contra la fecha evaluada (igual que en todo
  el resto del dominio — Principio IV). `Estado` no gatilla ni bloquea autorización por sí solo; existe para
  que la UI y los reportes de auditoría puedan mostrar "por qué terminó" sin tener que re-derivarlo, y para
  que la cascada pueda escribir la disposición final de un registro incluso cuando su `FechaHoraFin` efectiva
  es futura (ver §14.3) sin que eso implique bloquear el acceso antes de tiempo. Esta separación evita el
  riesgo de que dos fuentes de verdad (fechas vs. `Estado`) diverjan y produzcan un bloqueo prematuro.
- **Rationale — `RevocadoPorPertenenciaId`**: satisface directamente el requisito de auditoría "debe poder
  determinarse qué contextos/asignaciones/credenciales fueron afectados" por una pertenencia específica, sin
  necesitar heurísticas de fecha/compañía aproximadas. Es un campo nuevo, pero mínimo (una sola columna FK
  nullable por entidad) y justificado por un requisito de auditoría explícito, no especulativo.
- **Alternatives considered**: usar solo `FechaHoraFin` sin ningún `Estado` explícito (descartado: no
  permite distinguir "terminó porque fue reemplazada" de "terminó porque fue revocada en cascada" — negocio
  pidió explícitamente distinguir el motivo); crear una entidad de log/evento de revocación separada en vez
  de campos en cada entidad afectada (descartado: los campos `CreatedAt/UpdatedAt/UpdatedById` que cada
  entidad ya tiene por el interceptor de auditoría (research.md §6) más `MotivoFin`/`RevocadoPorPertenenciaId`
  ya responden todas las preguntas de auditoría exigidas — "cuándo ocurrió" = `UpdatedAt`; "quién/qué lo
  causó" = `UpdatedById` del usuario que registró el cese + `RevocadoPorPertenenciaId` — sin necesitar una
  tabla nueva); añadir `MotivoFin` también a `AsignaciónCredencial` (descartado: redundante, `Estado =
  REVOCADA` ya es autoexplicativo).

### 14.3 Efectividad temporal: sin distinguir fecha administrativa de fecha efectiva

- **Decision**: `FechaHoraFin` (campo ya existente en las cuatro entidades) es, sin ambigüedad, la fecha
  efectiva de finalización — no se introduce ningún campo adicional para distinguir "cuándo se registró el
  cese" de "cuándo surte efecto". Cuando un cese se registra con `FechaHoraFin` futura, la cascada (§14.1)
  se ejecuta de inmediato (en la misma transacción del registro), propagando esa misma fecha futura a los
  registros dependientes — no se agenda ningún proceso para el futuro. Como la vigencia efectiva para
  autorización sigue siendo siempre una comparación de fechas (§14.2), el registro dependiente permanece
  genuinamente utilizable hasta que esa fecha futura llegue, exactamente como pide el ejemplo de negocio
  ("hoy 15/09, fin 30/09 ⇒ vigente hasta el 30/09").
- **Rationale**: Cumple explícitamente la instrucción de negocio "no inventes campos innecesarios si el
  modelo existente ya puede representar correctamente esta regla" — `FechaHoraFin` ya es exactamente ese
  campo. Ejecutar la propagación de inmediato (no en el futuro) evita necesitar un scheduler o job
  recurrente (infraestructura no solicitada y no justificada por el dominio).
- **Alternatives considered**: distinguir `FechaAdministrativa` (cuándo se registró) de `FechaEfectiva`
  (cuándo surte efecto) como campos separados (descartado explícitamente: el negocio pidió documentar esta
  decisión solo *si* el modelo existente no bastaba, y `FechaHoraFin` ya cumple el rol de fecha efectiva sin
  ambigüedad; `CreatedAt`/`UpdatedAt` de auditoría ya cumplen el rol de "cuándo se registró la acción");
  job programado que aplique la revocación exactamente en el instante futuro (descartado: innecesario dado
  que la vigencia ya es siempre derivada por fecha, no por un flag que deba conmutarse en el momento exacto).

### 14.4 Alcance de la cascada: por pertenencia, no por `PersonaId`

> **Corrección de redacción (Sesión 2026-09-14, corrección Modelo de Cardinalidad Definitivo)**: el texto
> original de esta subsección, aunque técnicamente correcto, estaba redactado de forma que podía
> malinterpretarse como si RF-014 limitara el número de `ContextoOperativoPersonaPrincipal` simultáneos.
> Nunca lo hizo. Se reescribe para que sea inequívoco: **una persona puede tener varios contextos operativos
> abiertos a la vez (uno por Principal, RF-052) — la cascada los alcanza a todos precisamente porque todos
> dependen de la única pertenencia activa que RF-014 permite, no porque solo pueda existir un contexto.**

- **Decision**: La cascada (§14.1) alcanza únicamente los registros que dependen de la
  `AsignaciónPersonaCompañía` específica que se cierra — nunca "todo lo de esta persona" sin condición. En
  el momento del cese, la persona puede tener **cero, uno o varios** `ContextoOperativoPersonaPrincipal`
  abiertos simultáneamente (uno por cada Compañía Principal con la que su compañía de pertenencia tuviera
  relación vigente — RF-052). Todos y cada uno de esos contextos abiertos dependen necesariamente de la
  misma (única) pertenencia que finaliza, porque RF-014 impide que exista, al mismo tiempo, una segunda
  `AsignaciónPersonaCompañía` activa de la que alguno de ellos pudiera depender en su lugar. Por tanto la
  cascada los alcanza a **todos** — sean uno o varios —, y cualquier pertenencia **posterior** de la misma
  persona (tras un cambio de compañía) abre contextos nuevos, vinculados a esa nueva pertenencia, no
  afectados por revocaciones previas. **RF-014 acota la cardinalidad de `AsignaciónPersonaCompañía`
  únicamente; en ningún momento acota la cardinalidad de `ContextoOperativoPersonaPrincipal`, que sigue
  gobernada solo por RF-052 (sin límite de Principales simultáneas).**
- **Nota sobre el Ejemplo 3 de negocio**: el ejemplo ilustrativo entregado por negocio para esta regla
  mostraba a una persona perteneciendo simultáneamente a dos compañías ("Servicios ACME" y "Servicios DEF"),
  lo cual contradice RF-014 (ya establecido, sin cambios, y no cuestionado en esta corrección). La lección
  que el ejemplo buscaba transmitir — la revocación no debe alcanzar contextos ajenos a la pertenencia que
  termina — permanece válida y correctamente implementada por el mecanismo de esta sección; queda
  correctamente ejemplificada por el Ejemplo 2 de negocio (una Contratista con relaciones vigentes
  simultáneas hacia varias Principales, donde ambos contextos resultantes se revocan juntos al terminar esa
  única pertenencia), no por el Ejemplo 3. Ver spec.md, Clarifications, para la nota completa.
- **Rationale**: Aplica RF-062 tal como negocio lo pidió ("la revocación debe determinarse por dependencia
  de la pertenencia concreta que finaliza, no simplemente por PersonaId"), y la propiedad de exclusividad de
  RF-014 (sobre `AsignaciónPersonaCompañía`, no sobre contextos) hace que la implementación sea, en la
  práctica, más simple de lo que el enunciado general sugiere: no se necesita ningún algoritmo de "trazar
  dependencias" complejo, basta con "todos los contextos abiertos de esta persona en este instante"
  (potencialmente varios) — porque, por construcción, ninguno de ellos puede depender de una pertenencia
  distinta y también vigente al mismo tiempo.
- **Alternatives considered**: revocar por `PersonaId` sin ninguna noción de dependencia (descartado
  explícitamente por negocio — "no simplemente por PersonaId" — aunque el resultado práctico bajo RF-014 sea
  equivalente para el caso vigente-al-cierre, la semántica explícita de "depende de esta pertenencia" es la
  que permite auditar correctamente y la que se preserva vía `RevocadoPorPertenenciaId`); limitar
  `ContextoOperativoPersonaPrincipal` a uno por persona por analogía con RF-014 (descartado explícitamente:
  contradice RF-052, el ejemplo de negocio de Pedro con Principal A y B simultáneas, y CS-013/CS-030 — fue
  precisamente la lectura errónea que esta sesión corrige).

### 14.5 Fuera de alcance de esta corrección

> **Corregida (Sesión 2026-09-20, cierre de Etapa 1 — D4)**. La versión original de esta subsección afirmaba
> que la re-validación dinámica «ya existente» cubría ambos disparadores y «deniega acceso correctamente».
> **Eso era falso para el caso de la `Compañía` marcada `INACTIVO`**: el algoritmo de §7 nunca consultaba
> `Compañía.Estado` en ningún paso, por lo que inactivar una compañía no denegaba nada. La auditoría de
> cierre de Etapa 1 lo detectó y D4 lo resolvió. El texto se reescribe abajo para reflejar qué quedó resuelto
> y qué sigue realmente fuera de alcance.

- **Decision (vigente)**: La cascada de escritura de §14.1 sigue limitada al cese de una pertenencia
  Persona–Compañía. De los dos disparadores que esta subsección dejaba fuera:
  - **(a) Fin de una `RelaciónContratistaPrincipal`** — sigue **fuera de alcance**: no tiene cascada de
    escritura equivalente y permanece protegido únicamente por la re-validación dinámica del paso 6 de §7
    (RF-059, RF-065), que sí está implementada y sí deniega correctamente cuando la relación no está vigente
    en la fecha evaluada. Extender la cascada a este caso seguiría requiriendo una decisión de negocio
    explícita (Decisión Pendiente #6, parte no resuelta).
  - **(b) `Compañía` marcada `INACTIVO`** — **resuelto por D4 (RF-079, §30)**: tampoco lleva cascada de
    escritura, pero ya no depende de una protección inexistente. La evaluación de acceso verifica
    explícitamente `Estado = ACTIVO` de la Compañía Principal propietaria del área (paso 5) y de la compañía
    de pertenencia vigente de la persona (paso 6), denegando con `COMPANIA_INACTIVA`. El efecto es inmediato,
    reversible y sin escritura alguna sobre registros dependientes.
- **Rationale**: Negocio especificó la cascada de §14.1 exclusivamente para el cese de pertenencia
  Persona–Compañía, y extenderla a otros disparadores sin pedido explícito sería modelado especulativo — eso
  no ha cambiado. Lo que cambió es la honestidad del texto: la ausencia de cascada solo es aceptable si existe
  de verdad un mecanismo alternativo que deniegue, y para la compañía inactivada ese mecanismo no existía
  hasta RF-079. La lección queda registrada: afirmar que «la evaluación dinámica ya lo cubre» exige
  verificar que el algoritmo consulte efectivamente el campo en cuestión.

## 15. Motor de base de datos oficial: SQL Server (Sesión 2026-09-14, Stack Tecnológico Oficial)

- **Decision**: SQL Server es el motor de base de datos relacional oficial del proyecto (decisión
  arquitectónica explícita del usuario, no negociable). PostgreSQL — que hasta esta sesión era un *supuesto*
  documentado en `spec.md` ("Stack recomendado"), nunca una decisión ratificada — queda descartado. Entity
  Framework Core 10 (proveedor `Microsoft.EntityFrameworkCore.SqlServer`) es el ORM oficial; LINQ es el
  mecanismo normal de consulta; no se introduce un repositorio genérico adicional solo por abstracción — los
  casos de uso de Aplicación consumen `DbContext`/`DbSet<T>` directamente (o detrás de interfaces de
  Aplicación específicas de caso de uso cuando eso ayuda a las pruebas unitarias, no un `IRepository<T>`
  genérico sin valor añadido).
- **Impacto en decisiones previas** (cada una detallada en su propia sección): generación de UUID (§1, sin
  cambio de fondo), CTEs recursivos para jerarquías (§4, soportado nativamente, sin cambio de fondo),
  **mecanismo de no-solapamiento temporal (§5, cambio real: trigger en vez de `EXCLUDE USING gist`)**,
  generación de OpenAPI (§10, cambia a `Microsoft.AspNetCore.OpenApi`), pruebas de integración (§11,
  `Testcontainers.MsSql`). Se agregan además decisiones que no eran relevantes bajo el diseño anterior:
  concurrencia optimista (§16), value conversions de enums (§17), distinción entre autorización de
  plataforma y motor de evaluación de dominio (§18), health checks (§19), clustering de PK GUID (§20), y
  formato de error HTTP `ProblemDetails` (§21, reemplaza el `ErrorResponse` propio de los 10 contratos).
- **Rationale**: El usuario fijó el stack como decisión explícita y no interpretable ("no la interpretes
  como una sugerencia"); no hay lugar para evaluar alternativas de motor de base de datos en esta sección —
  la única decisión de diseño real que quedaba pendiente era *cómo traducir* cada decisión ya tomada bajo el
  supuesto de PostgreSQL al motor oficial, no *si* usar SQL Server.
- **Alternatives considered**: No aplica (decisión de negocio no negociable); se documentan las alternativas
  de *traducción* de cada mecanismo específico en la sección correspondiente (§5 en particular, por ser la
  única con una contradicción real no resoluble por simple renombrado).

## 16. Concurrencia optimista

- **Decision**: Las entidades sujetas a escritura concurrente por distintos administradores — `Usuario`,
  `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`, `AsignaciónCredencial`, `PermisoAcceso`,
  `RelaciónContratistaPrincipal` — incluyen una columna `RowVersion` (`byte[]`, mapeada a `rowversion` de SQL
  Server vía `[Timestamp]`/`.IsRowVersion()` de EF Core). EF Core incluye automáticamente esta columna en la
  cláusula `WHERE` de cada `UPDATE`/`DELETE`; si otra transacción modificó la fila entretanto, EF Core lanza
  `DbUpdateConcurrencyException`, que la API traduce a `409 Conflict` con `ProblemDetails` (§21). El resto de
  entidades (catálogos maestros, entidades de solo-lectura administrativa infrecuente) no la incluyen.
- **Rationale**: Varias operaciones críticas de este dominio son inherentemente propensas a condiciones de
  carrera entre administradores — p. ej. dos usuarios cerrando la misma pertenencia simultáneamente
  (disparando la cascada de revocación dos veces), o un administrador revocando una credencial mientras otro
  la está devolviendo. `rowversion` es el mecanismo nativo e idiomático de SQL Server para concurrencia
  optimista (equivalente funcional a `xmin` de PostgreSQL, pero expuesto como tipo de columna de primera
  clase); usarlo evita mecanismos manuales (comparar `UpdatedAt` a mano, con el riesgo de colisión de
  precisión de reloj) y se integra de forma nativa con EF Core.
- **Alternatives considered**: Comparar `UpdatedAt` manualmente antes de escribir (descartado: `rowversion`
  es más preciso —incrementa en cada escritura, no depende de la resolución del reloj del servidor— y ya
  viene soportado de forma nativa por EF Core); bloqueo pesimista (`SELECT ... WITH (UPDLOCK, HOLDLOCK)`)
  (descartado como mecanismo general: retiene locks más tiempo del necesario para operaciones administrativas
  de baja frecuencia; se reserva como técnica puntual únicamente si el trigger de no-solapamiento de §5
  mostrara contención real bajo prueba de carga); aplicar `RowVersion` a todas las entidades sin excepción
  (descartado: sobre-ingeniería para catálogos maestros de escritura infrecuente sin riesgo real de colisión).

## 17. Value conversions de enums de negocio

- **Decision**: Todos los enums de negocio (`TipoCompañía`, `Estado` de las distintas entidades, `MotivoFin`,
  `EstadoCredencial`, `AlcancePermiso`, `DíaSemana`, etc.) se persisten como `nvarchar` (longitud acotada al
  valor más largo del enum, p. ej. `nvarchar(30)`) mediante `HasConversion<string>()` de EF Core, no como
  enteros. Se valida además con un `CHECK` de base de datos que restrinja los valores permitidos donde el
  motor lo permita razonablemente (o se confía en la validación de Aplicación cuando el mantenimiento del
  `CHECK` no compense el beneficio, p. ej. enums con muchos valores que cambian con frecuencia).
- **Rationale**: Persistir como texto hace que el contenido de la base de datos sea legible directamente
  (auditoría, soporte, consultas ad-hoc) sin necesitar una tabla de referencia para decodificar enteros, y
  elimina el riesgo de "renumerar" un enum por accidente al agregar/reordenar valores en el código (un bug
  clásico de enums respaldados por `int` cuando el orden de declaración cambia). El costo de espacio
  adicional es insignificante frente al beneficio de legibilidad y seguridad frente a este dominio, donde los
  valores de `Estado`/`MotivoFin` son precisamente lo que un auditor necesita leer directamente.
- **Alternatives considered**: Enums respaldados por `tinyint`/`int` (descartado: más compacto, pero ilegible
  sin diccionario de traducción y frágil ante reordenamientos accidentales del enum en el código); tablas de
  referencia (`Estados`, `Motivos`) con FK (descartado: sobre-ingeniería para conjuntos de valores fijos,
  pequeños y que no requieren mantenimiento administrativo — a diferencia de los catálogos maestros
  genuinos como `TipoDocumento`, que sí son administrables y sí ameritan tabla propia).

## 18. Autorización de plataforma (ASP.NET Core Authorization) vs. motor de evaluación de acceso de dominio

> Distinción explícitamente señalada como importante por el usuario: no confundir la autenticación/
> autorización del **usuario de la aplicación** (un administrador que inicia sesión) con el **motor de
> evaluación de acceso físico** del dominio (que decide si una **Persona** — una entidad de negocio, no un
> principal de ASP.NET Core — obtiene acceso a un `ÁreaAcceso`).

- **Decision**: Se usan dos mecanismos distintos y no intercambiables:
  1. **ASP.NET Core Authentication + Authorization** (JWT Bearer — research.md §2 — más `Policies`/`Claims`)
     protege el acceso a los **endpoints de la API** por parte del **Usuario administrativo autenticado**:
     autenticación (RF-001/RF-034), y una política de autorización personalizada
     (`CompaniaScopeRequirement` + `IAuthorizationHandler`) que valida, a partir del claim `alcanceCompanias`
     del JWT, que la `CompañíaId`/`CompañíaPrincipalId` objetivo de la operación esté dentro del alcance del
     usuario (RF-005, RF-049, RF-060) — como gate de primera línea a nivel de endpoint, ANTES de tocar la
     base de datos. El filtro de consulta global de EF Core (research.md §3) sigue siendo la segunda línea
     de defensa (defensa en profundidad ya establecida, sin cambios).
  2. El **motor de evaluación de acceso** (`EvaluadorDeAcceso`, el servicio de dominio de 15 pasos —
     research.md §7) es un servicio de **Domain/Application** invocado explícitamente por el endpoint
     `POST /api/evaluacion-acceso`; NO se modela como una `AuthorizationPolicy` de ASP.NET Core, porque
     evalúa si una **Persona** (una entidad de negocio evaluada, nunca el `ClaimsPrincipal` de la request
     HTTP) tiene acceso físico a un área — una decisión de negocio con su propio flujo de 15 pasos,
     precedencia y denegación por defecto, no una decisión de "¿puede este usuario llamar a este endpoint?".
- **Rationale**: ASP.NET Core Authorization está diseñado para responder "¿puede el `ClaimsPrincipal` actual
  de la request realizar esta acción?" — encaja naturalmente para el alcance administrativo (1), donde el
  actor evaluado ES el usuario autenticado. No encaja para (2): ahí el actor evaluado es una `Persona` de
  negocio, casi siempre *distinta* del `Usuario` administrativo que dispara la consulta, con un algoritmo de
  15 pasos que ya tiene sus propias pruebas unitarias por corte (research.md §7). Forzar (2) dentro de
  `AuthorizationPolicy` mezclaría dos conceptos con ciclos de vida y pruebas distintas, y complicaría (sin
  necesidad) razonar sobre CS-003 (evaluación p95 < 500ms), ya que las políticas de ASP.NET Core no están
  pensadas para lógica de negocio de esa complejidad ni para ser invocadas fuera del pipeline HTTP (p. ej.
  desde una prueba unitaria pura de dominio, sin `HttpContext`).
- **Alternatives considered**: Modelar `EvaluadorDeAcceso` como una `AuthorizationPolicy`/`IAuthorizationHandler`
  de ASP.NET Core (descartado explícitamente por la distinción que el usuario señaló como importante: mezcla
  autenticación del usuario de la aplicación con evaluación de acceso físico del dominio; complica las
  pruebas unitarias puras de `EvaluadorDeAcceso`, que no deben depender de `HttpContext`/`ClaimsPrincipal`);
  no usar ASP.NET Core Authorization en absoluto e implementar el gate de alcance de compañías (1) a mano en
  cada controlador (descartado: ASP.NET Core Authorization ya resuelve esto de forma estándar y centralizada
  — usarlo es exactamente "preferir las capacidades estándar de ASP.NET Core" que pide la decisión de stack).

## 19. Health checks

- **Decision**: Se usa `Microsoft.Extensions.Diagnostics.HealthChecks` con
  `AspNetCore.HealthChecks.SqlServer` (paquete de la comunidad AspNetCore.Diagnostics.HealthChecks,
  ampliamente adoptado) para verificar conectividad a SQL Server. Se exponen dos endpoints: `/health/live`
  (liveness — el proceso está corriendo, sin dependencias externas) y `/health/ready` (readiness — incluye el
  chequeo de SQL Server), ambos sin autenticación (son para el orquestador/balanceador, no para usuarios).
- **Rationale**: El stack oficial pide explícitamente "ASP.NET Core Health Checks para health/readiness
  checks"; separar liveness de readiness es la práctica estándar para que un orquestador (Docker
  Compose/Kubernetes-si-aplicara-a-futuro) no reinicie el contenedor solo porque la base de datos está
  temporalmente inalcanzable (readiness fallida ≠ proceso roto).
- **Alternatives considered**: Un único endpoint `/health` sin distinguir liveness/readiness (descartado:
  pierde la distinción operacional estándar sin ganar nada a cambio); health check personalizado sin el
  paquete de la comunidad (descartado: reinventar una verificación de conectividad SQL Server ya resuelta y
  probada por una librería ampliamente usada en el ecosistema ASP.NET Core).

## 20. Índice clúster para claves primarias GUID en SQL Server

- **Decision**: Para las entidades de alto volumen de inserción — los "históricos" (`AsignaciónPersonaCompañía`,
  `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial`,
  `RelaciónContratistaPrincipal`, `HistorialContraseña`) — la clave primaria `Id` (`uniqueidentifier`) se
  configura como **`PRIMARY KEY NONCLUSTERED`**, y se agrega un índice **clúster** sobre `CreatedAt`
  (columna de auditoría ya existente, monotónicamente creciente por ser estampada por el reloj del
  servidor). Para el resto de entidades (catálogos maestros, `Compañía`, `Persona`, `Usuario`, `PermisoAcceso`,
  etc. — bajo volumen de inserción relativo) se acepta el comportamiento por defecto de EF Core/SQL Server
  (PK como clúster).
- **Rationale**: SQL Server, a diferencia de PostgreSQL, ordena físicamente las filas de una tabla según su
  índice clúster (por defecto, la PK). Insertar GUIDs no secuenciales como clave de un índice clúster es un
  problema de rendimiento conocido y bien documentado en SQL Server (page splits, fragmentación) — UUID v7
  mitiga esto parcialmente por su monotonía temporal aproximada, pero SQL Server compara `uniqueidentifier`
  en un orden de bytes distinto al de la generación secuencial (no compara de izquierda a derecha como
  PostgreSQL), por lo que la mitigación es menor de lo que sería en PostgreSQL. Clusterizar por `CreatedAt`
  en las tablas de alta frecuencia de inserción (las que reciben una fila nueva por cada evento de negocio:
  altas de pertenencia, apertura de contexto, asignación de credencial) evita el problema en las tablas donde
  más importa, sin necesitar una columna sustituta de solo-clustering (`IDENTITY`/`SEQUENCE`) que complicaría
  el modelo sin necesidad. Las tablas de bajo volumen no lo necesitan — el costo de una PK-clúster GUID ahí
  es insignificante frente al beneficio de simplicidad de dejar el comportamiento por defecto.
- **Alternatives considered**: Agregar una columna `IDENTITY`/`SEQUENCE` interna (`RowId bigint`) solo para
  clustering, manteniendo `Id` (`Guid`) como PK lógica no-clúster en todas las tablas (descartado: introduce
  una columna técnica adicional sin propósito de negocio en todas las entidades, cuando clusterizar por
  `CreatedAt` —ya existente— resuelve el mismo problema en las tablas donde realmente importa, sin el campo
  extra); aceptar el comportamiento por defecto (PK-clúster GUID) en todas las tablas (descartado para las
  tablas de histórico de alto volumen: riesgo de fragmentación real a la escala de CS-002 —100,000+
  personas, con varios históricos por persona—; aceptable para catálogos y entidades de bajo volumen, donde
  sí se mantiene); `NEWSEQUENTIALID()` de SQL Server como generador (descartado ya en §1: ata la generación a
  la base de datos y pierde disponibilidad del ID antes del INSERT).

## 21. Formato de error HTTP: ProblemDetails (RFC 7807/9457)

> **Revisado (Sesión 2026-09-14, Stack Tecnológico Oficial) — CONTRADICCIÓN RESUELTA**: los 10 archivos de
> `contracts/*.yaml` definían un esquema de error propio, `ErrorResponse { codigo, mensaje, detalles[] }`,
> incompatible con `ProblemDetails`, que el stack oficial exige explícitamente. Se reemplaza en los 10
> contratos.

- **Decision**: Toda respuesta de error HTTP usa `ProblemDetails` (`Microsoft.AspNetCore.Mvc.ProblemDetails`,
  vía `AddProblemDetails()` + el middleware de excepciones de research.md §"Alcance de compañías" adaptado
  para producir este formato) con esta forma:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc9457",
    "title": "Solapamiento de vigencia",
    "status": 409,
    "detail": "Ya existe una asignación vigente para esta persona en el período indicado.",
    "instance": "/api/personas/{id}/historial-companias",
    "codigo": "SOLAPAMIENTO_VIGENCIA"
  }
  ```
  `codigo` es una extensión propia (`ProblemDetails.Extensions`) — un código de error de negocio estable y
  legible por máquina (para que el frontend distinga casos sin parsear `detail`, que es texto para humanos).
  Para errores de validación de campos (400), se usa `ValidationProblemDetails`, que añade `errors: {
  "campo": ["mensaje1", "mensaje2"] }`. El esquema `ErrorResponse` de los 10 contratos se renombra a
  `ProblemDetails` con esta forma exacta.
- **Rationale**: RFC 7807/9457 es el estándar de facto para errores HTTP en APIs REST, y ASP.NET Core lo
  soporta de forma nativa desde .NET 7 (con generación automática para errores no controlados desde .NET 8).
  Usarlo en vez de un envelope propio cumple la directriz explícita del stack, y da interoperabilidad gratis
  con herramientas de terceros que ya entienden el formato (algunos clientes HTTP, gateways, herramientas de
  observabilidad).
- **Alternatives considered**: Mantener el envelope propio `ErrorResponse` (descartado: contradice
  explícitamente la directriz de stack); usar `ProblemDetails` sin la extensión `codigo` (descartado: el
  frontend necesita un identificador estable y legible por máquina para manejar casos específicos —p. ej.
  mostrar un mensaje de UI distinto para `SOLAPAMIENTO_VIGENCIA` vs. `RELACION_CONTRATISTA_PRINCIPAL_VENCIDA`—
  sin tener que analizar el texto de `detail`, que es para humanos y puede cambiar de redacción).

## 22. Consultas agregadas/transversales: Auditoría, Históricos e indicadores operativos (Sesión 2026-09-14,
integración `ux-ui.md`)

> **Nueva sección**: formaliza la Decisión 2 resuelta en spec.md, Clarifications, Sesión "integración
> `ux-ui.md`" (RF-067 a RF-069, Historia 10). No introduce ninguna entidad, regla de cardinalidad, vigencia
> ni revocación nueva — expone, mediante endpoints dedicados, datos que Historia 5, Historia 10 y RF-026/
> RF-027 ya exigían que existieran.

- **Decision**: Se exponen tres capacidades de consulta server-side nuevas, cada una filtrando siempre por
  el alcance de compañías del usuario autenticado (Principio I, igual que cualquier otro endpoint):
  1. **Auditoría transversal** (RF-067): consulta agregada sobre los campos de auditoría
     (`CreatedAt/UpdatedAt/CreatedById/UpdatedById`) ya presentes en toda entidad (Principio III), filtrable
     por rango temporal, usuario, persona, compañía, Compañía Principal, entidad y acción.
  2. **Históricos transversales** (RF-068): consulta agregada sobre `AsignaciónPersonaCompañía`,
     `ContextoOperativoPersonaPrincipal` y `AsignaciónPersonaUnidadOrganizativa` (Historia 5), filtrable por
     persona, compañía, Compañía Principal, entidad y tipo de evento — distinta de las consultas de
     histórico ya existentes por persona individual (`contracts/people.yaml`), que se mantienen sin cambios.
  3. **Indicadores operativos agregados** (RF-069): conteos y próximos vencimientos (personas activas,
     Contratistas activas, Principales activas, credenciales próximas a vencer, relaciones/pertenencias
     próximas a finalizar) para el Dashboard (`ux-ui.md` §8).
  El contenido exacto de cada pantalla ya está descrito en `ux-ui.md` §8, §20 y §21; esta sección no lo
  duplica. La forma concreta de los endpoints (uno consolidado vs. varios, paginación, límites de rango de
  fechas) es una decisión de diseño de `contracts/` a resolver en una futura extensión de `tasks.md`, fuera
  de alcance de esta sección.
- **Rationale**: El Principio I exige que toda lectura respete el alcance de compañías del usuario en el
  servidor. Una pantalla que agrega datos de varias entidades/compañías **no puede** resolverse componiendo
  en el cliente los endpoints ya existentes sin antes filtrar por alcance en cada uno — y aun haciéndolo,
  ensamblar y correlacionar en el navegador miles de registros (CS-002: ≥100,000 personas) violaría los
  objetivos de rendimiento (CS-002, CS-003) y expondría al cliente más datos de los necesarios para
  renderizar un conteo o un filtro. Endpoints dedicados permiten aplicar el filtro de alcance una sola vez,
  en el servidor, con los índices adecuados.
- **Alternatives considered**: Composición en el cliente sobre los endpoints ya existentes por entidad
  (descartado: viola el Principio I en cuanto la vista combine compañías/Principales distintas, y no escala
  a CS-002); un único endpoint "god query" que devuelva todo el estado agregado del sistema en una sola
  llamada (descartado: dificulta paginar y filtrar independientemente auditoría/históricos/indicadores, que
  tienen formas y volúmenes de datos muy distintos); modelar un log de auditoría como una entidad de dominio
  nueva con su propia tabla de eventos (descartado por ahora: los campos de auditoría ya obligatorios en
  cada entidad — Principio III — son suficientes como fuente de la consulta transversal; una tabla de
  eventos dedicada quedaría como posible evolución futura si el volumen o la granularidad lo justifican,
  sin necesidad de decidirlo en esta sesión).

## 23. Confirmación: `AsignaciónCredencial` no requiere un campo `MotivoFin` propio (Sesión 2026-09-14,
integración `ux-ui.md`)

> Formaliza la Decisión 3 resuelta en spec.md, Clarifications. No modifica `data-model.md` ni
> `contracts/credentials.yaml`.

- **Decision**: `AsignaciónCredencial` conserva únicamente `Estado` (`ASIGNADO`/`DEVUELTO`/`ELIMINADO`/
  `REVOCADA`) y `RevocadoPorPertenenciaId`, sin agregar un `MotivoFin` propio, a diferencia de
  `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal` y `AsignaciónPersonaUnidadOrganizativa`.
- **Rationale**: RF-063 exige distinguir explícitamente un "motivo de finalización" en los registros
  afectados por la cascada de RF-061. En las otras tres entidades, `Estado` es genérico (ACTIVA/FINALIZADA
  o ACTIVO/INACTIVO) y no distingue por sí solo la causa (cese de pertenencia vs. reemplazo de asignación
  vs. cierre manual), de ahí la necesidad de un `MotivoFin` separado con varios valores posibles. En
  `AsignaciónCredencial`, en cambio, `Estado` ya es específico por causa: `REVOCADA` solo puede significar
  "finalizó por la cascada de RF-061" (no hay, en el diseño vigente, ninguna otra causa que produzca ese
  valor), distinto de `DEVUELTO` (devolución física voluntaria) y `ELIMINADO` (baja lógica administrativa).
  `Estado` por sí solo, combinado con `RevocadoPorPertenenciaId` (que ya identifica la pertenencia de
  origen), satisface el literal (d) de RF-063 para esta entidad sin necesitar una columna redundante.
- **Alternatives considered**: Agregar `MotivoFin` a `AsignaciónCredencial` por simetría estructural con las
  otras tres entidades (descartado: sería una columna sin ningún valor informativo adicional al que ya
  aporta `Estado`, dado que hoy existe una única causa posible de `REVOCADA`; se revisará si negocio
  introduce explícitamente una segunda causa de revocación no derivada de RF-061).

## 24. Vigencia temporal de `AsignaciónCredencial`, independiente de `Estado` (Sesión 2026-09-14, auditoría
de consistencia RF-066; corregida en la Sesión "vigencia temporal jerárquica")

> Formaliza la decisión de negocio resuelta en spec.md, Clarifications (RF-070, RF-071). **Revisión**: la
> versión original de esta sección (Sesión "auditoría de consistencia RF-066") permitía `FechaHoraFin =
> null` como "vigencia indefinida/abierta". La Sesión "vigencia temporal jerárquica" **eliminó esa opción
> por completo**: `FechaHoraFin` ahora es obligatoria (nunca `null`, nunca fecha centinela) desde la
> creación, para `AsignaciónCredencial` y para el resto de asociaciones temporales de una persona (RF-071).
> Esta sección se corrige para reflejarlo.

- **Decision**: `AsignaciónCredencial.FechaHoraFin` DEBE tener un valor real y conocido desde la creación
  (p. ej. una "Credencial Temporal", Historia 9, o cualquier duración que negocio determine) — nunca `null`.
  Una `AsignaciónCredencial` es **vigente** para efectos de RF-066 si y solo si se cumple la conjunción:
  `Estado = ASIGNADO` **y** `FechaHoraInicio <= fecha evaluada <= FechaHoraFin`. Si `Estado = ASIGNADO` y
  `FechaHoraFin` ya pasó, la credencial está temporalmente expirada — deniega el paso 7 de §7 (paso 6 antes de
  la renumeración de la Sesión 2026-09-20) — pero el
  sistema **NO** transiciona su `Estado` automáticamente por el mero paso del tiempo; solo una acción
  administrativa explícita (devolución, baja lógica, o la cascada de revocación de §14) cambia `Estado`, y
  esa acción sigue siendo la única que puede acortar `FechaHoraFin` por cierre (nunca extenderla) y la única
  que registra `RevocadoPorPertenenciaId`. Adicionalmente, su ventana completa DEBE estar contenida dentro
  de la vigencia de la `AsignaciónPersonaCompañía` vigente de la persona al momento de su creación (RF-072,
  §25).
- **Rationale**: Esta es exactamente la misma semántica que RF-063 ya establece para las otras entidades
  revocables (`AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`,
  `AsignaciónPersonaUnidadOrganizativa`): `Estado` es administrativo/informativo; la vigencia efectiva para
  autorización se determina siempre comparando fechas, nunca por `Estado` en aislamiento (Principio IV).
  Mantener a `FechaHoraFin` y `Estado` como dos fuentes de información distintas y conjuntas (nunca una
  sustituyendo a la otra) evita una segunda fuente de verdad divergente sobre "¿esta credencial produce
  acceso ahora mismo?", igual que ya se decidió para las demás entidades en §14. La obligatoriedad de
  `FechaHoraFin` (RF-071) es una decisión de negocio explícita posterior, no derivada de esta sección —
  exige que toda credencial declare su fin de vigencia desde el momento de la asignación, sin excepción.
- **Alternatives considered**: Permitir `FechaHoraFin = null` como vigencia indefinida (descartado en la
  Sesión "vigencia temporal jerárquica": negocio determinó que ninguna asociación temporal de persona debe
  tener vigencia indefinida); usar una fecha centinela como `9999-12-31` en vez de `null` (descartado
  explícitamente por negocio — RF-071 — por ser una forma indirecta de representar exactamente lo que se
  quiso eliminar); modelar la expiración temporal como una tarea programada que transicione `Estado`
  automáticamente al vencer (descartado: contradice explícitamente el punto 7 de la decisión de negocio —
  "el sistema NO debe cambiar automáticamente el Estado por el mero paso del tiempo" — y además introduciría
  un job en segundo plano y una ventana de inconsistencia entre el vencimiento real y su procesamiento, sin
  ningún beneficio sobre la comparación de fechas dinámica ya usada en toda evaluación de acceso); agregar
  un tercer campo booleano o un `Estado` adicional tipo `EXPIRADO` (descartado: duplicaría la fuente de
  verdad ya disponible en `FechaHoraFin`, y contradice el mandato explícito de negocio de que `Estado` no
  cambia por vencimiento temporal).

## 25. Contención temporal de las asociaciones dependientes de `AsignaciónPersonaCompañía` (Sesión
2026-09-14, "vigencia temporal jerárquica")

> Formaliza RF-072 (spec.md). Corrige RF-048 y las secciones de `data-model.md` de
> `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y `AsignaciónCredencial`, que
> afirmaban (en distintas redacciones) que no existía validación cruzada con el histórico de compañía de
> pertenencia. No introduce ninguna entidad, migración estructural ni cambio de cardinalidad.

- **Decision**: `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y
  `AsignaciónCredencial` — las tres asociaciones que la cascada de RF-061 revoca por depender de una
  `AsignaciónPersonaCompañía` — DEBEN validar, al crearse, que su ventana temporal esté contenida dentro de
  la vigencia de la `AsignaciónPersonaCompañía` vigente de la persona en ese momento:
  `FechaHoraInicio_hija >= FechaHoraInicio_pertenencia` **Y** `FechaHoraFin_hija <= FechaHoraFin_pertenencia`.
  No se exige igualdad — una hija puede finalizar antes que la pertenencia. La validación se ejecuta en la
  capa de **Aplicación** (no es un `CHECK` de SQL Server porque cruza tablas: `ContextoOperativoPersonaPrincipal`/
  `AsignaciónCredencial` referencian a `AsignaciónPersonaCompañía` solo indirectamente por `PersonaId`, y
  `AsignaciónPersonaUnidadOrganizativa` ni siquiera tiene una FK directa a ella). `AsignaciónTipoPersona` y
  `PermisoAcceso` quedan explícitamente fuera de esta regla — el modelo de dominio no las establece como
  dependientes de la pertenencia (RF-011, RF-061).
- **Rationale**: Es la validación cruzada exacta que RF-048 negaba hasta esta sesión, ahora exigida
  explícitamente por negocio. Se resuelve en Aplicación, no en base de datos, siguiendo el mismo patrón ya
  usado para RF-053/RF-054 (validación de legitimidad Principal/Contratista al abrir un contexto operativo,
  también cruzada entre `AsignaciónPersonaCompañía` y las entidades de Historia 5) — mantiene toda la lógica
  de negocio de vigencia jerárquica en un único lugar (`Application/People`), consistente con la Regla de
  Arquitectura e Ingeniería de la Constitución de mantener la lógica de dominio fuera de la infraestructura.
  Esta validación es complementaria, no redundante, con el acortamiento que la cascada de RF-061/RF-064 ya
  aplica cuando la pertenencia se cierra después de que la hija existe (§14.1): la contención de esta
  sección se valida **al crear** la hija contra la pertenencia vigente en ese momento; la cascada ajusta las
  hijas **después**, si la pertenencia se cierra anticipadamente.
- **Alternatives considered**: Un `CHECK CONSTRAINT` de SQL Server con una subconsulta a
  `AsignaciónPersonaCompañía` (descartado: SQL Server no permite subconsultas en `CHECK CONSTRAINT`, solo
  expresiones sobre la misma fila — se necesitaría un trigger adicional, duplicando lógica que ya vive mejor
  en Aplicación, donde además puede producir un `ProblemDetails` con mensaje claro en vez de un error SQL
  crudo); extender la contención también a `AsignaciónTipoPersona`/`PermisoAcceso` "por consistencia"
  (descartado explícitamente por negocio: la obligatoriedad de `FechaHoraFin`, RF-071, y la contención,
  RF-072, son conceptos distintos — ninguna regla de negocio establece que estas dos entidades dependan
  temporalmente de la pertenencia, y no se inventa esa dependencia). **[Decisión del Baseline, ampliada
  post-Baseline por VF-007: RF-082 aplica la contención a `AsignaciónTipoPersona` y a `PermisoAcceso` con
  alcance PERSONA, sin incorporarlas a la cascada. Ver §35.]**

## 26. Renovación de `AsignaciónPersonaCompañía` (Sesión 2026-09-14, "renovación de
AsignaciónPersonaCompañía")

> Formaliza RF-073 (spec.md), que cierra la Decisión Pendiente #9 abierta por la Sesión "vigencia temporal
> jerárquica". No introduce ninguna entidad, columna ni migración nueva.

- **Decision**: Se agrega una tercera operación de escritura sobre `AsignaciónPersonaCompañía`, distinta de
  la creación (con reemplazo automático de la anterior) y del cierre (cese explícito): la **renovación**,
  que extiende `FechaHoraFin` hacia una fecha estrictamente posterior a la ya vigente, sin crear ningún
  registro nuevo, sin disparar la cascada de RF-061, y sin tocar `FechaHoraInicio`, `Estado` ni `MotivoFin`.
  Solo aplica si `Estado = ACTIVA` **y** la pertenencia sigue vigente dinámicamente en el momento de renovar
  (`fecha actual <= FechaHoraFin` ya declarada — Sesión "cierre Decisión Pendiente #10") — una pertenencia
  `FINALIZADA`, o una `ACTIVA` ya expirada dinámicamente sin cierre administrativo, NO es renovable en
  ningún caso (requiere una nueva `AsignaciónPersonaCompañía`, preservando el histórico secuencial ya
  establecido, RF-014). Las asociaciones
  dependientes ya existentes (`ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`,
  `AsignaciónCredencial`) no se tocan; el único efecto es ampliar el techo temporal que la contención de
  RF-072 permitirá para asociaciones creadas después de la renovación. Se expone como un endpoint dedicado
  (`POST .../historial-companias/{id}/renovar`, `contracts/people.yaml`), paralelo a
  `.../finalizar`, pero semánticamente inverso: `finalizar` acorta y cierra (`Estado → FINALIZADA`,
  dispara cascada); `renovar` extiende sin cerrar (`Estado` intacto, sin cascada).
- **Rationale**: Modelar la renovación como una operación de escritura explícita y separada (en vez de,
  p. ej., permitir un `PUT` genérico sobre `FechaHoraFin`) mantiene el mismo patrón ya usado para `finalizar`
  — una intención de negocio nombrada, con sus propias reglas de validación y su propio código de error —,
  en vez de un endpoint CRUD genérico que permitiría escrituras arbitrarias sobre un campo con semántica de
  negocio compleja (RF-063, RF-072). Reutilizar `UpdatedAt`/`UpdatedById` (Principio III, ya obligatorio en
  toda entidad) para el rastro de auditoría de la renovación evita duplicar el mecanismo de auditoría
  automático ya existente con un campo o tabla de histórico de cambios dedicada — consistente con cómo el
  resto del dominio no captura diffs de "valor anterior/valor nuevo" en ningún otro campo.
- **Alternatives considered**: Modelar la renovación como cierre + nueva `AsignaciónPersonaCompañía` en la
  misma transacción (crear una continuación, en vez de extender la existente) — descartado explícitamente
  por negocio: "la extensión representa continuidad de la misma relación... no debe crear automáticamente
  una nueva `AsignaciónPersonaCompañía`" — además, esto dispararía innecesariamente la cascada de RF-061
  sobre asociaciones dependientes que no deben verse afectadas; permitir la renovación mediante el mismo
  endpoint `PUT` genérico de creación/edición (descartado: mezclaría dos intenciones de negocio muy
  distintas — declarar los datos de una asignación nueva vs. extender una vigencia existente — bajo el mismo
  contrato, dificultando aplicar las reglas de validación específicas de cada una, p. ej. que `renovar`
  exige `Estado = ACTIVA` y una fecha estrictamente posterior, mientras que la creación tiene sus propias
  reglas de solapamiento); permitir renovar una pertenencia `ACTIVA` ya expirada dinámicamente, "puenteando"
  el vacío temporal transcurrido (descartado explícitamente por negocio, Sesión "cierre Decisión Pendiente
  #10": volvería retroactivamente vigente, para efectos de auditoría e histórico, un período en el que la
  persona dinámicamente no tenía acceso — contradice el principio ya establecido de que la vigencia
  dinámica, una vez transcurrida, no se reescribe; el camino correcto para ese caso es crear una nueva
  `AsignaciónPersonaCompañía`, que además ya funciona sin cambios porque no hay solapamiento posible entre
  una pertenencia expirada y una nueva que empieza después).

## Cierre de Etapa 1 — Decisiones D1 a D9 (Sesión 2026-09-20)

> Estas siete secciones (§27 a §33) documentan el diseño técnico derivado de las nueve decisiones de negocio
> (D1 a D9) que cierran las 16 preguntas de la matriz de auditoría de cierre de Etapa 1
> (`docs/auditorias/decisiones-etapa1-2026-09-16.html`). D8 (consultas transversales, RF-067 a RF-069) y D9
> (decisiones heredadas #1/#3/#7) no requieren diseño técnico nuevo — D8 queda diferida a una etapa futura
> sin contrato ni código; D9 ratifica comportamiento ya implementado sin cambio alguno — y se documentan
> únicamente como anotación en `spec.md`, no aquí. Ninguna de las nueve decisiones requirió enmendar la
> Constitución (ver plan.md, "Re-chequeo de cierre de Etapa 1").

## 27. Administración de usuarios y RBAC (D1)

- **Decision**: Se reemplaza `AlcanceUsuarioCompañía` (join plano `UsuarioId`/`CompañíaId`, sin rol ni
  vigencia) por una entidad de asignación de rol administrativo, `AsignaciónRolAdministrativo`
  (`UsuarioId`, `Rol` — enum cerrado `GLOBAL_ADMINISTRATOR`/`COMPANY_ADMINISTRATOR`, `CompañíaId` — `Guid?`,
  `FechaHoraInicio`/`FechaHoraFin` **NOT NULL** desde la creación, mismo patrón que RF-071 aplicado por
  primera vez a una entidad ligada a `Usuario` en vez de a `Persona`). Regla fundamental:
  `Rol = GLOBAL_ADMINISTRATOR ⇒ CompañíaId = NULL`; `Rol = COMPANY_ADMINISTRATOR ⇒ CompañíaId` obligatorio y
  válido. Trigger de no-solapamiento particionado por `(UsuarioId, CompañíaId)` **solo quando `Rol =
  COMPANY_ADMINISTRATOR`** (mismo patrón idiomático de research.md §5); `GLOBAL_ADMINISTRATOR` queda exento
  de esa partición — pueden existir varias asignaciones Global Administrator simultáneas, incluso del mismo
  usuario, sin conflicto. Un usuario puede tener varias asignaciones `COMPANY_ADMINISTRATOR` simultáneas si
  son de compañías distintas. `Rol` se modela como enum cerrado del Dominio (mismo patrón que `EstadoUsuario`
  y `TipoCompañía`), no como catálogo versionado: agregar un tercer rol exige una modificación explícita del
  modelo de autorización, no una inserción de datos en tiempo de ejecución — precisamente para que ampliar
  el conjunto de privilegios disponibles sea un cambio de código auditable, no una operación administrativa.

  Autorización: `CompaniaScopeAuthorizationHandler`/`CompaniaScopeRequirement` se redefine — hoy solo
  comprueba `alcance.CompaniaIds.Count > 0` (lo que denegaría a un Global Administrator sin compañías
  enumeradas); pasa a comprobar "¿el usuario tiene al menos una `AsignaciónRolAdministrativo` vigente
  (`GLOBAL_ADMINISTRATOR`, o `COMPANY_ADMINISTRATOR` con al menos una compañía)?". `IAlcanceCompaniaAccessor`
  se redefine: `EstaEnAlcance(companiaId)` devuelve `true` incondicionalmente si el usuario tiene una
  asignación `GLOBAL_ADMINISTRATOR` vigente; en caso contrario, verifica membresía contra el conjunto de
  `CompañíaId` de sus asignaciones `COMPANY_ADMINISTRATOR` vigentes. El JWT (`JwtTokenService`) deja de emitir
  "un claim por compañía" (no puede representar un alcance GLOBAL no enumerado sin listar todas las
  compañías existentes, y rompería con cada compañía nueva); pasa a emitir un claim de rol
  (`ClaimsPersonalizados.Rol`) y, cuando el rol es `COMPANY_ADMINISTRATOR`, un claim por compañía asignada
  (igual que hoy, pero acotado a ese caso).

  `UsuarioService` (siete operaciones) se reescribe para aplicar Resource Ownership (research.md §29):
  `ListarAsync`/`ObtenerAsync` se filtran por el alcance de quien consulta (Global ve todos; Company
  Administrator solo usuarios con alguna asignación en su propia compañía); `CrearAsync` exige que quien crea
  tenga permiso para asignar el rol solicitado (Global: cualquier rol/compañía; Company Administrator: solo
  `COMPANY_ADMINISTRATOR` para su propia compañía, nunca `GLOBAL_ADMINISTRATOR` ni otra compañía);
  `ActualizarAsync`/`DesbloquearAsync` se acotan al mismo alcance; `ObtenerAlcanceAsync`/`ReemplazarAlcanceAsync`
  se reemplazan por operaciones sobre `AsignaciónRolAdministrativo` (listar asignaciones de un usuario;
  crear/finalizar una asignación individual — nunca "reemplazar el conjunto completo", que no encaja con
  asignaciones auditables con vigencia propia). Lecturas fuera de alcance devuelven `404` (mismo patrón ya
  usado en el resto del sistema); las escrituras siguen el contrato específico de cada endpoint.

- **Rationale**: Modelar el rol como una asignación temporal auditable (no como un campo simple en
  `Usuario`) es el único diseño consistente con la propia decisión de negocio, que exige vigencia y
  auditoría en la asignación misma, y con el patrón de renovación/cierre ya usado en todo el dominio
  (`AsignaciónPersonaCompañía`, RF-073). Rechazar el `Rol` como catálogo versionado sigue el precedente
  directo de `EstadoUsuario`/`TipoCompañía` en este mismo `data-model.md`: son clasificaciones cerradas y
  de código, no datos maestros de negocio (Principio VI no exige lo contrario para este tipo de campo).
  Colapsar "RBAC" y "Scope" en un único mecanismo (`IAlcanceCompaniaAccessor`) en vez de dos middlewares
  independientes evita duplicar la resolución del alcance en dos lugares que podrían divergir.
- **Alternatives considered**: Mantener `AlcanceUsuarioCompañía` y agregar un campo `EsGlobal: bool` en
  `Usuario` (descartado: no captura vigencia ni auditoría de la asignación, y un booleano no es
  extensible si en el futuro se necesita más de un rol simultáneo con distinto alcance); modelar `Rol` como
  catálogo versionado igual que `TipoPersona`/`TipoCredencial` (descartado: permitiría insertar un nuevo rol
  administrativo en tiempo de ejecución sin revisión de código, un vector de escalada de privilegios
  inaceptable para un campo de seguridad); mantener el endpoint `PUT .../alcance-companias` que reemplaza el
  conjunto completo (descartado: no es compatible con asignaciones individuales auditables con vigencia
  propia — un `PUT` que sobrescribe todo pierde el historial de cada asignación individual).

## 28. Bootstrap del primer administrador (D2)

- **Decision**: El primer usuario administrador se crea automáticamente mediante una **rutina de arranque de
  la aplicación** (no una migración de EF Core clásica), ejecutada después de aplicar las migraciones,
  idempotente: verifica si ya existe al menos una `AsignaciónRolAdministrativo` con
  `Rol = GLOBAL_ADMINISTRATOR` antes de crear nada. Si no existe ninguna, crea un `Usuario` con
  `Correo`/`PasswordHash` provenientes de configuración (`Bootstrap:AdminEmail`, obligatorio, debe ser un
  correo válido conforme a RF-001; `Bootstrap:AdminPassword`, obligatorio, nunca con valor por defecto
  committeado — a diferencia de `JWT_SIGNING_KEY`/`MSSQL_SA_PASSWORD`, que sí tienen un valor de repaldo de
  desarrollo en `docker-compose.yml`; esta contraseña debe ser provista por cada desarrollador en su propio
  `.env` no versionado), validado contra `PasswordPolicyValidator` sin excepción, con
  `RequiereCambioPassword = true`. Se crea también su `AsignaciónRolAdministrativo`
  (`Rol = GLOBAL_ADMINISTRATOR`, `CompañíaId = NULL`, `FechaHoraInicio` = instante de arranque,
  `FechaHoraFin = MAX_VALIDITY_DATE` = `2999-12-31T23:59:59Z`). `CreatedById`/`UpdatedById` del `Usuario` y de
  su asignación quedan en `NULL` (ya son `Guid?` en `EntidadBase`/`IAuditable` — no requiere cambio de
  modelo): representan "creado por el sistema", no por otro `Usuario`.

  `MAX_VALIDITY_DATE` es una **excepción explícita, documentada y acotada exclusivamente** a esta única
  asignación de bootstrap — nunca un valor de configuración de propósito general, nunca reutilizable para
  otra asignación de rol ni para ninguna entidad ligada a `Persona` (RF-071 sigue prohibiendo, sin excepción,
  cualquier fecha centinela para esas seis entidades). Puede modificarse, renovarse o revocarse
  posteriormente por los mismos mecanismos administrativos que cualquier otra `AsignaciónRolAdministrativo`.

- **Rationale**: Una rutina de arranque idempotente es el único mecanismo que puede leer configuración/secrets
  en tiempo de ejecución (las migraciones de EF Core, usadas para sembrar catálogos públicos como
  `TipoDocumento`/`TipoSangre`/`Género` vía `HasData`, no tienen ese acceso de forma natural, y no deben
  usarse para sembrar un secreto). Excluir un valor de repaldo committeado para la contraseña (a diferencia
  de los otros dos secretos de desarrollo) es una decisión explícita del usuario, más estricta que el
  precedente existente, para minimizar el riesgo de credencial por defecto conocida — el propio mecanismo de
  "forzar cambio en primer login" solo mitiga ese riesgo si alguien inicia sesión antes de exponer el sistema
  en red.
- **Alternatives considered**: Sembrar el usuario bootstrap en una migración de EF Core con
  `migrationBuilder.InsertData` (descartado: expondría un hash de contraseña fijo en el historial de git de
  forma permanente, y no puede leer `IOptions` en tiempo de aplicación de la migración); exigir
  `Bootstrap:AdminPassword` en cada arranque incluso cuando ya existe un Global Administrator (descartado:
  fricción operativa innecesaria — la rutina solo la necesita la primera vez; si está presente en arranques
  posteriores, simplemente no se usa).

## 29. Aislamiento por alcance — cadena de autorización y Resource Ownership (D3)

- **Decision**: Se formaliza la cadena `Authentication → RBAC → Scope → Resource Ownership → Business Rules
  → Operation → ALLOW/DENY` como el modelo explícito de autorización administrativa de plataforma (distinto
  del motor de evaluación de acceso físico de dominio, research.md §18, que no cambia). `Authentication` =
  JWT Bearer ya emitido; `RBAC` + `Scope` colapsan en el mecanismo único de `IAlcanceCompaniaAccessor`
  descrito en §27 (no dos middlewares independientes en este código). `Resource Ownership` se resuelve por
  tipo de entidad: `ÁreaAcceso` vía `CompañíaPrincipalId` directo (RF-046); `UnidadOrganizativa` vía
  `CompañíaPrincipalUnidadOrganizativaRaiz` + recorrido de ancestros (RF-044/045);
  `RelaciónContratistaPrincipal`/`ContextoOperativoPersonaPrincipal`/`AsignaciónCredencial` vía
  `CompañíaPrincipalId` (RF-060); `Compañía` es ella misma el recurso. Para `Persona` — sin una única
  compañía propietaria — la regla es la **unión**: una `Persona` está dentro del alcance de un usuario si
  su compañía de pertenencia vigente (`AsignaciónPersonaCompañía`) coincide con una compañía del alcance del
  usuario, **o** si tiene al menos un `ContextoOperativoPersonaPrincipal` vigente con una Compañía Principal
  del alcance del usuario — lo que habilita el caso operativo real de que una Principal administre a las
  personas de sus Contratistas que operan en su instalación, sin depender de quién es el empleador formal.
  `Business Rules` son las excepciones ya modeladas explícitamente (nunca implícitas) — p. ej. la relación
  Contratista↔Principal habilitando qué Principales puede seleccionar el usuario. Lecturas fuera de alcance
  → `404` (nunca `403`, para no confirmar existencia — ya era el patrón en `CompaniaService`, se generaliza a
  todos los servicios); escrituras fuera de alcance siguen el contrato específico de cada endpoint. "Todo
  acceso administrativo relevante auditable" se satisface con el interceptor de auditoría de escritura ya
  existente (Principio III) — no se agrega una bitácora nueva de lecturas ni de intentos denegados en esta
  decisión.

  Servicios que hoy no aplican ningún control de alcance y deben corregirse para implementar Resource
  Ownership: `AsignacionUnidadOrganizativaService`, `EstadoEfectivoService`, `RevocacionService`,
  `UnidadOrganizativaService`, `AreaAccesoService` (verificado por ausencia total de referencias a
  `IAlcanceCompaniaAccessor`/`EstaEnAlcance` en su código fuente). Servicios con algún control ya presente,
  a auditar y completar contra la regla de unión de `Persona` arriba: `PersonaService`,
  `HistorialPersonaService`, `ContextoOperativoService`, `CredencialService`, `CompaniaService`,
  `RelacionContratistaPrincipalService`.

- **Rationale**: Expresar la cadena de forma explícita, con un paso de "Resource Ownership" nombrado y
  distinto de "Scope", hace visible en el diseño el defecto raíz de F-01/F-02 (el código comprobaba "¿tiene
  alcance no vacío?" pero nunca "¿este recurso concreto pertenece a ese alcance?"). La regla de unión para
  `Persona` prioriza la utilidad operativa real (quien gestiona el contexto de una persona necesita poder
  administrarla) sobre una lectura más estricta que solo mirara al empleador formal, que dejaría a las
  Principales sin capacidad de gestión sobre el personal de sus propias Contratistas.
- **Alternatives considered**: Restringir `Persona` únicamente a su compañía de pertenencia vigente
  (descartado explícitamente por el usuario: impediría a una Principal administrar a las personas de sus
  Contratistas que operan en su instalación, un caso de uso central del dominio); agregar una bitácora de
  lecturas/intentos denegados como parte de esta decisión (descartado: alcance mayor, se deja para una
  eventual extensión de Historia 10/D8, no bloquea D3).

## 30. Inactivación de Compañía en la evaluación de acceso (D4)

- **Decision**: `EvaluadorDeAcceso` (research.md §7) pasa de 14 a **15 pasos**, mismo tipo de ampliación ya
  ocurrida de 13 a 14 por RF-066. Se **inserta un paso 5 nuevo**: verificar que la Compañía Principal
  propietaria del área, determinada en el paso 4, tenga `Compañía.Estado = ACTIVO`; sin ello, `DENEGADO`
  (nuevo motivo `COMPANIA_INACTIVA`) sin evaluar el resto. Y se **amplía el paso 6** —el antiguo paso 5, que
  ya re-valida dinámicamente la compañía de pertenencia vigente de la persona para determinar la legitimidad
  del contexto operativo (RF-061)— para exigir además que esa compañía de pertenencia tenga
  `Estado = ACTIVO`; sin ello, `DENEGADO` (mismo motivo `COMPANIA_INACTIVA`) sin evaluar el resto. La
  verificación de la Principal se numera como paso propio porque es un corte independiente y barato; la de la
  compañía de pertenencia se pliega en el paso que ya la resuelve, para no calcularla dos veces. Ambos
  casos comparten un único valor de `MotivoDenegacion` (`COMPANIA_INACTIVA`), consistente con el patrón ya
  establecido por `SIN_CONTEXTO_OPERATIVO_VIGENTE`/`SIN_CREDENCIAL_VIGENTE` (varias causas subyacentes, un
  solo código, detalle en texto libre si se expone vía `ProblemDetails`). Ningún campo, entidad ni migración
  nueva: `Compañía.Estado` ya existe (RF-006, RF-032). Sin cascada de escritura: la denegación es
  exclusivamente resultado de la evaluación dinámica; inactivar o reactivar una compañía no modifica ningún
  registro dependiente, por lo que el efecto es inmediato y reversible sin intervención adicional.

  `research.md §14.5` queda corregida: ya no describe esto como "fuera de alcance" de la corrección de
  Revocación Automática, sino como la decisión definitiva de D4, y deja de afirmar (incorrectamente) que la
  re-validación dinámica "ya" cubre este caso — el código nunca lo implementó hasta esta corrección.

- **Rationale**: Extender los pasos 4 y 5 (en vez de agregar dos pasos nuevos separados) evita duplicar la
  resolución de "cuál es la Compañía Principal" y "cuál es la compañía de pertenencia de la persona", que
  esos mismos pasos ya calculan — coherente con el principio de diseño original del algoritmo (agrupar
  verificaciones de legitimidad antes de evaluar elegibilidad y permisos). Un solo motivo de denegación para
  ambos disparadores sigue el patrón ya validado de no fragmentar el enum `MotivoDenegacion` por causa
  subyacente cuando el detalle puede comunicarse en texto libre.
- **Alternatives considered**: Cascada de escritura que revoque en cascada los dependientes de una compañía
  inactivada, análoga a RF-061 (descartada explícitamente por la decisión de negocio D4 — Alternativa A, no
  B); agregar dos motivos de denegación distintos, uno por disparador (descartado: rompe el patrón ya
  establecido y no aporta información que el campo de detalle no pueda comunicar).

## 31. Zona horaria por Compañía Principal (D5)

- **Decision**: Se agrega el campo `ZonaHorariaIana` (`string`, identificador IANA, p. ej. `America/Lima`,
  `America/Santiago`) a la entidad `Compañía`, obligatorio y validado (mismo mecanismo de validación que hoy
  usa `RelojEmpresarial` al arrancar — `DateTimeZoneProviders.Tzdb.GetZoneOrNull`, movido a validarse por
  request al crear/actualizar una `Compañía`, no solo una vez al arrancar la aplicación) cuando
  `TipoCompañía = PRINCIPAL_MANDANTE`; sin uso funcional para `CONTRATISTA` (no poseen áreas, contextos ni
  bloques horarios propios — RF-045/046). Los timestamps siguen persistidos siempre en UTC (`datetime2(3)`,
  sin cambios); la zona solo se usa para interpretar entradas, presentar salidas, y evaluar bloques horarios.

  `IRelojEmpresarial` (`DiaSemanaLocal(instanteUtc)`, `HoraLocal(instanteUtc)`) se redefine para recibir
  además la Compañía Principal cuya zona debe resolverse (p. ej. `DiaSemanaLocal(instanteUtc, companiaPrincipalId)`),
  reemplazando el `DateTimeZone` único construido una vez en el constructor de `RelojEmpresarial` desde
  `IOptions<ZonaHorariaOptions>`. `EvaluadorDeAcceso` (paso de bloque horario, **paso 13** en la numeración
  vigente de 15 pasos; era el 12 antes de la inserción del paso 5 por D4) pasa la Compañía Principal
  propietaria del área determinada en el paso 4.
  `IRelojSistema` (misma clase, solo `UtcNow`) no cambia. La configuración global existente
  (`ZonaHoraria:TimeZoneId`) se conserva como **zona de repaldo**, usada únicamente para entidades cuya
  vigencia (RF-016) no es resoluble a una única Compañía Principal: `AsignaciónPersonaCompañía` cuando
  referencia una compañía `CONTRATISTA`, y `AsignaciónTipoPersona` (sin compañía asociada en absoluto).

  Cambiar la zona de una Compañía Principal **nunca reinterpreta** instantes UTC ya persistidos; sí cambia la
  representación local en consultas/presentaciones futuras — no se versiona históricamente la zona de cada
  compañía: toda presentación, pasada o presente, usa siempre la zona **actual** configurada (compatible sin
  fricción con RF-037/CS-007, que reconstruyen estado efectivo comparando instantes UTC, ajenos a cualquier
  zona). El cambio de zona se audita por el mecanismo general ya existente (interceptor de EF Core sobre el
  `UpdatedAt`/`UpdatedById` de la fila `Compañía`), sin bitácora dedicada nueva.

- **Rationale**: Anclar la zona a la Compañía Principal es consistente con cómo el resto del dominio ya
  particiona por Principal (áreas, contextos, credenciales) — es la primera entidad de la que depende un
  cálculo de negocio (bloques horarios) sin ser, ella misma, ya un atributo directo de `ÁreaAcceso` o
  `ContextoOperativoPersonaPrincipal`, así que vive en `Compañía` en vez de duplicarse. No versionar
  históricamente la zona evita una clase entera de complejidad (una tabla de vigencias de zona) que el propio
  texto de la decisión de negocio hace innecesaria al aceptar explícitamente que la representación local
  pasada puede cambiar.
- **Alternatives considered**: Zona horaria configurable por `ÁreaAcceso` en vez de por `Compañía` (descartado:
  granularidad no solicitada por el negocio, y todas las áreas de una misma Principal comparten ubicación
  operativa razonablemente); versionar históricamente la zona de cada compañía (descartado explícitamente
  por el propio texto de D5, que acepta el cambio de representación local hacia adelante); mantener
  `ZonaHoraria:TimeZoneId` como única fuente para todo (descartado: no satisface la decisión de negocio de
  una zona por Principal; se conserva solo como repaldo para los casos no resolubles a una única Principal).

## 32. Validación de dependientes al cambiar TipoCompania (D6)

- **Decision**: `CompaniaService.ActualizarAsync` (hoy asigna `TipoCompañía` sin ninguna comprobación) se
  amplía para rechazar el cambio de `TipoCompañía` si la compañía tiene alguna de estas dependencias
  incompatibles con el tipo destino: (1) áreas de acceso propias (`ÁreaAcceso.CompañíaPrincipalId`); (2)
  raíces de unidad organizativa (`CompañíaPrincipalUnidadOrganizativaRaiz`); (3) relaciones
  `RelaciónContratistaPrincipal` vigentes donde la compañía participa como Contratista o como Principal
  — evaluado **únicamente** contando filas reales de esa entidad, nunca inspeccionando
  `AsignaciónPersonaCompañía` ni personas empleadas directamente (esas ya están cubiertas por la categoría
  siguiente); (4) contextos operativos (`ContextoOperativoPersonaPrincipal.CompañíaPrincipalId`); (5)
  credenciales (`AsignaciónCredencial.CompañíaPrincipalId`). La regla es **simétrica**: aplica igual en
  ambas direcciones (`PRINCIPAL_MANDANTE → CONTRATISTA` y `CONTRATISTA → PRINCIPAL_MANDANTE`) — una
  Contratista con relaciones vigentes como Contratista queda igualmente bloqueada para pasar a
  `PRINCIPAL_MANDANTE`. Si existe alguna dependencia incompatible, la operación se rechaza con `409 Conflict`
  (consistente con el precedente ya existente de `DOCUMENTO_YA_REGISTRADO` — "el estado actual del recurso
  impide la operación", no un error de formato de entrada) y un código de negocio nuevo
  (`CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS`) con detalle suficiente para ser accionable (qué categorías
  bloquearon el cambio, RF-033). Nunca se elimina, cierra, revoca ni modifica ninguna dependencia
  automáticamente. El cambio exitoso se audita por el interceptor ya existente, sin mecanismo nuevo.

- **Rationale**: Contar dependencias exactamente en las cinco categorías ya modeladas evita introducir
  heurísticas nuevas; separar estrictamente "relaciones empresariales" (categoría 3) de "contextos
  operativos" (categoría 4) refleja una distinción real del dominio confirmada explícitamente por el usuario
  (`RelaciónContratistaPrincipal` es exclusivamente Compañía↔Compañía; un empleado directo de una Principal
  se modela vía `AsignaciónPersonaCompañía` + `ContextoOperativoPersonaPrincipal` auto-fijado por RF-053,
  nunca vía una relación "Compañía A → Compañía A"). `409` sigue el precedente ya establecido en este mismo
  contrato para conflictos de estado existente.
- **Alternatives considered**: Cascada automática que ajuste o elimine dependientes al cambiar el tipo
  (descartada explícitamente por la decisión de negocio D6); permitir el cambio sin restricción alguna,
  dejando las dependencias en un estado inconsistente respecto de RF-045/046 (statu quo, descartado: es
  exactamente el defecto que D6 corrige); `400 Bad Request` en vez de `409` (descartado: el problema no es
  el formato de la petición sino el estado actual del recurso, igual que `DOCUMENTO_YA_REGISTRADO`).

## 33. Interfaz de Historia 5 — Casos A y B de asignación de unidad organizativa (D7)

- **Decision**: Se completa, dentro de Etapa 1, la interfaz de asignación de unidad organizativa para
  reflejar el flujo funcional completo de Caso A (persona con compañía de pertenencia `PRINCIPAL_MANDANTE`:
  contexto operativo fijado automáticamente, RF-053) y Caso B (persona con compañía de pertenencia
  `CONTRATISTA`: selector de Compañía Principal limitado a relaciones `RelaciónContratistaPrincipal`
  vigentes, RF-054), como un wizard de varios pasos: selección de persona/contexto → creación de la
  pertenencia si no existe (reutilizando el hook ya existente `useCrearPertenencia`) → creación/apertura del
  contexto operativo (reutilizando `useAbrirContexto`, con el selector de Principal solo en Caso B) →
  selección de unidad organizativa mediante árbol jerárquico (reutilizando el componente ya existente y
  genérico `frontend/src/components/Tree/Tree.tsx`, en vez de construir uno nuevo, satisfaciendo CS-021) →
  asociación del perfil (`AsignaciónTipoPersona`) — capacidad que hoy no tiene ningún cliente HTTP en el
  frontend y debe agregarse contra el endpoint ya existente `/personas/{id}/perfiles` (T113). Ningún cambio
  de backend, modelo de datos ni contrato: las siete entidades y todos los endpoints necesarios (T102-T114)
  ya existen y funcionan.

- **Rationale**: Reutilizar `useCrearPertenencia`/`useAbrirContexto` (ya implementados y probados, solo sin
  invocar desde ninguna pantalla) y el componente `Tree` genérico (ya usado en el mantenimiento de unidades
  organizativas) minimiza el trabajo nuevo real a composición de UI y al cliente HTTP de perfiles, en vez de
  reconstruir capacidades que ya existen.
- **Alternatives considered**: Construir un selector de unidad organizativa plano (statu quo actual,
  descartado: contradice CS-021, que exige explícitamente un árbol); construir un componente de árbol nuevo
  específico para este flujo (descartado: duplicaría `Tree.tsx` sin necesidad).

## 34. Cierre de las desviaciones D-1, D-2 y D-4 del Baseline (Sesión 2026-09-20, `/speckit-plan`)

> **Nomenclatura — leer antes de esta sección**: las etiquetas `D-1`, `D-2`, `D-4` (con guion) identifican
> las **desviaciones de implementación** detectadas al terminar T169–T228 y auditadas con
> `/speckit-analyze`. NO son las decisiones de negocio `D1`–`D9` (sin guion) de §27 a §33, que provienen de
> la matriz de 16 preguntas del cierre de Etapa 1. La coincidencia de letra es accidental y los contenidos
> no se corresponden (p. ej. `D4` = inactivación de compañía, §30; `D-4` = búsqueda de usuarios, §34.3).
>
> Las otras dos desviaciones no generan trabajo de implementación y no se planifican aquí: **D-3** se cerró
> como implementación válida sin cambios (la migración EF generada al final es consistente con el modelo
> final), y **D-5** se cerró por configuración y documentación (`docker-compose.yml`, `README.md`,
> `quickstart.md`), sin modificar RF-078, cuyo texto ya era inequívoco.

### 34.1 Renovación de `AsignaciónRolAdministrativo` expuesta como operación HTTP (D-1)

- **Decision**: Se expone `POST /api/usuarios/{id}/roles/{asignacionId}/renovar`, con cuerpo
  `{ "fechaHoraFin": "<date-time>" }` y respuesta `204`, como contraparte inversa de
  `.../roles/{asignacionId}/finalizar` ya existente — exactamente el mismo par de operaciones y la misma
  forma de contrato que `contracts/people.yaml` ya estableció para `AsignaciónPersonaCompañía`
  (`.../historial-companias/{asignacionId}/renovar`, §26). **La lógica de negocio no se rescribe**: ya está
  implementada y es correcta en `AsignacionRolAdministrativoService.RenovarAsync` (invariantes: renovable
  solo si sigue vigente dinámicamente, nueva fecha estrictamente posterior, revalidación de no-solapamiento)
  y en `UsuarioService.RenovarRolAsync`, que le antepone el alcance de RF-077
  (`ObtenerEnAlcanceAsync` ⇒ `404` fuera de alcance) y la autorización de RF-076 (`ValidarPuedeAsignar` ⇒
  `403 ROL_NO_AUTORIZADO`, misma regla que ya protege finalizar: quien no podría crear esa asignación tampoco
  puede extenderla). Lo único ausente es la **exposición**: el controlador, el contrato, el cliente del
  frontend y las pruebas.

  El mapeo de códigos queda determinado por los tipos de excepción ya existentes, sin inventar ninguno:
  `RecursoNoEncontradoException` ⇒ `404`; `RolNoAutorizadoException` ⇒ `403 ROL_NO_AUTORIZADO`;
  `ConflictoEstadoException(ASIGNACION_ROL_NO_VIGENTE)` ⇒ `409`;
  `ConflictoEstadoException(SOLAPAMIENTO_VIGENCIA)` ⇒ `409`; validación de cuerpo ⇒ `400`.

  **Dos correcciones puntuales detectadas en el código actual**, ambas necesarias para que la operación
  expuesta sea consistente con el precedente ya vigente — no son funcionalidad nueva:

  1. `RenovarAsignacionRolRequest(DateTime NuevaFechaHoraFin)` serializa como `nuevaFechaHoraFin`, pero el
     contrato de renovación ya establecido en `people.yaml` —y el cuerpo aprobado para esta operación— usa
     `fechaHoraFin`. La propiedad debe pasar a llamarse `FechaHoraFin`. Es un DTO todavía no alcanzable por
     HTTP, así que el cambio no rompe ningún consumidor existente.
  2. `AsignacionRolAdministrativoService.RenovarAsync` lanza `ReglaNegocioInvalidaException` (⇒ `400`) para
     `RENOVACION_NO_POSTERIOR`, mientras que `HistorialPersonaService.RenovarAsync` lanza
     `ConflictoEstadoException` (⇒ `409`) para **la misma regla y el mismo código de negocio**, y
     `people.yaml` documenta `409`. Debe unificarse en `ConflictoEstadoException`: el mismo código de error
     no puede significar dos estados HTTP distintos según la entidad.

- **Rationale**: Reutilizar la operación de dominio ya implementada y probada evita duplicar reglas de
  vigencia; exponerla con la forma exacta del precedente (`/finalizar` ↔ `/renovar`, `204`, cuerpo de un solo
  campo) mantiene la simetría que ya existe para `AsignaciónPersonaCompañía` y hace que el frontend y las
  pruebas de contrato se escriban por analogía en vez de por invención. Las dos correcciones se incluyen aquí
  —y no como hallazgos separados— porque exponer la operación sin resolverlas publicaría un contrato
  inconsistente consigo mismo: un cliente que trate `RENOVACION_NO_POSTERIOR` como conflicto en personas y
  como error de validación en usuarios tendría que ramificar por entidad.
- **Alternatives considered**: `PATCH` sobre la asignación con `fechaHoraFin` (descartado: convertiría la
  renovación en una edición genérica y abriría la puerta a modificar `rol`/`compañíaId`, que RF-075 prohíbe
  explícitamente — la operación dedicada es lo que hace inexpresable el cambio no permitido); devolver `200`
  con la asignación renovada (descartado: `RenovarAsync` sí devuelve el DTO, pero `/finalizar` y el
  `/renovar` de personas ya responden `204`, y romper esa simetría por conveniencia del cliente no se
  justifica — el frontend invalida la caché y refetch, como ya hace tras finalizar); generalizar RF-073 a
  todas las entidades renovables (descartado explícitamente: RF-073 permanece específico de
  `AsignaciónPersonaCompañía`; RF-075 ya referencia sus reglas sin absorberlas).

> **Estado (Sesión 2026-09-20, posterior a esta planificación)**: las tres desviaciones quedaron
> **implementadas y verificadas** (T229 a T242). Dos precisiones que el plan no anticipó y que la
> implementación resolvió sin cambiar ninguna decisión de negocio:
>
> 1. **La cobertura de CS-037 se amplió de cinco servicios a los siete recursos que el criterio declara**
>    (hallazgo C2 de la auditoría posterior): T237/T238 cubren los cinco servicios que D-2 identificó, y
>    **T242** añade usuarios, compañías, personas, contextos operativos y credenciales. Se cerró ampliando la
>    cobertura, no recortando el criterio ni reescribiendo T215.
> 2. **`EstadoEfectivoService` y `RevocacionService` no tienen recurso propio que pedir**: el primero es una
>    proyección de la persona y el segundo un colaborador interno. Su aislamiento se verifica por la
>    superficie de la persona (`estado-efectivo`) y por su invocador (`historial-companias/{id}/finalizar`),
>    comprobando además que la pertenencia ajena no se modifica — un `404` que escribiera de todas formas
>    sería peor que un `403`.

### 34.2 Cobertura de alcance GLOBAL vs COMPANY en los cinco servicios auditados (D-2)

- **Decision**: **Ningún cambio de código de producción.** La verificación confirma que el control de alcance
  exigido por RF-077 ya está aplicado, con dos formas distintas y ambas correctas:
  `UnidadOrganizativaService` y `AreaAccesoService` resuelven el alcance directamente (vía
  `CompañíaPrincipalId` y el recorrido de raíces de §29); `AsignacionUnidadOrganizativaService` y
  `EstadoEfectivoService` lo delegan a un único punto de control, `PersonaService.ExigirAlcanceHistoricoAsync`,
  invocado al inicio de cada operación pública; y `RevocacionService` **no tiene ni debe tener control
  propio**: no es un punto de entrada, es un colaborador interno cuyo único invocador es
  `HistorialPersonaService`, que ya validó el alcance de la persona antes de disparar la cascada.

  Lo que falta es exclusivamente **cobertura de pruebas de integración** que demuestre ese comportamiento
  bajo el modelo RBAC actual. Verificado: las tres suites de aislamiento existentes
  (`OrgUnits/AislamientoMultiPrincipalTests`, `AreaAccess/AislamientoAreasMultiPrincipalTests`,
  `People/AislamientoPertenenciasTests`) son anteriores a D1/D3 y no mencionan `GLOBAL_ADMINISTRATOR` ni
  `COMPANY_ADMINISTRATOR`: prueban el aislamiento **de datos** entre Principales, no el aislamiento **por rol
  del actor autenticado**. Y `RolesAdministrativosTests` declara CS-037 en su comentario de clase pero no
  contiene ningún método `CS037_*`: sus pruebas cubren CS-036 (el recurso `Usuario`) y CS-038 (bootstrap),
  nunca los cinco servicios de esta desviación.

  La cobertura a agregar tiene dos casos por servicio: (a) un `GLOBAL_ADMINISTRATOR` opera sobre recursos de
  **dos Principales distintas** en la misma prueba —lo que falsaría cualquier regresión que volviera a exigir
  `CompaniaIds.Count > 0`—, y (b) un `COMPANY_ADMINISTRATOR` de la Principal A recibe `404` sobre un recurso
  de la Principal B, nunca `403`. Para `RevocacionService` el caso se ejercita a través de su invocador
  (`POST /api/personas/{id}/historial-companias/{asignacionId}/finalizar`), porque no tiene endpoint propio.

- **Rationale**: Modificar código que ya satisface el requisito solo para que se parezca más al texto
  original de T186/T187 introduciría riesgo sin beneficio. La ausencia de pruebas, en cambio, sí es un
  defecto real frente al Principio VII de la Constitución, que exige cobertura automatizada explícita de las
  fugas de datos entre compañías: hoy una regresión en el alcance de esos cinco servicios no rompería
  ninguna prueba. Distinguir los dos casos (GLOBAL sobre dos Principales / COMPANY con `404`) es lo que
  convierte la prueba en una red de seguridad real y no en una confirmación del camino feliz.
- **Alternatives considered**: Dar D-2 por cerrada solo con la evidencia de lectura de código (descartado:
  deja el invariante sin red de regresión, contra el Principio VII); agregar a `RevocacionService` un control
  de alcance propio para poder probarlo aisladamente (descartado: duplicaría la verificación que su único
  invocador ya hace, y un doble control divergente es peor que uno solo bien ubicado).

### 34.3 Búsqueda server-side de usuarios dentro del alcance autorizado (D-4)

- **Decision**: Se agrega el parámetro de consulta **`texto`** (`string`, opcional) a `GET /api/usuarios`,
  reutilizando literalmente la convención ya establecida por `contracts/people.yaml`
  (`texto` — "Búsqueda por nombres, apellidos o número de documento") y `contracts/companies.yaml`
  (`texto` — "Búsqueda por nombre o número de documento"). Para `Usuario` el único atributo textual del
  contrato es `correo`, de modo que la semántica es **coincidencia parcial sobre el correo**, resuelta en el
  servidor con `EF.Functions.Like(u.Correo, $"%{texto}%")` — exactamente el mecanismo que
  `CompaniaService.ListarAsync` ya usa, ejecutado por SQL Server con la colación de la base (insensible a
  mayúsculas por defecto). En la capa de aplicación se introduce el registro `FiltroUsuarios(EstadoUsuario?
  Estado, string? Texto)`, simétrico a `FiltroCompanias` ya existente, en vez de seguir sumando parámetros
  sueltos a `ListarAsync`.

  El **orden de composición** queda fijado y es el contenido sustantivo de la decisión: `AplicarAlcance(...)`
  (RF-077) → filtro `estado` → filtro `texto` → `CountAsync` (total) → `OrderBy(u => u.Correo)` →
  `Skip`/`Take`. La búsqueda se aplica sobre el `IQueryable` **ya restringido al alcance** y **antes** de
  contar y paginar, por lo que: nunca amplía el alcance (es un `Where` adicional sobre un conjunto ya
  acotado, jamás un punto de entrada alternativo); `total` refleja las coincidencias dentro del alcance, no
  el universo del sistema; y un usuario de la página 2 se encuentra igual que uno de la página 1. `estado` y
  `texto` se combinan con **AND**, como ya ocurre entre `estado`, `tipoCompania` y `texto` en compañías. Sin
  coincidencias ⇒ `200` con `items: []` y `total: 0` — nunca `404`, que en este contrato significa "recurso
  fuera de alcance" (RF-077) y no "búsqueda vacía".

  **No se agregan requisitos de ordenamiento ni de rendimiento**: `OrderBy(u => u.Correo)` ya existe y no
  cambia; CS-002 fija un objetivo de rendimiento para la búsqueda de **personas** (≥100.000 registros), no
  para la de usuarios administrativos, y ningún artefacto vigente establece un umbral para esta operación —
  inventarlo excedería la decisión tomada.

  En el frontend, `UsuariosPage.tsx` deja de filtrar `consulta.data?.items` por correo y pasa `texto` al
  hook `useUsuarios`, que ya deriva su clave de caché del filtro completo (cambiar el texto produce una
  consulta nueva, no una reutilización). Los filtros de **rol**, **compañía** y **solo vigentes** permanecen
  en el cliente: `ux-ui.md` los enumera junto a la búsqueda por correo, pero la decisión D-4 y la enmienda de
  RF-077/UX-22 hablan explícitamente de la **búsqueda**, y el contrato no declara parámetros para ellos.
  Migrarlos al servidor sería alcance nuevo, no el cierre de esta desviación.

- **Rationale**: Adoptar `texto` en vez de acuñar un nombre nuevo (`correo`, `q`, `busqueda`) mantiene una
  sola convención de búsqueda en toda la API: los tres listados paginados del sistema se consultan igual, y
  el cliente HTTP tipado no necesita un caso especial. Aplicar el filtro sobre el `IQueryable` ya acotado
  —en vez de sobre la página materializada— es lo que traduce la semántica aprobada
  (`Scope → Search → Pagination`) a una única línea de LINQ, sin capa nueva ni consulta adicional: el alcance
  no se "vuelve a aplicar" después de buscar porque nunca se soltó. Devolver una página vacía en vez de
  `404` preserva la propiedad de ocultamiento de existencia: un correo fuera del alcance y un correo
  inexistente producen exactamente la misma respuesta, de modo que la búsqueda no puede usarse como oráculo
  de enumeración.
- **Alternatives considered**: Mantener el filtrado en cliente (descartado por la decisión de negocio D-4,
  Opción B: no encuentra a quien esté en otra página); buscar también sobre los identificadores de rol o
  compañía dentro del mismo parámetro `texto` (descartado: `people.yaml`/`companies.yaml` restringen `texto`
  a atributos textuales del propio recurso, y mezclar criterios heterogéneos en un parámetro libre haría el
  resultado impredecible); agregar parámetros server-side para rol/compañía/vigentes en la misma pasada
  (descartado: alcance mayor que la desviación, no respaldado por la decisión tomada); usar búsqueda de
  texto completo de SQL Server (descartado: exige infraestructura de catálogo adicional que la Constitución
  obliga a justificar con un requisito explícito, inexistente aquí).

## 35. Cambio post-Baseline VF-007 — contención de perfiles y permisos de alcance PERSONA (RF-082)

> Cambio de requisito posterior al cierre del Baseline de Etapa 1 (T001–T242), formalizado en spec.md
> (Sesión 2026-09-25, RF-082, CS-042, CS-043) a partir del hallazgo VF-007
> (`docs/functional-validation/post-baseline-validation.md` §9). §25 conserva la decisión histórica. Esta
> sección registra solo las decisiones **técnicas** necesarias para implementar RF-082; la regla funcional
> vive en spec.md y no se repite aquí.

### 35.1 Punto de integración

- **Decision**: Reutilizar `ContencionTemporalValidator.ValidarAsync(personaId, inicio, fin)` sin cambiar su
  lógica. Se inyecta en `EstadoEfectivoService` (alta de perfil) y en `PermisoAccesoService` (alta y
  actualización de permisos de alcance PERSONA). En el alta de perfil se invoca justo después de
  `Vigencia.NormalizarRango` y antes de verificar el `TipoPersona`, el mismo orden que usa
  `ContextoOperativoService` (normalizar → contener → reglas propias). En `CrearAsync` se invoca después de
  `ValidarSujetoAsync`, porque sin una persona existente no hay pertenencia que consultar. En `ActualizarAsync`
  se invoca después de la comprobación de alcance inmutable y antes de mutar la entidad, usando el
  `PersonaId` almacenado. En los permisos PERSONA, **antes** del validador se exige el alcance histórico del
  actor sobre la persona (§35.6), de modo que el orden completo es: sujeto existente → alcance del actor
  sobre la persona → contención.
- **Rationale**: RF-082 exige la misma forma de contención que RF-072 (D1). El validador ya implementa
  exactamente esa forma, ya resuelve la pertenencia de referencia "vigente por fechas en el instante de la
  operación" y ya emite `400 SIN_PERTENENCIA_VIGENTE` y `409 FUERA_DE_CONTENCION_TEMPORAL`. T107 y T165
  exigen un único punto de validación, y la prueba estructural `El_validador_es_el_unico_punto...` lo
  verifica. Validar el sujeto antes de la contención conserva el `400` actual ante una persona inexistente,
  en vez de sustituirlo por `SIN_PERTENENCIA_VIGENTE`.
- **Alternatives considered**: Una variante del validador para estas dos entidades (descartada: D1 confirma
  la misma regla, y una segunda implementación rompería el criterio de punto único). Un `CHECK` o trigger
  de base de datos (descartado por las mismas razones que en §25: cruza tablas y además no puede distinguir
  las operaciones de D4).

### 35.2 Predicado de D4 como función pura

- **Decision**: Aislar en una función estática pura de `Application/Permissions` la decisión de si una
  escritura de `PermisoAcceso` requiere contención. La función recibe el alcance, el estado previo (o
  "nuevo"), el estado resultante y si cambiaron las fechas, y devuelve `true` solo si el alcance es `PERSONA`,
  el estado resultante es `ACTIVO` y además (a) el registro es nuevo, (b) cambiaron las fechas o (c) el
  estado pasa de `INACTIVO` a `ACTIVO`. En perfiles el predicado es trivial, porque hoy solo existe el alta
  (caso a) y no necesita función.
- **Rationale**: La tabla de D4 tiene más combinaciones que casos de prueba de integración razonables. Una
  función pura permite probarla de forma exhaustiva con una prueba unitaria parametrizada, y dejar la
  integración para los casos representativos de CS-042 y CS-043.
- **Alternatives considered**: Condicionales en línea dentro de `ActualizarAsync` (descartado: la matriz
  solo se podría verificar mediante integración, más lenta y menos exhaustiva).

### 35.3 Detección de "cambio de fechas"

> **[Superada para `PermisoAcceso` por VF-004 — ver §36.3]** Con fechas civiles (RF-083), un extremo "cambia"
> cuando cambia su fecha civil, y la tolerancia de 1 ms deja de aplicarse al permiso
> (`ReglaContencionPermiso.FechasCambian` se retira en T292). El texto siguiente se conserva como registro
> histórico de VF-007.

- **Decision**: Las fechas se consideran cambiadas si alguno de los dos instantes recibidos (tras
  `InstanteUtc.Desde`) difiere del almacenado en 1 ms o más.
- **Rationale**: La columna es `datetime2(3)`, que redondea a milisegundos. Una comparación exacta en ticks
  trataría como "cambiada" una fecha reenviada sin modificar con precisión submilisegundo, y bloquearía un
  cambio de solo bloques sobre un registro histórico (CS-043 b). El formulario ya reenvía los valores
  originales sin tocarlos (`conservarSiNoCambio`), pero el servidor no debe depender de eso.
- **Alternatives considered**: Pedir al cliente que omita las fechas no modificadas (descartado: cambia el
  esquema de la petición, que RF-082 no requiere).

### 35.4 Contratos y snapshot OpenAPI

- **Decision**: `contracts/people.yaml` y `contracts/permissions.yaml` pasan de `1.0.0` a `1.1.0` (MINOR,
  aditivo). Se declaran las respuestas `400` y `409` en `POST /api/personas/{id}/perfiles`, y `409` en
  `POST` y `PUT /api/permisos`, además de las descripciones de `400` ya existentes, sin cambiar ningún
  esquema. Los controladores deben declarar esos códigos con `ProducesResponseType`.
- **Rationale**: `OpenApiSnapshotTests` exige que cada código declarado en el contrato exista en la API
  publicada. Entre este plan y la implementación, esa prueba estará en rojo para las tres operaciones. Es
  el mismo estado transitorio que el proyecto ya aceptó con D-1 (§34): el contrato es el artefacto de
  diseño y el código lo alcanza después.
- **Alternatives considered**: Actualizar el contrato en la misma tarea que el código (descartado: rompe el
  flujo del proyecto, donde `/speckit-plan` actualiza los contratos).

### 35.5 Frontend: ayuda al usuario, nunca regla de negocio

> **[Superada en parte para `PermisoAcceso` por VF-004 — ver §36.7]** El principio se mantiene (el cliente
> orienta y el servidor decide), pero en el formulario de permisos el límite deja de calcularse sobre el
> instante UTC de la pertenencia con `datetime-local`. Pasa a usar las fechas declaradas de la pertenencia
> con controles de fecha (RF-083 (c)). La parte de perfiles no cambia.

- **Decision**: El frontend refleja la regla sin sustituirla. En los perfiles se calcula `min`/`max` a partir
  de la pertenencia vigente, que el historial de la persona ya obtiene con `useHistorialCompanias`. En los
  permisos se intenta obtener esa pertenencia al elegir la persona. Si la consulta falla, por ejemplo con
  `404` porque el usuario administra el área pero no tiene alcance sobre la persona (RF-049 frente a
  RF-077), el formulario funciona sin límites y el backend decide. En ambos casos se muestran mensajes
  específicos para `SIN_PERTENENCIA_VIGENTE` y `FUERA_DE_CONTENCION_TEMPORAL`. El límite del permiso se
  calcula sobre el instante UTC de la pertenencia (23:59:59.999 UTC), convertido a la hora local del control
  `datetime-local`.
- **Rationale**: La Constitución (Principio I) sitúa la validación en el servidor. El límite en el cliente
  evita el error más común, pero no puede ser obligatorio porque el cliente no siempre ve la pertenencia.
  Calcular el máximo en UTC evita que un fin "31/07 23:59" en hora local, que en UTC es posterior, parezca
  válido en el control y se rechace en el servidor. La normalización a día completo de los permisos
  (VF-004) queda fuera de alcance.
- **Alternatives considered**: Exponer la vigencia de la pertenencia dentro del DTO de la persona o del
  permiso (descartado: cambia esquemas que RF-082 no requiere cambiar).

### 35.6 Frontera de alcance antes de consultar la pertenencia (corregida tras `/speckit-analyze`, hallazgo C1)

> La versión anterior de esta sección aceptaba que `SIN_PERTENENCIA_VIGENTE` revelara a un administrador de
> área el estado de pertenencia de la persona. `/speckit-analyze` lo clasificó como CRITICAL (C1): con
> `400`/`409` se podía inferir, probando fechas, la vigencia exacta de la pertenencia de una persona cuya
> compañía está fuera del alcance del actor. Eso viola el Principio I ("un usuario NUNCA DEBE poder leer …
> datos de una compañía fuera de su alcance autorizado, incluso si conoce el identificador"). Esa
> aceptación queda **retirada** y sustituida por la decisión siguiente.

- **Decision**: En toda escritura de un `PermisoAcceso` con `Alcance = PERSONA` que, según D4, requiera
  contención, el orden es obligatorio:
  1. Primero se evalúa el alcance del actor sobre la persona, reutilizando exactamente
     `PersonaService.ExigirAlcanceHistoricoAsync(personaId)`: el mismo control que ya protege el alta de
     perfil (`EstadoEfectivoService`), sin una lógica paralela de autorización.
  2. Si la persona está fuera del alcance del actor, la operación termina con `404 RECURSO_NO_ENCONTRADO`,
     indistinguible de un recurso inexistente.
  3. Solo después de superar el alcance se consulta la pertenencia vigente (`ContencionTemporalValidator`).
  4. Por tanto, `400 SIN_PERTENENCIA_VIGENTE` y `409 FUERA_DE_CONTENCION_TEMPORAL` nunca pueden usarse para
     inferir datos temporales de una persona fuera del alcance del actor.

  El alcance se evalúa en el `PersonaId` de la petición (alta) o en el almacenado (actualización). Las
  escrituras que D4 no somete a contención (solo bloques, desactivar, registro resultante `INACTIVO`) y los
  alcances `UNIDAD_ORGANIZATIVA`/`COMPANIA` no consultan la pertenencia, así que no exponen nada y no cambian:
  VF-007 no añade autorización fuera de su propio punto de exposición.
- **Rationale**: El riesgo solo aparece cuando el servidor consulta la pertenencia, así que la protección se
  coloca justo antes de esa consulta. `ExigirAlcanceHistoricoAsync` ya resuelve el alcance histórico
  (pertenencias y contextos operativos, RF-077 con la unión de D3). Una persona sin ningún histórico sigue
  siendo administrable por cualquiera, como en el alta de perfil. Así, el contratista con contexto en la
  Principal del área, que es el caso legítimo de RF-049, conserva el acceso.
- **Fuera de alcance, deuda preexistente**: `ValidarSujetoAsync` ya respondía, antes de VF-007, `400` si la
  persona no existe, lo que revela su existencia a un administrador de área. VF-007 no la corrige ni la
  convierte en tarea: queda registrada como observación independiente en
  `docs/functional-validation/post-baseline-validation.md` §9.9.
- **Alternatives considered**: Mantener la aceptación del riesgo (descartado: viola el Principio I y la
  constitución no se relaja desde research). Crear una comprobación de alcance propia de permisos
  (descartado: duplicaría la autorización que `PersonaService` ya centraliza). Exigir el alcance de la
  persona en todas las escrituras PERSONA, incluidas las que no consultan la pertenencia (descartado: cambia
  comportamiento del Baseline sin que VF-007 lo requiera).

### 35.7 Numeración de tareas

- **Decision**: Las tareas de VF-007 se generan en `/speckit-tasks` como un bloque nuevo y separado, marcado
  como post-Baseline, a continuación de T242 y sin tocar ninguna tarea anterior. El número exacto lo fija
  `/speckit-tasks`.
- **Rationale**: Es el precedente del proyecto para bloques delta (T169–T228 y T229–T242). La trazabilidad se
  mantiene citando RF-082 y VF-007 en cada tarea, sin modificar las tareas históricas.

## 36. Cambio post-Baseline VF-004 — vigencia diaria del permiso de acceso (RF-083)

> Cambio de requisito posterior al cierre del Baseline de Etapa 1, formalizado en spec.md (Sesión 2026-09-25
> VF-004, RF-083, CS-044 a CS-047; decisiones F-1 a F-7) a partir del hallazgo VF-004
> (`docs/functional-validation/post-baseline-validation.md` §19). Esta sección registra solo las decisiones
> **técnicas**; la regla funcional vive en spec.md y no se repite aquí.

### 36.1 Conversión de una fecha civil a instantes UTC (F-1, cambios de horario)

- **Decision**: Una clase estática pura nueva, `VigenciaDiariaPermiso` (capa `Application`, carpeta
  `Permissions`), que recibe una `DateOnly` y una `NodaTime.DateTimeZone`:
  - inicio = `zona.AtStartOfDay(LocalDate.FromDateOnly(fecha))`, convertido a `DateTime` UTC;
  - fin = `zona.AtStartOfDay(LocalDate.FromDateOnly(fecha).PlusDays(1))` − 1 ms, convertido a `DateTime` UTC;
  - fecha civil de un instante = `Instant.FromDateTimeUtc(instante).InZone(zona).Date`, como `DateOnly`.

  `AtStartOfDay` devuelve el **primer instante válido** del día. Si las 00:00 no existen porque el reloj salta
  de 00:00 a 01:00 (p. ej. `America/Santiago` al empezar el horario de verano), devuelve las 01:00. Si las
  00:00 ocurren dos veces, devuelve la primera. Como el fin se calcula a partir del inicio del día siguiente,
  el día dura 23, 24 o 25 horas según corresponda, sin suponer que 23:59:59.999 local existe o es único.
  `Application` ya referencia NodaTime 3.3.4, que expone `LocalDate.FromDateOnly`.
- **Caso extremo técnico**: `AtStartOfDay` lanza `SkippedTimeException` si la zona omite un día **entero**.
  En tzdb solo ocurre en fechas históricas (p. ej. `Pacific/Apia`, 30/12/2011). Se traduce a `400`
  `VALIDACION_ENTRADA` ("la fecha no existe en la zona horaria de la Compañía Principal"), sin inventar una
  regla de negocio. Es inalcanzable con fechas actuales.
- **Rationale**: Es la única forma de cumplir F-1 cuando hay cambio de horario. Una función pura con la zona
  como parámetro se prueba en `EnterpriseAccessControl.UnitTests`, que solo referencia `Application`, con
  zonas tzdb reales y sin base de datos.
- **Alternatives considered**: Reutilizar `Vigencia.NormalizarInicio/NormalizarFin` (descartado: normalizan el
  día **UTC**, no el de la zona; ver registro §19.6). Construir 00:00 y 23:59:59.999 locales con
  `LocalDateTime` e `InZoneLeniently` (descartado: 00:00 puede no existir y 23:59:59.999 no es "el primer
  instante del día siguiente − 1 ms" si el día siguiente empieza a las 01:00). `TimeZoneInfo` (descartado por
  research.md §5 y §31: no expone con claridad los huecos y ambigüedades, y los identificadores IANA dependen
  de la plataforma).

### 36.2 Zona que define el día: Área → Compañía Principal → zona efectiva

- **Decision**: La zona es la de la Compañía Principal propietaria del área
  (`ÁreaAcceso.CompañíaPrincipalId` → `Compañía.ZonaHorariaIana`). Se resuelve con el mismo criterio que el
  paso 13: `IRelojEmpresarial.ZonaEfectiva(zonaIana)` devuelve un identificador utilizable (el propio, o el
  global de respaldo si falta o no es válido) y `DateTimeZoneProviders.Tzdb[id]` lo convierte en
  `DateTimeZone`. `IRelojEmpresarial` no cambia.
  - Alta: la consulta que hoy verifica que el área esté en el alcance del actor (`ExigirAreaEnAlcanceAsync`)
    devuelve además la zona de su Principal. Así la zona solo se lee **después** de confirmar el alcance, y un
    área fuera de alcance sigue produciendo `404`.
  - Actualización: la zona se lee a partir del `AreaAccesoId` almacenado, después de `ObtenerEnAlcanceAsync`.
    El área de un permiso no es editable.
  - Lectura (listado y detalle): una consulta por lote obtiene la zona de las Principales de los permisos de
    la página, igual que `BloquesDeAsync` obtiene los bloques.
- **Rationale**: RF-083 exige que la vigencia y los bloques se interpreten en la **misma** zona. Reutilizar
  `ZonaEfectiva` garantiza que la conversión y el paso 13 usan siempre la misma, incluido el respaldo, sin
  duplicar la resolución.
- **Alternatives considered**: Añadir a `IRelojEmpresarial` métodos de inicio de día (descartado: obligaría a
  cambiar el reloj falso `EscenarioEvaluacion.RelojLima` sin beneficio, porque la conversión no depende del
  reloj sino solo de la zona). Fallar si la Principal no tiene zona (descartado: el paso 13 cae en el respaldo,
  y un criterio distinto rompería la coherencia que exige RF-083).

### 36.3 Edición por extremo (F-6) y supuestos de D4

- **Decision**: En `ActualizarAsync` cada extremo se resuelve por separado con una función pura de
  `VigenciaDiariaPermiso`. Si la fecha solicitada es igual a la fecha civil del instante almacenado (calculada
  en la zona efectiva **actual**), se conserva exactamente ese instante. Si difiere, se normaliza según §36.1.
  "Cambian las fechas", a efectos de `ReglaContencionPermiso.RequiereContencion` (D4, sin cambios), significa
  que **algún** extremo cambió de fecha civil.
  `ReglaContencionPermiso.FechasCambian` (comparación de instantes con tolerancia de 1 ms, §35.3) deja de
  usarse para los permisos y se elimina junto con sus casos de prueba. La matriz de `RequiereContencion` no
  cambia.
  **Tras resolver ambos extremos** se exige `FinUtc > InicioUtc` (RF-039), o `400 PERIODO_INVALIDO`, antes de
  la contención y de persistir. Con un extremo antiguo conservado, dos fechas civiles válidas pueden producir
  instantes vacíos o invertidos (p. ej. un fin antiguo a las 00:00 locales del día Dx y un inicio nuevo en Dx),
  y no existe CHECK en base de datos que lo impida. *(Añadido tras `/speckit-analyze`, hallazgo U1.)*
- **Rationale**: Con fechas civiles, "reenviar sin cambios" significa reenviar la misma fecha, y la
  tolerancia de milisegundos pierde su sentido. Decidirlo en el servidor cumple F-6 aunque el cliente no
  conserve los instantes originales.
- **Alternatives considered**: Mantener `conservarSiNoCambio` en el cliente (descartado: F-6 exige que decida
  el servidor, y con un contrato `date` el cliente ya no puede reenviar instantes). Normalizar ambos extremos
  si cambia uno (descartado por F-6).

### 36.4 Contención de RF-082 por fecha civil (F-2)

- **Decision**: `ContencionTemporalValidator` gana una variante para permisos,
  `ValidarFechasCivilesAsync(personaId, DateOnly inicio, DateOnly fin)`, con su comprobación pura
  `ValidarFechasCiviles(pertenencia, inicio, fin)`. Obtiene la pertenencia vigente con el método actual
  (`ObtenerPertenenciaVigenteAsync`, sin cambios) y compara: `inicio >= fechaDeclaradaInicio` **y**
  `fin <= fechaDeclaradaFin`, con igualdad válida. Usa los mismos códigos (`SIN_PERTENENCIA_VIGENTE` `400`,
  `FUERA_DE_CONTENCION_TEMPORAL` `409`) y los mismos mensajes. `ValidarAsync` (por instantes) no cambia y
  sigue siendo el de RF-072 y de los perfiles.
  - **Fecha declarada de la pertenencia** = `DateOnly.FromDateTime(pertenencia.FechaHoraInicio)` y
    `DateOnly.FromDateTime(pertenencia.FechaHoraFin)`. Toda `AsignaciónPersonaCompañía` se crea o renueva por
    `HistorialPersonaService`, que aplica `Vigencia.NormalizarRango` o `Vigencia.NormalizarFin`. Ambos guardan
    el día declarado en sus límites UTC (00:00:00.000 y 23:59:59.999 UTC), así que los componentes de fecha UTC
    son exactamente el día declarado. No es una reconversión a la zona del permiso (RF-083 (c)).
  - **Fechas civiles del permiso** = las fechas de la petición. Para un extremo conservado (§36.3), la fecha
    solicitada coincide por definición con la fecha civil de su instante almacenado.
  - **Orden de autorización (sin cambios, §35.6)**: primero el alcance del actor sobre el área (`404`),
    después el sujeto, después el alcance histórico del actor sobre la persona (`404`) y solo entonces la
    pertenencia y la contención. Por último, la escritura.
- **Rationale**: Es la única forma de que dos rangos con las mismas fechas civiles no se rechacen por la
  diferencia de representación UTC (en Lima, el fin del permiso es 04:59:59.999 UTC del día siguiente y el de
  la pertenencia, 23:59:59.999 UTC). No depende de corregir las pertenencias (§19.6, fuera de alcance): si
  algún día se corrigen, la fecha declarada se seguirá obteniendo del mismo registro.
- **Alternatives considered**: Convertir el instante de la pertenencia a la zona del permiso (descartado: en
  Lima, 00:00 UTC del 01/08 es el 31/07 local y rechazaría el primer día). Comparar instantes (descartado por
  F-2).

### 36.5 Contrato `permissions.yaml` v2.0.0 (F-3)

- **Decision**:
  - **Petición** (`PermisoAccesoRequest`, `POST` y `PUT /api/permisos`): `fechaInicioVigencia` y
    `fechaFinVigencia`, `type: string, format: date`, obligatorias. Sustituyen a `fechaHoraInicioVigencia` y
    `fechaHoraFinVigencia`, que desaparecen de la petición.
  - **Respuesta** (`PermisoAcceso`, en `GET`, `POST` y `PUT`):
    - se conservan `fechaHoraInicioVigencia` y `fechaHoraFinVigencia` (`date-time`), ahora documentadas como
      los **instantes UTC efectivos** que evalúa el paso 12;
    - se añaden `fechaInicioVigencia` y `fechaFinVigencia` (`date`, fecha civil en la zona efectiva actual);
    - se añade `vigenciaEnDiasCompletos` (`boolean`, §36.6);
    - se añade `zonaHorariaIana` (`string`, zona efectiva con la que se calcularon las fechas civiles).
  - **Nombres**: siguen la convención ya existente, en la que `fecha*` es una `date` (`fechaNacimiento` en
    `people.yaml`) y `fechaHora*` es un `date-time` (`fechaHoraInicio` en todos los contratos). `zonaHorariaIana`
    es el nombre que ya usa `Compañía`.
  - **Versión**: 1.1.0 → **2.0.0**. Renombrar y cambiar el tipo de campos obligatorios de la petición es
    incompatible. Los añadidos de la respuesta son compatibles.
  - **Compatibilidad**: sin doble versión ni aceptación de los campos antiguos. Los únicos consumidores son la
    SPA del proyecto y los helpers E2E, que se despliegan con el mismo commit. Un cliente antiguo que envíe
    `fechaHora*` recibe `400` con `fechaInicioVigencia`/`fechaFinVigencia` entre los errores de validación: la
    API falla cerrada y nunca interpreta mal un instante.
  - **OpenAPI**: en C#, `DateOnly` se publica como `type: string, format: date` (precedente: `fechaNacimiento`).
    `OpenApiSnapshotTests` compara nombres de propiedades por operación, así que queda en rojo desde que se
    actualiza el contrato hasta que se implementa el DTO. Es el estado esperado, igual que en VF-007.
- **Rationale**: F-3 exige `date` en la petición. Mantener los instantes en la respuesta conserva la
  trazabilidad de lo que realmente se evalúa y hace posible la presentación con hora de F-5.
- **Alternatives considered**: Reutilizar `fechaHora*` con `format: date` (descartado: nombre engañoso y rompe
  la convención). Aceptar ambos formatos durante una transición (descartado: sin consumidores externos no hay
  nada que proteger, y duplicaría la lógica). Quitar los instantes de la respuesta (descartado: F-5 los
  necesita para los permisos históricos).

### 36.6 Presentación (F-5)

- **Decision**: El servidor calcula `vigenciaEnDiasCompletos = (inicio == InicioUtc(FechaCivil(inicio))) &&
  (fin == FinUtc(FechaCivil(fin)))` en la zona efectiva actual. En el listado de la SPA:
  - si es `true`, se muestran solo `fechaInicioVigencia` – `fechaFinVigencia`, formateadas como fechas sin
    conversión de zona;
  - si es `false`, se muestran `fechaHoraInicioVigencia` – `fechaHoraFinVigencia` con fecha y hora en
    `zonaHorariaIana`, mediante `formatearFechaHora`.

  Esto sustituye el `toLocaleString()` actual, que usa la zona del navegador y no la de la Principal (RF-080).
  Tras un cambio de zona, un permiso cuyos instantes dejan de coincidir con los límites de día pasa a mostrarse
  con hora (RF-083 (h)).
- **Rationale**: Una sola implementación de tzdb (NodaTime) decide qué es "un día completo". `Intl` del
  navegador no ofrece "primer instante del día", y duplicarlo en TypeScript arriesga divergencias en los días
  con cambio de horario.
- **Alternatives considered**: Calcularlo en el cliente (descartado por lo anterior).

### 36.7 Formulario de permisos

- **Decision**:
  - Controles `type="date"` para inicio y fin.
  - Validación zod: ambas obligatorias y fin ≥ inicio. La comparación léxica de `AAAA-MM-DD` es cronológica, y
    la igualdad es válida (CS-045).
  - En edición, los valores iniciales son `fechaInicioVigencia` y `fechaFinVigencia` de la respuesta, y se
    envían tal cual. Se elimina `conservarSiNoCambio`: el servidor decide (§36.3).
  - Si `vigenciaEnDiasCompletos = false`, una nota informativa muestra los instantes actuales con fecha y hora
    en la zona de la Principal y avisa de que conservar una fecha conserva su hora (F-5, F-6).
  - Límite de RF-082: `min`/`max` = fecha declarada de la pertenencia (`fechaHoraInicio.slice(0, 10)` y
    `fechaHoraFin.slice(0, 10)`), el mismo criterio que `PerfilesPersona`. Sustituye, solo para permisos, el
    límite por instante UTC de §35.5. La ayuda muestra la pertenencia como fechas.
  - `lib/fechas.ts` gana dos helpers sin conversión de zona: `fechaDeclarada(iso)` y `formatearFecha(fecha)`.
    `aValorLocal` y `aIsoUtc` se conservan, porque las usan `EvaluacionAccesoPage` y `UsuarioDetalle`.
- **Rationale**: Mantiene el principio de §35.5: el cliente orienta y el servidor decide. Así el límite ya no
  depende de la zona del navegador.

### 36.8 Datos existentes (F-4, F-7)

- **Decision**: Sin migración de base de datos, sin migración de EF Core y sin proceso de corrección.
  Columnas, tipos, índices y restricciones de `PermisoAcceso` no cambian.
  - **A. Permisos existentes con instantes históricos**: se leen, se evalúan (paso 12 sin cambios) y se
    devuelven con sus instantes exactos. Se muestran con hora si no coinciden con límites de día.
  - **B. Permisos nuevos (RF-083)**: se guardan con los instantes de §36.1.
  - **C. Permisos existentes editados (F-6)**: por extremo, según §36.3. Cambiar solo bloques o estado reenvía
    las fechas civiles actuales y conserva ambos instantes.
  - **Cambio de zona (F-7)**: no se toca ninguna fila. Las fechas civiles y `vigenciaEnDiasCompletos` se
    recalculan en cada lectura.
- **Rationale**: F-4 y F-7. Es el mismo criterio no retroactivo de RF-082 (CS-043).

### 36.9 Evaluación de acceso

- **Decision**: Sin cambios en `PermisoAcceso.EstaVigenteEn`, `EvaluadorDeAcceso`, `EvaluacionAccesoService`,
  `BloqueHorarioPermiso`, los bloques ni la cascada. El paso 12 sigue comparando instantes; la novedad está
  solo en cómo se calculan al escribir.
- **Rationale**: RF-083 (b) y RF-080. Minimiza el riesgo de regresión en la evaluación.

### 36.10 Pruebas E2E: reloj del escenario

- **Decision**: `soporte/tiempo.ts` adelanta `inicio` a `referencia − 2 días` y añade `fechaInicioPermiso` y
  `fechaFinPermiso`, que son las fechas declaradas de la pertenencia del escenario (`inicio.slice(0, 10)` y
  `fin.slice(0, 10)`). Así el permiso queda contenido por fecha civil y su primer instante en Lima
  (00:00 = 05:00 UTC de esa fecha) es siempre anterior a `referencia` y a `fueraDelBloque`, a cualquier hora del
  día. `seccion5.ts` y `multi-principal-quickstart.spec.ts` envían los campos `date`.
- **Rationale**: Con el `inicio` actual (`referencia − 3 h`), entre las 22:00 y las 23:59 de Lima la fecha UTC
  de `inicio` sería la de `referencia`, y el permiso diario empezaría después de ella: el E2E fallaría según la
  hora de ejecución.

### 36.11 Numeración de tareas

- **Decision**: `/speckit-tasks` genera un bloque separado "POST-BASELINE — VF-004" a partir de T287, sin tocar
  T001–T286.
