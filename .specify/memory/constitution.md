# Constitución: Control de Acceso Empresarial

<!--
Informe de impacto de sincronización
- Cambio de versión: 1.0.0 → 1.1.0 (MINOR: nuevos principios explícitos + reorganización sin pérdida de obligaciones)
- Modificados:
  - "I. Seguridad Primero" → "I. Seguridad Server-Side, Denegación por Defecto y Autorización por Compañías"
    (se integra explícitamente el alcance de autorización por compañías)
  - "II. Integridad del Dominio y Vigencia Temporal" → "IV. Integridad Temporal e Históricos"
    (se explicitan permisos temporales y bloques horarios)
  - "III. Auditoría por Defecto" → "III. Auditoría Automática y Trazabilidad"
    (se fusiona la trazabilidad explícita del cambio)
  - "IV. Modelado Explícito del Dominio" → "VI. Modelado Explícito del Dominio"
    (se explicitan perfiles de persona y credenciales/fotocheck)
  - "V. Consistencia entre API e Interfaz" → trasladado a "Reglas de Arquitectura e Ingeniería"
    (deja de ser principio nuclear independiente, se conserva como regla de ingeniería)
  - "VI. Entrega Incremental y Verificable" → "VII. Pruebas Automatizadas Obligatorias (NO NEGOCIABLE)"
    (se eleva a no-negociable)
- Agregados:
  - "II. Identificadores Únicos Autogenerados (UID/UUID)" (principio nuclear nuevo)
  - "V. Jerarquías sin Ciclos" (principio nuclear nuevo, aplica a unidades organizativas y áreas de acceso)
