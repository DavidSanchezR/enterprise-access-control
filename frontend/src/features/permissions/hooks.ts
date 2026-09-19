import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import {
  actualizarPermiso,
  crearPermiso,
  listarPermisos,
  type FiltroPermisos,
  type PaginaPermisos,
  type PermisoAccesoRequest,
} from './api'

export const clavesPermisos = {
  todo: ['permisos'] as const,
  lista: (filtro: FiltroPermisos) => [...clavesPermisos.todo, 'lista', filtro] as const,
}

/**
 * Permisos de un área.
 *
 * Solo consulta con un área elegida: el listado sin filtro devolvería permisos de todas las
 * Principales del alcance mezclados, y ux-ui.md §18 exige mostrarlos siempre dentro del contexto de
 * una Principal.
 */
export function usePermisos(filtro: FiltroPermisos): UseQueryResult<PaginaPermisos> {
  return useQuery({
    queryKey: clavesPermisos.lista(filtro),
    queryFn: () => listarPermisos(filtro),
    enabled: Boolean(filtro.areaAccesoId),
    placeholderData: (anterior) => anterior,
  })
}

function useInvalidarPermisos(): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesPermisos.todo })
}

export function useCrearPermiso() {
  const invalidar = useInvalidarPermisos()
  return useMutation({ mutationFn: crearPermiso, onSuccess: invalidar })
}

export function useActualizarPermiso() {
  const invalidar = useInvalidarPermisos()

  return useMutation({
    mutationFn: (entrada: { id: string } & PermisoAccesoRequest) => {
      const { id, ...cuerpo } = entrada
      return actualizarPermiso(id, cuerpo)
    },
    onSuccess: invalidar,
  })
}
