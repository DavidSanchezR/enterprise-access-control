# Validación funcional post-Baseline

## 1. Propósito

Este documento registra la validación funcional realizada después del cierre del Baseline del proyecto Enterprise Access Control.

El objetivo es identificar, analizar, corregir y validar comportamientos funcionales o de experiencia de usuario observados durante el uso real de la solución, sin reabrir el Baseline cerrado.

Esta validación no constituye una nueva etapa de desarrollo ni un Stage 2.

El Baseline T001–T242 permanece cerrado.

---

## 2. Contexto de validación

La validación se realiza sobre la implementación integrada del sistema después de:

- completar las tareas T001–T242;
- cerrar las decisiones del Baseline;
- completar las pruebas correspondientes;
- cerrar la especificación y documentación del Baseline;
- integrar los cambios finales en `main`;
- desplegar el entorno local de desarrollo;
- realizar pruebas funcionales utilizando la interfaz de usuario.

Los hallazgos registrados aquí representan observaciones obtenidas durante la validación funcional posterior al cierre del Baseline.

---

## 3. Alcance

La validación comprende:

- comportamiento funcional;
- reglas de negocio;
- vigencias y relaciones temporales;
- aislamiento y autorización;
- interacción de los controles de usuario;
- jerarquías;
- formularios;
- presentación de información;
- coherencia entre frontend, backend y contratos;
- cobertura de pruebas cuando corresponda.

No forma parte del alcance:

- reabrir requisitos del Baseline;
- modificar retrospectivamente las tareas T001–T242;
- crear una nueva etapa de desarrollo;
- iniciar Stage 2;
- modificar la especificación únicamente para hacer coincidir la implementación existente.

Cuando un hallazgo contradiga una decisión explícita del Baseline, deberá identificarse como tal y determinarse si corresponde a:

- un defecto de implementación;
- un defecto de UX;
- un defecto de integración;
- un vacío de pruebas;
- o un nuevo requerimiento/regla de negocio.

---

## 4. Clasificación de hallazgos

Cada hallazgo deberá clasificarse de acuerdo con su naturaleza:

### Defecto funcional

El comportamiento implementado contradice un requisito o decisión de negocio ya aprobado.

### Defecto UX

El comportamiento funcional puede ser correcto, pero la interacción o presentación no proporciona una experiencia adecuada al usuario.

### Defecto de integración

Existe una inconsistencia entre componentes, por ejemplo:

- frontend y backend;
- API y contrato;
- persistencia y servicio;
- servicio y reglas de negocio.

### Vacío de pruebas

La funcionalidad está correctamente implementada, pero no existe cobertura suficiente para detectar regresiones o validar un caso importante.

### Vacío de requisito

El comportamiento esperado observado durante la validación no está definido en el Baseline.

Si además contradice una decisión explícita del Baseline, deberá registrarse expresamente dicha contradicción.

---

## 5. Hallazgos identificados

| ID | Módulo | Hallazgo | Tipo | Evidencia | Severidad |
|---|---|---|---|---|---|
| VF-001 | Compañías | En el formulario de editar y nueva compañía, en el campo del tipo de documento deben visualizarse los valores de la tabla maestra para poder elegir el valor y no ingresar el ID autogenerado, ya que ese valor debería estar oculto dentro del control del listado. | Defecto de implementación (frontend) — registrado inicialmente como UX; ver §16 | Captura | Alta |
| VF-002 | Unidades Organizativas | El control que muestra las unidades organizativas creadas no es amigable con el usuario. No se puede interactuar con el control, que debería ser un control del tipo árbol que permita desplegar los nodos y/o ramas de acuerdo con la jerarquía de las unidades organizativas registradas. | Defecto de implementación (frontend) — registrado inicialmente como UX; ver §17 | Captura | Alta |
| VF-003 | Áreas de Acceso | El control que muestra las áreas de acceso creadas no es amigable con el usuario. No se puede interactuar con el control, que debería ser un control del tipo árbol que permita desplegar los nodos y/o ramas de acuerdo con la jerarquía de las áreas de acceso registradas. | Defecto de implementación (frontend) — registrado inicialmente como UX; ver §18 | Captura | Alta |
| VF-004 | Permisos de Acceso | La fecha de inicio y fin de vigencia solo debería ser del tipo fecha y el control solo debería mostrar la fecha correspondiente. | Cambio de requisito post-Baseline (modifica Historia 8 y RF-029; crea RF-083) — registrado inicialmente como defecto funcional; ver §19 | Comportamiento esperado no especificado | Media |
| VF-005 | Permisos de Acceso | En el formulario de Nuevo Permiso, por Persona, en el control de "Buscar Persona", debería existir un placeholder que indique: "Ingrese su nro. de documento". | UX | Captura | Baja |
| VF-006 | Personas | En el formulario de históricos de la persona, en el registro de los contextos operativos, no permite elegir la unidad administrativa a asignar a la persona. Debería permitir abrir un modal donde se liste toda la estructura de las unidades organizativas y permita elegir la que se encuentre habilitada y que corresponda. | Comportamiento correcto — cerrado sin cambios tras validación manual; registrado inicialmente como defecto funcional; ver §20 | Captura | Alta |
| VF-007 | Personas | Mientras el trabajador tenga al menos una vigencia con una compañía o registro de pertenencia, al momento de asignar un tipo de perfil y/o tipo de persona, debería permitir registrarle dicho dato, limitando su vigencia a la fecha fin de vigencia de la compañía asociada o del registro de pertenencia. Esta regla de contención temporal debe aplicarse también a las entidades que actualmente no se encuentran sujetas a dicha contención: `AsignaciónTipoPersona` y `PermisoAcceso`. | Cambio de requisito post-Baseline (modifica RF-072; ver §9) | Captura | Alta |
| VF-008 | Personas | En el formulario de históricos de la persona, el registro del perfil y/o tipo de persona de la persona seleccionada está permitiendo solapamiento de fechas. Solo debería existir un tipo de persona asignado en un rango de tiempo y debería permitir eliminar y/o eliminar el perfil seleccionado. | Comportamiento conforme a RF-011 — cerrado sin cambios tras validación manual; registrado inicialmente como defecto funcional; ver §21 | Captura | Alta |
| VF-009 | Personas | Mientras el trabajador tenga al menos una vigencia con una compañía o registro de pertenencia, al momento de asignar una credencial, debería permitir registrarle dicho dato, limitando su vigencia a la fecha fin de vigencia de la compañía asociada o del registro de pertenencia. | Comportamiento correcto (RF-072) — cerrado sin cambios tras validación manual; registrado inicialmente como defecto funcional; ver §22 | Captura | Alta |
| VF-010 | Usuario | En el formulario de crear nuevo usuario, los controles de Rol de Usuario se muestran desalineados, brindando una mala experiencia al usuario. | Defecto de implementación (visual) — registrado inicialmente como UX; ver §14 | Captura | Baja |
| VF-011 | Usuario | En el formulario de crear nuevo usuario, en la última pantalla de resumen se está mostrando el ID de la compañía en vez de mostrarse el nombre de la compañía, que es lo que el usuario debe visualizar. | Defecto de implementación — registrado inicialmente como UX; ver §15 | Captura | Baja |

---

## 6. Agrupación inicial de hallazgos

Los hallazgos se agrupan inicialmente de la siguiente manera:

### Vigencia y reglas temporales

- VF-004
- VF-007
- VF-008
- VF-009

### Contexto organizacional

- VF-006

### Interfaces jerárquicas

- VF-002
- VF-003

### Formularios y presentación

- VF-001
- VF-005
- VF-010
- VF-011

---

## 7. Trazabilidad

Cada hallazgo deberá analizarse contra:

1. Requisitos funcionales.
2. Decisiones de negocio.
3. Modelo de datos.
4. Contratos API.
5. Implementación backend.
6. Implementación frontend.
7. Pruebas existentes.
8. Reglas de seguridad y autorización.
9. Reglas de vigencia.
10. Documentación del Baseline.

No deberá modificarse una especificación existente únicamente para que el comportamiento actual sea considerado correcto.

Cuando el hallazgo represente una nueva regla de negocio, deberá identificarse explícitamente como tal.

---

## 8. Orden de análisis

El análisis se realizará inicialmente en el siguiente orden:

1. VF-007
2. VF-008
3. VF-009
4. VF-006
5. VF-004
6. VF-001
7. VF-011
8. VF-002
9. VF-003
10. VF-005
11. VF-010

Este orden prioriza primero las reglas de negocio y vigencia que pueden afectar múltiples entidades y funcionalidades.

---

## 9. Análisis de VF-007

VF-007 deberá analizar específicamente la regla de contención temporal existente para:

- Contexto Operativo;
- Unidad Organizativa;
- Credencial.

La validación deberá determinar cómo extender dicha regla para incluir:

- `AsignaciónTipoPersona`;
- `PermisoAcceso`.

El análisis deberá determinar:

- si la fecha de inicio también debe estar contenida;
- si la fecha de fin debe ser menor o igual a la fecha de fin de la pertenencia;
- qué ocurre cuando no existe una pertenencia vigente;
- qué pertenencia constituye el límite temporal;
- qué sucede cuando existen renovaciones;
- qué sucede con registros existentes que actualmente superan la vigencia de la pertenencia;
- impacto sobre la revocación;
- impacto sobre la evaluación de acceso;
- impacto sobre contratos;
- impacto sobre frontend;
- impacto sobre pruebas existentes.

El análisis deberá diferenciar claramente:

- el comportamiento actualmente aprobado por el Baseline;
- el nuevo comportamiento solicitado por VF-007;
- las partes que constituyen una modificación de regla de negocio.

### 9.1 Estado

**CLOSED** (2026-09-25).

Historial de estados:

| Estado | Detalle |
|---|---|
| ANALYZING | Análisis completado y decisiones D1–D4 resueltas (§9.2–§9.8). |
| FIXED | Implementado como cambio de requisito post-Baseline mediante el bloque POST-BASELINE — VF-007 de tasks.md, con RF-082, CS-042 y CS-043 en spec.md. **T243–T266 completadas**; T001–T242 intactas y sin tareas adicionales. |
| VALIDATED | Validación funcional realizada por el usuario **sobre la interfaz**, con resultado satisfactorio: (1) la asignación de perfil/tipo de persona respeta la contención temporal respecto de la pertenencia vigente; (2) la asignación de permisos con `Alcance = PERSONA` respeta esa misma contención; (3) los escenarios de rechazo y sus mensajes de validación funcionan. |
| CLOSED | Cierre documental de VF-007 (T266). |

Observaciones de la validación funcional, **fuera del alcance de VF-007**:

- La asignación de credenciales, sujeta a RF-072 desde el Baseline, también se validó funcionalmente y se
  comporta correctamente. No forma parte de VF-007 y no genera cambios en este hallazgo (ver VF-009).
