import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import {
  actualizarUsuario,
  crearUsuario,
  desbloquearUsuario,
  listarUsuarios,
  obtenerAlcance,
  reemplazarAlcance,
  type EstadoUsuario,
  type FiltroUsuarios,
  type PaginaUsuarios,
  type Usuario,
} from './api'

/**
 * Claves de caché. Se derivan del filtro para que cambiar de página o de estado no reutilice datos
 * de otra consulta, y para poder invalidar todas las listas de una vez tras una escritura.
 */
export const clavesUsuarios = {
  todo: ['usuarios'] as const,
  listas: () => [...clavesUsuarios.todo, 'lista'] as const,
  lista: (filtro: FiltroUsuarios) => [...clavesUsuarios.listas(), filtro] as const,
  alcance: (id: string) => [...clavesUsuarios.todo, 'alcance', id] as const,
}

export function useUsuarios(filtro: FiltroUsuarios): UseQueryResult<PaginaUsuarios> {
  return useQuery({
    queryKey: clavesUsuarios.lista(filtro),
    queryFn: () => listarUsuarios(filtro),
    // La página anterior permanece visible mientras llega la siguiente: evita el parpadeo de una
    // tabla que se vacía en cada cambio de página.
    placeholderData: (anterior) => anterior,
  })
}

export function useAlcanceUsuario(id: string | null): UseQueryResult<string[]> {
  return useQuery({
    queryKey: clavesUsuarios.alcance(id ?? ''),
    queryFn: () => obtenerAlcance(id!),
    enabled: id !== null,
  })
}

/** Invalida toda la caché de usuarios: cualquier escritura puede alterar cualquier página. */
function useInvalidarUsuarios(): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesUsuarios.todo })
}

export function useCrearUsuario() {
  const invalidar = useInvalidarUsuarios()

  return useMutation({
    mutationFn: crearUsuario,
    onSuccess: invalidar,
  })
}

export function useActualizarUsuario() {
  const invalidar = useInvalidarUsuarios()

  return useMutation({
    mutationFn: (entrada: { id: string; correo: string; estado: EstadoUsuario }) =>
      actualizarUsuario(entrada.id, { correo: entrada.correo, estado: entrada.estado }),
    onSuccess: invalidar,
  })
}

export function useDesbloquearUsuario() {
  const invalidar = useInvalidarUsuarios()

  return useMutation({
    mutationFn: (id: string) => desbloquearUsuario(id),
    onSuccess: invalidar,
  })
}

export function useReemplazarAlcance() {
  const invalidar = useInvalidarUsuarios()

  return useMutation({
    mutationFn: (entrada: { id: string; companiaIds: string[] }) =>
      reemplazarAlcance(entrada.id, entrada.companiaIds),
    onSuccess: invalidar,
  })
}

export type { EstadoUsuario, FiltroUsuarios, PaginaUsuarios, Usuario }
