import { useState, type ReactElement } from 'react'
import { Dialogo } from '../../components/Dialogo'
import { useSesion } from '../auth/useSesion'
import {
  aInstanteUtc,
  CamposAsignacion,
  rolesAsignables,
  validarAsignacion,
  type BorradorAsignacion,
} from './CamposAsignacion'
import { useAsignarRol } from './hooks'
import { describirError } from './mensajesRol'
import type { Usuario } from './api'

/**
 * Asignar un rol a un usuario existente (UX-19, ux-ui.md §35).
 *
 * Mismo contenido que los pasos 2 a 4 del alta. La interfaz dice explícitamente que **agrega** una
 * asignación en lugar de reemplazar las existentes, porque mientras varias estén vigentes el efecto
 * sobre el alcance es acumulativo (RF-077) y lo contrario sería una pérdida silenciosa de permisos.
 */
export function AsignarRolDialogo({
  usuario,
  alCerrar,
}: {
  usuario: Usuario
  alCerrar: () => void
}): ReactElement {
  const { sesion } = useSesion()
  const asignar = useAsignarRol()

  const rolPropio = sesion?.rol ?? null
  const companiasPropias = sesion?.companiaIds ?? []

  const [valores, setValores] = useState<BorradorAsignacion>({
    rol: rolesAsignables(rolPropio)[0],
    companiaId: companiasPropias.length === 1 ? companiasPropias[0] : '',
    fechaHoraInicio: '',
    fechaHoraFin: '',
  })

  const [enviado, setEnviado] = useState(false)

  const errores = validarAsignacion(valores)
  const error = describirError(asignar.error)

  return (
    <Dialogo titulo={`Asignar rol a ${usuario.correo}`} alCerrar={alCerrar}>
      <p className="aviso info">
        Esta operación <strong>agrega</strong> una asignación nueva. Las que ya estén vigentes se
        conservan y el alcance del usuario será la suma de todas.
      </p>

      {error && (
        <p className="aviso error" role="alert">
          {error.mensaje}
        </p>
      )}

      <CamposAsignacion
        prefijo="asignar"
        valores={valores}
        alCambiar={setValores}
        rolPropio={rolPropio}
        companiasPropias={companiasPropias}
        errores={enviado ? errores : {}}
      />

      <div className="dialogo-acciones">
        <button type="button" onClick={alCerrar}>
          Cancelar
        </button>

        <button
          type="button"
          className="primario"
          disabled={asignar.isPending}
          onClick={() => {
            setEnviado(true)

            if (Object.keys(errores).length > 0) {
              return
            }

            asignar.mutate(
              {
                id: usuario.id,
                rol: valores.rol,
                companiaId: valores.rol === 'GLOBAL_ADMINISTRATOR' ? null : valores.companiaId,
                fechaHoraInicio: aInstanteUtc(valores.fechaHoraInicio),
                fechaHoraFin: aInstanteUtc(valores.fechaHoraFin),
              },
              { onSuccess: alCerrar },
            )
          }}
        >
          {asignar.isPending ? 'Asignando…' : 'Agregar asignación'}
        </button>
      </div>
    </Dialogo>
  )
}
