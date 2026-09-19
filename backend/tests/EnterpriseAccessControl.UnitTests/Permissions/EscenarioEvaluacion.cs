using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Domain.Services;

namespace EnterpriseAccessControl.UnitTests.Permissions;

/// <summary>
/// Construye el estado de una evaluación que concede, para que cada prueba rompa exactamente un paso.
/// </summary>
/// <remarks>
/// El evaluador tiene 14 cortes y casi todos comparten los mismos datos de partida. Definir una vez
/// el caso que concede y derivar cada denegación de él hace que la prueba diga qué paso está
/// ejercitando en lugar de enterrarlo en veinte líneas de montaje repetido.
/// </remarks>
internal static class EscenarioEvaluacion
{
    /// <summary>Martes 15 de septiembre de 2026, 14:00 UTC = 09:00 en America/Lima (UTC-5).</summary>
    public static readonly DateTime Instante = new(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc);

    public static readonly Guid PersonaId = Guid.CreateVersion7();
    public static readonly Guid PrincipalId = Guid.CreateVersion7();
    public static readonly Guid UnidadId = Guid.CreateVersion7();
    public static readonly Guid TipoPersonaId = Guid.CreateVersion7();

    /// <summary>Reloj fijo en America/Lima, sin NodaTime ni configuración: el corte es determinista.</summary>
    public sealed class RelojLima : IRelojEmpresarial
    {
        public DayOfWeek DiaSemanaLocal(DateTime instanteUtc) =>
            instanteUtc.AddHours(-5).DayOfWeek;

        public TimeOnly HoraLocal(DateTime instanteUtc) =>
            TimeOnly.FromDateTime(instanteUtc.AddHours(-5));
    }

    public static EvaluadorDeAcceso Evaluador() => new(new RelojLima());

    public static AreaAcceso Area(Estado estado = Estado.ACTIVO) => new()
    {
        Nombre = "Planta Concentradora",
        CompaniaPrincipalId = PrincipalId,
        Estado = estado,
    };

    public static Compania Principal() => new()
    {
        Nombre = "Minera Principal",
        TipoDocumentoId = Guid.CreateVersion7(),
        NumeroDocumento = "20100000001",
        TipoCompania = TipoCompania.PRINCIPAL_MANDANTE,
    };

    public static Compania Contratista() => new()
    {
        Nombre = "Servicios Contratista",
        TipoDocumentoId = Guid.CreateVersion7(),
        NumeroDocumento = "20100000002",
        TipoCompania = TipoCompania.CONTRATISTA,
    };

    public static ContextoOperativoPersonaPrincipal Contexto(
        DateTime? inicio = null,
        DateTime? fin = null) => new()
    {
        PersonaId = PersonaId,
        CompaniaPrincipalId = PrincipalId,
        FechaHoraInicio = inicio ?? Instante.AddYears(-1),
        FechaHoraFin = fin ?? Instante.AddYears(1),
    };

    public static AsignacionCredencial Credencial(
        EstadoCredencial estado = EstadoCredencial.ASIGNADO,
        DateTime? fin = null) => new()
    {
        PersonaId = PersonaId,
        CompaniaPrincipalId = PrincipalId,
        TipoCredencialId = Guid.CreateVersion7(),
        FechaHoraInicio = Instante.AddMonths(-1),
        FechaHoraFin = fin ?? Instante.AddMonths(1),
        Estado = estado,
    };

    public static PermisoConBloques Permiso(
        AlcancePermiso alcance,
        Guid? sujetoId = null,
        DateTime? inicio = null,
        DateTime? fin = null,
        Estado estado = Estado.ACTIVO,
        DiaSemana dia = DiaSemana.MARTES,
        string horaInicio = "08:00",
        string horaFin = "17:00")
    {
        var permiso = new PermisoAcceso
        {
            AreaAccesoId = Guid.CreateVersion7(),
            Alcance = alcance,
            PersonaId = alcance == AlcancePermiso.PERSONA ? sujetoId ?? PersonaId : null,
            UnidadOrganizativaId = alcance == AlcancePermiso.UNIDAD_ORGANIZATIVA
                ? sujetoId ?? UnidadId
                : null,
            CompaniaId = alcance == AlcancePermiso.COMPANIA ? sujetoId : null,
            FechaHoraInicioVigencia = inicio ?? Instante.AddYears(-1),
            FechaHoraFinVigencia = fin ?? Instante.AddYears(1),
            Estado = estado,
        };

        var bloque = new BloqueHorarioPermiso
        {
            PermisoAccesoId = permiso.Id,
            DiaSemana = dia,
            HoraInicio = TimeOnly.Parse(horaInicio, System.Globalization.CultureInfo.InvariantCulture),
            HoraFin = TimeOnly.Parse(horaFin, System.Globalization.CultureInfo.InvariantCulture),
        };

        return new PermisoConBloques(permiso, [bloque]);
    }

    /// <summary>Estado de partida que concede; cada prueba altera solo lo que quiere romper.</summary>
    public static DatosDeEvaluacion Concede(Compania? pertenencia = null) => new()
    {
        FechaHoraUtc = Instante,
        PersonaId = PersonaId,
        Area = Area(),
        UsuarioTieneAlcanceSobrePrincipal = true,
        ContextoOperativo = Contexto(),
        CompaniaPertenencia = pertenencia ?? PrincipalComoPertenencia(),
        RelacionContratistaPrincipalVigente = false,
        Credencial = Credencial(),
        TiposPersonaVigentes = [TipoPersonaId],
        TiposPersonaAutorizadosEnArea = [TipoPersonaId],
        UnidadOrganizativaVigenteId = UnidadId,
        PermisosDelArea = [Permiso(AlcancePermiso.PERSONA)],
    };

    /// <summary>
    /// La Principal evaluada, usada como compañía de pertenencia de la persona.
    /// </summary>
    /// <remarks>
    /// El identificador debe ser exactamente <see cref="PrincipalId"/>: el paso 5 concede legitimidad
    /// automática solo cuando la persona pertenece a la propia Principal evaluada (RF-053).
    /// </remarks>
    public static Compania PrincipalComoPertenencia()
    {
        var compania = Principal();
        ForzarId(compania, PrincipalId);
        return compania;
    }

    /// <summary>
    /// Fija el identificador de una entidad recién construida.
    /// </summary>
    /// <remarks>
    /// <c>EntidadBase.Id</c> se genera en el constructor y su setter es privado, precisamente para que
    /// el código de producción no pueda reasignarlo. Aquí hace falta que la compañía de pertenencia
    /// tenga el mismo identificador que la Principal del área, así que se fija por reflexión y solo
    /// dentro de las pruebas.
    /// </remarks>
    public static void ForzarId(EntidadBase entidad, Guid id) =>
        typeof(EntidadBase)
            .GetProperty(nameof(EntidadBase.Id))!
            .SetValue(entidad, id);
}
