/**
 * ProblemDetails RFC 7807/9457 — único formato de error de la API (research.md §21).
 */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  /**
   * Extensión propia: código de error de negocio estable y legible por máquina. El frontend debe
   * ramificar por este valor y nunca por el texto de `detail`, que es para humanos y puede cambiar
   * de redacción sin aviso.
   */
  codigo?: string
  /** Presente solo en ValidationProblemDetails (errores por campo). */
  errors?: Record<string, string[]>
}

/** Códigos de negocio que el frontend distingue explícitamente (contracts/*.yaml). */
export const CodigosError = {
  SOLAPAMIENTO_VIGENCIA: 'SOLAPAMIENTO_VIGENCIA',
  PERIODO_INVALIDO: 'PERIODO_INVALIDO',
  FUERA_DE_CONTENCION_TEMPORAL: 'FUERA_DE_CONTENCION_TEMPORAL',
  /** RF-072/RF-082: la persona no tiene pertenencia vigente que sirva de límite temporal. */
  SIN_PERTENENCIA_VIGENTE: 'SIN_PERTENENCIA_VIGENTE',
  FECHA_FIN_OBLIGATORIA: 'FECHA_FIN_OBLIGATORIA',
  RENOVACION_NO_POSTERIOR: 'RENOVACION_NO_POSTERIOR',
  PERTENENCIA_NO_RENOVABLE: 'PERTENENCIA_NO_RENOVABLE',
  SIN_RELACION_CONTRATISTA_PRINCIPAL_VIGENTE: 'SIN_RELACION_CONTRATISTA_PRINCIPAL_VIGENTE',
  CICLO_JERARQUICO: 'CICLO_JERARQUICO',
  CONFLICTO_CONCURRENCIA: 'CONFLICTO_CONCURRENCIA',
} as const

export function esProblemDetails(valor: unknown): valor is ProblemDetails {
  return (
    typeof valor === 'object' &&
    valor !== null &&
    ('title' in valor || 'status' in valor || 'codigo' in valor)
  )
}

/**
 * Mensaje presentable al usuario. Se prefiere `detail` (texto pensado para humanos) y se cae a
 * `title`; nunca se muestra un volcado técnico.
 */
export function mensajeDeProblema(problema: ProblemDetails | undefined): string {
  if (!problema) {
    return 'No se pudo completar la operación.'
  }
  return problema.detail ?? problema.title ?? 'No se pudo completar la operación.'
}
