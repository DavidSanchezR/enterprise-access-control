import { useState, type ReactElement } from 'react'
import { Dialogo } from '../../../components/Dialogo'
import { ApiError } from '../../../lib/apiClient'
import type { Compania } from '../api'
import { useCompanias, useCrearRelacion, useFinalizarRelacion, useRelaciones } from '../hooks'

/** Fecha/hora local en el formato que espera `datetime-local`. */
function ahoraLocal(): string {
  const ahora = new Date()
  const desplazado = new Date(ahora.getTime() - ahora.getTimezoneOffset() * 60_000)
  return desplazado.toISOString().slice(0, 16)
}

function formatear(iso: string | null): string {
  if (!iso) {
    return '—'
  }
  return new Date(iso).toLocaleString()
}

/**
 * Relaciones de una Compañía Contratista con Compañías Principales (RF-051, CS-012).
 *
 * Una contratista puede mantener relaciones vigentes con varias principales a la vez; declarar una
 * nueva con la *misma* principal cierra automáticamente la anterior, cosa que la pantalla advierte
 * antes de guardar para que no sorprenda.
 */
export function RelacionesDialogo({
  contratista,
  alCerrar,
}: {
  contratista: Compania
  alCerrar: () => void
}): ReactElement {
  const relaciones = useRelaciones(contratista.id)
  const crear = useCrearRelacion()
  const finalizar = useFinalizarRelacion()

  // Solo las principales pueden ser destino de una relación (RF-051).
  const principales = useCompanias({ tipoCompania: 'PRINCIPAL_MANDANTE', tamañoPagina: 200 })

  const [companiaPrincipalId, setCompaniaPrincipalId] = useState('')
  const [fechaHoraInicio, setFechaHoraInicio] = useState(ahoraLocal())

  const error =
    crear.error instanceof ApiError
      ? crear.error
      : finalizar.error instanceof ApiError
        ? finalizar.error
        : undefined

  const vigentes = (relaciones.data ?? []).filter((r) => r.fechaHoraFin === null)

  const yaVigenteConEsaPrincipal =
    companiaPrincipalId !== '' &&
    vigentes.some((r) => r.companiaPrincipalId === companiaPrincipalId)

  return (
    <Dialogo titulo={`Principales de ${contratista.nombre}`} alCerrar={alCerrar}>
      {error && (
        <p className="aviso error" role="alert">
          {error.message}
        </p>
      )}

      <h3>Relaciones declaradas</h3>

      {relaciones.isPending && <p>Cargando relaciones…</p>}

      {!relaciones.isPending && (relaciones.data ?? []).length === 0 && (
        <p className="campo-ayuda">
          Esta contratista todavía no tiene relaciones declaradas con ninguna principal.
        </p>
      )}

      {(relaciones.data ?? []).length > 0 && (
        <table className="relaciones-tabla">
          <caption className="sr-only">
            Relaciones vigentes e históricas con compañías principales
          </caption>
          <thead>
            <tr>
              <th scope="col">Principal</th>
              <th scope="col">Desde</th>
              <th scope="col">Hasta</th>
              <th scope="col">
                <span className="sr-only">Acciones</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {relaciones.data!.map((relacion) => {
              const principal = principales.data?.items.find(
                (c) => c.id === relacion.companiaPrincipalId,
              )
              const vigente = relacion.fechaHoraFin === null

              return (
                <tr key={relacion.id}>
                  <td>{principal?.nombre ?? relacion.companiaPrincipalId}</td>
                  <td>{formatear(relacion.fechaHoraInicio)}</td>
                  <td>
                    {/* "Vigente" en texto, no solo por ausencia de fecha. */}
                    {vigente ? (
                      <span className="etiqueta ACTIVO">Vigente</span>
                    ) : (
                      formatear(relacion.fechaHoraFin)
                    )}
                  </td>
                  <td>
                    {vigente && (
                      <button
                        type="button"
                        className="peligro"
                        disabled={finalizar.isPending}
                        onClick={() =>
                          finalizar.mutate({
                            contratistaId: contratista.id,
                            relacionId: relacion.id,
                          })
                        }
                      >
                        Finalizar
                      </button>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}

      <h3 className="relaciones-alta">Declarar nueva relación</h3>

      <div className="campo">
        <label htmlFor="relacion-principal">Compañía principal</label>
        <select
          id="relacion-principal"
          value={companiaPrincipalId}
          onChange={(e) => setCompaniaPrincipalId(e.target.value)}
        >
          <option value="">Seleccione…</option>
          {principales.data?.items.map((principal) => (
            <option key={principal.id} value={principal.id}>
              {principal.nombre}
            </option>
          ))}
        </select>
      </div>

      <div className="campo">
        <label htmlFor="relacion-inicio">Vigente desde</label>
        <input
          id="relacion-inicio"
          type="datetime-local"
          value={fechaHoraInicio}
          onChange={(e) => setFechaHoraInicio(e.target.value)}
        />
      </div>

      {yaVigenteConEsaPrincipal && (
        <p className="aviso" role="status">
          Ya existe una relación vigente con esa principal. Al guardar, la anterior se cerrará en la
          fecha indicada.
        </p>
      )}

      <div className="dialogo-acciones">
        <button type="button" onClick={alCerrar}>
          Cerrar
        </button>
        <button
          type="button"
          className="primario"
          disabled={companiaPrincipalId === '' || fechaHoraInicio === '' || crear.isPending}
          onClick={() =>
            crear.mutate(
              {
                contratistaId: contratista.id,
                companiaPrincipalId,
                // El servidor trabaja en UTC; el control entrega hora local.
                fechaHoraInicio: new Date(fechaHoraInicio).toISOString(),
              },
              { onSuccess: () => setCompaniaPrincipalId('') },
            )
          }
        >
          {crear.isPending ? 'Guardando…' : 'Declarar relación'}
        </button>
      </div>
    </Dialogo>
  )
}
