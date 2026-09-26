using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Application.Permissions;

/// <summary>
/// Cuándo una escritura de <c>PermisoAcceso</c> queda sujeta a la contención temporal de RF-082.
/// </summary>
/// <remarks>
/// Cambio de requisito post-Baseline VF-007 (research.md §35.2). La contención solo tiene sentido
/// cuando la operación <em>concede o amplía</em> acceso a una persona concreta: un permiso de alcance
/// PERSONA que queda <c>ACTIVO</c> y además es nuevo, cambia su vigencia o se reactiva. Cambiar solo los
/// bloques horarios, desactivarlo o dejarlo <c>INACTIVO</c> nunca amplía el acceso, y por eso no
/// consulta la pertenencia: así un permiso anterior a RF-082 que excede la pertenencia sigue pudiendo
/// desactivarse o reprogramarse (CS-043), porque reducir acceso nunca debe quedar bloqueado.
///
/// Los alcances UNIDAD_ORGANIZATIVA y COMPANIA no tienen una persona cuya pertenencia sirva de
/// límite, así que nunca se contienen (D2).
///
/// Es una función pura, sin acceso a datos: la matriz completa de D4 se verifica con una prueba
/// unitaria, y la contención en sí sigue siendo exclusiva de <c>ContencionTemporalValidator</c>.
///
/// Desde VF-004 (RF-083), "cambian las fechas" significa que cambia la fecha civil de algún extremo. Lo
/// decide <see cref="VigenciaDiariaPermiso.ResolverExtremo"/>. La comparación de instantes con tolerancia de
/// 1 ms que había aquí (research.md §35.3, introducida por T243) se retiró en T292: con fechas civiles, reenviar
/// sin cambios significa reenviar la misma fecha.
/// </remarks>
public static class ReglaContencionPermiso
{
    /// <summary>
    /// Indica si la escritura debe validar la contención respecto a la pertenencia de la persona.
    /// </summary>
    /// <param name="alcance">Alcance del permiso; no es editable, así que es el mismo antes y después.</param>
    /// <param name="estadoPrevio"><c>null</c> en un alta; el estado almacenado en una actualización.</param>
    /// <param name="estadoResultante">Estado con el que el permiso queda tras la escritura.</param>
    /// <param name="fechasCambian">Si la escritura modifica la vigencia almacenada.</param>
    public static bool RequiereContencion(
        AlcancePermiso alcance,
        Estado? estadoPrevio,
        Estado estadoResultante,
        bool fechasCambian) =>
        alcance == AlcancePermiso.PERSONA
        && estadoResultante == Estado.ACTIVO
        && (estadoPrevio is null || fechasCambian || estadoPrevio == Estado.INACTIVO);
}
