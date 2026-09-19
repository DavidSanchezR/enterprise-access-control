# UX/UI Specification — Control de Acceso Empresarial

**Especificación:** `001-control-acceso-empresarial`  
**Artefacto:** `ux-ui.md`  
**Estado:** Propuesta para incorporación al plan e implementación.

## 1. Propósito

Definir la experiencia web y el sistema visual de la solución de control de acceso empresarial. Este documento define **cómo se presenta y opera el dominio**, pero no modifica reglas de negocio ni cardinalidades.

La UX debe hacer explícita esta cadena contextual:

`Persona → Compañía de pertenencia → Principal → Contexto operativo → UO → Permisos/Áreas → Credencial`

La compañía de pertenencia y los contextos operativos respecto de Principales son conceptos distintos.

## 2. Principios UX

1. **Contexto primero:** toda operación de acceso debe identificar Principal y contexto.
2. **Seguridad por diseño:** operaciones de alto impacto muestran consecuencias antes de confirmar.
3. **Histórico preservado:** finalizar/revocar no significa eliminar.
4. **Estado ≠ autorización:** el estado administrativo no sustituye al motor de evaluación.
5. **Alta densidad informativa:** tablas, búsqueda, filtros, árboles, badges y timelines son preferibles a gráficos decorativos.
6. **Consistencia:** una misma entidad conserva la misma semántica visual en toda la aplicación.
7. **No duplicar reglas de negocio en frontend:** el frontend valida UX; el backend/dominio decide vigencia, relaciones, autorización y revocación.

## 3. Dirección visual

Estilo: **Enterprise Operations Console / Industrial Access Control**.

Características:

- profesional y sobrio;
- alta legibilidad;
- alta densidad de información;
- jerarquía visual clara;
- controles predecibles;
- énfasis en seguridad y trazabilidad.

Evitar gradientes decorativos, exceso de animaciones, colores saturados como decoración y dashboards llenos de gráficos sin valor operativo.

## 4. Design tokens

### Colores base

| Token | Valor | Uso |
|---|---|---|
| `background` | `#F5F7FA` | Fondo |
| `surface` | `#FFFFFF` | Cards, tablas, formularios |
| `sidebar` | `#172033` | Navegación |
| `text-primary` | `#1F2937` | Texto principal |
| `text-secondary` | `#667085` | Texto secundario |
| `border` | `#D9DEE7` | Bordes |
| `primary` | `#2563EB` | Acción primaria |
| `primary-hover` | `#1D4ED8` | Hover |

### Colores semánticos

| Token | Valor | Significado |
|---|---|---|
| `success` | `#16A34A` | Activo, permitido, válido |
| `warning` | `#D97706` | Atención |
| `danger` | `#DC2626` | Revocado, error, destructivo |
| `info` | `#2563EB` | Información |
| `neutral` | `#64748B` | Inactivo/histórico |

Los colores **no representan el tipo de compañía**. Contratista no debe verse como error.

## 5. Tipografía

Primera opción: **Inter**. Fallback: `system-ui`, `-apple-system`, `Segoe UI`, sans-serif.

- H1: 28–32 px.
- H2: 22–24 px.
- H3: 18–20 px.
- Body: 14–16 px.
- Metadata: 12–13 px.

## 6. Layout

Desktop-first:

```text
┌─────────────────────────────────────────────────────────────┐
│ Logo / Producto                         Notificaciones Usuario│
├───────────────┬─────────────────────────────────────────────┤
│ Dashboard     │                                             │
│ Personas      │                 CONTENIDO                   │
│ Compañías     │                                             │
│ Organización  │                                             │
│ Accesos       │                                             │
│ Históricos    │                                             │
│ Auditoría     │                                             │
│ Configuración │                                             │
└───────────────┴─────────────────────────────────────────────┘
```

Sidebar persistente en desktop y breadcrumb en pantallas profundas.

## 7. Navegación

- **Dashboard**
- **Personas**
- **Compañías**
  - Principales/Mandantes
  - Contratistas
  - Relaciones
- **Organización**
  - UO
  - Áreas de acceso
- **Accesos**
  - Permisos
  - Credenciales
  - Evaluación de acceso
- **Históricos**
- **Auditoría**
- **Configuración**

## 8. Dashboard

Debe responder: **qué ocurre ahora y qué requiere atención**.

Mostrar:

- personas activas;
- contratistas activos;
- Principales activas;
- accesos/evaluaciones recientes;
- credenciales próximas a vencer;
- relaciones o pertenencias próximas a finalizar;
- actividad reciente.

No depender de gráficos para información crítica.

## 9. Compañías

### Listado

Columnas:

- Compañía;
- Tipo;
- Estado;
- identificador;
- datos administrativos relevantes;
- acciones.

Filtros:

- búsqueda;
- tipo;
- estado.

Tipos:

- `PRINCIPAL_MANDANTE`;
- `CONTRATISTA`.

### Principal

Tabs:

- Resumen;
- Organización;
- Contratistas;
- Personas;
- Áreas de acceso;
- Histórico.

### Contratista

Tabs:

- Resumen;
- Principales relacionadas;
- Personas;
- Histórico.

## 10. Relaciones Contratista ↔ Principal

Representar explícitamente:

```text
Contratista
    ↓
Relación vigente
    ↓
Principal
```

Mostrar:

- Contratista;
- Principal;
- estado;
- fecha inicio;
- fecha fin;
- acciones.

Una relación vigente no debe confundirse con un contexto operativo individual.

## 11. Personas

### Listado

Filtros:

- nombre;
- documento/identificador;
- compañía;
- estado;
- tipo;
- Principal;
- UO;
- estado de credencial.

Columnas:

- Persona;
- compañía de pertenencia;
- estado;
- Principales activas;
- credenciales relevantes;
- acciones.

### Detalle

Cabecera:

```text
Pedro García
● ACTIVO
```

Bloque de pertenencia:

```text
COMPAÑÍA DE PERTENENCIA

Servicios ACME
CONTRATISTA

Vigencia
01/01/2026 → 30/09/2026
```

Después debe mostrarse cada Principal como **contexto independiente**.

## 12. Contextos multi-Principal

Una persona puede tener contextos vigentes simultáneamente para Principales distintas.

```text
Pedro García
Compañía de pertenencia: Servicios ACME

┌─────────────────────────────────────────┐
│ Minera ABC                 ● ACTIVO     │
│ UO: Operaciones / Mina / Mantenimiento │
│ Permisos: 12                            │
│ Credencial: CRD-001                     │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│ Minera DEF                 ● ACTIVO     │
│ UO: Operaciones / Planta                │
│ Permisos: 8                             │
│ Credencial: CRD-002                     │
└─────────────────────────────────────────┘
```

Cada contexto debe aislar Principal, UO, permisos, credencial y vigencia.

## 13. Asignación de UO

Usar un wizard contextual:

### Paso 1 — Persona
Mostrar persona y compañía de pertenencia.

### Paso 2 — Principal
Seleccionar una Principal válida.

### Paso 3 — UO
Mostrar árbol contextual:

```text
Minera ABC
├─ Operaciones
│  └─ Mina
│     ├─ Mantenimiento
│     └─ Seguridad
└─ Administración
```

### Paso 4 — Vigencia
- FechaHoraInicio;
- FechaHoraFin cuando corresponda.

### Paso 5 — Confirmación
Mostrar:

`Persona → Compañía → Principal → UO → Vigencia`

## 14. Árbol de UO

Componente TreeView con:

- expandir/contraer;
- selección;
- búsqueda;
- breadcrumb;
- nodo seleccionado;
- contexto de Principal;
- desambiguación de nombres iguales.

Una UO siempre se muestra dentro de su árbol/Principal.

## 15. Cambio de compañía de pertenencia

No debe ser un simple `Guardar`.

Flujo:

```text
Compañía actual
        ↓
Nueva compañía
        ↓
Advertencia
        ↓
Confirmación
        ↓
Cierre de pertenencia
        ↓
Revocación automática de dependencias
        ↓
Histórico preservado
```

Confirmación:

```text
⚠ CAMBIO DE COMPAÑÍA DE PERTENENCIA

Actual:
Servicios ACME

Nueva:
Servicios XYZ

La operación finalizará la pertenencia vigente y
revocará automáticamente los contextos, UO y
credenciales dependientes.

El histórico será conservado.

Motivo:
[ Cambio de compañía ]

Fecha efectiva:
[ ... ]

[Cancelar] [Confirmar cambio]
```

Antes de confirmar debe mostrar el impacto:

- finalizar pertenencia;
- finalizar/revocar contextos dependientes;
- revocar asignaciones UO dependientes;
- revocar credenciales dependientes;
- conservar histórico.