- La diferencia visual entre perfiles/permisos y credenciales es un comportamiento de UX distinto, no un
  defecto. No se abre ningún hallazgo por ella en este momento.

Evidencia automatizada (todas las suites en verde tras la implementación):

| Suite | Resultado | Cobertura de VF-007 |
|---|---|---|
| Unitarias backend | 141/141 | `ReglaContencionPermisoTests` (matriz D4, tolerancia de fechas) |
| Integración backend (SQL Server real, Testcontainers) | 567/567 | CS-042: `ContencionPerfilesTests`, `ContencionPermisosPersonaTests` (incluye aislamiento `404`, hallazgo C1); CS-043: `RegistrosAnterioresRf082Tests`; RF-073 y D3: `Rf082RenovacionYCascadaTests`; regresión de RF-072 sin cambios |
| Contrato (incluido el snapshot OpenAPI) | 250/250 | `people.yaml` y `permissions.yaml` v1.1.0 respaldados por la API |
| Vitest | 172/172 | `PerfilesPersona.test.tsx`, casos RF-082 de `PermisosPage.test.tsx` |
| Playwright (sobre la API reconstruida) | 9/9 | Flujos de quickstart §5/§6 con el nuevo orden pertenencia → perfil |

Los escenarios de quickstart.md §9 están cubiertos por esas pruebas, ejecutadas por HTTP contra la API y
SQL Server reales. No se reprodujeron a mano en la base de validación, para no escribir en ella datos de
prueba. El recorrido manual en la interfaz forma parte de la validación funcional (VALIDATED).

Sin cambios en `RevocacionService`, `ReglasRevocacion`, `EvaluadorDeAcceso`, `EvaluacionAccesoService` ni en
los servicios de las tres entidades de RF-072. Sin migraciones. La lógica de `ContencionTemporalValidator`
no cambió; solo sus comentarios.

### 9.2 Comportamiento aprobado por el Baseline

RF-072 (spec.md) aplica la contención temporal completa exclusivamente a
`ContextoOperativoPersonaPrincipal`, `AsignaciónPersonaUnidadOrganizativa` y `AsignaciónCredencial`,
y **excluye expresamente** `AsignaciónTipoPersona` y `PermisoAcceso` ("su vigencia es obligatoria pero
libre"). La exclusión consta también en data-model.md, research.md §25 (alternativa "descartada
explícitamente por negocio"), `contracts/people.yaml`, `contracts/permissions.yaml` y las tareas
T106/T107/T165. La implementación y las pruebas del Baseline son coherentes con esa exclusión.

### 9.3 Clasificación

VF-007 **no es un defecto de implementación**: el código cumple el Baseline. Es un **cambio de
requisito post-Baseline que modifica la regla de negocio RF-072**. Los artefactos del Baseline
(spec.md, data-model.md, research.md, tasks.md) no se modifican; esta sección es el registro de la
decisión que los complementa. Los contratos API sí deberán actualizarse al implementar, porque las
pruebas de contrato validan la implementación contra ellos.

### 9.4 Regla de negocio confirmada (D1, D2)

Para toda operación sujeta a validación (ver §9.5):

```text
FechaHoraInicio_hija >= FechaHoraInicio_pertenencia
AND
FechaHoraFin_hija    <= FechaHoraFin_pertenencia
```

- **D1 — Contención completa (confirmada)**. Misma forma que RF-072 y CS-034; la igualdad en ambos
  extremos es válida.
- **D2 — Entidades (confirmada)**:
  1. `ContextoOperativoPersonaPrincipal` (sin cambios);
  2. `AsignaciónPersonaUnidadOrganizativa` (sin cambios);
  3. `AsignaciónCredencial` (sin cambios);
  4. `AsignaciónTipoPersona` (**nueva**);
  5. `PermisoAcceso` **solo cuando `Alcance = PERSONA`** (**nueva**). No aplica a
     `UNIDAD_ORGANIZATIVA` ni a `COMPANIA`: no tienen persona cuya pertenencia sirva de límite.
- **Pertenencia límite**: la `AsignaciónPersonaCompañía` vigente por fechas en el instante de la
  operación, igual que hoy para las tres entidades existentes (RF-072, "vigente en ese momento").
- **Sin pertenencia vigente**: la operación sujeta a validación se rechaza con
  `400 SIN_PERTENENCIA_VIGENTE`.
- **Fuera de la vigencia**: `409 FUERA_DE_CONTENCION_TEMPORAL`.
- **Punto único**: la regla se valida exclusivamente mediante `ContencionTemporalValidator`, sin
  lógica duplicada (mismo criterio que T107/T165).

### 9.5 Registros existentes y operaciones (D4)

La nueva regla **no se aplica retroactivamente** y **no modifica automáticamente ningún dato
existente**: no hay migración de datos, ni restricción de base de datos, ni proceso que acorte o
desactive registros. Los `AsignaciónTipoPersona` y `PermisoAcceso` (alcance PERSONA) creados bajo la
regla anterior conservan sus fechas aunque queden fuera de la pertenencia. Esto es coherente con:
RF-072 (validación en creación/actualización), RF-073 d) y CS-035 (las asociaciones existentes
conservan sus fechas), la cascada de RF-061 (solo acorta con `MIN` y nunca reescribe
`FechaHoraInicio`), la conservación íntegra del histórico y la auditoría ya existente
(`UpdatedAt`/`UpdatedById`, RF-026/RF-027).

**Criterio único**: la contención se valida cuando la operación **crea o amplía la capacidad del
registro de otorgar acceso**, y nunca cuando la reduce o no altera su vigencia. En concreto, se valida
si el registro resultante queda `ACTIVO` y además (a) es nuevo, (b) cambia su `FechaHoraInicio` o su
`FechaHoraFin`, o (c) pasa de `INACTIVO` a `ACTIVO`.

| # | Operación | ¿Valida contención? | ¿Exige pertenencia vigente? | Resultado sobre registros anteriores a VF-007 |
|---|---|---|---|---|
| 1 | Crear una nueva asignación | Sí, completa | Sí | No aplica (registro nuevo). |
| 2 | Modificar fechas (renovar, ampliar, acortar o mover) | Sí, sobre el rango resultante completo | Sí | Si el rango resultante no cabe (p. ej., un inicio ya transcurrido anterior a la pertenencia), se rechaza. El camino es desactivar el registro y crear uno nuevo; no se reescribe un inicio ya transcurrido. |
| 3 | Modificar otros atributos sin cambiar la vigencia (bloques horarios de un permiso) | No | No | Se permite; las fechas se conservan tal cual. |
| 3b | Reactivar (`INACTIVO` → `ACTIVO`) | Sí, completa | Sí | Se rechaza si está fuera de contención: reactivar vuelve a otorgar acceso (el paso 12 de la evaluación exige `Estado = ACTIVO`). |
| 4 | Desactivar / revocar (`ACTIVO` → `INACTIVO`) | No | No | Siempre se permite, sin modificar las fechas. Reducir acceso nunca se bloquea (denegación por defecto). |
| 5 | Consultar el histórico | No | No | Se devuelve tal como está almacenado, sin recalcular, filtrar ni marcar. |

"Cambiar la fecha" se determina comparando los instantes recibidos con los almacenados. El
formulario de permisos ya reenvía los valores originales cuando el usuario no los modifica
(`conservarSiNoCambio`).

Operaciones disponibles hoy:

- `PermisoAcceso`: `POST` (caso 1) y `PUT` (casos 2, 3, 3b y 4, según qué cambie).
- `AsignaciónTipoPersona`: solo `POST` (caso 1) y `GET` (caso 5). No existe hoy ninguna operación de
  modificación, desactivación o eliminación; si VF-008 las introduce, deberán seguir esta tabla.

### 9.6 Renovación, revocación y evaluación

- **Renovación de la pertenencia (RF-073)**: sin cambios. Amplía el límite para operaciones
  posteriores; no modifica perfiles ni permisos existentes.
- **Revocación / cierre de la pertenencia (D3, confirmada)**: la cascada de RF-061 a RF-065 **no se
  modifica** y sigue sin alcanzar perfiles ni permisos. Tras un cese anticipado, esos registros
  conservan sus fechas; la contención se evalúa en cada operación, no como invariante permanente.
- **Evaluación de acceso**: sin cambios. Sin pertenencia vigente en el instante evaluado, el paso 6
  deniega antes de evaluar perfiles (paso 9) y permisos (pasos 11–12), así que un registro anterior
  que exceda la pertenencia nunca ha producido ni producirá acceso fuera de ella.

### 9.7 Riesgo identificado (relacionado con VF-004)

Las vigencias de `PermisoAcceso` no se normalizan a días completos: se capturan con `datetime-local`
y se convierten a UTC. La pertenencia termina a las 23:59:59.999 UTC. Un permiso cuyo fin se capture
como "31/07 23:59" en America/Lima (04:59 UTC del día siguiente) excedería la pertenencia y sería
rechazado. VF-007 no cambia la normalización (corresponde a VF-004). Las pruebas de VF-007 deben
cubrir este límite y el frontend debe calcular el máximo permitido a partir del instante UTC de la
pertenencia.

### 9.8 Alcance de implementación

Incluido:

- contención en `EstadoEfectivoService.AsignarPerfilAsync`;
- contención en `PermisoAccesoService.CrearAsync` y `ActualizarAsync` para `Alcance = PERSONA`,
  según §9.5;
- respuestas `400`/`409` en `contracts/people.yaml` y `contracts/permissions.yaml`, y retirada de la
  mención "RF-072 no aplica";
- manejo de ambos códigos de error en `PerfilesPersona.tsx` y `PermisoFormulario.tsx`;
- ajuste de las pruebas que hoy verifican la exclusión y nuevas pruebas de regresión.

Excluido:

- cascada o revocación (D3);
- permisos de alcance `UNIDAD_ORGANIZATIVA` o `COMPANIA`;
- migración o corrección de datos existentes;
- normalización de vigencias de permisos (VF-004);
- solapamiento, eliminación o desactivación de perfiles (VF-008);
- credenciales (VF-009, ya sujetas a RF-072);
- cualquier modificación o renumeración de T001–T242.

> **Nota de evolución**: la exclusión de cambios en spec.md, data-model.md, research.md y tasks.md, y de la
> creación de nuevas tareas, correspondía solo a la fase inicial de análisis y quedó superada. VF-007 se
> formalizó como cambio de requisito post-Baseline mediante el flujo SpecKit
> (`/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` → `/speckit-implement`):
> RF-082, CS-042 y CS-043 en spec.md, los artefactos de diseño correspondientes y el bloque de tareas
> POST-BASELINE — VF-007, **T243–T266, creadas y completadas**. No se modificó T001–T242 ni se crearon tareas
> adicionales.

