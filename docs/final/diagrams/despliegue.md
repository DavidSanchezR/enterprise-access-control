# Diagrama de Despliegue

**Fuente**: `docker-compose.yml`, `docker-compose.prod.yml`, `backend/Dockerfile`, README §Despliegue.

## Topología de contenedores — producción

```mermaid
flowchart TB
    Internet(["Internet"]) -->|"HTTPS (TLS)"| Proxy

    subgraph Host["Host Linux (docker-compose.prod.yml)"]
        Proxy["Proxy inverso propio\n(NO incluido en el repositorio)\nTermina TLS · sirve frontend/dist/\nreenvía /api/** y /health/**"]

        subgraph ComposeNet["Red de Docker Compose"]
            API["Contenedor eac-api\nmcr.microsoft.com/dotnet/aspnet:10.0\nPuerto 8080 — publicado SOLO en 127.0.0.1"]
            SQL["Contenedor SQL Server\nmcr.microsoft.com/mssql/server:2022-latest\nPuerto 1433 — publicado SOLO en 127.0.0.1"]
            VOL[("Volumen Docker\nsqlserver-data")]
        end

        Proxy -->|"/ (estáticos)"| DIST["frontend/dist/\n(npm run build, servido por el proxy)"]
        Proxy -->|"/api/**, /health/**"| API
        API -->|"depends_on: service_healthy"| SQL
        SQL --- VOL
    end

    Admin(["Operador (host local)"]) -->|"127.0.0.1:1433\ndotnet ef database update"| SQL
    Admin -->|"127.0.0.1:8080\nverificación directa"| API

    classDef notInRepo stroke-dasharray: 5 5;
    class Proxy,DIST notInRepo
```

## Topología de contenedores — desarrollo

```mermaid
flowchart LR
    Dev(["Desarrollador"]) -->|"http://localhost:5173"| Vite["Vite dev server\n(npm run dev)"]
    Vite -->|"proxy /api, /health\nVITE_API_PROXY_TARGET"| API["Contenedor eac-api\n:8080 (docker compose)"]
    API --> SQL["Contenedor eac-sqlserver\n:1433 (docker compose)\nMSSQL_PID=Developer"]
    Dev -->|"dotnet ef database update\n(desde el host)"| SQL
```

## Secuencia de arranque (ambos entornos)

```mermaid
sequenceDiagram
    participant Op as Operador
    participant Compose as Docker Compose
    participant SQL as SQL Server
    participant EF as dotnet ef (host)
    participant API as API (contenedor)

    Op->>Compose: up -d --build --wait sqlserver
    Compose->>SQL: levantar contenedor
    SQL-->>Compose: healthcheck OK (sqlcmd SELECT 1)
    Op->>EF: dotnet ef database update
    EF->>SQL: aplicar 12 migraciones + 1 seed
    Op->>Compose: up -d --build api
    Compose->>API: levantar contenedor
    API->>SQL: StartAsync — ¿existe el esquema?
    alt esquema ausente
        API-->>Op: falla (SQL error 4060) — contenedor termina
    else esquema presente
        API->>SQL: bootstrap idempotente del GLOBAL_ADMINISTRATOR (RF-078)
        API-->>Op: /health/ready → 200
    end
```

## Notas verificadas

- **Solo la API está contenedorizada.** No existe Dockerfile ni servicio de Compose para la SPA.
- En producción, **ningún puerto se expone fuera de `127.0.0.1`**: todo el tráfico externo entra por el
  proxy inverso (no versionado en el repositorio).
- La API **no aplica migraciones al arrancar**: el orden correcto es siempre
  `sqlserver sano → dotnet ef database update → api`.
- Ver [Manual de Despliegue e Implementación](../01-manual-despliegue-implementacion.md) para el
  procedimiento paso a paso completo.
