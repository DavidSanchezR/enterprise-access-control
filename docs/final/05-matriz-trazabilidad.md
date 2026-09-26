# Matriz Final de Trazabilidad

**Proyecto**: Enterprise Access Control Platform — **PROYECTO CERRADO**
**Versión**: 1.0 — 2026-09-26

> Esta matriz relaciona historias de usuario, requisitos funcionales, decisiones, tareas, hallazgos VF, tests,
> validación manual y evidencia Git/GitHub. Es un complemento de
> [`04-documentacion-tecnica-cierre.md`](04-documentacion-tecnica-cierre.md) §13, no un duplicado: aquí se
> prioriza cobertura amplia por fila; allí, profundidad narrativa por Historia.

## 1. Historias de usuario → requisitos → tareas → evidencia

| Elemento | Fuente | Implementación | Prueba | Evidencia | Estado |
|---|---|---|---|---|---|
| H1 — Login y alcance de gestión (P1) | `spec.md` Historia 1 | `AuthController`, `Usuario`, `HistorialContraseña` | Unitarias + integración + contrato `auth.yaml` | README §Capacidades principales | Implementada (API + UI) |
| H2 — Compañías y unidades organizativas (P1) | `spec.md` Historia 2 | `CompaniasController`, `UnidadesOrganizativasController` | Integración (ciclos, aislamiento por Principal) + contrato `companies.yaml`/`org-units.yaml` | data-model.md `Compañía`/`UnidadOrganizativa` | Implementada (API + UI) |
| H3 — Datos maestros (P1) | `spec.md` Historia 3 | `MaestrosController` | Contrato `masters.yaml` | data-model.md catálogos | Implementada (API + UI) |
| H4 — Personas (P1) | `spec.md` Historia 4 | `PersonasController` | Integración (unicidad de documento, RF-041) | data-model.md `Persona` | Implementada (API + UI) |
| H5 — Históricos, contexto operativo y revocación (P1) | `spec.md` Historia 5 | `PersonaService`, `RevocacionService`, `ReglasRevocacion` | Integración (cascada RF-061 a RF-065, contención RF-072) | data-model.md, research.md §14 | Implementada (API + UI) |
| H6 — Árbol de áreas físicas (P1) | `spec.md` Historia 6 | `AreasAccesoController`, componente `Tree` | Vitest (`Tree.test.tsx`, `Tree.a11y.test.tsx`) + integración (ciclos) | RF-009, RF-038 | Implementada (API + UI) |
| H7 — Tipos de persona por área (P1) | `spec.md` Historia 7 | `AreasAccesoController` (tipos-persona) | Contrato `area-access.yaml` + integración | RF-019 | Implementada (API + UI) |
| H8 — Permisos, vigencia, horarios y evaluación (P1) | `spec.md` Historia 8 | `EvaluadorDeAcceso` (15 pasos), `EvaluacionAccesoService`, `PermisosController` | Unitarias (algoritmo) + integración + E2E (`cs009-quickstart`, `multi-principal-quickstart`) | research.md §7, contracts/access-evaluation.yaml | Implementada (API + UI) |
| H9 — Credencial/fotocheck por Principal (P2) | `spec.md` Historia 9 | `CredencialesController`, `AsignaciónCredencial` | Integración (solapamiento RF-057, contención RF-072) | data-model.md `AsignaciónCredencial` | Implementada (API + UI, dentro del historial de persona) |
| H10 — Auditoría automática por entidad (P2) | `spec.md` Historia 10 (parte) | Interceptor de `SaveChanges` | Integración (verificación de `CreatedAt`/`UpdatedAt`/`*ById`) | RF-026, RF-027 | Implementada |
| H10 — Consultas transversales (P2) | `spec.md` Historia 10 (parte, RF-067 a RF-069, CS-032) | — | — | Decisión D8 | **Fuera del alcance del proyecto cerrado** |

## 2. Decisiones de negocio y de implementación → requisitos → tareas

