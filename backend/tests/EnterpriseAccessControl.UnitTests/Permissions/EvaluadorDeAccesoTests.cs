using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.Domain.Services;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.Permissions;

/// <summary>
/// Los 14 pasos de <c>EvaluadorDeAcceso</c>, uno a uno y sin base de datos
/// (research.md §7, §18; Principio VII).
/// </summary>
/// <remarks>
/// Cada prueba parte del escenario que concede y rompe exactamente un paso, de modo que el motivo
/// devuelto identifique sin ambigüedad el corte que se está ejercitando. Lo que aquí se fija son las
/// reglas; que las consultas traigan los datos correctos se verifica en las pruebas de integración
/// contra SQL Server, que es donde esa parte puede fallar.
/// </remarks>
public sealed class EvaluadorDeAccesoTests
{
    private static ResultadoDeEvaluacion Evaluar(DatosDeEvaluacion datos) =>
        EscenarioEvaluacion.Evaluador().Evaluar(datos);

    [Fact]
    public void El_escenario_completo_concede()
    {
        var resultado = Evaluar(EscenarioEvaluacion.Concede());

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.MotivoDenegacion.Should().BeNull();
        resultado.CompaniaPrincipalId.Should().Be(EscenarioEvaluacion.PrincipalId);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.PERSONA);
        resultado.PermisoAplicadoId.Should().NotBeNull();
    }

    // --- Pasos 2, 3, 4 y 1 -----------------------------------------------------------------------

    [Fact]
    public void Paso_2_sin_persona_deniega_por_persona_no_encontrada()
    {
        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { PersonaId = null });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.PERSONA_NO_ENCONTRADA);
    }

    [Fact]
    public void Paso_3_sin_area_deniega_por_area_no_encontrada()
    {
        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { Area = null });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.AREA_NO_ENCONTRADA);
        resultado.CompaniaPrincipalId.Should().BeNull("sin área no hay Principal que determinar");
    }

    [Fact]
    public void Paso_1_sin_alcance_del_usuario_deniega_e_informa_la_principal()
    {
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with { UsuarioTieneAlcanceSobrePrincipal = false });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.FUERA_DE_ALCANCE_USUARIO);

        // El paso 1 se enuncia primero pero depende de la Principal del paso 4: para entonces ya se
        // conoce, y el contrato pide informarla incluso al denegar.
        resultado.CompaniaPrincipalId.Should().Be(EscenarioEvaluacion.PrincipalId);
    }

    // --- Paso 5: contexto operativo y legitimidad -----------------------------------------------

    [Fact]
    public void Paso_5_sin_contexto_operativo_deniega()
    {
        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { ContextoOperativo = null });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);
    }

    [Fact]
    public void Paso_5_con_contexto_expirado_deniega_aunque_su_estado_siga_activo()
    {
        // Principio IV: la vigencia se decide por fechas, nunca por el Estado administrativo.
        var expirado = EscenarioEvaluacion.Contexto(
            inicio: EscenarioEvaluacion.Instante.AddYears(-2),
            fin: EscenarioEvaluacion.Instante.AddDays(-1));

        expirado.Estado.Should().Be(Estado.ACTIVO);

        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { ContextoOperativo = expirado });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);
    }

    [Fact]
    public void Paso_5_sin_pertenencia_vigente_deniega_aunque_el_contexto_exista()
    {
        // Re-validación dinámica (RF-065): el contexto puede seguir escrito y haber dejado de ser
        // legítimo. El evaluador no lo cierra, solo se niega a usarlo.
        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { CompaniaPertenencia = null });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);
        resultado.ContextoOperativoId.Should().NotBeNull("el contexto existe; lo que falta es su legitimidad");
    }

    [Fact]
    public void Paso_5_con_pertenencia_a_otra_principal_deniega()
    {
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede(pertenencia: EscenarioEvaluacion.Principal()));

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CONTEXTO_OPERATIVO_VIGENTE);
    }

    [Fact]
    public void Paso_5_contratista_sin_relacion_vigente_deniega_con_su_propio_motivo()
    {
        var datos = EscenarioEvaluacion.Concede(pertenencia: EscenarioEvaluacion.Contratista())
            with
        { RelacionContratistaPrincipalVigente = false };

        var resultado = Evaluar(datos);

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.RELACION_CONTRATISTA_PRINCIPAL_VENCIDA);
    }

    [Fact]
    public void Paso_5_contratista_con_relacion_vigente_sigue_adelante()
    {
        var contratista = EscenarioEvaluacion.Contratista();

        var datos = EscenarioEvaluacion.Concede(pertenencia: contratista) with
        {
            RelacionContratistaPrincipalVigente = true,
            // El permiso de alcance COMPAÑÍA apunta a la contratista, no a la Principal (§12).
            PermisosDelArea =
            [
                EscenarioEvaluacion.Permiso(AlcancePermiso.COMPANIA, sujetoId: contratista.Id),
            ],
        };

        var resultado = Evaluar(datos);

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.COMPANIA);
    }

    // --- Paso 6: gate de credencial (RF-066, RF-070) ---------------------------------------------

    [Fact]
    public void Paso_6_sin_credencial_asignada_nunca_deniega()
    {
        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { Credencial = null });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    [Theory]
    [InlineData(EstadoCredencial.DEVUELTO)]
    [InlineData(EstadoCredencial.ELIMINADO)]
    [InlineData(EstadoCredencial.REVOCADA)]
    public void Paso_6_credencial_en_estado_de_cierre_deniega(EstadoCredencial estado)
    {
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with { Credencial = EscenarioEvaluacion.Credencial(estado) });

        // Los tres estados de cierre comparten motivo con la ausencia total: el contrato no los
        // distingue en la respuesta.
        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    [Fact]
    public void Paso_6_credencial_asignada_pero_expirada_deniega_sin_cambiarle_el_estado()
    {
        var expirada = EscenarioEvaluacion.Credencial(
            EstadoCredencial.ASIGNADO,
            fin: EscenarioEvaluacion.Instante.AddDays(-1));

        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { Credencial = expirada });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);

        // El mero vencimiento no dispara ninguna transición de estado (RF-070, research.md §24).
        expirada.Estado.Should().Be(EstadoCredencial.ASIGNADO);
    }

    [Fact]
    public void Paso_6_la_credencial_sigue_vigente_en_el_instante_exacto_de_su_fin()
    {
        // RF-070, research.md §7 paso 6: FechaHoraInicio <= fecha evaluada <= FechaHoraFin, con
        // ambos extremos inclusivos.
        var terminaAhora = EscenarioEvaluacion.Credencial(fin: EscenarioEvaluacion.Instante);

        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { Credencial = terminaAhora });

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
    }

    [Fact]
    public void Paso_6_se_evalua_antes_que_el_area_y_el_perfil()
    {
        // research.md §7: agrupar las verificaciones de identidad antes de la elegibilidad evita
        // calcular unidad y permisos para alguien que será denegado igual.
        var datos = EscenarioEvaluacion.Concede() with
        {
            Credencial = null,
            Area = EscenarioEvaluacion.Area(Estado.INACTIVO),
            TiposPersonaAutorizadosEnArea = [],
        };

        Evaluar(datos).MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_CREDENCIAL_VIGENTE);
    }

    // --- Pasos 7 y 8 ------------------------------------------------------------------------------

    [Fact]
    public void Paso_7_area_inactiva_deniega()
    {
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with { Area = EscenarioEvaluacion.Area(Estado.INACTIVO) });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.AREA_INACTIVA);
    }

    [Fact]
    public void Paso_8_perfil_no_autorizado_en_el_area_deniega()
    {
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with
            {
                TiposPersonaAutorizadosEnArea = [Guid.CreateVersion7()],
            });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.PERFIL_NO_AUTORIZADO_EN_AREA);
    }

    [Fact]
    public void Paso_8_un_area_sin_tipos_autorizados_no_concede_a_nadie()
    {
        // Default-deny: un área que no declara perfiles no admite a ninguno (RF-024).
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with { TiposPersonaAutorizadosEnArea = [] });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.PERFIL_NO_AUTORIZADO_EN_AREA);
    }

    // --- Pasos 10 a 12 ----------------------------------------------------------------------------

    [Fact]
    public void Paso_10_sin_permisos_del_area_deniega()
    {
        var resultado = Evaluar(EscenarioEvaluacion.Concede() with { PermisosDelArea = [] });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_PERMISO_APLICABLE);
    }

    [Fact]
    public void Paso_10_un_permiso_de_otra_persona_no_es_aplicable()
    {
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with
            {
                PermisosDelArea =
                [
                    EscenarioEvaluacion.Permiso(AlcancePermiso.PERSONA, sujetoId: Guid.CreateVersion7()),
                ],
            });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_PERMISO_APLICABLE);
    }

    [Fact]
    public void Paso_10_un_permiso_de_unidad_no_aplica_si_la_persona_no_tiene_unidad_vigente()
    {
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with
            {
                UnidadOrganizativaVigenteId = null,
                PermisosDelArea = [EscenarioEvaluacion.Permiso(AlcancePermiso.UNIDAD_ORGANIZATIVA)],
            });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.SIN_PERMISO_APLICABLE);
    }

    [Fact]
    public void Paso_11_permiso_fuera_de_vigencia_deniega()
    {
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with
            {
                PermisosDelArea =
                [
                    EscenarioEvaluacion.Permiso(
                        AlcancePermiso.PERSONA,
                        inicio: EscenarioEvaluacion.Instante.AddYears(-2),
                        fin: EscenarioEvaluacion.Instante.AddDays(-1)),
                ],
            });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.PERMISO_FUERA_DE_VIGENCIA);
    }

    [Fact]
    public void Paso_11_un_permiso_dado_de_baja_no_concede()
    {
        // Estado INACTIVO es baja lógica: un permiso retirado que siguiera concediendo haría inútil
        // retirarlo.
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with
            {
                PermisosDelArea =
                [
                    EscenarioEvaluacion.Permiso(AlcancePermiso.PERSONA, estado: Estado.INACTIVO),
                ],
            });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.PERMISO_FUERA_DE_VIGENCIA);
    }

    [Fact]
    public void Paso_12_fuera_del_bloque_horario_deniega()
    {
        // El instante evaluado es martes 09:00 en Lima; el bloque abre a las 14:00.
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with
            {
                PermisosDelArea =
                [
                    EscenarioEvaluacion.Permiso(
                        AlcancePermiso.PERSONA, horaInicio: "14:00", horaFin: "18:00"),
                ],
            });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.FUERA_DE_BLOQUE_HORARIO);
    }

    [Fact]
    public void Paso_12_el_dia_de_la_semana_se_evalua_en_hora_local()
    {
        // 15/09/2026 02:00 UTC es todavía lunes 21:00 en Lima: un bloque de martes no debe cubrirlo.
        var lunesEnLima = new DateTime(2026, 9, 15, 2, 0, 0, DateTimeKind.Utc);

        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with
            {
                FechaHoraUtc = lunesEnLima,
                PermisosDelArea =
                [
                    EscenarioEvaluacion.Permiso(
                        AlcancePermiso.PERSONA,
                        dia: DiaSemana.MARTES,
                        horaInicio: "00:00",
                        horaFin: "23:59"),
                ],
            });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.FUERA_DE_BLOQUE_HORARIO);
    }

    [Fact]
    public void Paso_12_el_fin_del_bloque_es_exclusivo()
    {
        // 09:00 local con bloque 08:00–09:00: el último instante cubierto es 08:59:59.
        var resultado = Evaluar(
            EscenarioEvaluacion.Concede() with
            {
                PermisosDelArea =
                [
                    EscenarioEvaluacion.Permiso(
                        AlcancePermiso.PERSONA, horaInicio: "08:00", horaFin: "09:00"),
                ],
            });

        resultado.MotivoDenegacion.Should().Be(MotivoDenegacion.FUERA_DE_BLOQUE_HORARIO);
    }

    // --- Paso 13: precedencia (RF-025) -------------------------------------------------------------

    [Fact]
    public void Paso_13_persona_gana_a_unidad_y_a_compania()
    {
        var pertenencia = EscenarioEvaluacion.PrincipalComoPertenencia();

        var personal = EscenarioEvaluacion.Permiso(AlcancePermiso.PERSONA);

        var datos = EscenarioEvaluacion.Concede(pertenencia) with
        {
            PermisosDelArea =
            [
                EscenarioEvaluacion.Permiso(AlcancePermiso.COMPANIA, sujetoId: pertenencia.Id),
                EscenarioEvaluacion.Permiso(AlcancePermiso.UNIDAD_ORGANIZATIVA),
                personal,
            ],
        };

        var resultado = Evaluar(datos);

        resultado.NivelAplicado.Should().Be(AlcancePermiso.PERSONA);
        resultado.PermisoAplicadoId.Should().Be(personal.Permiso.Id);
    }

    [Fact]
    public void Paso_13_unidad_gana_a_compania_cuando_no_hay_permiso_personal()
    {
        var pertenencia = EscenarioEvaluacion.PrincipalComoPertenencia();

        var deUnidad = EscenarioEvaluacion.Permiso(AlcancePermiso.UNIDAD_ORGANIZATIVA);

        var datos = EscenarioEvaluacion.Concede(pertenencia) with
        {
            PermisosDelArea =
            [
                EscenarioEvaluacion.Permiso(AlcancePermiso.COMPANIA, sujetoId: pertenencia.Id),
                deUnidad,
            ],
        };

        var resultado = Evaluar(datos);

        resultado.NivelAplicado.Should().Be(AlcancePermiso.UNIDAD_ORGANIZATIVA);
        resultado.PermisoAplicadoId.Should().Be(deUnidad.Permiso.Id);
    }

    [Fact]
    public void Paso_13_la_precedencia_se_aplica_solo_entre_los_que_superan_los_filtros()
    {
        // Un permiso de nivel PERSONA fuera de su horario no puede ganar por precedencia sobre uno de
        // COMPAÑÍA que sí aplica: la precedencia desempata, no rescata.
        var pertenencia = EscenarioEvaluacion.PrincipalComoPertenencia();

        var deCompania = EscenarioEvaluacion.Permiso(
            AlcancePermiso.COMPANIA, sujetoId: pertenencia.Id);

        var datos = EscenarioEvaluacion.Concede(pertenencia) with
        {
            PermisosDelArea =
            [
                EscenarioEvaluacion.Permiso(
                    AlcancePermiso.PERSONA, horaInicio: "20:00", horaFin: "23:00"),
                deCompania,
            ],
        };

        var resultado = Evaluar(datos);

        resultado.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
        resultado.NivelAplicado.Should().Be(AlcancePermiso.COMPANIA);
        resultado.PermisoAplicadoId.Should().Be(deCompania.Permiso.Id);
    }
}
