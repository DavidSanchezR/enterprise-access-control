# Quickstart — Validación de Control de Acceso Empresarial

Guía de validación end-to-end para comprobar que la funcionalidad implementada cumple las historias P1 y
CS-009. No incluye código de implementación (modelos, servicios, migraciones); esos artefactos se generan
en las fases de `tasks.md` e implementación. Referencia los contratos en [`contracts/`](./contracts/) y las
entidades en [`data-model.md`](./data-model.md).

## Prerrequisitos

- .NET 10 SDK
- Node.js LTS (20+) y npm
- Docker Desktop (o motor Docker compatible) — requerido por `Testcontainers.MsSql` en las pruebas de
  integración, y para levantar SQL Server local de desarrollo (imagen `mcr.microsoft.com/mssql/server`,
  stack ratificado en la Sesión 2026-09-14 "Stack Tecnológico Oficial" — reemplaza PostgreSQL)

## 1. Levantar dependencias locales

```bash
docker run --name eac-sqlserver -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Dev-Password1!" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

No se requiere ninguna extensión adicional: el mecanismo de no-solapamiento de vigencia se implementa con un
trigger `AFTER INSERT, UPDATE` por tabla, aplicado como parte de las migraciones de EF Core (research.md §5)
— no como una extensión de servidor que deba habilitarse manualmente.

## 2. Backend: restaurar, migrar y ejecutar

El primer arranque crea automáticamente el administrador inicial (RF-078) y exige `Bootstrap__AdminPassword`
por variable de entorno: no existe ningún valor por defecto en `appsettings.json` ni en
`appsettings.Development.json`, y la API no arranca sin ella.

```bash
cd backend
dotnet restore
export Bootstrap__AdminPassword='<contraseña-que-cumpla-la-política-de-contraseñas>'
dotnet ef database update --project src/EnterpriseAccessControl.Infrastructure \
  --startup-project src/EnterpriseAccessControl.Api
dotnet run --project src/EnterpriseAccessControl.Api
```

Verificación esperada: la API expone el documento OpenAPI generado en `/openapi/v1.json` (generador nativo
`Microsoft.AspNetCore.OpenApi`, research.md §10) y debe ser consistente con los archivos publicados en
`contracts/` (validado formalmente por `ContractTests`, paso 4); `GET /health/live` y `GET /health/ready`
responden `200` (research.md §19).

## 3. Frontend: instalar y ejecutar

```bash
cd frontend
npm install
npm run dev
```

Verificación esperada: la SPA carga en el navegador y la pantalla de login llama a
`POST /api/auth/login` (ver [`contracts/auth.yaml`](./contracts/auth.yaml)). La SPA llama a la API en su
mismo origen: el servidor de Vite reenvía `/api` y `/health` a `VITE_API_PROXY_TARGET` (por defecto
`http://localhost:5290`, la API de `dotnet run`; `http://localhost:8080` si la API corre con
`docker compose`), así que la API no necesita CORS.

## 4. Ejecutar la suite de pruebas automatizadas

```bash
# Backend: unitarias + integración (requiere Docker activo) + contrato
cd backend
dotnet test tests/EnterpriseAccessControl.UnitTests
dotnet test tests/EnterpriseAccessControl.IntegrationTests
dotnet test tests/EnterpriseAccessControl.ContractTests

# Frontend: unitarias/componentes + end-to-end
cd ../frontend
npm run test        # Vitest
npm run test:e2e     # Playwright
```

Cobertura mínima esperada (Principio VII, CS-008): cada historia P1 tiene al menos una prueba automatizada
que la verifica de forma independiente, con casos explícitos de denegación por defecto, ciclos jerárquicos,
solapamientos temporales inválidos y fuga de datos entre compañías.

## 5. Escenario de validación manual — CS-009