- Eliminados: ninguna obligación previa fue eliminada
- Pendiente (histórico): TODO(TECH_STACK) — quedó resuelto en la Sesión 2026-09-14 ("Stack Tecnológico
  Oficial"): el usuario ratificó explícitamente .NET 10 + C# + ASP.NET Core 10 + EF Core 10 + SQL Server +
  LINQ como decisión arquitectónica no negociable. El detalle técnico completo vive en
  `specs/001-control-acceso-empresarial/plan.md` (Technical Context) y `research.md` (§15-§21) — no se
  duplica aquí; ver el bullet correspondiente en "Reglas de Arquitectura e Ingeniería" más abajo (PATCH
  1.1.0 → 1.1.1: aclaración de redacción, ninguna obligación de los principios cambia).
-->

## Principios Fundamentales

### I. Seguridad Server-Side, Denegación por Defecto y Autorización por Compañías

Toda autenticación, autorización, credencial/fotocheck, área de acceso y decisión de permisos DEBE
ser evaluada y aplicada en el servidor. El cliente NO DEBE considerarse una frontera de seguridad;
cualquier validación mostrada en la interfaz es solo una ayuda de experiencia de usuario y NUNCA
sustituye la validación del backend. Cuando el sistema no pueda determinar de forma inequívoca un
permiso válido, DEBE aplicarse denegación por defecto. Toda operación de lectura o escritura DEBE
evaluarse dentro del alcance de las compañías que el usuario autenticado tiene habilitadas para
administrar; un usuario NUNCA DEBE poder leer, modificar o listar datos de una compañía fuera de su
alcance autorizado, incluso si conoce el identificador del recurso.

**Rationale**: el sistema gestiona acceso físico y credenciales; un fallo de autorización o una
validación solo en cliente puede traducirse directamente en acceso físico indebido a instalaciones.

### II. Identificadores Únicos Autogenerados (UID/UUID)

Toda entidad persistente (personas, compañías, unidades organizativas, áreas físicas de acceso,
perfiles de persona, permisos temporales, bloques horarios, credenciales/fotocheck) DEBE
identificarse mediante un UID/UUID generado por el sistema en el momento de su creación. Estos
identificadores NO DEBEN ser editables por el usuario final, NO DEBEN reutilizarse tras una
eliminación lógica, y NO DEBEN derivarse ni depender de atributos de negocio mutables (nombres,
códigos, documentos de identidad, etc.).

**Rationale**: separar la identidad técnica de los atributos de negocio evita colisiones, sostiene
la trazabilidad estable en auditoría e históricos, y evita enumeración de recursos mediante
identificadores secuenciales o predecibles.

### III. Auditoría Automática y Trazabilidad

Toda entidad de negocio persistente DEBE registrar automáticamente metadatos de auditoría: fecha de
creación, fecha de última actualización, usuario de creación e usuario de última actualización.
Estos campos DEBEN ser generados y mantenidos exclusivamente por el servidor, nunca enviados o
aceptados desde el cliente, y NO DEBEN exponerse como campos editables en los formularios de
mantenimiento. Toda alteración relevante sobre permisos, credenciales, asignaciones jerárquicas y
datos de acceso DEBE quedar registrada de forma que sea posible reconstruir de manera fiable quién
realizó cada cambio, cuándo lo hizo y sobre qué entidad.

**Rationale**: en un sistema de control de acceso empresarial, la capacidad de reconstruir el
historial de cambios es un requisito de cumplimiento y de investigación de incidentes, no una
característica opcional.

### IV. Integridad Temporal e Históricos

Los permisos temporales, bloques horarios, asignaciones de compañía, asignaciones de unidad
organizativa y asignaciones de credenciales/fotocheck DEBEN modelarse con fecha/hora de inicio y fin
de vigencia explícitas. El sistema NO DEBE permitir estados temporales contradictorios: solapamientos
inválidos entre vigencias del mismo tipo, fin de vigencia anterior al inicio, o vigencias que excedan
el rango de vigencia del recurso padre del que dependen. Los registros históricos DEBEN conservarse
íntegros y disponibles para auditoría; NO DEBE realizarse eliminación física de historial relevante
para trazabilidad. Toda fecha/hora DEBE persistirse en UTC y convertirse a la zona horaria empresarial
configurada únicamente en la capa de presentación.

**Rationale**: los permisos de acceso son inherentemente temporales (turnos, bloques horarios,
vigencias contractuales); un error de integridad temporal puede otorgar u ocultar acceso físico fuera
de la ventana autorizada.

### V. Jerarquías sin Ciclos

Las unidades organizativas y las áreas físicas de acceso se modelan como jerarquías padre-hijo
navegables en árbol. El sistema DEBE impedir, mediante validación server-side, la creación de
referencias circulares: un nodo NO puede ser ancestro de sí mismo, ni directa ni indirectamente. Toda
operación de creación, reubicación o reasignación de un nodo dentro de la jerarquía DEBE validar la
ausencia de ciclos antes de confirmarse, y DEBE rechazarse con un error explícito si se detecta un
ciclo potencial.

**Rationale**: los permisos y las áreas de acceso se heredan o evalúan recorriendo el árbol; un ciclo
no detectado puede producir recursión infinita, bloqueos, o evaluaciones de permisos incorrectas.

### VI. Modelado Explícito del Dominio

Personas, compañías, unidades organizativas, áreas físicas de acceso, perfiles de persona y
credenciales/fotocheck DEBEN modelarse como entidades y relaciones explícitas, nunca como campos
libres, texto no estructurado o combinaciones implícitas de otros campos. Los perfiles de persona
DEBEN determinar de forma explícita qué plantillas de permisos y áreas de acceso les aplican. Las
credenciales/fotocheck DEBEN vincularse de forma trazable a una única persona y a su vigencia
temporal correspondiente. Los datos maestros DEBEN validarse contra catálogos controlados y
versionados.

**Rationale**: un dominio de control de acceso empresarial con jerarquías, perfiles y credenciales
requiere un modelo relacional explícito para que las reglas de autorización sean verificables y
auditables.

### VII. Pruebas Automatizadas Obligatorias (NO NEGOCIABLE)

Toda funcionalidad relacionada con seguridad, autorización por compañías, reglas temporales,
jerarquías, permisos, credenciales/fotocheck y auditoría DEBE contar con pruebas automatizadas
(unitarias, de integración, de contrato y/o end-to-end según corresponda) antes de considerarse
completa. Cada historia de usuario DEBE poder verificarse de forma independiente mediante pruebas
automatizadas, con especial cobertura de casos de denegación por defecto, ciclos jerárquicos,
solapamientos temporales inválidos y fugas de datos entre compañías.

**Rationale**: dado que los defectos en este dominio se traducen en accesos físicos indebidos o en
pérdida de trazabilidad, la verificación manual por sí sola es insuficiente como red de seguridad.

## Reglas de Arquitectura e Ingeniería

- Preferir arquitectura web modular separando presentación, aplicación, dominio e infraestructura.
- La interfaz web y el backend DEBEN utilizar contratos explícitos; toda validación mostrada en la
  interfaz también DEBE aplicarse en el backend (ver Principio I). Las respuestas de API NO DEBEN
  exponer datos internos innecesarios.
- Utilizar transacciones para operaciones que modifiquen históricos relacionados.
- Evitar eliminación física de información histórica necesaria para auditoría.
- Persistir fechas/horas en UTC y convertirlas en la interfaz a la zona horaria empresarial
  configurada.
- Utilizar UID/UUID generados por el sistema; nunca son editables (ver Principio II).
- Los datos maestros para Perú DEBEN provenir de un catálogo versionado y validado.
- Las contraseñas NUNCA deben almacenarse en texto plano.
- La autorización DEBE considerar siempre el alcance de compañías administrables por el usuario.
- Los controles de árbol (jerarquías) deben ser accesibles y operables mediante teclado.
- El stack tecnológico ratificado es .NET 10 + C# + ASP.NET Core 10 Web API + Entity Framework Core 10 +
  SQL Server + LINQ (decisión arquitectónica explícita del usuario, Sesión 2026-09-14; detalle completo en
  `specs/001-control-acceso-empresarial/plan.md` y `research.md`). Toda pieza de infraestructura adicional
  (mensajería, caché distribuido, microservicios, proveedor de identidad externo, u otra no listada aquí)
  DEBE justificarse mediante un requisito funcional o no funcional explícito antes de incorporarse — no se
  asume ni se introduce especulativamente.

## Gobernanza

Esta constitución prevalece sobre cualquier otra práctica, plantilla o convención en caso de
conflicto. Toda especificación, plan o tarea generada por Spec Kit para este proyecto DEBE verificar
cumplimiento con estos principios antes de aprobarse.

Los cambios a estos principios requieren actualizar este documento mediante una nueva propuesta
explícita. Se aplica versionado semántico: MAJOR para cambios incompatibles o eliminación/redefinición
de principios existentes, MINOR para incorporación de nuevos principios o ampliaciones materiales de
guía existente, y PATCH para aclaraciones de redacción sin cambio de obligaciones. Toda modificación
DEBE documentarse en el informe de impacto de sincronización al inicio de este archivo.

**Version**: 1.1.1 | **Ratified**: 2026-09-14 | **Last Amended**: 2026-09-14
