import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { listarTiposPersonaDeArea, reemplazarTiposPersonaDeArea } from './api'

export const clavesTiposPersonaArea = {
  todo: ['areas-acceso', 'tipos-persona'] as const,
  deArea: (areaId: string) => [...clavesTiposPersonaArea.todo, areaId] as const,
}

export function useTiposPersonaDeArea(areaId: string | null): UseQueryResult<string[]> {
  return useQuery({
    queryKey: clavesTiposPersonaArea.deArea(areaId ?? ''),
    queryFn: () => listarTiposPersonaDeArea(areaId!),
    enabled: areaId !== null && areaId !== '',
  })
}

export function useReemplazarTiposPersonaDeArea() {
  const cliente = useQueryClient()

  return useMutation({
    mutationFn: (entrada: { areaId: string; tipoPersonaIds: string[] }) =>
      reemplazarTiposPersonaDeArea(entrada.areaId, entrada.tipoPersonaIds),
    // Solo el área tocada: los conjuntos son independientes entre áreas, incluso dentro del mismo
    // árbol (RF-019 declara la autorización por área, sin herencia).
    onSuccess: (_, entrada) =>
      cliente.invalidateQueries({ queryKey: clavesTiposPersonaArea.deArea(entrada.areaId) }),
  })
}
