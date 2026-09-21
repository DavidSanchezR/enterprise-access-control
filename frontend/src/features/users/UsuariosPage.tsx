import { useMemo, useState, type ReactElement } from 'react'
import { formatearFechaHora } from '../../lib/fechas'
import type { RolAdministrativo } from '../../lib/apiClient'
import { ROLES_ADMINISTRATIVOS } from '../../lib/apiClient'
import { useSesion } from '../auth/useSesion'
import { useCompanias } from '../companies/hooks'
import { etiquetaRol } from './CamposAsignacion'
import { AsignarRolDialogo } from './AsignarRolDialogo'
import { CrearUsuarioWizard } from './CrearUsuarioWizard'
import { UsuarioDetalle } from './UsuarioDetalle'
import { UsuarioFormulario } from './UsuarioFormulario'
import { ESTADOS_USUARIO, type AsignacionRol, type EstadoUsuario, type Usuario } from './api'
import { useDesbloquearUsuario, useUsuarios } from './hooks'
import { describirError } from './mensajesRol'
import './users.css'

const TAMANO_PAGINA = 20

/** Asignación vigente que vence antes, para la columna de vigencia (ux-ui.md §35). */
function proximaAVencer(asignaciones: AsignacionRol[]): AsignacionRol | undefined {
  return [...asignaciones]
    .filter((a) => a.vigente)
    .sort((a, b) => a.fechaHoraFin.localeCompare(b.fechaHoraFin))[0]
}

/**
 * Administración de usuarios y roles administrativos (UX-22; ux-ui.md §35).
 *
 * **Aislamiento (RF-077)**: la página no filtra en el cliente lo que el servidor ya le ocultó. El
 * listado, el total y la paginación provienen de una consulta que el backend acota al alcance del
 * solicitante, de modo que un usuario fuera de alcance no aparece, no se cuenta y no se insinúa por
 * un hueco en la numeración. Los selectores de compañía se limitan igualmente al alcance.
 */
