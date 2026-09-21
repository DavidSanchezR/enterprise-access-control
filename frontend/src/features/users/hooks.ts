import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import {
  actualizarUsuario,
  asignarRol,
  crearUsuario,
  desbloquearUsuario,
  finalizarRol,
  listarRoles,
  listarUsuarios,
  renovarRol,
  type AsignacionRol,
  type DatosAsignacion,
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
  roles: (id: string) => [...clavesUsuarios.todo, 'roles', id] as const,
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

/** Asignaciones vigentes e históricas de un usuario (UX-18). */
export function useRolesUsuario(id: string | null): UseQueryResult<AsignacionRol[]> {
  return useQuery({
    queryKey: clavesUsuarios.roles(id ?? ''),
    queryFn: () => listarRoles(id!),
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

export function useAsignarRol() {
  const invalidar = useInvalidarUsuarios()

  return useMutation({
    mutationFn: (entrada: { id: string } & DatosAsignacion) =>
      asignarRol(entrada.id, {
        rol: entrada.rol,
        companiaId: entrada.companiaId,
        fechaHoraInicio: entrada.fechaHoraInicio,
        fechaHoraFin: entrada.fechaHoraFin,
      }),
    onSuccess: invalidar,
  })
}

export function useFinalizarRol() {
  const invalidar = useInvalidarUsuarios()

  return useMutation({
    mutationFn: (entrada: { id: string; asignacionId: string }) =>
      finalizarRol(entrada.id, entrada.asignacionId),
    onSuccess: invalidar,
  })
}

/** Extiende la vigencia de una asignación (UX-20). Contraparte de `useFinalizarRol`. */
export function useRenovarRol() {
  const invalidar = useInvalidarUsuarios()

  return useMutation({
    mutationFn: (entrada: { id: string; asignacionId: string; fechaHoraFin: string }) =>
      renovarRol(entrada.id, entrada.asignacionId, entrada.fechaHoraFin),
    onSuccess: invalidar,
  })
}

export type { AsignacionRol, DatosAsignacion, EstadoUsuario, FiltroUsuarios, PaginaUsuarios, Usuario }