### 9.9 Observación independiente — fuga preexistente en `ValidarSujetoAsync` (fuera de VF-007)

Detectada durante `/speckit-analyze` sobre el bloque VF-007 (hallazgo C1). **No forma parte de VF-007 ni
genera tareas en T243–T266.**

- **Qué ocurre**: desde antes de VF-007, `PermisoAccesoService.ValidarSujetoAsync` responde `400` cuando la
  persona indicada en un permiso de alcance PERSONA no existe. Un administrador del área puede así distinguir
  un identificador de persona existente de uno inexistente, aunque la persona esté fuera de su alcance.
- **Relación con VF-007**: C1 se corrigió dentro de VF-007 solo para la información **nueva** que RF-082
  expone, es decir, la pertenencia temporal. Antes de consultarla se exige el alcance histórico del actor
  (`PersonaService.ExigirAlcanceHistoricoAsync`, `404`) (research.md §35.6; T245, T246, T252). La existencia
  de la persona ya se revelaba antes y no la introduce VF-007.
- **Estado**: pendiente de análisis como hallazgo propio del registro, con su propia trazabilidad frente al
  Principio I y a RF-049/RF-077. No se corrige en VF-007.

---

## 10. Análisis de VF-008

VF-008 deberá analizar la regla de no solapamiento de vigencias para:

- `AsignaciónTipoPersona`;
- tipo de persona;
- perfil.

Debe determinarse:

- si la regla ya está definida;
- si está definida en el frontend;
- si está definida en el backend;
- si está definida en el modelo;
- si existe una restricción de base de datos;
- si existen pruebas;
- qué comportamiento debe tener la eliminación o revocación del perfil.

No debe confundirse VF-008 con la regla de contención temporal de VF-007.

---

## 11. Análisis de VF-009

VF-009 deberá verificar la regla de dependencia temporal de las credenciales respecto de la pertenencia de la persona a una compañía.

Debe verificarse:

- existencia de pertenencia vigente;
- fecha de inicio;
- fecha de fin;
- comportamiento ante pertenencia expirada;
- comportamiento ante renovación;
- validación backend;
- validación frontend;
- contrato API;
- pruebas.

---

## 12. Corrección e implementación

Cada hallazgo deberá pasar por las siguientes etapas:

```text
OPEN
  ↓
ANALYZING
  ↓
FIXED
  ↓
VALIDATED
  ↓
CLOSED
```

---

## 13. Análisis de VF-005 — Placeholder de "Buscar persona"

### 13.1 Estado

**CLOSED** (2026-09-25).

| Estado | Detalle |
|---|---|
| ANALYZING | Análisis técnico completado (§13.2–§13.4). |
| FIXED | Placeholder añadido (T267) y pruebas de componente (T268), en el bloque POST-BASELINE — VF-005 de tasks.md. |
| VALIDATED | Validación técnica del frontend (T269): pruebas relevantes, Vitest completo, typecheck y ESLint (§13.6). |
| CLOSED | Cierre documental (T269). |

### 13.2 Hallazgo y clasificación

- **Hallazgo**: en el formulario de Nuevo Permiso, con alcance Persona, el control "Buscar persona" debe
  mostrar el placeholder "Ingrese su nro. de documento".
- **Clasificación**: **Gap UX**. No es un defecto de implementación, porque ningún RF, CS ni decisión del
  Baseline exigía ese placeholder. Tampoco es un cambio de requisito, porque ninguna regla cambia.
- **Severidad**: Baja.
- **Estado inicial**: ANALYZING.

### 13.3 Análisis

- **Componente afectado**: `frontend/src/features/permissions/PermisoFormulario.tsx`, input
  `permiso-buscar-persona`. Solo se muestra al **crear** un permiso con **alcance PERSONA**.
- **Placeholder previo**: ninguno. Solo la etiqueta "Buscar persona".
- **Criterio de búsqueda existente**: el texto alimenta `usePersonas({ texto, tamañoPagina: 20 })` y se envía
  como `GET /api/personas?texto=…`. `PersonaService.AplicarFiltrosDeTexto` busca coincidencias parciales por
  **nombres, apellidos o número de documento**, dentro del alcance del usuario (RF-035), tal como declara el
  parámetro `texto` de `contracts/people.yaml`.
- **Regla UX aplicable**: ux-ui.md §23 exige labels persistentes y no depender de placeholders. El
  placeholder solo puede complementar a la etiqueta.

### 13.4 Cambio realizado

- Se añadió `placeholder="Ingrese su nro. de documento"` al input, con el texto exacto aprobado.
- **La etiqueta "Buscar persona" permanece**. El placeholder es solo una ayuda complementaria.
- **El comportamiento de búsqueda no cambió**: se sigue buscando por nombres, apellidos y documento con el
  mismo parámetro `texto`. El placeholder orienta hacia el criterio más preciso, pero no restringe la
  búsqueda.
- **Archivos modificados**:
  - `frontend/src/features/permissions/PermisoFormulario.tsx` (1 atributo);
  - `frontend/tests/unit/PermisosPage.test.tsx` (2 pruebas);
  - `specs/001-control-acceso-empresarial/tasks.md` (bloque POST-BASELINE — VF-005, T267–T269);
  - este registro. Además, se cerró la valla de código del diagrama de §12, que quedaba abierta y habría
    englobado esta sección.

### 13.5 Pruebas

En `PermisosPage.test.tsx`:

1. `el buscador de persona conserva su etiqueta, orienta con el placeholder y sigue buscando por texto`: el
   campo se localiza por su etiqueta "Buscar persona", tiene exactamente el placeholder aprobado, y escribir
   un número de documento llama a `buscarPersonas` con ese `texto`. El mock y el contrato de la búsqueda no
   cambiaron.
2. `el buscador de persona solo aparece con alcance persona`: el campo aparece con alcance Persona y
   desaparece al cambiar a Unidad organizativa.

Sin pruebas de backend, contrato, integración ni E2E: el cambio no las afecta.

### 13.6 Validación (T269)

| Verificación | Resultado |
|---|---|
| `PermisosPage.test.tsx` | 19/19 |
| Vitest completo | 174/174 (19 archivos) |
| Typecheck (`tsc -b`) | Sin errores |
| ESLint sobre los archivos de VF-005 | Sin errores ni avisos |
| ESLint completo | 1 error y 4 avisos **preexistentes y no relacionados** (`PertenenciaContextoWizard.tsx`, `CamposAsignacion.tsx`), ya presentes antes de VF-005 |

La validación de VF-005 es la validación técnica del frontend definida en T269.

### 13.7 Trazabilidad

| Elemento | Relación |
|---|---|
| VF-005 | Hallazgo de origen |
| UX-07 (ux-ui.md) | Pantalla afectada: configurar permisos/áreas |
| ux-ui.md §23 | Labels persistentes; el placeholder complementa, no sustituye |
| RF-035 | Búsqueda de personas dentro del alcance autorizado: **sin cambios** |
| T267, T268, T269 | Bloque POST-BASELINE — VF-005 |

### 13.8 Alcance y Baseline

- Sin cambios de backend, API, contratos (`people.yaml`, snapshot OpenAPI), requisitos (spec.md), modelo de
  datos ni decisiones del Baseline.
- No se modificaron T001–T242 (Baseline) ni T243–T266 (VF-007). No se crearon tareas más allá de T269.
- No se abrió Stage 2 ni ningún otro hallazgo.

---

## 14. Análisis de VF-010 — Alineación de los controles de "Rol"

### 14.1 Estado

**CLOSED** (2026-09-25).

| Estado | Detalle |
|---|---|
| ANALYZING | Causa localizada y reproducida con el marcado y las hojas de estilo reales (§14.3). |
| FIXED | Regla CSS acotada (T270) y comprobación de geometría en el E2E (T271), en el bloque POST-BASELINE — VF-010 de tasks.md. |
| VALIDATED | Validación técnica (T272): E2E de administración de usuarios, prueba negativa sin la corrección, Vitest, typecheck y ESLint (§14.6). |
| CLOSED | Cierre documental (T272). |

### 14.2 Hallazgo y clasificación

- **Hallazgo**: en el formulario de crear nuevo usuario, los controles de Rol se muestran desalineados.
- **Clasificación**: **defecto de implementación (visual)**. Se registró inicialmente como "UX" y se
  reclasificó tras el análisis. El propio componente declara el diseño esperado (`.opcion-radio`: flex,
  `align-items: center`, `gap`), el mismo esquema que el resto de grupos de opciones de la aplicación, pero
  una regla global de la cascada impedía que se cumpliera. No es un gap UX, porque el diseño no faltaba; no
  es un cambio de requisito ni una regresión.
- **Severidad**: Baja. **Impacto funcional**: ninguno.
- **Estado inicial**: ANALYZING.

### 14.3 Análisis

- **Componente afectado**: `frontend/src/features/users/CamposAsignacion.tsx`, `<fieldset className="campo">`
  con `<legend>Rol</legend>` y un `<label className="opcion-radio">` con `<input type="radio">` por rol. Lo
  usan el alta de usuario (`CrearUsuarioWizard`, UX-17) y la asignación de rol (`AsignarRolDialogo`, UX-19).
- **Causa**: la regla global `input, select { width: 100% }` de `frontend/src/index.css` se aplicaba también a
  los radios. Dentro de la etiqueta flex, cada radio se estiraba casi al ancho completo. Como los dos textos
  tienen longitudes distintas, las cajas de los radios también eran distintas: los círculos quedaban en
  posiciones horizontales diferentes y el texto se desplazaba a la derecha, partido en dos líneas.
- **Evidencia**: reproducción con el marcado real y `index.css`, `dialogo.css` y `users.css` reales en Chromium.
  Los radios medían 342 px y 320 px dentro de etiquetas de 456 px; con la corrección, 13 px cada uno,
  alineados y con el texto en una línea.
- **Origen**: Baseline. La regla global procede de `f8fb451` (T001–T168); `.opcion-radio` y `CamposAsignacion`,
  de `0783e24` (T169–T242). No lo introdujeron VF-007 ni VF-005: los archivos implicados no tenían cambios.

### 14.4 Cambio realizado

- Se añadió en `frontend/src/features/users/users.css`, junto a `.opcion-radio`:

  ```css
  .opcion-radio input {
    width: auto;
  }
  ```

- **No se modificó `index.css`**: una regla global cambiaría el aspecto de otras pantallas que VF-010 no
  cubre. La corrección reutiliza el recurso que el proyecto ya aplica a inputs concretos (`width: auto`).
- **Sin cambios** en el marcado de `CamposAsignacion.tsx`, `rolesAsignables`, handlers, validación,
  autorización (RF-074 a RF-077), API, contratos, backend ni requisitos.
