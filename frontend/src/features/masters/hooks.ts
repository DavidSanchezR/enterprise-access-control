import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import type { Estado } from '../companies/api'
import {
  actualizarMaestro,
  crearMaestro,
  listarMaestro,
  type MasterItem,
  type MasterItemRequest,
  type RutaCatalogo,
} from './api'

export const clavesMaestros = {
  todo: ['maestros'] as const,
  catalogo: (ruta: RutaCatalogo) => [...clavesMaestros.todo, ruta] as const,
  lista: (ruta: RutaCatalogo, estado?: Estado) =>
    [...clavesMaestros.catalogo(ruta), estado ?? 'todos'] as const,
}

export function useMaestro(ruta: RutaCatalogo, estado?: Estado): UseQueryResult<MasterItem[]> {
  return useQuery({
    queryKey: clavesMaestros.lista(ruta, estado),
    queryFn: () => listarMaestro(ruta, estado),
    placeholderData: (anterior) => anterior,
  })
}

/** Invalida solo el catálogo tocado: los cinco son independientes entre sí. */
function useInvalidarCatalogo(ruta: RutaCatalogo): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesMaestros.catalogo(ruta) })
}

export function useCrearMaestro(ruta: RutaCatalogo) {
  const invalidar = useInvalidarCatalogo(ruta)

  return useMutation({
    mutationFn: (entrada: MasterItemRequest) => crearMaestro(ruta, entrada),
    onSuccess: invalidar,
  })
}

export function useActualizarMaestro(ruta: RutaCatalogo) {
  const invalidar = useInvalidarCatalogo(ruta)

  return useMutation({
    mutationFn: (entrada: { id: string } & MasterItemRequest) =>
      actualizarMaestro(ruta, entrada.id, { nombre: entrada.nombre, estado: entrada.estado }),
    onSuccess: invalidar,
  })
}
