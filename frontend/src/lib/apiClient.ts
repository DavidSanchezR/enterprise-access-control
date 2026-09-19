import axios, { AxiosError, type AxiosInstance } from 'axios'
import { esProblemDetails, mensajeDeProblema, type ProblemDetails } from './problemDetails'

const SESION_STORAGE_KEY = 'eac.sesion'

export interface SesionAlmacenada {
  accessToken: string
  expiraEn: string
  alcanceCompanias: string[]
  requiereCambioPassword: boolean
}

export function leerSesion(): SesionAlmacenada | null {
  try {
    const bruto = localStorage.getItem(SESION_STORAGE_KEY)
    return bruto ? (JSON.parse(bruto) as SesionAlmacenada) : null
  } catch {
    return null
  }
}

export function guardarSesion(sesion: SesionAlmacenada): void {
  localStorage.setItem(SESION_STORAGE_KEY, JSON.stringify(sesion))
}

export function borrarSesion(): void {
  localStorage.removeItem(SESION_STORAGE_KEY)
}

/**
 * Error de API ya normalizado a ProblemDetails, para que las pantallas no tengan que conocer axios.
 */
export class ApiError extends Error {
  readonly problema: ProblemDetails | undefined
  readonly status: number | undefined

  constructor(problema: ProblemDetails | undefined, status: number | undefined) {
    super(mensajeDeProblema(problema))
    this.name = 'ApiError'
    this.problema = problema
    this.status = status
  }

  /** Código de negocio estable (extensión `codigo` de ProblemDetails). */
  get codigo(): string | undefined {
    return this.problema?.codigo
  }
}

export function crearApiClient(baseURL: string): AxiosInstance {
  const client = axios.create({
    baseURL,
    headers: { 'Content-Type': 'application/json' },
  })

  // Inyección del token en cada request: ningún componente manipula cabeceras de autenticación.
  client.interceptors.request.use((config) => {
    const sesion = leerSesion()
    if (sesion?.accessToken) {
      config.headers.Authorization = `Bearer ${sesion.accessToken}`
    }
    return config
  })

  client.interceptors.response.use(
    (response) => response,
    (error: AxiosError) => {
      const data = error.response?.data
      const problema = esProblemDetails(data) ? data : undefined

      // 401: el token expiró o es inválido — se limpia la sesión para que el guard redirija al
      // login en lugar de dejar la SPA en un estado a medias.
      if (error.response?.status === 401) {
        borrarSesion()
      }

      return Promise.reject(new ApiError(problema, error.response?.status))
    },
  )

  return client
}

/**
 * Por defecto la SPA llama a la API en su **mismo origen** (rutas relativas `/api/...`): en desarrollo
 * las reenvía el proxy de Vite (vite.config.ts) y en despliegue un proxy inverso. Así la API no
 * necesita abrir CORS a ningún origen externo.
 *
 * `VITE_API_BASE_URL` solo se define para apuntar deliberadamente a una API en otro origen, lo que
 * exigiría configurar CORS en ella.
 */
export const apiClient = crearApiClient(
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '',
)
