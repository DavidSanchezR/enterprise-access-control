import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import {
  abrirContexto,
  asignarUnidad,
  crearPertenencia,
  finalizarPertenencia,
  listarContextos,
  listarHistorialCompanias,
  listarUnidadesDelContexto,
  renovarPertenencia,
  type AsignacionCompania,
  type AsignacionUnidadOrganizativa,
  type ContextoOperativo,
} from './api'

export const clavesHistorial = {
  todo: (personaId: string) => ['persona-historial', personaId] as const,
  companias: (personaId: string) => [...clavesHistorial.todo(personaId), 'companias'] as const,
  contextos: (personaId: string) => [...clavesHistorial.todo(personaId), 'contextos'] as const,
  unidades: (personaId: string, contextoId: string) =>
    [...clavesHistorial.todo(personaId), 'unidades', contextoId] as const,
}

export function useHistorialCompanias(personaId: string): UseQueryResult<AsignacionCompania[]> {
  return useQuery({
    queryKey: clavesHistorial.companias(personaId),
    queryFn: () => listarHistorialCompanias(personaId),
  })
}

export function useContextos(personaId: string): UseQueryResult<ContextoOperativo[]> {
  return useQuery({
    queryKey: clavesHistorial.contextos(personaId),
    queryFn: () => listarContextos(personaId),
  })
}

export function useUnidadesDelContexto(
  personaId: string,
  contextoId: string | null,
): UseQueryResult<AsignacionUnidadOrganizativa[]> {
  return useQuery({
    queryKey: clavesHistorial.unidades(personaId, contextoId ?? ''),
    queryFn: () => listarUnidadesDelContexto(personaId, contextoId!),
    enabled: contextoId !== null,
  })
}

/**
 * Invalida todo el histórico de la persona.
 *
 * Cualquier escritura sobre la pertenencia puede revocar en cascada sus contextos, unidades y
 * credenciales (RF-061): refrescar solo la lista tocada dejaría el resto mostrando datos que el
 * servidor ya cambió.
 */
function useInvalidarHistorial(personaId: string): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesHistorial.todo(personaId) })
}

export function useCrearPertenencia(personaId: string) {
  const invalidar = useInvalidarHistorial(personaId)

  return useMutation({
    mutationFn: (entrada: { companiaId: string; fechaHoraInicio: string; fechaHoraFin: string }) =>
      crearPertenencia(personaId, entrada),
    onSuccess: invalidar,
  })
}

export function useFinalizarPertenencia(personaId: string) {
  const invalidar = useInvalidarHistorial(personaId)

  return useMutation({
    mutationFn: (entrada: { asignacionId: string; fechaHoraFin: string }) =>
      finalizarPertenencia(personaId, entrada.asignacionId, entrada.fechaHoraFin),
    onSuccess: invalidar,
  })
}

export function useRenovarPertenencia(personaId: string) {
  const invalidar = useInvalidarHistorial(personaId)

  return useMutation({
    mutationFn: (entrada: { asignacionId: string; fechaHoraFin: string }) =>
      renovarPertenencia(personaId, entrada.asignacionId, entrada.fechaHoraFin),
    onSuccess: invalidar,
  })
}

export function useAbrirContexto(personaId: string) {
  const invalidar = useInvalidarHistorial(personaId)

  return useMutation({
    mutationFn: (entrada: {
      companiaPrincipalId: string
      fechaHoraInicio: string
      fechaHoraFin: string
    }) => abrirContexto(personaId, entrada),
    onSuccess: invalidar,
  })
}

export function useAsignarUnidad(personaId: string) {
  const invalidar = useInvalidarHistorial(personaId)

  return useMutation({
    mutationFn: (entrada: {
      contextoId: string
      unidadOrganizativaId: string
      fechaHoraInicio: string
      fechaHoraFin: string
    }) =>
      asignarUnidad(personaId, entrada.contextoId, {
        unidadOrganizativaId: entrada.unidadOrganizativaId,
        fechaHoraInicio: entrada.fechaHoraInicio,
        fechaHoraFin: entrada.fechaHoraFin,
      }),
    onSuccess: invalidar,
  })
}
