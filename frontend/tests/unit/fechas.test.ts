import { describe, expect, it } from 'vitest'
import { fechaDeclarada, formatearFecha } from '../../src/lib/fechas'

// VF-004 (RF-083): fechas civiles sin conversión de zona.
describe('fechaDeclarada', () => {
  it('toma el día declarado de una vigencia normalizada al día UTC, sin llevarlo a la zona local', () => {
    // En Lima estos instantes son el 31/07 19:00 y el 31/07/2027 18:59: el día declarado es otro.
    expect(fechaDeclarada('2026-08-01T00:00:00Z')).toBe('2026-08-01')
    expect(fechaDeclarada('2027-07-31T23:59:59.999Z')).toBe('2027-07-31')
  })
})

describe('formatearFecha', () => {
  it('presenta una fecha civil como dd/mm/aaaa sin desplazarla de día', () => {
    expect(formatearFecha('2026-09-25')).toBe('25/09/2026')
    expect(formatearFecha('2026-01-01')).toBe('01/01/2026')
  })

  it('devuelve una cadena vacía si el valor no es una fecha civil', () => {
    expect(formatearFecha('')).toBe('')
    expect(formatearFecha('2026-09-25T00:00:00Z')).toBe('')
  })
})