Reproduce el criterio de éxito CS-009 ("un usuario debe poder crear una jerarquía de tres niveles y
configurar un permiso horario") usando la API documentada en `contracts/`:

1. **Login** — `POST /api/auth/login` con un usuario `ACTIVO` cuyo alcance incluya al menos una compañía.
   *(Actualizado en la Sesión 2026-09-20, RF-078: el primer administrador ya no se crea a mano.)* El primer
   arranque de la API crea automáticamente, si no existe ninguno, un usuario con rol `GLOBAL_ADMINISTRATOR`
   tomando su correo de `Bootstrap:AdminEmail` y su contraseña de `Bootstrap:AdminPassword` (variable de
   entorno `Bootstrap__AdminPassword` si no hay gestor de secretos; nunca un valor versionado en el
   repositorio). La rutina es idempotente: reiniciar la API no crea un segundo administrador.

   Ese primer login devuelve `requiereCambioPassword = true`, de modo que el siguiente paso obligatorio es
   `POST /api/auth/cambiar-password` con una contraseña que cumpla la política vigente; recién después se
   continúa con el escenario. Verificar también que la respuesta del login indique el rol
   `GLOBAL_ADMINISTRATOR` (alcance global, sin compañías enumeradas). La automatización E2E de esta
   sección —T159— aprovisiona su propio usuario en la preparación, sin depender de esta rutina.
2. **Compañía Principal** — `POST /api/companias` con `tipoCompania = PRINCIPAL_MANDANTE` y `estado =
   ACTIVO` (RF-042). *(Actualizado en la Sesión 2026-09-20: el alcance administrativo se expresa mediante
   `AsignaciónRolAdministrativo`, no mediante una lista plana de compañías — RF-074 a RF-077.)* Si se continúa
   con el administrador del paso 1, no hace falta ninguna acción: su rol `GLOBAL_ADMINISTRATOR` tiene alcance
   sobre todas las compañías, incluidas las creadas después de su asignación (RF-074, RF-077). Para ejecutar
   el resto del escenario como `COMPANY_ADMINISTRATOR` de esta compañía, crear la asignación con
   `POST /api/usuarios/{id}/roles` —`rol = COMPANY_ADMINISTRATOR`, `companiaId` = la compañía recién creada, y
   `fechaHoraInicio`/`fechaHoraFin` reales y obligatorias (RF-075)— e iniciar sesión de nuevo, porque el
   alcance viaja en el token (RF-005, RF-077).
3. **Jerarquía de 3 niveles de áreas de acceso** — `POST /api/areas-acceso` tres veces encadenadas:
   - Nivel 1 (raíz): `areaSuperiorId = null`, `companiaPrincipalId` = ID de la Compañía Principal del
     paso 2 (RF-046).
   - Nivel 2: `areaSuperiorId` = ID del nivel 1 (hereda `companiaPrincipalId`).
   - Nivel 3: `areaSuperiorId` = ID del nivel 2 (hereda `companiaPrincipalId`).
   Verificar con `GET /api/areas-acceso/arbol?companiaPrincipalId={id}` que los tres nodos aparecen
   anidados correctamente.
4. **Prueba de rechazo de ciclo** — intentar `POST /api/areas-acceso/{nivel1Id}/mover` con
   `nuevoPadreId = nivel3Id`. Debe responder `409` (RF-038, Principio V).
5. **Tipo de persona y persona** — crear un `TipoPersona` (`POST /api/maestros/tipos-persona`), una
   `Persona` (`POST /api/personas`) con todos los campos obligatorios de Historia 4, y asignarle el perfil
   (`POST /api/personas/{id}/perfiles`, con `fechaHoraInicio`/`fechaHoraFin` reales — RF-071) y una compañía
   vigente (`POST /api/personas/{id}/historial-companias`) apuntando a la Compañía Principal del paso 2, con
   `fechaHoraInicio`/`fechaHoraFin` reales y obligatorias (p. ej. `fechaHoraInicio` = ahora,
   `fechaHoraFin` = ahora + 1 año — RF-071; ya no admite `null` ni fecha centinela). Esta ventana es la
   referencia de contención para los pasos 7 y 9 (RF-072).
6. **Autorizar el tipo de persona en el área de nivel 3** —
   `PUT /api/areas-acceso/{nivel3Id}/tipos-persona` incluyendo el `TipoPersona` creado.
7. **Contexto operativo** — `POST /api/personas/{id}/contextos-operativos` con `companiaPrincipalId` = la
   Compañía Principal del paso 2 y `fechaHoraInicio`/`fechaHoraFin` reales, contenidas dentro de la vigencia
   de la pertenencia del paso 5 (RF-072; p. ej. la misma ventana de un año). Como la persona pertenece
   directamente a esa Principal, la asignación se acepta automáticamente sin necesitar relación
   Contratista↔Principal (RF-053). **Este paso es obligatorio incluso para un permiso de alcance PERSONA**:
   el algoritmo de evaluación de 14 pasos exige un contexto operativo vigente con la Principal propietaria
   del área, y una credencial vigente con esa misma Principal (paso 9), antes de evaluar cualquier permiso
   (RF-059, RF-066, research.md §7).
8. **Permiso con bloque horario** — `POST /api/permisos` con `alcance = PERSONA`, `personaId` de la
   persona creada, `areaAccesoId` del nivel 3, `fechaHoraInicioVigencia`/`fechaHoraFinVigencia` reales y
   obligatorias que cubran "ahora" (RF-021, RF-071), y un `bloqueHorario` que cubra el día de semana y hora
   actuales en `America/Lima`.
9. **Tipo de credencial y asignación** — crear un `TipoCredencial` (`POST /api/maestros/tipos-credencial`)
   con `estado = ACTIVO`, y asignarlo a la persona para la Compañía Principal del paso 2
   (`POST /api/personas/{id}/credenciales`) con `fechaHoraInicio`/`fechaHoraFin` reales, contenidas dentro
   de la vigencia de la pertenencia del paso 5 (RF-072). **Este paso es obligatorio para que el paso 10
   conceda acceso**: la evaluación exige una `AsignacionCredencial` vigente (`Estado = ASIGNADO`, dentro de
   su ventana) para la Principal evaluada, además de los permisos — es condición necesaria, no suficiente
   (RF-066, RF-070, RF-071).
10. **Evaluar acceso** — `POST /api/evaluacion-acceso` con esa persona, esa área y la fecha/hora actual.
    Resultado esperado: `resultado = CONCEDIDO`, `nivelAplicado = PERSONA`,
    `contextoOperativoId` = el creado en el paso 7.
11. **Prueba de denegación por defecto** — repetir el paso 10 con una `fechaHora` fuera del bloque horario
    configurado. Resultado esperado: `resultado = DENEGADO`,
    `motivoDenegacion = FUERA_DE_BLOQUE_HORARIO`.
12. **Prueba de aislamiento entre compañías** — con un segundo usuario cuyo `alcanceCompanias` NO incluye la
    compañía creada en el paso 2, repetir `GET /api/personas/{id}` de la persona creada. Resultado esperado:
    `404` (RF-005, Historia 1 criterio 4).

Si los 12 pasos producen los resultados esperados, la funcionalidad cumple CS-001 (implícito en el paso 1),
CS-003 (medir latencia del paso 10), CS-006 (paso 4), CS-009 (pasos 3–10), CS-020 (paso 7), CS-022 (paso 9,
credencial requiere Compañía Principal y TipoCredencial), CS-031 (paso 9, gate de credencial) y el criterio
de aislamiento por compañía del Principio I (paso 12).

## 6. Escenario adicional — Contexto Operativo multi-Principal (Pedro García / Servicios ACME)

Reproduce el ejemplo completo de la corrección Contexto Operativo (RF-051 a RF-060; Historia 2, Historia 5,
Historia 9): una persona de una Compañía Contratista trabajando **simultáneamente** para dos Compañías
Principales distintas, cada una con su propia unidad organizativa, permiso y credencial independientes.
Usa la Compañía Principal ("Minera ABC") y la jerarquía de áreas creadas en la sección 5.

1. **Unidad organizativa raíz de Minera ABC** — `POST /api/unidades-organizativas` con
   `unidadSuperiorId = null` y `companiaPrincipalId` = ID de Minera ABC (paso 2 de la sección 5). Verificar
   con `GET /api/unidades-organizativas/arbol?companiaPrincipalId={id}` que aparece como raíz (RF-045).
2. **Segunda Compañía Principal ("Minera XYZ"), con su propia unidad raíz** — `POST /api/companias` con
   `tipoCompania = PRINCIPAL_MANDANTE`, y `POST /api/unidades-organizativas` con una unidad raíz propia.
   Verificar que `GET /api/unidades-organizativas/arbol?companiaPrincipalId={idMineraXYZ}` NO incluye la
   unidad de Minera ABC del paso 1 (CS-011: dos Principales nunca comparten unidades organizativas).
3. **Área de acceso en Minera XYZ** — `POST /api/areas-acceso` con `companiaPrincipalId` = Minera XYZ (un
   área raíz simple basta para este escenario), y autorizar en ella el `TipoPersona` creado en el paso 5 de
   la sección 5 (`PUT /api/areas-acceso/{id}/tipos-persona` — RF-019, RF-024).
4. **Compañía Contratista ("Servicios ACME")** — `POST /api/companias` con `tipoCompania = CONTRATISTA`.
   Intentar `POST /api/unidades-organizativas` con `unidadSuperiorId = null` y `companiaPrincipalId`
   apuntando a Servicios ACME. Resultado esperado: `400` (RF-045 — una Contratista no puede poseer unidades
   organizativas).
5. **Persona de la Contratista ("Pedro García")** — crear una `Persona`, asignarle como compañía vigente
   Servicios ACME (`POST /api/personas/{id}/historial-companias`) con `fechaHoraInicio`/`fechaHoraFin`
   reales y obligatorias (p. ej. `fechaHoraInicio` = ahora, `fechaHoraFin` = ahora + 1 año — RF-071; ya no
   admite `null`). Esta ventana es la referencia de contención (RF-072) para los pasos 6, 8, 9 y 10.
   Asignarle además el perfil de ese mismo `TipoPersona` (`POST /api/personas/{id}/perfiles`, con
   `fechaHoraFin` obligatoria — RF-071; el perfil no está sujeto a contención, RF-072): el paso 8 del
   algoritmo de evaluación exige un perfil vigente autorizado en el área antes de evaluar permisos (RF-024,
   research.md §7).
6. **Contexto operativo sin relación vigente — debe rechazarse** — intentar
   `POST /api/personas/{id}/contextos-operativos` con `companiaPrincipalId` = Minera ABC y
   `fechaHoraInicio`/`fechaHoraFin` dentro de la ventana del paso 5. Resultado esperado: `400` (RF-054 —
   Servicios ACME todavía no tiene relación vigente con ninguna Principal; el rechazo es por esta razón, no
   por fechas, que ya cumplen RF-072).
7. **Relaciones Contratista↔Principal simultáneas** —
   `POST /api/companias/{servicioAcmeId}/relaciones-principales` dos veces: una con `companiaPrincipalId`
   = Minera ABC, otra con `companiaPrincipalId` = Minera XYZ, ambas con vigencia que cubra "ahora" (CS-012:
   relaciones simultáneas con Principales distintas). `RelaciónContratistaPrincipal` no está vinculada a una
   persona — su `fechaHoraFin` sigue siendo opcional (RF-071 no le aplica).
8. **Dos contextos operativos simultáneos** — repetir el paso 6 (ahora debe aceptarse, `201`), y repetir
   para Minera XYZ, ambos con `fechaHoraInicio`/`fechaHoraFin` contenidas en la ventana del paso 5 (RF-072).
   Verificar con `GET /api/personas/{id}/contextos-operativos` que ambos contextos aparecen vigentes al
   mismo tiempo (CS-013).
9. **Unidad organizativa distinta por contexto** —
   `POST /api/personas/{id}/contextos-operativos/{contextoAbcId}/unidad-organizativa` con la unidad de
   Minera ABC del paso 1, y `POST .../{contextoXyzId}/unidad-organizativa` con la unidad de Minera XYZ del
   paso 2 — ambas con `fechaHoraInicio`/`fechaHoraFin` reales, contenidas en la ventana del paso 5 (RF-071,
   RF-072). Verificar con `GET /api/personas/{id}/estado-efectivo?fechaHora=ahora` que
   `contextosOperativosVigentes` contiene dos entradas, cada una con su propia
   `unidadOrganizativaVigenteId` (CS-014).
10. **Permisos y credenciales independientes por Principal** — crear un `PermisoAcceso` de alcance
    `UNIDAD_ORGANIZATIVA` sobre el área de Minera XYZ (paso 3) referenciando la unidad del contexto XYZ, con
    `fechaHoraInicioVigencia`/`fechaHoraFinVigencia` reales y obligatorias (RF-021, RF-071) y un bloque
    horario que cubra el día de semana y la hora actuales en `America/Lima` (RF-022); y dos
    `AsignacionCredencial` para Pedro (previa creación de un `TipoCredencial` vía
    `POST /api/maestros/tipos-credencial`) — una con `companiaPrincipalId` = Minera ABC, otra con
    `companiaPrincipalId` = Minera XYZ, ambas `Estado = ASIGNADO` simultáneamente, con
    `fechaHoraInicio`/`fechaHoraFin` reales contenidas en la ventana del paso 5 (RF-071, RF-072). Verificar
    que ambas coexisten sin conflicto (CS-016, CS-017) y que cada una requirió su propio contexto operativo
    vigente (RF-056).
11. **Evaluar acceso al área de Minera XYZ** — `POST /api/evaluacion-acceso` con Pedro, el área del paso 3,
    y la fecha/hora actual. Resultado esperado: `resultado = CONCEDIDO`, `nivelAplicado =
    UNIDAD_ORGANIZATIVA`, `companiaPrincipalId` = Minera XYZ, `contextoOperativoId` = el contexto XYZ del
    paso 8 — confirma que Pedro accede a través de su contexto operativo con Minera XYZ, no por ningún
    vínculo implícito con Servicios ACME (RF-048, RF-059).
12. **Aislamiento cruzado entre Principales** — `POST /api/evaluacion-acceso` con Pedro, un área
    perteneciente a Minera ABC (nivel 3 de la sección 5) y la fecha/hora actual, sin haber creado ningún
    permiso para Pedro en Minera ABC. Resultado esperado: `resultado = DENEGADO`,
    `motivoDenegacion = SIN_PERMISO_APLICABLE` — el permiso creado en el paso 10 (contexto de Minera XYZ)
    nunca se evalúa para un área de Minera ABC (CS-018).

*(Sesión 2026-09-15: se añadieron al paso 3 la autorización del tipo de persona en el área, al paso 5 la
asignación del perfil y al paso 10 el bloque horario. Sin ellos, los pasos 11 y 12 terminaban en
`PERFIL_NO_AUTORIZADO_EN_AREA` —el paso 8 del algoritmo de 14 pasos se evalúa antes que los permisos— y el
permiso del paso 10 era rechazado por no tener bloques. Es una corrección de la guía respecto de RF-022,
RF-024 y research.md §7, sin cambio de reglas.)*

Si los 12 pasos producen los resultados esperados, la funcionalidad cumple RF-043, RF-045, RF-047, RF-048,
RF-051 a RF-060, RF-071 y RF-072 (fechas obligatorias y contención temporal en los pasos 5, 6, 8, 9 y 10), y
el escenario de ejemplo completo de la corrección Contexto Operativo (Pedro García / Servicios ACME /
Minera ABC / Minera XYZ).

## 7. Escenarios de validación del cierre de Etapa 1 (Decisiones D1 a D9, Sesión 2026-09-20)

Estos escenarios validan las correcciones planificadas en `research.md` §27-§33. **Implementados en la
sesión del 2026-09-20** (tareas T169 a T228) y cubiertos por pruebas automatizadas, de modo que ejecutarlos
a mano es una verificación de confirmación y no la única evidencia:

| Escenario | Cobertura automatizada |
|---|---|
| 1 — Aislamiento administrativo (D1, D3) | `RolesAdministrativosTests` (CS-036, CS-037) |
| 2 — Bootstrap (D2) | `RolesAdministrativosTests.CS038_*` |
| 3 — Inactivación de Compañía (D4) | `CompaniaInactivaYZonaHorariaTests.CS039_*` |
| 4 — Zona horaria por compañía (D5) | `CompaniaInactivaYZonaHorariaTests.CS040_*` |
| 5 — Cambio de TipoCompania (D6) | `CambioTipoCompaniaTests` (CS-041) |
| 6 — Interfaz de Historia 5 (D7) | `historia5-casos-a-b.spec.ts` (Playwright) |

Referencian los mismos usuarios/compañías de las secciones 5 y 6 cuando sea posible.

1. **Aislamiento administrativo (D1, D3 — cierra F-01/F-02)**: con un usuario `COMPANY_ADMINISTRATOR` cuya
   única asignación de rol es sobre Minera ABC (sección 5), listar `GET /api/usuarios` — resultado esperado:
   solo usuarios con alguna asignación vigente en Minera ABC, nunca la lista completa del sistema.
   `POST /api/usuarios/{id}/roles` intentando asignar `GLOBAL_ADMINISTRATOR`, o `COMPANY_ADMINISTRATOR` para
   Minera XYZ (sección 6) — resultado esperado: `403`. `PUT /api/unidades-organizativas/{id}` sobre una
   unidad de Minera XYZ — resultado esperado: `404` (no `403`, para no confirmar existencia fuera de
   alcance).
2. **Bootstrap (D2)**: desde una base recién migrada, arrancar la API sin ningún `Usuario` existente —
   resultado esperado: se crea automáticamente un único `Usuario` con `Rol = GLOBAL_ADMINISTRATOR`,
   correo/contraseña provenientes de `Bootstrap:AdminEmail`/`Bootstrap:AdminPassword`,
   `requiereCambioPassword = true`. Reiniciar la API — resultado esperado: no se crea un segundo Global
   Administrator (idempotencia).
3. **Inactivación de Compañía (D4)**: repetir el paso 10 de la sección 5 (evaluación `CONCEDIDO`) y luego
   `PUT /api/companias/{id}` sobre Minera ABC con `estado = INACTIVO`; repetir la evaluación — resultado
   esperado: `DENEGADO`, `motivoDenegacion = COMPANIA_INACTIVA`, sin que ningún contexto/UO/credencial de la
   persona cambie de estado. Reactivar (`estado = ACTIVO`) y repetir — resultado esperado: `CONCEDIDO` de
   nuevo, sin ninguna acción adicional.
4. **Zona horaria por compañía (D5)**: configurar `zonaHorariaIana` distinta en Minera ABC y Minera XYZ
   (sección 6, p. ej. `America/Lima` y `America/Santiago`); evaluar acceso a un área de cada una a la misma
   `fechaHora` UTC, con bloques horarios que solo cubran la hora local de una de las dos zonas — resultado
   esperado: `CONCEDIDO` en la que coincide con su hora local, `DENEGADO`/`FUERA_DE_BLOQUE_HORARIO` en la
   otra, confirmando que cada Compañía Principal usa su propia zona.
5. **Cambio de TipoCompania con dependientes (D6)**: intentar `PUT /api/companias/{id}` cambiando Minera ABC
   (con áreas/UO ya creadas en la sección 5) a `CONTRATISTA` — resultado esperado: `409`,
   `codigo = CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS`. Repetir con una compañía recién creada sin dependientes
   — resultado esperado: `200`, cambio aceptado en cualquier dirección.
6. **Interfaz de Historia 5 (D7)**: desde la UI (no la API), para una persona de una Contratista sin
   pertenencia previa, completar el wizard de asignación: crear pertenencia → Caso B, seleccionar Principal
   entre las relaciones vigentes de su Contratista → abrir contexto → seleccionar unidad organizativa
   mediante el árbol → asociar perfil — resultado esperado: los mismos efectos de dominio que crear cada
   recurso por API (secciones 5 y 6), verificables con `GET /api/personas/{id}/estado-efectivo`.

Decisiones sin escenario de validación funcional (no requieren uno): D8 (fuera de alcance de Etapa 1, sin
funcionalidad que probar) y D9 (ratifican comportamiento ya cubierto por las suites existentes de política de
contraseñas y de `CredencialService`).

## 8. Próximos pasos

Este quickstart valida el comportamiento end-to-end una vez implementado. La secuencia de construcción
(entidades → migraciones → casos de uso → endpoints → UI) se define en `tasks.md`, generado por el comando
`/speckit-tasks` a partir de este plan. Las tareas de corrección del cierre de Etapa 1 (D1-D9) se agregarán
como tareas nuevas (numeración ≥T169) en una futura ejecución de `/speckit-tasks`, sin renumerar T001–T168.
