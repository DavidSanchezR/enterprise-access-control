import type { ReactElement } from 'react'
import type { RolAdministrativo } from '../../lib/apiClient'
import { useCompanias } from '../companies/hooks'

/** Valores de una asignación en edición, antes de enviarse. */
export interface BorradorAsignacion {
  rol: RolAdministrativo
  companiaId: string
  fechaHoraInicio: string
  fechaHoraFin: string
}

/**
 * Roles que el operador puede asignar (RF-076, ux-ui.md §35 paso 2).
 *
 * La opción no disponible **no se ofrece**: la interfaz nunca propone una acción que la autorización
 * va a rechazar (§32). Un `COMPANY_ADMINISTRATOR` solo ve su propio nivel.
 */
export function rolesAsignables(rolPropio: RolAdministrativo | null): RolAdministrativo[] {
  return rolPropio === 'GLOBAL_ADMINISTRATOR'
    ? ['GLOBAL_ADMINISTRATOR', 'COMPANY_ADMINISTRATOR']
    : ['COMPANY_ADMINISTRATOR']
}

const ETIQUETA_ROL: Record<RolAdministrativo, string> = {
  GLOBAL_ADMINISTRATOR: 'Administrador global',
  COMPANY_ADMINISTRATOR: 'Administrador de compañía',
}

export function etiquetaRol(rol: RolAdministrativo): string {
  return ETIQUETA_ROL[rol]
}

/**
 * Pasos 2 a 4 de ux-ui.md §35: rol, compañía condicional y vigencia.
 *
 * Se comparten entre el alta de usuario y la asignación a un usuario existente porque la sección los
 * define con el mismo contenido; duplicarlos dejaría dos formularios que podrían divergir.
 */
