import { apiClient } from '../../lib/apiClient'

/** contracts/people.yaml — Persona. */
export interface Persona {
  id: string
  nombres: string
  apellidos: string
  /** Fecha civil sin hora (`YYYY-MM-DD`): no arrastra componente horario. */
  fechaNacimiento: string
  tipoDocumentoId: string
  numeroDocumento: string
  generoId: string
  correoElectronico: string
  tipoSangreId: string
  contactoEmergencia: string
  numeroEmergencia: string
}

/** contracts/people.yaml — PersonaRequest. No acepta `id`: es inmutable (RF-013). */
export type PersonaRequest = Omit<Persona, 'id'>

/** contracts/people.yaml — PaginaPersonas. */
export interface PaginaPersonas {
  items: Persona[]
  total: number
  pagina: number
  tamañoPagina: number
}

export interface FiltroPersonas {
  texto?: string
  tipoDocumentoId?: string
  companiaId?: string
  pagina?: number
  tamañoPagina?: number
}

export async function buscarPersonas(filtro: FiltroPersonas): Promise<PaginaPersonas> {
  const { data } = await apiClient.get<PaginaPersonas>('/api/personas', {
    params: {
      texto: filtro.texto || undefined,
      tipoDocumentoId: filtro.tipoDocumentoId || undefined,
      companiaId: filtro.companiaId || undefined,
      pagina: filtro.pagina,
      tamañoPagina: filtro.tamañoPagina,
    },
  })
  return data
}

export async function crearPersona(entrada: PersonaRequest): Promise<Persona> {
  const { data } = await apiClient.post<Persona>('/api/personas', entrada)
  return data
}

export async function actualizarPersona(id: string, entrada: PersonaRequest): Promise<Persona> {
  const { data } = await apiClient.put<Persona>(`/api/personas/${id}`, entrada)
  return data
}
