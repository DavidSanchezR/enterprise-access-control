using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Infrastructure.Persistence;

/// <summary>
/// Convenciones globales del modelo relacional (research.md §17 y §5).
/// </summary>
public static class ModelConventions
{
    /// <summary>Longitud fija para columnas de enum persistidas como texto.</summary>
    private const int LongitudEnum = 40;

    /// <summary>
    /// Tipo de columna de toda fecha/hora del modelo. Precisión de milisegundos, suficiente para
    /// vigencias de negocio y comparaciones de solapamiento, y persistida siempre en UTC
    /// (Constitución, Reglas de Arquitectura e Ingeniería).
    /// </summary>
    private const string TipoColumnaFechaHora = "datetime2(3)";

    public static void AplicarConvencionesGlobales(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (clrType.IsEnum)
                {
                    // Los enums de negocio se guardan como texto legible, no como entero: el valor
                    // es autodescriptivo en consultas directas y no se corrompe si el orden de los
                    // miembros del enum cambia en el código (research.md §17).
                    property.SetProviderClrType(typeof(string));
                    property.SetMaxLength(LongitudEnum);
                    property.SetIsUnicode(true);
                }
                else if (clrType == typeof(DateTime))
                {
                    property.SetColumnType(TipoColumnaFechaHora);
                }
            }
        }
    }
}
