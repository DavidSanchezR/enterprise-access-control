import { useState, type ReactElement } from 'react'
import { ApiError } from '../../../lib/apiClient'
import { useMaestro } from '../../masters/hooks'
import { useReemplazarTiposPersonaDeArea, useTiposPersonaDeArea } from './hooks'

/**
 * Tipos de persona autorizados en un área (Historia 7, RF-019).
 *
 * El contrato reemplaza el conjunto entero (`PUT`), así que la pantalla edita la lista completa y
 * guarda de una vez: marcar una casilla no escribe nada por sí solo. Eso evita que un guardado a
 * medias deje el área autorizando un conjunto que nadie pidió.
 *
 * El catálogo se pide filtrado por `ACTIVO` porque el servidor rechaza un tipo INACTIVO (RF-032);
 * ofrecerlo sería proponer una opción que va a fallar. Los que ya estuvieran asociados y se hayan
 * inactivado después se muestran igualmente, marcados, para que su retirada sea una decisión visible
 * y no una pérdida silenciosa.
 */
export function TiposPersonaPorArea({
  areaId,
  nombreArea,
}: {
  areaId: string
  nombreArea: string
}): ReactElement {
  const catalogo = useMaestro('tipos-persona', 'ACTIVO')
  const asociados = useTiposPersonaDeArea(areaId)
  const reemplazar = useReemplazarTiposPersonaDeArea()

  /**
   * `null` significa "todavía no se ha tocado nada": se muestra entonces lo que devuelve el
   * servidor. Derivarlo en el renderizado, en vez de copiarlo a estado con un efecto, evita pisar
   * una edición en curso cuando la consulta se refresca.
   */
  const [edicion, setEdicion] = useState<string[] | null>(null)
  const seleccion = edicion ?? asociados.data ?? []

  const error =
    (asociados.error instanceof ApiError && asociados.error) ||
    (reemplazar.error instanceof ApiError && reemplazar.error) ||
    undefined

  // Un tipo ya asociado que entretanto pasó a INACTIVO no viene en el catálogo filtrado; se añade
  // para que siga siendo visible y pueda retirarse.
  const opciones = [
    ...(catalogo.data ?? []),
    ...seleccion
      .filter((id) => !(catalogo.data ?? []).some((tipo) => tipo.id === id))
      .map((id) => ({ id, nombre: `${id} (inactivo)`, estado: 'INACTIVO' as const })),
  ]

  const cargando = catalogo.isPending || asociados.isPending

  function alternar(id: string): void {
    setEdicion(
      seleccion.includes(id) ? seleccion.filter((actual) => actual !== id) : [...seleccion, id],
    )
  }

  return (
    <div className="tarjeta areas-tipos">
      <h2>Tipos de persona autorizados</h2>
      <p className="campo-ayuda">
        Perfiles que pueden acceder a <strong>{nombreArea}</strong>. Cada área se declara por
        separado: un área hija no hereda los de su área superior.
      </p>

      {error && (
        <p className="aviso error" role="alert">
          {error.message}
        </p>
      )}

      {cargando && <p>Cargando tipos de persona…</p>}

      {!cargando && opciones.length === 0 && (
        <p className="areas-vacio">
          No hay tipos de persona activos en el catálogo. Créelos en Datos maestros para poder
          autorizarlos aquí.
        </p>
      )}

      {!cargando && opciones.length > 0 && (
        <fieldset className="areas-tipos-lista">
          <legend className="sr-only">Tipos de persona autorizados en {nombreArea}</legend>

          {opciones.map((tipo) => (
            <label key={tipo.id} className="areas-tipo-opcion">
              <input
                type="checkbox"
                checked={seleccion.includes(tipo.id)}
                onChange={() => alternar(tipo.id)}
              />
              {tipo.nombre}
            </label>
          ))}
        </fieldset>
      )}

      <p aria-live="polite" className="campo-ayuda">
        {seleccion.length === 0
          ? 'Ningún tipo autorizado: nadie podrá acceder a esta área.'
          : `${seleccion.length} tipo${seleccion.length === 1 ? '' : 's'} de persona autorizado${
              seleccion.length === 1 ? '' : 's'
            }.`}
      </p>

      <button
        type="button"
        className="primario"
        disabled={cargando || reemplazar.isPending}
        onClick={() =>
          reemplazar.mutate(
            { areaId, tipoPersonaIds: seleccion },
            { onSuccess: () => setEdicion(null) },
          )
        }
      >
        {reemplazar.isPending ? 'Guardando…' : 'Guardar autorizaciones'}
      </button>
    </div>
  )
}