export function CamposAsignacion({
  prefijo,
  valores,
  alCambiar,
  rolPropio,
  companiasPropias,
  errores,
}: {
  prefijo: string
  valores: BorradorAsignacion
  alCambiar: (valores: BorradorAsignacion) => void
  rolPropio: RolAdministrativo | null
  companiasPropias: string[]
  errores: Partial<Record<keyof BorradorAsignacion, string>>
}): ReactElement {
  const asignables = rolesAsignables(rolPropio)
  const esGlobalPropio = rolPropio === 'GLOBAL_ADMINISTRATOR'

  // El selector solo ofrece compañías del alcance de quien opera (RF-077, §35 "Aislamiento").
  // Un GLOBAL_ADMINISTRATOR no enumera compañías en su sesión, así que las consulta a la API, que ya
  // se las filtra por alcance.
  const consulta = useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })

  const companias = esGlobalPropio
    ? (consulta.data?.items ?? [])
    : (consulta.data?.items ?? []).filter((c) => companiasPropias.includes(c.id))

  const exigeCompania = valores.rol === 'COMPANY_ADMINISTRATOR'

  // Para un COMPANY_ADMINISTRATOR con una sola compañía, viene preseleccionada y no es modificable.
  const companiaFija = !esGlobalPropio && companiasPropias.length === 1

  return (
    <>
      <fieldset className="campo">
        <legend>Rol</legend>

        {asignables.map((rol) => (
          <label key={rol} className="opcion-radio" htmlFor={`${prefijo}-rol-${rol}`}>
            <input
              id={`${prefijo}-rol-${rol}`}
              type="radio"
              name={`${prefijo}-rol`}
              value={rol}
              checked={valores.rol === rol}
              onChange={() =>
                alCambiar({
                  ...valores,
                  rol,
                  // El alcance global no admite compañía (RF-074): se limpia al cambiar de rol para
                  // que no quede un valor invisible que el servidor rechazaría.
                  companiaId: rol === 'GLOBAL_ADMINISTRATOR' ? '' : valores.companiaId,
                })
              }
            />
            {etiquetaRol(rol)}
          </label>
        ))}

        {!esGlobalPropio && (
          <span className="campo-ayuda">
            Su rol solo autoriza a crear administradores de su propia compañía.
          </span>
        )}
      </fieldset>

      {exigeCompania ? (
        <div className="campo">
          <label htmlFor={`${prefijo}-compania`}>Compañía</label>
          <select
            id={`${prefijo}-compania`}
            value={valores.companiaId}
            disabled={companiaFija}
            aria-invalid={errores.companiaId ? 'true' : undefined}
            onChange={(evento) => alCambiar({ ...valores, companiaId: evento.target.value })}
          >
            <option value="">Seleccione una compañía…</option>
            {companias.map((compania) => (
              <option key={compania.id} value={compania.id}>
                {compania.nombre}
              </option>
            ))}
          </select>

          {companiaFija && (
            <span className="campo-ayuda">
              Su alcance cubre una sola compañía, por lo que no es modificable.
            </span>
          )}

          {errores.companiaId && (
            <span className="error-campo" role="alert">
              {errores.companiaId}
            </span>
          )}
        </div>
      ) : (
        <p className="aviso info">
          El alcance global cubre todas las compañías, incluidas las que se creen después. No se
          asocia ninguna compañía a esta asignación.
        </p>
      )}

      <div className="campo">
        <label htmlFor={`${prefijo}-inicio`}>Inicio de vigencia</label>
        <input
          id={`${prefijo}-inicio`}
          type="datetime-local"
          value={valores.fechaHoraInicio}
          aria-invalid={errores.fechaHoraInicio ? 'true' : undefined}
          onChange={(evento) => alCambiar({ ...valores, fechaHoraInicio: evento.target.value })}
        />
        {errores.fechaHoraInicio && (
          <span className="error-campo" role="alert">
            {errores.fechaHoraInicio}
          </span>
        )}
      </div>

      <div className="campo">
        <label htmlFor={`${prefijo}-fin`}>Fin de vigencia</label>
        <input
          id={`${prefijo}-fin`}
          type="datetime-local"
          value={valores.fechaHoraFin}
          aria-invalid={errores.fechaHoraFin ? 'true' : undefined}
          aria-describedby={`${prefijo}-fin-ayuda`}
          onChange={(evento) => alCambiar({ ...valores, fechaHoraFin: evento.target.value })}
        />
        {/* RF-075 no admite vacío ni "sin fin": no se ofrece casilla de indefinido. */}
        <span className="campo-ayuda" id={`${prefijo}-fin-ayuda`}>
          Obligatorio. Una asignación siempre tiene fecha de fin; para extenderla se renueva.
        </span>
        {errores.fechaHoraFin && (
          <span className="error-campo" role="alert">
            {errores.fechaHoraFin}
          </span>
        )}
      </div>
    </>
  )
}

/** Validaciones que se resuelven en el formulario antes de enviar (ux-ui.md §35). */
export function validarAsignacion(
  valores: BorradorAsignacion,
): Partial<Record<keyof BorradorAsignacion, string>> {
  const errores: Partial<Record<keyof BorradorAsignacion, string>> = {}

  if (valores.rol === 'COMPANY_ADMINISTRATOR' && valores.companiaId === '') {
    errores.companiaId = 'Indique la compañía que administrará.'
  }

  if (valores.fechaHoraInicio === '') {
    errores.fechaHoraInicio = 'Indique el inicio de vigencia.'
  }

  if (valores.fechaHoraFin === '') {
    errores.fechaHoraFin = 'Indique el fin de vigencia.'
  }

  if (
    valores.fechaHoraInicio !== '' &&
    valores.fechaHoraFin !== '' &&
    valores.fechaHoraFin <= valores.fechaHoraInicio
  ) {
    errores.fechaHoraFin = 'El fin debe ser posterior al inicio.'
  }

  return errores
}

/**
 * Convierte el valor de un `datetime-local` (hora local del navegador, sin zona) al instante UTC que
 * la API espera. Los instantes se persisten siempre en UTC (Principio IV, RF-080).
 */
export function aInstanteUtc(valorLocal: string): string {
  return new Date(valorLocal).toISOString()
}
