using System.Net;
using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Escenario completo con las tres asociaciones dependientes, para las pruebas de cascada.
/// </summary>
/// <remarks>
/// Las pruebas T092 a T098 parten todas del mismo montaje: una persona de una contratista con
/// contextos hacia una o dos Principales, cada uno con su unidad organizativa y su credencial. Se
/// construye aquí para que cada prueba se lea por lo que verifica y no por cómo se prepara.
/// </remarks>
internal static class CascadaSoporte
{
    /// <summary>Contexto operativo con su unidad y su credencial, todo vigente.</summary>
    internal sealed record Dependientes(
        ContextoOperativoDto Contexto,
        Guid UnidadOrganizativaId,
        AsignacionCredencialDto Credencial);

    /// <summary>
    /// Monta una persona con pertenencia vigente y un juego completo de dependientes por cada
    /// Principal indicada.
    /// </summary>
    public static async Task<(EscenarioUs5 Escenario, AsignacionCompaniaDto Pertenencia,
        List<Dependientes> Dependientes)> MontarConDependientesAsync(
        SqlServerFixture fixture,
        bool ambasPrincipales = true)
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();

        var pertenencia = await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        var principales = ambasPrincipales
            ? new[] { escenario.PrincipalA.Id, escenario.PrincipalB.Id }
            : [escenario.PrincipalA.Id];

        var dependientes = new List<Dependientes>();

        foreach (var principalId in principales)
        {
            dependientes.Add(await CrearDependientesAsync(escenario, principalId));
        }

        return (escenario, pertenencia, dependientes);
    }

    public static async Task<Dependientes> CrearDependientesAsync(
        EscenarioUs5 escenario,
        Guid principalId)
    {
        var contexto = await escenario.ContextoAsync(principalId);
        var unidad = await escenario.SembrarUnidadAsync(principalId);

        using (var asignada = await escenario.AsignarUnidadAsync(contexto.Id, unidad))
        {
            asignada.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var credencial = await escenario.AsignarCredencialAsync(principalId);

        return new Dependientes(contexto, unidad, credencial);
    }
}
