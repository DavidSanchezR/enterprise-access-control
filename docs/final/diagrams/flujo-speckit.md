# Diagrama del Flujo Spec → Implementation → Validation

**Fuente**: README §Documentación de especificación (Spec Kit), `tasks.md`,
`docs/functional-validation/post-baseline-validation.md`, historial Git.

```mermaid
flowchart TD
    REQ["REQUISITOS\n(negocio: control de acceso\nfísico empresarial)"] --> SPEC

    subgraph SpecKit["Ciclo Spec Kit (constitución ya ratificada v1.1.1)"]
        SPEC["/speckit-specify\nspec.md — 81 RF, 41 CS, 10 historias"]
        CLARIFY["/speckit-clarify\n15 sesiones de clarificación\n(D1–D9, RF-082, RF-083, …)"]
        PLAN["/speckit-plan\nplan.md, research.md (34 secciones),\ndata-model.md (22 entidades)"]
        TASKS["/speckit-tasks\ntasks.md — T001–T312"]
        ANALYZE["/speckit-analyze\nconsistencia cruzada\nspec↔plan↔tasks↔código"]
        IMPLEMENT["/speckit-implement\nejecución de tareas"]
        CHECKLIST["/speckit-checklist\nbaseline-gate.md — 45 ítems"]

        SPEC --> CLARIFY --> PLAN --> TASKS --> ANALYZE --> IMPLEMENT --> CHECKLIST
        CHECKLIST -.->|"hallazgos documentales"| SPEC
    end

    REQ --> SpecKit
    SpecKit --> AUTOTEST["AUTOMATED TESTS\n155 unit + 593 integración +\n252 contrato + 203 Vitest + 9 E2E"]
    AUTOTEST --> FUNCVAL["FUNCTIONAL VALIDATION\npost-Baseline: VF-001 a VF-011\n(OPEN→ANALYZING→FIXED→VALIDATED→CLOSED)"]
    FUNCVAL --> MANVAL["MANUAL VALIDATION\nvalidación funcional manual\nde la interfaz por el usuario"]
    MANVAL --> COMMIT["COMMIT\n244fa09 — feat: complete\npost-baseline functional validation"]
    COMMIT --> PR["PULL REQUEST\nPR #5"]
    PR --> MERGE["MERGE TO MAIN\n6a72260"]
    MERGE --> CLOSEDOC["Cierre documental\nPR #6 → dc1edea"]
    CLOSEDOC --> CLOSED["PROJECT CLOSED"]
```

## Iteración real observada (no lineal en una sola pasada)

El ciclo de la izquierda **se recorrió varias veces**, no una sola: cada corrección post-Baseline (VF-004,
VF-007) volvió a entrar por `/speckit-clarify` y a salir por `/speckit-implement`, sin reabrir ni renumerar
las tareas ya cerradas (T001–T242 permanecen intactas). El propio README lo declara: *"ninguna sesión
posterior renumeró, reabrió ni reescribió tareas ya cerradas"*.

```mermaid
flowchart LR
    B["Baseline\nT001–T242"] --> G1["Gate de cierre\nEtapa 1 (D1–D9)\nT169–T242"]
    G1 --> PB["Validación funcional\npost-Baseline"]
    PB --> VF007["VF-007 → RF-082\nT243–T266"]
    PB --> VF005["VF-005\nT267–T269"]
    PB --> VF010["VF-010\nT270–T272"]
    PB --> VF011["VF-011 (+ext.)\nT273–T278"]
    PB --> VF001["VF-001\nT279–T281"]
    PB --> VF002["VF-002\nT282–T284"]
    PB --> VF003["VF-003\nT285–T286"]
    PB --> VF004["VF-004 → RF-083\nT287–T312"]
    PB --> VF006["VF-006 (sin cambios)"]
    PB --> VF008["VF-008 (sin cambios)"]
    PB --> VF009["VF-009 (sin cambios)"]
    VF007 & VF005 & VF010 & VF011 & VF001 & VF002 & VF003 & VF004 & VF006 & VF008 & VF009 --> CLOSE["Cierre — T312 final\ntodos los VF en CLOSED"]
```

## Notas verificadas

- Cada VF-XXX que implicó cambio de código volvió a pasar por el ciclo completo de Spec Kit
  (`/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` → `/speckit-implement`) antes
  de tocar código de producción.
- Los VF que resultaron ser comportamiento correcto (VF-006, VF-008, VF-009) se cerraron **sin tareas
  nuevas**, tras validación manual, sin pasar por implementación.
- Ver [diagrama Git/GitHub](git-github.md) para el detalle de ramas, commits y PRs de este flujo.
