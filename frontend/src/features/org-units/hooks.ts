import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import type { Estado } from '../companies/api'
import {
  actualizarUnidad,
  crearUnidad,
  listarUnidades,
  moverUnidad,
  obtenerArbol,
  type NodoArbolUnidad,
  type UnidadOrganizativa,
} from './api'

/**
 * Las claves incluyen siempre la Compañía Principal: sus árboles están aislados (RF-043) y mezclar
 * su caché mostraría nodos de una principal bajo otra.
 */
export const clavesUnidades = {
  todo: ['unidades-organizativas'] as const,
  arbol: (companiaPrincipalId: string) =>
    [...clavesUnidades.todo, 'arbol', companiaPrincipalId] as const,
  lista: (companiaPrincipalId: string) =>
    [...clavesUnidades.todo, 'lista', companiaPrincipalId] as const,
}

export function useArbolUnidades(
  companiaPrincipalId: string | null,
): UseQueryResult<NodoArbolUnidad[]> {
  return useQuery({
    queryKey: clavesUnidades.arbol(companiaPrincipalId ?? ''),
    queryFn: () => obtenerArbol(companiaPrincipalId!),
    enabled: companiaPrincipalId !== null && companiaPrincipalId !== '',
  })
}

export function useUnidades(
  companiaPrincipalId: string | null,
): UseQueryResult<UnidadOrganizativa[]> {
  return useQuery({
    queryKey: clavesUnidades.lista(companiaPrincipalId ?? ''),
    queryFn: () => listarUnidades(companiaPrincipalId!),
    enabled: companiaPrincipalId !== null && companiaPrincipalId !== '',
  })
}

function useInvalidarUnidades(): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesUnidades.todo })
}

export function useCrearUnidad() {
  const invalidar = useInvalidarUnidades()
  return useMutation({ mutationFn: crearUnidad, onSuccess: invalidar })
}

export function useActualizarUnidad() {
  const invalidar = useInvalidarUnidades()

  return useMutation({
    mutationFn: (entrada: { id: string; nombre: string; estado: Estado }) =>
      actualizarUnidad(entrada.id, { nombre: entrada.nombre, estado: entrada.estado }),
    onSuccess: invalidar,
  })
}

export function useMoverUnidad() {
  const invalidar = useInvalidarUnidades()

  return useMutation({
    mutationFn: (entrada: { id: string; nuevoPadreId: string | null }) =>
      moverUnidad(entrada.id, entrada.nuevoPadreId),
    onSuccess: invalidar,
  })
}
