# Manual de Usuario

**Proyecto**: Enterprise Access Control Platform
**Estado**: **PROYECTO CERRADO** — este manual describe la funcionalidad implementada y validada, no una
propuesta ni un roadmap.
**Versión de este manual**: 1.0 — 2026-09-26
**Dirigido a**: usuarios funcionales (administradores de compañía y operadores del sistema), sin conocimientos
técnicos de programación.

> Las capturas de pantalla no se incluyen en este documento: no existen archivos de imagen versionados en el
> repositorio que puedan reutilizarse fielmente. Cada flujo indica dónde debería insertarse una captura y qué
> debe mostrar, para que quien realice la entrega final las incorpore desde el sistema en funcionamiento.

---

## 1. Introducción

Enterprise Access Control es un sistema web para administrar **quién puede entrar físicamente a qué área**
de las instalaciones de una empresa, en qué horario y con qué credencial. Está pensado para empresas grandes
(por ejemplo, mineras) donde una **Compañía Principal** (la dueña de las instalaciones) recibe servicios de
varias **Compañías Contratistas**, y necesita controlar el acceso de su propio personal y del de sus
contratistas de forma centralizada, con reglas claras de vigencia (desde cuándo y hasta cuándo alguien puede
acceder) y sin depender de procesos manuales.

Toda decisión de acceso la evalúa el sistema en el servidor: ningún control de la pantalla reemplaza esa
verificación.

## 2. Acceso al sistema

### Inicio de sesión

1. Abra la dirección de la aplicación en su navegador.
2. Ingrese su **correo electrónico** y su **contraseña**.
3. Si es su primer inicio de sesión (por ejemplo, como administrador recién creado), el sistema le exigirá
   **cambiar la contraseña** antes de continuar.
4. Si su usuario está **inactivo** o **bloqueado** (por demasiados intentos fallidos), el sistema le impedirá
   ingresar y deberá contactar a un administrador para que revise su estado.

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: pantalla de inicio de sesión, con los campos de correo y contraseña]

### Credenciales

Su acceso a la aplicación (usuario y contraseña) es distinto de la **credencial/fotocheck física** que el
sistema administra para el personal de campo (ver §9). Son dos conceptos independientes.

### Cierre de sesión

Utilice la opción de cierre de sesión disponible en la navegación superior. Su sesión también expira
automáticamente después de un tiempo definido por el administrador del sistema (por defecto, 60 minutos);
pasado ese tiempo deberá iniciar sesión nuevamente.

## 3. Navegación

El sistema organiza sus funciones en un menú lateral con los siguientes módulos, según lo que su rol le
permita ver y usar:

- **Compañías**
- **Unidades organizativas**
- **Áreas de acceso**
- **Permisos**
- **Evaluación de acceso**
- **Personas**
- **Datos maestros**
- **Configuración** → Usuarios y roles administrativos (solo para administradores)

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: menú lateral completo, con todos los módulos visibles para un
administrador global]

> **Nota sobre el alcance actual.** La navegación **no incluye** entradas de "Históricos" ni de "Auditoría"
> transversal, y la página de inicio (`/`) muestra un marcador de panel de indicadores en lugar de
> indicadores agregados. Esa funcionalidad quedó **fuera del alcance del proyecto cerrado** (ver §11 más
> abajo y el documento técnico de cierre).

## 4. Gestión de compañías

Una **Compañía** puede ser:

- **Principal/Mandante**: la empresa dueña de las instalaciones. Puede haber varias Principales en el
  sistema, cada una con su propia estructura organizativa y sus propias áreas de acceso, completamente
  independientes de las demás.
- **Contratista**: presta servicios dentro de instalaciones de una o varias Principales.

### Consultar

El listado de compañías muestra nombre, tipo de documento, número de documento, clasificación
(Principal/Contratista) y estado (Activo/Inactivo). Puede buscar por nombre o número de documento.

### Crear

1. Ingrese nombre, tipo de documento (elegido de una lista, nunca escribiendo un código interno) y número de
   documento.
2. Elija la clasificación: Principal/Mandante o Contratista.
3. Si es Principal, indique su zona horaria (por ejemplo, "America/Lima"): esa zona determina cómo se
   interpretan los horarios de acceso de sus áreas.

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: formulario de nueva compañía, con el selector de tipo de documento
mostrando nombres, no códigos]

### Editar

Los mismos campos son editables, salvo que **cambiar la clasificación de una compañía que ya tiene
dependencias** (áreas propias, unidades organizativas, relaciones con otras compañías, personal asignado,
credenciales) no está permitido hasta resolver esas dependencias primero.

### Tipos y datos maestros

