namespace EnterpriseAccessControl.Domain.Enums;

/// <summary>
/// Clasificación obligatoria de toda compañía (RF-042).
/// </summary>
/// <remarks>
/// Distinción central del dominio: una Compañía Principal/Mandante es dueña de la operación —
/// posee unidades organizativas y áreas de acceso—, mientras que una Contratista presta servicios
/// dentro de las instalaciones de una o varias Principales y no posee estructura organizativa
/// propia (RF-045, RF-046).
/// </remarks>
public enum TipoCompania
{
    PRINCIPAL_MANDANTE = 1,
    CONTRATISTA = 2,
}
