# Diagrama del Flujo de Autorización

**Fuente**: README §Modelo de autorización y §Motor de evaluación de acceso, `Program.cs`,
`EvaluadorDeAcceso.cs`, `research.md` §7 y §18.

> El sistema separa dos mecanismos de autorización **independientes**: la autorización de plataforma
> (quién puede administrar qué) y el motor de evaluación de acceso (si una persona puede entrar físicamente
> a un área). Este diagrama cubre ambos, en el orden en que realmente se ejecutan.

## 1. Autorización de plataforma (toda request administrativa)

```mermaid
flowchart TD
    R["Request HTTP"] --> Auth{"¿JWT Bearer\nválido y no expirado?\n(ClockSkew=0)"}
    Auth -- No --> D401["401 — no autenticado\n(fallback: RequireAuthenticatedUser)"]
    Auth -- Sí --> RBAC["Leer claims del token:\n'rol' y 'alcance_compania'"]
    RBAC --> Scope{"¿Rol vigente?"}
    Scope -- "Sin rol vigente" --> D401b["Sin alcance alguno\n→ toda operación protegida se rechaza"]
    Scope -- "GLOBAL_ADMINISTRATOR" --> Own["Alcance = todas las compañías"]
    Scope -- "COMPANY_ADMINISTRATOR" --> Own2["Alcance = compañías de sus\nasignaciones vigentes (claim alcance_compania)"]
    Own --> RO["Resource Ownership:\n¿el recurso concreto está\ndentro del alcance?"]
    Own2 --> RO
    RO -- No --> D404["404 — no revela existencia\n(nunca 403 por identificador ajeno)"]
    RO -- Sí --> BR["Business Rules:\nvigencias, contención,\njerarquías sin ciclos,\nno-solapamiento"]
    BR -- Inválido --> D40x["400 / 409 — ProblemDetails\ncon código estable"]
    BR -- Válido --> OP["Operation — ejecuta\nla escritura/lectura"]
    OP --> ALLOW["ALLOW"]
```

## 2. Motor de evaluación de acceso (Historia 8 — 15 pasos, independiente de lo anterior)

```mermaid
flowchart TD
    S1["1. Compañía Principal del área\n¿en el alcance del usuario que consulta?"] -->|No| DENY
    S1 -->|Sí| S234["2-4. Identificar persona, área\ny Principal propietaria"]
    S234 --> S5{"5. ¿Principal ACTIVO?\n(RF-079)"}
    S5 -->|No| DENY_INACTIVA["DENEGADO:\nCOMPANIA_INACTIVA"]
    S5 -->|Sí| S6{"6. ¿Contexto operativo vigente\ny legítimo con esa Principal?"}
    S6 -->|No| DENY_CTX["DENEGADO:\nSIN_CONTEXTO_OPERATIVO_VIGENTE"]
    S6 -->|Sí| S7{"7. ¿AsignaciónCredencial vigente\n(ASIGNADO + dentro de fecha)?"}
    S7 -->|No| DENY_CRED["DENEGADO:\nSIN_CREDENCIAL_VIGENTE"]
    S7 -->|Sí| S8{"8. ¿Área ACTIVA?"}
    S8 -->|No| DENY_AREA["DENEGADO:\nAREA_INACTIVA"]
    S8 -->|Sí| S9{"9. ¿Algún perfil vigente\nautorizado en el área?"}
    S9 -->|No| DENY_PERFIL["DENEGADO:\nPERFIL_NO_AUTORIZADO_EN_AREA"]
    S9 -->|Sí| S10["10. Determinar unidad organizativa\nvigente en ese contexto"]
    S10 --> S11["11. Evaluar permisos aplicables:\nPERSONA, UNIDAD_ORGANIZATIVA, COMPAÑÍA"]
    S11 --> S12{"12. ¿Alguno vigente\npor fecha (RF-083)?"}
    S12 -->|No| DENY_VIG["DENEGADO:\nSIN_PERMISO_APLICABLE /\nPERMISO_FUERA_DE_VIGENCIA"]
    S12 -->|Sí| S13{"13. ¿Día/hora dentro de\nbloque horario (zona IANA\nde la Principal)?"}
    S13 -->|No| DENY_HORA["DENEGADO:\nFUERA_DE_BLOQUE_HORARIO"]
    S13 -->|Sí| S14["14. Precedencia:\nPERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA"]
    S14 --> S15["15. Resolver"]
    S15 --> GRANT["CONCEDIDO"]

    DENY["DENEGADO:\nFUERA_DE_ALCANCE_USUARIO /\nPERSONA_NO_ENCONTRADA /\nAREA_NO_ENCONTRADA"]
```

## Notas verificadas

- Cualquier paso sin resultado inequívoco produce **DENEGADO** (denegación por defecto, Principio I) — no
  hay una rama "indeterminado ⇒ conceder".
- La credencial (paso 7) es **condición necesaria, no suficiente**: sin ella se deniega antes de evaluar
  ningún permiso, pero tenerla no concede acceso por sí sola.
- Inactivar una compañía (paso 5) deniega por **evaluación dinámica**, no por cascada de escritura:
  reactivarla restablece el acceso sin tocar ningún registro.
- Los 12 motivos de `MotivoDenegacion` están tipificados en `contracts/access-evaluation.yaml`.
- Detalle completo: [README §Motor de evaluación de acceso](../../../README.md#motor-de-evaluación-de-acceso)
  y `research.md` §7.
