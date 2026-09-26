# Diagrama de Arquitectura

**Fuente**: README.md §Arquitectura, `backend/src/*/*.csproj`, `frontend/src/features/*`.

## Capas del backend + módulos del frontend

```mermaid
flowchart TB
    U["Usuario"] --> SPA

    subgraph SPA["Frontend — SPA React (Vite, por features)"]
        direction LR
        f_auth["auth"]
        f_companies["companies"]
        f_orgunits["org-units"]
        f_people["people"]
        f_area["area-access"]
        f_perm["permissions"]
        f_cred["credentials"]
        f_masters["masters"]
        f_users["users"]
        f_eval["access-evaluation"]
    end

    SPA -->|"HTTP/JSON — mismo origen, sin CORS"| API

    subgraph Backend["Backend .NET 10 — arquitectura en 4 capas"]
        API["Api\nControllers · JWT Bearer · ProblemDetails (RFC 7807/9457)\nHealth checks · Composición de dependencias"]
        APP["Application\nCasos de uso por módulo · DTOs\nFluentValidation vía ValidacionAutomaticaFilter"]
        DOM["Domain\nEntidades · Enums · EvaluadorDeAcceso (lógica pura, 15 pasos)"]
        INFRA["Infrastructure\nDbContext EF Core · Migraciones · Interceptor de auditoría\nHashing de contraseñas · Reloj empresarial (NodaTime)"]

        API --> APP
        APP --> DOM
        APP --> INFRA
        INFRA --> DOM
    end

    INFRA --> SQL[("SQL Server 2022\n(datetime2(3) UTC, 6 triggers de\nno-solapamiento, rowversion)")]

    classDef layer fill:#00000000,stroke-width:1px;
    class API,APP,DOM,INFRA layer
```

## Notas verificadas

- **Dependencias permitidas**: `Domain` no depende de ninguna capa externa; `Application` depende solo de
  `Domain`; `Infrastructure` implementa contratos de `Application`/`Domain`; `Api` compone todo en
  `Program.cs`.
- El **motor de evaluación de acceso** (`EvaluadorDeAcceso.cs`) vive en `Domain` como lógica pura —
  `EvaluacionAccesoService` (Application) es quien recolecta de la base de datos únicamente los datos que
  cada uno de sus 15 pasos necesita.
- La API **no configura CORS**: la SPA debe consumirse desde el mismo origen, detrás de un proxy inverso
  (ver [diagrama de despliegue](despliegue.md)).
- El frontend no tiene una capa de "servicios" central: cada *feature* encapsula sus propias llamadas HTTP
  (vía `lib/apiClient.ts`) y su propio estado de servidor (TanStack Query).
