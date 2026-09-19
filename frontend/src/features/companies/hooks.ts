import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import {
  actualizarCompania,
  crearCompania,
  crearRelacion,
  finalizarRelacion,
  listarCompanias,
  listarRelaciones,
  type CompaniaRequest,
  type FiltroCompanias,
  type PaginaCompanias,
  type RelacionContratistaPrincipal,
} from './api'

export const clavesCompanias = {
  todo: ['companias'] as const,
  lista: (filtro: FiltroCompanias) => [...clavesCompanias.todo, 'lista', filtro] as const,
  relaciones: (contratistaId: string) =>
    [...clavesCompanias.todo, 'relaciones', contratistaId] as const,
}

export function useCompanias(filtro: FiltroCompanias): UseQueryResult<PaginaCompanias> {
  return useQuery({
    queryKey: clavesCompanias.lista(filtro),
    queryFn: () => listarCompanias(filtro),
    placeholderData: (anterior) => anterior,
  })
}

export function useRelaciones(
  contratistaId: string | null,
): UseQueryResult<RelacionContratistaPrincipal[]> {
  return useQuery({
    queryKey: clavesCompanias.relaciones(contratistaId ?? ''),
    queryFn: () => listarRelaciones(contratistaId!),
    enabled: contratistaId !== null,
  })
}

function useInvalidarCompanias(): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesCompanias.todo })
}

export function useCrearCompania() {
  const invalidar = useInvalidarCompanias()
  return useMutation({ mutationFn: crearCompania, onSuccess: invalidar })
}

export function useActualizarCompania() {
  const invalidar = useInvalidarCompanias()

  return useMutation({
    mutationFn: (entrada: { id: string } & CompaniaRequest) => {
      const { id, ...resto } = entrada
      return actualizarCompania(id, resto)
    },
    onSuccess: invalidar,
  })
}

export function useCrearRelacion() {
  const invalidar = useInvalidarCompanias()

  return useMutation({
    mutationFn: (entrada: {
      contratistaId: string
      companiaPrincipalId: string
      fechaHoraInicio: string
    }) =>
      crearRelacion(entrada.contratistaId, {
        companiaPrincipalId: entrada.companiaPrincipalId,
        fechaHoraInicio: entrada.fechaHoraInicio,
      }),
    onSuccess: invalidar,
  })
}

export function useFinalizarRelacion() {
  const invalidar = useInvalidarCompanias()

  return useMutation({
    mutationFn: (entrada: { contratistaId: string; relacionId: string }) =>
      finalizarRelacion(entrada.contratistaId, entrada.relacionId),
    onSuccess: invalidar,
  })
}
