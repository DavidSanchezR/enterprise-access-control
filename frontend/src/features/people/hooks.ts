import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query'
import {
  actualizarPersona,
  buscarPersonas,
  crearPersona,
  type FiltroPersonas,
  type PaginaPersonas,
  type PersonaRequest,
} from './api'

export const clavesPersonas = {
  todo: ['personas'] as const,
  lista: (filtro: FiltroPersonas) => [...clavesPersonas.todo, 'lista', filtro] as const,
}

export function usePersonas(filtro: FiltroPersonas): UseQueryResult<PaginaPersonas> {
  return useQuery({
    queryKey: clavesPersonas.lista(filtro),
    queryFn: () => buscarPersonas(filtro),
    placeholderData: (anterior) => anterior,
  })
}

function useInvalidarPersonas(): () => Promise<void> {
  const cliente = useQueryClient()
  return () => cliente.invalidateQueries({ queryKey: clavesPersonas.todo })
}

export function useCrearPersona() {
  const invalidar = useInvalidarPersonas()
  return useMutation({ mutationFn: crearPersona, onSuccess: invalidar })
}

export function useActualizarPersona() {
  const invalidar = useInvalidarPersonas()

  return useMutation({
    mutationFn: (entrada: { id: string } & PersonaRequest) => {
      const { id, ...resto } = entrada
      return actualizarPersona(id, resto)
    },
    onSuccess: invalidar,
  })
}