| Decisión | Fuente | Requisitos formalizados | Tareas | Evidencia | Estado |
|---|---|---|---|---|---|
| D1 — RBAC de administración | Auditoría de cierre 2026-09-20 | RF-074 a RF-077 | T169–T228 | `AsignaciónRolAdministrativo`, CS-036/CS-037 | Cerrada |
| D2 — Bootstrap del primer administrador | Auditoría de cierre 2026-09-20 | RF-078 | T169–T228 | Rutina de `StartAsync` | Cerrada |
| D3 — Aislamiento por alcance | Auditoría de cierre 2026-09-20 | RF-077 | T169–T228, T237–T239, T242 | CS-037 (7 recursos) | Cerrada |
| D4 — Inactivación de compañía | Auditoría de cierre 2026-09-20 | RF-079 | T169–T228 | `EvaluadorDeAcceso` paso 5 | Cerrada |
| D5 — Calendario de vigencias por zona IANA | Auditoría de cierre 2026-09-20 | RF-080 | T169–T228 | `Compañía.ZonaHorariaIana` | Cerrada |
| D6 — Reclasificación de `TipoCompañía` | Auditoría de cierre 2026-09-20 | RF-081 | T169–T228 | `409 CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS` | Cerrada |
| D7 — Interfaz de Historia 5 | Auditoría de cierre 2026-09-20 | — | T169–T228 | UX-17 a UX-22 | Cerrada |
| D8 — Consultas transversales | Auditoría de cierre 2026-09-20 | RF-067 a RF-069 (anotadas, sin implementar) | Ninguna | `spec.md` `[DIFERIDA A ETAPA 2]` | **Fuera de alcance** |
| D9 — Decisiones heredadas (#1, #3, #7) | Auditoría de cierre 2026-09-20 | Política de contraseñas, retención, prioridad H9 | — | README §Decisiones de la auditoría | Cerrada |
| D-1 — Renovación de rol sin endpoint | Gate 2026-09-20 | `contracts/users.yaml` v2.1.0 | T229–T242 | `POST .../roles/{id}/renovar` | Cerrada |
| D-2 — Falsa alarma de control de alcance | Gate 2026-09-20 | — | T237, T238 | Cobertura de regresión | Cerrada |
| D-3 — Orden de generación de migración | Gate 2026-09-20 | — | T229–T242 | Migración validada | Cerrada |
| D-4 — Búsqueda de usuarios en toda la página | Gate 2026-09-20 | RF-077 | T229–T242 | Búsqueda server-side | Cerrada |
| D-5 — Contraseña de arranque por defecto | Gate 2026-09-20 | RF-078 | T229–T242 | `BOOTSTRAP_ADMIN_PASSWORD` obligatoria | Cerrada |

## 3. Hallazgos VF → requisitos/decisión → tareas → tests → validación manual

| VF | Requisito/decisión afectada | Tareas | Tests automatizados | Validación manual | Estado |
|---|---|---|---|---|---|
| VF-001 | RF-013, Principio II | T279–T281 | `CompaniasPage.test.tsx` (11/11) | Sí | **CLOSED** |
| VF-002 | RF-036 | T282–T284 | `Tree.test.tsx`, `UnidadesOrganizativasPage.test.tsx` (35/35) | Sí | **CLOSED** |
| VF-003 | RF-009, RF-036 | T285–T286 | `AreasAccesoPage.test.tsx` y relacionados (49/49) | Sí | **CLOSED** |
| VF-004 | RF-083 (nuevo), RF-021, RF-029, RF-080, RF-082 (matizados) | T287–T312 | `VigenciaDiariaPermisoTests`, `ContencionFechaCivilPermisoTests`, `VigenciaDiariaPermisosTests`, `PermisosHistoricosVigenciaDiariaTests`, contrato `permissions.yaml` v2.0.0 | Sí | **CLOSED** |
| VF-005 | UX-07, RF-035 | T267–T269 | `PermisosPage.test.tsx` (19/19) | Sí (técnica) | **CLOSED** |
| VF-006 | Historia 5 (Casos A/B), decisión D7 | — | E2E `historia5-casos-a-b.spec.ts` (preexistente) | Sí | **CLOSED** (sin cambios) |
| VF-007 | RF-082 (nuevo), RF-072 (ampliado) | T243–T266 | `ReglaContencionPermisoTests`, `ContencionPerfilesTests`, `ContencionPermisosPersonaTests`, `RegistrosAnterioresRf082Tests`, `Rf082RenovacionYCascadaTests` | Sí | **CLOSED** |
| VF-008 | RF-011 | — | Cobertura preexistente de perfiles múltiples | Sí | **CLOSED** (sin cambios; eliminación de perfiles fuera de alcance) |
| VF-009 | RF-072 | — | `ContencionTemporalTests` (preexistente) | Sí | **CLOSED** (sin cambios) |
| VF-010 | UX-17, UX-19 | T270–T272 | E2E `administracion-usuarios.spec.ts` (prueba de geometría) | Sí (técnica) | **CLOSED** |
| VF-011 | RF-013, Principio II, UX-17, UX-18 | T273–T278 | `UsuariosPage.test.tsx`, `UsuarioDetalle.test.tsx` | Sí (técnica) | **CLOSED** |

## 4. Tests → propósito → Git/PR

| Suite | Cantidad final | Bloque que la hizo crecer | PR de integración |
|---|---:|---|---|
| Unitarias backend | 155/155 | Baseline (101) + D1–D9 (T169–T228) + VF-007 (141) + VF-004 (155) | PR #1, PR #5 |
| Integración backend | 593/593 | Baseline (534) + VF-007 (567) + VF-004 (593) | PR #1, PR #5 |
| Contrato backend | 252/252 | Baseline (246) + VF-007 (250) + VF-004 (252) | PR #1, PR #5 |
| Vitest frontend | 203/203 (20 archivos) | Baseline (160) + VF-005/010/011 (176–179) + VF-001/002/003 (183–193) + VF-004 (203) | PR #1, PR #5 |
| E2E Playwright | 9/9 (acumulada) | Baseline (8/9 limpia · 9/9 acumulada) + VF-007/VF-004 (sin regresión) | PR #1, PR #5 |

## 5. Git/GitHub — resumen de trazabilidad de cierre

| Elemento | Valor verificado |
|---|---|
| Commit final de funcionalidad | `244fa09` — *feat: complete post-baseline functional validation* |
| Pull Request de la funcionalidad | **PR #5** (`feature/post-baseline-validation` → `main`) |
| Merge de la funcionalidad | `6a72260` |
| Commit de cierre documental | `6d9d4d1` — *docs: record final closure of post-baseline validation* |
| Pull Request de cierre documental | **PR #6** (`docs/close-post-baseline-validation` → `main`) |
| Merge de cierre documental | `dc1edea` (**HEAD** de `main`) |
| Total de Pull Requests mergeados | 6 (PR #1 a PR #6) |
| Tags de release | Ninguno (`git tag` vacío) — fuera del alcance del cierre |

---

**Documentos relacionados**: [`04-documentacion-tecnica-cierre.md`](04-documentacion-tecnica-cierre.md) ·
[Diagrama Git/GitHub](diagrams/git-github.md) ·
[Diagrama del flujo Spec → Implementation → Validation](diagrams/flujo-speckit.md)
