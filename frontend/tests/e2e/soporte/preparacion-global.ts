import { execFileSync, execSync } from 'node:child_process'
import { pbkdf2Sync, randomBytes } from 'node:crypto'
import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { RUTA_ENTORNO, type EntornoE2E } from './entorno.ts'

/**
 * Preparación del entorno E2E: SQL Server y la API contenedorizados, base de datos dedicada con las
 * migraciones reales, y usuarios de prueba.
 *
 * Los usuarios se insertan directamente en la base de datos para fijar exactamente el alcance que
 * cada escenario necesita: `admin` es GLOBAL_ADMINISTRATOR y `ajeno` es COMPANY_ADMINISTRATOR de
 * otra compañía, que es lo que permite contrastar el aislamiento (RF-074, RF-077). El arranque de
 * RF-078 crea su propio administrador aparte; estas filas son preparación de pruebas, no una semilla
 * del producto, y viven solo en esta base `_E2E`.
 */

const RAIZ = path.resolve(import.meta.dirname, '../../../..')
const BASE_DATOS = 'EnterpriseAccessControl_E2E'
const SA_PASSWORD = process.env.MSSQL_SA_PASSWORD ?? 'Dev-Password1!'
const PASSWORD_PRUEBAS = 'E2e-Clave-Segura1'

/** Tipo de documento RUC de la semilla versionada de maestros (SeedMaestrosPeru). */
const RUC_ID = '0199b0d0-0001-7000-8000-000000000004'

export default async function preparacionGlobal(): Promise<void> {
  const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:8080'
  const puerto = new URL(apiUrl).port || '8080'

  const entornoDocker = {
    ...process.env,
    MSSQL_DATABASE: BASE_DATOS,
    MSSQL_SA_PASSWORD: SA_PASSWORD,
    API_PORT: puerto,
  }

  // 1. SQL Server; la API se levanta después de migrar, para que /health/ready vea el esquema final.
  execSync('docker compose up -d --build --wait sqlserver', {
    cwd: RAIZ,
    env: entornoDocker,
    stdio: 'inherit',
  })

  // 2. Esquema: las mismas migraciones que se aplican en cualquier otro entorno.
  execFileSync(
    'dotnet',
    [
      'ef',
      'database',
      'update',
      '--project',
      'backend/src/EnterpriseAccessControl.Infrastructure',
      '--startup-project',
      'backend/src/EnterpriseAccessControl.Api',
      '--connection',
      `Server=localhost,1433;Database=${BASE_DATOS};User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=True`,
    ],
    { cwd: RAIZ, stdio: 'inherit' },
  )

  execSync('docker compose up -d --build api', { cwd: RAIZ, env: entornoDocker, stdio: 'inherit' })
  await esperarListo(`${apiUrl}/health/ready`)

  // 3. Usuarios de prueba.
  const anclaId = uuidV7()
  const otraAnclaId = uuidV7()
  const admin = { id: uuidV7(), correo: `e2e.admin.${Date.now()}@empresa.cl` }
  const ajeno = { id: uuidV7(), correo: `e2e.ajeno.${Date.now()}@empresa.cl` }

  sql(`
    INSERT INTO Compania (Id, Nombre, TipoDocumentoId, NumeroDocumento, TipoCompania, Estado, ZonaHorariaIana, CreatedAt, UpdatedAt)
    VALUES ('${anclaId}', 'E2E ancla ${Date.now()}', '${RUC_ID}', '${numero(11)}', 'PRINCIPAL_MANDANTE', 'ACTIVO', 'America/Lima', SYSUTCDATETIME(), SYSUTCDATETIME()),
           ('${otraAnclaId}', 'E2E ancla ajena ${Date.now()}', '${RUC_ID}', '${numero(11)}', 'PRINCIPAL_MANDANTE', 'ACTIVO', 'America/Lima', SYSUTCDATETIME(), SYSUTCDATETIME());

    INSERT INTO Usuario (Id, Correo, CorreoNormalizado, Estado, FechaUltimoCambioPassword, IntentosFallidosConsecutivos, PasswordHash, RequiereCambioPassword, CreatedAt, UpdatedAt)
    VALUES ('${admin.id}', '${admin.correo}', '${admin.correo.toUpperCase()}', 'ACTIVO', SYSUTCDATETIME(), 0, '${hashIdentity(PASSWORD_PRUEBAS)}', 0, SYSUTCDATETIME(), SYSUTCDATETIME()),
           ('${ajeno.id}', '${ajeno.correo}', '${ajeno.correo.toUpperCase()}', 'ACTIVO', SYSUTCDATETIME(), 0, '${hashIdentity(PASSWORD_PRUEBAS)}', 0, SYSUTCDATETIME(), SYSUTCDATETIME());

    INSERT INTO AsignacionRolAdministrativo (Id, UsuarioId, Rol, CompaniaId, FechaHoraInicio, FechaHoraFin, CreatedAt, UpdatedAt)
    VALUES ('${uuidV7()}', '${admin.id}', 'GLOBAL_ADMINISTRATOR', NULL, DATEADD(day, -1, SYSUTCDATETIME()), DATEADD(year, 1, SYSUTCDATETIME()), SYSUTCDATETIME(), SYSUTCDATETIME()),
           ('${uuidV7()}', '${ajeno.id}', 'COMPANY_ADMINISTRATOR', '${otraAnclaId}', DATEADD(day, -1, SYSUTCDATETIME()), DATEADD(year, 1, SYSUTCDATETIME()), SYSUTCDATETIME(), SYSUTCDATETIME());
  `)

  const entorno: EntornoE2E = { apiUrl, password: PASSWORD_PRUEBAS, admin, ajeno, anclaId }

  mkdirSync(path.dirname(RUTA_ENTORNO), { recursive: true })
  writeFileSync(RUTA_ENTORNO, JSON.stringify(entorno, null, 2))
}

