# Implementation Plan: Solución Web de Control de Acceso Empresarial

**Branch**: `001-control-acceso-empresarial` | **Date**: 2026-09-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-control-acceso-empresarial/spec.md`; UX/UI specification
from `/specs/001-control-acceso-empresarial/ux-ui.md` (incorporada Sesión 2026-09-14 — ver nota en Technical
Context)

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Construir una aplicación web (API ASP.NET Core 10 + SPA React/TypeScript sobre SQL Server) para administrar
acceso físico empresarial: jerarquías de compañía/unidad organizativa/área de acceso sin ciclos, personas con
históricos de pertenencia sin solapamiento, permisos de acceso con vigencia y bloques horarios semanales
resueltos con precedencia PERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA, mantenimiento de datos maestros
versionados para Perú, y trazabilidad de credenciales/fotocheck. El enfoque técnico prioriza autorización y
validación 100% server-side (denegación por defecto, alcance de compañías del usuario), integridad temporal
aplicada tanto en base de datos (restricciones de exclusión) como en la capa de aplicación, UUID generados por
el sistema, y auditoría automática vía interceptor de EF Core, conforme a la Constitución del proyecto.

## Technical Context

> **Stack tecnológico ratificado explícitamente por el usuario (Sesión 2026-09-14, decisión arquitectónica,
> no negociable)**: .NET 10 LTS + C# + ASP.NET Core 10 Web API + Entity Framework Core 10 + **SQL Server** +
> LINQ. Reemplaza el supuesto anterior de PostgreSQL (que en `spec.md` siempre fue un "stack recomendado",
> nunca una decisión ratificada). Ver research.md §15 a §21 para el detalle completo, incluida la traducción
> del mecanismo de no-solapamiento temporal (contradicción real detectada y resuelta — `EXCLUDE USING gist`
> no existe en SQL Server, ver research.md §5).

> **UX/UI incorporado como entrada formal de diseño (Sesión 2026-09-14)**: [`ux-ui.md`](./ux-ui.md) define la
> experiencia web (estilo "Enterprise Operations Console", design tokens, navegación, 16 pantallas/flujos
> críticos UX-01 a UX-16, 27 componentes base + 9 componentes de dominio, accesibilidad WCAG 2.2 AA,
> responsive desktop-first) como entrada explícita de este plan y de la próxima regeneración de `tasks.md`,
> conforme exige `ux-ui.md` §33. Verificado contra el dominio ya aprobado: el stack de frontend que propone
> (React + TypeScript + Vite) coincide exactamente con el ya ratificado en este Technical Context; los flujos
> de cambio de compañía (§15) y revocación automática (§15, §17, UX-10/UX-11) reflejan correctamente
> RF-061 a RF-065 (finalizar pertenencia → revocar contextos/UO/credenciales dependientes → preservar
> histórico, nunca eliminar); el aislamiento de contextos por Compañía Principal (§12, UX-09) refleja
> correctamente RF-052/RF-055/RF-057 (contextos, UO, permisos y credenciales simultáneos e independientes
> por Principal, sin límite de cardinalidad — CS-030). `ux-ui.md` §32 reitera explícitamente que no autoriza
> cambios de cardinalidad, relaciones, vigencias, revocación automática, autorización ni aislamiento por
> Principal — no se modificó ninguna regla de negocio, entidad, campo, `research.md` ni `data-model.md` como
> resultado de esta incorporación en sí. Sí se detectaron una contradicción real y dos vacíos de capacidad,
> **resueltos en una sesión de `/speckit-clarify` posterior (Sesión 2026-09-14, "integración `ux-ui.md`")**:
> (1) la credencial SÍ gatilla la denegación de acceso (RF-066, nuevo paso 6 del algoritmo, ahora de 14
> pasos — reemplaza la afirmación anterior de Historia 9); (2) Dashboard/Históricos-transversal/Auditoría
> requieren endpoints agregados dedicados server-side (RF-067 a RF-069), resuelto por aplicación directa del
> Principio I, sin necesitar pregunta interactiva; (3) `AsignaciónCredencial` NO necesita un `MotivoFin`
> propio (confirmado, sin cambio de modelo). Ver "Re-chequeo" más abajo, spec.md Clarifications, y
> research.md §7, §22, §23 para el detalle completo de las tres decisiones.

**Language/Version**: C# 13 / .NET 10 LTS (backend); TypeScript 5.6+ con React 18 (frontend)

**Primary Dependencies**: ASP.NET Core 10 Web API; Entity Framework Core 10 (proveedor
`Microsoft.EntityFrameworkCore.SqlServer`) + EF Core Migrations para evolución de esquema; LINQ como
mecanismo normal de consulta (sin repositorio genérico adicional solo por abstracción — research.md §15);
FluentValidation; NodaTime (manejo de zonas horarias IANA por Compañía Principal — RF-080, research.md §31;
`America/Lima` es el valor de respaldo por defecto, no una zona única del sistema); ASP.NET Core Identity solo como base de
hashing de contraseñas (`PasswordHasher<T>`) sin su modelo de usuario completo; ASP.NET Core Authentication
(JWT Bearer) + Authorization (Policies/Claims) para el alcance administrativo — distinto del motor de
evaluación de acceso de dominio (research.md §18); Options Pattern para configuración fuertemente tipada
(research.md §2); `Microsoft.AspNetCore.OpenApi` nativo para el contrato/documentación de la API
(reemplaza Swashbuckle — research.md §10); `ProblemDetails` para errores HTTP (RFC 7807/9457, reemplaza el
`ErrorResponse` propio de los contratos — research.md §21); `Microsoft.Extensions.Logging` para logging;
ASP.NET Core Health Checks (`AspNetCore.HealthChecks.SqlServer`) para `/health/live` y `/health/ready`
(research.md §19); ASP.NET Core built-in Dependency Injection; Docker para contenerización; React 18 + Vite,
TanStack Query, React Hook Form + Zod, React Router, una librería de árbol accesible por teclado (p. ej.
Radix/Ark UI primitives o componente propio basado en patrón ARIA `treeview`); sistema de diseño propio
conforme a [`ux-ui.md`](./ux-ui.md) (design tokens de color/tipografía, tipografía Inter con fallback
`system-ui`, componentes base — `DataTable`, `TreeView`, `Timeline`, `Stepper`, `StatusBadge`,
`ConfirmationDialog`, etc. — y componentes de dominio — `CompanyTypeBadge`, `ContextCard`,
`RevocationImpact`, `AccessDecision`, `AuditEvent`, etc. — ver ux-ui.md §4-§5, §22)

**Storage**: SQL Server (motor de base de datos relacional oficial — decisión arquitectónica explícita, ver
nota superior); columnas de fecha/hora `datetime2(3)` en UTC; no-solapamiento de vigencia respaldado por
trigger `AFTER INSERT, UPDATE` por tabla particionada (equivalente idiomático de SQL Server a `EXCLUDE USING
gist`, que no existe en este motor — research.md §5); consultas jerárquicas con CTE recursivos (soportado
nativamente por SQL Server); concurrencia optimista vía `rowversion` en entidades de escritura concurrente
(research.md §16); enums de negocio persistidos como `nvarchar` vía value conversions (research.md §17);
índice clúster sobre `CreatedAt` (en vez de la PK GUID) en las entidades de histórico de alto volumen de
inserción, para evitar fragmentación (research.md §20)

**Testing**: Backend: xUnit + FluentAssertions + Testcontainers.MsSql (integración contra una instancia real
de SQL Server — imprescindible para verificar el trigger de no-solapamiento, que no tiene representación en
el modelo de EF Core y no puede probarse con un proveedor en memoria) + pruebas de contrato basadas en el
OpenAPI generado (incluido el formato `ProblemDetails`). Frontend: Vitest + React Testing Library (unitario/
componentes) + Playwright (end-to-end de los flujos P1)

**Target Platform**: API contenedorizada en Linux (Docker) desplegable en cualquier host compatible con
contenedores; imagen oficial `mcr.microsoft.com/mssql/server` para SQL Server en Docker (compatible con el
despliegue Linux ya asumido); SPA servida como estáticos (o vía el mismo host) y consumida desde navegadores
modernos de escritorio; MVP como aplicación monolítica modular (una API ASP.NET Core, una base SQL Server,
separación interna Domain/Application/Infrastructure/API) — sin microservicios, sin proveedor de identidad
externo, sin mensajería/colas, sin caché distribuido, salvo que un requisito funcional o no funcional
explícito lo justifique en el futuro (decisión de stack, sección 9)

**Project Type**: web (frontend + backend separados, arquitectura backend en capas: Dominio / Aplicación /
Infraestructura / API)

**Performance Goals**: Login + carga de alcance < 10 s (CS-001); búsqueda de personas p95 < 2 s con ≥100,000
personas (CS-002); evaluación de acceso p95 < 500 ms (CS-003)

**Constraints**: 100% de endpoints protegidos deben aplicar autenticación + alcance de compañías (CS-004);
100% de altas/actualizaciones deben auditarse automáticamente (CS-005); 100% de operaciones jerárquicas deben
impedir ciclos (CS-006); 100% de históricos deben permitir reconstrucción temporal a una fecha/hora dada
(CS-007); denegación por defecto ante ambigüedad de permisos (Constitución, Principio I); accesibilidad
objetivo **WCAG 2.2 AA** en toda la interfaz (ux-ui.md §26 — contraste, navegación completa por teclado,
foco visible, estados nunca dependientes solo del color, ARIA en `TreeView`/tablas/diálogos); responsive
desktop-first con soporte hasta <768px (ux-ui.md §25)

**Scale/Scope**: ≥100,000 personas, 22 entidades de dominio (ver data-model.md; incluye
`CompañíaPrincipalUnidadOrganizativaRaiz` de la corrección Compañía Principal/Contratista, y
`RelaciónContratistaPrincipal` + `ContextoOperativoPersonaPrincipal` de la corrección Contexto Operativo,
ambas de la Sesión 2026-09-14), 10 historias de usuario (8 P1 + 2 P2), 7 grupos de contrato API (auth,
maestros, personas, jerarquías, credenciales, permisos, evaluación de acceso — RF-040); múltiples compañías
`PRINCIPAL_MANDANTE` soportadas simultáneamente, cada una con su propio contexto organizacional aislado
(RF-043); una persona puede tener múltiples contextos operativos, unidades organizativas, permisos y
credenciales simultáneos, uno por cada Compañía Principal (RF-052, RF-055, RF-057)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Justificación |
|---|---|---|
| I. Seguridad Server-Side, Denegación por Defecto y Autorización por Compañías | PASS | Toda autorización y evaluación de permisos se ejecuta en la API (ASP.NET Core); ninguna decisión de acceso depende del cliente. El alcance de compañías del usuario autenticado se aplica mediante un filtro de consulta global de EF Core (`AlcanceUsuarioCompañía`) más verificación explícita en cada caso de uso de Aplicación; ausencia de match válido ⇒ denegado. Extendido en la corrección Compañía Principal/Contratista: `UnidadOrganizativa` (sin FK directa a `Compañía` — RF-044) resuelve su alcance vía `CompañíaPrincipalUnidadOrganizativaRaiz` + recorrido de ancestros hasta la raíz; `ÁreaAcceso` lo resuelve directamente por su `CompañíaPrincipalId` (RF-046, RF-049). Extendido de nuevo en la corrección Contexto Operativo: el algoritmo de evaluación de acceso (research.md §7) determina la Principal propietaria del área ANTES de evaluar cualquier permiso, y exige un `ContextoOperativoPersonaPrincipal` vigente (con `RelaciónContratistaPrincipal` vigente cuando aplica) como precondición de denegación por defecto (RF-059) — el permiso de la Principal B nunca puede satisfacer una evaluación sobre la Principal A. Ver research.md §"Alcance de compañías", §"Jerarquías y prevención de ciclos", §7, §12 y §13. |
| II. Identificadores Únicos Autogenerados (UID/UUID) | PASS | Toda entidad persistente usa `Guid` como PK generado en la capa de Aplicación/Infraestructura (`Guid.CreateVersion7()`), nunca aceptado del cliente ni derivado de campos de negocio. Ver research.md §"Generación de UUID". |
| III. Auditoría Automática y Trazabilidad | PASS | Un `SaveChangesInterceptor` de EF Core estampa `CreatedAt/UpdatedAt/CreatedBy/UpdatedById` en cada operación; estos campos no existen en los DTOs de entrada de la API ni son editables desde el frontend. Extendido en la corrección Revocación Automática: la cascada de revocación (RF-061 a RF-065) reutiliza este mismo interceptor para registrar quién y cuándo se disparó cada revocación, sin necesitar una entidad de log adicional — `MotivoFin`/`RevocadoPorPertenenciaId` (nuevos) completan el resto del rastro de auditoría exigido (research.md §14.2). Ver research.md §"Auditoría automática" y §14. |
| IV. Integridad Temporal e Históricos | PASS | Vigencias con inicio/fin explícitos; un trigger `AFTER INSERT, UPDATE` por tabla (equivalente idiomático de SQL Server a `EXCLUDE USING gist`, que no existe en este motor — contradicción real detectada y resuelta en la Sesión 2026-09-14 "Stack Tecnológico Oficial", research.md §5) impide solapamientos de asignaciones, con clave de partición específica por entidad (p. ej. `AsignaciónPersonaUnidadOrganizativa` por `ContextoOperativoId`, `AsignaciónCredencial` y `RelaciónContratistaPrincipal`/`ContextoOperativoPersonaPrincipal` por par de compañías/persona-Principal — research.md §5, §13) para permitir simultaneidad entre Compañías Principales distintas sin permitir solapamiento dentro de la misma; timestamps persistidos en UTC (`datetime2(3)`), conversión a hora local solo en evaluación de negocio y presentación vía NodaTime, usando la zona de la Compañía Principal correspondiente (RF-080, Sesión 2026-09-20; antes `America/Lima` fijo); baja lógica (`Estado`), nunca eliminación física de históricos. Extendido en la corrección Revocación Automática: la revocación en cascada (RF-061 a RF-065) nunca elimina registros ni modifica `FechaHoraInicio`, solo fija `FechaHoraFin`/`Estado`/`MotivoFin` — coherente con "NO DEBE realizarse eliminación física de historial relevante para trazabilidad". `Estado` es explícitamente administrativo/informativo, nunca la fuente de verdad de vigencia (que sigue siendo siempre la comparación de fechas), evitando una segunda fuente de verdad divergente. Ver data-model.md y research.md §"Modelado temporal", §5 y §14. |
| V. Jerarquías sin Ciclos | PASS | Validación server-side de ausencia de ciclos (recorrido de ancestros vía CTE recursivo) antes de confirmar creación/reubicación de nodos en `UnidadOrganizativa` y `ÁreaAcceso`. Ver research.md §"Jerarquías y prevención de ciclos". |
| VI. Modelado Explícito del Dominio | PASS | Las 22 entidades de data-model.md representan de forma explícita personas, compañías (clasificadas PRINCIPAL_MANDANTE/CONTRATISTA), sus relaciones (`RelaciónContratistaPrincipal`), unidades, áreas, perfiles, permisos, credenciales (tipo/diseño visual, nunca tecnología física — RF-058) y el contexto operativo persona↔Principal (`ContextoOperativoPersonaPrincipal`) con FKs y catálogos versionados; sin campos libres para relaciones de negocio. La relación Contratista↔Principal y la relación operativa persona↔Principal se modelan como entidades explícitas de primera clase (research.md §12, decisión revertida tras corrección de negocio, y §13). |
| VII. Pruebas Automatizadas Obligatorias (NO NEGOCIABLE) | PASS (compromiso de diseño) | Estrategia definida en Technical Context (xUnit + Testcontainers + pruebas de contrato + Playwright). `tasks.md` (fase posterior) deberá generar pruebas por historia P1/P2 cubriendo denegación por defecto, ciclos, solapamientos y fuga entre compañías, conforme CS-008. |
| Reglas de Arquitectura e Ingeniería | PASS | Separación presentación/aplicación/dominio/infraestructura (ver Project Structure); transacciones de EF Core para escrituras que afectan históricos relacionados; UTC persistido y convertido solo en presentación/evaluación; catálogos Perú versionados (research.md §"Semilla de datos maestros"); contraseñas hasheadas (`PasswordHasher<T>`, nunca texto plano); árbol de UI accesible por teclado (frontend, patrón ARIA `treeview`). |

**Resultado**: Sin violaciones. No se requiere la tabla de Complexity Tracking.

**Re-chequeo post-diseño (Fase 1)**: confirmado tras generar [`research.md`](./research.md),
[`data-model.md`](./data-model.md) y `contracts/*.yaml`. Las entidades de data-model.md conservan PK
`Guid`, campos de auditoría y vigencias explícitas sin excepción; las restricciones `EXCLUDE USING gist` y
la validación de ciclos vía CTE quedaron documentadas en research.md §4–§5; ningún contrato en `contracts/`
expone campos de auditoría como editables ni omite el filtro de alcance de compañías. Sin nuevas
violaciones introducidas durante el diseño.

**Re-chequeo post-corrección (Sesión 2026-09-14, Compañía Principal/Contratista)**: confirmado tras
incorporar `TipoCompañía`, la entidad de enlace `CompañíaPrincipalUnidadOrganizativaRaiz` y
`ÁreaAcceso.CompañíaPrincipalId`. La restricción explícita de negocio "no FK directa `UnidadOrganizativa →
Compañía`" se respetó sin dejar sin resolver el aislamiento multi-Principal exigido por RF-043 (ver la
opción elegida en research.md §4). El Principio I se refuerza, no se debilita: ahora `UnidadOrganizativa` y
`ÁreaAcceso` también quedan sujetas al filtro de alcance de compañías, algo que el modelo original (sin esta
corrección) no exigía por carecer de cualquier vínculo con `Compañía`. Sin violaciones nuevas.

**Re-chequeo post-corrección (Sesión 2026-09-14, Contexto Operativo)**: confirmado tras incorporar
`RelaciónContratistaPrincipal`, `ContextoOperativoPersonaPrincipal`, y rescopear la exclusividad de
`AsignaciónPersonaUnidadOrganizativa` (por `ContextoOperativoId`) y `AsignaciónCredencial` (por
`(PersonaId, CompañíaPrincipalId)`). Esta corrección **revierte** una decisión previa de research.md §12
(rechazar `RelaciónContratistaPrincipal`); la reversión está documentada explícitamente con su rationale en
research.md y no deja ninguna violación pendiente: el Principio I se refuerza de nuevo (el algoritmo de
evaluación ahora exige contexto operativo vigente — y relación Contratista-Principal vigente cuando aplica —
como precondición de denegación por defecto, antes incluso de mirar el primer permiso); el Principio IV se
mantiene (todas las nuevas entidades usan `EXCLUDE USING gist` con la clave de partición correcta para
permitir simultaneidad entre Principales sin permitir solapamiento dentro de la misma Principal); el
Principio VI se refuerza (la relación Contratista↔Principal, antes considerada "derivable", ahora es una
entidad explícita de primera clase). Sin violaciones nuevas.

**Re-chequeo post-auditoría (Sesión 2026-09-14, `/speckit-clarify` — cierre de decisiones pendientes)**:
confirmado. Esta sesión resolvió las dos únicas Decisiones Pendientes que quedaban abiertas (cambio de
compañía de pertenencia — RF-061 nuevo; exclusividad del contexto operativo — confirmada sin cambios) sin
introducir ninguna entidad, columna ni migración nueva: el cambio es puramente de lógica de evaluación
(research.md §7 paso 5, §13.1). El Principio I se refuerza una vez más (la re-validación dinámica cierra el
vacío de un contexto automático que sobrevive al cambio de empleador de la persona); el Principio IV se
mantiene sin alteración (ningún registro histórico se modifica ni se cierra en cascada). No quedan
Decisiones Pendientes relacionadas con el modelo de dominio de esta corrección; las restantes (política de
contraseñas, retención legal) son ajenas a esta corrección y no bloquean el diseño. Sin violaciones nuevas.

**Re-chequeo post-corrección (Sesión 2026-09-14, Revocación Automática)**: confirmado. Esta corrección
**reversa** la conclusión del re-chequeo anterior (que aceptaba la re-validación dinámica como único
mecanismo) — negocio exigió revocación en cascada real, con escritura de estado. Cambios: campos
`Estado`/`MotivoFin` nuevos en `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal` y
`AsignaciónPersonaUnidadOrganizativa`; `RevocadoPorPertenenciaId` (FK nueva) en esas tres más
`AsignaciónCredencial`; valor `REVOCADA` nuevo en el `Estado` de `AsignaciónCredencial`. Ninguna entidad
nueva, ninguna migración estructural más allá de columnas adicionales sobre entidades ya existentes. El
Principio I se refuerza (la revocación ahora es una garantía de escritura, no solo de lectura dinámica —
research.md §14.1); el Principio III se refuerza (el rastro de auditoría de la revocación es completo y
consultable sin recalcular nada — research.md §14.2); el Principio IV se mantiene íntegro (ningún registro
se elimina, `FechaHoraInicio` nunca se modifica, `Estado` es explícitamente administrativo y no reemplaza a
las fechas como fuente de verdad de vigencia — research.md §14.2, §14.3). Queda una nueva Decisión
Pendiente #6 (extender o no la misma cascada al fin de `RelaciónContratistaPrincipal` o a la
inactivación de una `Compañía`), explícitamente fuera de alcance de esta corrección. Sin violaciones nuevas.

**Re-chequeo post-corrección (Sesión 2026-09-14, Modelo de Cardinalidad Definitivo)**: confirmado. Corrección
de redacción únicamente — **ninguna decisión de negocio, entidad, campo ni restricción cambia**. Negocio
detectó que RF-014 y RF-062 (spec.md), y su reflejo en research.md §14.4 y data-model.md, estaban
redactadas de forma ambigua, susceptible de malinterpretar que la cardinalidad de `AsignaciónPersonaCompañía`
(RF-014: máximo una activa) limitaba también la de `ContextoOperativoPersonaPrincipal` — nunca lo hizo
(RF-052 siempre permitió, y la restricción `EXCLUDE` de la base de datos siempre implementó, un contexto
vigente sin límite por cada Compañía Principal distinta). Se reescriben RF-014, RF-052 y RF-062 para
eliminar la ambigüedad, y se agrega CS-030 como confirmación explícita e independiente. El Constitution
Check no cambia: los mismos Principios I/III/IV/VI siguen aplicando exactamente como en el re-chequeo
anterior, ahora con redacción inequívoca. Sin violaciones, nuevas ni previas.

**Re-chequeo post-decisión (Sesión 2026-09-14, Stack Tecnológico Oficial)**: confirmado. El usuario ratificó
explícitamente el stack (.NET 10 + C# + ASP.NET Core 10 + EF Core 10 + **SQL Server** + LINQ), reemplazando
el supuesto de PostgreSQL. Se detectaron y resolvieron dos contradicciones reales (no cosméticas): (1) el
mecanismo de no-solapamiento temporal dependía de `EXCLUDE USING gist`, exclusivo de PostgreSQL — se
reemplaza por un trigger `AFTER INSERT, UPDATE` idiomático de SQL Server (research.md §5), preservando
exactamente la misma garantía (invariante respaldado por la base de datos, Principio IV); (2) los 10
contratos definían un `ErrorResponse` propio, incompatible con `ProblemDetails` (exigido explícitamente por
el stack) — se reemplaza en los 10 archivos (research.md §21). Ninguna entidad, RF, historia de usuario ni
regla de negocio del dominio (Compañía Principal/Contratista, Contexto Operativo, revocación automática,
cardinalidades, aislamiento entre Principales) cambió — la corrección es exclusivamente de la capa de
infraestructura/persistencia y del contrato de error HTTP. El Principio IV se reafirma con un mecanismo
distinto pero equivalente; los Principios I/II/III/V/VI/VII no requieren cambios (ninguno depende del motor
de base de datos). Se agregan a Technical Context: concurrencia optimista (`rowversion`, research.md §16),
value conversions de enums (research.md §17), distinción explícita entre ASP.NET Core Authorization
(alcance administrativo) y el motor de evaluación de acceso de dominio (research.md §18, señalada como
importante por el usuario), health checks (research.md §19), y estrategia de clustering de PK GUID
(research.md §20). Sin violaciones nuevas.

**Re-chequeo post-incorporación (Sesión 2026-09-14, UX/UI)**: `ux-ui.md` se incorporó como entrada formal
de diseño (Technical Context, arriba). No modifica ninguna entidad, RF, cardinalidad, relación, vigencia ni
regla de revocación — su propia §32 ("Regla de no desviación") lo prohíbe explícitamente, y la revisión
confirmó que la mayoría del documento no la contradice: los flujos de cambio de compañía y revocación
automática (§15, §17, UX-10/UX-11) reflejan RF-061 a RF-065 tal como están aprobados; el aislamiento de
contextos por Compañía Principal (§12, UX-09) refleja RF-052/RF-055/RF-057 tal como están aprobados,
incluida la cardinalidad sin límite confirmada por CS-030. El Principio I se refuerza en lo demás (§16
exige que la UI nunca presente `Estado` administrativo como si fuera autorización efectiva); el Principio
III se refuerza (§21 exige una vista de auditoría explícita); el Principio V ya contaba con soporte de UI
(patrón ARIA `treeview`, ahora con desambiguación de nombres iguales y breadcrumb — §14).

**Contradicción real detectada y RESUELTA (Sesión 2026-09-14, `/speckit-clarify` "integración `ux-ui.md`")**:
`ux-ui.md` §16 y §19 presentaban la validez/estado de la credencial (`AsignaciónCredencial.Estado`) como un
factor que determina el resultado de la evaluación de acceso — un ítem de checklist "✓ Credencial válida"
en el caso CONCEDIDO, y un ejemplo de DENEGADO con `Motivo: CREDENCIAL_REVOCADA`. El algoritmo
`EvaluadorDeAcceso` entonces aprobado (13 pasos) no incluía la credencial en ningún paso, y Historia 9
afirmaba explícitamente lo contrario ("nunca el mecanismo que por sí solo determina el permiso de acceso").
Esta contradicción se reportó al usuario en vez de resolverse unilateralmente (conforme a instrucción
explícita), y una sesión dedicada de `/speckit-clarify` la resolvió: **la credencial SÍ gatilla la
denegación**. `spec.md` fue modificado: nueva sesión de Clarifications, RF-066 (nuevo), Historia 8
(algoritmo de 14 pasos **en aquel momento** — nuevo paso 6, `SIN_CREDENCIAL_VIGENTE`; el algoritmo vigente
tiene 15 pasos y ese paso es hoy el 7, ver el re-chequeo de cierre de Etapa 1 más abajo y spec.md Historia 8),
Historia 9 (la afirmación anterior
queda `[REEMPLAZADA]`, anotada no eliminada), CS-031 (nuevo). `research.md` §7 se reescribió con el paso
nuevo; `contracts/access-evaluation.yaml` agregó `SIN_CREDENCIAL_VIGENTE` al enum `MotivoDenegacion`.
`data-model.md` **no cambió** (ningún campo nuevo — la cascada de RF-061 ya escribía `Estado = REVOCADA`;
el algoritmo simplemente ahora lo consulta). El Principio I se refuerza (nueva verificación de denegación
por defecto); ningún otro Principio ni RF/CS previamente aprobado cambia. **Consecuencia sin resolver aquí**:
esto crea una dependencia funcional de Historia 8 (P1) sobre Historia 9 (P2) — ver spec.md, Decisiones
Pendientes #7 (nuevo): si Historia 9 debe elevarse a P1 es una decisión de alcance/priorización, no una
decisión de dominio, y queda explícitamente pendiente para cuando se actualice `tasks.md`.

Los dos vacíos de **capacidad** detectados junto con la contradicción también quedaron resueltos en la
misma sesión de `/speckit-clarify`, sin necesitar pregunta interactiva (derivables de principios ya
aprobados):
> 1. **Consultas agregadas/transversales**: resuelto a favor de **endpoints dedicados server-side** para
>    Dashboard, Históricos transversales y Auditoría (RF-067 a RF-069, nuevos en `spec.md`, Historia 10;
>    research.md §22 documenta el rationale) — la composición en cliente se descartó por violar
>    directamente el Principio I (alcance de compañías) en cuanto la vista combine datos de más de una
>    compañía/Principal, y por los objetivos de rendimiento de CS-002/CS-003. La forma concreta del/los
>    endpoint(s) en `contracts/` queda para una futura extensión de `tasks.md`, no se diseña aquí.
> 2. **Asimetría de `MotivoFin` en credenciales**: confirmado que **no es una inconsistencia**.
>    `AsignaciónCredencial.Estado` (ASIGNADO/DEVUELTO/ELIMINADO/REVOCADA) ya es específico por causa, a
>    diferencia del `Estado` genérico de las otras tres entidades revocables — junto con
>    `RevocadoPorPertenenciaId`, satisface RF-063(d) sin necesitar una columna nueva. `data-model.md` y
>    `contracts/credentials.yaml` no cambian (research.md §23).

Sin violaciones de Constitución nuevas. `spec.md`, `research.md` y `contracts/access-evaluation.yaml`
**sí fueron modificados** en la sesión de `/speckit-clarify` referida arriba (no en esta redacción de
`plan.md`, que solo documenta el resultado); `data-model.md` y `contracts/credentials.yaml` no cambiaron
**en esa sesión** — sí cambiaron en la siguiente, ver abajo.

**Re-chequeo post-auditoría (Sesión 2026-09-14, auditoría de consistencia RF-066 —
`/speckit-clarify`)**: una auditoría de consistencia dedicada sobre el paso 6 recién agregado (RF-066)
encontró que `data-model.md` describía `AsignaciónCredencial.FechaHoraFin` como "`null` mientras esté
`ASIGNADO`" — una restricción no derivada de ningún requisito de negocio, que hacía inalcanzable el propio
escenario que RF-066 debía cubrir (una credencial `ASIGNADO` temporalmente expirada). Se reportó como
hallazgo sin corregirlo unilateralmente; negocio confirmó la corrección explícita (RF-070, nuevo):
`FechaHoraFin` puede tener valor, incluso futuro, mientras `Estado = ASIGNADO`; la vigencia efectiva se
determina siempre comparando fechas, nunca por `Estado` en aislamiento — mismo principio que RF-063 ya
aplicaba a las otras tres entidades revocables, ahora extendido explícitamente a `AsignaciónCredencial`.
**Esta vez sí cambiaron** `data-model.md` (campo `FechaHoraFin` corregido, más una frase de "Validaciones
clave" que había quedado sin actualizar desde la sesión anterior — seguía afirmando que la credencial
"nunca" determina el acceso por sí misma) y `contracts/access-evaluation.yaml`/`contracts/credentials.yaml`
(conjunción explícita Estado+ventana temporal). El Principio IV se refuerza (el mismo patrón "`Estado` es
administrativo, la vigencia es siempre por fechas" queda ahora aplicado de forma uniforme a las cuatro
entidades revocables, no a tres de cuatro). Ninguna cardinalidad, aislamiento entre Principales ni entidad
nueva. ~~**Observación sin resolver**: las otras tres entidades revocables comparten la misma redacción de
campo que `AsignaciónCredencial` tenía antes de esta corrección — no evaluada en esta sesión (alcance
explícitamente limitado a credenciales), ver spec.md Decisiones Pendientes #8.~~ **[RESUELTA — ver
re-chequeo siguiente: la Decisión Pendiente #8 se cerró sin requerir el mismo cambio.]**

**Re-chequeo post-auditoría (Sesión 2026-09-14, cierre de Decisión Pendiente #8 — `/speckit-clarify`)**:
confirmado. Una auditoría dedicada sobre si la semántica de vigencia temporal independiente de `Estado`
(RF-070) debía extenderse a `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal` y
`AsignaciónPersonaUnidadOrganizativa` concluyó que **no es necesario**, derivado sin ambigüedad de los
artefactos ya vigentes (sin pregunta interactiva al usuario): `contracts/people.yaml` no expone
`fechaHoraFin` en `AsignacionCompaniaRequest`, `ContextoOperativoRequest` ni
`AsignacionUnidadOrganizativaRequest` — para estas tres entidades, `FechaHoraFin` únicamente se escribe
junto con `Estado` en la misma operación de cierre (`/finalizar`, reemplazo automático, o la cascada de
RF-061), nunca de forma independiente y anterior a un cierre — a diferencia de `AsignaciónCredencial`
(Sesión anterior), para la que negocio confirmó explícitamente el caso de uso de vigencia fija conocida
desde la creación. La redacción actual de `data-model.md` ("`null` mientras esté vigente") para estas tres
entidades es correcta y no comparte el defecto que tenía `AsignaciónCredencial`; RF-063 y research.md §14.2
ya declaraban, de forma general y ya vigente antes de esta sesión, que `Estado` es administrativo y la
vigencia se determina siempre por fechas para las cuatro entidades de la cascada — no se requirió, ni se
agregó, ningún RF nuevo. Ningún artefacto (`spec.md`, `data-model.md`, `research.md`, `contracts/*.yaml`) se
modificó como resultado directo de este cierre. **Hallazgo futuro, sin bloquear el diseño, no resuelto
aquí**: `contracts/credentials.yaml` (`AsignacionCredencialRequest`) tampoco expone `fechaHoraFin` al
crear — el caso de uso de "Credencial Temporal" con vigencia fija desde el inicio que RF-070 habilita a
nivel de dominio no es alcanzable hoy vía ese endpoint. No contradice RF-070 ni el modelo de dominio; es una
brecha de contrato pendiente de una futura extensión, no una decisión de negocio abierta.

**Contradicción real detectada contra `ux-ui.md` — NO propagada, reportada para decisión explícita**:
`ux-ui.md` §13 ("Asignación de UO"), Paso 4 — Vigencia, lista *"FechaHoraFin cuando corresponda"* como un
campo del asistente de asignación de unidad organizativa — sugiriendo que la UI espera poder capturar una
`FechaHoraFin` al **crear** una `AsignaciónPersonaUnidadOrganizativa` (vigencia fija desde el inicio, el
mismo patrón que se confirmó para `AsignaciónCredencial`). Esto está en tensión directa con el cierre de la
Decisión Pendiente #8 de arriba, que concluyó que ese caso de uso **no existe** para esta entidad
precisamente porque `AsignacionUnidadOrganizativaRequest` no expone ese campo. No propago ni asumo cuál de
los dos artefactos debe ceder (ampliar el contrato para igualar `ux-ui.md`, o tratar la frase de `ux-ui.md`
como imprecisa/aspiracional igual que ocurrió con `ux-ui.md` §16/§19 en la sesión de RF-066) — queda como
decisión explícita pendiente, fuera de esta propagación. **[RESUELTA POR UNA DECISIÓN POSTERIOR — ver
re-chequeo siguiente]**: la sesión "vigencia temporal jerárquica" hizo `fechaHoraFin` obligatoria al crear
`AsignaciónPersonaUnidadOrganizativa`, alineando por fin el contrato con lo que `ux-ui.md` §13 ya mostraba —
la tensión desaparece sin que ninguno de los dos artefactos haya tenido que "ceder": el modelo alcanzó a la
UI, no al revés.

**Re-chequeo post-corrección (Sesión 2026-09-14, "vigencia temporal jerárquica" — `/speckit-clarify`)**:
confirmado. Corrección de mayor alcance sobre el modelado temporal: `FechaHoraFin` deja de ser nullable
("vigencia indefinida", incluida la opción recién confirmada para `AsignaciónCredencial` en el re-chequeo
anterior) para las **seis** asociaciones temporales vinculadas a una persona —
`AsignaciónPersonaCompañía` (la raíz), `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`,
`AsignaciónCredencial`, `AsignaciónTipoPersona`, y `PermisoAcceso` (los tres alcances — hallazgo adicional:
RF-021 ya lo exigía desde el spec original, nunca implementado correctamente). Se agrega **RF-072**
(contención temporal): `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y
`AsignaciónCredencial` — las tres que la cascada de RF-061 revoca por dependencia — deben quedar contenidas
dentro de la vigencia de la `AsignaciónPersonaCompañía` vigente al crearse; `AsignaciónTipoPersona` y
`PermisoAcceso` quedan explícitamente excluidos (el modelo de dominio no las establece como dependientes).
**RF-048 se reescribe**: su afirmación de "no existe validación cruzada" queda reemplazada — ahora sí
existe, explícitamente, para las tres dependientes. Se corrigió también una frase equivalente en la sección
`AsignaciónPersonaUnidadOrganizativa` de `data-model.md` que tenía el mismo problema y no se había detectado
en sesiones previas. El Principio IV se refuerza (vigencia siempre por fechas reales, nunca indefinida, para
toda asociación de persona); ninguna cardinalidad, aislamiento entre Principales o regla de
revocación/cascada (RF-061 a RF-065) cambia. `spec.md`, `data-model.md`, `research.md` (§7, §24 corregidas;
§25 nueva) y los 4 contratos que exponen estas entidades (`people.yaml`, `credentials.yaml`,
`permissions.yaml`, `access-evaluation.yaml`) fueron modificados. ~~**Nueva decisión pendiente (no resuelta
aquí)**: si `AsignaciónPersonaCompañía.FechaHoraFin` puede extenderse hacia adelante tras su creación
(renovación) — spec.md, Decisiones Pendientes #9.~~ **[RESUELTA — ver re-chequeo siguiente]**

**Re-chequeo post-corrección (Sesión 2026-09-14, "renovación de AsignaciónPersonaCompañía" —
`/speckit-clarify`)**: confirmado. Cierra la Decisión Pendiente #9: se agrega **RF-073**, una tercera
operación de escritura sobre `AsignaciónPersonaCompañía` — **renovación** —, distinta de la creación
(reemplazo automático) y del cierre (cese explícito), que extiende `FechaHoraFin` hacia una fecha
estrictamente posterior a la ya vigente sin crear un registro nuevo, sin disparar la cascada de RF-061, y
sin tocar `FechaHoraInicio`/`Estado`/`MotivoFin` ni ninguna asociación dependiente ya existente
(`ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial`); solo
aplica mientras `Estado = ACTIVA` — una pertenencia `FINALIZADA` no es renovable, preservando el histórico
secuencial ya establecido (RF-014). Se agrega un endpoint dedicado
(`POST .../historial-companias/{id}/renovar`, `contracts/people.yaml`), contraparte inversa de
`/finalizar`. El Principio III se refuerza (la renovación queda registrada por el interceptor de auditoría
ya existente, sin campo ni entidad nueva); el Principio IV se mantiene íntegro (ningún registro histórico se
elimina ni se reescribe retroactivamente — la renovación es puramente aditiva hacia adelante). Ninguna
entidad nueva, ninguna cardinalidad, ningún aislamiento entre Principales ni otra regla de revocación/cascada
cambia. `spec.md`, `data-model.md`, `research.md` (§26 nueva) y `contracts/people.yaml` fueron modificados.
~~**Nueva decisión pendiente (no resuelta aquí)**: si la renovación es válida también cuando la pertenencia
ya expiró dinámicamente antes de renovarse (`Estado = ACTIVA` pero `FechaHoraFin` ya pasada), o solo
mientras aún no ha expirado — spec.md, Decisiones Pendientes #10.~~ **[RESUELTA — ver re-chequeo siguiente]**

**Re-chequeo post-corrección (Sesión 2026-09-14, "cierre Decisión Pendiente #10" — `/speckit-clarify`)**:
confirmado, acotado. Cierra la Decisión Pendiente #10: la renovación (RF-073) exige, además de
`Estado = ACTIVA`, que la pertenencia siga **vigente dinámicamente** en el momento de renovar (`fecha
actual <= FechaHoraFin` ya declarada) — nunca puede usarse para puentear retroactivamente un vacío temporal
ya transcurrido. Una pertenencia `ACTIVA` pero dinámicamente expirada no es renovable, igual que una
`FINALIZADA`; ambos casos requieren una nueva `AsignaciónPersonaCompañía`, ya soportado sin cambios
adicionales (sin solapamiento posible). El Principio IV se refuerza (la vigencia dinámica ya transcurrida
nunca se reescribe retroactivamente, ni siquiera mediante una operación explícitamente aditiva como la
renovación). Ninguna entidad, columna, migración ni endpoint nuevo — se amplía la validación del endpoint
`/renovar` ya agregado en el re-chequeo anterior. `spec.md`, `data-model.md`, `research.md` (§26 ampliada) y
`contracts/people.yaml` fueron modificados. Sin decisiones pendientes nuevas.

**Re-chequeo post-decisión (Sesión 2026-09-15, decisión A — sin cierre automático de credencial previa)**:
confirmado, acotado. Durante la implementación de Historia 9 se detectó que `contracts/credentials.yaml`
(heredado de la regla genérica de research.md §5) atribuía a la asignación de credencial el cierre automático
de la credencial `ASIGNADO` previa, incompatible con research.md §23/§24 (estados terminales de causa única;
solo devolución, baja o cascada acortan `FechaHoraFin`). Negocio eligió la opción A: el solapamiento se rechaza
con 409 y la credencial previa no se modifica. RF-057 y el trigger de no-solapamiento se mantienen sin cambios.
El Principio IV se refuerza (ninguna vigencia de credencial se reescribe como efecto lateral de otra
asignación). Ninguna entidad, columna, estado, migración ni endpoint nuevo. `spec.md`, `research.md` (§5
acotada), `data-model.md` y `contracts/credentials.yaml` fueron modificados; `ux-ui.md` no contenía la
cláusula. Sin decisiones pendientes nuevas.

**Re-chequeo de cierre de Etapa 1 (Sesión 2026-09-20, Decisiones D1 a D9 — planificación de corrección de
baseline, sin implementar todavía)**: confirmado, sin violaciones nuevas. Este re-chequeo cubre
**exclusivamente** el plan de cierre y corrección del baseline ya implementado (T001–T168) — no replantea ni
reabre esas 168 tareas, que permanecen completas y válidas tal como están. Las nueve decisiones que cierran
las 16 preguntas de la matriz de auditoría de cierre (`docs/auditorias/decisiones-etapa1-2026-09-16.html`):

- **D1** (RBAC de administración de usuarios) y **D3** (aislamiento por alcance, Resource Ownership)
  **refuerzan el Principio I**: cierran F-01 (`UsuarioService` sin ningún control de alcance) y F-02
  (`AsignacionUnidadOrganizativaService`, `EstadoEfectivoService`, `RevocacionService`,
  `UnidadOrganizativaService`, `AreaAccesoService` verificados sin ningún control de alcance) — el defecto
  crítico que impedía congelar el baseline. `AlcanceUsuarioCompañía` es reemplazada por
  `AsignaciónRolAdministrativo` (research.md §27, data-model.md). Ninguna entidad de `Persona` ni regla de
  cardinalidad/aislamiento entre Principales cambia.
- **D2** (bootstrap del primer administrador) no introduce entidades ni migraciones nuevas — una rutina de
  arranque idempotente crea la primera `AsignaciónRolAdministrativo` (`GLOBAL_ADMINISTRATOR`) desde
  configuración/secrets (research.md §28). Único punto de atención: `FechaHoraFin = MAX_VALIDITY_DATE` es
  una **excepción explícita y acotada** a RF-071, exclusiva de esa fila — no debilita el Principio IV en
  ningún otro registro.
- **D4** (inactivación de Compañía) y **D5** (zona horaria por Compañía Principal) modifican
  `EvaluadorDeAcceso` (research.md §7) en el mismo tramo del algoritmo (pasos 4-5 y 12), que pasa de 14 a 15
  pasos — coordinados, no en conflicto. El Principio I se refuerza (nueva verificación de denegación por
  defecto); el Principio IV se mantiene (D4 no cierra ni modifica ningún registro dependiente; D5 nunca
  reinterpreta instantes UTC ya persistidos).
- **D6** (validación de dependientes al cambiar `TipoCompañía`) refuerza RF-044/045/046, ya vigentes,
  cerrando un vacío de validación — sin cascada automática, consistente con el patrón de D4.
- **D7** (interfaz de Historia 5, Casos A/B) es exclusivamente frontend — cero cambios de dominio, modelo de
  datos, autorización o contrato; reutiliza entidades, endpoints y el componente `Tree` ya existentes.
- **D8** (consultas transversales, RF-067 a RF-069) queda explícitamente **fuera del alcance de Etapa 1** —
  no se planifica ningún cambio de arquitectura, contrato ni dato para esta decisión en este re-chequeo.
- **D9** (decisiones heredadas #1 política de contraseñas, #3 retención legal, #7 prioridad de Historia 9)
  no requiere ningún cambio técnico — ratifica comportamiento ya implementado y verificado
  (`PasswordPolicyValidator`/`AutenticacionService`, ausencia de purga física ya exigida por el Principio
  IV, `CredencialService`/`CredencialesController` ya completos).

Ningún principio de la Constitución requirió enmienda. `research.md` (§27-§33), `data-model.md` (entidad
`AsignaciónRolAdministrativo`, campo `Compañía.ZonaHorariaIana`, validación de dependientes en `Compañía`) y
`contracts/users.yaml`, `contracts/auth.yaml`, `contracts/companies.yaml`, `contracts/access-evaluation.yaml`
fueron modificados en esta sesión de planificación. `spec.md` y `tasks.md` **no** fueron modificados por este
plan.

> **Actualización (Sesión 2026-09-20, posterior a este plan)**: la sesión de clarificación pendiente **ya se
> ejecutó**. `spec.md` incorpora ahora RF-074 a RF-081 y CS-036 a CS-041, la sesión de Clarifications del
> 2026-09-20, el renombrado de `AlcanceUsuarioCompañía` a `AsignaciónRolAdministrativo`, las anotaciones
> `[DIFERIDA A ETAPA 2]` de RF-067 a RF-069 y CS-032, la sincronización de Historia 8 a 15 pasos y el cierre de
> las diez Decisiones Pendientes. Lo único que sigue pendiente de este párrafo es `/speckit-tasks` (tareas de
> corrección con numeración ≥T169, sin renumerar T001–T168), aún no autorizado.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
├── ux-ui.md             # Entrada formal de UX/UI (incorporada Sesión 2026-09-14, ver Technical Context)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── EnterpriseAccessControl.Domain/            # Entidades, value objects, enums, reglas de dominio puras
│   │   ├── Entities/
│   │   ├── Enums/
│   │   └── ValueObjects/
│   ├── EnterpriseAccessControl.Application/       # Casos de uso, DTOs, interfaces de puertos, FluentValidation
│   │   ├── Auth/
│   │   ├── Masters/
│   │   ├── Companies/
│   │   ├── OrgUnits/
│   │   ├── People/
│   │   ├── AreaAccess/
│   │   ├── Permissions/
│   │   ├── AccessEvaluation/
│   │   └── Credentials/
│   ├── EnterpriseAccessControl.Infrastructure/    # EF Core DbContext (SQL Server), migraciones (incl. triggers de
│   │   ├── Persistence/                           # no-solapamiento vía SQL crudo), interceptor de auditoría,
│   │   ├── Auditing/                              # value conversions de enums, configuraciones IEntityTypeConfiguration<T>
│   │   └── Security/                              # hashing de contraseñas, filtros globales de alcance de compañías,
│   │                                               # CompaniaScopeAuthorizationHandler (Policy de ASP.NET Core;
│   │                                               # redefinido para RBAC — D1, research.md §27),
│   │                                               # RelojEmpresarial (resuelve zona por Compañía Principal — D5,
│   │                                               # research.md §31, en vez de un único DateTimeZone global)
│   └── EnterpriseAccessControl.Api/               # Composición DI, Controllers, ProblemDetails, Options Pattern,
│       ├── Controllers/                           # HealthChecks (/health/live, /health/ready), OpenAPI nativo,
│       └── Program.cs                             # autenticación JWT
└── tests/
    ├── EnterpriseAccessControl.UnitTests/         # Dominio + Aplicación (reglas de negocio, sin base de datos)
    ├── EnterpriseAccessControl.IntegrationTests/   # Testcontainers.MsSql: trigger de no-solapamiento, ciclos, alcance, auditoría
    └── EnterpriseAccessControl.ContractTests/      # Verifica la API (incl. ProblemDetails) contra contracts/*.yaml

frontend/
├── src/
│   ├── features/                                  # auth, companies, org-units, people, area-access, permissions,
│   │                                               # credentials, masters (cada uno: api client, hooks, forms,
│   │                                               # pages) + dashboard, historicos, audit (ux-ui.md §7-§8, §20-§21;
│   │                                               # RF-067 a RF-069 — decisión de capacidad resuelta por
│   │                                               # /speckit-clarify, Constitution Check "Re-chequeo UX/UI", pero
│   │                                               # SIN tareas en tasks.md ni contratos en contracts/ todavía)
│   ├── components/                                 # Design system (ux-ui.md §4-§5, §22): componentes base
│   │                                               # (DataTable, TreeView accesible ARIA treeview, Timeline,
│   │                                               # Stepper, StatusBadge, ConfirmationDialog, etc.) y componentes
│   │                                               # de dominio (CompanyTypeBadge, ContextCard, RevocationImpact,
│   │                                               # AccessDecision, AuditEvent, etc.), tablas, layout compartido
│   ├── app/                                        # Enrutamiento, providers (TanStack Query), guards de sesión
│   └── lib/                                        # Cliente HTTP tipado (generado desde contracts/), manejo de zona horaria
└── tests/
    ├── unit/                                       # Vitest + React Testing Library (incl. accesibilidad WCAG 2.2 AA)
    └── e2e/                                        # Playwright (flujos P1 + UX-01 a UX-16 de ux-ui.md §30)
```

**Structure Decision**: Opción "aplicación web" con backend en capas (Dominio → Aplicación → Infraestructura →
API) para separar presentación/aplicación/dominio/infraestructura tal como exige la Constitución, y un
frontend React independiente que consume exclusivamente el contrato HTTP documentado en `contracts/`. Los
ocho módulos de negocio (auth, maestros, compañías, unidades organizativas, personas, áreas de acceso,
permisos, credenciales) se reflejan simétricamente como namespaces en `Application/` y como `features/` en el
frontend para mantener trazabilidad 1:1 entre backend y UI (regla de ingeniería "consistencia entre API e
interfaz"). Las tres áreas de navegación transversales que introduce `ux-ui.md` (Dashboard, Históricos,
Auditoría — §7-§8, §20-§21; RF-067 a RF-069) son la excepción deliberada a esa simetría 1:1: no corresponden
a un único módulo de negocio sino a vistas agregadas server-side sobre varios módulos a la vez (decisión ya
resuelta, ver Constitution Check "Re-chequeo UX/UI" — la composición en cliente quedó descartada). Su
namespace exacto en `Application/` (uno consolidado vs. uno por vista) y su forma concreta en `contracts/`
quedan para una futura extensión de `tasks.md`, no se fijan en esta revisión de plan.

## Complexity Tracking

*No aplica: el Constitution Check no registró violaciones.*
