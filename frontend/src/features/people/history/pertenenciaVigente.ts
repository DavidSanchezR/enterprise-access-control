import type { AsignacionCompania } from './api'

/**
 * Pertenencia vigente de la persona en el instante indicado, o `undefined` si no tiene ninguna.
 *
 * Se decide por fechas y nunca por `estado`: una pertenencia `ACTIVA` cuya fecha de fin ya pasó no está
 * vigente (Principio IV), igual que en el servidor.
 *
 * Solo orienta la interfaz (límites de fecha y avisos de RF-082, cambio post-Baseline VF-007). La
 * contención la decide siempre el backend: el cliente no es una frontera de seguridad (Principio I).
 */
export function pertenenciaVigente(
  historial: readonly AsignacionCompania[] | undefined,
  ahora: number = Date.now(),
): AsignacionCompania | undefined {
  return historial?.find(
    (pertenencia) =>
      Date.parse(pertenencia.fechaHoraInicio) <= ahora &&
      ahora < Date.parse(pertenencia.fechaHoraFin),
  )
}
