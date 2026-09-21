using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Common.Abstractions;
using EnterpriseAccessControl.Application.Common.Errores;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.Application.People;

/// <summary>
/// Registro y búsqueda de personas (Historia 4, RF-012, RF-013, RF-035, RF-041).
/// </summary>
/// <remarks>
/// **Alcance (RF-035, RF-077)**: a diferencia de un área o una unidad organizativa, una persona no
/// tiene una compañía propietaria única, así que su Resource Ownership se resuelve por **unión**
/// (D3): está dentro del alcance si su compañía de pertenencia vigente está en el alcance **o** si
/// tiene al menos un contexto operativo vigente con una Compañía Principal del alcance. Sin esa
/// unión, el personal de contratista sería invisible para la Principal en cuyas instalaciones
/// trabaja, que es justo quien necesita administrarlo.
///
/// Una persona sin ninguna pertenencia ni contexto vigente no pertenece al alcance de nadie y no
/// aparece en las búsquedas: es la lectura por defecto-denegar del Principio I. Sí sigue siendo
/// registrable —el alta de la persona precede a su contratación— y visible para quien acaba de
/// crearla dentro de la misma operación.
/// </remarks>
public sealed class PersonaService(
    IAppDbContext db,
    IAlcanceCompaniaAccessor alcance,
    IRelojSistema reloj)
{
    public async Task<PaginaResponse<PersonaDto>> BuscarAsync(
        FiltroPersonas filtro,
        ParametrosPaginacion paginacion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacion);

        var companias = CompaniasConsultables(filtro.CompaniaId);

        if (companias is { Count: 0 })
        {
            // Filtrar por una compañía fuera del alcance no es un error: sencillamente no hay nada
            // que ver. Devolver 403 confirmaría que esa compañía existe.
            return new PaginaResponse<PersonaDto>(
                [], 0, paginacion.Pagina, paginacion.TamañoPagina);
        }

        var consulta = AplicarFiltrosDeTexto(PersonasEnAlcance(companias), filtro);

        var total = await consulta.CountAsync(ct).ConfigureAwait(false);

        var items = await consulta
            .OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres)
            .Skip(paginacion.Saltar)
            .Take(paginacion.TamañoPagina)
            .Select(p => AMapa(p))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PaginaResponse<PersonaDto>(items, total, paginacion.Pagina, paginacion.TamañoPagina);
    }

    public async Task<PersonaDto> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var persona = await PersonasEnAlcance(CompaniasConsultables(null))
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            .ConfigureAwait(false)
            ?? throw NoEncontrada();

        return AMapa(persona);
    }

    public async Task<PersonaDto> CrearAsync(PersonaRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var numero = request.NumeroDocumento.Trim();

        await ValidarDocumentoDisponibleAsync(request.TipoDocumentoId, numero, null, ct)
            .ConfigureAwait(false);

        await ValidarMaestrosActivosAsync(request, ct).ConfigureAwait(false);

        var persona = new Persona
        {
            Nombres = request.Nombres.Trim(),
            Apellidos = request.Apellidos.Trim(),
            FechaNacimiento = request.FechaNacimiento,
            TipoDocumentoId = request.TipoDocumentoId,
            NumeroDocumento = numero,
            GeneroId = request.GeneroId,
            CorreoElectronico = request.CorreoElectronico.Trim(),
            TipoSangreId = request.TipoSangreId,
            ContactoEmergencia = request.ContactoEmergencia.Trim(),
            NumeroEmergencia = request.NumeroEmergencia.Trim(),
        };

        db.Personas.Add(persona);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(persona);
    }

    public async Task<PersonaDto> ActualizarAsync(
        Guid id,
        PersonaRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Se resuelve dentro del alcance: actualizar una persona ajena debe ser indistinguible de
        // que no exista.
        var visible = await PersonasEnAlcance(CompaniasConsultables(null))
            .AnyAsync(p => p.Id == id, ct)
            .ConfigureAwait(false);

        if (!visible)
        {
            throw NoEncontrada();
        }

        var persona = await db.Personas.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false)
            ?? throw NoEncontrada();

        var numero = request.NumeroDocumento.Trim();

        await ValidarDocumentoDisponibleAsync(request.TipoDocumentoId, numero, id, ct)
            .ConfigureAwait(false);

        await ValidarMaestrosActivosAsync(request, ct).ConfigureAwait(false);

        persona.Nombres = request.Nombres.Trim();
        persona.Apellidos = request.Apellidos.Trim();
        persona.FechaNacimiento = request.FechaNacimiento;
        persona.TipoDocumentoId = request.TipoDocumentoId;
        persona.NumeroDocumento = numero;
        persona.GeneroId = request.GeneroId;
        persona.CorreoElectronico = request.CorreoElectronico.Trim();
        persona.TipoSangreId = request.TipoSangreId;
        persona.ContactoEmergencia = request.ContactoEmergencia.Trim();
        persona.NumeroEmergencia = request.NumeroEmergencia.Trim();

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AMapa(persona);
    }

    // --- Alcance ------------------------------------------------------------------------------

    /// <summary>
    /// Compañías sobre las que puede consultarse, intersecando el alcance del usuario con el filtro
    /// opcional de la petición.
    /// </summary>
    /// <remarks>
    /// <c>null</c> significa "sin restricción por compañía": es el alcance GLOBAL, que no se enumera
    /// (RF-074). Un <c>List</c> vacío, en cambio, significa "ninguna compañía consultable".
    /// </remarks>
    private List<Guid>? CompaniasConsultables(Guid? filtroCompaniaId)
    {
        if (alcance.EsGlobal)
        {
            return filtroCompaniaId is null ? null : [filtroCompaniaId.Value];
        }

        var enAlcance = alcance.CompaniaIds;

        if (filtroCompaniaId is null)
        {
            return [.. enAlcance];
        }

        return enAlcance.Contains(filtroCompaniaId.Value) ? [filtroCompaniaId.Value] : [];
    }

    /// <summary>
    /// Personas alcanzables por unión: pertenencia vigente **o** contexto operativo vigente con una
    /// Principal del alcance (RF-035, RF-077).
    /// </summary>
    private IQueryable<Persona> PersonasEnAlcance(List<Guid>? companias)
    {
        var consulta = db.Personas.AsNoTracking();

        if (companias is null)
        {
            // Alcance GLOBAL: toda persona es alcanzable, sin enumerar compañías.
            return consulta;
        }

        var ahora = reloj.UtcNow;

        // Se expresa como semi-join y no cargando pertenencias en memoria: con 100.000 personas,
        // resolverlo en el cliente no sostendría el objetivo de CS-002.
        return consulta.Where(p =>
            db.AsignacionesPersonaCompania.Any(a =>
                a.PersonaId == p.Id
                && companias.Contains(a.CompaniaId)
                && a.FechaHoraInicio <= ahora
                && ahora < a.FechaHoraFin)
            || db.ContextosOperativos.Any(c =>
                c.PersonaId == p.Id
                && companias.Contains(c.CompaniaPrincipalId)
                && c.FechaHoraInicio <= ahora
                && ahora < c.FechaHoraFin));
    }

    private static IQueryable<Persona> AplicarFiltrosDeTexto(
        IQueryable<Persona> consulta,
        FiltroPersonas filtro)
    {
        if (filtro.TipoDocumentoId is not null)
        {
            consulta = consulta.Where(p => p.TipoDocumentoId == filtro.TipoDocumentoId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            consulta = consulta.Where(p =>
                EF.Functions.Like(p.Nombres, $"%{texto}%") ||
                EF.Functions.Like(p.Apellidos, $"%{texto}%") ||
                EF.Functions.Like(p.NumeroDocumento, $"%{texto}%"));
        }

        return consulta;
    }

    /// <summary>
    /// Exige que la persona sea alcanzable por el usuario para operar sobre su <em>histórico</em>.
    /// </summary>
    /// <remarks>
    /// **Por qué difiere de la búsqueda.** <see cref="BuscarAsync"/> y <see cref="ObtenerAsync"/>
    /// muestran a una persona solo si su pertenencia está <em>vigente</em> en una compañía del
    /// alcance (RF-035), y eso no cambia: quien no tiene pertenencia vigente no pertenece hoy al
    /// alcance de nadie y no aparece en ningún listado ni ficha.
    ///
    /// Aplicar ese mismo criterio al histórico lo vuelve inutilizable justo donde más se necesita:
    /// <list type="bullet">
    ///   <item>Al escribir, la operación cuyo propósito es devolverle una pertenencia vigente
    ///   exigiría que ya tuviera una — la persona recién registrada y la que vuelve tras un tiempo
    ///   quedarían inalcanzables.</item>
    ///   <item>Al leer, RF-037 existe para reconstruir la situación de una persona en una fecha
    ///   dada, que es sobre todo lo que se consulta de quien ya no está activo.</item>
    /// </list>
    ///
    /// La regla aquí es temporalmente más amplia pero **igual de restrictiva en cuanto a alcance**:
    /// basta con que la persona haya pertenecido —ahora o antes— a alguna compañía del alcance. Una
    /// persona cuyo histórico está íntegramente en compañías ajenas sigue devolviendo 404.
    /// </remarks>
    public async Task ExigirAlcanceHistoricoAsync(Guid personaId, CancellationToken ct = default)
    {
        var existe = await db.Personas.AnyAsync(p => p.Id == personaId, ct).ConfigureAwait(false);

        if (!existe)
        {
            throw NoEncontrada();
        }

        if (alcance.EsGlobal)
        {
            return;
        }

        var historial = await db.AsignacionesPersonaCompania
            .AsNoTracking()
            .Where(a => a.PersonaId == personaId)
            .Select(a => a.CompaniaId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // La unión de D3 también rige en el histórico: el personal de contratista debe seguir siendo
        // administrable por la Principal en la que tuvo contexto operativo (RF-077).
        var principales = await db.ContextosOperativos
            .AsNoTracking()
            .Where(c => c.PersonaId == personaId)
            .Select(c => c.CompaniaPrincipalId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Sin historial la persona todavía no es de nadie: cualquier administrador puede darle su
        // primera pertenencia. Es el caso del alta seguida de contratación.
        if (historial.Count == 0 && principales.Count == 0)
        {
            return;
        }

        if (!historial.Exists(alcance.EstaEnAlcance) && !principales.Exists(alcance.EstaEnAlcance))
        {
            throw NoEncontrada();
        }
    }

    // --- Validaciones -------------------------------------------------------------------------

    private async Task ValidarDocumentoDisponibleAsync(
        Guid tipoDocumentoId,
        string numeroDocumento,
        Guid? excluyendo,
        CancellationToken ct)
    {
        // La consulta NO se limita al alcance: la unicidad del documento es global (RF-041). Si se
        // filtrara por alcance, dos administradores de compañías distintas podrían crear la misma
        // persona dos veces y partir su histórico de acceso.
        var duplicada = await db.Personas
            .AnyAsync(
                p => p.TipoDocumentoId == tipoDocumentoId
                     && p.NumeroDocumento == numeroDocumento
                     && (excluyendo == null || p.Id != excluyendo),
                ct)
            .ConfigureAwait(false);

        if (duplicada)
        {
            throw new ConflictoEstadoException(
                "DOCUMENTO_YA_REGISTRADO",
                "Ya existe una persona con ese tipo y número de documento.");
        }
    }

    /// <summary>
    /// Los tres valores de catálogo deben existir y estar ACTIVOS al registrar (RF-032).
    /// </summary>
    private async Task ValidarMaestrosActivosAsync(PersonaRequest request, CancellationToken ct)
    {
        await ExigirActivoAsync<TipoDocumento>(request.TipoDocumentoId, "tipo de documento", ct)
            .ConfigureAwait(false);

        await ExigirActivoAsync<Genero>(request.GeneroId, "género", ct).ConfigureAwait(false);

        await ExigirActivoAsync<TipoSangre>(request.TipoSangreId, "tipo de sangre", ct)
            .ConfigureAwait(false);
    }

    private async Task ExigirActivoAsync<TMaestro>(Guid id, string descripcion, CancellationToken ct)
        where TMaestro : Domain.Common.EntidadMaestra
    {
        var activo = await db.Maestro<TMaestro>()
            .AnyAsync(m => m.Id == id && m.Estado == Estado.ACTIVO, ct)
            .ConfigureAwait(false);

        if (!activo)
        {
            throw new ReglaNegocioInvalidaException(
                CodigosError.ValorMaestroInactivo,
                $"El {descripcion} indicado no existe o está inactivo.");
        }
    }

    private static PersonaDto AMapa(Persona p) =>
        new(
            p.Id,
            p.Nombres,
            p.Apellidos,
            p.FechaNacimiento,
            p.TipoDocumentoId,
            p.NumeroDocumento,
            p.GeneroId,
            p.CorreoElectronico,
            p.TipoSangreId,
            p.ContactoEmergencia,
            p.NumeroEmergencia);

    private static RecursoNoEncontradoException NoEncontrada() =>
        new(CodigosError.RecursoNoEncontrado, "Persona no encontrada.");
}
