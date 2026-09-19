import { useState, type ReactElement } from 'react'
import { NavLink, useParams } from 'react-router-dom'
import { ApiError } from '../../lib/apiClient'
import { ESTADOS, type Estado } from '../companies/api'
import { CATALOGOS, catalogoPorRuta, type MasterItem, type RutaCatalogo } from './api'
import { MaestroFormulario } from './MaestroFormulario'
import { useActualizarMaestro, useMaestro } from './hooks'
import './masters.css'

/**
 * Mantenimiento de los cinco catálogos maestros (Historia 3, RF-030 a RF-032).
 *
 * Una sola pantalla sirve a los cinco porque comparten exactamente la misma forma; el catálogo se
 * elige en la URL, de modo que cada uno conserva su propio enlace compartible.
 */
export function MaestrosPage(): ReactElement {
  const { catalogo: rutaParam } = useParams<{ catalogo: string }>()
  const catalogo = catalogoPorRuta(rutaParam ?? '') ?? CATALOGOS[0]

  const [estado, setEstado] = useState<Estado | ''>('')
  const [creando, setCreando] = useState(false)
  const [enEdicion, setEnEdicion] = useState<MasterItem | null>(null)

  const consulta = useMaestro(catalogo.ruta, estado === '' ? undefined : estado)
  const actualizar = useActualizarMaestro(catalogo.ruta)

  const error =
    (consulta.error instanceof ApiError && consulta.error) ||
    (actualizar.error instanceof ApiError && actualizar.error) ||
    undefined

  function alternarEstado(item: MasterItem): void {
    // Cambiar a INACTIVO nunca borra: el valor deja de ofrecerse en asignaciones nuevas y el
    // histórico que ya lo referencia permanece intacto (RF-032).
    actualizar.mutate({
      id: item.id,
      nombre: item.nombre,
      estado: item.estado === 'ACTIVO' ? 'INACTIVO' : 'ACTIVO',
    })
  }

  return (
    <section className="maestros">
      <header>
        <h1>Datos maestros</h1>
        <p className="maestros-subtitulo">
          Catálogos versionados del sistema. Desactivar un valor lo retira de las asignaciones
          nuevas sin afectar al histórico que ya lo usa.
        </p>
      </header>

      <nav className="maestros-pestanas" aria-label="Catálogos maestros">
        <ul>
          {CATALOGOS.map((item) => (
            <li key={item.ruta}>
              <NavLink to={`/maestros/${item.ruta}`}>{item.titulo}</NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <div className="maestros-barra">
        <div>
          <label htmlFor="filtro-estado">Estado</label>
          <select
            id="filtro-estado"
            value={estado}
            onChange={(evento) => setEstado(evento.target.value as Estado | '')}
          >
            <option value="">Todos</option>
            {ESTADOS.map((valor) => (
              <option key={valor} value={valor}>
                {valor}
              </option>
            ))}
          </select>
        </div>

        <button type="button" className="primario" onClick={() => setCreando(true)}>
          Nuevo valor
        </button>
      </div>

      {error && (
        <p className="aviso error" role="alert">
          {error.codigo === 'NOMBRE_YA_REGISTRADO'
            ? 'Ya existe un valor con ese nombre en este catálogo.'
            : error.message}
        </p>
      )}

      <div className="tarjeta maestros-tabla">
        <table>
          <caption className="sr-only">{catalogo.titulo}</caption>
          <thead>
            <tr>
              <th scope="col">Nombre</th>
              <th scope="col">Estado</th>
              <th scope="col">
                <span className="sr-only">Acciones</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {consulta.isPending && (
              <tr>
                <td colSpan={3}>Cargando {catalogo.titulo.toLowerCase()}…</td>
              </tr>
            )}

            {!consulta.isPending && consulta.data?.length === 0 && (
              <tr>
                <td colSpan={3}>
                  {estado === ''
                    ? `Todavía no hay valores en ${catalogo.titulo.toLowerCase()}.`
                    : `No hay valores en estado ${estado}.`}
                </td>
              </tr>
            )}

            {consulta.data?.map((item) => (
              <tr key={item.id}>
                <td>{item.nombre}</td>
                <td>
                  {/* Estado siempre como texto: el color es apoyo, no el dato. */}
                  <span className={`etiqueta ${item.estado}`}>{item.estado}</span>
                </td>
                <td className="maestros-acciones">
                  <button type="button" onClick={() => setEnEdicion(item)}>
                    Editar
                  </button>

                  <button
                    type="button"
                    disabled={actualizar.isPending}
                    onClick={() => alternarEstado(item)}
                  >
                    {item.estado === 'ACTIVO' ? 'Desactivar' : 'Reactivar'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {creando && (
        <MaestroFormulario modo="crear" catalogo={catalogo} alCerrar={() => setCreando(false)} />
      )}

      {enEdicion && (
        <MaestroFormulario
          modo="editar"
          catalogo={catalogo}
          item={enEdicion}
          alCerrar={() => setEnEdicion(null)}
        />
      )}
    </section>
  )
}

export type { RutaCatalogo }
