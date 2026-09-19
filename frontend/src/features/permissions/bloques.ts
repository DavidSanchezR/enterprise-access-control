import { DIAS_SEMANA, ETIQUETA_DIA, type BloqueHorario, type DiaSemana } from './api'

/** Formato del contrato: HH:mm de 00:00 a 23:59 (contracts/permissions.yaml). */
const PATRON_HORA = /^([01]\d|2[0-3]):[0-5]\d$/

/**
 * Reglas de RF-022 y RF-039 sobre un conjunto de bloques, evaluadas antes de enviar.
 *
 * Reproduce las del servidor —que sigue siendo quien decide— para que el error aparezca junto al
 * bloque que lo causa y no después de un viaje de ida y vuelta. Devuelve un mensaje por bloque
 * (mismo índice) y un mensaje general; `null` significa que no hay error.
 */
export function validarBloques(bloques: Omit<BloqueHorario, 'id'>[]): {
  porBloque: (string | null)[]
  general: string | null
} {
  const porBloque = bloques.map((bloque, indice): string | null => {
    if (!PATRON_HORA.test(bloque.horaInicio) || !PATRON_HORA.test(bloque.horaFin)) {
      return 'Indique ambas horas en formato HH:mm.'
    }

    // HH:mm con ceros a la izquierda se compara correctamente como texto.
    if (bloque.horaFin <= bloque.horaInicio) {
      return 'La hora de fin debe ser posterior a la de inicio.'
    }

    // Fin exclusivo: 08:00–12:00 y 12:00–17:00 son consecutivos, no solapados.
    const choca = bloques.some(
      (otro, j) =>
        j !== indice &&
        otro.diaSemana === bloque.diaSemana &&
        PATRON_HORA.test(otro.horaInicio) &&
        PATRON_HORA.test(otro.horaFin) &&
        otro.horaInicio < bloque.horaFin &&
        bloque.horaInicio < otro.horaFin,
    )

    return choca
      ? `Se solapa con otro bloque del ${ETIQUETA_DIA[bloque.diaSemana].toLowerCase()}.`
      : null
  })

  return {
    porBloque,
    general: bloques.length === 0 ? 'El permiso requiere al menos un bloque horario.' : null,
  }
}

export function hayErroresEnBloques(bloques: Omit<BloqueHorario, 'id'>[]): boolean {
  const { porBloque, general } = validarBloques(bloques)
  return general !== null || porBloque.some((mensaje) => mensaje !== null)
}

/** Agrupa los bloques por día conservando el orden de la semana y su índice original. */
export function bloquesPorDia(
  bloques: Omit<BloqueHorario, 'id'>[],
): { dia: DiaSemana; bloques: { bloque: Omit<BloqueHorario, 'id'>; indice: number }[] }[] {
  return DIAS_SEMANA.map((dia) => ({
    dia,
    bloques: bloques
      .map((bloque, indice) => ({ bloque, indice }))
      .filter(({ bloque }) => bloque.diaSemana === dia),
  }))
}
