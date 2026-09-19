import { z } from 'zod'

/**
 * Validación de formularios de autenticación.
 *
 * Deliberadamente **no** reproduce la política de complejidad del servidor (longitud mínima,
 * mayúscula, minúscula, dígito): esos umbrales son configurables y siguen pendientes de confirmación
 * de negocio (spec.md, Decisiones Pendientes #1). Duplicarlos aquí crearía una segunda fuente de
 * verdad que quedaría desfasada en silencio. El cliente comprueba lo que no depende de esa política
 * —campos presentes, confirmación coincidente— y muestra tal cual el `ProblemDetails` del servidor
 * cuando la política se incumple.
 */

export const esquemaLogin = z.object({
  correo: z.string().min(1, 'Indique su correo.').email('El correo no tiene un formato válido.'),
  password: z.string().min(1, 'Indique su contraseña.'),
})

export type FormularioLogin = z.infer<typeof esquemaLogin>

export const esquemaCambioPassword = z
  .object({
    passwordActual: z.string().min(1, 'Indique su contraseña actual.'),
    passwordNueva: z.string().min(1, 'Indique la contraseña nueva.'),
    confirmacion: z.string().min(1, 'Repita la contraseña nueva.'),
  })
  .refine((valores) => valores.passwordNueva === valores.confirmacion, {
    path: ['confirmacion'],
    message: 'Las contraseñas no coinciden.',
  })
  .refine((valores) => valores.passwordNueva !== valores.passwordActual, {
    path: ['passwordNueva'],
    message: 'La contraseña nueva debe ser distinta de la actual.',
  })

export type FormularioCambioPassword = z.infer<typeof esquemaCambioPassword>
