/**
 * Conversión entre instantes UTC —como viajan y se persisten— y el valor de un
 * `<input type="datetime-local">`, que se expresa en la hora local del navegador.
 */

/** ISO UTC → `YYYY-MM-DDTHH:mm` en hora local del navegador. */
export function aValorLocal(iso: string): string {
  const fecha = new Date(iso)
  const desfase = fecha.getTimezoneOffset() * 60_000
  return new Date(fecha.getTime() - desfase).toISOString().slice(0, 16)
}

/** `YYYY-MM-DDTHH:mm` en hora local del navegador → ISO UTC. */
export function aIsoUtc(valorLocal: string): string {
  return new Date(valorLocal).toISOString()
}