## 16. Estados, vigencias y autorización

No combinar estos conceptos.

Ejemplo:

```text
Estado administrativo: ACTIVA
Vigencia: 01/01/2026 → 30/09/2026
Acceso efectivo: PERMITIDO
```

También puede existir:

```text
Estado administrativo: ACTIVA
Vigencia: vigente
Acceso efectivo: DENEGADO
Motivo: CREDENCIAL_REVOCADA
```

La autorización efectiva proviene del motor de evaluación.

## 17. Credenciales

Detalle:

```text
Credencial CRD-001234

Estado
● REVOCADA

Persona
Pedro García

Principal
Minera ABC

Vigencia
01/01/2026 → 30/09/2026

Motivo
CESE_PERTENENCIA

Revocada por pertenencia
[referencia]
```

No presentar `TipoCredencial` como si fuera necesariamente una tecnología física.

## 18. Permisos

Siempre mostrar permisos dentro del contexto de Principal:

```text
Pedro García

Minera ABC
  Operaciones / Mina

  ✓ Acceso Mina
  ✓ Mantenimiento
  ✓ Zona restringida

Minera DEF
  Operaciones / Planta

  ✓ Acceso Planta
  ✓ Laboratorio
```

Filtros:

- Principal;
- UO;
- área;
- estado.

## 19. Evaluación de acceso

Pantalla administrativa explicativa:

```text
EVALUACIÓN DE ACCESO

Persona
Pedro García

Principal
Minera ABC

Área
Mina / Mantenimiento

Resultado
● PERMITIDO

Validaciones

✓ Persona válida
✓ Compañía de pertenencia válida
✓ Relación vigente
✓ Contexto operativo vigente
✓ UO asignada
✓ Área autorizada
✓ Perfil elegible
✓ Credencial válida
```

En denegación:

```text
● DENEGADO

Motivo:
CREDENCIAL_REVOCADA

Detalle:
La credencial fue revocada automáticamente
al finalizar la pertenencia correspondiente.
```

## 20. Históricos

Usar Timeline cuando la secuencia temporal sea relevante.

```text
2026

01 Oct
Cambio de compañía
Servicios ACME → Servicios XYZ

30 Sep
Contexto Minera ABC finalizado

30 Sep
Credencial CRD-001 revocada

01 Jan
Contexto Minera ABC creado
```

Filtros:

- persona;
- compañía;
- Principal;
- entidad;
- tipo de evento;
- fechas.

Nunca sugerir que un histórico fue eliminado.

## 21. Auditoría

Tabla:

- Fecha/hora;
- usuario;
- acción;
- entidad;
- identificador;
- Principal/contexto cuando aplique;
- resultado.

Filtros:

- rango temporal;
- usuario;
- persona;
- compañía;
- Principal;
- entidad;
- acción.

## 22. Componentes

Componentes base:

`Button`, `IconButton`, `Input`, `Select`, `Autocomplete`, `DatePicker`, `DateTimePicker`, `DateRangePicker`, `Search`, `Badge`, `StatusBadge`, `Alert`, `Toast`, `Dialog`, `ConfirmationDialog`, `Drawer`, `DataTable`, `Pagination`, `Tabs`, `Breadcrumb`, `TreeView`, `Timeline`, `Card`, `Stepper`, `EmptyState`, `LoadingState`, `ErrorState`.

Componentes de dominio:

- `CompanyBadge`;
- `CompanyTypeBadge`;
- `ContextCard`;
- `OrganizationalUnitTree`;
- `CredentialStatus`;
- `AccessDecision`;
- `RevocationImpact`;
- `AuditEvent`;
- `HistoryTimeline`.

## 23. Formularios

- labels persistentes;
- validación inline;
- fechas consistentes;
- no depender de placeholders;
- `Cancelar` y `Guardar/Confirmar` diferenciados;
- confirmación explícita para operaciones de alto impacto.

## 24. Tablas

Soportar:

- búsqueda;
- filtros;
- ordenamiento;
- paginación;
- estados;
- acciones contextuales.

## 25. Responsive

Desktop-first:

- ≥1440 px: completo;
- 1024–1439 px: adaptado;
- 768–1023 px: tablet;
- <768 px: versión simplificada.

En móvil se prioriza consulta, formularios simplificados y tablas convertibles a tarjetas.