Ver §7 para los catálogos de tipo de documento, tipo de sangre, género, tipo de persona y tipo de credencial
que alimentan los formularios de compañías y personas.

## 5. Unidades organizativas

Las unidades organizativas representan la estructura interna (áreas, gerencias, departamentos) **de una
Compañía Principal específica**. Cada Principal tiene su propio árbol de unidades, completamente aislado del
de cualquier otra Principal.

### Visualizar el árbol

1. Seleccione la Compañía Principal cuya estructura desea ver.
2. El árbol se muestra inicialmente **contraído**, mostrando solo los niveles superiores (raíces).

### Expandir/contraer

- Haga clic en el indicador (▸/▾) junto al nombre de una unidad para desplegar o contraer sus unidades
  hijas.
- Hacer clic directamente sobre el **nombre** de una unidad la selecciona, sin expandirla.
- El árbol también es completamente operable con el teclado (flechas para moverse y expandir/contraer),
  siguiendo el estándar de accesibilidad de árboles.

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: árbol de unidades organizativas con al menos dos niveles expandidos]

### Crear

Puede crear una unidad como raíz (sin unidad superior) o bajo la unidad actualmente seleccionada. El sistema
impide crear una unidad que generara un ciclo (una unidad no puede terminar siendo, directa o
indirectamente, superior de sí misma).

### Editar / seleccionar unidad

Seleccione una unidad en el árbol para editar su nombre o su estado (Activo/Inactivo), o para usarla como
destino al mover otra unidad.

## 6. Áreas de acceso

Las áreas de acceso son la jerarquía física (plantas, zonas, edificios) sobre la que se controla el ingreso.
A diferencia de las unidades organizativas, **cada área pertenece siempre a una única Compañía Principal**,
igual que sus áreas hijas (una unidad hija hereda automáticamente la Principal de su área superior).

### Árbol y jerarquía

El comportamiento de expansión, contracción y selección es idéntico al de unidades organizativas (§5): clic
en el indicador para desplegar, clic en el nombre para seleccionar, navegación completa por teclado.

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: árbol de áreas de acceso con un área seleccionada y su panel de tipos
de persona autorizados visible]

### Creación y edición

Puede crear un área como raíz o bajo el área seleccionada, y moverla dentro del árbol (siempre respetando
que no se generen ciclos y que conserve la Compañía Principal de su rama).

### Relación con permisos

Cada área tiene un panel de **tipos de persona autorizados**: solo las personas con alguno de esos perfiles
vigentes pueden, en principio, tener permisos evaluados sobre esa área (ver §10 y §11).

## 7. Personas

### Búsqueda

El buscador de personas admite nombres, apellidos o número de documento como criterio, y solo muestra
personas dentro del alcance de compañías que su usuario administra.

### Creación

Registre nombres, apellidos, fecha de nacimiento, tipo y número de documento, género, correo electrónico,
tipo de sangre y datos de contacto de emergencia. El número de documento es único en todo el sistema para
cada tipo de documento (no puede repetirse el mismo DNI dos veces, por ejemplo).

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: formulario de nueva persona con todos los campos de identificación]

### Edición e información histórica

El detalle de una persona incluye su historial completo: compañías de pertenencia, contextos operativos,
unidades organizativas, perfiles y credenciales, cada uno con su propia vigencia. Nada de este historial se
borra: cuando algo termina, queda registrado con su fecha de fin y su motivo.

## 8. Pertenencias y contextos

Estos conceptos son el corazón del modelo y conviene entenderlos bien:

- **Pertenencia a compañía**: a qué empresa pertenece la persona (su empleador). Una persona solo puede tener
  **una** pertenencia activa a la vez; si cambia de empresa, la anterior se cierra y se abre una nueva.
- **Contexto operativo**: para qué Compañía Principal trabaja/accede realmente la persona. Si su empleador es
  una Contratista, la persona puede tener **varios contextos operativos simultáneos**, uno por cada Principal
  para la que su empresa tenga una relación vigente. Si su empleador es una Principal, su contexto operativo
  es automáticamente esa misma Principal.
- **Unidad organizativa** y **perfiles/credencial**: se asignan **dentro de cada contexto operativo**, de
  forma independiente entre sí. Una persona que trabaja para dos Principales distintas puede tener una unidad,
  un permiso y una credencial diferentes para cada una, sin que se mezclen.

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: historial de una persona mostrando dos contextos operativos vigentes
simultáneos con Principales distintas]

**Revocación automática.** Si se cierra la pertenencia de una persona con su empresa (por ejemplo, termina su
contrato), el sistema **cierra automáticamente** todos los contextos operativos, unidades organizativas y
credenciales que dependían de esa pertenencia — sin necesidad de hacerlo uno por uno, y sin borrar el
historial.

