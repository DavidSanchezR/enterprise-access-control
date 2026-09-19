import { useCallback, useSyncExternalStore } from 'react'
import { borrarSesion, guardarSesion, leerSesion, type SesionAlmacenada } from '../../lib/apiClient'

/**
 * Suscriptores a cambios de la sesión dentro de esta pestaña.
 *
 * `localStorage` emite el evento `storage` sólo en *otras* pestañas, nunca en la que escribe. Sin
 * este canal propio, iniciar o cerrar sesión no volvería a renderizar los componentes que dependen
 * de ella y la interfaz quedaría mostrando el estado anterior.
 */
const suscriptores = new Set<() => void>()

function notificar(): void {
  for (const suscriptor of suscriptores) {
    suscriptor()
  }
}

function suscribir(alCambiar: () => void): () => void {
  suscriptores.add(alCambiar)
  window.addEventListener('storage', alCambiar)

  return () => {
    suscriptores.delete(alCambiar)
    window.removeEventListener('storage', alCambiar)
  }
}

/**
 * Instantánea estable de la sesión.
 *
 * `useSyncExternalStore` compara por identidad, así que devolver un objeto nuevo en cada lectura
 * provocaría un bucle infinito de renderizado. Se memoriza el último JSON leído y sólo se
 * deserializa de nuevo cuando el texto almacenado cambia.
 */
let ultimoBruto: string | null = null
let ultimaSesion: SesionAlmacenada | null = null

function instantanea(): SesionAlmacenada | null {
  const bruto = localStorage.getItem('eac.sesion')

  if (bruto !== ultimoBruto) {
    ultimoBruto = bruto
    ultimaSesion = leerSesion()
  }

  return ultimaSesion
}

export interface Sesion {
  sesion: SesionAlmacenada | null
  autenticado: boolean
  requiereCambioPassword: boolean
  iniciar: (sesion: SesionAlmacenada) => void
  cerrar: () => void
  marcarPasswordCambiada: () => void
}

export function useSesion(): Sesion {
  const sesion = useSyncExternalStore(suscribir, instantanea, () => null)

  const iniciar = useCallback((nueva: SesionAlmacenada) => {
    guardarSesion(nueva)
    notificar()
  }, [])

  const cerrar = useCallback(() => {
    borrarSesion()
    notificar()
  }, [])

  const marcarPasswordCambiada = useCallback(() => {
    const actual = leerSesion()
    if (actual) {
      guardarSesion({ ...actual, requiereCambioPassword: false })
      notificar()
    }
  }, [])

  return {
    sesion,
    autenticado: sesion !== null,
    requiereCambioPassword: sesion?.requiereCambioPassword === true,
    iniciar,
    cerrar,
    marcarPasswordCambiada,
  }
}
