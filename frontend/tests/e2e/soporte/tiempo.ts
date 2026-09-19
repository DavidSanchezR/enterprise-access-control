/**
 * Instantes para los escenarios de quickstart.md con bloques horarios en hora local empresarial
 * (America/Lima, RF-022, research.md §5).
 *
 * Todo se calcula a partir de un único instante de referencia que la prueba fija al empezar y envía como
 * `fechaHora` de la evaluación. Si se usara "ahora" en cada paso, un cambio de hora entre la creación del
 * bloque y la evaluación convertiría en fallo un artefacto del reloj de la ejecución.
 */

const DIAS = {
  Monday: 'LUNES',
  Tuesday: 'MARTES',
  Wednesday: 'MIERCOLES',
  Thursday: 'JUEVES',
  Friday: 'VIERNES',
  Saturday: 'SABADO',
  Sunday: 'DOMINGO',
} as const

type DiaSemana = (typeof DIAS)[keyof typeof DIAS]

const HORA_MS = 3_600_000

function horaEnLima(instante: Date): { dia: DiaSemana; hora: number; minuto: number } {
  const partes = new Intl.DateTimeFormat('en-US', {
    timeZone: 'America/Lima',
    weekday: 'long',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(instante)

  const valor = (tipo: string): string => partes.find((p) => p.type === tipo)!.value

  return {
    dia: DIAS[valor('weekday') as keyof typeof DIAS],
    hora: Number(valor('hour')),
    minuto: Number(valor('minute')),
  }
}

const dosDigitos = (n: number): string => n.toString().padStart(2, '0')

export interface Reloj {
  /** Instante evaluado en los pasos que esperan CONCEDIDO. */
  referencia: Date
  /** Inicio común de toda vigencia del escenario: cubre también {@link Reloj.fueraDelBloque}. */
  inicio: string
  /** Fin de la pertenencia; las asociaciones dependientes lo reutilizan (contención, RF-072). */
  fin: string
  /** Bloque del día de la referencia en Lima que la cubre: [hh:00, hh+1:00). */
  bloque: { diaSemana: DiaSemana; horaInicio: string; horaFin: string }
  /** Instante del mismo día en Lima fuera del bloque y dentro de todas las demás vigencias. */
  fueraDelBloque: Date
}

/**
 * Fija la referencia del escenario.
 *
 * Espera a salir de los dos últimos minutos del día en Lima: un bloque se expresa en HH:mm con fin
 * exclusivo, así que 23:59 no puede cubrirse.
 */
export async function fijarReloj(): Promise<Reloj> {
  while (horaEnLima(new Date()).hora === 23 && horaEnLima(new Date()).minuto >= 58) {
    await new Promise((resolver) => setTimeout(resolver, 15_000))
  }

  const referencia = new Date()
  const { dia, hora } = horaEnLima(referencia)

  // Dos horas después, o antes si ya es tarde: sigue siendo el mismo día en Lima y queda fuera de
  // [hh:00, hh+1:00). Las vigencias empiezan tres horas antes para cubrirlo en ambos sentidos, de modo
  // que el único motivo de denegación posible sea el horario.
  const fueraDelBloque = new Date(referencia.getTime() + (hora <= 21 ? 2 : -2) * HORA_MS)

  return {
    referencia,
    inicio: new Date(referencia.getTime() - 3 * HORA_MS).toISOString(),
    fin: new Date(referencia.getTime() + 365 * 24 * HORA_MS).toISOString(),
    bloque: {
      diaSemana: dia,
      horaInicio: `${dosDigitos(hora)}:00`,
      horaFin: hora < 23 ? `${dosDigitos(hora + 1)}:00` : '23:59',
    },
    fueraDelBloque,
  }
}
