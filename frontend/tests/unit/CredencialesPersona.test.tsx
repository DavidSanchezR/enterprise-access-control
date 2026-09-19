import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as companiasApi from '../../src/features/companies/api'
import * as credencialesApi from '../../src/features/credentials/api'
import type { AsignacionCredencial } from '../../src/features/credentials/api'
import { CredencialesPersona } from '../../src/features/credentials/CredencialesPersona'
import * as maestrosApi from '../../src/features/masters/api'
import * as historialApi from '../../src/features/people/history/api'
import type { ContextoOperativo } from '../../src/features/people/history/api'

const PERSONA = '0199b0d0-9000-7000-8000-000000000001'
const MINERA_NORTE = '0199b0d0-8000-7000-8000-00000000000a'
const MINERA_SUR = '0199b0d0-8000-7000-8000-00000000000b'
const FOTOCHECK = '0199b0d0-7000-7000-8000-0000000000f1'

const ayer = new Date(Date.now() - 86_400_000).toISOString()
const dentroDeUnAno = new Date(Date.now() + 365 * 86_400_000).toISOString()
const haceUnAno = new Date(Date.now() - 365 * 86_400_000).toISOString()
const haceUnMes = new Date(Date.now() - 30 * 86_400_000).toISOString()

function credencial(parcial: Partial<AsignacionCredencial>): AsignacionCredencial {
  return {
    id: 'k1',
    personaId: PERSONA,
    companiaPrincipalId: MINERA_NORTE,
    tipoCredencialId: FOTOCHECK,
    fechaHoraInicio: ayer,
    fechaHoraFin: dentroDeUnAno,
    estado: 'ASIGNADO',
    revocadoPorPertenenciaId: null,
    ...parcial,
  }
}

function contexto(principal: string): ContextoOperativo {
  return {
    id: `c-${principal}`,
    personaId: PERSONA,
    companiaPrincipalId: principal,
    fechaHoraInicio: ayer,
    fechaHoraFin: dentroDeUnAno,
    estado: 'ACTIVO',
    motivoFin: null,
    revocadoPorPertenenciaId: null,
  }
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <CredencialesPersona personaId={PERSONA} />
    </QueryClientProvider>,
  )
}

