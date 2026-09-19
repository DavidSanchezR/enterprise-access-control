import { useState, type ReactElement } from 'react'
import { ApiError } from '../../lib/apiClient'
import {
  ESTADOS,
  ETIQUETA_TIPO_COMPANIA,
  TIPOS_COMPANIA,
  type Compania,
  type Estado,
  type TipoCompania,
} from './api'
import { CompaniaFormulario } from './CompaniaFormulario'
import { RelacionesDialogo } from './RelacionesContratistaPrincipal/RelacionesDialogo'
import { useCompanias } from './hooks'
import './companies.css'

const TAMANO_PAGINA = 20

/**
 * Mantenimiento de compañías y su clasificación PRINCIPAL_MANDANTE/CONTRATISTA
 * (Historia 2, RF-006, RF-042).
 */
export function CompaniasPage(): ReactElement {
  const [tipoCompania, setTipoCompania] = useState<TipoCompania | ''>('')
  const [estado, setEstado] = useState<Estado | ''>('')
  const [texto, setTexto] = useState('')
  const [pagina, setPagina] = useState(1)

  const [creando, setCreando] = useState(false)
  const [enEdicion, setEnEdicion] = useState<Compania | null>(null)
  const [relacionesDe, setRelacionesDe] = useState<Compania | null>(null)

  const consulta = useCompanias({
    tipoCompania: tipoCompania === '' ? undefined : tipoCompania,
    estado: estado === '' ? undefined : estado,
    texto: texto.trim() === '' ? undefined : texto.trim(),
    pagina,
    tamañoPagina: TAMANO_PAGINA,
  })

  const total = consulta.data?.total ?? 0
  const ultimaPagina = Math.max(1, Math.ceil(total / TAMANO_PAGINA))
  const error = consulta.error instanceof ApiError ? consulta.error : undefined

  function reiniciarPagina<T>(aplicar: (valor: T) => void): (valor: T) => void {
    return (valor) => {
      aplicar(valor)
      setPagina(1)
    }
  }

  return (
    <section className="companias">
      <header className="companias-encabezado">
        <div>
          <h1>Compañías</h1>
          <p className="companias-subtitulo">
            Cada compañía se clasifica como principal mandante o contratista. Solo las principales
            poseen unidades organizativas.
          </p>
        </div>

        <button type="button" className="primario" onClick={() => setCreando(true)}>
          Nueva compañía
        </button>
      </header>

      <div className="companias-filtros">
        <div>
          <label htmlFor="filtro-tipo">Tipo</label>
          <select
            id="filtro-tipo"
            value={tipoCompania}
            onChange={(e) => reiniciarPagina(setTipoCompania)(e.target.value as TipoCompania | '')}
          >
            <option value="">Todos</option>
            {TIPOS_COMPANIA.map((valor) => (
              <option key={valor} value={valor}>
                {ETIQUETA_TIPO_COMPANIA[valor]}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label htmlFor="filtro-estado">Estado</label>
          <select
            id="filtro-estado"
            value={estado}
            onChange={(e) => reiniciarPagina(setEstado)(e.target.value as Estado | '')}
          >
            <option value="">Todos</option>
            {ESTADOS.map((valor) => (
              <option key={valor} value={valor}>
                {valor}
              </option>
            ))}
          </select>
        </div>

        <div className="companias-busqueda">
          <label htmlFor="filtro-texto">Buscar</label>
          <input
            id="filtro-texto"
            type="search"
            placeholder="Nombre o número de documento"
            value={texto}
            onChange={(e) => reiniciarPagina(setTexto)(e.target.value)}
          />
        </div>
      </div>

      {error && (
        <p className="aviso error" role="alert">
          {error.message}
        </p>
      )}

      <div className="tarjeta companias-tabla">
        <table>
          <caption className="sr-only">Compañías dentro de su alcance</caption>
          <thead>
            <tr>
              <th scope="col">Nombre</th>
              <th scope="col">Documento</th>
              <th scope="col">Tipo</th>
              <th scope="col">Estado</th>
              <th scope="col">
                <span className="sr-only">Acciones</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {consulta.isPending && (
              <tr>
                <td colSpan={5}>Cargando compañías…</td>
              </tr>
            )}

            {!consulta.isPending && consulta.data?.items.length === 0 && (
              <tr>
                <td colSpan={5}>No hay compañías que coincidan con los filtros aplicados.</td>
              </tr>
            )}

            {consulta.data?.items.map((compania) => (
              <tr key={compania.id}>
                <td>{compania.nombre}</td>
                <td>
                  <code>{compania.numeroDocumento}</code>
                </td>
                <td>
                  {/* El tipo se muestra como texto: el color nunca es el único indicador. */}
                  <span className={`etiqueta tipo-${compania.tipoCompania}`}>
                    {ETIQUETA_TIPO_COMPANIA[compania.tipoCompania]}
                  </span>
                </td>
                <td>
                  <span className={`etiqueta ${compania.estado}`}>{compania.estado}</span>
                </td>
                <td className="companias-acciones">
                  <button type="button" onClick={() => setEnEdicion(compania)}>
                    Editar
                  </button>

                  {/* Las relaciones se declaran desde la Contratista hacia sus Principales. */}
                  {compania.tipoCompania === 'CONTRATISTA' && (
                    <button type="button" onClick={() => setRelacionesDe(compania)}>
                      Principales
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <nav className="companias-paginacion" aria-label="Paginación de compañías">
        <button type="button" disabled={pagina <= 1} onClick={() => setPagina((p) => p - 1)}>
          Anterior
        </button>

        <span aria-live="polite">
          Página {pagina} de {ultimaPagina} · {total} compañías
        </span>

        <button
          type="button"
          disabled={pagina >= ultimaPagina}
          onClick={() => setPagina((p) => p + 1)}
        >
          Siguiente
        </button>
      </nav>

      {creando && <CompaniaFormulario modo="crear" alCerrar={() => setCreando(false)} />}

      {enEdicion && (
        <CompaniaFormulario
          modo="editar"
          compania={enEdicion}
          alCerrar={() => setEnEdicion(null)}
        />
      )}

      {relacionesDe && (
        <RelacionesDialogo contratista={relacionesDe} alCerrar={() => setRelacionesDe(null)} />
      )}
    </section>
  )
}