function sql(consulta: string): void {
  execFileSync(
    'docker',
    [
      'exec',
      'eac-sqlserver',
      '/opt/mssql-tools18/bin/sqlcmd',
      '-S',
      'localhost',
      '-U',
      'sa',
      '-P',
      SA_PASSWORD,
      '-C',
      '-b',
      '-d',
      BASE_DATOS,
      '-Q',
      consulta,
    ],
    { stdio: 'inherit' },
  )
}

async function esperarListo(url: string, limiteMs = 180_000): Promise<void> {
  const inicio = Date.now()

  while (Date.now() - inicio < limiteMs) {
    try {
      const respuesta = await fetch(url)
      if (respuesta.ok) {
        return
      }
    } catch {
      // La API todavía no acepta conexiones.
    }

    await new Promise((resolver) => setTimeout(resolver, 2_000))
  }

  throw new Error(`La API no quedó lista en ${url}`)
}

/**
 * Hash en el formato V3 de ASP.NET Core Identity (PasswordHasher&lt;T&gt;), que es el que verifica la API:
 * marcador 0x01, PRF, iteraciones y longitud de sal en big-endian, sal y subclave PBKDF2.
 */
function hashIdentity(password: string): string {
  const sal = randomBytes(16)
  const iteraciones = 100_000
  const subclave = pbkdf2Sync(password, sal, iteraciones, 32, 'sha512')

  const salida = Buffer.alloc(13 + sal.length + subclave.length)
  salida[0] = 0x01
  salida.writeUInt32BE(2, 1) // KeyDerivationPrf.HMACSHA512
  salida.writeUInt32BE(iteraciones, 5)
  salida.writeUInt32BE(sal.length, 9)
  sal.copy(salida, 13)
  subclave.copy(salida, 13 + sal.length)

  return salida.toString('base64')
}

/** UUID v7 (Principio II): marca de tiempo de 48 bits seguida de bits aleatorios. */
export function uuidV7(): string {
  const bytes = randomBytes(16)
  const ms = BigInt(Date.now())

  for (let i = 0; i < 6; i++) {
    bytes[i] = Number((ms >> BigInt(8 * (5 - i))) & 0xffn)
  }

  bytes[6] = (bytes[6] & 0x0f) | 0x70
  bytes[8] = (bytes[8] & 0x3f) | 0x80

  const hex = bytes.toString('hex')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

function numero(digitos: number): string {
  return Array.from({ length: digitos }, () => Math.floor(Math.random() * 10)).join('')
}
