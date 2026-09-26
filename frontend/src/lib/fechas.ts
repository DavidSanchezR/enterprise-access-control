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

/**
 * Zona horaria de respaldo cuando el instante no es resoluble a una única Compañía Principal
 * (RF-080): misma función que `ZonaHoraria:TimeZoneId` cumple en el servidor.
 */
export const ZONA_RESPALDO = 'America/Lima'

/**
 * Presenta un instante UTC en la zona horaria de una Compañía Principal (RF-080).
 *
 * El instante viaja y se almacena siempre en UTC; esta conversión es solo de presentación, así que
 * cambiar la zona de una Principal altera lo que se lee, nunca el dato. Cuando la zona no es
 * resoluble —una Contratista, o una entidad no ligada a una sola Principal— se usa la de respaldo,
 * igual que hace el servidor.
 */
export function formatearFechaHora(iso: string, zonaIana?: string | null): string {
  if (iso === '') {
    return ''
  }

  const fecha = new Date(iso)

  if (Number.isNaN(fecha.getTime())) {
    return ''
  }

  try {
    return new Intl.DateTimeFormat('es', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: zonaIana ?? ZONA_RESPALDO,
    }).format(fecha)
  } catch {
    // Un identificador IANA que este navegador no conozca no debe dejar la celda en blanco: se cae
    // a la zona de respaldo antes que perder el dato.
    return new Intl.DateTimeFormat('es', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: ZONA_RESPALDO,
    }).format(fecha)
  }
}

/**
 * Fecha civil que declara una vigencia diaria normalizada al día UTC, como la pertenencia
 * (`AsignaciónPersonaCompañía`): los diez primeros caracteres del instante.
 *
 * No se convierte a ninguna zona. En Lima, las 00:00 UTC del primer día de la pertenencia caen en el día
 * anterior, y el límite del formulario de permisos quedaría un día corrido (RF-082, RF-083; research.md
 * §36.4, §36.7).
 */
export function fechaDeclarada(iso: string): string {
  return iso.slice(0, 10)
}

/**
 * Fecha civil `AAAA-MM-DD` → `dd/mm/aaaa`, sin construir un `Date`.
 *
 * Una fecha civil no tiene zona. Pasarla por `new Date` la interpretaría como medianoche UTC y, en una zona
 * al oeste de UTC, se mostraría el día anterior (VF-004, RF-083 (f)).
 */
export function formatearFecha(fecha: string): string {
  const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(fecha)

  if (partes === null) {
    return ''
  }

  const [, anio, mes, dia] = partes
  return `${dia}/${mes}/${anio}`
}

/**
 * ISO UTC → valor de `datetime-local` interpretado en la zona de la Principal indicada (RF-080).
 *
 * Sin esto, un formulario de la Principal de Santiago mostraría la hora del navegador del operador
 * —que puede estar en otro huso— y el usuario ajustaría un valor que el servidor interpreta de otra
 * manera.
 */
export function aValorLocalEnZona(iso: string, zonaIana?: string | null): string {
  const fecha = new Date(iso)

  if (Number.isNaN(fecha.getTime())) {
    return ''
  }

  const partes = new Intl.DateTimeFormat('en-CA', {
    timeZone: zonaIana ?? ZONA_RESPALDO,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).formatToParts(fecha)

  const valor = (tipo: Intl.DateTimeFormatPartTypes): string =>
    partes.find((parte) => parte.type === tipo)?.value ?? '00'

  return `${valor('year')}-${valor('month')}-${valor('day')}T${valor('hour')}:${valor('minute')}`
}
