import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '../lib/apiClient'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (contador, error) => {
        // Un 4xx es una decisión del servidor (validación, alcance, conflicto): reintentarlo no
        // cambia el resultado y solo retrasa el mensaje al usuario.
        if (error instanceof ApiError && error.status && error.status < 500) {
          return false
        }
        return contador < 2
      },
    },
    mutations: {
      retry: false,
    },
  },
})