- **Archivos modificados**:
  - `frontend/src/features/users/users.css` (6 líneas añadidas);
  - `frontend/tests/e2e/administracion-usuarios.spec.ts` (14 líneas añadidas);
  - `specs/001-control-acceso-empresarial/tasks.md` (bloque POST-BASELINE — VF-010, T270–T272);
  - este registro.

### 14.5 Pruebas

- **E2E `administracion-usuarios.spec.ts`, paso UX-17**: en el paso "Rol", el ancho de `boundingBox()` de cada
  radio es como máximo 24 px y los dos radios comparten la misma `x` (tolerancia de 1 px). Es la única capa de
  pruebas capaz de detectar el defecto, porque jsdom (Vitest) no calcula maquetación.
- **Prueba negativa**: con la regla retirada temporalmente, el E2E falla (`Received: 317.34` frente a
  `Expected: <= 24`). La prueba detecta, por tanto, el defecto que corrige.

### 14.6 Validación (T272)

| Verificación | Resultado |
|---|---|
| E2E `administracion-usuarios` (con la corrección) | 3/3 |
| E2E sin la corrección (prueba negativa) | Falla, como se esperaba |
| Vitest completo | 174/174 (19 archivos) |
| Typecheck (`tsc -b`) | Sin errores |
| ESLint sobre el archivo de VF-010 | Sin errores ni avisos |
| ESLint completo | 1 error y 4 avisos **preexistentes y no relacionados** (`PertenenciaContextoWizard.tsx`, `CamposAsignacion.tsx`), ya presentes antes de VF-010. Los avisos de `CamposAsignacion.tsx` son de `react-refresh/only-export-components` y no tienen relación con la alineación |

La preparación global de los E2E recrea `eac-api` contra la base `EnterpriseAccessControl_E2E`. Al terminar,
el contenedor se devolvió a su configuración original, verificada por hash, y `eac-sqlserver` no se reinició.

### 14.7 Trazabilidad

| Elemento | Relación |
|---|---|
| VF-010 | Hallazgo de origen |
| UX-17, UX-19 (ux-ui.md) | Pantallas afectadas: alta de usuario y asignación de rol |
| ux-ui.md §35, paso 2 | "Elegir entre los dos roles disponibles": sin cambios de contenido |
| ux-ui.md §23 y §26 | Labels persistentes y formularios accesibles: se mantienen |
| RF-074 a RF-077 | Roles, vigencia, autorización y alcance: **sin cambios** |
| T270, T271, T272 | Bloque POST-BASELINE — VF-010 |

### 14.8 Alcance y Baseline

- Sin cambios de backend, API, contratos, requisitos (spec.md), modelo de datos, autorización ni decisiones del
  Baseline.
- No se modificaron T001–T242 (Baseline), T243–T266 (VF-007) ni T267–T269 (VF-005). No se crearon tareas más
  allá de T272.
- No se abrió Stage 2 ni ningún otro hallazgo.

---

## 15. Análisis de VF-011 — Nombre de la compañía en la confirmación del alta de usuario

### 15.1 Estado

**CLOSED** (2026-09-25).

| Estado | Detalle |
|---|---|
| ANALYZING | Causa localizada y trazado el dato de compañía (§15.3). |
| FIXED | Presentación corregida (T273) y pruebas de componente (T274), en el bloque POST-BASELINE — VF-011 de tasks.md. |
| VALIDATED | Validación técnica (T275): pruebas relevantes, prueba negativa sin la corrección, Vitest completo, typecheck y ESLint (§15.6). |
| CLOSED | Cierre documental (T275). |
| REOPENED (extensión) | La observación de §15.8 se incorpora a VF-011 por decisión aprobada, como el mismo incumplimiento de RF-013 en `UsuarioDetalle.tsx`. No se abre VF-012. |
| FIXED (extensión) | Presentación corregida en las pestañas Asignaciones e Histórico (T276) y pruebas de componente (T277), en el bloque POST-BASELINE — VF-011 (extensión) de tasks.md. |
| VALIDATED (extensión) | Validación técnica (T278): pruebas relevantes, prueba negativa, Vitest completo, typecheck y ESLint (§15.10). |
| CLOSED | Cierre documental de la extensión (T278). VF-011 queda cerrado con ambos alcances. |

### 15.2 Hallazgo y clasificación

