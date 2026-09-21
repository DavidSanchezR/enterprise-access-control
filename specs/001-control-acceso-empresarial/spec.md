# Especificación de Funcionalidad: Solución Web de Control de Acceso Empresarial

Rama de funcionalidad: `001-control-acceso-empresarial`

Creado: 14-09-2026

Estado: Borrador

## Resumen

Construir una solución web para administrar el acceso físico a áreas de una empresa. Las áreas de acceso
se modelan jerárquicamente mediante una estructura de árbol. Las personas se clasifican mediante tipos o
perfiles y mantienen históricos de compañía y unidad organizativa. Los permisos pueden asignarse
directamente a una persona, a una unidad organizativa o a una compañía y contienen vigencia temporal y
bloques horarios por día de la semana.

La solución está orientada principalmente a empresas mineras y otros sectores industriales de gran escala,
donde existe una o varias **Compañías Principales/Mandantes** (la empresa propietaria de la operación que
solicita el sistema de control de acceso) y múltiples **Compañías Contratistas** que prestan servicios
dentro de sus instalaciones. Toda compañía se clasifica como PRINCIPAL_MANDANTE o CONTRATISTA. Las
unidades organizativas representan exclusivamente la estructura organizativa de una Compañía Principal —
cada Compañía Principal tiene su propio contexto organizacional (unidades organizativas y áreas de acceso)
aislado del de cualquier otra Principal; las Compañías Contratistas no poseen unidades organizativas
propias.

Una Compañía Contratista puede prestar servicios simultáneamente a varias Compañías Principales; esta
relación se modela explícitamente mediante `RelaciónContratistaPrincipal`, con vigencia temporal propia.
Una persona mantiene su histórico de compañía de pertenencia (el empleador, sea Principal o Contratista) de
forma independiente de su(s) **contexto(s) operativo(s)**: un `ContextoOperativoPersonaPrincipal` vincula a
la persona con una Compañía Principal específica para la cual trabaja/accede, y una persona puede tener
ningún, uno o varios contextos operativos vigentes simultáneamente (uno por cada Compañía Principal). Dentro
de cada contexto operativo la persona tiene, de forma independiente por Principal, su propia unidad
organizativa asignada, sus propios permisos aplicables y su propia credencial vigente — dos contextos de la
misma persona nunca se mezclan entre sí. Una persona de una Compañía Contratista solo puede abrir un
contexto operativo con una Compañía Principal si existe una `RelaciónContratistaPrincipal` vigente entre su
compañía y esa Principal; una persona que pertenece directamente a una Compañía Principal usa esa misma
Principal como su único contexto operativo posible, determinado automáticamente por el sistema.

La solución también debe proporcionar módulos de mantenimiento para compañía, unidad organizativa,
tipo de documento, tipo de sangre, género, tipo de persona, tipo de credencial y usuario, además del
historial de credenciales/fotocheck.

## Clarifications

> **Nomenclatura — leer antes de esta sección**: las etiquetas `D1`–`D9` (sin guion) identifican las
> **decisiones de negocio** del cierre de Etapa 1, provenientes de la matriz de 16 preguntas, y son las que
> se registran y desarrollan en este documento. Las etiquetas `D-1`, `D-2`, `D-3`, `D-4` y `D-5` (con guion)
> que aparecen en este documento y en artefactos posteriores —`research.md` §34, `plan.md`, `quickstart.md`
> §8 y `tasks.md`— identifican las **desviaciones de implementación** detectadas al terminar T169–T228 y
> auditadas con `/speckit-analyze`. La coincidencia de letra es accidental y los contenidos no se
> corresponden (p. ej. `D4` = inactivación de compañía; `D-4` = búsqueda server-side de usuarios). Una
> etiqueta con guion NUNCA renumera ni sustituye a una decisión `D1`–`D9`: la trazabilidad histórica de ambas
> series se conserva intacta y por separado.

### Session 2026-09-14

- Q: ¿Una persona puede pertenecer activamente a más de una compañía al mismo tiempo, o solo a una compañía a la vez? → A: Una sola compañía activa a la vez (histórico secuencial, sin solapamiento). **[ÁMBITO ACLARADO — ver Sesión 2026-09-14 (corrección Modelo de Cardinalidad Definitivo): esta respuesta limita exclusivamente la relación laboral/contractual de pertenencia (`AsignaciónPersonaCompañía`, RF-014); NO limita, ni por analogía ni por extensión, el número de `ContextoOperativoPersonaPrincipal` vigentes simultáneos de la persona (RF-052), que se rige por su propia regla de cardinalidad independiente.]**
- Q: ¿Una persona puede pertenecer activamente a más de una unidad organizativa al mismo tiempo, o solo a una a la vez? → A: Una sola unidad organizativa activa a la vez (histórico secuencial, sin solapamiento). **[SUPERADA — ver Sesión 2026-09-14 (corrección Contexto Operativo) más abajo: la exclusividad ahora aplica por contexto operativo (Persona × Compañía Principal), no globalmente por persona; distintos contextos pueden tener asignaciones simultáneas.]**
- Q: Cuando coinciden permisos aplicables de persona, unidad organizativa y compañía para la misma área y horario, ¿cómo debe resolverse el conflicto de forma definitiva? → A: Precedencia definitiva PERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA (el nivel más específico siempre prevalece).
- Q: ¿El inicio de sesión debe requerir autenticación multifactor (MFA) además de correo y contraseña en esta fase, o solo usuario/contraseña con política de bloqueo/expiración? → A: Solo usuario/contraseña en esta fase; MFA queda fuera de alcance.
- Q: ¿El número de documento de una persona debe ser único en todo el sistema, o puede repetirse (por ejemplo, entre distintos tipos de documento o compañías)? → A: Único por tipo de documento: la combinación (tipo de documento, número de documento) es única globalmente.

### Sesión 2026-09-14 (corrección funcional: Compañía Principal/Mandante vs. Contratista)

Corrección de dominio reportada por negocio tras la planificación inicial: el modelo original no distinguía
compañías Principales/Mandantes de compañías Contratistas ni aislaba las unidades organizativas por
Principal. Las siguientes decisiones amplían y corrigen el spec original sin invalidar los requisitos ya
aprobados salvo donde se indica explícitamente.

- Q: ¿Debe existir una clasificación explícita de compañía? → A: Sí, como mínimo PRINCIPAL_MANDANTE y
  CONTRATISTA (RF-042).
- Q: ¿Puede existir más de una Compañía Principal/Mandante en el sistema? → A: Sí; no debe asumirse una
  única Principal global. El sistema soporta una o varias, cada una con su propio contexto organizacional y
  operacional aislado (RF-043).
- Q: Dado que `UnidadOrganizativa` no debe tener una columna/FK directa hacia `Compañía` (restricción
  explícita de negocio), pero cada Compañía Principal debe tener sus propias unidades organizativas
  aisladas de las de otras Principales, ¿cómo se modela esa pertenencia sin violar la restricción? → A: Una
  entidad de enlace separada (`CompañíaPrincipalUnidadOrganizativaRaiz`) vincula únicamente los nodos raíz
  de `UnidadOrganizativa` con su Compañía Principal propietaria; los nodos no raíz heredan esa pertenencia
  recorriendo `UnidadSuperiorId` hasta la raíz (RF-044, RF-045). `UnidadOrganizativa` en sí misma sigue sin
  ninguna relación directa hacia `Compañía`.
- Q: ¿Las Compañías Contratistas pueden tener sus propias unidades organizativas? → A: No; las unidades
  organizativas no pertenecen directamente a compañías contratistas (RF-045).
- Q: ¿Las Áreas de Acceso deben asociarse a una compañía? → A: Sí, cada Área de Acceso pertenece
  exclusivamente a una Compañía Principal (a diferencia de `UnidadOrganizativa`, aquí sí se modela como FK
  directa porque la restricción de negocio solo aplicaba a `UnidadOrganizativa`) — RF-046.
- Q: ¿Una persona de una Compañía Contratista obtiene acceso automático a todas las Compañías Principales
  para las que trabaja su empleador? → A: No. La relación operacional con una Principal específica se
  establece exclusivamente mediante la asignación explícita, histórica y temporal de la persona a una
  unidad organizativa de esa Principal (RF-048); no existe una entidad separada de "relación contractual"
  Contratista↔Principal a nivel de compañía. **[SUPERADA — ver Sesión 2026-09-14 (corrección Contexto
  Operativo): sí existe una entidad explícita `RelaciónContratistaPrincipal` a nivel de compañía, y una
  entidad explícita `ContextoOperativoPersonaPrincipal` a nivel de persona; la asignación de unidad
  organizativa ahora ocurre DENTRO de un contexto operativo, no de forma aislada.]**
- Q: ¿El histórico de compañía y el histórico de unidad organizativa de una persona deben validarse entre
  sí (p. ej. impedir asignar a alguien a una unidad de una Principal distinta a su compañía vigente)? → A:
  No; ambos históricos son independientes (ya lo eran en el diseño original) y no existe validación cruzada:
  cualquier persona activa puede asignarse a cualquier unidad organizativa de cualquier Compañía Principal
  dentro del alcance del usuario que realiza la asignación (RF-048). **[MATIZADA — ver Sesión 2026-09-14
  (corrección Contexto Operativo): la independencia entre el histórico de compañía (RF-014) y las
  asignaciones operativas se mantiene, pero ahora existe una restricción explícita para personas de
  compañías CONTRATISTA — RF-054.]**
- Q: ¿El alcance administrativo de un Usuario (`AlcanceUsuarioCompañía` — **[RENOMBRADA — Sesión 2026-09-20:
  esta entidad queda reemplazada por `AsignaciónRolAdministrativo`, RF-074]**) cambia con esta corrección? → A:
  No; permanece independiente de la relación operacional Persona→Compañía→UnidadOrganizativa, tal como en
  el diseño original (RF-050 lo hace explícito).

### Sesión 2026-09-14 (corrección funcional: Contexto Operativo Persona↔Principal y Relación
Contratista↔Principal)

Segunda corrección de dominio, reportada por negocio tras revisar la corrección anterior: el modelo previo
seguía asumiendo, implícitamente, que "Persona → Compañía → Principal" bastaba para representar el contexto
operativo de acceso, y que una persona tenía como máximo una unidad organizativa y una credencial vigentes
en todo momento. ninguno de los dos supuestos es correcto para el dominio real: una misma persona
(típicamente de una Contratista) puede trabajar simultáneamente para varias Compañías Principales, con
unidad organizativa, permisos y credencial propios e independientes para cada una. Las siguientes
decisiones corrigen y reemplazan las partes afectadas de la Sesión anterior (marcadas arriba como
SUPERADA/MATIZADA); el resto de requisitos no mencionados aquí sigue vigente sin cambios.

- Q: ¿Puede una persona de una Compañía Contratista trabajar simultáneamente para varias Compañías
  Principales? → A: Sí. No debe imponerse una relación 1:1 ni N:1 entre Persona y Principal; una persona
  puede tener ningún, uno o varios contextos operativos vigentes simultáneamente, cada uno con una
  Compañía Principal distinta (RF-052).
- Q: ¿Cómo se determina qué Principales puede seleccionar el usuario al asignar un contexto operativo a una
  persona de una Contratista? → A: Únicamente entre las Compañías Principales con las que la Contratista de
  esa persona tenga una `RelaciónContratistaPrincipal` vigente (RF-054, RF-051).
- Q: ¿Y para una persona que pertenece directamente a una Compañía Principal? → A: El sistema determina y
  fija automáticamente esa misma Principal como su contexto operativo; la pantalla no debe solicitar
  seleccionar una Principal distinta (RF-053).
- Q: ¿Puede una persona tener una Unidad Organizativa diferente para cada Principal con la que tiene
  contexto operativo vigente? → A: Sí, simultáneamente; la restricción de "máximo una asignación vigente"
  (RF-015) ahora aplica POR contexto operativo, no globalmente por persona (RF-055).
- Q: ¿La credencial de una persona es única y global, o puede tener una por Principal? → A: Puede tener una
  credencial vigente distinta por cada Compañía Principal con la que tenga contexto operativo, incluso
  simultáneamente; la asignación de credencial DEBE registrar explícitamente para qué Compañía Principal se
  emite (RF-056, RF-057).
- Q: ¿Qué representa `TipoCredencial`? → A: El tipo/diseño visual de la credencial (p. ej. "Credencial
  Contratista", "Credencial Corporativo", "Credencial Visitante"), nunca una tecnología de identificación
  física (RFID, QR, NFC, código de barras) ni un identificador físico; la impresión y el diseño gráfico
  quedan fuera de alcance (RF-058). El campo `CódigoFisico` incorporado como "decisión de trabajo" en la
  Sesión anterior de research.md §9 queda retirado — no hay ninguna decisión de negocio explícita que lo
  requiera.
- Q: ¿El motor de evaluación de acceso debe determinar primero la Principal propietaria del área antes de
  evaluar cualquier permiso? → A: Sí; si la persona no tiene un contexto operativo vigente con esa
  Principal (y, siendo de una Contratista, si la relación Contratista→Principal correspondiente no está
  vigente), el acceso se deniega por defecto antes de evaluar ningún permiso (RF-059).
- Q: ¿El aislamiento por Compañía Principal del alcance administrativo (RF-049) se extiende a
  `RelaciónContratistaPrincipal` y `ContextoOperativoPersonaPrincipal`? → A: Sí (RF-060).

### Sesión 2026-09-14 (auditoría final de consistencia — Contexto Operativo)

Sesión de `/speckit-clarify` centrada exclusivamente en cerrar las Decisiones Pendientes #4 y #5 dejadas
abiertas por la corrección de Contexto Operativo. Ambas se resolvieron por inferencia directa de las reglas
de negocio ya establecidas (sin necesidad de pregunta interactiva al usuario, según autorización explícita
del propio usuario para esta sesión); se documentan aquí como Q&A por trazabilidad. Ningún requisito
previamente aprobado fue cuestionado ni rediseñado.

- Q: ¿Qué debe ocurrir con los contextos operativos, asignaciones de unidad organizativa, permisos y
  credenciales vigentes de una persona cuando cambia su compañía de pertenencia? → A: Ninguno de esos
  registros se cierra automáticamente en cascada — permanecen como históricos íntegros hasta su cierre
  explícito o vencimiento natural de su propia vigencia (consistente con el principio de independencia ya
  establecido para estas relaciones, Historia 5). Sin embargo, la evaluación de acceso (Historia 8) DEBE
  re-validar en cada evaluación, usando la compañía de pertenencia vigente de la persona en la fecha
  evaluada, que su relación con la Compañía Principal del área siga siendo legítima — extendiendo
  simétricamente a la persona empleada directamente por una Principal (RF-053) la misma re-validación en
  vivo que ya aplicaba a personas de una Contratista (RF-054, RF-059). Esto cierra, sin cerrar ningún
  registro y sin necesitar cascada alguna, el vacío de seguridad de un contexto automático que sobrevive al
  cambio de empleador de la persona (RF-061). **[REEMPLAZADA — ver Sesión 2026-09-14 "corrección Revocación
  Automática": negocio corrigió esta decisión — la revocación automática en cascada SÍ es obligatoria, no
  solo la re-validación dinámica; ver RF-061 a RF-065.]**
- Q: ¿Debe permitirse más de un `ContextoOperativoPersonaPrincipal` simultáneo entre la misma persona y la
  misma Compañía Principal? → A: No. Ninguna regla de negocio establecida (en ninguna de las dos sesiones de
  corrección) describe o requiere más de un rol/contexto simultáneo con la **misma** Principal; todos los
  ejemplos y reglas de simultaneidad (Pedro García, CS-013 a CS-017) son siempre entre Principales
  **distintas**. La exclusividad estricta por par `(PersonaId, CompañíaPrincipalId)` ya definida en RF-052
  se confirma como decisión final, sin cambios al modelo de datos.

### Sesión 2026-09-14 (corrección funcional: Revocación Automática por Cese de Pertenencia)

Corrección reportada por negocio sobre la decisión de la sesión anterior (RF-061 original): "ningún registro
se cierra en cascada, solo re-validación dinámica" queda **reemplazada** — la revocación automática en
cascada es obligatoria, no opcional ni implícita en la sola evaluación dinámica.

- Q: ¿Debe el sistema revocar automáticamente los accesos derivados de una pertenencia Persona–Compañía que
  finaliza o se inactiva? → A: Sí, de forma obligatoria. El sistema DEBE revocar automáticamente todos los
  contextos operativos, asignaciones de unidad organizativa y credenciales que dependan de esa pertenencia
  específica y estén vigentes, sin eliminar ni modificar ningún registro histórico (RF-061).
- Q: ¿La revocación afecta también los contextos que la misma persona mantiene mediante otra pertenencia
  vigente? → A: No. La revocación se determina por dependencia de la pertenencia concreta que finaliza, no
  por `PersonaId` en bloque (RF-062).
- Q: ¿Cómo se distingue, en cada registro afectado, la fecha de inicio, la fecha de fin, el estado y el
  motivo de finalización? → A: `FechaHoraInicio` nunca se modifica; `FechaHoraFin` recibe el valor efectivo
  de finalización; se agrega un campo `Estado` (administrativo/informativo, no gatilla por sí solo la
  autorización) y un campo `MotivoFin`: nulo mientras el registro esté vigente, y poblado al cerrarse (RF-063).
- Q: ¿Qué ocurre si el cese se registra con una fecha de fin futura? → A: La `FechaHoraFin` (ya existente)
  se usa directamente como fecha efectiva y se propaga de inmediato a los registros dependientes en la misma
  operación — no se necesita un proceso programado ni una distinción entre fecha administrativa y fecha
  efectiva como campos separados; los registros dependientes permanecen genuinamente vigentes (y siguen
  produciendo acceso) hasta ese instante, exactamente igual que hoy determina la vigencia de cualquier otra
  asignación temporal del sistema (RF-064).
- Q: ¿La revocación automática reemplaza la re-validación dinámica de la evaluación de acceso (RF-059)? → A:
  No; ambas coexisten como defensa en profundidad — la revocación en cascada es la garantía primaria y
  auditable del dominio, y la re-validación dinámica sigue actuando como red de seguridad adicional ante
  cualquier inconsistencia de datos (RF-065).
- Q: ¿La misma revocación automática aplica cuando termina una `RelaciónContratistaPrincipal` (en vez de una
  pertenencia Persona–Compañía)? → A: Fuera de alcance de esta corrección — negocio solo especificó
  revocación en cascada para el cese de pertenencia Persona–Compañía. El fin de una
  `RelaciónContratistaPrincipal` sigue protegido únicamente por la re-validación dinámica ya existente
  (RF-059), sin cascada de escritura equivalente; queda como posible extensión futura si negocio lo solicita
  explícitamente (ver Decisiones Pendientes).

**Inconsistencia detectada y resuelta**: el Ejemplo 3 provisto por negocio para ilustrar el alcance de la
revocación ("Pedro pertenece simultáneamente a Servicios ACME y a Servicios DEF") contradice RF-014
(ya establecido y sin cambios: una persona tiene como máximo una `AsignaciónPersonaCompañía` activa a la
vez, sin solapamiento) — dos pertenencias simultáneas de la misma persona a compañías distintas no son
posibles en el modelo. La lección que el ejemplo buscaba ilustrar (la revocación no debe afectar contextos
que dependen de una pertenencia distinta) permanece completamente válida y queda correctamente representada
por el Ejemplo 2 (una única Contratista con relaciones vigentes simultáneas hacia varias Principales): al
finalizar esa única pertenencia se revocan **todos** los contextos dependientes de ella (que pueden ser
**varios simultáneamente**, uno por cada Principal con la que esa Contratista tuviera relación vigente — la
pluralidad de contextos es el caso normal, no una excepción), y una pertenencia posterior y distinta de la
misma persona (tras cambiar de compañía) abre contextos propios no afectados por revocaciones previas
(RF-062).

### Sesión 2026-09-14 (corrección funcional: Modelo de Cardinalidad Definitivo — RF-014 no limita
ContextoOperativoPersonaPrincipal)

