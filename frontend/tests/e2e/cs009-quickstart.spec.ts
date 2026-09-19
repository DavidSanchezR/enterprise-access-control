import { expect, test } from '@playwright/test'
import { ClienteApi } from './soporte/api.ts'
import { evaluar, montarSeccion5 } from './soporte/seccion5.ts'
import { fijarReloj } from './soporte/tiempo.ts'

/**
 * T159 — quickstart.md §5 (CS-009) completo contra la API real y SQL Server: jerarquía de tres niveles,
 * permiso horario, gate de credencial, denegación por defecto y aislamiento entre compañías.
 */
test('quickstart §5 — CS-009: jerarquía de 3 niveles y permiso horario de extremo a extremo', async () => {
  const api = await ClienteApi.iniciarComo('admin')
  const ajeno = await ClienteApi.iniciarComo('ajeno')

  try {
    await test.step('§5 paso 1 — login con alcance no vacío', async () => {
      const sesion = await api.iniciarSesion()
      expect(sesion.alcanceCompanias.length).toBeGreaterThan(0)
    })

    const reloj = await fijarReloj()
    const montaje = await montarSeccion5(api, reloj)

    await test.step('§5 paso 10 — evaluar acceso en el bloque: CONCEDIDO por PERSONA', async () => {
      const inicio = performance.now()
      const evaluacion = await evaluar(api, montaje.personaId, montaje.nivel3Id, reloj.referencia)
      const latenciaMs = performance.now() - inicio

      expect(evaluacion).toMatchObject({
        resultado: 'CONCEDIDO',
        nivelAplicado: 'PERSONA',
        companiaPrincipalId: montaje.mineraAbcId,
        contextoOperativoId: montaje.contextoOperativoId,
      })

      // CS-003 se mide con carga en EvaluacionAccesoPerformanceTests; aquí solo se deja constancia.
      test
        .info()
        .annotations.push({ type: 'CS-003 latencia (ms)', description: latenciaMs.toFixed(0) })
    })

    await test.step('§5 paso 11 — fuera del bloque horario: DENEGADO por defecto', async () => {
      const evaluacion = await evaluar(
        api,
        montaje.personaId,
        montaje.nivel3Id,
        reloj.fueraDelBloque,
      )

      expect(evaluacion).toMatchObject({
        resultado: 'DENEGADO',
        motivoDenegacion: 'FUERA_DE_BLOQUE_HORARIO',
      })
    })

    await test.step('§5 paso 12 — un usuario sin la compañía en su alcance recibe 404', async () => {
      const sesion = await ajeno.iniciarSesion()
      expect(sesion.alcanceCompanias).not.toContain(montaje.mineraAbcId)

      await ajeno.exigir(ajeno.get(`/api/personas/${montaje.personaId}`), 404)

      // Contraprueba: el 404 se debe al alcance, no a que la persona no exista.
      await api.exigir(api.get(`/api/personas/${montaje.personaId}`), 200)
    })
  } finally {
    await api.cerrar()
    await ajeno.cerrar()
  }
})
