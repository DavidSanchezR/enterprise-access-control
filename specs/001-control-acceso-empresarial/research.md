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
  zona horaria empresarial (`America/Lima`) se exponen mediante el **Options Pattern** de ASP.NET Core
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
  debería dispararse ante una condición de carrera genuina). La conversión a `America/Lima` (NodaTime `DateTimeZoneProviders.Tzdb["America/Lima"]`)
  ocurre únicamente en la evaluación de bloques horarios (Historia 8) y en la capa de presentación.
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

- **Decision**: Un servicio de dominio `EvaluadorDeAcceso` implementa, en orden, los siguientes 14 pasos:
  1. Verificar que el usuario autenticado que solicita la evaluación tenga, en su
     `AlcanceUsuarioCompañía`, la Compañía Principal que se determinará en el paso 4 (RF-005, RF-049;
     preservado de la lista anterior).
  2. Identificar a la persona evaluada.
  3. Identificar el `ÁreaAcceso` evaluada.
  4. Determinar la Compañía Principal propietaria del área vía `ÁreaAcceso.CompañíaPrincipalId`.
  5. Verificar que exista un `ContextoOperativoPersonaPrincipal` vigente entre la persona y esa Principal en
     la fecha evaluada, **y que esa relación siga siendo legítima según la compañía de pertenencia vigente
     de la persona en esa misma fecha** (RF-061, §13 — auditoría de consistencia, cierra Decisión Pendiente
     #4): si esa compañía vigente es exactamente la Principal evaluada, la legitimidad es automática
     (RF-053); si es `CONTRATISTA`, verificar que la `RelaciónContratistaPrincipal` correspondiente esté
     vigente en esa misma fecha (RF-054, RF-059); en cualquier otro caso (p. ej. la persona cambió de
     compañía de pertenencia a una Contratista sin relación vigente con esta Principal, o a una Principal
     distinta) el contexto se considera no legítimo. Sin contexto vigente o sin legitimidad vigente ⇒
     DENEGADO (`SIN_CONTEXTO_OPERATIVO_VIGENTE`), sin evaluar el resto de los pasos. Esta re-validación es
     dinámica (recalculada en cada evaluación) y **no** cierra ni modifica el registro de
     `ContextoOperativoPersonaPrincipal` en sí.
  6. Verificar que exista una `AsignaciónCredencial` **vigente** para la persona y la Compañía Principal
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
  7. Verificar que el área esté `ACTIVA`.
  8. Verificar que algún perfil (`TipoPersona`) vigente de la persona esté autorizado en el área (RF-024,
     Historia 7; preservado de la lista anterior).
  9. Determinar la unidad organizativa vigente de la persona dentro de ese contexto operativo (vía
     `AsignaciónPersonaUnidadOrganizativa` particionada por `ContextoOperativoId` — §5, §13).
  10. Recolectar permisos aplicables en los tres niveles: PERSONA (directo), UNIDAD_ORGANIZATIVA (la del
      paso 9) y COMPAÑÍA (la compañía de pertenencia vigente de la persona, sea Principal o Contratista —
      research.md §12).
  11. Filtrar por vigencia del permiso en la fecha evaluada.
  12. Filtrar por día de semana y bloque horario en `America/Lima`.
  13. Si hay permisos aplicables en más de un nivel, aplicar precedencia PERSONA > UNIDAD_ORGANIZATIVA >
      COMPAÑÍA.
  14. Conceder o denegar. Ante cualquier paso sin resultado inequívoco, el resultado es DENEGADO
      (Principio I).

  El resultado y sus factores determinantes se pueden loguear para auditoría, sin bloquear la respuesta p95
  requerida por CS-003.
- **Rationale**: Un único servicio de dominio con pasos ordenados y explícitos permite pruebas unitarias
  aisladas por cada corte (sin contexto operativo, relación Contratista-Principal vencida, sin credencial
  vigente, área inactiva, perfil no autorizado, permiso vencido, fuera de bloque horario, conflicto de
  precedencia) tal como exige el Principio VII. Determinar la Principal en el paso 4 **antes** de tocar
  cualquier permiso (paso 10) garantiza que un permiso de la Principal B nunca pueda satisfacer una
  evaluación sobre un área de la Principal A (CS-018), porque los permisos ni siquiera se consultan hasta
  que el contexto operativo y la credencial con la Principal correcta fueron confirmados. Colocar el paso 6
  (credencial) inmediatamente después del paso 5 (contexto operativo) agrupa todas las verificaciones de
  legitimidad/identidad de la persona frente a esa Principal antes de entrar a evaluar elegibilidad de
  perfil/área y permisos — evita, por ejemplo, calcular la unidad organizativa vigente (paso 9) para una
  persona que de todas formas será denegada por falta de credencial.
- **Alternatives considered**: Reglas de precedencia implícitas por orden de consulta SQL (`ORDER BY` +
  `LIMIT 1`) sin servicio de dominio explícito (descartado: dificulta probar unitariamente cada corte y oculta
  la lógica de negocio en la capa de infraestructura); omitir el paso 1 (autorización de la propia consulta)
  y el paso 8 (elegibilidad de perfil/área) por no estar en la lista original de 13 pasos de negocio
  (descartado: ambos siguen siendo RF vigentes — RF-005 y RF-024 — que la lista de negocio no derogó
  explícitamente); colocar el nuevo paso 6 (credencial) al final del algoritmo, justo antes de conceder/
  denegar (descartado: dispersaría las verificaciones de legitimidad de identidad entre el principio y el
  final del algoritmo sin beneficio, y obligaría a calcular unidad organizativa y permisos — pasos 9 a 13 —
  para personas que de todas formas serán denegadas por falta de credencial, sin ganancia de claridad ni de
  rendimiento).

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
  compañías `PRINCIPAL_MANDANTE` — sigue siendo un caso de uso legítimo (research.md §7, paso 9).

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

- **Decision**: Esta corrección NO extiende la cascada de escritura a: (a) el fin de una
  `RelaciónContratistaPrincipal` (protegido solo por la re-validación dinámica de §7 paso 5/RF-059, sin
  escritura equivalente); (b) una `Compañía` marcada `INACTIVO` administrativamente, lo que podría afectar a
  todo su personal simultáneamente. Ambos quedan como Decisión Pendiente #6 en spec.md.
- **Rationale**: Negocio especificó esta regla exclusivamente para el cese de pertenencia Persona–Compañía;
  extender el mismo mecanismo a estos otros dos disparadores sin que negocio lo haya pedido sería modelado
  especulativo. La re-validación dinámica ya existente cubre ambos casos como red de seguridad mínima
  (deniega acceso correctamente), aunque sin el beneficio de auditoría explícita ni de reflejar el estado
  "revocado" en los registros dependientes hasta que negocio confirme que también lo requiere.

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
  2. El **motor de evaluación de acceso** (`EvaluadorDeAcceso`, el servicio de dominio de 14 pasos —
     research.md §7) es un servicio de **Domain/Application** invocado explícitamente por el endpoint
     `POST /api/evaluacion-acceso`; NO se modela como una `AuthorizationPolicy` de ASP.NET Core, porque
     evalúa si una **Persona** (una entidad de negocio evaluada, nunca el `ClaimsPrincipal` de la request
     HTTP) tiene acceso físico a un área — una decisión de negocio con su propio flujo de 14 pasos,
     precedencia y denegación por defecto, no una decisión de "¿puede este usuario llamar a este endpoint?".
- **Rationale**: ASP.NET Core Authorization está diseñado para responder "¿puede el `ClaimsPrincipal` actual
  de la request realizar esta acción?" — encaja naturalmente para el alcance administrativo (1), donde el
  actor evaluado ES el usuario autenticado. No encaja para (2): ahí el actor evaluado es una `Persona` de
  negocio, casi siempre *distinta* del `Usuario` administrativo que dispara la consulta, con un algoritmo de
  14 pasos que ya tiene sus propias pruebas unitarias por corte (research.md §7). Forzar (2) dentro de
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
  `FechaHoraFin` ya pasó, la credencial está temporalmente expirada — deniega el paso 6 de §7 — pero el
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
  temporalmente de la pertenencia, y no se inventa esa dependencia).

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