Corrección de una ambigüedad de redacción detectada por negocio en la sesión anterior: ninguna restricción
estructural, de base de datos ni de las reglas ya vigentes (RF-052, RF-056, RF-057) impedía nunca que una
persona tuviera contextos operativos simultáneos con Principales distintas — pero la redacción de RF-014 y
la justificación de RF-062 (que hablaban de "una sola asignación de compañía activa" y de que "todos los
contextos... quedan alcanzados" por la cascada) eran suficientemente genéricas como para poder malinterpretarse,
por analogía, como una restricción también sobre `ContextoOperativoPersonaPrincipal`. Esta sesión corrige la
redacción para eliminar esa ambigüedad; **no cambia ninguna regla de negocio ya vigente**, solo la precisión
con la que se expresan.

- Q: ¿RF-014 (máximo una `AsignaciónPersonaCompañía` activa) limita de alguna forma el número de
  `ContextoOperativoPersonaPrincipal` vigentes simultáneos de una persona? → A: No, nunca lo hizo. RF-014
  limita exclusivamente la relación laboral/contractual de pertenencia (¿a qué compañía pertenece
  actualmente la persona?). El número de contextos operativos simultáneos se rige exclusivamente por RF-052
  (como máximo uno por cada Compañía Principal distinta, sin límite superior de Principales). RF-014 se
  reescribe para declarar esta independencia explícitamente.
- Q: ¿La frase de RF-062 "todos los contextos operativos... quedan alcanzados" implica que solo puede
  existir un contexto a la vez? → A: No; implica lo contrario — que pueden existir **varios** contextos
  simultáneos (uno por Principal), y que la cascada de revocación alcanza a **todos** ellos porque todos
  dependían de la misma (única) pertenencia que finalizó. RF-062 se reescribe para que esta lectura sea
  inequívoca sin depender de inferencia.
- Q: ¿Se confirma sin ambigüedad que el sistema debe soportar que una persona trabaje simultáneamente para
  múltiples Compañías Principales cuando su compañía de pertenencia (Contratista) tenga relaciones vigentes
  con todas ellas? → A: Sí, de forma explícita e incondicional (CS-030, nuevo).

### Sesión 2026-09-14 (corrección funcional: integración de `ux-ui.md` — Credencial como factor de gating,
Auditoría/Históricos/Dashboard transversales, y confirmación de `MotivoFin` en credenciales)

Sesión de `/speckit-clarify` motivada por la revisión de integración de `ux-ui.md` (entrada formal de UX/UI,
Sesión 2026-09-14) contra `plan.md`, `research.md`, `data-model.md` y este documento. Se detectaron una
contradicción real y dos vacíos de capacidad; las tres quedan resueltas aquí.

