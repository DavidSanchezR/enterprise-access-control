# Diagrama Simplificado del Dominio

**Fuente**: [`data-model.md`](../../../specs/001-control-acceso-empresarial/data-model.md) (22 entidades
completas; este diagrama simplifica las relaciones centrales para lectura rápida).

```mermaid
erDiagram
    USUARIO ||--o{ ASIGNACION_ROL_ADMINISTRATIVO : "tiene"
    COMPANIA ||--o{ ASIGNACION_ROL_ADMINISTRATIVO : "alcance (si COMPANY_ADMINISTRATOR)"

    COMPANIA ||--o{ RELACION_CONTRATISTA_PRINCIPAL : "como Contratista o Principal"
    COMPANIA ||--o{ ASIGNACION_PERSONA_COMPANIA : "pertenencia (empleador)"
    PERSONA ||--o{ ASIGNACION_PERSONA_COMPANIA : "histórico de pertenencia"

    COMPANIA ||--o{ COMPANIA_PRINCIPAL_UO_RAIZ : "raíces (solo Principal)"
    UNIDAD_ORGANIZATIVA ||--o| COMPANIA_PRINCIPAL_UO_RAIZ : "nodo raíz"
    UNIDAD_ORGANIZATIVA ||--o{ UNIDAD_ORGANIZATIVA : "unidad superior (self)"

    COMPANIA ||--o{ AREA_ACCESO : "propietaria (solo Principal)"
    AREA_ACCESO ||--o{ AREA_ACCESO : "área superior (self)"
    AREA_ACCESO ||--o{ AREA_ACCESO_TIPO_PERSONA : "tipos autorizados"
    TIPO_PERSONA ||--o{ AREA_ACCESO_TIPO_PERSONA : "autoriza"

    PERSONA ||--o{ CONTEXTO_OPERATIVO : "contextos (1 por Principal)"
    COMPANIA ||--o{ CONTEXTO_OPERATIVO : "Principal del contexto"
    CONTEXTO_OPERATIVO ||--o{ ASIGNACION_UO : "unidad organizativa"
    UNIDAD_ORGANIZATIVA ||--o{ ASIGNACION_UO : "asignada en"
    CONTEXTO_OPERATIVO ||--o{ ASIGNACION_CREDENCIAL : "credencial por Principal"
    TIPO_CREDENCIAL ||--o{ ASIGNACION_CREDENCIAL : "tipo/diseño"

    PERSONA ||--o{ ASIGNACION_TIPO_PERSONA : "perfiles (múltiples)"
    TIPO_PERSONA ||--o{ ASIGNACION_TIPO_PERSONA : "cataloga"

    AREA_ACCESO ||--o{ PERMISO_ACCESO : "permisos sobre el área"
    PERMISO_ACCESO ||--o{ BLOQUE_HORARIO : "bloques por día de semana"
    PERSONA ||--o{ PERMISO_ACCESO : "alcance PERSONA"
    UNIDAD_ORGANIZATIVA ||--o{ PERMISO_ACCESO : "alcance UNIDAD_ORGANIZATIVA"
    COMPANIA ||--o{ PERMISO_ACCESO : "alcance COMPAÑÍA"

    TIPO_DOCUMENTO ||--o{ PERSONA : "identifica"
    TIPO_SANGRE ||--o{ PERSONA : "clasifica"
    GENERO ||--o{ PERSONA : "clasifica"
```

## Relación de revocación en cascada (RF-061 a RF-065)

```mermaid
flowchart LR
    A["Cierre de\nAsignaciónPersonaCompañía\n(cese o reemplazo)"] -->|"misma transacción"| B["Revoca TODOS los\nContextoOperativoPersonaPrincipal\nvigentes de la persona"]
    B --> C["Revoca AsignaciónPersonaUnidadOrganizativa\ndependientes de cada contexto"]
    B --> D["Revoca AsignaciónCredencial\ndependientes de cada contexto"]
    C -.->|"nunca elimina, solo FechaHoraFin +\nEstado + MotivoFin + RevocadoPorPertenenciaId"| H[("Histórico íntegro\npreservado")]
    D -.-> H
```

## Notas verificadas

- `UnidadOrganizativa` **no tiene** ninguna columna ni relación directa hacia `Compañía` (restricción
  explícita de negocio, RF-044); su pertenencia se resuelve vía `CompañíaPrincipalUnidadOrganizativaRaiz` y
  recorriendo `UnidadSuperiorId` hasta la raíz.
- `ÁreaAcceso` sí tiene una FK directa `CompañíaPrincipalId` (sin la misma restricción).
- `AsignaciónPersonaUnidadOrganizativa` y `AsignaciónCredencial` cuelgan del **contexto operativo**, no
  directamente de la persona ni de la pertenencia — permitiendo que una persona tenga una unidad y una
  credencial distintas por cada Compañía Principal simultánea.
- Detalle completo de campos, validaciones e índices: `data-model.md` (22 entidades).
