import type { ReactElement } from 'react'
import { ETIQUETA_DIA, type BloqueHorario, type DiaSemana } from './api'
import { bloquesPorDia, validarBloques } from './bloques'

type Bloque = Omit<BloqueHorario, 'id'>

/**
 * Selector semanal de bloques horarios de un permiso (RF-022).
 *
 * Una fila por día de la semana, cada una con sus franjas. Las horas son locales de la zona
 * empresarial (America/Lima) y la pantalla lo dice: un bloque de negocio es "lunes de 08:00 a 17:00",
 * no un instante UTC.
 *
 * Es un componente controlado: no guarda estado propio, de modo que el formulario que lo contiene es
 * la única fuente de verdad de lo que se va a enviar.
 */
export function SelectorSemanal({
  bloques,
  onChange,
}: {
  bloques: Bloque[]
  onChange: (bloques: Bloque[]) => void
}): ReactElement {
  const { porBloque, general } = validarBloques(bloques)

  function agregar(dia: DiaSemana): void {
    onChange([...bloques, { diaSemana: dia, horaInicio: '08:00', horaFin: '17:00' }])
  }

  function quitar(indice: number): void {
    onChange(bloques.filter((_, i) => i !== indice))
  }

  function cambiar(indice: number, campo: 'horaInicio' | 'horaFin', valor: string): void {
    onChange(bloques.map((bloque, i) => (i === indice ? { ...bloque, [campo]: valor } : bloque)))
  }

  return (
    <fieldset className="semanal">
      <legend>Bloques horarios</legend>
      <p className="campo-ayuda">
        Horas locales de America/Lima. El fin es exclusivo: un bloque de 08:00 a 12:00 y otro de
        12:00 a 17:00 del mismo día son consecutivos, no se solapan.
      </p>

      {general && (
        <p className="error-campo" role="alert">
          {general}
        </p>
      )}

      <div className="semanal-dias">
        {bloquesPorDia(bloques).map(({ dia, bloques: delDia }) => (
          <div key={dia} className="semanal-dia" role="group" aria-label={ETIQUETA_DIA[dia]}>
            <span className="semanal-nombre">{ETIQUETA_DIA[dia]}</span>

            <div className="semanal-franjas">
              {delDia.length === 0 && <span className="semanal-sin">Sin acceso</span>}

              {delDia.map(({ bloque, indice }) => (
                <div key={indice} className="semanal-franja">
                  <input
                    type="time"
                    step={60}
                    aria-label={`${ETIQUETA_DIA[dia]}: hora de inicio`}
                    aria-invalid={porBloque[indice] ? 'true' : undefined}
                    value={bloque.horaInicio}
                    onChange={(evento) => cambiar(indice, 'horaInicio', evento.target.value)}
                  />
                  <span aria-hidden="true">–</span>
                  <input
                    type="time"
                    step={60}
                    aria-label={`${ETIQUETA_DIA[dia]}: hora de fin`}
                    aria-invalid={porBloque[indice] ? 'true' : undefined}
                    value={bloque.horaFin}
                    onChange={(evento) => cambiar(indice, 'horaFin', evento.target.value)}
                  />
                  <button
                    type="button"
                    className="semanal-quitar"
                    aria-label={`Quitar bloque del ${ETIQUETA_DIA[dia].toLowerCase()} ${bloque.horaInicio}–${bloque.horaFin}`}
                    onClick={() => quitar(indice)}
                  >
                    Quitar
                  </button>
                  {porBloque[indice] && (
                    <span className="error-campo" role="alert">
                      {porBloque[indice]}
                    </span>
                  )}
                </div>
              ))}
            </div>

            <button
              type="button"
              className="semanal-agregar"
              aria-label={`Agregar bloque el ${ETIQUETA_DIA[dia].toLowerCase()}`}
              onClick={() => agregar(dia)}
            >
              + Bloque
            </button>
          </div>
        ))}
      </div>
    </fieldset>
  )
}
