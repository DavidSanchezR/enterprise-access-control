import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as companiasApi from '../../src/features/companies/api'
import type { Compania, RelacionContratistaPrincipal } from '../../src/features/companies/api'
import * as historyApi from '../../src/features/people/history/api'
import { PertenenciaContextoWizard } from '../../src/features/people/history/PertenenciaContextoWizard'

const PERSONA = '0199b0d0-9000-7000-8000-000000000001'
const PRINCIPAL_A = '0199b0d0-8000-7000-8000-00000000000a'
const PRINCIPAL_B = '0199b0d0-8000-7000-8000-00000000000b'
const CONTRATISTA = '0199b0d0-8000-7000-8000-00000000000c'

function compania(id: string, nombre: string, tipo: Compania['tipoCompania']): Compania {
  return {
    id,
    nombre,
    tipoDocumentoId: '0199b0d0-0001-7000-8000-000000000001',
    numeroDocumento: id.slice(-8),
    tipoCompania: tipo,
    estado: 'ACTIVO',
    zonaHorariaIana: tipo === 'PRINCIPAL_MANDANTE' ? 'America/Lima' : null,
  }
}

function relacion(principalId: string): RelacionContratistaPrincipal {
  return {
    id: `rel-${principalId}`,
    companiaContratistaId: CONTRATISTA,
    companiaPrincipalId: principalId,
    fechaHoraInicio: new Date(Date.now() - 86_400_000).toISOString(),
    fechaHoraFin: null,
  }
}

/**
 * Elige la compania esperando primero a que sus opciones lleguen.
 *
 * `findByLabelText` resuelve en cuanto existe el `<select>`, que se renderiza con solo el marcador
 * de posicion: sin esta espera, `selectOptions` actuaria sobre una lista todavia vacia.
 */
async function elegirCompaniaAsync(nombre: string, id: string): Promise<void> {
  await screen.findByRole('option', { name: nombre })
  await userEvent.selectOptions(screen.getByLabelText('Compañía de pertenencia'), id)
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <PertenenciaContextoWizard personaId={PERSONA} alCerrar={() => {}} />
    </QueryClientProvider>,
  )
}

describe('PertenenciaContextoWizard', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue({
      items: [
        compania(PRINCIPAL_A, 'Minera Los Andes', 'PRINCIPAL_MANDANTE'),
        compania(PRINCIPAL_B, 'Minera Boreal', 'PRINCIPAL_MANDANTE'),
        compania(CONTRATISTA, 'Servicios Norte', 'CONTRATISTA'),
      ],
      total: 3,
      pagina: 1,
      tamañoPagina: 200,
    })

    vi.spyOn(companiasApi, 'listarRelaciones').mockResolvedValue([relacion(PRINCIPAL_B)])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('Caso A: no ofrece elegir principal cuando la pertenencia es a una Principal', async () => {
    // RF-053: el contexto se fija automáticamente a la propia Principal de pertenencia.
    renderizar()

    await elegirCompaniaAsync('Minera Los Andes', PRINCIPAL_A)

    expect(screen.queryByLabelText('Compañía principal del contexto')).toBeNull()
    expect(screen.getByText(/se abre automáticamente con esa misma principal/i)).toBeInTheDocument()
  })

  it('Caso B: ofrece solo las principales con relación vigente con la contratista', async () => {
    // RF-054: la interfaz no propone una Principal que el servidor rechazaría.
    renderizar()

    await elegirCompaniaAsync('Servicios Norte', CONTRATISTA)

    const selector = await screen.findByLabelText('Compañía principal del contexto')
    const opciones = within(selector).getAllByRole('option')

    expect(opciones.map((o) => o.textContent)).toEqual(['Seleccione…', 'Minera Boreal'])
  })

  it('Caso A: crea la pertenencia y abre el contexto con la misma principal', async () => {
    const crear = vi.spyOn(historyApi, 'crearPertenencia').mockResolvedValue({
      id: 'p1',
      personaId: PERSONA,
      companiaId: PRINCIPAL_A,
      fechaHoraInicio: '2026-01-01T00:00:00Z',
      fechaHoraFin: '2027-01-01T00:00:00Z',
      estado: 'ACTIVA',
      motivoFin: null,
    })

    const abrir = vi.spyOn(historyApi, 'abrirContexto').mockResolvedValue({
      id: 'c1',
      personaId: PERSONA,
      companiaPrincipalId: PRINCIPAL_A,
      fechaHoraInicio: '2026-01-01T00:00:00Z',
      fechaHoraFin: '2027-01-01T00:00:00Z',
      estado: 'ACTIVO',
      motivoFin: null,
      revocadoPorPertenenciaId: null,
    })

    renderizar()

    await elegirCompaniaAsync('Minera Los Andes', PRINCIPAL_A)
    await userEvent.type(screen.getByLabelText('Desde'), '2026-01-01')
    await userEvent.type(screen.getByLabelText('Hasta'), '2027-01-01')

    await userEvent.click(screen.getByRole('button', { name: 'Registrar' }))

    expect(crear.mock.calls[0][1].companiaId).toBe(PRINCIPAL_A)

    // El contexto se abre después de la pertenencia y con la misma Principal (RF-053).
    expect(abrir.mock.calls[0][1].companiaPrincipalId).toBe(PRINCIPAL_A)
  })

  it('exige fecha de fin: no existe pertenencia indefinida', async () => {
    renderizar()

    await elegirCompaniaAsync('Minera Los Andes', PRINCIPAL_A)
    await userEvent.type(screen.getByLabelText('Desde'), '2026-01-01')

    // RF-071: sin fecha de fin la acción no se habilita.
    expect(screen.getByRole('button', { name: 'Registrar' })).toBeDisabled()
  })
})
