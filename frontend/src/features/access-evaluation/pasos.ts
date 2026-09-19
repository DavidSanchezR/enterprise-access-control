import type { EvaluarAccesoResponse, MotivoDenegacion } from './api'

export type EstadoPaso = 'CUMPLIDO' | 'FALLIDO' | 'NO_EVALUADO'

export interface Paso {
  clave: string
  titulo: string
  estado: EstadoPaso
}

/**
 * Validaciones visibles en el orden del algoritmo de 14 pasos (research.md §7).
 *
 * Varios pasos del motor se agrupan en una sola línea cuando comparten motivo de denegación: el
 * contrato no los distingue en la respuesta, y separarlos en pantalla obligaría a adivinar cuál de
 * ellos falló.
 */
const PASOS: { clave: string; titulo: string; motivos: MotivoDenegacion[] }[] = [
  {
    clave: 'identificacion',
    titulo: 'Persona y área identificadas, dentro de su alcance',
    motivos: ['PERSONA_NO_ENCONTRADA', 'AREA_NO_ENCONTRADA', 'FUERA_DE_ALCANCE_USUARIO'],
  },
  {
    clave: 'contexto',
    titulo: 'Contexto operativo vigente y legítimo con la compañía principal',
    motivos: ['SIN_CONTEXTO_OPERATIVO_VIGENTE', 'RELACION_CONTRATISTA_PRINCIPAL_VENCIDA'],
  },
  {
    clave: 'credencial',
    titulo: 'Credencial vigente para esa compañía principal',
    motivos: ['SIN_CREDENCIAL_VIGENTE'],
  },
  { clave: 'area', titulo: 'Área activa', motivos: ['AREA_INACTIVA'] },
  {
    clave: 'perfil',
    titulo: 'Perfil de la persona autorizado en el área',
    motivos: ['PERFIL_NO_AUTORIZADO_EN_AREA'],
  },
  {
    clave: 'permiso',
    titulo: 'Permiso aplicable a la persona, su unidad o su compañía',
    motivos: ['SIN_PERMISO_APLICABLE'],
  },
  {
    clave: 'vigencia',
    titulo: 'Permiso vigente en la fecha evaluada',
    motivos: ['PERMISO_FUERA_DE_VIGENCIA'],
  },
  { clave: 'horario', titulo: 'Dentro del bloque horario', motivos: ['FUERA_DE_BLOQUE_HORARIO'] },
]

/**
 * Deriva el estado de cada validación a partir del resultado.
 *
 * El motor se detiene en el primer paso que falla (denegación por defecto), de modo que todo lo
 * anterior se cumplió y lo posterior no llegó a evaluarse. No se infiere nada más allá de eso.
 */
export function pasosDeEvaluacion(resultado: EvaluarAccesoResponse): Paso[] {
  if (resultado.resultado === 'CONCEDIDO') {
    return PASOS.map(({ clave, titulo }) => ({ clave, titulo, estado: 'CUMPLIDO' }))
  }

  const fallido = PASOS.findIndex((paso) =>
    resultado.motivoDenegacion ? paso.motivos.includes(resultado.motivoDenegacion) : false,
  )

  return PASOS.map(({ clave, titulo }, indice) => ({
    clave,
    titulo,
    estado:
      fallido === -1
        ? 'NO_EVALUADO'
        : indice < fallido
          ? 'CUMPLIDO'
          : indice === fallido
            ? 'FALLIDO'
            : 'NO_EVALUADO',
  }))
}

/** Explicación legible de cada motivo, fiel a lo que el contrato dice que cubre. */
export const EXPLICACION_MOTIVO: Record<MotivoDenegacion, string> = {
  PERSONA_NO_ENCONTRADA: 'La persona no existe o está fuera de su alcance.',
  AREA_NO_ENCONTRADA: 'El área no existe o está fuera de su alcance.',
  FUERA_DE_ALCANCE_USUARIO: 'La compañía principal del área no está en su alcance.',
  SIN_CONTEXTO_OPERATIVO_VIGENTE:
    'La persona no tiene un contexto operativo vigente con la compañía principal del área, o ya no es legítimo porque su compañía de pertenencia cambió.',
  RELACION_CONTRATISTA_PRINCIPAL_VENCIDA:
    'La persona es de una contratista cuya relación con la compañía principal no está vigente en la fecha evaluada.',
  SIN_CREDENCIAL_VIGENTE:
    'No hay una credencial vigente para esta compañía principal: nunca se asignó, fue devuelta, dada de baja o revocada, o su vigencia ya terminó.',
  AREA_INACTIVA: 'El área está inactiva.',
  PERFIL_NO_AUTORIZADO_EN_AREA:
    'Ninguno de los perfiles vigentes de la persona está autorizado en el área.',
  SIN_PERMISO_APLICABLE:
    'No existe un permiso del área para la persona, su unidad organizativa ni su compañía.',
  PERMISO_FUERA_DE_VIGENCIA:
    'Los permisos aplicables no están vigentes en la fecha evaluada o fueron dados de baja.',
  FUERA_DE_BLOQUE_HORARIO:
    'La fecha y hora evaluadas caen fuera de los bloques horarios de los permisos aplicables.',
}
