import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as evaluacionApi from '../../src/features/access-evaluation/api'
import type { EvaluarAccesoResponse } from '../../src/features/access-evaluation/api'
import { EvaluacionAccesoPage } from '../../src/features/access-evaluation/EvaluacionAccesoPage'
import { pasosDeEvaluacion } from '../../src/features/access-evaluation/pasos'
import * as areasApi from '../../src/features/area-access/api'
import * as companiasApi from '../../src/features/companies/api'
import * as personasApi from '../../src/features/people/api'

const P1 = '0199b0d0-0000-7000-8000-00000000000a'
const AREA = '0199b0d0-0000-7000-8000-0000000000a1'
const PERSONA = '0199b0d0-0000-7000-8000-0000000000f1'

function respuesta(parcial: Partial<EvaluarAccesoResponse>): EvaluarAccesoResponse {
  return {
    resultado: 'DENEGADO',
    motivoDenegacion: null,
    companiaPrincipalId: P1,
    contextoOperativoId: null,
    permisoAplicadoId: null,
    nivelAplicado: null,
    evaluadoEnZonaHoraria: 'America/Lima',
    ...parcial,
  }
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <EvaluacionAccesoPage />
    </QueryClientProvider>,
  )
}

async function completarFormulario(): Promise<void> {
  await screen.findByRole('option', { name: /Pérez, Ana/ })
  await userEvent.selectOptions(screen.getByLabelText('Persona'), PERSONA)
  await screen.findByRole('option', { name: 'Minera Norte' })
  await userEvent.selectOptions(screen.getByLabelText('Compañía principal'), P1)
  await screen.findByRole('option', { name: 'Planta' })
  await userEvent.selectOptions(screen.getByLabelText('Área'), AREA)
  fireEvent.change(screen.getByLabelText('Fecha y hora'), { target: { value: '2026-09-16T09:00' } })
}

describe('pasosDeEvaluacion', () => {
  it('marca todas las validaciones como cumplidas cuando se concede', () => {
    const pasos = pasosDeEvaluacion(respuesta({ resultado: 'CONCEDIDO' }))

    expect(pasos.every((p) => p.estado === 'CUMPLIDO')).toBe(true)
  })

  it('marca las previas como cumplidas, la que falla y las posteriores como no evaluadas', () => {
    const pasos = pasosDeEvaluacion(respuesta({ motivoDenegacion: 'SIN_CREDENCIAL_VIGENTE' }))

    expect(pasos.map((p) => [p.clave, p.estado])).toEqual([
      ['identificacion', 'CUMPLIDO'],
      ['contexto', 'CUMPLIDO'],
      ['credencial', 'FALLIDO'],
      ['area', 'NO_EVALUADO'],
      ['perfil', 'NO_EVALUADO'],
      ['permiso', 'NO_EVALUADO'],
      ['vigencia', 'NO_EVALUADO'],
      ['horario', 'NO_EVALUADO'],
    ])
  })

  it('agrupa la relación contratista vencida en la validación de contexto', () => {
    const pasos = pasosDeEvaluacion(
      respuesta({ motivoDenegacion: 'RELACION_CONTRATISTA_PRINCIPAL_VENCIDA' }),
    )

    expect(pasos.find((p) => p.clave === 'contexto')?.estado).toBe('FALLIDO')
    expect(pasos.find((p) => p.clave === 'credencial')?.estado).toBe('NO_EVALUADO')
  })
})

