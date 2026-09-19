import { readFileSync } from 'node:fs'
import path from 'node:path'

/** Datos que la preparación global deja para las pruebas E2E. */
export interface EntornoE2E {
  apiUrl: string
  password: string
  /** Usuario con alcance sobre la compañía ancla; administra los escenarios. */
  admin: { id: string; correo: string }
  /** Usuario con alcance solo sobre otra compañía: sirve para las pruebas de aislamiento. */
  ajeno: { id: string; correo: string }
  anclaId: string
}

/** Fuera de `test-results`, que Playwright vacía al comenzar cada ejecución. */
export const RUTA_ENTORNO = path.resolve(
  import.meta.dirname,
  '../../../node_modules/.tmp/e2e-entorno.json',
)

export function leerEntorno(): EntornoE2E {
  return JSON.parse(readFileSync(RUTA_ENTORNO, 'utf-8')) as EntornoE2E
}
