import { useState, type ReactElement } from 'react'
import { ApiError } from '../../lib/apiClient'
import { useAreas } from '../area-access/hooks'
import type { Estado } from '../companies/api'
import { useCompanias } from '../companies/hooks'
import { useUnidades } from '../org-units/hooks'
import { ETIQUETA_ALCANCE, ETIQUETA_DIA, sujetoDe, type PermisoAcceso } from './api'
import { usePermisos } from './hooks'
import { PermisoFormulario } from './PermisoFormulario'
import './permissions.css'

type Edicion = { modo: 'crear' } | { modo: 'editar'; permiso: PermisoAcceso } | null

/**
 * Mantenimiento de permisos de acceso por área (Historia 8, RF-020 a RF-022; ux-ui.md §18).
 *
 * Los permisos se muestran siempre dentro del contexto de una Compañía Principal y de un área suya:
 * primero se elige la Principal, luego el área. No existe una vista de "todos los permisos",
 * porque mezclaría configuraciones de Principales aisladas entre sí (RF-043).
 */
export function PermisosPage(): ReactElement {
  const [principalId, setPrincipalId] = useState('')
  const [areaId, setAreaId] = useState('')
  const [estado, setEstado] = useState<Estado | ''>('')
  const [edicion, setEdicion] = useState<Edicion>(null)

  const principales = useCompanias({
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
    tamañoPagina: 200,
  })

  const areas = useAreas(principalId === '' ? null : principalId)
  const unidades = useUnidades(principalId === '' ? null : principalId)
  const companias = useCompanias({ tamañoPagina: 200 })

  const permisos = usePermisos({
    areaAccesoId: areaId,
    estado: estado === '' ? undefined : estado,
    tamañoPagina: 200,
  })

  const area = (areas.data ?? []).find((a) => a.id === areaId)
  const error = permisos.error instanceof ApiError ? permisos.error : undefined

  function nombreSujeto(permiso: PermisoAcceso): string {
    const id = sujetoDe(permiso) ?? ''

    switch (permiso.alcance) {
      case 'UNIDAD_ORGANIZATIVA':
        return (unidades.data ?? []).find((u) => u.id === id)?.nombre ?? id
      case 'COMPANIA':
        return (companias.data?.items ?? []).find((c) => c.id === id)?.nombre ?? id
      case 'PERSONA':
        return id
    }
  }

  return (
    <section className="permisos">
      <header>
        <h1>Permisos de acceso</h1>
        <p className="permisos-subtitulo">
          Cada permiso concede acceso a un área de una compañía principal, con vigencia obligatoria
          y horario semanal. La autorización efectiva la decide siempre la evaluación de acceso.
        </p>
      </header>

      <div className="permisos-filtros">
        <div className="campo">
          <label htmlFor="permisos-principal">Compañía principal</label>
          <select
            id="permisos-principal"
            value={principalId}
            onChange={(evento) => {
              setPrincipalId(evento.target.value)
              setAreaId('')
            }}
          >
            <option value="">Seleccione una compañía principal…</option>
            {principales.data?.items.map((principal) => (
              <option key={principal.id} value={principal.id}>
                {principal.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label htmlFor="permisos-area">Área</label>
          <select
            id="permisos-area"
            value={areaId}
            disabled={principalId === ''}
            onChange={(evento) => setAreaId(evento.target.value)}
          >
            <option value="">Seleccione un área…</option>
            {(areas.data ?? []).map((a) => (
              <option key={a.id} value={a.id}>
                {a.nombre}
                {a.estado === 'INACTIVO' ? ' (inactiva)' : ''}
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label htmlFor="permisos-estado">Estado</label>
          <select
            id="permisos-estado"
            value={estado}
            onChange={(evento) => setEstado(evento.target.value as Estado | '')}
          >
            <option value="">Todos</option>
            <option value="ACTIVO">Activos</option>
            <option value="INACTIVO">Inactivos</option>
          </select>
        </div>

        <button
          type="button"
          className="primario"
          disabled={areaId === ''}
          onClick={() => setEdicion({ modo: 'crear' })}
        >
          Nuevo permiso
        </button>
      </div>

      {error && (
        <p className="aviso error" role="alert">
          {error.message}
        </p>
      )}

      {areaId === '' && (
        <p className="permisos-vacio">
          Elija una compañía principal y una de sus áreas para ver y administrar sus permisos.
        </p>
      )}

      {areaId !== '' && permisos.isPending && <p>Cargando permisos…</p>}

      {areaId !== '' && permisos.data && permisos.data.items.length === 0 && (
        <p className="permisos-vacio">
          Esta área no tiene permisos{estado === '' ? '' : ' con ese estado'}. Sin permisos
          aplicables, nadie obtiene acceso.
        </p>
      )}

      {areaId !== '' && permisos.data && permisos.data.items.length > 0 && (
        <div className="tarjeta permisos-tabla">
          <table>
            <caption className="sr-only">Permisos del área {area?.nombre}</caption>
            <thead>
              <tr>
                <th scope="col">Otorgado a</th>
                <th scope="col">Vigencia</th>
                <th scope="col">Horario</th>
                <th scope="col">Estado</th>
                <th scope="col">
                  <span className="sr-only">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {permisos.data.items.map((permiso) => (
                <tr key={permiso.id}>
                  <td>
                    <span className="permisos-alcance">{ETIQUETA_ALCANCE[permiso.alcance]}</span>
                    <br />
                    {nombreSujeto(permiso)}
                  </td>
                  <td>
                    {new Date(permiso.fechaHoraInicioVigencia).toLocaleString()}
                    <br />
                    hasta {new Date(permiso.fechaHoraFinVigencia).toLocaleString()}
                  </td>
                  <td>
                    <ul className="permisos-bloques">
                      {permiso.bloquesHorarios.map((b) => (
                        <li key={b.id ?? `${b.diaSemana}-${b.horaInicio}`}>
                          {ETIQUETA_DIA[b.diaSemana]} {b.horaInicio}–{b.horaFin}
                        </li>
                      ))}
                    </ul>
                  </td>
                  <td>{permiso.estado === 'ACTIVO' ? 'Activo' : 'Inactivo'}</td>
                  <td>
                    <button
                      type="button"
                      onClick={() => setEdicion({ modo: 'editar', permiso })}
                      aria-label={`Editar permiso de ${nombreSujeto(permiso)}`}
                    >
                      Editar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {edicion && area && (
        <PermisoFormulario
          {...edicion}
          areaAccesoId={area.id}
          nombreArea={area.nombre}
          companiaPrincipalId={principalId}
          alCerrar={() => setEdicion(null)}
        />
      )}
    </section>
  )
}
