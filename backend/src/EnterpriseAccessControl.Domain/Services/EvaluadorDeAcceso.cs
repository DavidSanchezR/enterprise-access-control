using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Services;

/// <summary>
/// Motor de evaluación de acceso: los 14 pasos ordenados de research.md §7
/// (Historia 8, RF-023 a RF-025, RF-059, RF-065, RF-066).
/// </summary>
/// <remarks>
/// **Servicio de dominio puro, deliberadamente no un <c>AuthorizationPolicy</c> de ASP.NET Core**
/// (research.md §18). Lo que se decide aquí no es si un usuario puede llamar a un endpoint, sino si
/// una persona —que puede no ser el usuario— puede entrar a un área física en un instante dado. Es
/// una regla de negocio del dominio, con su propio histórico y su propia auditoría; atarla a la
/// infraestructura de autorización HTTP la haría inevaluable fuera de una petición web.
///
/// Dos propiedades sostienen el resto del diseño:
///
/// - **Denegación por defecto** (Principio I): todo paso sin resultado inequívoco deniega. No hay
///   ninguna rama que conceda por omisión.
/// - **Orden significativo**: la Principal propietaria del área se determina antes de mirar ningún
///   permiso, de modo que un permiso de la Principal B no puede satisfacer una evaluación sobre un
///   área de la Principal A (CS-018) — los permisos ni siquiera se consultan hasta que el contexto
///   operativo y la credencial con la Principal correcta están confirmados.
///
/// El paso 1 (alcance del usuario que consulta) se enuncia primero en research.md §7 pero depende de
/// la Principal que determina el paso 4, así que se comprueba en cuanto esa Principal se conoce.
/// </remarks>
public sealed class EvaluadorDeAcceso(IRelojEmpresarial reloj)
{
    public ResultadoDeEvaluacion Evaluar(DatosDeEvaluacion datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // Paso 2: identificar a la persona evaluada.
        if (datos.PersonaId is null)
        {
            return Denegar(MotivoDenegacion.PERSONA_NO_ENCONTRADA);
        }

        // Pasos 3 y 4: identificar el área y la Compañía Principal propietaria.
        if (datos.Area is null)
        {
            return Denegar(MotivoDenegacion.AREA_NO_ENCONTRADA);
        }

        var principalId = datos.Area.CompaniaPrincipalId;

        // Paso 1: el usuario que consulta debe tener esa Principal en su alcance (RF-005, RF-049).
        if (!datos.UsuarioTieneAlcanceSobrePrincipal)
        {
            return Denegar(MotivoDenegacion.FUERA_DE_ALCANCE_USUARIO, principalId);
        }

        // Paso 5: contexto operativo vigente con esa Principal, y legitimidad re-derivada.
        var contexto = datos.ContextoOperativo;

        if (contexto is null || !contexto.EstaVigenteEn(datos.FechaHoraUtc))
        {
            return Denegar(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE, principalId);
        }

        var motivoIlegitimidad = EvaluarLegitimidad(datos, principalId);
        if (motivoIlegitimidad is not null)
        {
            return Denegar(motivoIlegitimidad.Value, principalId, contexto.Id);
        }

        // Paso 6: credencial vigente para esa misma Principal (RF-066, RF-070, RF-071).
        // Los cuatro casos —nunca asignada, DEVUELTO/ELIMINADO/REVOCADA, o ASIGNADO ya expirada—
        // se reportan con el mismo motivo, y ninguno modifica el estado de la credencial.
        if (datos.Credencial is null || !datos.Credencial.EstaVigenteEn(datos.FechaHoraUtc))
        {
            return Denegar(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE, principalId, contexto.Id);
        }

        // Paso 7: el área debe estar activa.
        if (datos.Area.Estado != Estado.ACTIVO)
        {
            return Denegar(MotivoDenegacion.AREA_INACTIVA, principalId, contexto.Id);
        }

        // Paso 8: algún perfil vigente de la persona debe estar autorizado en el área (RF-024).
        if (!datos.TiposPersonaVigentes.Any(datos.TiposPersonaAutorizadosEnArea.Contains))
        {
            return Denegar(MotivoDenegacion.PERFIL_NO_AUTORIZADO_EN_AREA, principalId, contexto.Id);
        }

        // Pasos 9 y 10: recolectar los permisos aplicables en los tres niveles.
        var aplicables = datos.PermisosDelArea
            .Where(p => EsAplicableAlSujeto(p.Permiso, datos))
            .ToList();

        if (aplicables.Count == 0)
        {
            return Denegar(MotivoDenegacion.SIN_PERMISO_APLICABLE, principalId, contexto.Id);
        }

        // Paso 11: filtrar por vigencia del permiso en la fecha evaluada.
        // Se exige además Estado ACTIVO: el estado es una baja lógica, y un permiso dado de baja que
        // siguiera concediendo acceso contradiría el propósito de darlo de baja. La vigencia en sí
        // se sigue evaluando por fechas y nunca por el estado (Principio IV).
        var vigentes = aplicables
            .Where(p => p.Permiso.Estado == Estado.ACTIVO
                        && p.Permiso.EstaVigenteEn(datos.FechaHoraUtc))
            .ToList();

        if (vigentes.Count == 0)
        {
            return Denegar(MotivoDenegacion.PERMISO_FUERA_DE_VIGENCIA, principalId, contexto.Id);
        }

        // Paso 12: filtrar por día de semana y bloque horario en la zona empresarial.
        var dia = ADiaSemana(reloj.DiaSemanaLocal(datos.FechaHoraUtc));
        var hora = reloj.HoraLocal(datos.FechaHoraUtc);

        var enHorario = vigentes
            .Where(p => p.Bloques.Any(b => b.Cubre(dia, hora)))
            .ToList();

        if (enHorario.Count == 0)
        {
            return Denegar(MotivoDenegacion.FUERA_DE_BLOQUE_HORARIO, principalId, contexto.Id);
        }

        // Paso 13: precedencia PERSONA > UNIDAD_ORGANIZATIVA > COMPAÑÍA (RF-025).
        var ganador = enHorario
            .OrderBy(p => (int)p.Permiso.Alcance)
            .ThenBy(p => p.Permiso.Id)
            .First()
            .Permiso;

        // Paso 14: conceder.
        return new ResultadoDeEvaluacion(
            ResultadoEvaluacion.CONCEDIDO,
            MotivoDenegacion: null,
            principalId,
            contexto.Id,
            ganador.Id,
            ganador.Alcance);
    }

