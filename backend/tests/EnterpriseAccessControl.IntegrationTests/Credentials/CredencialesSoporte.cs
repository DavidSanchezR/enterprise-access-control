using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Credentials;

/// <summary>
/// Utilidades compartidas por las pruebas de mantenimiento de credenciales (Historia 9).
/// </summary>
/// <remarks>
/// Se apoyan en <see cref="EscenarioUs5"/>: una persona de la Contratista con contextos operativos
/// vigentes con las Principales A y B, que es lo que habilita credenciales simultáneas para ambas
/// (CS-016, CS-017).
/// </remarks>
internal static class CredencialesSoporte
{
    public static async Task<EscenarioUs5> MontarConContextosAsync(SqlServerFixture fixture)
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();

        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);
        await escenario.ContextoAsync(escenario.PrincipalB.Id);

        return escenario;
    }

    public static Task<HttpResponseMessage> DevolverAsync(
        this EscenarioUs5 escenario,
        Guid credencialId,
        HttpClient? cliente = null) =>
        (cliente ?? escenario.Cliente).PostAsync(
            escenario.Ruta($"credenciales/{credencialId}/devolver"),
            content: null);

    public static Task<HttpResponseMessage> EliminarAsync(
        this EscenarioUs5 escenario,
        Guid credencialId,
        HttpClient? cliente = null) =>
        (cliente ?? escenario.Cliente).DeleteAsync(escenario.Ruta($"credenciales/{credencialId}"));

    public static async Task<IReadOnlyList<AsignacionCredencialDto>> ListarCredencialesAsync(
        this EscenarioUs5 escenario,
        Guid? companiaPrincipalId = null) =>
        await escenario.Cliente.GetFromJsonAsync<IReadOnlyList<AsignacionCredencialDto>>(
            escenario.Ruta(companiaPrincipalId is null
                ? "credenciales"
                : $"credenciales?companiaPrincipalId={companiaPrincipalId}"),
            ApiFactory.Json) ?? [];

    public static async Task<AsignacionCredencial> LeerCredencialAsync(
        this EscenarioUs5 escenario,
        Guid credencialId)
    {
        AsignacionCredencial? credencial = null;

        await escenario.ConDatosAsync(async db =>
            credencial = await db.Set<AsignacionCredencial>().AsNoTracking()
                .SingleAsync(c => c.Id == credencialId));

        return credencial!;
    }

    public static Task FijarEstadoAsync(
        this EscenarioUs5 escenario,
        Guid credencialId,
        EstadoCredencial estado) =>
        escenario.ConDatosAsync(async db =>
        {
            var credencial = await db.Set<AsignacionCredencial>().SingleAsync(c => c.Id == credencialId);
            credencial.Estado = estado;
            await db.SaveChangesAsync();
        });
}
