namespace EnterpriseAccessControl.Application.Common;

/// <summary>Interpretación de las fechas recibidas como instantes UTC.</summary>
public static class InstanteUtc
{
    /// <summary>
    /// Interpreta como UTC una fecha sin zona declarada.
    /// </summary>
    /// <remarks>
    /// Todo timestamp del sistema se persiste en UTC. Un valor con <c>Kind</c> local se convierte, y uno
    /// sin especificar se asume ya en UTC: es lo que envía un cliente que serializa con sufijo <c>Z</c>,
    /// y suponer lo contrario desplazaría la vigencia varias horas en silencio.
    /// </remarks>
    public static DateTime Desde(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Utc => valor,
        DateTimeKind.Local => valor.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc),
    };
}