## 26. Accesibilidad

Objetivo: **WCAG 2.2 AA**.

Requisitos:

- contraste suficiente;
- teclado;
- focus visible;
- labels accesibles;
- estados no dependientes solo del color;
- ARIA cuando corresponda;
- mensajes de error claros;
- dialogs accesibles;
- TreeView, tablas y formularios accesibles.

Ejemplo: `REVOCADA` debe acompañar al indicador rojo; nunca depender solo del color.

## 27. Feedback

- Toast: éxito de operaciones de bajo riesgo.
- Alert: información importante.
- Dialog: confirmaciones críticas.
- Inline validation: errores de formulario.

## 28. Stack tecnológico

### Frontend

- React;
- TypeScript;
- Vite;
- librería de componentes compatible con React;
- cliente HTTP para ASP.NET Core Web API;
- validación de formularios;
- testing unitario/componentes;
- testing end-to-end.

### Backend

- C#;
- .NET 10;
- ASP.NET Core Web API;
- Entity Framework Core 10;
- SQL Server;
- LINQ.

### Plataforma

- ASP.NET Core Authentication/Authorization;
- Authorization Policies;
- OpenAPI nativo;
- Health Checks;
- Options Pattern;
- ProblemDetails.

La autorización de plataforma permanece separada del motor de evaluación de dominio.

## 29. Arquitectura

```text
React + TypeScript + Vite
          │
          │ HTTPS / JSON / OpenAPI
          ▼
ASP.NET Core Web API / .NET 10
          │
          ├── Domain / Application
          ├── EF Core 10
          └── LINQ
                 │
                 ▼
              SQL Server
```

## 30. Flujos críticos a cubrir

- UX-01 Crear Principal.
- UX-02 Crear Contratista.
- UX-03 Relacionar Contratista con Principal.
- UX-04 Crear persona con compañía de pertenencia.
- UX-05 Crear contexto operativo.
- UX-06 Asignar UO.
- UX-07 Configurar permisos/áreas.
- UX-08 Asignar credencial.
- UX-09 Consultar múltiples Principales simultáneas.
- UX-10 Cambiar compañía de pertenencia.
- UX-11 Visualizar impacto de revocación automática.
- UX-12 Consultar históricos.
- UX-13 Consultar credencial revocada.
- UX-14 Evaluar acceso permitido.
- UX-15 Evaluar acceso denegado.
- UX-16 Consultar auditoría.

## 31. Criterios de aceptación UX

La implementación debe permitir:

1. identificar inequívocamente la compañía de pertenencia;
2. identificar todas las Principales con contexto vigente;
3. ver UO, permisos y credencial por contexto;
4. evitar la representación de permisos como globales;
5. visualizar consecuencias del cambio de compañía;
6. visualizar la revocación automática;
7. consultar históricos;
8. diferenciar estado, vigencia y autorización;
9. mostrar UO dentro del contexto de Principal;
10. distinguir relaciones Contratista → Principal de contextos operativos;
11. visualizar contextos simultáneos para Principales distintas;
12. distinguir estados sin depender únicamente del color;
13. exigir confirmación para acciones críticas;
14. soportar teclado;
15. cubrir UX-01 a UX-16 mediante pruebas apropiadas.

## 32. Regla de no desviación

Este documento no autoriza a cambiar:

- cardinalidades;
- relaciones de dominio;
- reglas de vigencia;
- revocación automática;
- autorización;
- restricciones;
- asociación de UO;
- aislamiento por Principal.

Todo cambio de negocio debe volver al proceso de especificación/clarificación.

## 33. Integración con Spec Kit

`ux-ui.md` debe ser una entrada explícita del `plan.md` y de `tasks.md`.

Antes de implementar:

1. incorporar el documento al directorio de la feature;
2. ejecutar una revisión/actualización del plan para incorporar UX/UI;
3. regenerar `tasks.md` si fue eliminado o si sus tareas no contemplan UX/UI;
4. verificar consistencia con `constitution.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/` y `plan.md`;
5. solo después ejecutar `/speckit-implement`.

No es necesario volver a `/speckit-clarify` únicamente por decisiones visuales. Solo debe usarse si durante esta integración aparece una **nueva decisión de negocio ambigua**.

## 34. Estado

Documento preparado para incorporación al proyecto y para servir como fuente explícita de UX/UI durante planificación e implementación.