- **Hallazgo**: la última pantalla del alta de usuario muestra el ID de la compañía en vez de su nombre.
- **Clasificación**: **defecto de implementación**. Se registró inicialmente como "UX" y se reclasificó tras el
  análisis. El comportamiento esperado está especificado: **RF-013** ("Todos los IDs … ocultos en la
  interfaz", Principio II) y **ux-ui.md §35, Paso 5 — Confirmación** ("Mostrar: Correo → Rol → Compañía (si
  aplica) → Vigencia"). La implementación de T201 no lo cumplía. No es un defecto funcional, porque se enviaba
  el `companiaId` correcto, ni un problema de contrato o API.
- **Severidad**: Baja. **Impacto**: solo presentación.
- **Estado inicial**: ANALYZING.

### 15.3 Análisis

- **Componente afectado**: `frontend/src/features/users/CrearUsuarioWizard.tsx`, paso 4 "Confirmación"
  (`<dl className="resumen-confirmacion">`), fila "Compañía", que solo aparece con el rol
  `COMPANY_ADMINISTRATOR`.
- **Causa**: el estado del wizard (`BorradorAsignacion`) guarda solo `companiaId`, y el resumen lo
  renderizaba directamente (`<dd>{asignacion.companiaId}</dd>`).
- **Disponibilidad del nombre**: ya estaba en el frontend. El selector del paso "Compañía" (`CamposAsignacion`)
  y el listado de `UsuariosPage` usan `useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })`, que devuelve
  `Compania { id, nombre, … }` (`contracts/companies.yaml`). No hacía falta ningún endpoint ni dato nuevo.

### 15.4 Cambio realizado

- En `CrearUsuarioWizard.tsx`, el resumen resuelve el nombre con `useCompanias({ estado: 'ACTIVO',
  tamañoPagina: 200 })`: **el mismo filtro** que el selector, así que comparte caché de TanStack Query y no
  añade llamadas HTTP. Si la compañía no puede resolverse, se muestra el texto "Compañía no disponible" (clase
  existente `.sin-resolver`), **nunca el UUID**.
- **Sin cambios** en `BorradorAsignacion`, `CamposAsignacion`, validación, cuerpo del `POST` (sigue enviando
  `companiaId`), backend, API, contratos, requisitos, modelo de datos ni autorización.
- **Archivos modificados**:
  - `frontend/src/features/users/CrearUsuarioWizard.tsx` (10 líneas añadidas, 1 eliminada: el `<dd>` anterior);
  - `frontend/tests/unit/UsuariosPage.test.tsx` (63 líneas añadidas);
  - `specs/001-control-acceso-empresarial/tasks.md` (bloque POST-BASELINE — VF-011, T273–T275);
  - este registro.

### 15.5 Pruebas

En `UsuariosPage.test.tsx`:

1. `la confirmación muestra el nombre de la compañía y no su identificador`: un `GLOBAL_ADMINISTRATOR` crea un
   administrador de compañía eligiendo "Minera Propia". La confirmación muestra "Minera Propia" y no su UUID, y
   el alta sigue enviando ese mismo `companiaId`.
2. `si la compañía no puede resolverse, la confirmación nunca muestra su identificador`: un
   `COMPANY_ADMINISTRATOR` con una compañía preseleccionada que no figura en la lista ve "Compañía no
   disponible" y no el UUID.

**Prueba negativa**: con el wizard original restaurado temporalmente, ambas pruebas fallan (2 failed, 15
passed). Las pruebas detectan, por tanto, el defecto que corrigen.

Sin pruebas E2E, de backend ni de contrato: jsdom verifica el texto renderizado y el cambio no afecta a
otras capas.

### 15.6 Validación (T275)

| Verificación | Resultado |
|---|---|
| `UsuariosPage.test.tsx` | 17/17 |
| Prueba negativa (sin la corrección) | 2 fallos, como se esperaba |
| Vitest completo | 176/176 (19 archivos) |
| Typecheck (`tsc -b`) | Sin errores |
| ESLint sobre los archivos de VF-011 | Sin errores ni avisos |
| ESLint completo | 1 error y 4 avisos **preexistentes y no relacionados** (`PertenenciaContextoWizard.tsx`, `CamposAsignacion.tsx`), ya presentes antes de VF-011 |

### 15.7 Trazabilidad

| Elemento | Relación |
|---|---|
| VF-011 | Hallazgo de origen |
| RF-013 / Principio II | IDs ocultos en la interfaz: **ahora se cumple** en la confirmación |
| ux-ui.md §35, Paso 5 | "Mostrar: Correo → Rol → Compañía (si aplica) → Vigencia" |
| UX-17 | Pantalla afectada: crear usuario con su primera asignación |
| T201 | Tarea del Baseline que implementó el wizard (no se modifica) |
| T273, T274, T275 | Bloque POST-BASELINE — VF-011 |

### 15.8 Observación independiente (fuera de VF-011)

`frontend/src/features/users/UsuarioDetalle.tsx` también muestra `asignacion.companiaId` sin resolver en la
pestaña de asignaciones y en el histórico, el mismo patrón que incumple RF-013. **No forma parte del texto de
VF-011 ni genera tareas en este bloque.** Queda pendiente de análisis como hallazgo propio si se considera
necesario.

> **Actualización**: esta observación se incorporó después a VF-011 como extensión (T276–T278), por ser el
> mismo incumplimiento de RF-013. No se abrió VF-012. Ver §15.10. El texto anterior se conserva como registro
> del cierre inicial.

### 15.9 Alcance y Baseline

- Sin cambios de backend, API, contratos, requisitos (spec.md), modelo de datos, autorización ni decisiones del
  Baseline.
- No se modificaron T001–T242 (Baseline), T243–T266 (VF-007), T267–T269 (VF-005) ni T270–T272 (VF-010). No se
  crearon tareas más allá de T275.
- No se abrió Stage 2 ni ningún otro hallazgo.

### 15.10 Extensión: nombre de la compañía en `UsuarioDetalle` (T276–T278)

- **Alcance**: las dos vistas de `frontend/src/features/users/UsuarioDetalle.tsx` que mostraban
  `asignacion.companiaId` (el UUID):
  1. pestaña **Asignaciones**, columna "Compañía";
  2. pestaña **Histórico**, línea de cada asignación.

  Los diálogos de renovar y finalizar no se tocan: allí `companiaId` solo decide el texto ("en esa compañía" o
  "con alcance global") y nunca se muestra.
- **Clasificación**: el mismo **defecto de implementación** de VF-011 (RF-013). Es una extensión del hallazgo,
  no un hallazgo nuevo; no hay VF-012.
- **Corrección de premisa**: `GET /api/usuarios/{id}/roles` (`AsignacionRolAdministrativoDto`; contrato
  `AsignacionRolAdministrativo`) devuelve solo `companiaId`, **no** el nombre. No se modificaron el backend ni
  el contrato para añadirlo.
- **Cambio realizado (T276)**: el componente resuelve el nombre con `useCompanias({ estado: 'ACTIVO',
  tamañoPagina: 200 })`, la consulta que `UsuariosPage` (única vía de apertura del detalle) ya mantiene
  activa, así que se sirve desde caché sin llamada HTTP adicional. Sigue el mismo patrón que T273: nombre si se
  resuelve, "Compañía no disponible" (`.sin-resolver`) si no, **nunca el UUID**. El alcance global conserva
  "Todas (alcance global)" y "alcance global".
- **Compañías históricas inactivas o fuera del alcance actual**: se muestran como "Compañía no disponible".
  Por decisión aprobada, **no** se hace una consulta adicional sin filtro de estado para resolverlas: ampliaría
  la corrección y abriría una vía para revelar información fuera del alcance (RF-077).
- **Sin cambios** en `AsignacionRol`, `useRolesUsuario`, diálogos, payloads, backend, API, contratos,
  requisitos, modelo de datos ni autorización.
- **Archivos modificados**:
  - `frontend/src/features/users/UsuarioDetalle.tsx` (21 líneas añadidas; 2 eliminadas: las dos que mostraban
    el UUID);
  - `frontend/tests/unit/UsuarioDetalle.test.tsx` (simulación de `listarCompanias` en el `beforeEach` existente,
    ya que el componente ahora consulta compañías, más un bloque nuevo de 3 pruebas);
  - `specs/001-control-acceso-empresarial/tasks.md` (bloque POST-BASELINE — VF-011 (extensión), T276–T278);
  - este registro.
- **Pruebas (T277)**, bloque `UsuarioDetalle — nombre de la compañía (VF-011, RF-013)`:
  1. la pestaña Asignaciones muestra "Minera Propia" y no su UUID, y la asignación global conserva "Todas
     (alcance global)";
  2. el Histórico muestra "Minera Propia" y "alcance global", sin el UUID;
  3. una compañía no resoluble muestra "Compañía no disponible" en ambas pestañas y nunca el UUID.

  **Prueba negativa**: con el componente original, las 3 pruebas fallan y las 8 existentes siguen pasando.
- **Validación (T278)**:

  | Verificación | Resultado |
  |---|---|
  | `UsuarioDetalle.test.tsx` | 11/11 |
  | Prueba negativa (sin la corrección) | 3 fallos, como se esperaba |
  | Vitest completo | 179/179 (19 archivos) |
  | Typecheck (`tsc -b`) | Sin errores |
  | ESLint sobre los archivos de la extensión | Sin errores ni avisos |
  | ESLint completo | 1 error y 4 avisos **preexistentes y no relacionados** (`PertenenciaContextoWizard.tsx`, `CamposAsignacion.tsx`) |

- **Trazabilidad**: VF-011 (extensión) → RF-013 / Principio II → UX-18 (consultar usuario y sus asignaciones) →
  T276, T277, T278. T273–T275 permanecen intactas como registro del cierre inicial.
- **Alcance y Baseline**: no se modificaron T001–T275. No se crearon tareas más allá de T278. No se abrió
  Stage 2 ni ningún otro hallazgo.

---

## 16. Análisis de VF-001 — Tipo de documento de compañía

### 16.1 Estado

**CLOSED** (2026-09-25).

| Estado | Detalle |
|---|---|
| ANALYZING | Causa localizada y trazado el campo de extremo a extremo (§16.3). |
| FIXED | Selector del maestro en el formulario (T279) y pruebas de componente (T280), en el bloque POST-BASELINE — VF-001 de tasks.md. |
| VALIDATED | Validación técnica (T281): pruebas relevantes, prueba negativa sin la corrección, Vitest completo, typecheck y ESLint (§16.6). |
| CLOSED | Cierre documental (T281). |

### 16.2 Hallazgo y clasificación

- **Hallazgo**: en los formularios de Nueva compañía y Editar compañía, el tipo de documento debe elegirse de la
  tabla maestra y no escribirse como ID autogenerado.
- **Clasificación**: **defecto de implementación (frontend)**. Se registró inicialmente como "UX" y se
  reclasificó tras el análisis. El campo era un texto libre donde había que escribir el UUID, validado con
  `z.string().uuid(...)` y con la ayuda "Identificador del maestro de tipos de documento.". Eso incumple
  **RF-013** ("IDs … ocultos en la interfaz") y el **Principio II** (IDs no editables ni visibles en pantallas
  normales), y no trata el campo como la referencia al catálogo que declara data-model (`TipoDocumentoId →
  TipoDocumento`). No es un problema de contrato ni de API ni un gap de especificación.
- **Severidad**: Alta: sin conocer UUIDs, en la práctica no se podía dar de alta una compañía desde la interfaz.
- **Estado inicial**: ANALYZING.

### 16.3 Análisis

- **Componente afectado**: `frontend/src/features/companies/CompaniaFormulario.tsx`, compartido por Nueva
  compañía y Editar compañía (abierto desde `CompaniasPage.tsx`).
- **Backend y contrato**: `CompaniaRequest.tipoDocumentoId` (`contracts/companies.yaml`, UUID obligatorio) ya
  espera el ID del maestro. No requieren cambios.
- **Fuente del catálogo**: `GET /api/maestros/tipos-documento?estado=ACTIVO` mediante
  `useMaestro('tipos-documento', 'ACTIVO')` (`MasterItem { id, nombre, estado }`).
- **Patrón reutilizado**: el mismo de `PersonaFormulario.tsx`: `<select>` con "Seleccione…", `value={tipo.id}`,
  texto `{tipo.nombre}`, solo valores ACTIVOS (RF-032) y zod `min(1)`.

### 16.4 Cambio realizado

- En `CompaniaFormulario.tsx`:
  - el `<input type="text">` pasa a ser un `<select>` alimentado por `useMaestro('tipos-documento', 'ACTIVO')`,
    con opción inicial "Seleccione…", `value={tipo.id}` y texto visible `{tipo.nombre}`;
  - validación `z.string().min(1, 'Seleccione el tipo de documento.')` en lugar de `z.string().uuid(...)`;
  - se retiró la ayuda "Identificador del maestro de tipos de documento.";
  - **ajuste necesario detectado en T280**: el catálogo llega después del primer render, cuando el `<select>` ya
    no puede mostrar el valor inicial. Al cargarse, un `useEffect` reaplica el `tipoDocumentoId` actual si está
    entre los tipos ACTIVOS (así se ve preseleccionado con su nombre en edición). Si no lo está (inactivo o no
    resoluble), lo deja vacío, de modo que hay que elegir un tipo activo para guardar y nunca se envía un id que
    el usuario no ve. Sin este ajuste, la edición mostraba "Seleccione…" aunque el formulario conservaba el id.
- Se sigue enviando `tipoDocumentoId` (el UUID) al backend: el payload no cambia.
- **Sin cambios** en backend, API, contratos, requisitos, modelo de datos, migraciones, autorización ni reglas de
  negocio.
- **Archivos modificados**:
  - `frontend/src/features/companies/CompaniaFormulario.tsx` (30 líneas añadidas y 9 eliminadas: el campo de texto,
    su ayuda, la validación `uuid` y el import de tipos);
  - `frontend/tests/unit/CompaniasPage.test.tsx` (simulación del maestro, 2 pruebas ajustadas y 4 nuevas);
  - `specs/001-control-acceso-empresarial/tasks.md` (bloque POST-BASELINE — VF-001, T279–T281);
  - este registro.

### 16.5 Pruebas

En `CompaniasPage.test.tsx`, con `listarMaestro` simulado (RUC y DNI activos):

- **Ajustadas**: `crea una compañía con su clasificación` y `explica el conflicto cuando el documento ya está
  registrado` escribían el UUID en el campo, es decir, codificaban el comportamiento defectuoso. Ahora eligen
  "RUC" por su nombre y siguen comprobando que se envía el id.
- **Nuevas**:
  1. el campo es un selector que ofrece `Seleccione…`, `RUC` y `DNI`, sin UUIDs ni la ayuda retirada;
  2. crear sin elegir tipo muestra "Seleccione el tipo de documento." y no llama al servidor;
  3. al editar, el tipo actual aparece preseleccionado como "RUC" y se envía su mismo id;
  4. al editar una compañía con un tipo no disponible, se muestra "Seleccione…" sin el UUID, guardar exige elegir
     un tipo y, tras elegir "DNI", se envía el id de DNI.

**Prueba negativa**: con el formulario original, fallan exactamente estas 6 pruebas (las 2 ajustadas y las 4
nuevas) y pasan las 5 no relacionadas.

### 16.6 Validación (T281)

| Verificación | Resultado |
|---|---|
| `CompaniasPage.test.tsx` | 11/11 |
| Prueba negativa (sin la corrección) | 6 fallos, como se esperaba |
| Vitest completo | 183/183 (19 archivos) |
| Typecheck (`tsc -b`) | Sin errores |
| ESLint sobre los archivos de VF-001 | Sin errores ni avisos |
| ESLint completo | 1 error y 4 avisos **preexistentes y no relacionados** (`PertenenciaContextoWizard.tsx`, `CamposAsignacion.tsx`) |

### 16.7 Trazabilidad

| Elemento | Relación |
|---|---|
| VF-001 | Hallazgo de origen |
| RF-013 / Principio II | IDs ocultos y no editables en la interfaz: **ahora se cumple** en el formulario de compañía |
| data-model, `Compañía.TipoDocumentoId` | Referencia al maestro `TipoDocumento` |
| RF-006, RF-031, RF-032, RF-041 | Tipo de documento de la compañía; catálogo; solo valores ACTIVOS; unicidad: **sin cambios** |
| T060 | Tarea del Baseline que implementó el mantenimiento de compañías (no se modifica) |
| T279, T280, T281 | Bloque POST-BASELINE — VF-001 |

### 16.8 Fuera de alcance (por decisión aprobada)

`CompaniaService` no comprueba que el tipo de documento exista y esté activo (a diferencia de `PersonaService`,
que usa `ExigirActivoAsync<TipoDocumento>`), y `Compania.TipoDocumentoId` no tiene clave foránea en base de datos.
**No se implementa en VF-001 ni se abre un hallazgo nuevo en este bloque.** Queda anotado aquí solo como
contexto: el selector corrige la vía normal desde la interfaz, pero no sustituye una validación del servidor.

### 16.9 Alcance y Baseline

- Sin cambios de backend, API, contratos, requisitos (spec.md), modelo de datos, migraciones, autorización ni
  decisiones del Baseline.
- No se modificaron T001–T278. No se crearon tareas más allá de T281.
- No se abrió Stage 2 ni ningún otro hallazgo.

---

## 17. Análisis de VF-002 — Árbol de Unidades Organizativas

### 17.1 Estado

**CLOSED** (2026-09-25).

| Estado | Detalle |
|---|---|
| ANALYZING | Causa localizada en el componente compartido `Tree` (§17.3). |
| FIXED | Expansión y contracción con el ratón (T282) y pruebas de componente y de página (T283), en el bloque POST-BASELINE — VF-002 de tasks.md. |
| VALIDATED | Validación técnica (T284): pruebas relevantes, prueba negativa sin la corrección, Vitest completo, typecheck y ESLint (§17.6). |
| CLOSED | Cierre documental (T284). |

### 17.2 Hallazgo y clasificación

- **Hallazgo**: el control de unidades organizativas "no se puede interactuar" y debería ser un árbol que permita
  desplegar nodos y ramas según la jerarquía.
- **Clasificación**: **defecto de implementación (frontend)**. Se registró inicialmente como "UX" y se
  reclasificó tras el análisis. El árbol **ya existía** (RF-008 cumplido): la página usa el componente compartido
  `Tree` (patrón ARIA `treeview`) con la jerarquía anidada que entrega el backend. Lo que fallaba era la
  interacción con el ratón: el clic solo seleccionaba, el indicador `▸`/`▾` era decorativo y la expansión solo
  funcionaba con el teclado (flechas), con el árbol inicialmente contraído. Eso incumple **RF-036** ("Los árboles
  DEBEN permitir expandir, contraer y seleccionar nodos") y ux-ui.md §14 ("expandir/contraer").
- **Severidad**: Alta: con ratón no se podía recorrer la jerarquía más allá de las raíces.
- **Estado inicial**: ANALYZING.

### 17.3 Análisis

- **Página**: `frontend/src/features/org-units/UnidadesOrganizativasPage.tsx` (selector de Compañía Principal y
  `<Tree>` con acciones de crear y mover sobre el nodo seleccionado).
- **Componente causante**: `frontend/src/components/Tree/Tree.tsx` (T022 del Baseline, "navegable por teclado").
- **Backend y contrato**: `GET /api/unidades-organizativas/arbol` (`org-units.yaml`) ya devuelve el árbol anidado
  (`NodoArbol { id, nombre, estado, hijos }`) "para el control de árbol del frontend, RF-036". No requieren cambios.
- **Alcance y seguridad**: el árbol se obtiene por Compañía Principal y el backend verifica el alcance sobre esa
  Principal (`404` fuera de alcance). Al funcionar por árbol completo, no puede aparecer un hijo sin su padre. El
  árbol solo representa lo recibido y no es frontera de autorización.

### 17.4 Cambio realizado

- En `Tree.tsx`, el indicador de los nodos con hijos pasa a ser interactivo: un clic alterna expandido/contraído
  con `stopPropagation`, así que **no selecciona**. El clic en el nombre **sigue seleccionando** sin expandir. El
  indicador conserva `aria-hidden` y queda fuera del orden de tabulación, porque el teclado ya cubre la expansión
  con las flechas (patrón ARIA `treeview`). Las hojas (`•`) no tienen acción.
- **Estado inicial**: sigue contraído, sin props nuevas ni auto-expansión (decisión aprobada).
- **Sin cambios** en la API pública del componente, la navegación por teclado, la accesibilidad existente, el
  backend, los contratos, el modelo, la autorización ni las dependencias (sin librerías nuevas).
- **Archivos modificados**:
  - `frontend/src/components/Tree/Tree.tsx` (16 líneas añadidas, 3 eliminadas: el indicador decorativo);
  - `frontend/tests/unit/Tree.test.tsx` (5 pruebas nuevas);
  - `frontend/tests/unit/UnidadesOrganizativasPage.test.tsx` (1 prueba nueva);
  - `specs/001-control-acceso-empresarial/tasks.md` (bloque POST-BASELINE — VF-002, T282–T284);
  - este registro.

### 17.5 Pruebas

- **`Tree.test.tsx`**, bloque `Tree — interacción con el ratón (VF-002, RF-036)`:
  1. el árbol empieza contraído y solo muestra las raíces;
  2. el indicador expande y contrae la rama sin seleccionar el nodo;
  3. se pueden bajar varios niveles expandiendo cada rama (`aria-level` 3);
  4. el clic en el nombre selecciona sin expandir;
  5. una hoja no ofrece expandir: su marcador no hace nada y el clic la selecciona.
- **`UnidadesOrganizativasPage.test.tsx`**: el hijo de la jerarquía solo aparece tras expandir la raíz con el
  ratón, y seleccionarlo habilita las acciones sobre la unidad seleccionada.
- **Prueba negativa**: con el `Tree` original fallan las 3 pruebas de expansión con ratón (2, 3 y la de la
  página). Las otras 3 pasan en ambos casos porque protegen el comportamiento que se conserva (estado inicial,
  selección por nombre y hojas).
- Las 20 pruebas de teclado y accesibilidad existentes del `Tree` no se modificaron y siguen en verde.

### 17.6 Validación (T284)

| Verificación | Resultado |
|---|---|
| `Tree.test.tsx`, `Tree.a11y.test.tsx`, `UnidadesOrganizativasPage.test.tsx` | 35/35 |
| Prueba negativa (sin la corrección) | 3 fallos, como se esperaba |
| Pantallas que comparten `Tree` (`AreasAccesoPage`, `AsignacionUnidadOrganizativa`) | 20/20 |
| Vitest completo | 189/189 (19 archivos) |
| Typecheck (`tsc -b`) | Sin errores |
| ESLint sobre los archivos de VF-002 | Sin errores ni avisos |
| ESLint completo | 1 error y 4 avisos **preexistentes y no relacionados** (`PertenenciaContextoWizard.tsx`, `CamposAsignacion.tsx`) |

### 17.7 Trazabilidad

| Elemento | Relación |
|---|---|
| VF-002 | Hallazgo de origen |
| RF-036 | Expandir, contraer y seleccionar: **ahora se cumple también con el ratón** |
| RF-008, RF-007 | Unidades organizativas como árbol, con unidad superior: sin cambios |
| ux-ui.md §14 | "Expandir/contraer" y "selección": cumplidos |
| T022, T061 | Tareas del Baseline del componente `Tree` y de la pantalla de unidades (no se modifican) |
| T282, T283, T284 | Bloque POST-BASELINE — VF-002 |

### 17.8 Relación con otros hallazgos y observaciones

- **VF-003 (Áreas de acceso)**: `Tree` es compartido, así que Áreas de acceso y la asignación de unidad
  organizativa heredan la expansión con el ratón. Por decisión aprobada, **VF-003 permanece como hallazgo
  independiente**, pendiente de su propio análisis, y **no se cierra** por este bloque.
- **Observación pendiente (fuera de VF-002)**: ux-ui.md §14 también enumera **búsqueda** y **breadcrumb** para el
  árbol, que no están implementados. No forman parte del texto de VF-002, no se implementan y no se abre un
  hallazgo nuevo por ellos.

### 17.9 Alcance y Baseline

- Sin cambios de backend, API, contratos, requisitos (spec.md), modelo de datos, autorización ni dependencias.
- No se modificaron T001–T281. No se crearon tareas más allá de T284.
- No se abrió Stage 2 ni ningún otro hallazgo.

---

## 18. Análisis de VF-003 — Árbol de Áreas de acceso

### 18.1 Estado

**CLOSED** (2026-09-25).

| Estado | Detalle |
|---|---|
| ANALYZING | Causa identificada: la misma que VF-002, en el componente compartido `Tree` (§18.3). |
| FIXED | Corrección de código aplicada en **T282** (bloque de VF-002). VF-003 se mantuvo independiente y T285 aporta las pruebas específicas de Áreas de acceso. |
| VALIDATED | Validación técnica (T286): pruebas de la pantalla, del `Tree` y de áreas relacionadas, prueba negativa con el `Tree` anterior a T282, Vitest completo, typecheck y ESLint (§18.6). |
| CLOSED | Cierre documental (T286). |

### 18.2 Hallazgo y clasificación

- **Hallazgo**: el control de áreas de acceso "no se puede interactuar" y debería ser un árbol que permita
  desplegar nodos y ramas según la jerarquía.
- **Clasificación**: **defecto de implementación (frontend)**. Se registró inicialmente como "UX" y se
  reclasificó tras el análisis. El árbol **ya existía** (RF-009, T125): `AreasAccesoPage` usa el componente
  compartido `Tree` con la jerarquía anidada que entrega el backend. Con el ratón no se podía expandir ni
  contraer, porque el indicador `▸`/`▾` era decorativo y la expansión solo funcionaba con el teclado. Eso
  incumple **RF-036**. **Es la misma causa raíz que VF-002.**
- **Severidad**: Alta (la registrada): con ratón no se podía recorrer la jerarquía de áreas, que gobierna la
  configuración de tipos de persona y permisos (UX-07).
- **Estado inicial**: ANALYZING.

### 18.3 Análisis

- **Página**: `frontend/src/features/area-access/AreasAccesoPage.tsx`. Selector de Compañía Principal, `<Tree>`
  y panel de acciones (crear raíz o bajo la seleccionada, mover) más el panel de tipos de persona del área
  seleccionada (RF-019). Su adaptador `aNodosArbol` es idéntico al de unidades organizativas.
- **Backend y contrato**: `GET /api/areas-acceso/arbol` (`area-access.yaml`, "árbol anidado… (RF-036)").
  `AreaAccesoService.ObtenerArbolAsync` comprueba que la Principal esté en el alcance y construye el árbol completo
  de esa Principal. El alcance funciona por árbol completo y RF-046 garantiza que un área hija comparte la Principal
  de su padre, así que no puede aparecer un hijo sin su padre. No requieren cambios.
- **Modelo**: `ÁreaAcceso.ÁreaSuperiorId` (FK a sí misma, `null` ⇒ raíz) y `CompañíaPrincipalId` (RF-046).
- **Estado tras T282–T284**: la causa desapareció en el componente compartido. No se encontró ningún defecto
  adicional propio de Áreas de acceso necesario para cumplir RF-009, RF-036, RF-038, RF-046 ni RF-019. VF-003 no
  se cerró por arrastre de VF-002: quedaba pendiente la **evidencia específica** de esta pantalla, porque su prueba
  de jerarquía expandía solo con el teclado.

### 18.4 Cambio realizado

- **Corrección de código**: la de **T282** en `frontend/src/components/Tree/Tree.tsx` (VF-002, §17.4). En este
  bloque **no se modificó código de producción**: ni `Tree.tsx`, ni `AreasAccesoPage.tsx`, ni backend, contratos,
  modelo o dependencias.
- **Archivos modificados en este bloque**:
  - `frontend/tests/unit/AreasAccesoPage.test.tsx` (4 pruebas nuevas; las 11 existentes, incluida la de teclado,
    sin cambios);
  - `specs/001-control-acceso-empresarial/tasks.md` (bloque POST-BASELINE — VF-003, T285–T286);
  - este registro.

### 18.5 Pruebas (T285)

En `AreasAccesoPage.test.tsx`, bloque `árbol de áreas con el ratón (VF-003)`, con la jerarquía `Planta
Concentradora > Molienda > Chancado`. Las búsquedas se acotan al árbol (`role="tree"`), porque los nombres también
aparecen en el selector "Nueva área superior":

1. el árbol empieza contraído, `▸` despliega la raíz y muestra "Molienda", y `▾` la vuelve a contraer;
2. se recorre un segundo nivel ("Chancado" con `aria-level` 3);
3. el clic en el nombre selecciona el área (`aria-selected`) sin desplegarla;
4. seleccionar el área hija "Molienda" habilita "Mover" y "Crear bajo la seleccionada", y muestra el panel
   "Tipos de persona autorizados" **de esa área**: nombre "Molienda" en el panel, consulta de tipos con el id del
   hijo y el tipo "Operario" marcado (RF-019).

**Prueba negativa**: con el `Tree` anterior a T282 restaurado temporalmente, fallan las 3 pruebas de expansión con
ratón (1, 2 y 4). La 3 pasa en ambos casos porque protege el comportamiento que se conserva, y las 11 existentes
también pasan. Después, `Tree.tsx` se restauró al estado de T282 y el hash de su diff quedó idéntico al anterior.

### 18.6 Validación (T286)

| Verificación | Resultado |
|---|---|
| `AreasAccesoPage.test.tsx` | 15/15 |
| `AreasAccesoPage`, `Tree`, `Tree.a11y` y `TiposPersonaPorArea` | 49/49 |
| Prueba negativa (`Tree` anterior a T282) | 3 fallos, como se esperaba |
| Vitest completo | 193/193 (19 archivos) |
| Typecheck (`tsc -b`) | Sin errores |
| ESLint sobre el archivo afectado | Sin errores ni avisos |
| ESLint completo | 1 error y 4 avisos **preexistentes y no relacionados** (`PertenenciaContextoWizard.tsx`, `CamposAsignacion.tsx`) |

### 18.7 Trazabilidad

| Elemento | Relación |
|---|---|
| VF-003 | Hallazgo de origen (independiente de VF-002) |
| RF-009 | Áreas de acceso como árbol: cumplido desde T125 |
| RF-036 | Expandir, contraer y seleccionar: **cumplido también con ratón** (corrección T282, evidencia T285) |
| RF-019, RF-038, RF-046, Historia 6 | Tipos de persona por área, ciclos y Principal del área: sin cambios |
| T125 | Tarea del Baseline de la pantalla de áreas (no se modifica) |
| T282 | Corrección de código compartida (bloque de VF-002) |
| T285, T286 | Bloque POST-BASELINE — VF-003 |

### 18.8 Fuera de alcance

- **Búsqueda y breadcrumb** (ux-ui.md §14): ya registrados como observación pendiente en §17.8.
- **Auto-expansión del primer nivel**: descartada por decisión aprobada en VF-002.
- **Selector "Nueva área superior"**: lista plana de nombres, sin jerarquía ni forma de distinguir nombres
  repetidos. No forma parte de VF-003, no se modifica y no se abre un hallazgo.

### 18.9 Alcance y Baseline

- Sin cambios de código de producción, backend, API, contratos, requisitos (spec.md), modelo de datos,
  autorización ni dependencias.
- No se modificaron T001–T284. No se crearon tareas más allá de T286.
- No se abrió Stage 2 ni ningún otro hallazgo.

---

## 19. Análisis de VF-004 — Vigencia diaria del permiso de acceso

### 19.1 Estado

**CLOSED** (cierre registrado el 2026-09-26). ~~**VALIDATED** (técnicamente, 2026-09-25). Pendiente de la
validación funcional del usuario en la interfaz para pasar a **CLOSED**.~~ La validación funcional manual de la
interfaz se completó con resultado satisfactorio.

| Estado | Detalle |
|---|---|
| ANALYZING | Análisis completado; opción 2 aprobada por negocio; decisiones F-1 a F-7 cerradas en clarify y registradas en spec.md (Sesión 2026-09-25 VF-004, RF-083, CS-044–CS-047). Plan técnico en plan.md ("Plan post-Baseline VF-004", WP-1 a WP-12) y research.md §36; data-model.md (duodécima revisión), `contracts/permissions.yaml` v2.0.0, quickstart.md §10 y ux-ui.md §18 actualizados. Tres pasadas de `/speckit-analyze` corregidas antes de implementar. |
| FIXED | Implementado como cambio de requisito post-Baseline mediante el bloque POST-BASELINE — VF-004 de tasks.md, **T287–T311**, con cierre documental en T312. T001–T286 intactas y ninguna tarea posterior a T312. |
| VALIDATED | Validación **técnica** (T309–T311): las cinco suites en verde (tabla siguiente), regresión dirigida de T310 y verificación de regresión y de datos existentes de T309. |
| CLOSED | Validación funcional **manual** de la interfaz realizada por el usuario, con resultado satisfactorio: alta y edición de permisos con controles de fecha, listado con fechas o con fecha y hora, y rechazos de contención. La implementación se integró en `main` mediante el commit `244fa09` (*feat: complete post-baseline functional validation*), el PR #5 y el merge `6a72260`. |

Evidencia automatizada:

| Suite | Resultado | Cobertura de VF-004 |
|---|---|---|
| Unitarias backend | 155/155 | `VigenciaDiariaPermisoTests` (15: Lima, Santiago con día sin 00:00 y día de 25 h, Apia, fecha civil, días completos, edición por extremo); `ContencionFechaCivilPermisoTests` (3); `ReglaContencionPermisoTests` (matriz D4 sin cambios) |
| Integración backend (SQL Server real, Testcontainers) | 593/593 | CS-044/CS-045: `VigenciaDiariaPermisosTests` (11); RF-082 por fecha civil y aislamiento: `ContencionPermisoFechaCivilTests` (7); CS-046/CS-047 e instantes invertidos: `PermisosHistoricosVigenciaDiariaTests` (7); regresión de RF-072, perfiles, evaluación, revocación y bloques sin cambios |
| Contrato (incluido el snapshot OpenAPI) | 252/252 | `permissions.yaml` v2.0.0 respaldado por la API (formatos `date`/`date-time`, campos nuevos de la respuesta) |
| Vitest | 203/203 | `PermisosPage.test.tsx` (casos v2.0.0 y VF-004), `fechas.test.ts` |
| Playwright (sobre la API reconstruida) | 9/9 | `cs009-quickstart` y `multi-principal-quickstart` crean permisos con la v2.0.0 |

Regresión dirigida (T310), cada alteración introducida por separado y deshecha antes de la siguiente:

| Alteración temporal | Pruebas que la detectan |
|---|---|
| (a) `InicioUtc`/`FinUtc` con el día UTC (`Vigencia.NormalizarInicio/Fin`) | 8 de 15 en `VigenciaDiariaPermisoTests` (Lima, Santiago, Apia…); 4 de 11 en `VigenciaDiariaPermisosTests` (incluida la persistencia exacta de CS-044) |
| (b) `ContenerEnPertenenciaAsync` con `ValidarAsync` por instantes | Exactamente los 2 casos previstos de `ContencionPermisoFechaCivilTests`: fin = último día (Lima) e inicio = primer día (`Asia/Tokyo`) |
| (c) `ResolverExtremo` que siempre normaliza | 2 de 15 en `VigenciaDiariaPermisoTests` (conservación); 5 de 7 en `PermisosHistoricosVigenciaDiariaTests` |

Tras restaurar, `VigenciaDiariaPermiso.cs`, `ContencionTemporalValidator.cs` y `PermisoAccesoService.cs`
coinciden por hash con la implementación correcta, y las suites afectadas vuelven a verde.

Sin cambios en `EvaluadorDeAcceso`, `EvaluacionAccesoService`, `BloqueHorarioPermiso`, `Vigencia`, la cascada,
los servicios de RF-072 y de perfiles, ni en la lógica de `PermisoAcceso.EstaVigenteEn`, que solo cambia en
comentarios. `ContencionTemporalValidator` solo gana líneas. Sin migraciones, cambios de configuración de EF
ni scripts de conversión de datos.

### 19.2 Hallazgo y clasificación

- **Hallazgo**: la vigencia del permiso debería ser solo de fecha, y el control debería mostrar solo la fecha.
- **Comportamiento aprobado por el Baseline**: Historia 8 ("fecha/hora de inicio", "fecha/hora de fin") y
  RF-029 ("los campos de fecha/hora DEBEN manejar fecha y hora") exigían fecha y hora. La implementación
  (`datetime-local`, instantes UTC sin normalizar, contrato `date-time`) los cumplía.
- **Clasificación**: **cambio de requisito post-Baseline**, no defecto. Se registró inicialmente como defecto
  funcional y se reclasificó tras el análisis. La causa anotada, "comportamiento esperado no especificado", no
  era exacta: el comportamiento estaba especificado en sentido contrario.
- **Decisión funcional**: opción 2. La vigencia del permiso son dos fechas civiles, cada una un día completo en
  la zona de la Principal propietaria del área. La precisión dentro del día queda en los bloques horarios
  (RF-022). Se descartó la opción "solo presentación".

### 19.3 Decisiones de clarify (F-1 a F-7)

| Decisión | Contenido |
|---|---|
| F-1 = A | Inicio = primer instante válido del día local; fin = primer instante válido del día siguiente − 1 ms. La evaluación se mantiene como `inicio <= instante < fin`. |
| F-2 = A | Contención de RF-082 para el permiso por fecha civil contra fecha civil de la pertenencia. RF-072 y las demás entidades no cambian. |
| F-3 = B | Contrato de creación y actualización con `format: date`; las respuestas pueden exponer los instantes UTC. Cambio incompatible y nueva versión mayor de `permissions.yaml`. |
| F-4 = B | Los permisos existentes conservan sus instantes; sin migración, reinterpretación ni corrección masiva. |
| F-5 = B | Los permisos con límites de día completo se muestran solo con fecha; los demás, con fecha y hora. |
| F-6 = A | Al editar, cada extremo por separado: la fecha civil sin cambios conserva el instante y la que cambia se normaliza. Se decide en el servidor. |
| F-7 = A | Un cambio de zona no modifica instantes; la fecha presentada se recalcula con la zona actual. |

### 19.4 Requisitos afectados (spec.md)

| Elemento | Cambio |
|---|---|
| Historia 8 | Lista de campos: "fecha/hora" pasa a fecha de inicio y de fin (texto anterior tachado y anotado); párrafo nuevo sobre la vigencia diaria. |
| RF-021 | Complementado: inicio y fin como fechas civiles. |
| RF-029 | Matizado: excepción para la vigencia de `PermisoAcceso`; no se pronuncia sobre otras entidades. |
| RF-080 | Complementado: la zona de la Principal convierte las fechas del permiso; la evaluación sigue en UTC. |
| RF-082 | Matizado: comparación por fecha civil solo para `PermisoAcceso`. |
| RF-083 | **Nuevo**: vigencia diaria del permiso (conversión, evaluación, contención, edición, contrato, presentación, registros existentes, cambio de zona). |
| CS-042 | Anotado: para el permiso, las fechas son civiles. |
| CS-044 a CS-047 | **Nuevos**. |

Sin cambios: RF-016, D5, RF-022, RF-072, RF-061 a RF-065, y las decisiones D1 a D4 de VF-007 (solo
matizadas para el permiso en RF-082 y RF-083).

### 19.5 ~~Pendiente para `/speckit-plan`~~ Resuelto en `/speckit-plan`

- `contracts/permissions.yaml` v2.0.0 y data-model.md (duodécima revisión) alineados con RF-083.
- Contrato: petición con `fechaInicioVigencia`/`fechaFinVigencia` (`date`); respuesta con los instantes UTC
  efectivos, las fechas civiles, `vigenciaEnDiasCompletos` y `zonaHorariaIana` (research.md §36.5).
- Fecha declarada de la pertenencia: componentes de fecha UTC de sus instantes normalizados (research.md
  §36.4).
- research.md §36, ux-ui.md §18 y quickstart.md §10 actualizados.
- Hasta la implementación, `OpenApiSnapshotTests` y `PermisosContractTests` quedan en rojo por el cambio de
  contrato (estado esperado).

### 19.6 Observación independiente (fuera de VF-004)

D5 y RF-016 interpretan el día de las vigencias diarias (pertenencias, contextos, unidades, credenciales,
perfiles) en la zona de la Principal. La implementación (`Vigencia.NormalizarRango`) y el frontend
(`${fecha}T00:00:00Z`) lo hacen en el **día UTC**, como ya indicaban §9.7 y research.md §35.5. RF-083 (c) no
depende de esa diferencia, porque compara con el día que la pertenencia declara. Corregir esa representación
queda **fuera de VF-004** por decisión expresa y no se abre un hallazgo.

### 19.7 Fuera de alcance

- Corregir la representación UTC de pertenencias, contextos, unidades, credenciales o perfiles (§19.6).
- Modificar RF-022 para cubrir el tramo 23:59–24:00, que ningún bloque horario puede cubrir hoy.
- Cambios de cascada (RF-061 a RF-065).
- Migraciones de datos existentes.

### 19.8 Alcance y Baseline

- Documentación: spec.md y este registro (clarify); plan.md, research.md, data-model.md,
  `contracts/permissions.yaml`, quickstart.md y ux-ui.md (plan); tasks.md (bloque T287–T312) y quickstart.md
  §10–§11 (cierre, T312).
- Código de producción (T287–T293, T303–T306):
  - backend: `VigenciaDiariaPermiso.cs` (nuevo), `PermisoAccesoService.cs`, `ReglaContencionPermiso.cs`,
    `ContencionTemporalValidator.cs` (solo añade), `PermisoAccesoDtos.cs`, `PermisosRequestValidators.cs` y
    `PermisoAcceso.cs` (solo comentarios);
  - frontend: `lib/fechas.ts`, `permissions/api.ts`, `PermisoFormulario.tsx` y `PermisosPage.tsx`.
- Pruebas (T294–T302, T307, T308): unitarias, de integración, de contrato, Vitest y E2E, según tasks.md.
- No se modificaron T001–T286. No se creó ninguna tarea posterior a T312.
- No se abrió Stage 2 ni ningún otro hallazgo.

---

## 20. Análisis de VF-006 — Unidad organizativa en el contexto operativo

### 20.1 Estado

**CLOSED** (cierre registrado el 2026-09-26).

| Estado | Detalle |
|---|---|
| ANALYZING | El hallazgo pedía poder elegir la unidad organizativa al registrar el contexto operativo, idealmente en un diálogo con toda la estructura. |
| VALIDATED | Validación funcional **manual** de la interfaz realizada por el usuario: el comportamiento esperado ya estaba disponible. |
| CLOSED | Cerrado **sin cambios de código ni tareas nuevas**. |

### 20.2 Clasificación y evidencia

- **Clasificación**: comportamiento correcto; no es un defecto.
- **Evidencia en el repositorio**: en el historial de la persona, cada contexto operativo muestra el componente
  `AsignacionUnidadOrganizativa`, que presenta el árbol de unidades de su Compañía Principal (componente `Tree`)
  para elegir la unidad vigente (Historia 5, casos A y B, decisión D7). Lo cubre la prueba E2E
  `historia5-casos-a-b.spec.ts` ("CS-021: la unidad organizativa se elige sobre el árbol, no sobre una lista
  plana").
- **Observación**: la selección se hace sobre el árbol dentro del propio contexto, no en un diálogo modal. La
  validación manual confirmó que esa forma cubre la necesidad del hallazgo.

---

## 21. Análisis de VF-008 — Perfiles simultáneos de la persona

### 21.1 Estado

**CLOSED** (cierre registrado el 2026-09-26).

| Estado | Detalle |
|---|---|
| ANALYZING | El hallazgo indicaba que se permitía el solapamiento de perfiles (tipos de persona) y pedía poder eliminarlos. |
| VALIDATED | Validación funcional **manual** de la interfaz realizada por el usuario. |
| CLOSED | Cerrado **sin cambios de código ni tareas nuevas**. |

### 21.2 Clasificación y evidencia

- **Clasificación**: comportamiento conforme a la especificación; no es un defecto.
- **Solapamiento**: RF-011 establece que "una persona DEBE poder tener uno o varios tipos/perfiles", y el
  modelo trata los perfiles como múltiples y sin exclusividad (spec.md, Supuestos). Que dos perfiles coincidan
  en el tiempo es el comportamiento especificado. Desde VF-007 (RF-082), cada perfil queda además contenido en
  la pertenencia vigente de la persona.
- **Eliminación de perfiles**: la API de perfiles expone solo `GET` y `POST /api/personas/{id}/perfiles`, y la
  especificación no define su eliminación ni su desactivación. Queda **fuera del alcance del proyecto cerrado**;
  no se implementó ni se abre como tarea.

---

## 22. Análisis de VF-009 — Contención de la credencial en la pertenencia

### 22.1 Estado

**CLOSED** (cierre registrado el 2026-09-26).

| Estado | Detalle |
|---|---|
| ANALYZING | El hallazgo pedía limitar la vigencia de la credencial a la de la pertenencia vigente de la persona. |
| VALIDATED | Validación funcional **manual** de la interfaz realizada por el usuario: el comportamiento esperado ya existía. |
| CLOSED | Cerrado **sin cambios de código ni tareas nuevas**. |

### 22.2 Clasificación y evidencia

- **Clasificación**: comportamiento correcto; no es un defecto.
- **Evidencia en el repositorio**: `AsignaciónCredencial` está sujeta a la contención temporal de RF-072 desde el
  Baseline (`ContencionTemporalValidator`, `CredencialService`), con pruebas de contención en
  `ContencionTemporalTests`. Durante la validación funcional de VF-007 (§9.1) ya se había comprobado en la
  interfaz que la asignación de credenciales respeta esa contención.

---

## 23. Cierre de la validación funcional post-Baseline

**CLOSED.** Los once hallazgos siguieron el ciclo OPEN → ANALYZING → FIXED → VALIDATED → CLOSED. Los que
estaban correctos desde el inicio pasaron de VALIDATED a CLOSED sin corrección.

| Hallazgo | Resultado | Tareas | Estado final |
|---|---|---|---|
| VF-001 | Corregido (selector de tipo de documento) | T279–T281 | CLOSED |
| VF-002 | Corregido (árbol de unidades organizativas) | T282–T284 | CLOSED |
| VF-003 | Evidencia propia de la corrección de T282 | T285–T286 | CLOSED |
| VF-004 | Cambio de requisito implementado (RF-083) | T287–T312 | CLOSED |
| VF-005 | Corregido (placeholder del buscador) | T267–T269 | CLOSED |
| VF-006 | Comportamiento correcto, sin cambios | — | CLOSED |
| VF-007 | Cambio de requisito implementado (RF-082) | T243–T266 | CLOSED |
| VF-008 | Conforme a RF-011; eliminación de perfiles fuera de alcance | — | CLOSED |
| VF-009 | Comportamiento correcto (RF-072), sin cambios | — | CLOSED |
| VF-010 | Corregido (alineación de radios de rol) | T270–T272 | CLOSED |
| VF-011 | Corregido (nombre de compañía, incluida la extensión) | T273–T278 | CLOSED |

- **Validación**: pruebas automatizadas (unitarias 155, integración 593, contrato 252, Vitest 203, Playwright 9)
  y validación funcional **manual** de la interfaz.
- **Integración**: el trabajo post-Baseline se consolidó en la rama `feature/post-baseline-validation`, commit
  `244fa09` (*feat: complete post-baseline functional validation*), PR #5, fusionado en `main` con el merge
  `6a72260`. Este registro de cierre se añadió después, en un commit documental propio.
- **Historial**: T001–T242 (Baseline) y los bloques T243–T312 no se reescribieron. VF-006, VF-008 y VF-009 se
  cierran sin tareas nuevas.
- **Fuera del alcance del proyecto cerrado**: las consultas transversales de auditoría e históricos y el
  dashboard (RF-067 a RF-069, diferidos por la decisión D8) y la eliminación de perfiles (§21.2).
- **Observaciones independientes registradas durante la validación**: la §15.8 quedó resuelta con la extensión de
  VF-011 (T276–T278). Las §9.9 (un permiso PERSONA sobre una persona inexistente responde `400`, lo que revela
  su existencia), §17.8 (búsqueda y breadcrumb del árbol) y §19.6 (día UTC en las vigencias diarias ajenas al
  permiso) no se abrieron como hallazgos y quedan documentadas como **limitaciones conocidas, fuera del alcance
  del proyecto cerrado**.

