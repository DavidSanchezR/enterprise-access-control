import { expect, test } from '@playwright/test'
import { ClienteApi } from './soporte/api.ts'
import { evaluar, montarSeccion5 } from './soporte/seccion5.ts'
import { fijarReloj } from './soporte/tiempo.ts'

interface NodoArbol {
  id: string
  hijos: NodoArbol[]
}

interface ContextoOperativo {
  id: string
  companiaPrincipalId: string
  fechaHoraInicio: string
  fechaHoraFin: string
  estado: string
}

const idsDelArbol = (nodos: NodoArbol[]): string[] =>
  nodos.flatMap((n) => [n.id, ...idsDelArbol(n.hijos)])

/**
 * T160 — quickstart.md §6: Pedro García (Servicios ACME) trabaja a la vez para Minera ABC y Minera XYZ,
 * con unidad organizativa, permiso y credencial independientes por Principal. Parte de lo que monta §5
 * (Minera ABC, sus áreas y el tipo de persona), como indica la guía.
 */
test('quickstart §6 — contexto operativo multi-Principal de extremo a extremo', async () => {
  const api = await ClienteApi.iniciarComo('admin')

  try {
    const reloj = await fijarReloj()
    const s5 = await montarSeccion5(api, reloj)

    const unidadAbcId =
      await test.step('§6 paso 1 — unidad organizativa raíz de Minera ABC', async () => {
        const unidad = await api.exigir<{ id: string }>(
          api.post('/api/unidades-organizativas', {
            nombre: 'Operaciones ABC',
            estado: 'ACTIVO',
            unidadSuperiorId: null,
            companiaPrincipalId: s5.mineraAbcId,
          }),
          201,
        )

        const arbol = await api.exigir<NodoArbol[]>(
          api.get(`/api/unidades-organizativas/arbol?companiaPrincipalId=${s5.mineraAbcId}`),
          200,
        )
        expect(arbol.map((n) => n.id)).toContain(unidad.id)

        return unidad.id
      })

    const { mineraXyzId, unidadXyzId } =
      await test.step('§6 paso 2 — Minera XYZ con su propia unidad raíz (CS-011)', async () => {
        const xyz = await api.crearCompaniaEnAlcance('Minera XYZ', 'PRINCIPAL_MANDANTE')

        const unidad = await api.exigir<{ id: string }>(
          api.post('/api/unidades-organizativas', {
            nombre: 'Operaciones XYZ',
            estado: 'ACTIVO',
            unidadSuperiorId: null,
            companiaPrincipalId: xyz,
          }),
          201,
        )

        const arbol = await api.exigir<NodoArbol[]>(
          api.get(`/api/unidades-organizativas/arbol?companiaPrincipalId=${xyz}`),
          200,
        )
        expect(idsDelArbol(arbol)).toContain(unidad.id)
        expect(idsDelArbol(arbol)).not.toContain(unidadAbcId)

        return { mineraXyzId: xyz, unidadXyzId: unidad.id }
      })

    const areaXyzId =
      await test.step('§6 paso 3 — área de Minera XYZ con el tipo de persona autorizado', async () => {
        const area = await api.exigir<{ id: string }>(
          api.post('/api/areas-acceso', {
            nombre: 'Almacén XYZ',
            estado: 'ACTIVO',
            areaSuperiorId: null,
            companiaPrincipalId: mineraXyzId,
          }),
          201,
        )

        await api.exigir(
          api.put(`/api/areas-acceso/${area.id}/tipos-persona`, {
            tipoPersonaIds: [s5.tipoPersonaId],
          }),
          204,
        )

        return area.id
      })

    const acmeId =
      await test.step('§6 paso 4 — Servicios ACME no puede poseer unidades (RF-045)', async () => {
        const acme = await api.crearCompaniaEnAlcance('Servicios ACME', 'CONTRATISTA')

        const codigo = await api.codigo(
          api.post('/api/unidades-organizativas', {
            nombre: 'Unidad ACME',
            estado: 'ACTIVO',
            unidadSuperiorId: null,
            companiaPrincipalId: acme,
          }),
          400,
        )
        expect(codigo).toBe('COMPANIA_DEBE_SER_PRINCIPAL')

        return acme
      })

    const pedroId =
      await test.step('§6 paso 5 — Pedro García: pertenencia a ACME y perfil', async () => {
        const pedro = await api.crearPersona('Pedro', 'García E2E')

        await api.exigir(
          api.post(`/api/personas/${pedro}/historial-companias`, {
            companiaId: acmeId,
            fechaHoraInicio: reloj.inicio,
            fechaHoraFin: reloj.fin,
          }),
          201,
        )

        await api.exigir(
          api.post(`/api/personas/${pedro}/perfiles`, {
            tipoPersonaId: s5.tipoPersonaId,
            fechaHoraInicio: reloj.inicio,
            fechaHoraFin: reloj.fin,
          }),
          201,
        )

        return pedro
      })

    const crearContexto = (principalId: string) =>
      api.post(`/api/personas/${pedroId}/contextos-operativos`, {
        companiaPrincipalId: principalId,
        fechaHoraInicio: reloj.inicio,
        fechaHoraFin: reloj.fin,
      })

    await test.step('§6 paso 6 — contexto sin relación contratista vigente se rechaza (RF-054)', async () => {
      const codigo = await api.codigo(crearContexto(s5.mineraAbcId), 400)
      expect(codigo).toBe('SIN_RELACION_CONTRATISTA_PRINCIPAL_VIGENTE')
    })

    await test.step('§6 paso 7 — relaciones simultáneas con ABC y XYZ (CS-012)', async () => {
      for (const principalId of [s5.mineraAbcId, mineraXyzId]) {
        await api.exigir(
          api.post(`/api/companias/${acmeId}/relaciones-principales`, {
            companiaPrincipalId: principalId,
            fechaHoraInicio: reloj.inicio,
          }),
          201,
        )
      }
    })

    const { contextoAbcId, contextoXyzId } =
      await test.step('§6 paso 8 — dos contextos operativos vigentes a la vez (CS-013)', async () => {
        const abc = await api.exigir<ContextoOperativo>(crearContexto(s5.mineraAbcId), 201)
        const xyz = await api.exigir<ContextoOperativo>(crearContexto(mineraXyzId), 201)

        const contextos = await api.exigir<ContextoOperativo[]>(
          api.get(`/api/personas/${pedroId}/contextos-operativos`),
          200,
        )

        const t = reloj.referencia.getTime()
        const vigentes = contextos.filter(
          (c) =>
            c.estado === 'ACTIVO' &&
            new Date(c.fechaHoraInicio).getTime() <= t &&
            t < new Date(c.fechaHoraFin).getTime(),
        )

        expect(vigentes.map((c) => c.id).sort()).toEqual([abc.id, xyz.id].sort())

        return { contextoAbcId: abc.id, contextoXyzId: xyz.id }
      })

    await test.step('§6 paso 9 — unidad organizativa distinta por contexto (CS-014)', async () => {
      const asignar = (contextoId: string, unidadId: string) =>
        api.exigir(
          api.post(
            `/api/personas/${pedroId}/contextos-operativos/${contextoId}/unidad-organizativa`,
            {
              unidadOrganizativaId: unidadId,
              fechaHoraInicio: reloj.inicio,
              fechaHoraFin: reloj.fin,
            },
          ),
          201,
        )

      await asignar(contextoAbcId, unidadAbcId)
      await asignar(contextoXyzId, unidadXyzId)

      const estado = await api.exigir<{
        contextosOperativosVigentes: {
          contextoOperativoId: string
          companiaPrincipalId: string
          unidadOrganizativaVigenteId: string | null
        }[]
      }>(
        api.get(
          `/api/personas/${pedroId}/estado-efectivo?fechaHora=${encodeURIComponent(reloj.referencia.toISOString())}`,
        ),
        200,
      )

      expect(estado.contextosOperativosVigentes).toHaveLength(2)
      expect(estado.contextosOperativosVigentes).toEqual(
        expect.arrayContaining([
          {
            contextoOperativoId: contextoAbcId,
            companiaPrincipalId: s5.mineraAbcId,
            unidadOrganizativaVigenteId: unidadAbcId,
          },
          {
            contextoOperativoId: contextoXyzId,
            companiaPrincipalId: mineraXyzId,
            unidadOrganizativaVigenteId: unidadXyzId,
          },
        ]),
      )
    })

    await test.step('§6 paso 10 — permiso y credenciales independientes por Principal', async () => {
      await api.exigir(
        api.post('/api/permisos', {
          areaAccesoId: areaXyzId,
          alcance: 'UNIDAD_ORGANIZATIVA',
          unidadOrganizativaId: unidadXyzId,
          fechaInicioVigencia: reloj.fechaInicioPermiso,
          fechaFinVigencia: reloj.fechaFinPermiso,
          estado: 'ACTIVO',
          bloquesHorarios: [reloj.bloque],
        }),
        201,
      )

      const asignarCredencial = (principalId: string) =>
        api.post(`/api/personas/${pedroId}/credenciales`, {
          companiaPrincipalId: principalId,
          tipoCredencialId: s5.tipoCredencialId,
          fechaHoraInicio: reloj.inicio,
          fechaHoraFin: reloj.fin,
        })

      await api.exigir(asignarCredencial(s5.mineraAbcId), 201)
      await api.exigir(asignarCredencial(mineraXyzId), 201)

      const credenciales = await api.exigir<{ companiaPrincipalId: string; estado: string }[]>(
        api.get(`/api/personas/${pedroId}/credenciales`),
        200,
      )

      // CS-016/CS-017: coexisten, una por Principal, ambas ASIGNADO.
      expect(
        credenciales
          .filter((c) => c.estado === 'ASIGNADO')
          .map((c) => c.companiaPrincipalId)
          .sort(),
      ).toEqual([s5.mineraAbcId, mineraXyzId].sort())

      // RF-056: cada credencial exige su propio contexto. La compañía ancla también es Principal y está
      // en el alcance, pero Pedro no tiene contexto con ella.
      const codigo = await api.codigo(asignarCredencial(api.entorno.anclaId), 400)
      expect(codigo).toBe('SIN_CONTEXTO_OPERATIVO_VIGENTE')
    })

    await test.step('§6 paso 11 — acceso al área de XYZ por su contexto XYZ', async () => {
      const evaluacion = await evaluar(api, pedroId, areaXyzId, reloj.referencia)

      expect(evaluacion).toMatchObject({
        resultado: 'CONCEDIDO',
        nivelAplicado: 'UNIDAD_ORGANIZATIVA',
        companiaPrincipalId: mineraXyzId,
        contextoOperativoId: contextoXyzId,
      })
    })

    await test.step('§6 paso 12 — el permiso de XYZ nunca se evalúa para un área de ABC (CS-018)', async () => {
      const evaluacion = await evaluar(api, pedroId, s5.nivel3Id, reloj.referencia)

      expect(evaluacion).toMatchObject({
        resultado: 'DENEGADO',
        motivoDenegacion: 'SIN_PERMISO_APLICABLE',
      })
    })
  } finally {
    await api.cerrar()
  }
})