## 9. Credenciales

La credencial/fotocheck representa el carné físico de la persona **para una Compañía Principal específica**.
Puede tener una credencial vigente distinta por cada Principal para la que trabaje simultáneamente.

- **Asignación**: se elige el tipo de credencial (por ejemplo, "Credencial Contratista" o "Credencial
  Visitante" — el tipo es solo el diseño visual, no una tecnología física) y su vigencia (desde/hasta), que
  no puede exceder la vigencia de la pertenencia vigente de la persona.
- **Vigencia**: si la fecha de fin de la credencial ya pasó, deja de habilitar el acceso, aunque su estado
  administrativo siga marcado como "Asignado" — el sistema no la cambia de estado automáticamente por el
  simple paso del tiempo.
- **Estados**: Asignado, Devuelto (devolución física), Eliminado (baja administrativa) o Revocada
  (automáticamente, por cierre de la pertenencia que la sustentaba).
- **Devolución/revocación**: asignar una nueva credencial **no** cierra automáticamente una anterior: si hay
  solapamiento de fechas con una credencial ya asignada para la misma Principal, el sistema rechaza la nueva
  asignación.

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: sección de credenciales dentro del historial de una persona]

## 10. Permisos

Un permiso de acceso autoriza a una **persona**, una **unidad organizativa** o una **compañía completa** a
entrar a un área durante un período de vigencia y en ciertos bloques horarios.

- **Creación**: elija el área, el alcance (Persona / Unidad organizativa / Compañía) y, según el alcance, la
  persona (buscada por número de documento), la unidad o la compañía beneficiaria.
- **Vigencia**: se define con **fechas** (día de inicio y día de fin); el control de captura solo pide la
  fecha, no una hora.
- **Bloques horarios**: por cada día de la semana en que aplica el permiso, defina la hora de inicio y de fin
  (en la zona horaria de la Compañía Principal dueña del área).
- **Alcance**: si varios permisos aplicables coinciden (por ejemplo, uno de la persona y otro de su unidad),
  siempre gana el más específico: Persona sobre Unidad organizativa, y esta sobre Compañía.

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: formulario de nuevo permiso con alcance Persona, mostrando el buscador
de persona con su placeholder "Ingrese su nro. de documento"]

**Importante sobre la vigencia y la pertenencia.** Mientras la persona tenga una pertenencia o contexto
vigente, el sistema **limita** la vigencia del permiso (y de sus perfiles/credencial) para que no exceda la
fecha de fin de esa pertenencia. Si no existe una pertenencia vigente, el sistema rechaza la operación.

## 11. Evaluación de acceso

Esta pantalla permite consultar, para una persona, un área y una fecha/hora determinadas, si el acceso sería
**CONCEDIDO** o **DENEGADO**, y por qué.

El sistema evalúa, en este orden funcional:

1. Que usted tenga autorización para consultar esa área (según su alcance administrativo).
2. Que la persona y el área existan.
3. Que la Compañía Principal dueña del área esté activa.
4. Que la persona tenga un contexto operativo vigente y legítimo con esa Principal.
5. Que la persona tenga una **credencial vigente** para esa Principal — sin credencial vigente, el acceso se
   deniega antes de mirar ningún permiso.
6. Que el área esté activa.
7. Que algún perfil vigente de la persona esté autorizado en el área.
8. Que exista un permiso aplicable (por persona, unidad o compañía) que cubra la fecha, el día de la semana y
   el bloque horario evaluados.

