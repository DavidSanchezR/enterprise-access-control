import { useState, type ReactElement } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../lib/apiClient'
import { useMaestro } from '../masters/hooks'
import type { Persona } from './api'
import { PersonaFormulario } from './PersonaFormulario'
import { usePersonas } from './hooks'
import './people.css'

const TAMANO_PAGINA = 20

/**
 * Búsqueda y registro de personas (Historia 4, RF-012, RF-013, RF-035).
 *
 * La lista solo muestra personas cuya pertenencia vigente está dentro del alcance del usuario; la
 * restricción la aplica el servidor y la pantalla la explica para que un resultado vacío no se
 * confunda con un fallo.
 */
export function PersonasPage(): ReactElement {
  const [texto, setTexto] = useState('')
  const [tipoDocumentoId, setTipoDocumentoId] = useState('')
  const [pagina, setPagina] = useState(1)

  const [creando, setCreando] = useState(false)
  const [enEdicion, setEnEdicion] = useState<Persona | null>(null)

  const tiposDocumento = useMaestro('tipos-documento')

  const consulta = usePersonas({
    texto: texto.trim() === '' ? undefined : texto.trim(),
    tipoDocumentoId: tipoDocumentoId === '' ? undefined : tipoDocumentoId,
    pagina,
    tamañoPagina: TAMANO_PAGINA,
  })

  const total = consulta.data?.total ?? 0
  const ultimaPagina = Math.max(1, Math.ceil(total / TAMANO_PAGINA))
  const error = consulta.error instanceof ApiError ? consulta.error : undefined

  const nombreTipoDocumento = (id: string): string =>
    tiposDocumento.data?.find((t) => t.id === id)?.nombre ?? '—'

  return (
    <section className="personas">
      <header className="personas-encabezado">
        <div>
          <h1>Personas</h1>
          <p className="personas-subtitulo">
            Solo aparecen las personas con pertenencia vigente a una compañía de su alcance.
          </p>
        </div>

        <button type="button" className="primario" onClick={() => setCreando(true)}>
          Registrar persona
        </button>
      </header>

      <div className="personas-filtros">
        <div className="personas-busqueda">
          <label htmlFor="filtro-texto">Buscar</label>
          <input
            id="filtro-texto"
            type="search"
            placeholder="Nombres, apellidos o documento"
            value={texto}
            onChange={(evento) => {
              setTexto(evento.target.value)
              setPagina(1)
            }}
          />
        </div>

        <div>
          <label htmlFor="filtro-tipo-documento">Tipo de documento</label>
          <select
            id="filtro-tipo-documento"
            value={tipoDocumentoId}
            onChange={(evento) => {
              setTipoDocumentoId(evento.target.value)
              setPagina(1)
            }}
          >
            <option value="">Todos</option>
            {tiposDocumento.data?.map((tipo) => (
              <option key={tipo.id} value={tipo.id}>
                {tipo.nombre}
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

      <div className="tarjeta personas-tabla">
        <table>
          <caption className="sr-only">Personas dentro de su alcance</caption>
          <thead>
            <tr>
              <th scope="col">Apellidos y nombres</th>
              <th scope="col">Documento</th>
              <th scope="col">Correo</th>
              <th scope="col">Contacto de emergencia</th>
              <th scope="col">
                <span className="sr-only">Acciones</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {consulta.isPending && (
              <tr>
                <td colSpan={5}>Cargando personas…</td>
              </tr>
            )}

            {!consulta.isPending && consulta.data?.items.length === 0 && (
              <tr>
                <td colSpan={5}>
                  No hay personas que coincidan. Recuerde que solo se listan las que tienen
                  pertenencia vigente a una compañía de su alcance.
                </td>
              </tr>
            )}

            {consulta.data?.items.map((persona) => (
              <tr key={persona.id}>
                {/* El identificador técnico nunca se muestra (RF-013). */}
                <td>
                  {persona.apellidos}, {persona.nombres}
                </td>
                <td>
                  {nombreTipoDocumento(persona.tipoDocumentoId)}{' '}
                  <code>{persona.numeroDocumento}</code>
                </td>
                <td>{persona.correoElectronico}</td>
                <td>
                  {persona.contactoEmergencia}
                  <span className="personas-telefono"> · {persona.numeroEmergencia}</span>
                </td>
                <td className="personas-acciones">
                  <button type="button" onClick={() => setEnEdicion(persona)}>
                    Editar
                  </button>

                  {/* El histórico es donde vive la pertenencia, los contextos y su revocación. */}
                  <Link className="personas-enlace" to={`/personas/${persona.id}/historial`}>
                    Histórico
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <nav className="personas-paginacion" aria-label="Paginación de personas">
        <button type="button" disabled={pagina <= 1} onClick={() => setPagina((p) => p - 1)}>
          Anterior
        </button>

        <span aria-live="polite">
          Página {pagina} de {ultimaPagina} · {total} personas
        </span>

        <button
          type="button"
          disabled={pagina >= ultimaPagina}
          onClick={() => setPagina((p) => p + 1)}
        >
          Siguiente
        </button>
      </nav>

      {creando && <PersonaFormulario modo="crear" alCerrar={() => setCreando(false)} />}

      {enEdicion && (
        <PersonaFormulario modo="editar" persona={enEdicion} alCerrar={() => setEnEdicion(null)} />
      )}
    </section>
  )
}
