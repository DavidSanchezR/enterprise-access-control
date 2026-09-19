using System.Net.Http.Json;
using EnterpriseAccessControl.Application.AreaAccess;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;

namespace EnterpriseAccessControl.IntegrationTests.AreaAccess;

/// <summary>
/// Utilidades compartidas por las pruebas del árbol de áreas de acceso.
/// </summary>
/// <remarks>
/// Refleja a propósito la forma de <c>UnidadesOrganizativasSoporte</c>: las dos jerarquías se operan
/// igual desde fuera, y que las pruebas se lean en paralelo hace evidente dónde difieren de verdad
/// —la pertenencia a la Principal, que aquí viaja en el propio nodo (RF-046)—.
/// </remarks>
internal static class AreasAccesoSoporte
{
    public const string Password = "Contrasena1Segura";

    public static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    /// <summary>Cliente autenticado cuyo alcance cubre exactamente las compañías indicadas.</summary>
    public static async Task<HttpClient> ClienteDeAreasAsync(
        this ApiFactory api,
        params Guid[] companiaIds)
    {
        var admin = await api.SembrarUsuarioAsync(
            CorreoUnico("areas"),
            Password,
            alcanceCompanias: companiaIds);

        return await api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    /// <summary>Envía la creación de un área raíz sin exigir que tenga éxito.</summary>
    public static Task<HttpResponseMessage> PostRaizAsync(
        this HttpClient cliente,
        string nombre,
        Guid companiaPrincipalId) =>
        cliente.PostAsJsonAsync(
            new Uri("/api/areas-acceso", UriKind.Relative),
            new AreaAccesoRequest(nombre, Estado.ACTIVO, null, companiaPrincipalId),
            ApiFactory.Json);

    /// <summary>Crea un área raíz vía API y devuelve el DTO resultante.</summary>
    public static async Task<AreaAccesoDto> CrearRaizAsync(
        this HttpClient cliente,
        string nombre,
        Guid companiaPrincipalId)
    {
        using var respuesta = await cliente.PostRaizAsync(nombre, companiaPrincipalId);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<AreaAccesoDto>(ApiFactory.Json))!;
    }

    /// <summary>Crea un área hija vía API y devuelve el DTO resultante.</summary>
    public static async Task<AreaAccesoDto> CrearHijaAsync(
        this HttpClient cliente,
        string nombre,
        Guid areaSuperiorId)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/areas-acceso", UriKind.Relative),
            new AreaAccesoRequest(nombre, Estado.ACTIVO, areaSuperiorId),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<AreaAccesoDto>(ApiFactory.Json))!;
    }

    public static Task<HttpResponseMessage> MoverAreaAsync(
        this HttpClient cliente,
        Guid areaId,
        Guid? nuevoPadreId) =>
        cliente.PostAsJsonAsync(
            new Uri($"/api/areas-acceso/{areaId}/mover", UriKind.Relative),
            new MoverAreaRequest(nuevoPadreId),
            ApiFactory.Json);

    public static async Task<AreaAccesoDto> ObtenerAreaAsync(this HttpClient cliente, Guid areaId) =>
        (await cliente.GetFromJsonAsync<AreaAccesoDto>(
            new Uri($"/api/areas-acceso/{areaId}", UriKind.Relative),
            ApiFactory.Json))!;

    public static async Task<IReadOnlyList<NodoAreaDto>> ArbolAreasAsync(
        this HttpClient cliente,
        Guid companiaPrincipalId) =>
        await cliente.GetFromJsonAsync<IReadOnlyList<NodoAreaDto>>(
            new Uri($"/api/areas-acceso/arbol?companiaPrincipalId={companiaPrincipalId}",
                UriKind.Relative),
            ApiFactory.Json) ?? [];

    public static async Task<IReadOnlyList<AreaAccesoDto>> ListarAreasAsync(
        this HttpClient cliente,
        Guid companiaPrincipalId,
        Estado? estado = null) =>
        await cliente.GetFromJsonAsync<IReadOnlyList<AreaAccesoDto>>(
            new Uri(
                $"/api/areas-acceso?companiaPrincipalId={companiaPrincipalId}"
                + (estado is null ? string.Empty : $"&estado={estado}"),
                UriKind.Relative),
            ApiFactory.Json) ?? [];

    public static async Task<IReadOnlyList<Guid>> TiposPersonaDeAsync(
        this HttpClient cliente,
        Guid areaId) =>
        await cliente.GetFromJsonAsync<IReadOnlyList<Guid>>(
            new Uri($"/api/areas-acceso/{areaId}/tipos-persona", UriKind.Relative),
            ApiFactory.Json) ?? [];

    public static Task<HttpResponseMessage> ReemplazarTiposPersonaAsync(
        this HttpClient cliente,
        Guid areaId,
        params Guid[] tipoPersonaIds) =>
        cliente.PutAsJsonAsync(
            new Uri($"/api/areas-acceso/{areaId}/tipos-persona", UriKind.Relative),
            new ReemplazarTiposPersonaRequest(tipoPersonaIds),
            ApiFactory.Json);

    /// <summary>Inserta un tipo de persona propio de la prueba (el catálogo no trae semilla, RF-010).</summary>
    public static async Task<Guid> SembrarTipoPersonaAsync(
        this ApiFactory api,
        string? nombre = null,
        Estado estado = Estado.ACTIVO)
    {
        var id = Guid.Empty;

        await api.ConDbContextAsync(async db =>
        {
            var tipo = new TipoPersona
            {
                Nombre = nombre ?? $"Perfil {Guid.CreateVersion7():N}"[..20],
                Estado = estado,
            };

            db.Set<TipoPersona>().Add(tipo);
            await db.SaveChangesAsync();
            id = tipo.Id;
        });

        return id;
    }

    /// <summary>Aplana un árbol anidado a la lista de identificadores que contiene.</summary>
    public static IEnumerable<Guid> Aplanar(this IEnumerable<NodoAreaDto> nodos)
    {
        foreach (var nodo in nodos)
        {
            yield return nodo.Id;

            foreach (var descendiente in nodo.Hijos.Aplanar())
            {
                yield return descendiente;
            }
        }
    }
}