    /// <summary>
    /// Paso 5: re-deriva si el contexto operativo sigue siendo legítimo (RF-061, RF-065).
    /// </summary>
    /// <remarks>
    /// Devuelve <c>null</c> cuando es legítimo. La re-derivación es dinámica y no escribe nada: no
    /// cierra ni modifica el contexto, solo se niega a usarlo.
    /// </remarks>
    private static MotivoDenegacion? EvaluarLegitimidad(DatosDeEvaluacion datos, Guid principalId)
    {
        var pertenencia = datos.CompaniaPertenencia;

        if (pertenencia is null)
        {
            // Sin pertenencia vigente no hay nada que legitime el contexto.
            return MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE;
        }

        // La persona pertenece a la propia Principal evaluada: legitimidad automática (RF-053).
        if (pertenencia.Id == principalId)
        {
            return null;
        }

        if (pertenencia.TipoCompania == TipoCompania.CONTRATISTA)
        {
            // Personal de contratista: la relación con esta Principal debe seguir vigente (RF-054).
            return datos.RelacionContratistaPrincipalVigente
                ? null
                : MotivoDenegacion.RELACION_CONTRATISTA_PRINCIPAL_VENCIDA;
        }

        // Pertenece a otra Principal distinta: el contexto ya no es legítimo.
        return MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE;
    }

    /// <summary>Paso 10: el permiso apunta al sujeto evaluado en su nivel.</summary>
    private static bool EsAplicableAlSujeto(PermisoAcceso permiso, DatosDeEvaluacion datos) =>
        permiso.Alcance switch
        {
            AlcancePermiso.PERSONA =>
                permiso.PersonaId == datos.PersonaId,

            // Solo si la persona tiene unidad vigente en este contexto (paso 9).
            AlcancePermiso.UNIDAD_ORGANIZATIVA =>
                datos.UnidadOrganizativaVigenteId is not null
                && permiso.UnidadOrganizativaId == datos.UnidadOrganizativaVigenteId,

            // La compañía de pertenencia vigente, sea Principal o Contratista (research.md §12).
            AlcancePermiso.COMPANIA =>
                datos.CompaniaPertenencia is not null
                && permiso.CompaniaId == datos.CompaniaPertenencia.Id,

            _ => false,
        };

    private static DiaSemana ADiaSemana(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => DiaSemana.LUNES,
        DayOfWeek.Tuesday => DiaSemana.MARTES,
        DayOfWeek.Wednesday => DiaSemana.MIERCOLES,
        DayOfWeek.Thursday => DiaSemana.JUEVES,
        DayOfWeek.Friday => DiaSemana.VIERNES,
        DayOfWeek.Saturday => DiaSemana.SABADO,
        DayOfWeek.Sunday => DiaSemana.DOMINGO,
        _ => throw new ArgumentOutOfRangeException(nameof(dia)),
    };

    private static ResultadoDeEvaluacion Denegar(
        MotivoDenegacion motivo,
        Guid? principalId = null,
        Guid? contextoId = null) =>
        new(
            ResultadoEvaluacion.DENEGADO,
            motivo,
            principalId,
            contextoId,
            PermisoAplicadoId: null,
            NivelAplicado: null);
}
