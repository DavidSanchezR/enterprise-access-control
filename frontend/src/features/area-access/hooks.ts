import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import type { Estado } from '../companies/api'
import {
  actualizarArea,
  crearArea,
  listarAreas,
  moverArea,
  obtenerArbolAreas,
  type AreaAcceso,
  type NodoArbolArea,
} from './api'

/**
 * Las claves incluyen siempre la Compañía Principal: sus árboles de áreas están aislados (RF-043) y
 * mezclar su caché mostraría áreas de una principal bajo otra.
 */
export const clavesAreas = {
  todo: ['areas-acceso'] as const,
  arbol: (companiaPrincipalId: string) =>
    [...clavesAreas.todo, 'arbol', companiaPrincipalId] as const,
  lista: (companiaPrincipalId: string) =>
    [...clavesAreas.todo, 'lista', companiaPrincipalId] as const,
}

export function useArbolAreas(companiaPrincipalId: string | null): UseQueryResult<NodoArbolArea[]> {
  return useQuery({
    queryKey: clavesAreas.arbol(companiaPrincipalId ?? ''),
    queryFn: () => obtenerArbolAreas(companiaPrincipalId!),
    enabled: companiaPrincipalId !== null && companiaPrincipalId !== '',
  })
}

export function useAreas(companiaPrincipalId: string | null): UseQueryResult<AreaAcceso[]> {
  return useQuery({
    queryKey: clavesAreas.lista(companiaPrincipalId ?? ''),
    queryFn: () => listarAreas(companiaPrincipalId!),
    enabled: companiaPrincipalId !== null && companiaPrincipalId !== '',
  })
}

function useInvalidarAreas(): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesAreas.todo })
}

export function useCrearArea() {
  const invalidar = useInvalidarAreas()
  return useMutation({ mutationFn: crearArea, onSuccess: invalidar })
}

export function useActualizarArea() {
  const invalidar = useInvalidarAreas()

  return useMutation({
    mutationFn: (entrada: { id: string; nombre: string; estado: Estado }) =>
      actualizarArea(entrada.id, { nombre: entrada.nombre, estado: entrada.estado }),
    onSuccess: invalidar,
  })
}

export function useMoverArea() {
  const invalidar = useInvalidarAreas()

  return useMutation({
    mutationFn: (entrada: { id: string; nuevoPadreId: string | null }) =>
      moverArea(entrada.id, entrada.nuevoPadreId),
    onSuccess: invalidar,
  })
}
