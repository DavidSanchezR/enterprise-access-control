import { expect, test } from '@playwright/test'
import type { ClienteApi } from './api.ts'
import type { Reloj } from './tiempo.ts'

interface NodoArbol {
  id: string
  hijos: NodoArbol[]
}

/** Lo que quickstart.md §5 (pasos 2 a 9) deja montado; §6 lo reutiliza como precondición. */
export interface MontajeSeccion5 {
  mineraAbcId: string
  nivel1Id: string
  nivel2Id: string
  nivel3Id: string
  tipoPersonaId: string
  tipoCredencialId: string
  personaId: string
  contextoOperativoId: string
}

/**
 * Pasos 2 a 9 de quickstart.md §5, cada uno con la verificación que la guía indica.
 *
 * Todas las vigencias usan la ventana del {@link Reloj}: empieza tres horas antes de la referencia y dura un
 * año, de modo que las asociaciones dependientes quedan contenidas en la pertenencia (RF-072).
 */
export async function montarSeccion5(api: ClienteApi, reloj: Reloj): Promise<MontajeSeccion5> {
  const mineraAbcId = await test.step('§5 paso 2 — Compañía Principal en el alcance', () =>
    api.crearCompaniaEnAlcance('Minera ABC', 'PRINCIPAL_MANDANTE'))

  const { nivel1Id, nivel2Id, nivel3Id } =
    await test.step('§5 paso 3 — jerarquía de 3 niveles de áreas de acceso', async () => {
      const crearArea = async (nombre: string, padre: string | null): Promise<string> => {
        const area = await api.exigir<{ id: string }>(
          api.post('/api/areas-acceso', {
            nombre,
            estado: 'ACTIVO',
            areaSuperiorId: padre,
            companiaPrincipalId: padre ? null : mineraAbcId,
          }),
          201,
        )
        return area.id
      }

      const n1 = await crearArea('Planta', null)
      const n2 = await crearArea('Edificio A', n1)
      const n3 = await crearArea('Sala de control', n2)

      const arbol = await api.exigir<NodoArbol[]>(
        api.get(`/api/areas-acceso/arbol?companiaPrincipalId=${mineraAbcId}`),
        200,
      )

      expect(arbol.map((n) => n.id)).toEqual([n1])
      expect(arbol[0].hijos.map((n) => n.id)).toEqual([n2])
      expect(arbol[0].hijos[0].hijos.map((n) => n.id)).toEqual([n3])

      return { nivel1Id: n1, nivel2Id: n2, nivel3Id: n3 }
    })

  await test.step('§5 paso 4 — mover la raíz bajo su nieto se rechaza con 409', async () => {
    const codigo = await api.codigo(
      api.post(`/api/areas-acceso/${nivel1Id}/mover`, { nuevoPadreId: nivel3Id }),
      409,
    )
    expect(codigo).toBe('CICLO_JERARQUICO')
  })

  const { tipoPersonaId, personaId } =
    await test.step('§5 paso 5 — tipo de persona, persona, perfil y pertenencia', async () => {
      const tipo = await api.crearMaestro('tipos-persona', 'Supervisor E2E')
      const persona = await api.crearPersona('Ana', 'Quispe E2E')

      // La pertenencia va primero: desde RF-082 (cambio post-Baseline VF-007) el perfil debe quedar
      // contenido en una pertenencia vigente, y sin ella el alta responde 400 SIN_PERTENENCIA_VIGENTE.
      await api.exigir(
        api.post(`/api/personas/${persona}/historial-companias`, {
          companiaId: mineraAbcId,
          fechaHoraInicio: reloj.inicio,
          fechaHoraFin: reloj.fin,
        }),
        201,
      )

      await api.exigir(
        api.post(`/api/personas/${persona}/perfiles`, {
          tipoPersonaId: tipo,
          fechaHoraInicio: reloj.inicio,
          fechaHoraFin: reloj.fin,
        }),
        201,
      )

      return { tipoPersonaId: tipo, personaId: persona }
    })

  await test.step('§5 paso 6 — autorizar el tipo de persona en el nivel 3', async () => {
    await api.exigir(
      api.put(`/api/areas-acceso/${nivel3Id}/tipos-persona`, { tipoPersonaIds: [tipoPersonaId] }),
      204,
    )
  })

  const contextoOperativoId =
    await test.step('§5 paso 7 — contexto operativo con la Principal', async () => {
      // RF-053: la persona pertenece a la propia Principal, así que no hace falta relación contratista.
      const contexto = await api.exigir<{ id: string }>(
        api.post(`/api/personas/${personaId}/contextos-operativos`, {
          companiaPrincipalId: mineraAbcId,
          fechaHoraInicio: reloj.inicio,
          fechaHoraFin: reloj.fin,
        }),
        201,
      )
      return contexto.id
    })

  await test.step('§5 paso 8 — permiso PERSONA con bloque horario', async () => {
    await api.exigir(
      api.post('/api/permisos', {
        areaAccesoId: nivel3Id,
        alcance: 'PERSONA',
        personaId,
        fechaInicioVigencia: reloj.fechaInicioPermiso,
        fechaFinVigencia: reloj.fechaFinPermiso,
        estado: 'ACTIVO',
        bloquesHorarios: [reloj.bloque],
      }),
      201,
    )
  })

  const tipoCredencialId =
    await test.step('§5 paso 9 — tipo de credencial y asignación', async () => {
      const tipo = await api.crearMaestro('tipos-credencial', 'Fotocheck E2E')

      const credencial = await api.exigir<{ estado: string }>(
        api.post(`/api/personas/${personaId}/credenciales`, {
          companiaPrincipalId: mineraAbcId,
          tipoCredencialId: tipo,
          fechaHoraInicio: reloj.inicio,
          fechaHoraFin: reloj.fin,
        }),
        201,
      )

      expect(credencial.estado).toBe('ASIGNADO')
      return tipo
    })

  return {
    mineraAbcId,
    nivel1Id,
    nivel2Id,
    nivel3Id,
    tipoPersonaId,
    tipoCredencialId,
    personaId,
    contextoOperativoId,
  }
}

export interface ResultadoEvaluacion {
  resultado: 'CONCEDIDO' | 'DENEGADO'
  motivoDenegacion: string | null
  companiaPrincipalId: string | null
  contextoOperativoId: string | null
  permisoAplicadoId: string | null
  nivelAplicado: string | null
}

export function evaluar(
  api: ClienteApi,
  personaId: string,
  areaAccesoId: string,
  fechaHora: Date,
): Promise<ResultadoEvaluacion> {
  return api.exigir<ResultadoEvaluacion>(
    api.post('/api/evaluacion-acceso', {
      personaId,
      areaAccesoId,
      fechaHora: fechaHora.toISOString(),
    }),
    200,
  )
}