describe('CredencialesPersona', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue({
      items: [
        {
          id: MINERA_NORTE,
          nombre: 'Minera Norte',
          tipoDocumentoId: 'ruc',
          numeroDocumento: '20100000001',
          tipoCompania: 'PRINCIPAL_MANDANTE',
          estado: 'ACTIVO',
        },
        {
          id: MINERA_SUR,
          nombre: 'Minera Sur',
          tipoDocumentoId: 'ruc',
          numeroDocumento: '20100000002',
          tipoCompania: 'PRINCIPAL_MANDANTE',
          estado: 'ACTIVO',
        },
      ],
      total: 2,
      pagina: 1,
      tamañoPagina: 200,
    })
    vi.spyOn(maestrosApi, 'listarMaestro').mockResolvedValue([
      { id: FOTOCHECK, nombre: 'Credencial Contratista', estado: 'ACTIVO' },
    ])
    vi.spyOn(historialApi, 'listarContextos').mockResolvedValue([contexto(MINERA_NORTE)])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('agrupa el histórico por compañía principal', async () => {
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([
      credencial({ id: 'k1', companiaPrincipalId: MINERA_NORTE }),
      credencial({ id: 'k2', companiaPrincipalId: MINERA_SUR }),
    ])

    renderizar()

    // CS-016/CS-017: credenciales simultáneas de dos Principales, cada una en su propio grupo.
    const norte = await screen.findByRole('region', { name: 'Minera Norte' })
    const sur = screen.getByRole('region', { name: 'Minera Sur' })

    expect(within(norte).getAllByRole('listitem')).toHaveLength(1)
    expect(within(sur).getAllByRole('listitem')).toHaveLength(1)
    expect(within(norte).getByText('Credencial Contratista')).toBeInTheDocument()
  })

  it('muestra estado y vigencia por separado: una asignada ya expirada no está vigente', async () => {
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([
      credencial({ fechaHoraInicio: haceUnAno, fechaHoraFin: haceUnMes }),
    ])

    renderizar()

    // RF-070: el vencimiento no cambia el estado, pero la credencial deja de estar vigente.
    expect(await screen.findByText('Asignada')).toBeInTheDocument()
    expect(screen.getByText('No vigente')).toBeInTheDocument()
  })

  it('solo ofrece acciones sobre credenciales asignadas', async () => {
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([
      credencial({ id: 'k1', estado: 'ASIGNADO' }),
      credencial({ id: 'k2', estado: 'DEVUELTO', fechaHoraInicio: haceUnAno }),
      credencial({ id: 'k3', estado: 'ELIMINADO', fechaHoraInicio: haceUnAno }),
    ])

    renderizar()

    await screen.findByText('Devuelta')

    // Los estados de cierre son terminales (contracts/credentials.yaml).
    expect(screen.getAllByRole('button', { name: 'Devolver' })).toHaveLength(1)
    expect(screen.getAllByRole('button', { name: 'Dar de baja' })).toHaveLength(1)
  })

  it('devuelve una credencial asignada', async () => {
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([credencial({ id: 'k1' })])
    const devolver = vi.spyOn(credencialesApi, 'devolverCredencial').mockResolvedValue()

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Devolver' }))

    expect(devolver).toHaveBeenCalledWith(PERSONA, 'k1')
  })

  it('pide confirmación antes de dar de baja', async () => {
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([credencial({ id: 'k1' })])
    const eliminar = vi.spyOn(credencialesApi, 'eliminarCredencial').mockResolvedValue()

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Dar de baja' }))
    expect(eliminar).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Confirmar baja' }))
    expect(eliminar).toHaveBeenCalledWith(PERSONA, 'k1')
  })

  it('identifica la pertenencia que revocó una credencial en cascada', async () => {
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([
      credencial({ estado: 'REVOCADA', revocadoPorPertenenciaId: 'pertenencia-7' }),
    ])

    renderizar()

    expect(
      await screen.findByText(/Revocada automáticamente al cesar la pertenencia/),
    ).toBeInTheDocument()
    expect(screen.getByText('pertenencia-7')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Devolver' })).not.toBeInTheDocument()
  })

  it('solo ofrece para asignar las principales con contexto operativo vigente', async () => {
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([])

    renderizar()

    const selector = await screen.findByLabelText('Compañía principal')
    await within(selector).findByRole('option', { name: 'Minera Norte' })

    // RF-056: sin contexto con Minera Sur no se puede emitir una credencial para ella.
    expect(within(selector).queryByRole('option', { name: 'Minera Sur' })).not.toBeInTheDocument()
  })

  it('asigna una credencial con la principal, el tipo y la vigencia completa', async () => {
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([])
    const asignar = vi
      .spyOn(credencialesApi, 'asignarCredencial')
      .mockResolvedValue(credencial({ id: 'nueva' }))

    renderizar()

    const selector = await screen.findByLabelText('Compañía principal')
    await within(selector).findByRole('option', { name: 'Minera Norte' })
    await userEvent.selectOptions(selector, MINERA_NORTE)
    await screen.findByRole('option', { name: 'Credencial Contratista' })
    await userEvent.selectOptions(screen.getByLabelText('Tipo de credencial'), FOTOCHECK)
    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-09-16' } })

    // Sin fecha de fin no se puede enviar (RF-071).
    expect(screen.getByRole('button', { name: 'Asignar credencial' })).toBeDisabled()

    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2027-09-15' } })
    await userEvent.click(screen.getByRole('button', { name: 'Asignar credencial' }))

    expect(asignar).toHaveBeenCalledWith(PERSONA, {
      companiaPrincipalId: MINERA_NORTE,
      tipoCredencialId: FOTOCHECK,
      fechaHoraInicio: '2026-09-16T00:00:00.000Z',
      fechaHoraFin: '2027-09-15T00:00:00.000Z',
    })
  })

  it('explica el rechazo por solapamiento con otra credencial de la misma principal', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([])
    vi.spyOn(credencialesApi, 'asignarCredencial').mockRejectedValue(
      new ApiError({ status: 409, detail: 'Solapamiento…', codigo: 'SOLAPAMIENTO_VIGENCIA' }, 409),
    )

    renderizar()

    const selector = await screen.findByLabelText('Compañía principal')
    await within(selector).findByRole('option', { name: 'Minera Norte' })
    await userEvent.selectOptions(selector, MINERA_NORTE)
    await screen.findByRole('option', { name: 'Credencial Contratista' })
    await userEvent.selectOptions(screen.getByLabelText('Tipo de credencial'), FOTOCHECK)
    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-09-16' } })
    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2027-09-15' } })
    await userEvent.click(screen.getByRole('button', { name: 'Asignar credencial' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'ya tiene una credencial asignada para esa compañía principal',
    )
  })
})
