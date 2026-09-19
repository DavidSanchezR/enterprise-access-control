import '@testing-library/jest-dom/vitest'

/**
 * jsdom no implementa el modo modal de `<dialog>` (`showModal`/`close`), así que cualquier
 * componente que lo use fallaría en las pruebas aunque funcione en el navegador. Se aporta la
 * mínima implementación que mantiene coherente `open` y emite los eventos `close`/`cancel`, para no
 * tener que renunciar al elemento nativo —que es justamente el que aporta la accesibilidad del
 * diálogo— sólo por una carencia del entorno de pruebas.
 */
if (typeof HTMLDialogElement !== 'undefined' && !HTMLDialogElement.prototype.showModal) {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement): void {
    this.open = true
  }

  HTMLDialogElement.prototype.show = function show(this: HTMLDialogElement): void {
    this.open = true
  }

  HTMLDialogElement.prototype.close = function close(
    this: HTMLDialogElement,
    valorRetorno?: string,
  ): void {
    this.open = false
    if (valorRetorno !== undefined) {
      this.returnValue = valorRetorno
    }
    this.dispatchEvent(new Event('close'))
  }
}