export function UsuariosPage(): ReactElement {
  const { sesion } = useSesion()
  const rolPropio = sesion?.rol ?? null

  const [estado, setEstado] = useState<EstadoUsuario | ''>('')
  const [texto, setTexto] = useState('')
  const [rolFiltro, setRolFiltro] = useState<RolAdministrativo | ''>('')
  const [companiaFiltro, setCompaniaFiltro] = useState('')
  const [soloVigentes, setSoloVigentes] = useState(false)
  const [pagina, setPagina] = useState(1)

  const [creando, setCreando] = useState(false)
  const [enEdicion, setEnEdicion] = useState<Usuario | null>(null)
  const [enDetalle, setEnDetalle] = useState<Usuario | null>(null)
  const [asignandoA, setAsignandoA] = useState<Usuario | null>(null)

  const consulta = useUsuarios({
    estado: estado === '' ? undefined : estado,
    // La búsqueda por correo la resuelve el servidor sobre todo el alcance autorizado y antes de
    // paginar (RF-077, UX-22): un usuario de la página 3 se encuentra desde la página 1.
    texto: texto.trim() === '' ? undefined : texto.trim(),
    pagina,
    tamañoPagina: TAMANO_PAGINA,
  })

  const desbloquear = useDesbloquearUsuario()

  // El selector de compañía ya viene acotado por el servidor al alcance del solicitante.
  const companias = useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })

  const nombrePorCompania = useMemo(() => {
    const mapa = new Map<string, string>()
    for (const compania of companias.data?.items ?? []) {
      mapa.set(compania.id, compania.nombre)
    }
    return mapa
  }, [companias.data])

  const zonaPorCompania = useMemo(() => {
    const mapa = new Map<string, string | null>()
    for (const compania of companias.data?.items ?? []) {
      mapa.set(compania.id, compania.zonaHorariaIana ?? null)
    }
    return mapa
  }, [companias.data])

  const total = consulta.data?.total ?? 0
  const ultimaPagina = Math.max(1, Math.ceil(total / TAMANO_PAGINA))

  const error = describirError(consulta.error) ?? describirError(desbloquear.error)

  // El correo y el estado los filtra el servidor (contracts/users.yaml v2.1.0). Rol, compañía y
  // vigencia siguen aplicándose sobre la página recibida: el contrato no declara parámetros para
  // ellos, y moverlos al servidor sería alcance nuevo, no el cierre de esta desviación.
  const visibles = (consulta.data?.items ?? []).filter((usuario) => {
    if (rolFiltro !== '' && !usuario.asignacionesRol.some((a) => a.rol === rolFiltro)) {
      return false
    }

    if (companiaFiltro !== '' && !usuario.asignacionesRol.some((a) => a.companiaId === companiaFiltro)) {
      return false
    }

    if (soloVigentes && !usuario.asignacionesRol.some((a) => a.vigente)) {
      return false
    }

    return true
  })

  return (
    <section className="usuarios">
      <header className="usuarios-encabezado">
        <div>
          <h1>Usuarios y roles administrativos</h1>
          <p className="usuarios-subtitulo">
            Cuentas que operan el sistema y las asignaciones de rol que definen su alcance.
          </p>
        </div>

        <button type="button" className="primario" onClick={() => setCreando(true)}>
          Nuevo usuario
        </button>
      </header>

      <div className="usuarios-filtros">
        <div>
          <label htmlFor="filtro-texto">Correo</label>
          <input
            id="filtro-texto"
            type="search"
            value={texto}
            onChange={(evento) => {
              setTexto(evento.target.value)
              // La búsqueda cambia el conjunto completo, así que la página actual deja de
              // significar lo mismo: buscar desde la página 3 mostraría un hueco vacío.
              setPagina(1)
            }}
          />
        </div>

        <div>
          <label htmlFor="filtro-estado">Estado</label>
          <select
            id="filtro-estado"
            value={estado}
            onChange={(evento) => {
              setEstado(evento.target.value as EstadoUsuario | '')
              setPagina(1)
            }}
          >
            <option value="">Todos</option>
            {ESTADOS_USUARIO.map((valor) => (
              <option key={valor} value={valor}>
                {valor}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="filtro-rol">Rol</label>
          <select
            id="filtro-rol"
            value={rolFiltro}
            onChange={(evento) => setRolFiltro(evento.target.value as RolAdministrativo | '')}
          >
            <option value="">Todos</option>
            {ROLES_ADMINISTRATIVOS.map((rol) => (
              <option key={rol} value={rol}>
                {etiquetaRol(rol)}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="filtro-compania">Compañía</label>
          <select
            id="filtro-compania"
            value={companiaFiltro}
            onChange={(evento) => setCompaniaFiltro(evento.target.value)}
          >
            <option value="">Todas</option>
            {(companias.data?.items ?? []).map((compania) => (
              <option key={compania.id} value={compania.id}>
                {compania.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="filtro-casilla">
          <label htmlFor="filtro-vigentes">
            <input
              id="filtro-vigentes"
              type="checkbox"
              checked={soloVigentes}
              onChange={(evento) => setSoloVigentes(evento.target.checked)}
            />
            Solo con asignaciones vigentes
          </label>
        </div>
      </div>

      {error && (
        <p className="aviso error" role="alert">
          {error.mensaje}
        </p>
      )}

      <div className="tarjeta usuarios-tabla">
        <table>
          <caption className="sr-only">
            Usuarios dentro de su alcance, con su estado, roles vigentes y vigencia
          </caption>
          <thead>
            <tr>
              <th scope="col">Correo</th>
              <th scope="col">Estado</th>
              <th scope="col">Roles vigentes</th>
              <th scope="col">Compañías</th>
              <th scope="col">Vence</th>
              <th scope="col">
                <span className="sr-only">Acciones</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {consulta.isPending && (
              <tr>
                <td colSpan={6}>Cargando usuarios…</td>
              </tr>
            )}

            {!consulta.isPending && visibles.length === 0 && (
              <tr>
                <td colSpan={6}>No hay usuarios que coincidan con los filtros aplicados.</td>
              </tr>
            )}

            {visibles.map((usuario) => {
              const proxima = proximaAVencer(usuario.asignacionesRol)

              return (
                <tr key={usuario.id}>
                  <td>
                    {usuario.correo}
                    {usuario.requiereCambioPassword && (
                      <span className="usuarios-nota"> · debe cambiar su contraseña</span>
                    )}
                  </td>
                  <td>
                    {/* El literal de estado se muestra como texto: el color no es el único indicador. */}
                    <span className={`etiqueta ${usuario.estado}`}>{usuario.estado}</span>
                  </td>
                  {/* Rol, compañía y vigencia son tres dimensiones independientes: se muestran en
                      columnas separadas y no fusionadas en un solo texto (ux-ui.md §35, §16). */}
                  <td>
                    {usuario.asignacionesRol.length === 0
                      ? '—'
                      : usuario.asignacionesRol.map((a) => (
                          <span key={a.id} className="etiqueta rol">
                            {etiquetaRol(a.rol)}
                          </span>
                        ))}
                  </td>
                  <td>
                    {usuario.asignacionesRol.map((a) => (
                      <span key={a.id} className="etiqueta compania">
                        {a.companiaId === null
                          ? 'Todas'
                          : (nombrePorCompania.get(a.companiaId) ?? 'Compañía fuera de su alcance')}
                      </span>
                    ))}
                  </td>
                  <td>
                    {proxima
                      ? formatearFechaHora(
                          proxima.fechaHoraFin,
                          proxima.companiaId === null
                            ? null
                            : zonaPorCompania.get(proxima.companiaId),
                        )
                      : '—'}
                  </td>
                  <td className="usuarios-acciones">
                    <button type="button" onClick={() => setEnDetalle(usuario)}>
                      Ver
                    </button>

                    <button type="button" onClick={() => setEnEdicion(usuario)}>
                      Editar
                    </button>

                    <button type="button" onClick={() => setAsignandoA(usuario)}>
                      Asignar rol
                    </button>

                    {usuario.estado === 'BLOQUEADO' && (
                      <button
                        type="button"
                        className="peligro"
                        disabled={desbloquear.isPending}
                        onClick={() => desbloquear.mutate(usuario.id)}
                      >
                        Desbloquear
                      </button>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <nav className="usuarios-paginacion" aria-label="Paginación de usuarios">
        <button type="button" disabled={pagina <= 1} onClick={() => setPagina((p) => p - 1)}>
          Anterior
        </button>

        <span aria-live="polite">
          Página {pagina} de {ultimaPagina} · {total} usuarios en su alcance
        </span>

        <button
          type="button"
          disabled={pagina >= ultimaPagina}
          onClick={() => setPagina((p) => p + 1)}
        >
          Siguiente
        </button>
      </nav>

      {rolPropio === 'COMPANY_ADMINISTRATOR' && (
        <p className="aviso info">
          Su rol administra únicamente usuarios de su compañía. Los de otras compañías no aparecen
          aquí ni se cuentan en el total.
        </p>
      )}

      {creando && <CrearUsuarioWizard alCerrar={() => setCreando(false)} />}

      {enEdicion && (
        <UsuarioFormulario usuario={enEdicion} alCerrar={() => setEnEdicion(null)} />
      )}

      {enDetalle && <UsuarioDetalle usuario={enDetalle} alCerrar={() => setEnDetalle(null)} />}

      {asignandoA && (
        <AsignarRolDialogo usuario={asignandoA} alCerrar={() => setAsignandoA(null)} />
      )}
    </section>
  )
}