describe('EvaluacionAccesoPage', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(personasApi, 'buscarPersonas').mockResolvedValue({
      items: [
        {
          id: PERSONA,
          nombres: 'Ana',
          apellidos: 'Pérez',
          fechaNacimiento: '1990-05-20',
          tipoDocumentoId: 'dni',
          numeroDocumento: '12345678',
          generoId: 'g',
          correoElectronico: 'ana@empresa.cl',
          tipoSangreId: 't',
          contactoEmergencia: 'x',
          numeroEmergencia: 'y',
        },
      ],
      total: 1,
      pagina: 1,
      tamañoPagina: 20,
    })
    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue({
      items: [
        {
          id: P1,
          nombre: 'Minera Norte',
          tipoDocumentoId: 'ruc',
          numeroDocumento: '20100000001',
          tipoCompania: 'PRINCIPAL_MANDANTE',
          estado: 'ACTIVO',
        },
      ],
      total: 1,
      pagina: 1,
      tamañoPagina: 200,
    })
    vi.spyOn(areasApi, 'listarAreas').mockResolvedValue([
      {
        id: AREA,
        nombre: 'Planta',
        areaSuperiorId: null,
        companiaPrincipalId: P1,
        estado: 'ACTIVO',
      },
    ])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('no permite evaluar hasta tener persona, área y fecha', async () => {
    renderizar()

    expect(await screen.findByRole('button', { name: 'Evaluar acceso' })).toBeDisabled()
  })

  it('envía persona, área y el instante en UTC', async () => {
    const evaluar = vi
      .spyOn(evaluacionApi, 'evaluarAcceso')
      .mockResolvedValue(respuesta({ resultado: 'CONCEDIDO' }))

    renderizar()
    await completarFormulario()
    await userEvent.click(screen.getByRole('button', { name: 'Evaluar acceso' }))

    expect(evaluar.mock.calls[0][0]).toEqual({
      personaId: PERSONA,
      areaAccesoId: AREA,
      fechaHora: new Date('2026-09-16T09:00').toISOString(),
    })
  })

  it('muestra un acceso concedido con el permiso y el nivel que lo determinan', async () => {
    vi.spyOn(evaluacionApi, 'evaluarAcceso').mockResolvedValue(
      respuesta({
        resultado: 'CONCEDIDO',
        permisoAplicadoId: 'permiso-1',
        nivelAplicado: 'UNIDAD_ORGANIZATIVA',
      }),
    )

    renderizar()
    await completarFormulario()
    await userEvent.click(screen.getByRole('button', { name: 'Evaluar acceso' }))

    expect(await screen.findByText('Acceso concedido')).toBeInTheDocument()
    expect(screen.getByText(/Nivel Unidad organizativa/)).toBeInTheDocument()
    expect(screen.getByText('America/Lima')).toBeInTheDocument()
  })

  it('muestra la denegación por falta de credencial vigente y qué quedó sin evaluar', async () => {
    vi.spyOn(evaluacionApi, 'evaluarAcceso').mockResolvedValue(
      respuesta({ motivoDenegacion: 'SIN_CREDENCIAL_VIGENTE', contextoOperativoId: 'ctx-1' }),
    )

    renderizar()
    await completarFormulario()
    await userEvent.click(screen.getByRole('button', { name: 'Evaluar acceso' }))

    expect(await screen.findByText('Acceso denegado')).toBeInTheDocument()
    expect(screen.getByText('SIN_CREDENCIAL_VIGENTE')).toBeInTheDocument()
    expect(
      screen.getByText(/nunca se asignó, fue devuelta, dada de baja o revocada/),
    ).toBeInTheDocument()

    const pasos = within(screen.getByRole('list'))
    expect(pasos.getByText(/Credencial vigente para esa compañía principal/)).toHaveTextContent(
      '(no cumplido)',
    )
    expect(pasos.getByText(/Área activa/)).toHaveTextContent('(no evaluado)')
    expect(pasos.getByText(/Contexto operativo vigente/)).toHaveTextContent('(cumplido)')
  })

  it('traduce el 404 del contrato a un mensaje que no revela si el recurso existe', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(evaluacionApi, 'evaluarAcceso').mockRejectedValue(
      new ApiError(
        { status: 404, detail: 'Persona o área no encontrada…', codigo: 'RECURSO_NO_ENCONTRADO' },
        404,
      ),
    )

    renderizar()
    await completarFormulario()
    await userEvent.click(screen.getByRole('button', { name: 'Evaluar acceso' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Persona o área no encontrada, o fuera de su alcance.',
    )
  })
})
