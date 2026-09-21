import { ApiError } from '../../lib/apiClient'

/**
 * Traduce los códigos de negocio de la administración de usuarios a mensajes orientados a la acción
 * (ux-ui.md §35, tabla de validaciones y errores; UX-21).
 *
 * Ningún mensaje revela datos de compañías o usuarios fuera del alcance: al operar sobre un recurso
 * ajeno la interfaz se comporta como si no existiera (RF-077). Por eso `RECURSO_NO_ENCONTRADO` habla
 * de "no pertenece a su alcance" sin nombrar nada, y `ROL_NO_AUTORIZADO` explica la limitación del
 * rol propio en lugar de describir el recurso destino.
 */
const MENSAJES: Record<string, string> = {
  ROL_NO_AUTORIZADO:
    'Su rol no autoriza esa asignación. Un administrador de compañía solo puede asignar el rol de administrador de compañía dentro de la suya.',
  ROL_COMPANIA_INCONSISTENTE:
    'La combinación de rol y compañía no es válida: el alcance global no admite compañía, y el alcance por compañía exige una.',
  RECURSO_NO_ENCONTRADO: 'Ese usuario no pertenece a su alcance. Vuelva al listado para continuar.',
  CORREO_YA_REGISTRADO:
    'Ese correo ya está registrado. Búsquelo en el listado en lugar de crear un usuario nuevo.',
  SOLAPAMIENTO_VIGENCIA:
    'Ya existe una asignación vigente para ese usuario y compañía en el período indicado. Finalícela antes de crear otra o ajuste las fechas.',
  PERIODO_INVALIDO: 'La fecha de fin debe ser posterior a la de inicio.',
  RENOVACION_NO_POSTERIOR: 'La nueva fecha de fin debe ser posterior a la vigente.',
  ASIGNACION_ROL_NO_VIGENTE: 'Esa asignación ya no está vigente, así que no admite esta operación.',
  PASSWORD_NO_CUMPLE_POLITICA:
    'La contraseña no cumple la política vigente. Revise los requisitos indicados en el campo.',
  VALOR_MAESTRO_INACTIVO: 'La compañía indicada no existe o está inactiva.',
}

/**
 * Campo del formulario al que pertenece cada error, para poder señalarlo en vez de mostrar solo un
 * aviso general (ux-ui.md §35).
 */
const CAMPOS: Record<string, string> = {
  CORREO_YA_REGISTRADO: 'correo',
  PASSWORD_NO_CUMPLE_POLITICA: 'passwordInicial',
  PERIODO_INVALIDO: 'fechaHoraFin',
  RENOVACION_NO_POSTERIOR: 'fechaHoraFin',
  ROL_COMPANIA_INCONSISTENTE: 'companiaId',
  VALOR_MAESTRO_INACTIVO: 'companiaId',
}

export interface ErrorDeAsignacion {
  mensaje: string
  /** Nombre del campo a señalar, si el error corresponde a uno concreto. */
  campo?: string
}

export function describirError(error: unknown): ErrorDeAsignacion | undefined {
  if (!(error instanceof ApiError)) {
    return undefined
  }

  const codigo = error.codigo

  // Sin código conocido se prefiere el detalle del servidor a un texto genérico: sigue siendo más
  // informativo que "ha ocurrido un error".
  return {
    mensaje: (codigo && MENSAJES[codigo]) ?? error.message,
    campo: codigo ? CAMPOS[codigo] : undefined,
  }
}
