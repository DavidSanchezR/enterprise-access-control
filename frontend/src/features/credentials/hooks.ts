import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import { clavesHistorial } from '../people/history/hooks'
import {
  asignarCredencial,
  devolverCredencial,
  eliminarCredencial,
  listarCredenciales,
  type AsignacionCredencial,
  type AsignacionCredencialRequest,
} from './api'

/**
 * Las credenciales cuelgan del histórico de la persona: finalizar su pertenencia las revoca en
 * cascada (RF-061), y la invalidación de ese histórico debe alcanzarlas también.
 */
export const clavesCredenciales = {
  dePersona: (personaId: string) => [...clavesHistorial.todo(personaId), 'credenciales'] as const,
}

export function useCredenciales(personaId: string): UseQueryResult<AsignacionCredencial[]> {
  return useQuery({
    queryKey: clavesCredenciales.dePersona(personaId),
    queryFn: () => listarCredenciales(personaId),
  })
}

function useInvalidarCredenciales(personaId: string): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesCredenciales.dePersona(personaId) })
}

export function useAsignarCredencial(personaId: string) {
  const invalidar = useInvalidarCredenciales(personaId)

  return useMutation({
    mutationFn: (entrada: AsignacionCredencialRequest) => asignarCredencial(personaId, entrada),
    onSuccess: invalidar,
  })
}

export function useDevolverCredencial(personaId: string) {
  const invalidar = useInvalidarCredenciales(personaId)

  return useMutation({
    mutationFn: (credencialId: string) => devolverCredencial(personaId, credencialId),
    onSuccess: invalidar,
  })
}

export function useEliminarCredencial(personaId: string) {
  const invalidar = useInvalidarCredenciales(personaId)

  return useMutation({
    mutationFn: (credencialId: string) => eliminarCredencial(personaId, credencialId),
    onSuccess: invalidar,
  })
}
