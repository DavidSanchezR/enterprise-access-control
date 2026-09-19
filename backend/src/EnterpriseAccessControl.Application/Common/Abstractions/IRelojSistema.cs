namespace EnterpriseAccessControl.Application.Common.Abstractions;

/// <summary>
/// Reloj del sistema en UTC. Todo timestamp persistido usa UTC (Constitución, Reglas de
/// Arquitectura e Ingeniería); la conversión a la zona horaria empresarial ocurre solo en
/// evaluación de reglas horarias y en presentación (ver <c>IRelojEmpresarial</c>).
/// </summary>
/// <remarks>Abstracción explícita para que las pruebas puedan fijar el instante evaluado.</remarks>
public interface IRelojSistema
{
    DateTime UtcNow { get; }
}