- Q: `ux-ui.md` §16 y §19 presentan la validez de la credencial como un factor que determina el resultado de
  la evaluación de acceso, mientras que Historia 9 (texto original) afirmaba que la credencial "nunca" es
  "el mecanismo que por sí solo determina el permiso de acceso" y el algoritmo de 13 pasos (Historia 8,
  research.md §7) nunca consultaba `AsignaciónCredencial`. ¿Debe una `AsignaciónCredencial` en Estado
  distinto de `ASIGNADO` (o su ausencia) para la Compañía Principal evaluada impedir por sí misma el acceso
  físico? → A: **Sí.** El sistema DEBE denegar el acceso si la persona no tiene una `AsignaciónCredencial`
  vigente en Estado `ASIGNADO` para la Compañía Principal propietaria del área evaluada, en la fecha
  evaluada (RF-066, nuevo). Esto **reemplaza** la afirmación categórica anterior de Historia 9 ("nunca el
  mecanismo que por sí solo determina el permiso de acceso"), que queda superada — ver anotación en Historia
  9 más abajo. La credencial pasa de ser puramente informativa a ser, además, un requisito habilitante
  (gate) de la evaluación, sin por ello sustituir a los permisos (`PermisoAcceso` sigue siendo necesario;
  tener una credencial `ASIGNADO` válida es condición necesaria, no suficiente, para `CONCEDIDO`).
- Q: ¿En qué punto del algoritmo de 14 pasos (antes 13) debe verificarse, y qué motivo de denegación
  corresponde? → A: Inmediatamente después de confirmar que el contexto operativo es vigente y legítimo
  (antiguo paso 5), como nuevo paso 6, antes de evaluar si el área está activa — agrupando así todas las
  verificaciones de legitimidad/identidad (alcance, persona, área, Principal, contexto operativo, credencial)
  antes de las verificaciones de elegibilidad y permisos. El motivo de denegación es un único valor nuevo
  `SIN_CREDENCIAL_VIGENTE` (cubre por igual: nunca se asignó una credencial para esa Principal, o la última
  vigente está `DEVUELTO`, `ELIMINADO` o `REVOCADA`) — siguiendo el mismo patrón ya establecido por
  `SIN_CONTEXTO_OPERATIVO_VIGENTE`, que tampoco distingue sus causas subyacentes en el valor del enum; el
  detalle específico (p. ej. "revocada automáticamente por cese de pertenencia") puede seguir comunicándose
  en el campo de texto libre de la respuesta, no en el código de motivo.
- Q: ¿Cómo se relaciona esta regla con la revocación automática por cese de pertenencia (RF-061 a RF-065)? →
  A: No la modifica en absoluto — la reutiliza. La cascada de revocación (RF-061) ya escribe
  `Estado = REVOCADA` en `AsignaciónCredencial` cuando finaliza la pertenencia que la sustenta; RF-066
  simplemente hace que la evaluación de acceso **consulte** ese campo ya existente, exactamente como RF-065
  ya hace con `ContextoOperativoPersonaPrincipal` (re-validación dinámica como defensa adicional). No se
  agrega ninguna entidad, columna ni migración nueva.
- Q: ¿Requiere cambios en `data-model.md` o en los contratos? → A: `data-model.md` no cambia (ningún campo
  nuevo). `contracts/access-evaluation.yaml` sí: se agrega `SIN_CREDENCIAL_VIGENTE` al enum
  `MotivoDenegacion` y se actualiza la descripción del flujo de 13 a 14 pasos. `research.md` §7 se reescribe
  con el paso adicional.
- Q: ¿Modifica alguna regla de dominio ya aprobada? → A: Reemplaza únicamente la afirmación de Historia 9
  citada arriba (que nunca fue un RF numerado, solo texto narrativo de la historia). No modifica RF-014,
  RF-052, RF-056, RF-057, RF-061 a RF-065, ni ninguna regla de cardinalidad, aislamiento entre Principales o
  revocación ya vigente. CS-024 (una credencial de la Principal A no sirve para la Principal B) se mantiene
  sin cambios y ahora es, además, consistente con el nuevo gate (que también es por Principal).
  **Consecuencia no resuelta aquí** (ver Decisiones Pendientes #7): la evaluación de acceso (Historia 8, P1)
  pasa a depender funcionalmente de que exista una credencial asignada (Historia 9, actualmente P2) — la
  prioridad relativa de Historia 9 no se modifica en esta sesión, queda como decisión de alcance pendiente.

- Q: `ux-ui.md` §8 (Dashboard), §20 (Históricos, timeline transversal) y §21 (Auditoría) asumen consultas
  agregadas/transversales entre entidades (credenciales próximas a vencer, relaciones próximas a finalizar,
  un log filtrable por usuario/entidad/Principal/rango de fechas) que hoy no tienen contrato. ¿Requieren
  endpoints dedicados o pueden resolverse componiendo en el cliente los endpoints ya existentes? → A:
  Requieren **endpoints dedicados**, resueltos server-side. La composición en cliente violaría el Principio
  I (Seguridad Server-Side y Autorización por Compañías): cualquier vista que combine datos de varias
  compañías/Principales debe filtrar por alcance en el servidor, no ensamblar en el navegador datos que el
  cliente no debería poder ver sin filtrar primero. CS-002/CS-003 (rendimiento con ≥100,000 personas) también
  descartan agregar en cliente. El contenido de cada pantalla ya está descrito con suficiente detalle en
  `ux-ui.md` §8/§20/§21 y no se duplica aquí (RF-067 a RF-069, nuevos, formalizan la exigencia funcional sin
  fijar la forma exacta del endpoint, que es una decisión de `plan.md`/`contracts/`).
- Q: ¿Esto constituye un requisito funcional, una decisión arquitectónica, o ambas? → A: Ambas, pero de bajo
  riesgo: la **existencia** de una vista consultable de auditoría/histórico transversal/indicadores
  agregados es un requisito funcional nuevo (RF-067 a RF-069) que opera sobre datos que Historia 5, Historia
  9 y RF-026/RF-027 ya exigían que existieran — no introduce ninguna regla de negocio, cardinalidad,
  relación o vigencia nueva, solo una superficie de consulta sobre datos ya obligatorios. La forma concreta
  del/los endpoint(s) es una decisión arquitectónica que corresponde a una futura actualización de
  `contracts/` (fuera de alcance de esta sesión de clarificación).

- Q: `AsignaciónCredencial` tiene `Estado` y `RevocadoPorPertenenciaId` pero no `MotivoFin`, a diferencia de
  `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal` y `AsignaciónPersonaUnidadOrganizativa`.
  RF-063 exige distinguir explícitamente un "motivo de finalización" en los registros afectados por la
  revocación automática, y `AsignaciónCredencial` está entre ellos (RF-061). ¿Es esto una inconsistencia del
  modelo que requiere agregar `MotivoFin` a `AsignaciónCredencial`? → A: **No es una inconsistencia; es una
  asimetría intencional y correcta.** A diferencia de las otras tres entidades (cuyo `Estado` es genérico —
  ACTIVA/FINALIZADA — y por eso necesitan un `MotivoFin` separado para distinguir la causa), el `Estado` de
  `AsignaciónCredencial` (ASIGNADO/DEVUELTO/ELIMINADO/**REVOCADA**) ya es específico por causa: `REVOCADA`
  significa inequívocamente "finalizó por la cascada de RF-061", distinto de `DEVUELTO` (devolución física
  voluntaria) o `ELIMINADO` (baja lógica administrativa). Junto con `RevocadoPorPertenenciaId` (que ya
  identifica la pertenencia de origen), `Estado` por sí solo satisface el literal (d) de RF-063 para esta
  entidad. No se modifica `data-model.md` ni `contracts/credentials.yaml`. Si en el futuro negocio introduce
  una segunda causa posible de `Estado = REVOCADA` no derivada de RF-061, esta decisión debería revisarse
  explícitamente (no se anticipa aquí).

### Sesión 2026-09-14 (corrección funcional: auditoría de consistencia de RF-066 — vigencia temporal de
`AsignaciónCredencial` independiente de `Estado`)

Sesión de `/speckit-clarify` motivada por una auditoría de consistencia dedicada sobre RF-066 (Sesión
anterior). La auditoría encontró que `data-model.md` restringía `AsignaciónCredencial.FechaHoraFin` a
`null` mientras `Estado = ASIGNADO` — una regla que, de mantenerse, volvería inalcanzable el escenario
mismo que RF-066/paso 6 del algoritmo debían cubrir (una credencial `ASIGNADO` cuya vigencia temporal ya
expiró sin que nadie la haya cerrado administrativamente). Negocio confirmó la corrección explícitamente.

- Q: ¿Puede `AsignaciónCredencial.FechaHoraFin` tener un valor (incluso futuro) mientras `Estado =
  ASIGNADO`, o debe permanecer `null` hasta que la credencial se cierre administrativamente? → A: **Sí,
  puede tener un valor**, incluso futuro, mientras `Estado = ASIGNADO` — por ejemplo, una "Credencial
  Temporal" (Historia 9) asignada desde el inicio con una vigencia de duración conocida. La restricción
  anterior de `data-model.md` ("`null` mientras esté `ASIGNADO`") queda **eliminada**; no describía ninguna
  regla de negocio establecida, era una inferencia incorrecta introducida al documentar el campo (RF-070,
  nuevo).
- Q: Si `Estado = ASIGNADO` y `FechaHoraFin` ya pasó, ¿la credencial permite acceso? → A: No — está
  **temporalmente expirada** y no satisface el gate de RF-066, aunque su `Estado` siga siendo `ASIGNADO`. El
  sistema NO DEBE cambiar automáticamente el `Estado` por el mero paso del tiempo; la denegación surge
  exclusivamente de la verificación dinámica del paso 6 del algoritmo (research.md §7), nunca de una
  escritura de estado (RF-070).
- Q: ¿Cómo se distingue esta expiración temporal de la revocación administrativa (cese/reemplazo de
  pertenencia, devolución, baja lógica)? → A: Son mecanismos completamente independientes. La revocación
  administrativa (RF-061, y las transiciones `ASIGNADO → DEVUELTO`/`ELIMINADO`) sigue siendo la única que
  escribe `Estado`, fija `FechaHoraFin` cuando corresponde y nunca modifica `FechaHoraInicio` — sin cambios
  respecto a lo ya vigente. La expiración temporal nunca escribe nada: es una comparación de fechas
  evaluada en cada consulta de acceso, exactamente con el mismo patrón ya establecido para
  `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal` y `AsignaciónPersonaUnidadOrganizativa`
  (RF-063: "`Estado` es administrativo/informativo... la vigencia efectiva... se determina siempre
  comparando `FechaHoraInicio`/`FechaHoraFin`... nunca por `Estado` de forma aislada") — RF-070 extiende
  explícitamente ese mismo principio a `AsignaciónCredencial`, que hasta ahora no lo tenía declarado con la
  misma precisión.
- Q: ¿Requiere esto cambios en el algoritmo de evaluación de acceso, `contracts/access-evaluation.yaml` o
  `contracts/credentials.yaml`? → A: El algoritmo (Historia 8, paso 6; research.md §7) y
  `contracts/access-evaluation.yaml` (`SIN_CREDENCIAL_VIGENTE`) se actualizan para expresar explícitamente
  la conjunción de las dos condiciones, no solo `Estado`. `contracts/credentials.yaml` no requería cambio
  estructural (su esquema ya no imponía la restricción incorrecta), solo una nota aclaratoria.
- Q: ¿Existe alguna otra regla, RF o entidad que dependa de la restricción eliminada, o que quede
  contradicha por esta corrección? → A: No se encontró ninguna. RF-057 (máximo una credencial `ASIGNADO`
  activa por Compañía Principal, sin solapamiento) y el trigger de no-solapamiento (research.md §5)
  seguían siendo válidos sin cambios: la plantilla genérica del trigger ya comparaba
  `FechaHoraInicio`/`ISNULL(FechaHoraFin, '9999-12-31')`, por lo que nunca dependió de que `FechaHoraFin`
  fuera `null` durante `ASIGNADO` — de hecho, con esta corrección el trigger pasa a comportarse de forma
  más precisa (permite una segunda credencial `ASIGNADO` cuya vigencia comience después de que la anterior
  haya expirado temporalmente, sin exigir su cierre administrativo previo). **Observación relacionada, fuera
  de alcance de esta sesión** (no se resuelve, se deja registrada): `data-model.md` describe la misma
  restricción ("`null` mientras esté vigente") en los campos `FechaHoraFin` de
  `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal` y `AsignaciónPersonaUnidadOrganizativa`.
  Esta sesión NO evaluó si esas tres entidades tienen el mismo problema (p. ej. una asignación de plazo fijo
  conocido desde el inicio) — la decisión de negocio de esta sesión se limitó explícitamente a
  `AsignaciónCredencial`; extenderla a las otras tres requeriría una decisión de negocio explícita adicional
  (ver Decisiones Pendientes #8).

### Sesión 2026-09-14 (corrección funcional: vigencia temporal jerárquica de las asociaciones de una
persona — cierra Decisión Pendiente #8 con una decisión más amplia)

Corrección de negocio que reemplaza la conclusión de la sesión anterior (que cerró la Decisión Pendiente #8
confirmando que `null` = vigencia indefinida seguía siendo válido para las asociaciones de persona). Negocio
decidió eliminar por completo esa posibilidad: toda asociación temporal vinculada a una persona debe tener
`FechaHoraFin` real desde su creación, y las asociaciones dependientes de la pertenencia Persona–Compañía
deben estar temporalmente contenidas dentro de su vigencia.

- Q: ¿`FechaHoraFin` debe ser obligatoria (nunca `null`) también para `AsignaciónPersonaCompañía`, la
  asociación raíz? → A: Sí, sin excepción. Ya no puede existir una pertenencia Persona–Compañía indefinida
  (RF-071).
- Q: ¿La obligatoriedad de `FechaHoraFin` aplica también a `AsignaciónTipoPersona` y a `PermisoAcceso`
  (cuando `Alcance = PERSONA`), aunque no sean dependientes de la pertenencia en el sentido de la cascada de
  RF-061? → A: Sí a la obligatoriedad (RF-071); pero determinando según el modelo de dominio ya vigente
  (RF-011, RF-061, Historia 8) cuáles asociaciones son realmente **dependientes** de la pertenencia para
  efectos de la regla de **contención** (RF-072, distinta de la obligatoriedad): son dependientes
  exclusivamente las tres que la cascada de RF-061 ya revoca por depender de ella —
  `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial`.
  `AsignaciónTipoPersona` (RF-011: perfiles múltiples sin exclusividad, nunca mencionada en RF-061) y
  `PermisoAcceso` (configuración compartida evaluada independientemente del histórico de pertenencia —
  RF-061 lo excluye textualmente de la cascada) **NO** están sujetas a contención: su vigencia es
  obligatoria pero libre, sin relación temporal con la pertenencia de la persona. No se asumió ninguna
  relación de dependencia que RF-061 o Historia 8 no establecieran ya explícitamente.
- Q: ¿Cómo se representa "vigencia indefinida" si `FechaHoraFin` ya no admite `null`? → A: No se representa
  — negocio confirmó explícitamente que no debe usarse `null` ni una fecha centinela (p. ej.
  `9999-12-31`): `FechaHoraFin` DEBE ser una fecha/hora real y conocida, elegida en el momento de crear la
  asociación (RF-071). Ya no existe, para estas seis entidades, el concepto de "vigencia permanente".
- Q: ¿RF-048 debe reescribirse? → A: Sí. Su afirmación de que "no existe validación cruzada" entre el
  histórico de compañía de pertenencia y el contexto operativo queda **reemplazada**: ahora sí existe una
  validación cruzada temporal explícita (RF-072) para las tres asociaciones dependientes.
- Q: ¿Cuál es la regla de contención exacta? → A: `FechaHoraInicio_hija >= FechaHoraInicio_pertenencia` Y
  `FechaHoraFin_hija <= FechaHoraFin_pertenencia`, validada contra la `AsignaciónPersonaCompañía` vigente de
  la persona en el momento de crear la asociación hija. Las hijas pueden terminar antes que la pertenencia
  (no se exige igualdad); nunca pueden empezar antes ni terminar después (RF-072, CS-034).
- Q: ¿El vencimiento temporal de estas asociaciones escribe algo automáticamente? → A: No. Se confirma sin
  cambios el principio ya establecido para `AsignaciónCredencial` (RF-070) y para las demás entidades
  revocables (RF-063): la vigencia se evalúa dinámicamente comparando fechas; `Estado` solo cambia por
  revocación/cese administrativo, que conserva íntegramente la cascada ya definida (RF-061 a RF-065).

**Hallazgo adicional, no solicitado pero directamente relevante**: al auditar `contracts/permissions.yaml`
se encontró que RF-021 ("Todo permiso DEBE tener inicio y fin de vigencia") ya exigía, **desde el spec
original**, `FechaHoraFinVigencia` obligatoria para `PermisoAcceso` — para los tres alcances, no solo
PERSONA —, pero `data-model.md` y `contracts/permissions.yaml` siempre la documentaron como nullable
("vigencia indefinida"), contradiciendo RF-021 desde antes de cualquier sesión de esta serie. Se corrige
aquí aprovechando que ya se está tocando este campo: `PermisoAcceso.FechaHoraFinVigencia` pasa a ser
obligatoria para **los tres alcances** (no solo PERSONA), alineando por fin `data-model.md` y los contratos
con RF-021 tal como siempre estuvo escrito. Esto excede ligeramente el pedido explícito de esta sesión
("cuando Alcance = PERSONA"), pero se reporta con transparencia: no es una regla nueva, es la corrección de
una que ya existía y nunca se implementó correctamente.

**Nueva ambigüedad detectada, NO resuelta — reportada como Decisión Pendiente #9**: ninguna de las
decisiones de esta sesión aborda si `AsignaciónPersonaCompañía.FechaHoraFin` puede **extenderse hacia
adelante** después de su creación (p. ej. renovación de un contrato cuyo plazo declarado se amplía). El
mecanismo de "reemplazo automático" (RF-063) y el cese explícito (RF-064) solo **acortan** `FechaHoraFin`;
ninguno describe una operación de extensión. Dado que ahora toda pertenencia nace con una fecha de fin
real y comprometida (no indefinida), esta pregunta se vuelve operacionalmente relevante de una forma que no
lo era antes. No se infiere ninguna respuesta — queda como decisión de negocio explícita pendiente.

### Sesión 2026-09-14 (corrección funcional: renovación de `AsignaciónPersonaCompañía` — cierra Decisión
Pendiente #9)

Corrección de negocio que resuelve la Decisión Pendiente #9. Negocio confirmó que sí debe existir una
operación de **renovación**: la extensión hacia adelante de `FechaHoraFin` representa continuidad de la
misma relación contractual, no una relación nueva.

- Q: ¿Puede extenderse `FechaHoraFin` de una `AsignaciónPersonaCompañía` ya creada? → A: Sí, mediante una
  operación explícita de **renovación**, distinta de la creación de una nueva `AsignaciónPersonaCompañía` y
  distinta del cierre (reemplazo/cese). La extensión representa continuidad de la misma relación
  Persona–Compañía; NO debe crear automáticamente una nueva `AsignaciónPersonaCompañía` (RF-073).
- Q: ¿Qué límites tiene la renovación? → A: (1) Solo puede mover `FechaHoraFin` hacia una fecha **posterior**
  a la ya vigente — nunca hacia atrás (esa operación sigue siendo exclusiva de la cascada de cese/reemplazo,
  RF-061/RF-064, un mecanismo distinto que esta decisión no modifica ni sustituye). (2) NO modifica
  `FechaHoraInicio` (sin cambios respecto a RF-063: nunca se modifica tras la creación). (3) NO modifica
  `Estado` ni `MotivoFin` automáticamente, ni crea ninguna asociación dependiente nueva. (4) Las asociaciones
  dependientes ya existentes (`ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`,
  `AsignaciónCredencial`) conservan su propia `FechaHoraFin` sin cambios — renovar la pertenencia solo
  amplía el techo temporal que RF-072 permite para asociaciones futuras, o para una extensión explícita y
  separada de una asociación existente (si negocio la solicita en el futuro; esta sesión no define esa
  operación para las asociaciones dependientes, solo para la raíz). (5) Por lo mismo, renovar la pertenencia
  NO implica renovar automáticamente ningún contexto, unidad organizativa o credencial.
- Q: ¿Cómo queda registrada la renovación? → A: Mediante los mecanismos de auditoría ya existentes
  (`UpdatedAt`/`UpdatedById`, RF-026/RF-027) — no se introduce ningún campo ni entidad de auditoría nueva.
- Q: ¿Puede renovarse una pertenencia ya `FINALIZADA`? → A: No. Una relación ya finalizada (`Estado =
  FINALIZADA`, por cese explícito o por haber sido reemplazada) no es renovable — conserva su semántica
  histórica existente sin cambios. Si la persona vuelve a pertenecer a la misma compañía después de un
  cierre, eso requiere una nueva `AsignaciónPersonaCompañía` (histórico secuencial, RF-014), no una
  renovación de la anterior.

**Nueva ambigüedad detectada, NO resuelta — reportada como Decisión Pendiente #10**: los ocho puntos de la
decisión de negocio especifican que la nueva `FechaHoraFin` debe ser posterior a la ya vigente, pero no
distinguen el caso en que la pertenencia ya **expiró dinámicamente** antes de renovarse — `Estado = ACTIVA`
pero `FechaHoraFin` ya anterior a la fecha actual en el momento de renovar (posible bajo RF-070/RF-071,
donde `Estado` nunca cambia automáticamente por el mero paso del tiempo; distinto de `Estado = FINALIZADA`,
que sí está resuelto arriba: no renovable). ¿Debe permitirse "puentear" ese vacío temporal — lo que
retroactivamente volvería vigente un período en el que dinámicamente no lo era, con las implicancias que eso
tendría sobre cualquier evaluación de acceso ya denegada durante ese vacío — o la renovación solo debe ser
válida mientras la pertenencia aún no ha expirado (`fecha actual <= FechaHoraFin` vigente al momento de
renovar)? No se asume ninguna respuesta.

### Sesión 2026-09-14 (cierre de Decisión Pendiente #10 — renovación exige vigencia dinámica)

Corrección de negocio que cierra la Decisión Pendiente #10 dejada abierta por la sesión anterior.

- Q: ¿Puede renovarse una `AsignaciónPersonaCompañía` cuyo `Estado` sigue `ACTIVA` pero cuya `FechaHoraFin`
  ya pasó (expiración dinámica, sin cierre administrativo)? → A: **No.** La renovación exige, además de
  `Estado = ACTIVA`, que la pertenencia siga **vigente dinámicamente** en el momento de renovar (`fecha
  actual <= FechaHoraFin` ya declarada). No debe usarse para "puentear" retroactivamente un vacío temporal
  en el que la pertenencia ya no producía acceso. Si `FechaHoraFin` ya pasó, la pertenencia NO es renovable:
  DEBE crearse una nueva `AsignaciónPersonaCompañía` en su lugar (histórico secuencial, RF-014) — ya
  soportado sin cambios adicionales, porque al no existir solapamiento posible entre una pertenencia
  dinámicamente expirada y una nueva que inicia después de esa fecha, la anterior simplemente no se toca
  (data-model.md, "Validaciones clave" de `AsignaciónPersonaCompañía`). Esto extiende RF-073, no lo
  contradice: la restricción de vigencia dinámica es un requisito adicional de la operación de renovación,
  no un cambio a sus otras siete reglas ya resueltas.

### Sesión 2026-09-15 (decisión A: sin cierre automático de la credencial previa)

Contradicción detectada durante la implementación de Historia 9: `contracts/credentials.yaml` indicaba que
asignar una credencial "cierra automáticamente cualquier credencial ASIGNADO previa" de la misma persona y
Compañía Principal, mientras que RF-057, research.md §23/§24 y el propio 409 del contrato solo contemplan
impedir el solapamiento, y ninguno de los estados terminales existentes representa un reemplazo.

- Q: Al asignar una credencial, ¿se cierra automáticamente la credencial `ASIGNADO` previa de la misma
  persona y Compañía Principal? → A: **No (decisión A).** No se permite el solapamiento temporal entre
  credenciales `ASIGNADO` de la misma persona y Compañía Principal (RF-057, sin cambios): si existe una
  credencial `ASIGNADO` cuyo período se solapa con la nueva asignación, la operación se rechaza con 409 y la
  credencial previa no se cierra, finaliza, devuelve, elimina ni revoca automáticamente. `DEVUELTO`
  representa devolución física, `ELIMINADO` baja administrativa y `REVOCADA` exclusivamente la revocación
  automática por la cascada de RF-061. No se crea ningún estado ni motivo de reemplazo. Se elimina la
  cláusula de `contracts/credentials.yaml` y se acota research.md §5, que la atribuía genéricamente a todas
  las entidades particionadas.

### Sesión 2026-09-20 (cierre de Etapa 1: formalización de las decisiones D1 a D9)

Sesión de `/speckit-clarify` que **no abre ninguna decisión nueva**: transcribe a requisitos las nueve
decisiones de negocio ya aprobadas (D1 a D9) que cerraron las 16 preguntas de la matriz de auditoría de
cierre de Etapa 1, y corrige las inconsistencias documentales que el análisis de consistencia del 20/09/2026
detectó entre `spec.md` y el diseño técnico ya escrito en `plan.md`, `research.md` y `contracts/`. Las
respuestas ya existían al iniciar la sesión; no se formuló ninguna pregunta al usuario.

- Q: ¿Cómo se controla la administración de usuarios y su alcance? → A: Mediante RBAC con un catálogo cerrado
  de dos roles, `GLOBAL_ADMINISTRATOR` (alcance GLOBAL, sin compañía) y `COMPANY_ADMINISTRATOR` (alcance
  limitado a una compañía), asignados mediante la entidad `AsignaciónRolAdministrativo` con vigencia temporal
  y auditoría. Regla fundamental: `CompañíaId` nulo si y solo si el rol es `GLOBAL_ADMINISTRATOR`. Agregar un
  rol nuevo exige modificar el modelo de autorización, no insertar datos (RF-074, RF-075).
- Q: ¿Qué puede y qué no puede hacer un `COMPANY_ADMINISTRATOR`? → A: Administra usuarios solo de su propia
  compañía y puede crear asignaciones de su mismo nivel para ella; no puede administrar fuera de su alcance,
  reasignar fuera de él, elevar su propio alcance, asignar `GLOBAL_ADMINISTRATOR` ni asignar
  `COMPANY_ADMINISTRATOR` para otra compañía. Cualquier asignación fuera de esos límites exige un
  `GLOBAL_ADMINISTRATOR` o el mecanismo de arranque (RF-076, RF-078).
- Q: ¿Cómo se resuelve el alcance y qué significa exactamente «dentro del alcance» para una `Persona`? → A:
  El alcance se deriva de las asignaciones de rol vigentes (GLOBAL = todas las compañías; en caso contrario,
  las compañías de sus asignaciones por compañía). Una `Persona` está dentro del alcance si su compañía de
  pertenencia vigente lo está **o** si tiene un contexto operativo vigente con una Principal del alcance.
  Poseer el identificador de un recurso no otorga autorización: las lecturas fuera de alcance responden 404
  (RF-077).
- Q: ¿Cómo se crea el primer administrador en un despliegue nuevo? → A: Con una rutina de arranque
  idempotente posterior a las migraciones, que crea un `GLOBAL_ADMINISTRATOR` solo si no existe ninguno, con
  correo y contraseña provistos por configuración segura del entorno, cumpliendo la política de contraseñas
  vigente y forzando su cambio en el primer inicio de sesión. Su vigencia inicial usa `MAX_VALIDITY_DATE`
  (`2999-12-31T23:59:59Z`) como excepción explícita y acotada exclusivamente a esa asignación (RF-078).
- Q: ¿Qué efecto tiene inactivar una compañía sobre el acceso? → A: Denegación por evaluación dinámica, no
  cascada de escritura: la evaluación exige `Estado = ACTIVO` tanto en la Principal propietaria del área como
  en la compañía de pertenencia vigente de la persona, y deniega con `COMPANIA_INACTIVA` sin modificar ningún
  registro dependiente; reactivar restablece el acceso automáticamente (RF-079; cierra la Decisión Pendiente
  #6 para este caso). Hasta esta sesión, este documento y `research.md` §14.5 afirmaban incorrectamente que
  ese comportamiento ya existía.
- Q: ¿En qué calendario se interpretan las fechas y los bloques horarios? → A: Cada Compañía Principal tiene
  su propia zona horaria IANA (`ZonaHorariaIana`), obligatoria para `PRINCIPAL_MANDANTE`; los timestamps
  siguen persistiéndose siempre en UTC y la zona solo convierte entre ese instante y la hora local. Las
  asociaciones no resolubles a una única Principal usan la zona global de respaldo del sistema. Cambiar la
  zona no reinterpreta instantes ya persistidos, solo su representación futura, y no se versiona
  históricamente (RF-080; resuelve la ambigüedad de RF-016).
- Q: ¿Puede cambiarse el `TipoCompañía` de una compañía que ya tiene dependientes? → A: No mientras existan
  dependencias incompatibles con el tipo destino (áreas de acceso, raíces de unidad organizativa, relaciones
  Contratista↔Principal vigentes —simétricamente en ambas direcciones—, contextos operativos y credenciales).
  La operación se rechaza indicando qué resolver primero; nunca se eliminan, cierran ni revocan dependencias
  en cascada (RF-081).
- Q: ¿Entran las consultas transversales (RF-067 a RF-069, CS-032) en el baseline de Etapa 1? → A: No. Quedan
  explícitamente fuera del alcance obligatorio de Etapa 1 y diferidas a una etapa futura, sin retirarse de
  este documento y sin generar tareas de baseline (anotaciones `[DIFERIDA A ETAPA 2]` en cada una).
- Q: ¿Qué ocurre con las decisiones heredadas #1, #3 y #7? → A: Las tres se cierran sin trabajo técnico
  derivado: #1 la política de contraseñas ya configurada queda como autoridad única; #3 el baseline no
  implementa retención ni purga y conserva el comportamiento actual; #7 Historia 9 mantiene P2. Ver la sección
  Decisiones Pendientes.

### Sesión 2026-09-20 (cierre de la desviación D-1 — renovación de `AsignaciónRolAdministrativo`)

Desviación detectada al finalizar `/speckit-implement` sobre T169–T228: el dominio implementa la renovación
de una `AsignaciónRolAdministrativo` (`RenovarAsync`/`RenovarRolAsync`, con las reglas de vigencia de RF-073
referenciadas desde RF-075), pero no se expuso ningún endpoint HTTP porque `contracts/users.yaml` no lo
declaraba — publicar una ruta no declarada habría roto la conformidad contractual que vigilan las pruebas de
contrato. Sesión de `/speckit-clarify` que no abre ninguna decisión de negocio nueva: cierra esa desviación
transcribiendo a `spec.md` una decisión de alcance ya aprobada.

- Q: ¿La renovación de una `AsignaciónRolAdministrativo` es una capacidad del Baseline de Etapa 1? → A:
  **Sí.** Se incorpora al contrato funcional y HTTP del Baseline, reutilizando íntegramente las reglas de
  vigencia ya aprobadas en RF-073 (referenciadas, sin reabrirlas ni modificar su texto, que sigue siendo
  específico de `AsignaciónPersonaCompañía`) y aplicando las mismas restricciones de autorización (RF-076) y
  de alcance/ocultamiento de existencia (RF-077) que el resto de operaciones sobre asignaciones de rol. La
  operación HTTP (ruta, verbo, cuerpo y códigos de respuesta) se define en la fase de planificación siguiendo
  el precedente ya vigente de `contracts/people.yaml` (`.../historial-companias/{id}/renovar`) y el patrón ya
  usado por `.../roles/{asignacionId}/finalizar` en el propio `contracts/users.yaml`; no se declara en esta
  sesión de clarificación para no adelantar contenido de `research.md`/`contracts/`, que corresponde a
  `/speckit-plan` (RF-075).

### Sesión 2026-09-20 (cierre de las desviaciones D-4 y D-5 — auditoría de decisión del Baseline)

Auditoría de decisión sobre D-2 a D-5 realizada tras T169–T228 (`/speckit-analyze`, sesión previa). D-2 y D-3
se cerraron como implementación válida sin cambios de especificación (clasificación A). Esta sesión de
`/speckit-clarify` formaliza las dos decisiones que sí requerían intervención del usuario, ya tomadas
explícitamente por el usuario, sin reabrirlas como preguntas.

- Q: ¿La búsqueda de usuarios dentro del alcance autorizado (UX-22) debe operar sobre todo el conjunto
  autorizado o solo sobre la página ya cargada? → A: **Sobre todo el conjunto autorizado.** La búsqueda es
  server-side: se aplica sobre el universo de usuarios ya restringido al alcance del actor (RF-077) y el
  resultado se pagina después, nunca al revés — el filtrado nunca se limita a los registros de la página
  actualmente visible en el cliente. La búsqueda NO amplía el alcance autorizado bajo ninguna circunstancia:
  un `COMPANY_ADMINISTRATOR` solo obtiene resultados de su propia compañía y un `GLOBAL_ADMINISTRATOR` busca
  dentro de su alcance GLOBAL, con las mismas reglas de aislamiento, autorización y ocultamiento de existencia
  que el listado normal (RF-077); conocer un correo o identificador no concede acceso al recurso. El contrato
  HTTP actual (`contracts/users.yaml`, `GET /api/usuarios` con `pagina`/`tamañoPagina`/`estado`) no declara
  todavía un parámetro de búsqueda — su nombre, el soporte de múltiples criterios, los códigos de error
  asociados y cualquier requisito de ordenamiento o rendimiento se definen en `/speckit-plan`, no en esta
  sesión. *(Nuevo — Sesión 2026-09-20, cierre de la desviación D-4; precisa RF-077 y UX-22 sin
  reemplazarlos.)*
- Q: ¿La contraseña inicial del `GLOBAL_ADMINISTRATOR` de arranque (RF-078) puede tener un valor por defecto
  en entornos de desarrollo? → A: **No, en ningún entorno.** RF-078 ya exigía que la contraseña NO esté
  incrustada en archivos de configuración versionados ni en el repositorio, sin distinguir entre desarrollo y
  producción; esa regla no se relaja por conveniencia de desarrollo. La configuración de desarrollo DEBE
  requerir la variable de entorno correspondiente igual que producción, y el arranque DEBE fallar
  explícitamente si no se provee, en lugar de levantar con una contraseña adivinable. Esta sesión no modifica
  el texto de RF-078 —ya era inequívoco— y cierra la desviación detectada en `docker-compose.yml`, cuya
  corrección de configuración y documentación se realiza fuera de `spec.md`. *(Cierre de la desviación D-5 —
  Sesión 2026-09-20; RF-078 sin cambios de texto.)*

## Historias de Usuario y Pruebas

### Historia 1 - Inicio de sesión y alcance de gestión (Prioridad P1)

Como usuario autorizado, quiero iniciar sesión mediante correo electrónico y contraseña para que el
sistema determine qué compañías puedo gestionar y limite todas mis operaciones a ese alcance.

Criterios de aceptación:

1. Un usuario ACTIVO con contraseña válida puede iniciar sesión y obtiene su alcance de compañías.
2. Un usuario INACTIVO o BLOQUEADO no puede iniciar sesión.
3. Si la política de seguridad exige cambio de contraseña, el sistema debe solicitarlo.
4. Un usuario que gestiona las compañías A y B no puede consultar ni modificar personas de C.

### Historia 2 - Compañías (Principal/Contratista), sus relaciones y unidades organizativas (Prioridad P1)

Como usuario autorizado, quiero clasificar cada compañía como PRINCIPAL_MANDANTE o CONTRATISTA, declarar
qué compañías Contratistas prestan servicios a qué compañías Principales, y administrar las unidades
organizativas jerárquicas que representan exclusivamente la estructura organizativa de una Compañía
Principal/Mandante.

El sistema puede tener una o varias Compañías Principales/Mandantes simultáneamente; no debe asumirse una
única Principal global. Cada Compañía Principal constituye su propio contexto organizacional: sus unidades
organizativas están aisladas de las de cualquier otra Compañía Principal, y puede tener varios árboles de
unidades organizativas (varios nodos raíz) simultáneamente. Las Compañías Contratistas no poseen unidades
organizativas propias; prestan servicios a una o varias Compañías Principales de forma simultánea o en
distintos períodos, mediante una `RelaciónContratistaPrincipal` explícita y con vigencia temporal — no
mediante un único campo `PrincipalId` en la Contratista, ya que una misma Contratista puede tener relaciones
vigentes con varias Principales a la vez.

Criterios de aceptación:

1. Toda compañía se crea con una clasificación explícita PRINCIPAL_MANDANTE o CONTRATISTA.
2. Una unidad organizativa raíz (sin unidad superior) se asocia a exactamente una Compañía Principal al
   crearse.
3. Una unidad hija aparece debajo de su unidad superior y pertenece implícitamente a la misma Compañía
   Principal que su raíz (heredada recorriendo la jerarquía, no mediante una relación propia).
4. No se permite que una unidad sea ancestro de sí misma.
5. No es posible asociar una unidad organizativa raíz a una compañía clasificada como CONTRATISTA.
6. Una compañía o unidad INACTIVA no puede recibir nuevas asignaciones activas.
7. Las unidades organizativas de una Compañía Principal solo son visibles y administrables por usuarios
   cuyo alcance de compañías incluya esa Compañía Principal — alcance resuelto a partir de sus
   `AsignaciónRolAdministrativo` vigentes (RF-074, RF-077; antes de la Sesión 2026-09-20 esta entidad se
   llamaba `AlcanceUsuarioCompañía`).
8. Una Compañía Principal puede tener varios nodos raíz de unidad organizativa (varios árboles) al mismo
   tiempo (CS-010); dos Compañías Principales distintas nunca comparten unidades organizativas (CS-011).
9. Una Compañía Contratista puede declararse con relación vigente simultánea hacia dos o más Compañías
   Principales distintas (CS-012); no es posible declarar una relación entre dos compañías que no sean,
   respectivamente, CONTRATISTA y PRINCIPAL_MANDANTE.
10. Las relaciones Contratista↔Principal solo son visibles/administrables por usuarios cuyo alcance de
    compañías incluya la Compañía Principal de esa relación (RF-060).

### Historia 3 - Datos maestros (Prioridad P1)

Como usuario autorizado, quiero administrar los datos maestros para mantener catálogos consistentes.

Debe existir mantenimiento para Tipo de Documento, Tipo de Sangre, Género, Tipo de Persona y Tipo de
Credencial. Los datos genéricos iniciales para Perú deben cargarse mediante datos versionados (aplica a
Tipo de Documento, Tipo de Sangre y Género; Tipo de Persona y Tipo de Credencial son catálogos configurados
por negocio, sin semilla obligatoria). Tipo de Credencial representa el tipo/diseño visual de la credencial
(ver Historia 9), no una tecnología de identificación física.

### Historia 4 - Personas (Prioridad P1)

Como usuario autorizado, quiero registrar personas con sus datos personales, de identificación y contacto.

Los datos obligatorios son:
- ID autogenerado
- nombres
- apellidos
- fecha de nacimiento
- tipo de documento
- número de documento
- género
- correo electrónico
- tipo de sangre
- contacto de emergencia
- número de emergencia

El ID es UID/UUID, autogenerado, no editable y no visible en las interfaces gráficas.

La combinación de tipo de documento y número de documento DEBE ser única en todo el sistema: no se
permite registrar dos personas con el mismo tipo y número de documento.

### Historia 5 - Históricos de compañía, contexto operativo por Principal, unidad organizativa y perfil (Prioridad P1)

Como usuario autorizado, quiero conservar los históricos de pertenencia de una persona a su compañía
empleadora, y administrar sus contextos operativos — uno por cada Compañía Principal para la que trabaja —
cada uno con su propia unidad organizativa asignada.

Cada asignación debe registrar fecha/hora de inicio y fecha/hora de fin — **ambas obligatorias desde la
creación** (RF-071, Sesión 2026-09-14 "vigencia temporal jerárquica"): ya no existe una compañía de
pertenencia, contexto operativo o unidad organizativa de vigencia indefinida/abierta; toda asignación nace
con una fecha de fin real y conocida. Para compañía, contexto operativo y unidad organizativa, el inicio
debe normalizarse a 00:00 y el fin a 23:59 del último día de vigencia.

Los períodos incompatibles no deben solaparse. Para compañía de pertenencia, una persona solo puede tener
una asignación activa a la vez (histórico secuencial, RF-014, sin cambios respecto a la decisión original).
Además, todo contexto operativo, asignación de unidad organizativa y credencial que dependa de esa
pertenencia DEBE estar temporalmente contenido dentro de su vigencia: no puede iniciar antes que ella ni
extenderse más allá de su fin, aunque sí puede terminar antes (RF-072, contención, no igualdad). La
`FechaHoraFin` de una pertenencia vigente (`Estado = ACTIVA`) puede **renovarse** — extenderse hacia una
fecha posterior — sin crear una nueva `AsignaciónPersonaCompañía` ni afectar a sus asociaciones dependientes
ya existentes; una pertenencia `FINALIZADA` no es renovable (RF-073).

**Compañía de pertenencia vs. contexto(s) operativo(s) — distinción fundamental**: el histórico de compañía
de una persona (`AsignaciónPersonaCompañía`) representa únicamente su relación laboral/contractual — a qué
empresa pertenece (Principal o Contratista) — y NO debe utilizarse para representar para qué Compañía
Principal está trabajando o accediendo. Esa segunda relación se modela mediante
`ContextoOperativoPersonaPrincipal`, independiente del histórico de compañía: una persona puede tener
ningún, uno o varios contextos operativos vigentes **simultáneamente**, cada uno con una Compañía Principal
distinta (CS-013). Dentro de cada contexto operativo, la persona tiene su propia unidad organizativa
asignada (`AsignaciónPersonaUnidadOrganizativa`, referenciada al contexto), independiente de la unidad
asignada en cualquier otro contexto (CS-014, CS-005 se mantiene pero ahora aplica POR contexto: como máximo
una asignación de unidad organizativa activa a la vez **dentro de un mismo contexto operativo**; contextos
distintos pueden tener asignaciones de unidad organizativa simultáneas y vigentes).

**Restricción de apertura de un contexto operativo**:
- Si la compañía vigente de la persona es PRINCIPAL_MANDANTE, su contexto operativo con esa misma
  Principal se determina y fija automáticamente por el sistema (CS-020); no se permite abrir un contexto
  con una Principal distinta.
- Si la compañía vigente de la persona es CONTRATISTA, un nuevo contexto operativo solo puede abrirse con
  una Compañía Principal para la cual exista una `RelaciónContratistaPrincipal` vigente con esa Contratista
  (CS-019).
- En ambos casos, el nuevo contexto operativo DEBE registrar su propia `FechaHoraFin` real (RF-071), y su
  vigencia completa DEBE estar contenida dentro de la vigencia de la `AsignaciónPersonaCompañía` vigente de
  la persona (RF-072) — el sistema DEBE rechazar un contexto cuya vigencia exceda a la de la pertenencia que
  lo sustenta. La misma contención aplica a la asignación de unidad organizativa dentro del contexto y a la
  credencial emitida para esa Principal (RF-072).

### Comportamiento de la pantalla de asignación de Unidad Organizativa

La pantalla debe comportarse de forma distinta según el tipo de compañía a la que pertenece la persona:

**Caso A — persona perteneciente a una Compañía Principal.** El sistema determina automáticamente la
Compañía Principal (la misma a la que pertenece la persona); no debe solicitar seleccionar otra. Se muestra
directamente el árbol de unidades organizativas de esa Principal para que el usuario seleccione la unidad y
registre su vigencia.

**Caso B — persona perteneciente a una Compañía Contratista.** La pantalla debe ofrecer un selector de
Compañía Principal, cuyas opciones se limitan exclusivamente a las Principales con las que la Contratista de
la persona tenga una `RelaciónContratistaPrincipal` vigente. Tras seleccionar la Principal, el sistema carga
exclusivamente el árbol de unidades organizativas de esa Principal (CS-021) — nunca debe mostrarse en un
mismo árbol unidades pertenecientes a Principales distintas. El usuario selecciona la unidad correspondiente
dentro de ese árbol y registra su vigencia; esto crea o reutiliza el contexto operativo con esa Principal.

**Ejemplo** (equivalente al del spec): Pedro García pertenece a Servicios ACME (CONTRATISTA), que tiene
relaciones vigentes con Minera ABC y Minera XYZ. Pedro puede tener, simultáneamente, un contexto operativo
con Minera ABC (unidad Operaciones > Mina > Mantenimiento) y otro con Minera XYZ (unidad Operaciones >
Planta); ambos contextos, sus unidades, permisos y credenciales permanecen completamente independientes
entre sí.

### Revocación automática por cese de pertenencia (regla fundamental)

> El cese o inactivación de una pertenencia Persona–Compañía provoca la revocación automática de todos los
> contextos operativos, asignaciones de Unidad Organizativa y credenciales que dependan de dicha
> pertenencia, preservando íntegramente el histórico. La revocación no afecta contextos que dependan de
> otras pertenencias vigentes de la misma persona.

Esta regla es obligatoria (RF-061 a RF-065) y corrige la decisión previa de la Sesión 2026-09-14 "auditoría
final de consistencia" (que dejaba la revocación exclusivamente a cargo de la re-validación dinámica de la
evaluación de acceso). Ambos mecanismos coexisten ahora: la revocación en cascada es la garantía primaria
del dominio, auditable y aplicada en el momento del cese; la re-validación dinámica (Historia 8) permanece
como defensa adicional.

Cuando finaliza (o se inactiva) la `AsignaciónPersonaCompañía` vigente de una persona — por cierre explícito
o porque una nueva asignación la reemplaza automáticamente —, el sistema DEBE, en la misma operación:
1. Fijar `FechaHoraFin` en cada `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y
   `AsignaciónCredencial` de esa persona que esté vigente y dependa de esa pertenencia, usando el mismo
   valor efectivo de finalización (sin extender ninguna `FechaHoraFin` ya fijada a una fecha anterior).
2. Marcar su `Estado` como finalizado/revocado y registrar el `MotivoFin` correspondiente.
3. Conservar íntegramente el registro — nunca eliminarlo físicamente ni alterar su `FechaHoraInicio`.
4. Dejar sin afectar cualquier contexto, asignación o credencial que dependa de otra pertenencia vigente de
   la misma persona (no aplica en la práctica salvo tras un cambio de compañía posterior, dado que RF-014
   impide pertenencias simultáneas — ver Clarifications, Sesión "corrección Revocación Automática").

Si el cese se registra con una `FechaHoraFin` futura, los registros dependientes permanecen efectivamente
vigentes (y siguen produciendo acceso) hasta ese instante — el sistema propaga la misma fecha de inmediato,
sin esperar a que llegue, y sin necesitar un proceso programado.

### Historia 6 - Árbol de áreas físicas de acceso (Prioridad P1)

Como usuario autorizado, quiero crear y visualizar las áreas físicas de acceso como un árbol.

Cada área puede tener un área padre. No se permiten ciclos. Cada área de acceso pertenece exclusivamente a
una Compañía Principal/Mandante (la compañía que administra físicamente esas instalaciones); un área hija
pertenece a la misma Compañía Principal que su área padre. Los usuarios solo pueden crear, ver o administrar
áreas de las Compañías Principales dentro de su alcance de compañías.

Criterio de aceptación adicional: un área raíz se crea asociada a exactamente una Compañía Principal; no es
posible asociarla a una compañía clasificada como CONTRATISTA.

### Historia 7 - Tipos de persona autorizados por área (Prioridad P1)

Como usuario autorizado, quiero indicar qué tipos de persona pueden acceder a cada área.

Un área puede tener uno o varios tipos de persona asociados, por ejemplo Trabajador, Visitante y
Proveedor. La autorización evalúa esta restricción dentro del contexto de la Compañía Principal propietaria
del área (Historia 8, paso 9 del algoritmo de evaluación — corregido en la Sesión 2026-09-20: la referencia
anterior apuntaba al paso 7, que verifica el área activa, no el perfil autorizado).

### Historia 8 - Permisos de acceso con vigencia y horarios (Prioridad P1)

Como usuario autorizado, quiero configurar permisos de acceso por persona, unidad organizativa o compañía,
siempre evaluados dentro del contexto de la Compañía Principal propietaria del área.

Una persona perteneciente a una Compañía Contratista puede tener permisos concedidos sobre áreas de acceso
administradas por una Compañía Principal, siempre que tenga un contexto operativo vigente con esa Principal
(Historia 5) y cumpla el resto de las reglas de autorización, perfil, unidad organizativa, vigencia y
horario; no existe restricción adicional derivada del tipo de compañía (PRINCIPAL_MANDANTE o CONTRATISTA)
de la persona ni de la compañía indicada en un permiso de alcance COMPAÑÍA. Una persona puede tener
permisos distintos para cada Principal con la que tiene contexto operativo vigente, y esos permisos nunca
se mezclan entre Principales (CS-015, CS-018): el acceso a un área de la Principal A nunca puede ser
otorgado por un permiso correspondiente a la Principal B.

Cada permiso debe tener:
- área de acceso
- alcance: PERSONA, UNIDAD_ORGANIZATIVA o COMPAÑÍA
- fecha/hora de inicio
- fecha/hora de fin
- estado
- uno o varios bloques horarios por día de semana

La evaluación de acceso DEBE seguir, en orden, los siguientes pasos (mantiene el principio de denegación por
defecto: cualquier paso sin resultado inequívoco produce DENEGADO):

1. Verificar que el usuario autenticado que solicita la evaluación tenga, en su alcance de compañías, la
   Compañía Principal que se determinará en el paso 4 (autorización de la propia consulta, RF-005, RF-049).
2. Identificar a la persona evaluada.
3. Identificar el Área de Acceso evaluada.
4. Determinar la Compañía Principal propietaria del área (vía `ÁreaAcceso.CompañíaPrincipalId`).
5. Verificar que esa Compañía Principal tenga `Estado = ACTIVO` (RF-079, Sesión 2026-09-20). Si está
   `INACTIVO`, el acceso se deniega por defecto (`COMPANIA_INACTIVA`) sin evaluar los pasos restantes. La
   verificación es dinámica: no escribe ni modifica ningún registro, y reactivar la compañía restablece el
   acceso sin intervención adicional.
6. Verificar que la persona tenga un `ContextoOperativoPersonaPrincipal` vigente con esa Principal en la
   fecha evaluada, **y que esa relación siga siendo legítima según la compañía de pertenencia vigente de la
   persona en esa misma fecha** (RF-061): automática si esa compañía es la propia Principal (RF-053), o
   mediante una `RelaciónContratistaPrincipal` vigente si es una Contratista (RF-054, RF-059). Un cambio de
   compañía de pertenencia NO cierra el contexto operativo, pero sí puede hacer que deje de ser legítimo
   para efectos de esta evaluación (Decisión Pendiente #4 resuelta, Clarifications). Además, esa compañía de
   pertenencia vigente DEBE tener `Estado = ACTIVO`; si está `INACTIVO`, el acceso se deniega por defecto
   con el mismo motivo `COMPANIA_INACTIVA` del paso 5, también de forma dinámica y reversible (RF-079).
7. Verificar que exista una `AsignaciónCredencial` **vigente** para la persona y la Compañía Principal
   determinada en el paso 4, en la fecha evaluada — donde "vigente" significa la conjunción de **ambas**
   condiciones (RF-066, RF-070, RF-071): (a) `Estado = ASIGNADO`, y (b) `FechaHoraInicio <= fecha evaluada
   <= FechaHoraFin` (ambos campos son obligatorios desde RF-071; ya no existe la rama "`FechaHoraFin` es
   `null`"). En ausencia de una credencial que cumpla ambas condiciones (nunca asignada; en Estado
   `DEVUELTO`, `ELIMINADO` o `REVOCADA`; o en Estado `ASIGNADO`
   pero **temporalmente expirada**, es decir con `FechaHoraFin` ya pasado) el acceso se deniega por defecto
   (`SIN_CREDENCIAL_VIGENTE`). Una credencial `ASIGNADO` cuya `FechaHoraFin` ya pasó NO se transiciona
   automáticamente a otro `Estado` por el mero paso del tiempo (RF-070) — la denegación surge exclusivamente
   de esta verificación dinámica, no de una escritura de estado.
8. Verificar que el área esté ACTIVA.
9. Verificar que algún perfil (tipo de persona) vigente de la persona esté autorizado en el área (Historia
   7, RF-024).
10. Determinar la unidad organizativa vigente de la persona dentro de ese contexto operativo (si existe).
11. Evaluar los permisos aplicables en los tres niveles: PERSONA, UNIDAD_ORGANIZATIVA (la determinada en el
    paso 10) y COMPAÑÍA (la compañía de pertenencia vigente de la persona).
12. Evaluar la vigencia de fechas de cada permiso aplicable.
13. Evaluar día de semana y bloque horario en la **zona horaria de la Compañía Principal propietaria del
    área** determinada en el paso 4 (`Compañía.ZonaHorariaIana`, RF-080, Sesión 2026-09-20); si esa zona no
    fuera resoluble, en la zona horaria global de respaldo del sistema. *(Antes de esa sesión este paso
    usaba `America/Lima` de forma fija para todas las compañías.)*
14. Resolver conflictos entre permisos aplicables en varios niveles con precedencia definitiva PERSONA >
    UNIDAD_ORGANIZATIVA > COMPAÑÍA (el nivel más específico siempre prevalece).
15. Conceder o denegar el acceso.

> Nota de consolidación: esta lista reemplazó originalmente la de 9 pasos de una corrección anterior,
> fusionándola con una lista de 13 pasos entregada por negocio. Se preservaron dos verificaciones de la
> lista anterior que esa lista de negocio no repetía explícitamente pero que seguían vigentes y no fueron
> contradichas: la autorización de la propia consulta por alcance de compañías (paso 1, RF-005) y la
> elegibilidad de perfil/área (paso 8, RF-024, Historia 7). El paso 6 (credencial vigente) se agregó en la
> Sesión 2026-09-14 de integración de `ux-ui.md` (RF-066), llevando el total de 13 a 14 pasos. Ver
> research.md §7.
>
> **Renumeración (Sesión 2026-09-20, cierre de Etapa 1)**: el total pasa de 14 a **15 pasos** al insertarse el
> paso 5 (Compañía Principal `ACTIVO`, RF-079). Los pasos 1 a 4 conservan su número; **todos los posteriores
> se desplazan en uno**: el antiguo paso 5 (contexto operativo) es ahora el 6, el 6 (credencial) el 7, el 7
> (área activa) el 8, el 8 (perfil) el 9, el 9 (unidad organizativa) el 10, el 10 (permisos) el 11, el 11
> (vigencia) el 12, el 12 (bloque horario) el 13, el 13 (precedencia) el 14 y el 14 (conceder/denegar) el 15.
> Cualquier referencia a un número de paso escrita **antes** de esta sesión —en las sesiones de Clarifications
> anteriores, en `research.md` o en los contratos— debe leerse contra esa correspondencia.

### Historia 9 - Asignación de credencial/fotocheck por Compañía Principal (Prioridad P2)

Como usuario autorizado, quiero asignar una credencial a una persona **dentro del contexto de una Compañía
Principal** y conservar su histórico.

**Corrección de alcance de `TipoCredencial`**: `TipoCredencial` representa el tipo/diseño visual de la
credencial que usará la persona (p. ej. "Credencial Contratista", "Credencial Corporativo", "Credencial
Visitante", "Credencial Proveedor", "Credencial Temporal", u otros tipos definidos por negocio) — **nunca**
una tecnología de identificación física (RFID, QR, NFC, código de barras) ni un identificador físico. La
impresión física y el diseño gráfico de la credencial quedan fuera del alcance de este sistema; el sistema
únicamente registra qué tipo de credencial corresponde a la persona (RF-058).

**Credencial por Principal, no global**: las credenciales se asignan dentro del contexto de una Compañía
Principal específica (RF-056). Una persona puede tener credenciales distintas, vigentes simultáneamente,
para cada Compañía Principal con la que tenga contexto operativo (Historia 5) — p. ej. una "Credencial
Contratista" vigente para Minera ABC y otra "Credencial Contratista" vigente, al mismo tiempo, para Minera
XYZ (CS-016, CS-017). No se modela una única credencial global de la persona.

Cada asignación contiene como mínimo:
- persona
- Compañía Principal para la cual se emite (RF-056)
- tipo de credencial
- fecha/hora de inicio
- fecha/hora de fin (obligatoria, real y conocida desde la creación — RF-071; ya no existe una credencial de
  vigencia indefinida, y su ventana completa DEBE estar contenida dentro de la vigencia de la compañía de
  pertenencia que la sustenta — RF-072)
- estado: ASIGNADO, DEVUELTO, ELIMINADO, **REVOCADA** (nuevo — RF-061, revocación automática por cese de la
  pertenencia de la persona a su compañía)

Cuando finaliza la pertenencia de la persona a su compañía, toda credencial `ASIGNADO` que dependa de esa
pertenencia pasa automáticamente a `REVOCADA` (Historia 5, "Revocación automática por cese de pertenencia");
el registro se conserva íntegro, nunca se elimina. `REVOCADA` es distinto de `DEVUELTO` (devolución física
voluntaria) y de `ELIMINADO` (baja lógica administrativa): representa específicamente que el acceso fue
invalidado por una causa ajena a la credencial misma.

En cualquier momento dado, una persona DEBE tener como máximo una credencial ASIGNADA activa **por Compañía
Principal** (sin solapamiento dentro de la misma Principal); distintas Principales pueden tener credenciales
ASIGNADAS simultáneas para la misma persona (RF-057). Asignar una credencial nunca cierra, finaliza, devuelve,
elimina ni revoca automáticamente una credencial anterior: si su período se solapa con el de otra credencial
ASIGNADA de la misma persona y Compañía Principal, la asignación se rechaza y la existente no se modifica
(Sesión 2026-09-15, decisión A). La asignación de una credencial requiere una Compañía
Principal y un TipoCredencial (CS-022); una credencial emitida en el contexto de la Principal A no puede
utilizarse para satisfacer reglas de contexto o autorización de la Principal B (CS-024). **[REEMPLAZADA —
ver Sesión 2026-09-14 "integración `ux-ui.md`" en Clarifications]** ~~la credencial es un elemento
complementario de identificación dentro de ese contexto, nunca el mecanismo que por sí solo determina el
permiso de acceso (Historia 8).~~ La credencial DEBE estar **vigente** (RF-070: `Estado = ASIGNADO` **y**
dentro de su ventana `FechaHoraInicio`/`FechaHoraFin`) para la Compañía Principal evaluada, como condición
**necesaria** (no suficiente) para conceder acceso — su ausencia deniega el acceso por defecto, aunque la
persona cumpla el resto de las reglas de Historia 8 (RF-066); una credencial `ASIGNADO` cuya vigencia
temporal ya expiró NO deja de estar `ASIGNADA` por ese solo hecho (el sistema no la transiciona
automáticamente), pero tampoco es vigente para efectos de esta evaluación. Esto no cambia CS-024 (una
credencial de la Principal A sigue sin poder usarse para la Principal B), que ahora
aplica también a este nuevo gate.

Eliminar significa baja lógica, no eliminación física del histórico.

### Historia 10 - Auditoría (Prioridad P2)

Todas las entidades persistentes deben registrar automáticamente:
- Fecha de Registro
- Fecha de Última Actualización
- ID Usuario Creación
- ID Usuario Última Actualización

Estos campos no se registran manualmente y no son visibles/editables en las pantallas normales.

**Consulta transversal de auditoría e históricos, e indicadores agregados (Sesión 2026-09-14, integración
`ux-ui.md`)**: además del registro automático por entidad ya exigido arriba, el sistema DEBE exponer una
consulta agregada y transversal de auditoría — no limitada a una entidad individual — filtrable por rango
temporal, usuario, persona, compañía, Compañía Principal, entidad y acción, respetando siempre el alcance
de compañías del usuario autenticado (RF-067). De forma análoga, los históricos de compañía de pertenencia,
contexto operativo y unidad organizativa (Historia 5) DEBEN poder consultarse de forma transversal —no solo
por persona individual—, filtrables por persona, compañía, Compañía Principal, entidad y tipo de evento
(RF-068). El sistema también DEBE exponer indicadores operativos agregados (p. ej. personas activas,
Compañías Contratistas activas, Compañías Principales activas, credenciales próximas a vencer, relaciones o
pertenencias próximas a finalizar) respetando el mismo alcance de compañías (RF-069). El contenido detallado
de cada pantalla se especifica en `ux-ui.md` §8, §20 y §21; este documento permanece funcional y no se
amplía con detalles de presentación. La forma concreta del/los endpoint(s) que resuelven estas consultas es
una decisión arquitectónica de `plan.md`/`contracts/`, fuera de alcance de este documento.

## Requisitos Funcionales

- RF-001: El sistema DEBE autenticar mediante correo electrónico y contraseña.
- RF-002: El usuario DEBE soportar estados ACTIVO, INACTIVO y BLOQUEADO.
- RF-003: DEBE existir historial de contraseñas; las contraseñas no pueden almacenarse en texto plano.
- RF-004: El alcance de compañías administrables DEBE ser una entidad independiente. **[AMPLIADA — Sesión
  2026-09-20, D1: esa entidad es `AsignaciónRolAdministrativo` y expresa el alcance mediante un rol
  administrativo con tipo de alcance GLOBAL o COMPAÑÍA, no como una lista plana de compañías. Ver RF-074 a
  RF-077.]**
- RF-005: Las consultas y operaciones DEBEN respetar el alcance de compañías del usuario autenticado.
  **[AMPLIADA — Sesión 2026-09-20, D1/D3: el alcance se resuelve a partir del rol administrativo vigente
  (RF-077), y la comprobación incluye la pertenencia del recurso concreto a ese alcance, no solo que el
  alcance no esté vacío.]**
- RF-006: Compañía DEBE contener ID, compañía, tipo de documento, número de documento, tipo de compañía
  (PRINCIPAL_MANDANTE o CONTRATISTA) y estado.
- RF-007: Unidad Organizativa DEBE contener ID, nombre, unidad superior y estado.
- RF-008: Las unidades organizativas DEBEN visualizarse como árbol.
- RF-009: Las áreas de acceso DEBEN visualizarse como árbol.
- RF-010: DEBEN existir tipos/perfiles de persona.
- RF-011: Una persona DEBE poder tener uno o varios tipos/perfiles.
- RF-012: Persona DEBE contener todos los datos personales especificados.
- RF-013: Todos los IDs DEBEN ser UID/UUID autogenerados, inmutables y ocultos en la interfaz.
- RF-014: DEBE existir histórico de compañía **de pertenencia laboral/contractual**
  (`AsignaciónPersonaCompañía`) por persona; en cualquier momento dado, una persona DEBE tener como máximo
  UNA asignación de compañía de pertenencia activa (sin solapamiento) — es decir, a qué única compañía
  pertenece actualmente. **Esta restricción NO limita, ni directa ni indirectamente ni por analogía, el
  número de `ContextoOperativoPersonaPrincipal` vigentes simultáneos de la persona**, que se rige de forma
  completamente independiente por su propia regla de cardinalidad (RF-052: como máximo un contexto activo
  por cada Compañía Principal distinta, sin límite superior de Principales simultáneas). *(Redacción
  aclarada — Sesión 2026-09-14 "corrección Modelo de Cardinalidad Definitivo"; no cambia ninguna regla de
  negocio ya vigente.)*
- RF-015: DEBE existir histórico de unidad organizativa por persona, registrado dentro de un contexto
  operativo `ContextoOperativoPersonaPrincipal` vigente (RF-052); en cualquier momento dado, una persona
  DEBE tener como máximo una asignación de unidad organizativa activa **por contexto operativo** (sin
  solapamiento dentro del mismo contexto) — distintos contextos operativos (distintas Compañías
  Principales) pueden tener asignaciones de unidad organizativa simultáneas y vigentes para la misma
  persona. *(Corrige la versión anterior de este requisito, que imponía como máximo una asignación activa
  por persona a nivel global; ver Clarifications, Sesión 2026-09-14 "corrección Contexto Operativo".)*
- RF-016: Las asignaciones de compañía y unidad DEBEN iniciar a 00:00 y finalizar a 23:59 del último día.
  **[MATIZADA — Sesión 2026-09-20, D5: el «día» se interpreta en la zona horaria de la Compañía Principal
  correspondiente (`Compañía.ZonaHorariaIana`) y, cuando la asignación no es resoluble a una única Compañía
  Principal, en la zona horaria global de respaldo del sistema. Ver RF-080.]**
- RF-017: Tipo de Credencial DEBE tener estado ACTIVO/INACTIVO.
- RF-018: DEBE existir histórico de asignación de credenciales.
- RF-019: Un área DEBE poder asociarse a uno o varios tipos de persona.
- RF-020: Los permisos DEBEN soportar alcance persona, unidad organizativa y compañía.
- RF-021: Todo permiso DEBE tener inicio y fin de vigencia.
- RF-022: Todo permiso DEBE soportar bloques horarios por día de semana.
- RF-023: La evaluación DEBE utilizar las asignaciones vigentes en la fecha evaluada.
- RF-024: La elegibilidad de perfil/área DEBE validarse antes de conceder acceso.
- RF-025: Los conflictos entre permisos aplicables en distintos niveles DEBEN resolverse de forma
  determinística usando la precedencia PERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA (el nivel más
  específico prevalece).
- RF-026: Todas las entidades persistentes DEBEN tener auditoría.
- RF-027: La auditoría DEBE ser generada por el sistema.
- RF-028: Los campos de negocio DEBEN ser obligatorios salvo que se definan explícitamente como opcionales.
- RF-029: Los campos de fecha/hora DEBEN manejar fecha y hora.
- RF-030: DEBEN existir módulos de mantenimiento para todas las entidades indicadas.
- RF-031: DEBEN cargarse valores maestros genéricos válidos para Perú.
- RF-032: Los valores maestros INACTIVOS no pueden utilizarse en nuevas asignaciones.
- RF-033: Las validaciones deben devolver mensajes comprensibles.
- RF-034: Toda operación protegida DEBE requerir autorización.
- RF-035: DEBE existir búsqueda de personas dentro del alcance autorizado.
- RF-036: Los árboles DEBEN permitir expandir, contraer y seleccionar nodos.
- RF-037: Los históricos DEBEN permitir reconstruir el estado efectivo de una persona en una fecha/hora.
- RF-038: Los ciclos jerárquicos DEBEN rechazarse.
- RF-039: Los períodos inválidos y solapamientos incompatibles DEBEN rechazarse.
- RF-040: DEBE existir contrato API para autenticación, maestros, personas, jerarquías, credenciales,
  permisos, evaluación de acceso, y las consultas agregadas de auditoría/históricos transversales e
  indicadores operativos (RF-067 a RF-069, nuevo — Sesión 2026-09-14, integración `ux-ui.md`).
- RF-041: La combinación de tipo de documento y número de documento DEBE ser única en todo el sistema;
  el sistema DEBE rechazar el registro de una persona con una combinación ya existente.
- RF-042: Toda Compañía DEBE clasificarse como PRINCIPAL_MANDANTE o CONTRATISTA.
- RF-043: El sistema DEBE soportar una o varias compañías PRINCIPAL_MANDANTE simultáneamente; no DEBE
  asumirse una única compañía Principal global. Cada Compañía Principal constituye su propio contexto
  organizacional y operacional, aislado del de cualquier otra Compañía Principal.
- RF-044: UnidadOrganizativa NO DEBE tener ninguna columna ni relación (FK) directa hacia Compañía.
- RF-045: Toda Unidad Organizativa raíz (sin unidad superior) DEBE asociarse a exactamente una Compañía
  clasificada como PRINCIPAL_MANDANTE mediante una entidad de enlace independiente de
  `UnidadOrganizativa`; no DEBE ser posible asociarla a una compañía CONTRATISTA. Toda unidad no raíz
  hereda la Compañía Principal propietaria recorriendo la jerarquía (`UnidadSuperiorId`) hasta su raíz. Las
  compañías CONTRATISTA NO DEBEN tener unidades organizativas propias.
- RF-046: Toda ÁreaAcceso DEBE pertenecer a exactamente una Compañía clasificada como PRINCIPAL_MANDANTE;
  un área hija DEBE pertenecer a la misma Compañía Principal que su área padre.
- RF-047: El histórico de compañía de una persona (RF-014) puede referenciar indistintamente una compañía
  PRINCIPAL_MANDANTE o CONTRATISTA.
- RF-048: La asignación de una persona a una unidad organizativa (RF-015) se realiza dentro de un contexto
  operativo `ContextoOperativoPersonaPrincipal` (RF-052) vigente entre la persona y la Compañía Principal
  propietaria de esa unidad. **[REEMPLAZADA — ver Sesión 2026-09-14 "vigencia temporal jerárquica" en
  Clarifications]** ~~no existe validación cruzada entre el histórico de compañía de pertenencia (RF-014) y
  el contexto operativo, salvo la restricción de RF-054~~ SÍ existe validación cruzada temporal: la vigencia
  de `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y `AsignaciónCredencial` DEBE
  estar contenida dentro de la vigencia de la `AsignaciónPersonaCompañía` vigente de la persona en el
  momento de su creación (RF-072) — además, sin cambios, la restricción de RF-054 (una persona de una
  Contratista solo puede abrir contexto con Principales con relación vigente). Esta asignación, junto con el
  contexto operativo que la contiene, es la relación operacional explícita, histórica y temporal entre la
  persona y esa Compañía Principal; el sistema NO DEBE conceder acceso implícito de una persona a otras
  Compañías Principales para las que trabaje su empleador fuera de esta asignación. *(Corrige la versión
  anterior de este requisito, que no exigía un contexto operativo explícito ni la restricción de relación
  vigente para personas de compañías Contratistas; ver Clarifications, Sesión 2026-09-14 "corrección
  Contexto Operativo".)*
- RF-049: Las operaciones de mantenimiento y asignación sobre unidades organizativas y áreas de acceso
  DEBEN limitarse a las Compañías Principales dentro del alcance de compañías del usuario autenticado
  (resuelto vía la entidad de enlace de raíz para unidades organizativas, o directamente para áreas de
  acceso).
- RF-050: El alcance administrativo de un Usuario (`AsignaciónRolAdministrativo`, RF-004 y RF-074; entidad
  llamada `AlcanceUsuarioCompañía` antes de la Sesión 2026-09-20) DEBE mantenerse
  independiente de la relación operacional Persona→Compañía→UnidadOrganizativa; un usuario administrativo
  puede gestionar una o varias compañías según su alcance, sin que ello implique ni derive de ninguna
  asignación operacional de personas.
- RF-051: DEBE existir una relación explícita `RelaciónContratistaPrincipal` entre una Compañía CONTRATISTA
  y una o varias Compañías PRINCIPAL_MANDANTE, con vigencia temporal propia (fecha/hora de inicio y fin);
  una misma Contratista puede tener relaciones vigentes simultáneas con múltiples Principales, y una misma
  Principal puede tener relaciones vigentes simultáneas con múltiples Contratistas.
- RF-052: DEBE existir un `ContextoOperativoPersonaPrincipal` que represente la relación operativa entre
  una Persona y una Compañía Principal, independiente del histórico de compañía de pertenencia (RF-014);
  una persona puede tener ningún, uno o varios contextos operativos vigentes simultáneamente, cada uno con
  una Compañía Principal distinta, **sin límite superior en el número de Compañías Principales
  simultáneas** más allá de las que su compañía de pertenencia sustente legítimamente (directamente si es
  PRINCIPAL_MANDANTE — RF-053 — o vía `RelaciónContratistaPrincipal` vigente si es CONTRATISTA — RF-054);
  en cualquier momento dado, una persona DEBE tener como máximo un contexto operativo activo por cada
  Compañía Principal (sin solapamiento dentro del mismo par persona-Principal) — la exclusividad es por
  **par** `(Persona, Compañía Principal)`, nunca global por persona.
- RF-053: Si la compañía vigente de una persona es PRINCIPAL_MANDANTE, su contexto operativo con esa misma
  Principal DEBE fijarse automáticamente por el sistema, sin permitir seleccionar ni registrar un contexto
  con una Principal distinta.
- RF-054: Si la compañía vigente de una persona es CONTRATISTA, un nuevo contexto operativo DEBE
  restringirse únicamente a Compañías Principales con las que esa Contratista tenga una
  `RelaciónContratistaPrincipal` vigente en el momento de la asignación.
- RF-055: La asignación de unidad organizativa de una persona (RF-015, RF-048) DEBE realizarse dentro de un
  `ContextoOperativoPersonaPrincipal` vigente, y la unidad organizativa seleccionada DEBE pertenecer a la
  misma Compañía Principal de ese contexto.
- RF-056: La asignación de credencial (RF-018) DEBE registrar explícitamente la Compañía Principal para la
  cual se emite; una persona puede tener credenciales vigentes simultáneas para distintas Compañías
  Principales, condicionado a que exista un contexto operativo vigente entre la persona y esa Principal.
- RF-057: En cualquier momento dado, una persona DEBE tener como máximo una credencial en estado ASIGNADO
  activa **por Compañía Principal** (sin solapamiento dentro de la misma Principal); distintas Principales
  pueden tener credenciales ASIGNADAS simultáneas para la misma persona.
- RF-058: `TipoCredencial` DEBE representar el tipo/diseño visual de la credencial (p. ej. Contratista,
  Corporativo, Visitante, Proveedor, Temporal), NUNCA una tecnología de identificación física (RFID, QR,
  NFC, código de barras) ni un identificador físico; la impresión y el diseño gráfico quedan fuera del
  alcance del sistema.
- RF-059: La evaluación de acceso (RF-023) DEBE determinar primero la Compañía Principal propietaria del
  área solicitada, y DEBE denegar el acceso por defecto si la persona no tiene un contexto operativo
  vigente con esa Principal (y, siendo de una Contratista, si la relación Contratista→Principal
  correspondiente no está vigente en la fecha evaluada), antes de evaluar cualquier permiso.
- RF-060: Las operaciones sobre `RelaciónContratistaPrincipal` y `ContextoOperativoPersonaPrincipal` DEBEN
  limitarse a las Compañías Principales dentro del alcance de compañías del usuario autenticado (extiende
  RF-049).
- RF-061: **[REEMPLAZADA — ver Sesión 2026-09-14 "corrección Revocación Automática" en Clarifications]**
  El cese o inactivación de una pertenencia Persona–Compañía (`AsignaciónPersonaCompañía`, RF-014) —ya sea
  por cierre explícito o porque una nueva asignación reemplaza automáticamente a la vigente— DEBE provocar
  la revocación automática de todos los `ContextoOperativoPersonaPrincipal`,
  `AsignaciónPersonaUnidadOrganizativa` y `AsignaciónCredencial` de esa persona que dependan de dicha
  pertenencia y estén vigentes en ese momento, preservando íntegramente el histórico. La revocación no
  afecta contextos, asignaciones ni credenciales que dependan de otra pertenencia vigente de la misma
  persona (regla fundamental; RF-062 a RF-065 detallan alcance, campos, efectividad temporal y relación con
  la evaluación dinámica). `PermisoAcceso` no se modifica por esta revocación: es configuración compartida,
  no un registro por persona; su aplicabilidad efectiva deja de producirse como consecuencia transitiva de
  que el contexto/asignación de unidad organizativa de la que dependía ya no está vigente (RF-065).
- RF-062: La revocación automática (RF-061) DEBE determinarse por dependencia de la pertenencia concreta
  que finaliza, no por `PersonaId` en bloque: solo se revocan los registros cuya legitimidad dependía
  específicamente de esa pertenencia (según la compañía de pertenencia vigente en el momento en que cada
  contexto operativo fue abierto — RF-053/RF-054). Una persona puede tener **múltiples**
  `ContextoOperativoPersonaPrincipal` vigentes simultáneamente en el momento del cese — uno por cada
  Compañía Principal con la que su (única) compañía de pertenencia tuviera relación vigente (RF-052) — y la
  cascada de revocación DEBE alcanzarlos a **todos ellos**, precisamente porque, al existir como máximo una
  `AsignaciónPersonaCompañía` activa a la vez (RF-014), **todo** contexto abierto en ese momento depende
  necesariamente de esa misma pertenencia que finaliza (no puede existir un contexto abierto que dependiera
  de una pertenencia distinta y también vigente al mismo tiempo, porque eso requeriría dos pertenencias
  activas simultáneas, lo que RF-014 prohíbe). Esto es una consecuencia de la cardinalidad de
  `AsignaciónPersonaCompañía` (RF-014), **no una restricción sobre la cardinalidad de
  `ContextoOperativoPersonaPrincipal`** (que sigue rigiéndose únicamente por RF-052). Una pertenencia
  distinta y posterior de la misma persona (p. ej. tras cambiar de compañía) abre contextos nuevos y
  propios, no afectados por revocaciones previas.
- RF-063: Los registros afectados por la revocación automática (RF-061) DEBEN conservar su `FechaHoraInicio`
  original sin modificarla, NO DEBEN eliminarse físicamente, y DEBEN distinguir explícitamente: (a) fecha de
  inicio, (b) fecha de fin de vigencia, (c) estado actual, y (d) motivo de finalización. `Estado` es
  administrativo/informativo (refleja la disposición final del registro para auditoría y presentación),
  mientras que la vigencia efectiva para autorización se determina siempre comparando `FechaHoraInicio`/
  `FechaHoraFin` contra la fecha evaluada (Principio IV) — nunca por `Estado` de forma aislada. Cada
  registro afectado DEBE conservar una referencia a la `AsignaciónPersonaCompañía` que originó su
  revocación, para permitir auditar qué contextos, asignaciones de unidad organizativa y credenciales fueron
  afectados por una pertenencia específica.
- RF-064: Si el cese de una pertenencia se registra con una `FechaHoraFin` futura, los registros
  dependientes permanecen efectivamente vigentes hasta ese instante — el sistema NO DEBE esperar a que la
  fecha llegue para aplicar la revocación: DEBE propagar la misma `FechaHoraFin` a los registros
  dependientes en la misma operación en que se registra el cese (sin necesitar un proceso programado ni
  distinguir una fecha administrativa de una fecha efectiva separadas — `FechaHoraFin` ya representa la
  fecha efectiva). La propagación NUNCA debe extender una `FechaHoraFin` ya fijada a una fecha anterior en
  un registro dependiente.
- RF-065: La re-validación dinámica de la evaluación de acceso (RF-059) DEBE mantenerse como defensa
  adicional y NO ser reemplazada por la revocación automática (RF-061): el motor de autorización DEBE seguir
  considerando inválido cualquier contexto operativo cuya pertenencia de origen ya no esté vigente, incluso
  ante datos históricos inconsistentes.
- RF-066: La evaluación de acceso (RF-023, RF-059) DEBE verificar, además de los pasos ya establecidos, que
  exista una `AsignaciónCredencial` **vigente** (definida con precisión en RF-070) para la persona y la
  Compañía Principal determinada en el paso 4 del algoritmo (Historia 8), en la fecha evaluada; en su
  ausencia, el acceso DEBE denegarse por defecto (`SIN_CREDENCIAL_VIGENTE`), sin evaluar los pasos restantes
  del algoritmo. Esta verificación es condición necesaria, no suficiente: no sustituye la evaluación de
  permisos (RF-020 a RF-025). *(Nuevo — Sesión 2026-09-14, integración `ux-ui.md`; reemplaza la afirmación
  anterior de Historia 9 de que la credencial nunca determina el acceso por sí misma.)*
- RF-067: El sistema DEBE exponer una consulta agregada y transversal de auditoría (no limitada a una
  entidad individual), filtrable por rango temporal, usuario, persona, compañía, Compañía Principal, entidad
  y acción, respetando el alcance de compañías del usuario autenticado (RF-005). *(Nuevo — Sesión
  2026-09-14, integración `ux-ui.md`, Historia 10.)* **[DIFERIDA A ETAPA 2 — Sesión 2026-09-20, D8: queda
  fuera del alcance obligatorio del baseline de Etapa 1. El requisito se conserva íntegro como capacidad
  futura; no genera tareas de baseline ni contrato en esta etapa.]**
- RF-068: El sistema DEBE exponer una consulta transversal de los históricos de compañía de pertenencia,
  contexto operativo y unidad organizativa (Historia 5), filtrable por persona, compañía, Compañía
  Principal, entidad y tipo de evento, respetando el alcance de compañías del usuario autenticado (RF-005).
  *(Nuevo — Sesión 2026-09-14, integración `ux-ui.md`, Historia 5.)* **[DIFERIDA A ETAPA 2 — Sesión
  2026-09-20, D8: ver la anotación de RF-067.]**
- RF-069: El sistema DEBE exponer indicadores operativos agregados (conteos de personas/Contratistas/
  Principales activas, credenciales próximas a vencer, relaciones o pertenencias próximas a finalizar),
  respetando el alcance de compañías del usuario autenticado (RF-005). *(Nuevo — Sesión 2026-09-14,
  integración `ux-ui.md`.)* **[DIFERIDA A ETAPA 2 — Sesión 2026-09-20, D8: ver la anotación de RF-067.]**
- RF-070: `AsignaciónCredencial.FechaHoraFin` representa el fin de vigencia de la credencial. **[MATIZADA —
  ver Sesión 2026-09-14 "vigencia temporal jerárquica" en Clarifications: `FechaHoraFin` deja de ser
  nullable — RF-071 exige un valor real conocido desde la creación, `null` ya no representa vigencia
  indefinida]** Puede tener cualquier fecha/hora conocida al momento de la asignación (p. ej. una
  "Credencial Temporal" de corta duración, Historia 9, o una vigencia larga si negocio así la define), y la
  credencial permanece en Estado `ASIGNADO` incluso después de que esa fecha pase — el sistema NO DEBE
  transicionar automáticamente su `Estado` por el mero paso del tiempo. Una `AsignaciónCredencial` es
  **vigente** (satisface RF-066) si y solo si se cumplen conjuntamente: (a) `Estado = ASIGNADO`, y (b)
  `FechaHoraInicio <= fecha evaluada <= FechaHoraFin`. `Estado` sigue siendo administrativo/informativo
  (igual que para las demás entidades revocables, RF-063) y la revocación administrativa (cese/reemplazo de
  pertenencia — RF-061; devolución; baja lógica) sigue siendo el único mecanismo que cambia `Estado`,
  pudiendo acortar `FechaHoraFin` cuando corresponda (nunca extenderla) y conservando siempre
  `FechaHoraInicio` sin modificar; la expiración temporal (b) es una condición evaluada dinámicamente en
  cada evaluación de acceso, distinta e independiente de una revocación administrativa, y nunca escribe ni
  modifica ningún campo por sí sola. *(Sesión 2026-09-14, integración `ux-ui.md`, auditoría de consistencia
  RF-066 — corrigió data-model.md, que restringía incorrectamente `FechaHoraFin` a `null` mientras
  `Estado = ASIGNADO`; matizada en la sesión siguiente al eliminarse la nulabilidad por completo, RF-071.)*
- RF-071: Toda asociación temporal vinculada a una persona DEBE tener `FechaHoraInicio` y `FechaHoraFin`
  **obligatorias** desde su creación: `AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal`,
  `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial`, `AsignaciónTipoPersona`, y `PermisoAcceso`
  (los tres alcances — ya exigido de forma general por RF-021 desde el spec original, nunca reflejado
  correctamente en `data-model.md`, que lo documentaba como nullable). `FechaHoraFin` NO DEBE representarse
  con `null` ni con una fecha centinela (p. ej. `9999-12-31`); DEBE ser una fecha/hora real y conocida
  elegida en el momento de la creación de la asociación. No existe, para estas entidades, ningún estado de
  "vigencia indefinida/permanente". `RelaciónContratistaPrincipal` queda fuera del alcance de este
  requisito — no está vinculada a una persona (es una relación Compañía↔Compañía). *(Nuevo — Sesión
  2026-09-14, "vigencia temporal jerárquica".)*
- RF-072: `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y `AsignaciónCredencial`
  — las tres asociaciones que la cascada de RF-061 revoca por dependencia de una `AsignaciónPersonaCompañía`
  — DEBEN estar temporalmente contenidas dentro de la vigencia de la `AsignaciónPersonaCompañía` vigente de
  la persona en el momento de su creación: `FechaHoraInicio_hija >= FechaHoraInicio_pertenencia` **Y**
  `FechaHoraFin_hija <= FechaHoraFin_pertenencia`. Una asociación hija puede finalizar antes que la
  pertenencia (no se exige igualdad de `FechaHoraFin`), pero nunca puede iniciar antes que ella ni extenderse
  más allá de su fin. Esta contención se valida en el momento de creación/actualización de la asociación
  hija, contra la `AsignaciónPersonaCompañía` vigente en ese momento; es independiente del acortamiento que
  la cascada de RF-061/RF-064 ya aplica cuando la pertenencia se cierra después de que la hija existe (ambos
  mecanismos son complementarios, no redundantes). `AsignaciónTipoPersona` y `PermisoAcceso` NO están
  sujetas a esta contención — el modelo de dominio no las establece como dependientes de la pertenencia
  (RF-011: perfiles múltiples sin exclusividad; `PermisoAcceso` no está en la lista de entidades revocadas
  por RF-061 — Historia 8 las trata como configuración evaluada independientemente del histórico de
  pertenencia). *(Nuevo — Sesión 2026-09-14, "vigencia temporal jerárquica"; ver RF-048 reescrito.)*
- RF-073: `AsignaciónPersonaCompañía.FechaHoraFin` DEBE poder extenderse hacia una fecha posterior a la ya
  vigente mediante una operación explícita de **renovación**, distinta de la creación de una nueva
  `AsignaciónPersonaCompañía` y distinta del cierre por reemplazo/cese (RF-061, RF-064) — la renovación
  representa continuidad de la misma relación contractual, no una relación nueva, y NO dispara la cascada de
  revocación. La renovación: (a) solo aplica a una pertenencia en `Estado = ACTIVA` **y que además siga
  vigente dinámicamente en el momento de renovar** (`fecha actual <= FechaHoraFin` ya declarada) — una
  pertenencia `FINALIZADA`, o una `ACTIVA` cuya `FechaHoraFin` ya pasó (expiración dinámica sin cierre
  administrativo), NO es renovable en ningún caso: requiere una nueva `AsignaciónPersonaCompañía` (histórico
  secuencial, RF-014); la renovación nunca "puentea" retroactivamente un vacío temporal ya transcurrido;
  (b) NO modifica `FechaHoraInicio` (RF-063, sin cambios); (c) NO modifica `Estado` ni `MotivoFin`, ni crea
  ninguna asociación dependiente nueva; (d) las asociaciones dependientes ya existentes
  (`ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial`)
  conservan su propia `FechaHoraFin` sin cambios — la renovación solo amplía el techo temporal que RF-072
  permite para asociaciones futuras; (e) NUNCA puede usarse para acortar `FechaHoraFin` ni para modificarla
  hacia una fecha anterior — esa operación sigue siendo exclusiva de la cascada de cese/reemplazo; (f) queda
  registrada mediante los mecanismos de auditoría ya existentes (`UpdatedAt`/`UpdatedById`, RF-026/RF-027),
  sin necesitar un campo o entidad nueva. (g) **Clasificación del rechazo, normativa y única**: intentar
  renovar con una fecha que no sea estrictamente posterior a la vigente, o renovar algo que ya no está
  vigente, NO son errores de validación de entrada sino **conflictos con el estado actual del recurso**: la
  petición está bien formada y el rechazo depende de contra qué se compara. Esta clasificación es la misma
  para toda entidad que herede estas reglas de renovación —incluida `AsignaciónRolAdministrativo` por
  RF-075—, de modo que un mismo código de negocio no puede corresponder a categorías distintas según la
  entidad; la traducción de cada categoría a un código de estado HTTP concreto vive en `contracts/`, que es
  su fuente normativa. *(Nuevo — Sesión 2026-09-14, "renovación de
  AsignaciónPersonaCompañía", cierra Decisión Pendiente #9; punto (a) ampliado en la Sesión "cierre Decisión
  Pendiente #10" con la exigencia de vigencia dinámica; punto (g) añadido en la Sesión 2026-09-21 al cerrar
  los hallazgos documentales del gate — documenta la clasificación ya vigente, sin alterarla.)*
- RF-074: La administración de usuarios DEBE controlarse mediante un catálogo **cerrado** de roles
  administrativos con exactamente dos valores: `GLOBAL_ADMINISTRATOR` (alcance GLOBAL sobre todo el sistema)
  y `COMPANY_ADMINISTRATOR` (alcance limitado a una compañía específica). Incorporar un rol administrativo
  nuevo NO DEBE ser una operación de datos en tiempo de ejecución: exige una modificación explícita del
  modelo de autorización. La asignación de un rol a un `Usuario` DEBE modelarse como una entidad propia,
  `AsignaciónRolAdministrativo` (reemplaza a `AlcanceUsuarioCompañía`, RF-004), con `UsuarioId`, `Rol`,
  `CompañíaId` y vigencia temporal. **Regla fundamental**: `Rol = GLOBAL_ADMINISTRATOR` ⇒ `CompañíaId` DEBE
  ser nulo; `Rol = COMPANY_ADMINISTRATOR` ⇒ `CompañíaId` DEBE existir y referenciar una compañía válida. El
  alcance GLOBAL comprende todas las compañías del sistema sin enumerarlas, incluidas las creadas después de
  la asignación. *(Nuevo — Sesión 2026-09-20, D1.)*
- RF-075: Toda `AsignaciónRolAdministrativo` DEBE tener `FechaHoraInicio` y `FechaHoraFin` obligatorias desde
  su creación, nunca nulas ni expresadas con una fecha centinela — mismo principio que RF-071, aplicado por
  primera vez a una entidad vinculada a un `Usuario` y no a una `Persona`. **Única excepción**: la asignación
  `GLOBAL_ADMINISTRATOR` creada por el mecanismo de arranque inicial usa el valor `MAX_VALIDITY_DATE`
  (`2999-12-31T23:59:59Z`) como `FechaHoraFin`, conforme a la excepción explícita y acotada declarada en
  RF-078; esa excepción aplica exclusivamente a esa asignación sembrada y NO DEBE extenderse a ninguna otra
  asignación de rol. NO DEBEN existir asignaciones
  `COMPANY_ADMINISTRATOR` temporalmente solapadas para el mismo par (`UsuarioId`, `CompañíaId`); asignaciones
  consecutivas sin solapamiento sí son válidas. Un mismo usuario PUEDE tener varias asignaciones
  `COMPANY_ADMINISTRATOR` vigentes simultáneamente cuando correspondan a compañías distintas.
  `GLOBAL_ADMINISTRATOR` queda fuera de esa restricción de solapamiento (no tiene compañía asociada) y PUEDEN
  coexistir varios usuarios con ese rol simultáneamente. La extensión de la vigencia de una asignación DEBE
  seguir las reglas de renovación ya establecidas en RF-073, con una diferencia de modelo que debe hacerse
  explícita: `AsignaciónRolAdministrativo` **no tiene campo `Estado`** —a diferencia de
  `AsignaciónPersonaCompañía`, donde RF-073 sí puede distinguir `ACTIVA` de `FINALIZADA`—, de modo que aquí la
  renovabilidad se determina **exclusivamente por la vigencia temporal**: una asignación es renovable solo
  mientras `FechaHoraInicio <= ahora < FechaHoraFin` en el instante de renovar, y la nueva `FechaHoraFin` DEBE
  ser estrictamente posterior a la ya declarada. Una asignación cuya vigencia ya expiró —incluida aquella que
  se cerró anticipadamente fijando su `FechaHoraFin` al instante de la finalización— NO es renovable en ningún
  caso y exige una asignación nueva: la renovación nunca puentea retroactivamente un intervalo en el que el
  usuario no tuvo autorización. La renovación NUNCA
  modifica `Rol` ni `CompañíaId` de la asignación existente — para cambiar cualquiera de los dos se finaliza la
  asignación y se crea una distinta (RF-074) — y DEBE quedar expuesta como una operación administrativa propia,
  análoga a la ya existente para `AsignaciónPersonaCompañía` (RF-073), sujeta a las mismas restricciones de
  autorización que crear o finalizar esa misma asignación (RF-076) y al mismo régimen de alcance y
  ocultamiento de existencia que el resto de operaciones sobre `Usuario` (RF-077). *(Nuevo — Sesión 2026-09-20,
  D1; alcance de la operación de renovación aclarado explícitamente — Sesión 2026-09-20, cierre de la
  desviación D-1.)*
- RF-076: Un `COMPANY_ADMINISTRATOR` DEBE poder crear y administrar usuarios únicamente dentro de la compañía
  sobre la que tiene autorización administrativa vigente, y NO DEBE poder: administrar usuarios de otras
  compañías, reasignar usuarios fuera de su alcance, elevar su propio alcance, asignar el rol
  `GLOBAL_ADMINISTRATOR`, ni asignar `COMPANY_ADMINISTRATOR` para una compañía distinta de la suya. SÍ DEBE
  poder crear asignaciones `COMPANY_ADMINISTRATOR` para su propia compañía. Crear o modificar cualquier
  asignación de rol fuera de esos límites DEBE requerir un `GLOBAL_ADMINISTRATOR` vigente o el mecanismo de
  arranque inicial (RF-078). *(Nuevo — Sesión 2026-09-20, D1.)*
- RF-077: El alcance efectivo de un usuario autenticado DEBE resolverse a partir de sus
  `AsignaciónRolAdministrativo` vigentes: con una asignación `GLOBAL_ADMINISTRATOR` vigente, el alcance
  comprende todas las compañías; en caso contrario, comprende exactamente las compañías de sus asignaciones
  `COMPANY_ADMINISTRATOR` vigentes. La autorización se evalúa siempre sobre las asignaciones **vigentes del
  solicitante** en el instante de la operación, nunca sobre las del recurso destino ni sobre asignaciones ya
  expiradas. Un usuario autenticado que **no tenga ninguna asignación vigente** —porque nunca tuvo, porque
  todas expiraron o porque todas fueron finalizadas— NO tiene alcance alguno: no se le concede acceso parcial
  ni de solo lectura, y toda operación administrativa protegida DEBE rechazarse por denegación por defecto
  (Constitución, Principio I). Ese rechazo es distinto del `404` por recurso fuera de alcance que se describe
  más abajo: aquí el solicitante no está autorizado a operar en absoluto, con independencia del recurso, de
  modo que no hay existencia de recurso que ocultar. Toda operación DEBE verificar además que el **recurso concreto**
  pertenezca a ese alcance, no solo que el alcance no esté vacío; conocer o poseer el identificador de un
  recurso NO otorga autorización sobre él. Para una `Persona` —que no tiene una única compañía propietaria—
  el recurso se considera dentro del alcance si su compañía de pertenencia vigente está en el alcance del
  usuario **o** si tiene al menos un `ContextoOperativoPersonaPrincipal` vigente con una Compañía Principal
  del alcance del usuario. Las operaciones de **lectura** sobre recursos fuera del alcance DEBEN responder
  `404`, sin revelar la existencia del recurso; las de **escritura** DEBEN respetar el contrato específico de
  cada endpoint, sin asumir `404` automáticamente. Toda operación denegada por alcance —sea lectura o
  escritura— DEBE ser **libre de efectos**: la verificación de alcance precede a cualquier escritura, de modo
  que el recurso ajeno y todo lo que dependa de él quedan exactamente como estaban. Esto es especialmente
  exigible en las operaciones que, de haberse autorizado, habrían disparado lógica en cascada: una denegación
  que igualmente revocara, cerrara o modificara registros dependientes sería peor que un rechazo explícito,
  porque dejaría un efecto irreversible detrás de una respuesta que afirma que el recurso no existe. Cualquier operación de **búsqueda o filtrado** sobre un
  listado —incluida la búsqueda de usuarios de UX-22— DEBE aplicarse sobre el conjunto ya restringido al
  alcance del actor, nunca sobre un universo mayor, y DEBE evaluarse **antes** de la paginación: el resultado
  paginado refleja los elementos que coinciden con el criterio de búsqueda dentro de ese alcance, no solo los
  de la página actualmente cargada en el cliente. Esa composición es normativa y su orden no es
  intercambiable: **alcance → filtros → total → orden → página**. De ella se derivan cuatro reglas que DEBEN
  cumplirse y son objetivamente verificables:
  (a) **criterio de coincidencia** — la búsqueda de `Usuario` se resuelve por **subcadena sobre el correo**
  (coincidencia parcial en cualquier posición, no solo por prefijo), con el término de búsqueda recortado de
  espacios al inicio y al final; la comparación la resuelve el motor de base de datos y su sensibilidad a
  mayúsculas y acentos es la de la colación de la base, que la aplicación no normaliza. No existe búsqueda
  por nombre, fonética ni aproximada;
  (b) **combinación de filtros** — cuando se proporciona más de un filtro, todos DEBEN cumplirse
  conjuntamente (AND), nunca de forma alternativa, y todos se aplican antes de calcular el total y de paginar;
  (c) **ordenamiento** — el listado de usuarios se ordena por **correo ascendente**, y el orden se aplica
  después de los filtros y **antes** de la paginación, de modo que las páginas de un mismo resultado son
  disjuntas y reproducibles;
  (d) **ausencia de coincidencias** — una búsqueda o filtrado que no encuentra elementos DEBE responder con
  una **colección vacía** y un total de cero, conservando la estructura de página válida. NUNCA DEBE
  responder `404`: el `404` de este requisito significa exclusivamente "recurso individual fuera de alcance o
  inexistente", de modo que un filtro sin resultados y un recurso ajeno son situaciones distintas y no deben
  producir la misma respuesta. Esto es además lo que impide que la búsqueda sirva como oráculo de
  enumeración: un correo ajeno y un correo inexistente devuelven exactamente el mismo resultado vacío. *(Nuevo — Sesión 2026-09-20, D1 y D3; precisa RF-005,
  RF-049 y RF-060 sin reemplazarlos. Semántica de búsqueda/paginación aclarada explícitamente — Sesión
  2026-09-20, cierre de la desviación D-4.)*
- RF-078: El sistema DEBE crear automáticamente un primer usuario con rol `GLOBAL_ADMINISTRATOR` durante el
  arranque de la aplicación, mediante una rutina **idempotente** ejecutada después de aplicar las migraciones
  —no mediante datos sembrados en una migración—, y solo si no existe ya ninguna asignación
  `GLOBAL_ADMINISTRATOR`. Su identidad DEBE ser un correo electrónico válido (RF-001 sin cambios: no se
  introduce autenticación por nombre de usuario) provisto por configuración obligatoria del entorno. Su
  contraseña inicial DEBE provenirse de configuración segura o gestor de secretos del entorno de ejecución,
  NO DEBE estar incrustada en el código fuente, en archivos de configuración versionados, en las
  especificaciones ni en el repositorio, DEBE cumplir la política de contraseñas vigente sin excepción
  (Decisión #1) y DEBE exigir cambio obligatorio en el primer inicio de sesión. La vigencia inicial de esa
  asignación de rol usa el valor `MAX_VALIDITY_DATE` del sistema (`2999-12-31T23:59:59Z`), que constituye una
  **excepción explícita y acotada exclusivamente a esa asignación** frente a RF-071/RF-075: NO DEBE
  generalizarse a ninguna otra asignación de rol ni a ninguna entidad vinculada a una `Persona`. Esa
  asignación puede modificarse, renovarse o revocarse después por los mecanismos administrativos normales.
  Todo usuario creado por otro usuario de mayor nivel DEBE exigir igualmente el cambio de contraseña en su
  primer inicio de sesión. *(Nuevo — Sesión 2026-09-20, D2.)*
- RF-079: La evaluación de acceso DEBE verificar dinámicamente que las compañías involucradas tengan
  `Estado = ACTIVO`: tanto (a) la Compañía Principal propietaria del área evaluada, como (b) la compañía de
  pertenencia vigente de la persona. Si cualquiera de las dos está `INACTIVO`, el acceso DEBE denegarse por
  defecto con el motivo `COMPANIA_INACTIVA`, sin evaluar los pasos restantes (Historia 8, pasos 5 y 6). La
  inactivación de una compañía NO DEBE modificar, cerrar, finalizar ni revocar ninguna asignación, relación,
  credencial, permiso ni registro histórico asociado: no existe cascada de escritura, y el efecto sobre el
  acceso surge exclusivamente de esta verificación dinámica. Por lo mismo, el efecto es **inmediato y
  reversible**: al reactivar la compañía, el acceso se restablece automáticamente si las demás condiciones de
  autorización siguen siendo válidas. El cambio de `Estado` de la compañía DEBE quedar registrado por el
  mecanismo general de auditoría (RF-026, RF-027); la denegación en sí no escribe nada. *(Nuevo — Sesión
  2026-09-20, D4; cierra la Decisión Pendiente #6 para el caso de inactivación de compañía.)*
- RF-080: Cada Compañía Principal DEBE tener asociada una zona horaria propia, identificada mediante un
  identificador estándar IANA (p. ej. `America/Lima`, `America/Santiago`), obligatoria y validada cuando
  `TipoCompañía = PRINCIPAL_MANDANTE`; las compañías `CONTRATISTA` no la usan funcionalmente, al no poseer
  áreas de acceso, contextos operativos ni bloques horarios propios. Esa zona determina la interpretación de
  las fechas y horas ingresadas y presentadas, y la evaluación de los bloques horarios de los permisos de sus
  áreas (Historia 8, paso 13). Los timestamps DEBEN persistirse siempre como instante absoluto en UTC: la
  zona NO DEBE usarse para almacenar representaciones distintas del mismo instante, solo para convertir entre
  ese instante y la fecha/hora local. La evaluación temporal de vigencias DEBE seguir comparando instantes
  UTC. Para las asociaciones cuya vigencia no sea resoluble a una única Compañía Principal —una
  `AsignaciónPersonaCompañía` que referencia una compañía `CONTRATISTA`, y `AsignaciónTipoPersona`, que no
  está vinculada a ninguna compañía— rige la **zona horaria global de respaldo** del sistema. Cambiar la zona
  de una Compañía Principal NO DEBE modificar ni reinterpretar retrospectivamente los instantes UTC ya
  persistidos; sí cambia su representación local en consultas y presentaciones futuras, por lo que DEBE ser
  una operación controlada y auditable. La zona vigente es siempre la actualmente configurada: no se versiona
  históricamente. *(Nuevo — Sesión 2026-09-20, D5; resuelve la ambigüedad de calendario de RF-016.)*
- RF-081: El `TipoCompañía` de una compañía NO DEBE poder modificarse mientras existan dependencias de
  dominio incompatibles con el tipo destino. El sistema DEBE verificarlas antes de aceptar el cambio y, si
  existen, DEBE rechazar la operación informando qué dependencias deben resolverse previamente. NO DEBE
  realizarse ninguna eliminación, cierre, revocación ni modificación automática o masiva de esas dependencias
  como consecuencia del cambio de tipo. Se consideran dependencias relevantes, como mínimo: (a) áreas de
  acceso asociadas a la compañía; (b) raíces de unidad organizativa y estructuras organizativas que dependan
  de ella; (c) `RelaciónContratistaPrincipal` vigentes incompatibles con el tipo destino, evaluadas de forma
  **simétrica en ambas direcciones** —una compañía `CONTRATISTA` con relaciones vigentes como Contratista
  queda igualmente bloqueada para pasar a `PRINCIPAL_MANDANTE`— y contando **únicamente** registros reales de
  esa entidad, nunca `AsignaciónPersonaCompañía` ni personas, porque `RelaciónContratistaPrincipal` es
  exclusivamente una relación Compañía↔Compañía y una persona empleada directamente por una Principal se
  modela vía `AsignaciónPersonaCompañía` con su contexto operativo fijado automáticamente (RF-053), nunca
  como una relación de una compañía consigo misma; (d) contextos operativos asociados a la compañía; (e)
  credenciales cuya pertenencia o contexto dependa de ella. El cambio exitoso DEBE quedar registrado por el
  mecanismo general de auditoría (RF-026, RF-027). *(Nuevo — Sesión 2026-09-20, D6.)*

## Entidades Principales

- Usuario
- HistorialContraseña
- AsignaciónRolAdministrativo (asignación temporal y auditable de un rol administrativo —
  `GLOBAL_ADMINISTRATOR` o `COMPANY_ADMINISTRATOR` — a un Usuario, con alcance GLOBAL o por compañía;
  reemplaza a `AlcanceUsuarioCompañía` desde la Sesión 2026-09-20 — RF-074 a RF-077)
- Compañía (clasificada como PRINCIPAL_MANDANTE o CONTRATISTA)
- RelaciónContratistaPrincipal (relación vigente entre una Compañía CONTRATISTA y una o varias Compañías
  PRINCIPAL_MANDANTE)
- UnidadOrganizativa (sin relación directa hacia Compañía)
- CompañíaPrincipalUnidadOrganizativaRaiz (enlace exclusivo de nodos raíz de UnidadOrganizativa con su
  Compañía Principal propietaria)
- TipoPersona
- Persona
- AsignaciónTipoPersona
- AsignaciónPersonaCompañía (histórico de compañía de pertenencia/empleadora; su cierre dispara revocación
  automática en cascada — RF-061)
- ContextoOperativoPersonaPrincipal (relación operativa vigente entre una Persona y una Compañía Principal;
  una persona puede tener varios contextos simultáneos; revocable automáticamente — RF-061)
- AsignaciónPersonaUnidadOrganizativa (dentro de un contexto operativo; revocable automáticamente — RF-061)
- ÁreaAcceso (pertenece a exactamente una Compañía Principal)
- ÁreaAccesoTipoPersona
- PermisoAcceso
- BloqueHorarioPermiso
- TipoCredencial (tipo/diseño visual de credencial, no tecnología de identificación física)
- AsignaciónCredencial (dentro del contexto de una Compañía Principal; revocable automáticamente, estado
  REVOCADA — RF-061)
- TipoDocumento
- TipoSangre
- Género

## Criterios de Éxito

- CS-001: Inicio de sesión y carga del alcance en menos de 10 segundos en condiciones normales.
- CS-002: Búsqueda de personas p95 < 2 segundos para al menos 100,000 personas.
- CS-003: Evaluación de acceso p95 < 500 ms.
- CS-004: 100% de endpoints protegidos deben aplicar autenticación y alcance.
- CS-005: 100% de operaciones de creación/actualización deben generar auditoría automáticamente.
- CS-006: 100% de operaciones jerárquicas deben impedir ciclos.
- CS-007: 100% de históricos deben permitir reconstrucción temporal.
- CS-008: Las historias P1 deben tener pruebas automatizadas antes de liberar.
- CS-009: Un usuario debe poder crear una jerarquía de tres niveles y configurar un permiso horario.
- CS-010: Una Compañía Principal puede tener múltiples árboles de Unidad Organizativa (varios nodos raíz)
  simultáneamente.
- CS-011: Dos Compañías Principales diferentes nunca comparten Unidades Organizativas.
- CS-012: Una Compañía Contratista puede estar relacionada simultáneamente con múltiples Compañías
  Principales mediante `RelaciónContratistaPrincipal`.
- CS-013: Una persona perteneciente a una Compañía Contratista puede tener simultáneamente contextos
  operativos vigentes con múltiples Compañías Principales.
- CS-014: Una persona puede tener una Unidad Organizativa diferente para cada Compañía Principal con la que
  tenga contexto operativo vigente.
- CS-015: Una persona puede tener permisos diferentes para cada Compañía Principal con la que tenga
  contexto operativo vigente.
- CS-016: Una persona puede tener una credencial diferente para cada Compañía Principal con la que tenga
  contexto operativo vigente.
- CS-017: Una persona puede tener credenciales vigentes simultáneamente para dos Compañías Principales
  diferentes.
- CS-018: El acceso a un área de la Compañía Principal A nunca puede ser otorgado por un permiso
  correspondiente a la Compañía Principal B.
- CS-019: Una persona perteneciente a una Compañía Contratista solamente puede abrir un contexto operativo
  con una Compañía Principal si existe una `RelaciónContratistaPrincipal` vigente entre su compañía y esa
  Principal.
- CS-020: Una persona perteneciente directamente a una Compañía Principal utiliza automáticamente esa
  Principal como su contexto operativo, sin poder seleccionar una Principal arbitraria.
- CS-021: El árbol mostrado en la pantalla de asignación de Unidad Organizativa para una persona de una
  Compañía Contratista corresponde exclusivamente a la Compañía Principal seleccionada.
- CS-022: La asignación de una credencial requiere obligatoriamente una Compañía Principal y un
  TipoCredencial.
- CS-023: `TipoCredencial` representa el diseño/tipo visual de la credencial, nunca una tecnología de
  identificación física (RFID, QR, NFC, código de barras).
- CS-024: Una credencial emitida en el contexto de la Compañía Principal A no puede utilizarse para
  satisfacer las reglas de contexto o autorización de la Compañía Principal B.
- CS-025: Cuando finaliza la pertenencia de una persona a su compañía, todos sus contextos operativos,
  asignaciones de unidad organizativa y credenciales vigentes que dependan de esa pertenencia quedan
  revocados automáticamente, sin intervención manual adicional.
- CS-026: Ningún registro histórico se elimina físicamente ni cambia su `FechaHoraInicio` como consecuencia
  de la revocación automática.
- CS-027: Una persona que trabaja simultáneamente para varias Compañías Principales pierde el acceso
  derivado de **todas ellas** cuando termina la única pertenencia (Contratista) de la que dependían, pero
  conserva sin cambios cualquier contexto que dependa de una pertenencia distinta y posterior.
- CS-028: Un cese de pertenencia registrado con fecha de fin futura mantiene los accesos derivados vigentes
  hasta esa fecha, y los revoca automáticamente a partir de ese instante, sin requerir un proceso programado
  separado.
- CS-029: La re-validación dinámica de la evaluación de acceso sigue denegando el acceso derivado de una
  pertenencia no vigente, incluso en un escenario hipotético donde la revocación en cascada no se hubiera
  aplicado correctamente sobre algún registro.
- CS-030: Ninguna regla del sistema limita el número de Compañías Principales distintas para las que una
  persona puede tener `ContextoOperativoPersonaPrincipal` vigentes simultáneamente — el único límite es que
  cada una de esas Principales debe estar sustentada legítimamente por la compañía de pertenencia vigente
  de la persona (directamente, o vía `RelaciónContratistaPrincipal` vigente si es una Contratista). En
  particular, RF-014 (máximo una compañía de pertenencia activa) NO impone ningún límite al número de
  Principales simultáneas.
- CS-031: El acceso se deniega si la persona no tiene una `AsignaciónCredencial` vigente en Estado
  `ASIGNADO` para la Compañía Principal propietaria del área evaluada, incluso si el resto de las reglas de
  Historia 8 (contexto operativo, área activa, perfil, unidad organizativa, permiso, vigencia, horario) se
  cumplen.
- CS-032: Las consultas agregadas/transversales de auditoría, históricos e indicadores operativos (Historia
  10) respetan siempre el alcance de compañías del usuario autenticado — ningún usuario puede observar,
  mediante estas vistas, actividad de una compañía fuera de su alcance. **[DIFERIDO A ETAPA 2 — Sesión
  2026-09-20, D8: describe una propiedad de RF-067 a RF-069, diferidos junto con ellos. No es verificable en
  el baseline de Etapa 1 por ausencia deliberada de la capacidad, no por incumplimiento.]**
- CS-033: Ninguna asociación temporal vinculada a una persona (`AsignaciónPersonaCompañía`,
  `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`, `AsignaciónCredencial`,
  `AsignaciónTipoPersona`, `PermisoAcceso`) puede crearse con `FechaHoraFin` nula o con una fecha centinela;
  siempre requiere una fecha/hora real y conocida desde la creación (RF-071).
- CS-034: Dada una `AsignaciónPersonaCompañía` con `FechaHoraInicio = 01/08/2026` y
  `FechaHoraFin = 31/07/2027`, una `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa`
  o `AsignaciónCredencial` dependiente de ella: (a) con `FechaHoraFin = 31/03/2027` es válida; (b) con
  `FechaHoraFin` igual a `31/07/2027` (la misma fecha que la pertenencia) es válida; (c) con
  `FechaHoraFin = 01/08/2027` es inválida (excede la vigencia de la pertenencia); (d) con
  `FechaHoraInicio = 01/07/2026` (anterior al inicio de la pertenencia) es inválida (RF-072).
- CS-035: Renovar una `AsignaciónPersonaCompañía` extendiendo su `FechaHoraFin` de `31/07/2027` a
  `31/01/2028` no modifica `FechaHoraInicio`, no modifica `Estado` ni `MotivoFin`, y no cambia la
  `FechaHoraFin` de ningún `ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` o
  `AsignaciónCredencial` ya existente de esa persona; solo amplía el techo temporal permitido (RF-072) para
  asociaciones creadas después de la renovación (RF-073).
- CS-036: Un usuario cuyo único rol vigente es `COMPANY_ADMINISTRATOR` sobre una compañía no puede, en ninguna
  operación, listar ni modificar usuarios de otra compañía, asignarse a sí mismo un alcance mayor, ni crear
  una asignación `GLOBAL_ADMINISTRATOR`; sí puede crear otra asignación `COMPANY_ADMINISTRATOR` para su propia
  compañía (RF-074, RF-076).
- CS-037: Una lectura fuera del alcance del usuario autenticado responde `404` y no revela la existencia del
  recurso, incluso cuando el identificador es correcto y conocido (RF-077). El criterio se considera
  satisfecho cuando esa propiedad queda verificada sobre los **siete tipos de recurso** que el sistema expone
  con identificador propio: (1) `Usuario`, (2) `Compañía`, (3) `UnidadOrganizativa`, (4) `ÁreaAcceso`,
  (5) `Persona`, (6) `ContextoOperativoPersonaPrincipal` y (7) `AsignaciónCredencial`. La enumeración es
  **cerrada y exhaustiva**: "fuera del alcance" no admite lectura como cobertura parcial o discrecional, y
  cualquier tipo de recurso nuevo que se exponga con identificador propio queda sujeto a este mismo criterio
  desde su incorporación. Las **operaciones y proyecciones que no tienen recurso propio** —el estado efectivo
  de una persona, que es una proyección suya, y la cascada de revocación, que es un efecto interno de
  finalizar una pertenencia— no constituyen tipos de recurso adicionales: heredan el alcance del recurso a
  través del cual se invocan y DEBEN aplicar el mismo control server-side, de modo que tampoco son
  alcanzables cuando ese recurso está fuera del alcance del solicitante. *(Enumeración explícita — Sesión
  2026-09-21; precisa el criterio sin ampliarlo, conforme a la cobertura verificada en T237, T238 y T242.)*
- CS-038: Un despliegue desde cero, sin ningún usuario en la base de datos, queda operable tras el primer
  arranque: existe exactamente un `GLOBAL_ADMINISTRATOR` creado automáticamente, con cambio de contraseña
  obligatorio pendiente; reiniciar la aplicación no crea un segundo (RF-078).
- CS-039: Inactivar una Compañía Principal deniega inmediatamente el acceso a sus áreas con motivo
  `COMPANIA_INACTIVA` sin alterar ningún contexto operativo, credencial ni permiso existente, y reactivarla
  restablece el acceso sin ninguna otra intervención (RF-079).
- CS-040: Dos Compañías Principales con zonas horarias IANA distintas evalúan el mismo instante UTC contra sus
  propios bloques horarios locales, de forma independiente entre ellas; cambiar la zona de una no altera
  ningún instante ya persistido (RF-080).
- CS-041: Intentar cambiar el `TipoCompañía` de una compañía que tiene áreas de acceso, raíces de unidad
  organizativa, relaciones Contratista↔Principal vigentes, contextos operativos o credenciales dependientes se
  rechaza informando las dependencias a resolver, y ninguna de ellas se modifica; la misma operación sobre una
  compañía sin dependencias se acepta en ambas direcciones (RF-081).

## Supuestos

- Aplicación web con frontend y backend API separados.
- Base de datos relacional.
- Stack tecnológico ratificado (decisión arquitectónica del usuario, Sesión 2026-09-14): .NET 10, ASP.NET
  Core 10, Entity Framework Core 10, SQL Server, React + TypeScript (ver plan.md, Technical Context, para el
  detalle completo — este documento se mantiene funcional y no se amplía con detalles de implementación).
- Perú es el locale inicial.
- Los timestamps persistidos utilizan UTC.
- Las reglas de negocio de horario se evalúan en la zona horaria de la Compañía Principal correspondiente
  (`Compañía.ZonaHorariaIana`, RF-080), con una zona global de respaldo del sistema para los casos no
  resolubles a una única Principal. *(Antes de la Sesión 2026-09-20 este supuesto fijaba `America/Lima` para
  todo el sistema; Perú sigue siendo el locale inicial y el valor de respaldo por defecto.)*
- Una persona puede tener múltiples tipos/perfiles, salvo que negocio defina lo contrario.
- Los permisos iniciales son concesiones; el modelo de denegación explícita queda para una fase posterior.
- El ID interno es UID/UUID y no se muestra en pantallas normales.
- La autenticación multifactor (MFA) queda fuera de alcance en esta fase; el inicio de sesión usa
  únicamente correo electrónico y contraseña, con bloqueo por intentos fallidos y expiración periódica
  de contraseña.
- El sistema soporta una o varias Compañías Principales/Mandantes; cada una constituye su propio contexto
  organizacional y operacional aislado (unidades organizativas y áreas de acceso propias, pudiendo tener
  varios árboles/nodos raíz de unidad organizativa).
- Una Compañía Contratista puede prestar servicios, de forma simultánea o en distintos períodos, a una o
  varias Compañías Principales. **Esta relación SÍ se modela como una entidad explícita a nivel de
  compañía** (`RelaciónContratistaPrincipal`, con vigencia temporal propia — corrección de la Sesión
  2026-09-14 "Contexto Operativo", que reemplaza la decisión anterior de no modelarla). Adicionalmente, la
  relación operativa entre una persona y una Compañía Principal específica se captura mediante
  `ContextoOperativoPersonaPrincipal`, independiente del histórico de compañía de pertenencia de la persona
  y admitiendo múltiples contextos simultáneos.
- Una persona puede tener, simultáneamente, unidad organizativa, permisos y credencial distintos e
  independientes para cada Compañía Principal con la que tenga contexto operativo vigente.

## Decisiones Pendientes

1. ~~Umbrales concretos de política de contraseña: número de intentos fallidos antes de bloqueo, período
   de expiración y flujo de recuperación (MFA descartado para esta fase; ver Clarifications).~~ **Resuelto
   (Sesión 2026-09-20, D9)**: la política de contraseñas actualmente configurada queda como **autoridad
   única y definitiva** del baseline — longitud mínima, exigencia de mayúscula/minúscula/dígito, umbral de
   intentos fallidos para bloqueo, período de expiración e historial no reutilizable son los ya
   parametrizados. NO se introducen umbrales especiales para el usuario de arranque (RF-078): su contraseña
   cumple exactamente esta misma política. NO se incorpora al baseline ningún mecanismo adicional de
   recuperación de contraseña ni ninguna política de caducidad distinta de la ya definida.
2. ~~Si una credencial necesita un identificador físico adicional: número, código de barras, QR, UID,
   etc.~~ **Resuelto (Sesión 2026-09-14, corrección Contexto Operativo)**: NO. `TipoCredencial` es
   únicamente tipo/diseño visual; no se incorpora ningún identificador físico ni tecnología de
   identificación salvo que negocio lo solicite explícitamente en el futuro (RF-058).
3. ~~Requisitos legales de privacidad y retención de información.~~ **Resuelto (Sesión 2026-09-20, D9)**: el
   baseline NO implementa ninguna política específica de retención legal ni eliminación automática basada en
   períodos no definidos. Los datos y registros existentes se conservan conforme al comportamiento actual del
   sistema, que ya prohíbe la eliminación física de historial relevante para trazabilidad (Principio IV de la
   Constitución). Definir períodos concretos de retención y eliminación legal queda **fuera del alcance
   funcional del baseline** y requerirá una decisión específica antes de implementar esa funcionalidad.
4. ~~Al cambiar la compañía vigente de una persona (nueva `AsignaciónPersonaCompañía`), ¿deben cerrarse
   automáticamente sus contextos operativos, asignaciones de unidad organizativa y credenciales vigentes, o
   permanecen abiertos hasta su cierre manual o vencimiento natural de su propia vigencia?~~ **Resuelto —
   corregido en Sesión 2026-09-14 "corrección Revocación Automática"** (reemplaza la resolución previa de la
   Sesión "auditoría final de consistencia", que solo exigía re-validación dinámica): SÍ deben revocarse
   automáticamente en cascada — es obligatorio, no opcional. Ver RF-061 a RF-065 y Historia 5, "Revocación
   automática por cese de pertenencia".
5. ~~¿Debe poder declararse más de un `ContextoOperativoPersonaPrincipal` simultáneo entre la misma persona y
   la misma Principal en algún escenario (p. ej. dos roles distintos con la misma Principal), o la
   exclusividad por par (Persona, Principal) es siempre correcta?~~ **Resuelto (Sesión 2026-09-14, auditoría
   final de consistencia)**: NO; la exclusividad estricta por par `(PersonaId, CompañíaPrincipalId)` de
   RF-052 se confirma como decisión final. Ningún caso de negocio establecido requiere múltiples contextos
   simultáneos con la misma Principal.
6. ~~¿Debe aplicarse la misma revocación automática en cascada cuando finaliza una
   `RelaciónContratistaPrincipal` (en vez de una pertenencia Persona–Compañía), o cuando una `Compañía` es
   marcada `INACTIVO` administrativamente (afectando potencialmente a todo su personal a la vez)?~~
   **Resuelto para la inactivación de compañía (Sesión 2026-09-20, D4)**: NO se aplica cascada de escritura.
   Se adopta la denegación por **evaluación dinámica**: la evaluación de acceso comprueba
   `Compañía.Estado = ACTIVO` tanto de la Principal propietaria del área como de la compañía de pertenencia
   vigente de la persona, y deniega con `COMPANIA_INACTIVA` sin modificar ningún registro dependiente — efecto
   inmediato y reversible (RF-079, Historia 8 pasos 5 y 6, research.md §30). **Nota importante**: hasta esa
   sesión, tanto este documento como `research.md` §14.5 afirmaban que la re-validación dinámica ya cubría
   este caso; era incorrecto — el motor de evaluación nunca consultaba `Compañía.Estado`. RF-079 es lo que
   convierte esa afirmación en comportamiento exigido. **El caso del fin de una `RelaciónContratistaPrincipal`
   sigue abierto** y permanece protegido únicamente por la re-validación dinámica ya existente
   (RF-059/RF-065), sin cascada de escritura equivalente a RF-061; extender ese mecanismo requeriría una
   decisión de negocio explícita adicional.
7. ~~**(Nueva — Sesión 2026-09-14, integración `ux-ui.md`)** RF-066 hace que la evaluación de acceso (Historia
   8, P1) dependa funcionalmente de que exista una `AsignaciónCredencial` vigente (Historia 9, actualmente
   P2)... ¿Debe elevarse la prioridad de Historia 9 a P1?~~ **Resuelto (Sesión 2026-09-20, D9)**: Historia 9
   **mantiene la prioridad P2**. La decisión es documental y de priorización, sin impacto técnico sobre el
   baseline: no modifica el modelo de dominio, la autorización, la API, la persistencia ni los criterios de
   cierre de Etapa 1, y no genera tareas de implementación. La dependencia funcional que RF-066 introdujo ya
   quedó satisfecha en la práctica, porque la funcionalidad de credenciales de Historia 9 se construyó dentro
   del baseline con independencia de su etiqueta de prioridad.
8. ~~`AsignaciónPersonaCompañía`, `ContextoOperativoPersonaPrincipal` y `AsignaciónPersonaUnidadOrganizativa`
   conservan en `data-model.md` la misma redacción de campo que `AsignaciónCredencial` tenía antes de la
   sesión de auditoría de RF-066 ("`FechaHoraFin`... `null` mientras esté vigente"). ¿Tienen la misma
   necesidad de permitir `FechaHoraFin` con valor mientras están vigentes?~~ **Resuelto dos veces
   (Sesión 2026-09-14): primero** en la sesión de cierre de Decisión Pendiente #8, que concluyó que NO era
   necesario extender la semántica de `AsignaciónCredencial` a las otras tres (sus contratos de creación
   estructuralmente lo impedían). **Después, REEMPLAZADO por una decisión de negocio más amplia** en la
   Sesión "vigencia temporal jerárquica": `FechaHoraFin` deja de ser nullable **para las seis entidades**
   vinculadas a una persona (no solo para las tres originalmente en duda) — ya no es una pregunta de "permitir
   valor mientras vigente", sino de exigir siempre un valor real desde la creación, sin `null` como opción.
   Ver RF-071, RF-072.
9. ~~¿Puede extenderse hacia adelante la `FechaHoraFin` de una `AsignaciónPersonaCompañía` ya creada (p. ej.
   renovación de un contrato cuyo plazo se amplía), o toda extensión de vigencia requiere necesariamente
   crear una nueva `AsignaciónPersonaCompañía`?~~ **Resuelto (Sesión 2026-09-14, "renovación de
   AsignaciónPersonaCompañía")**: SÍ, mediante una operación explícita de **renovación**, distinta del
   reemplazo/cese, que solo extiende `FechaHoraFin` hacia una fecha posterior, sin crear una nueva
   `AsignaciónPersonaCompañía`, sin modificar `FechaHoraInicio` ni `Estado`, y sin afectar a las asociaciones
   dependientes ya existentes. Ver RF-073.
10. ~~Las reglas de renovación (RF-073) especifican que la nueva `FechaHoraFin` debe ser posterior a la ya
    vigente, pero no especifican si esto incluye el caso en que la pertenencia ya expiró **dinámicamente**
    antes de renovarse (`Estado = ACTIVA` pero `FechaHoraFin` ya anterior a la fecha actual)?~~ **Resuelto
    (Sesión 2026-09-14, "cierre Decisión Pendiente #10")**: NO. La renovación exige, además de `Estado =
    ACTIVA`, que la pertenencia siga **vigente dinámicamente** en el momento de renovar (`fecha actual <=
    FechaHoraFin` ya declarada). Si `FechaHoraFin` ya pasó — aunque `Estado` continúe `ACTIVA` porque nadie
    la cerró administrativamente —, la pertenencia NO es renovable: se rechaza, y DEBE crearse una nueva
    `AsignaciónPersonaCompañía` (histórico secuencial, RF-014) en su lugar. Esto ya estaba soportado sin
    cambios adicionales: al no haber solapamiento posible entre una pertenencia dinámicamente expirada y una
    nueva que inicia después, la anterior no se modifica (RF-063, "Validaciones clave" de
    `AsignaciónPersonaCompañía` en data-model.md). Ver RF-073 (ampliado).
