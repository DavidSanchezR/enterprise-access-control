import { useState, type ReactElement } from 'react'
import { ApiError } from '../../lib/apiClient'
import { ESTADOS_USUARIO, type EstadoUsuario, type Usuario } from './api'
import { useDesbloquearUsuario, useUsuarios } from './hooks'
import { UsuarioFormulario } from './UsuarioFormulario'
import { AlcanceCompaniasDialogo } from './AlcanceCompaniasDialogo'
import './users.css'

const TAMANO_PAGINA = 20

/**
 * Mantenimiento de usuarios (RF-004, RF-005; contracts/users.yaml).
 */
export function UsuariosPage(): ReactElement {
  const [estado, setEstado] = useState<EstadoUsuario | ''>('')
  const [pagina, setPagina] = useState(1)
  const [enEdicion, setEnEdicion] = useState<Usuario | null>(null)
  const [creando, setCreando] = useState(false)
  const [alcanceDe, setAlcanceDe] = useState<Usuario | null>(null)

  const consulta = useUsuarios({
    estado: estado === '' ? undefined : estado,
    pagina,
    tamañoPagina: TAMANO_PAGINA,
  })

  const desbloquear = useDesbloquearUsuario()

  const total = consulta.data?.total ?? 0
  const ultimaPagina = Math.max(1, Math.ceil(total / TAMANO_PAGINA))
  const error = consulta.error instanceof ApiError ? consulta.error : undefined

  return (
    <section className="usuarios">
      <header className="usuarios-encabezado">
        <div>
          <h1>Usuarios</h1>
          <p className="usuarios-subtitulo">
            Cuentas que operan el sistema y las compañías que cada una administra.
          </p>
        </div>

        <button type="button" className="primario" onClick={() => setCreando(true)}>
          Nuevo usuario
        </button>
      </header>

      <div className="usuarios-filtros">
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
      </div>

      {error && (
        <p className="aviso error" role="alert">
          {error.message}
        </p>
      )}

      {desbloquear.error instanceof ApiError && (
        <p className="aviso error" role="alert">
          {desbloquear.error.message}
        </p>
      )}

      <div className="tarjeta usuarios-tabla">
        <table>
          <caption className="sr-only">
            Usuarios del sistema con su estado y alcance de compañías
          </caption>
          <thead>
            <tr>
              <th scope="col">Correo</th>
              <th scope="col">Estado</th>
              <th scope="col">Compañías</th>
              <th scope="col">
                <span className="sr-only">Acciones</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {consulta.isPending && (
              <tr>
                <td colSpan={4}>Cargando usuarios…</td>
              </tr>
            )}

            {!consulta.isPending && consulta.data?.items.length === 0 && (
              <tr>
                <td colSpan={4}>
                  {estado === ''
                    ? 'Todavía no hay usuarios registrados.'
                    : `No hay usuarios en estado ${estado}.`}
                </td>
              </tr>
            )}

            {consulta.data?.items.map((usuario) => (
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
                <td>{usuario.alcanceCompanias.length}</td>
                <td className="usuarios-acciones">
                  <button type="button" onClick={() => setEnEdicion(usuario)}>
                    Editar
                  </button>

                  <button type="button" onClick={() => setAlcanceDe(usuario)}>
                    Alcance
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
            ))}
          </tbody>
        </table>
      </div>

      <nav className="usuarios-paginacion" aria-label="Paginación de usuarios">
        <button type="button" disabled={pagina <= 1} onClick={() => setPagina((p) => p - 1)}>
          Anterior
        </button>

        <span aria-live="polite">
          Página {pagina} de {ultimaPagina} · {total} usuarios
        </span>

        <button
          type="button"
          disabled={pagina >= ultimaPagina}
          onClick={() => setPagina((p) => p + 1)}
        >
          Siguiente
        </button>
      </nav>

      {creando && <UsuarioFormulario modo="crear" alCerrar={() => setCreando(false)} />}

      {enEdicion && (
        <UsuarioFormulario modo="editar" usuario={enEdicion} alCerrar={() => setEnEdicion(null)} />
      )}

      {alcanceDe && (
        <AlcanceCompaniasDialogo usuario={alcanceDe} alCerrar={() => setAlcanceDe(null)} />
      )}
    </section>
  )
}
