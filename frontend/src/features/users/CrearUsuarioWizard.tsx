import { useState, type ReactElement } from 'react'
import { Dialogo } from '../../components/Dialogo'
import { useSesion } from '../auth/useSesion'
import {
  aInstanteUtc,
  CamposAsignacion,
  etiquetaRol,
  rolesAsignables,
  validarAsignacion,
  type BorradorAsignacion,
} from './CamposAsignacion'
import { useCrearUsuario } from './hooks'
import { describirError } from './mensajesRol'

/** Los cinco pasos de ux-ui.md §35 "Crear usuario". */
const PASOS = ['Identidad', 'Rol', 'Compañía', 'Vigencia', 'Confirmación'] as const

/**
 * Alta de usuario en cinco pasos (UX-17, ux-ui.md §35).
 *
 * Es un wizard y no un formulario plano porque un usuario **nace siempre con su primera asignación**
 * (RF-074): rol, compañía y vigencia no son datos opcionales que se añadan después, son parte del
 * alta. Los pasos 2 a 4 comparten componente con la asignación a un usuario existente.
 */
export function CrearUsuarioWizard({ alCerrar }: { alCerrar: () => void }): ReactElement {
  const { sesion } = useSesion()
  const crear = useCrearUsuario()

  const rolPropio = sesion?.rol ?? null
  const companiasPropias = sesion?.companiaIds ?? []

  const [paso, setPaso] = useState(0)
  const [correo, setCorreo] = useState('')
  const [passwordInicial, setPasswordInicial] = useState('')

  const [asignacion, setAsignacion] = useState<BorradorAsignacion>({
    // Si el operador solo puede asignar un rol, ya viene elegido: no se ofrece una decisión falsa.
    rol: rolesAsignables(rolPropio)[0],
    companiaId: companiasPropias.length === 1 ? companiasPropias[0] : '',
    fechaHoraInicio: '',
    fechaHoraFin: '',
  })

  const [erroresIdentidad, setErroresIdentidad] = useState<{
    correo?: string
    passwordInicial?: string
  }>({})

  const erroresAsignacion = validarAsignacion(asignacion)
  const error = describirError(crear.error)

  function validarIdentidad(): boolean {
    const errores: { correo?: string; passwordInicial?: string } = {}

    if (correo.trim() === '') {
      errores.correo = 'Indique el correo.'
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo.trim())) {
      errores.correo = 'El correo no tiene un formato válido.'
    }

    if (passwordInicial === '') {
      errores.passwordInicial = 'Indique la contraseña inicial.'
    }

    setErroresIdentidad(errores)
    return Object.keys(errores).length === 0
  }

  /** El paso 3 se omite para el alcance global, que no admite compañía (RF-074). */
  function siguiente(): void {
    if (paso === 0 && !validarIdentidad()) {
      return
    }

    if (paso === 2 && erroresAsignacion.companiaId) {
      return
    }

    if (paso === 3 && (erroresAsignacion.fechaHoraInicio || erroresAsignacion.fechaHoraFin)) {
      return
    }

    setPaso((actual) => Math.min(actual + 1, PASOS.length - 1))
  }

  function enviar(): void {
    if (!validarIdentidad() || Object.keys(erroresAsignacion).length > 0) {
      return
    }

    crear.mutate(
      {
        correo: correo.trim(),
        passwordInicial,
        rol: asignacion.rol,
        companiaId: asignacion.rol === 'GLOBAL_ADMINISTRATOR' ? null : asignacion.companiaId,
        fechaHoraInicio: aInstanteUtc(asignacion.fechaHoraInicio),
        fechaHoraFin: aInstanteUtc(asignacion.fechaHoraFin),
      },
      { onSuccess: alCerrar },
    )
  }

  return (
    <Dialogo titulo="Nuevo usuario" alCerrar={alCerrar}>
      <ol className="wizard-pasos" aria-label="Pasos del alta de usuario">
        {PASOS.map((nombre, indice) => (
          <li
            key={nombre}
            className={indice === paso ? 'actual' : indice < paso ? 'completado' : undefined}
            aria-current={indice === paso ? 'step' : undefined}
          >
            {nombre}
          </li>
        ))}
      </ol>

      {error && (
        <p className="aviso error" role="alert">
          {error.mensaje}
        </p>
      )}

      {paso === 0 && (
        <>
          <div className="campo">
            <label htmlFor="crear-correo">Correo</label>
            <input
              id="crear-correo"
              type="email"
              autoFocus
              value={correo}
              aria-invalid={erroresIdentidad.correo ? 'true' : undefined}
              onChange={(evento) => setCorreo(evento.target.value)}
            />
            <span className="campo-ayuda">Es el identificador con el que iniciará sesión.</span>
            {erroresIdentidad.correo && (
              <span className="error-campo" role="alert">
                {erroresIdentidad.correo}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="crear-password">Contraseña inicial</label>
            <input
              id="crear-password"
              type="password"
              autoComplete="new-password"
              value={passwordInicial}
              aria-invalid={erroresIdentidad.passwordInicial ? 'true' : undefined}
              aria-describedby="crear-password-ayuda"
              onChange={(evento) => setPasswordInicial(evento.target.value)}
            />
            {/* Se anuncia aquí y no como sorpresa posterior (ux-ui.md §35 paso 1). */}
            <span className="campo-ayuda" id="crear-password-ayuda">
              Se rige por la política vigente y el titular deberá cambiarla en su primer ingreso.
            </span>
            {erroresIdentidad.passwordInicial && (
              <span className="error-campo" role="alert">
                {erroresIdentidad.passwordInicial}
              </span>
            )}
          </div>
        </>
      )}

      {paso >= 1 && paso <= 3 && (
        <CamposAsignacion
          prefijo="crear"
          valores={asignacion}
          alCambiar={setAsignacion}
          rolPropio={rolPropio}
          companiasPropias={companiasPropias}
          errores={erroresAsignacion}
        />
      )}

      {paso === 4 && (
        <dl className="resumen-confirmacion">
          <dt>Correo</dt>
          <dd>{correo}</dd>

          <dt>Rol</dt>
          <dd>{etiquetaRol(asignacion.rol)}</dd>

          {asignacion.rol === 'COMPANY_ADMINISTRATOR' && (
            <>
              <dt>Compañía</dt>
              <dd>{asignacion.companiaId}</dd>
            </>
          )}

          <dt>Vigencia</dt>
          <dd>
            {asignacion.fechaHoraInicio} → {asignacion.fechaHoraFin}
          </dd>
        </dl>
      )}

      <div className="dialogo-acciones">
        <button type="button" onClick={alCerrar}>
          Cancelar
        </button>

        {paso > 0 && (
          <button type="button" onClick={() => setPaso((actual) => actual - 1)}>
            Anterior
          </button>
        )}

        {paso < PASOS.length - 1 && (
          <button type="button" className="primario" onClick={siguiente}>
            Siguiente
          </button>
        )}

        {paso === PASOS.length - 1 && (
          <button type="button" className="primario" disabled={crear.isPending} onClick={enviar}>
            {crear.isPending ? 'Creando…' : 'Crear usuario'}
          </button>
        )}
      </div>
    </Dialogo>
  )
}