Si **cualquiera** de estas condiciones falla, el resultado es **DENEGADO**, con un motivo específico (por
ejemplo: "sin contexto operativo vigente", "sin credencial vigente", "compañía inactiva", "fuera de bloque
horario"). El sistema nunca concede acceso "por defecto": ante la duda, deniega.

[CAPTURA DE PANTALLA — INSERTAR AQUÍ: pantalla de evaluación de acceso mostrando un resultado DENEGADO con su
motivo]

## 12. Auditoría

Dentro del alcance del proyecto cerrado, la auditoría existe **a nivel de cada registro**: toda entidad
(persona, compañía, permiso, credencial, etc.) guarda automáticamente quién la creó y quién la modificó por
última vez, y cuándo. Esa información es visible en el detalle de cada registro.

**No existe** (fuera del alcance del proyecto cerrado, ver §11 del documento técnico de cierre) una pantalla
transversal de auditoría que permita filtrar todos los cambios del sistema por usuario, entidad, compañía o
rango de fechas, ni un panel de indicadores agregados en la página de inicio.

## 13. Mensajes y errores frecuentes

| Situación | Mensaje / comportamiento | Qué significa |
|---|---|---|
| Correo o contraseña incorrectos | "Correo o contraseña incorrectos" | Puede ser un error real de credenciales, **o** un problema de conectividad con el servidor (ver el manual de despliegue, sección de troubleshooting) |
| Usuario bloqueado | No permite iniciar sesión | Se alcanzó el número máximo de intentos fallidos configurado; un administrador debe desbloquearlo |
| Cambio de contraseña obligatorio | Se le solicita elegir una nueva contraseña antes de continuar | Es su primer inicio de sesión, o su contraseña expiró según la política vigente |
| "Compañía no disponible" en vez de un identificador | Aparece en confirmaciones y detalles cuando la compañía no puede resolverse dentro de su alcance actual | Nunca se muestra un identificador técnico (UUID) en pantalla; si el nombre no puede mostrarse, se usa este texto |
| Rechazo al asignar un perfil, permiso o credencial fuera de fecha | El sistema indica que la operación excede la vigencia de la pertenencia | La persona no tiene una pertenencia vigente que contenga esas fechas |
| Rechazo por solapamiento de fechas | El sistema indica conflicto de vigencia | Ya existe un registro del mismo tipo (credencial, relación, asignación) que se superpone en el tiempo |
| Acceso denegado / recurso no encontrado | La pantalla muestra "no encontrado" en vez de negar el permiso explícitamente | Por diseño, el sistema no revela si un recurso fuera de su alcance existe o no |

## 14. Capturas de pantalla

Todas las capturas requeridas se señalan en línea, dentro de cada flujo de las secciones 2 a 11, con el
formato `[CAPTURA DE PANTALLA — INSERTAR AQUÍ: …]` y una descripción de qué debe mostrarse. Ninguna captura
se incluye aquí de forma inventada: deben tomarse directamente del sistema desplegado (ver
[`01-manual-despliegue-implementacion.md`](01-manual-despliegue-implementacion.md)) antes de la entrega
final impresa o publicada de este manual.

## 15. Flujos principales

### Crear una compañía

1. Módulo **Compañías** → "Nueva compañía".
2. Complete nombre, tipo y número de documento, clasificación.
3. Si es Principal, defina su zona horaria.
4. Guarde.

### Crear una unidad organizativa

1. Módulo **Unidades organizativas** → seleccione la Compañía Principal.
2. "Nueva unidad" (raíz o bajo la seleccionada).
3. Complete nombre y guarde.

### Crear una persona

1. Módulo **Personas** → "Nueva persona".
2. Complete datos personales, de identificación y de contacto.
3. Guarde.

### Asignar una pertenencia

1. Abra el detalle de la persona → pestaña de historial de compañía.
2. "Nueva pertenencia": elija la compañía y el período de vigencia.
3. Si existía una pertenencia previa vigente que se solapa, el sistema la cierra automáticamente al confirmar.

### Asignar un contexto operativo

1. Dentro del historial de la persona → "Nuevo contexto operativo".
2. Si su compañía es Principal, la Principal del contexto se fija automáticamente.
3. Si su compañía es Contratista, elija entre las Principales con relación vigente con esa Contratista.
4. Defina el período de vigencia, contenido dentro de la pertenencia vigente.

### Asignar una unidad organizativa (dentro de un contexto)

1. Dentro del contexto operativo → elegir unidad organizativa sobre el árbol de esa Principal.
2. Defina el período de vigencia.

### Asignar un perfil

1. Dentro del historial de la persona → "Nuevo perfil/tipo de persona".
2. Elija el tipo (por ejemplo, Trabajador, Visitante) y el período de vigencia.
3. Una persona puede tener varios perfiles simultáneos.

### Asignar una credencial

1. Dentro del historial de la persona → "Nueva credencial", eligiendo la Compañía Principal correspondiente.
2. Elija el tipo de credencial y el período de vigencia.

### Asignar un permiso

1. Módulo **Permisos** → "Nuevo permiso".
2. Elija el área y el alcance (Persona / Unidad organizativa / Compañía).
3. Defina fechas de vigencia y bloques horarios por día de la semana.

### Evaluar acceso

1. Módulo **Evaluación de acceso**.
2. Elija persona, área y fecha/hora a evaluar.
3. Consulte el resultado (CONCEDIDO/DENEGADO) y, si corresponde, el motivo de la denegación.

---

**Documentos relacionados**: [`01-manual-despliegue-implementacion.md`](01-manual-despliegue-implementacion.md) ·
[`03-manual-administrador.md`](03-manual-administrador.md) ·
[`04-documentacion-tecnica-cierre.md`](04-documentacion-tecnica-cierre.md) ·
[Diagrama del flujo de autorización](diagrams/autorizacion.md)
