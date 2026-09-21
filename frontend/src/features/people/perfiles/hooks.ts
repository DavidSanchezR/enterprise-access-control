import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import {
  asignarPerfil,
  listarPerfiles,
  type AsignacionTipoPersona,
  type AsignacionTipoPersonaRequest,
} from './api'

export const clavesPerfiles = {
  todo: (personaId: string) => ['personas', personaId, 'perfiles'] as const,
}

export function usePerfiles(personaId: string): UseQueryResult<AsignacionTipoPersona[]> {
  return useQuery({
    queryKey: clavesPerfiles.todo(personaId),
    queryFn: () => listarPerfiles(personaId),
    enabled: personaId !== '',
  })
}

export function useAsignarPerfil(personaId: string) {
  const cliente = useQueryClient()

  return useMutation({
    mutationFn: (entrada: AsignacionTipoPersonaRequest) => asignarPerfil(personaId, entrada),
    // El estado efectivo depende de los perfiles vigentes: se invalida todo el histórico de la
    // persona, no solo esta lista.
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['personas', personaId] }),
  })
}

export type { AsignacionTipoPersona, AsignacionTipoPersonaRequest }
