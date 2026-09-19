import { useEffect, useId, useRef, type ReactElement, type ReactNode } from 'react'
import './dialogo.css'

/**
 * Diálogo modal accesible construido sobre el elemento nativo `<dialog>`.
 *
 * Se usa `showModal()` en lugar de un `<div role="dialog">` propio porque el navegador ya aporta el
 * comportamiento que de otro modo habría que reimplementar —y que se suele implementar mal—:
 * atrapar el foco dentro del diálogo, inertizar el resto de la página para lectores de pantalla y
 * cerrar con Escape (ux-ui.md §26).
 */
export function Dialogo({
  titulo,
  children,
  alCerrar,
}: {
  titulo: string
  children: ReactNode
  alCerrar: () => void
}): ReactElement {
  const referencia = useRef<HTMLDialogElement>(null)
  const idTitulo = useId()

  useEffect(() => {
    const dialogo = referencia.current
    if (dialogo && !dialogo.open) {
      dialogo.showModal()
    }
  }, [])

  return (
    <dialog
      ref={referencia}
      className="dialogo"
      aria-labelledby={idTitulo}
      // Escape dispara "cancel": se propaga al cierre para que el estado de React no quede
      // pensando que el diálogo sigue abierto.
      onCancel={(evento) => {
        evento.preventDefault()
        alCerrar()
      }}
      onClose={alCerrar}
    >
      <div className="dialogo-encabezado">
        <h2 id={idTitulo}>{titulo}</h2>
        <button
          type="button"
          className="dialogo-cerrar"
          onClick={alCerrar}
          aria-label="Cerrar el diálogo"
        >
          ✕
        </button>
      </div>

      {children}
    </dialog>
  )
}
