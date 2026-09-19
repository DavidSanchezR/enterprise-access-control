import { apiClient } from '../../lib/apiClient'
import type { AlcancePermiso } from '../permissions/api'

/** contracts/access-evaluation.yaml — ResultadoEvaluacion. */
export type ResultadoEvaluacion = 'CONCEDIDO' | 'DENEGADO'

/** contracts/access-evaluation.yaml — MotivoDenegacion (11 valores). */
export const MOTIVOS_DENEGACION = [
  'PERSONA_NO_ENCONTRADA',
  'AREA_NO_ENCONTRADA',
  'FUERA_DE_ALCANCE_USUARIO',
  'SIN_CONTEXTO_OPERATIVO_VIGENTE',
  'RELACION_CONTRATISTA_PRINCIPAL_VENCIDA',
  'SIN_CREDENCIAL_VIGENTE',
  'AREA_INACTIVA',
  'PERFIL_NO_AUTORIZADO_EN_AREA',
  'SIN_PERMISO_APLICABLE',
  'PERMISO_FUERA_DE_VIGENCIA',
  'FUERA_DE_BLOQUE_HORARIO',
] as const
export type MotivoDenegacion = (typeof MOTIVOS_DENEGACION)[number]

/** contracts/access-evaluation.yaml — EvaluarAccesoRequest. */
export interface EvaluarAccesoRequest {
  personaId: string
  areaAccesoId: string
  /** Instante UTC; el servidor lo convierte a America/Lima para los bloques horarios. */
  fechaHora: string
}

/** contracts/access-evaluation.yaml — EvaluarAccesoResponse. */
export interface EvaluarAccesoResponse {
  resultado: ResultadoEvaluacion
  motivoDenegacion: MotivoDenegacion | null
  companiaPrincipalId: string | null
  contextoOperativoId: string | null
  permisoAplicadoId: string | null
  nivelAplicado: AlcancePermiso | null
  evaluadoEnZonaHoraria: string
}

export async function evaluarAcceso(entrada: EvaluarAccesoRequest): Promise<EvaluarAccesoResponse> {
  const { data } = await apiClient.post<EvaluarAccesoResponse>('/api/evaluacion-acceso', entrada)
  return data
}
