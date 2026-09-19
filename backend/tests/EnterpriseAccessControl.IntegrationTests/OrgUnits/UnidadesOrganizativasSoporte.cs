using System.Net.Http.Json;
using EnterpriseAccessControl.Application.OrgUnits;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;

namespace EnterpriseAccessControl.IntegrationTests.OrgUnits;

/// <summary>
/// Utilidades compartidas por las pruebas de unidades organizativas.
/// </summary>
/// <remarks>
/// Las cuatro clases de esta carpeta parten del mismo escenario —una Compañía Principal en alcance
/// y un árbol sobre ella—, así que la construcción vive aquí en lugar de repetirse. Cada prueba
/// siembra sus propias compañías y su propio usuario: comparten la base de datos del contenedor,
/// no los datos.
/// </remarks>
internal static class UnidadesOrganizativasSoporte
{
    public const string Password = "Contrasena1Segura";

    public static string CorreoUnico(string prefijo) =>
        $"{prefijo}.{Guid.CreateVersion7():N}@empresa.cl";

    /// <summary>Cliente autenticado cuyo alcance cubre exactamente las compañías indicadas.</summary>
    public static async Task<HttpClient> ClienteConAlcanceAsync(
        this ApiFactory api,
        params Guid[] companiaIds)
    {
        var admin = await api.SembrarUsuarioAsync(
            CorreoUnico("admin"),
            Password,
            alcanceCompanias: companiaIds);

        return await api.CrearClienteAutenticadoAsync(admin.Id, admin.Correo);
    }

    /// <summary>Crea un nodo raíz vía API y devuelve el DTO resultante.</summary>
    public static async Task<UnidadOrganizativaDto> CrearRaizAsync(
        this HttpClient cliente,
        string nombre,
        Guid companiaPrincipalId)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/unidades-organizativas", UriKind.Relative),
            new UnidadOrganizativaRequest(nombre, Estado.ACTIVO, null, companiaPrincipalId),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<UnidadOrganizativaDto>(ApiFactory.Json))!;
    }

    /// <summary>Crea un nodo hijo vía API y devuelve el DTO resultante.</summary>
    public static async Task<UnidadOrganizativaDto> CrearHijaAsync(
        this HttpClient cliente,
        string nombre,
        Guid unidadSuperiorId)
    {
        using var respuesta = await cliente.PostAsJsonAsync(
            new Uri("/api/unidades-organizativas", UriKind.Relative),
            new UnidadOrganizativaRequest(nombre, Estado.ACTIVO, unidadSuperiorId),
            ApiFactory.Json);

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<UnidadOrganizativaDto>(ApiFactory.Json))!;
    }

    public static Task<HttpResponseMessage> MoverAsync(
        this HttpClient cliente,
        Guid unidadId,
        Guid? nuevoPadreId) =>
        cliente.PostAsJsonAsync(
            new Uri($"/api/unidades-organizativas/{unidadId}/mover", UriKind.Relative),
            new MoverUnidadRequest(nuevoPadreId),
            ApiFactory.Json);

    public static async Task<IReadOnlyList<NodoArbolDto>> ArbolAsync(
        this HttpClient cliente,
        Guid companiaPrincipalId) =>
        await cliente.GetFromJsonAsync<IReadOnlyList<NodoArbolDto>>(
            new Uri($"/api/unidades-organizativas/arbol?companiaPrincipalId={companiaPrincipalId}",
                UriKind.Relative),
            ApiFactory.Json) ?? [];

    /// <summary>Aplana un árbol anidado a la lista de identificadores que contiene.</summary>
    public static IEnumerable<Guid> Aplanar(this IEnumerable<NodoArbolDto> nodos)
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
