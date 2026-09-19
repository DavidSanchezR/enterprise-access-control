import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as areasApi from '../../src/features/area-access/api'
import type { AreaAcceso } from '../../src/features/area-access/api'
import * as companiasApi from '../../src/features/companies/api'
import type { Compania } from '../../src/features/companies/api'
import * as unidadesApi from '../../src/features/org-units/api'
import * as personasApi from '../../src/features/people/api'
import * as permisosApi from '../../src/features/permissions/api'
import type { PermisoAcceso } from '../../src/features/permissions/api'
import { validarBloques } from '../../src/features/permissions/bloques'
import { PermisosPage } from '../../src/features/permissions/PermisosPage'

const P1 = '0199b0d0-0000-7000-8000-00000000000a'
const AREA = '0199b0d0-0000-7000-8000-0000000000a1'
const UNIDAD = '0199b0d0-0000-7000-8000-0000000000e1'
const PERSONA = '0199b0d0-0000-7000-8000-0000000000f1'

function principal(): Compania {
  return {
    id: P1,
    nombre: 'Minera Norte',
    tipoDocumentoId: '0199b0d0-0000-7000-8000-0000000000dd',
    numeroDocumento: '20100000001',
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
  }
}

function area(): AreaAcceso {
  return {
    id: AREA,
    nombre: 'Planta',
    areaSuperiorId: null,
    companiaPrincipalId: P1,
    estado: 'ACTIVO',
  }
}

function permiso(parcial: Partial<PermisoAcceso> = {}): PermisoAcceso {
  return {
    id: '0199b0d0-0000-7000-8000-0000000000c1',
    areaAccesoId: AREA,
    alcance: 'UNIDAD_ORGANIZATIVA',
    personaId: null,
    unidadOrganizativaId: UNIDAD,
    companiaId: null,
    fechaHoraInicioVigencia: '2026-09-01T00:00:00.000Z',
    fechaHoraFinVigencia: '2027-08-31T23:59:59.999Z',
    estado: 'ACTIVO',
    bloquesHorarios: [{ id: 'b1', diaSemana: 'LUNES', horaInicio: '08:00', horaFin: '17:00' }],
    ...parcial,
  }
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <PermisosPage />
    </QueryClientProvider>,
  )
}

async function elegirArea(): Promise<void> {
  await screen.findByRole('option', { name: 'Minera Norte' })
  await userEvent.selectOptions(screen.getByLabelText('Compañía principal'), P1)
  await screen.findByRole('option', { name: 'Planta' })
  await userEvent.selectOptions(screen.getByLabelText('Área'), AREA)
}

describe('validarBloques', () => {
  it('rechaza un bloque con fin no posterior al inicio', () => {
    const { porBloque } = validarBloques([
      { diaSemana: 'LUNES', horaInicio: '17:00', horaFin: '08:00' },
    ])

    expect(porBloque[0]).toBe('La hora de fin debe ser posterior a la de inicio.')
  })

  it('detecta bloques solapados del mismo día y acepta los consecutivos', () => {
    const solapados = validarBloques([
      { diaSemana: 'JUEVES', horaInicio: '08:00', horaFin: '13:00' },
      { diaSemana: 'JUEVES', horaInicio: '12:00', horaFin: '17:00' },
    ])
    expect(solapados.porBloque.every((m) => m?.startsWith('Se solapa'))).toBe(true)

    const consecutivos = validarBloques([
      { diaSemana: 'JUEVES', horaInicio: '08:00', horaFin: '12:00' },
      { diaSemana: 'JUEVES', horaInicio: '12:00', horaFin: '17:00' },
    ])
    expect(consecutivos.porBloque).toEqual([null, null])
  })

  it('no considera solapado el mismo horario en días distintos', () => {
    const { porBloque } = validarBloques([
      { diaSemana: 'LUNES', horaInicio: '08:00', horaFin: '17:00' },
      { diaSemana: 'MARTES', horaInicio: '08:00', horaFin: '17:00' },
    ])

    expect(porBloque).toEqual([null, null])
  })

  it('exige al menos un bloque', () => {
    expect(validarBloques([]).general).toBe('El permiso requiere al menos un bloque horario.')
  })
})

describe('PermisosPage', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue({
      items: [principal()],
      total: 1,
      pagina: 1,
      tamañoPagina: 200,
    })
    vi.spyOn(areasApi, 'listarAreas').mockResolvedValue([area()])
    vi.spyOn(unidadesApi, 'listarUnidades').mockResolvedValue([
      {
        id: UNIDAD,
        nombre: 'Operaciones',
        unidadSuperiorId: null,
        companiaPrincipalId: P1,
        estado: 'ACTIVO',
      },
    ])
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
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('no lista permisos hasta elegir una principal y un área', async () => {
    const listar = vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [],
      total: 0,
      pagina: 1,
      tamañoPagina: 200,
    })

    renderizar()

    // ux-ui.md §18: los permisos se muestran siempre dentro del contexto de una Principal.
    expect(
      await screen.findByText(
        'Elija una compañía principal y una de sus áreas para ver y administrar sus permisos.',
      ),
    ).toBeInTheDocument()
    expect(listar).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Nuevo permiso' })).toBeDisabled()
  })

  it('lista los permisos del área con su sujeto, vigencia y horario', async () => {
    const listar = vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [permiso()],
      total: 1,
      pagina: 1,
      tamañoPagina: 200,
    })

    renderizar()
    await elegirArea()

    expect(await screen.findByText('Operaciones')).toBeInTheDocument()
    expect(screen.getByText('Lunes 08:00–17:00')).toBeInTheDocument()
    expect(listar.mock.calls.at(-1)?.[0]).toMatchObject({ areaAccesoId: AREA })
  })

  it('no envía un permiso sin fecha de fin de vigencia', async () => {
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [],
      total: 0,
      pagina: 1,
      tamañoPagina: 200,
    })
    const crear = vi.spyOn(permisosApi, 'crearPermiso')

    renderizar()
    await elegirArea()
    await userEvent.click(screen.getByRole('button', { name: 'Nuevo permiso' }))

    const dialogo = within(screen.getByRole('dialog'))

    fireEvent.change(dialogo.getByLabelText('Inicio de vigencia'), {
      target: { value: '2026-09-01T08:00' },
    })
    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el lunes' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    // RF-021, RF-071: fin obligatorio para los tres alcances.
    expect(
      await dialogo.findByText('Indique el fin de vigencia: es obligatorio.'),
    ).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })

  it('no envía un permiso con bloques solapados en el mismo día', async () => {
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [],
      total: 0,
      pagina: 1,
      tamañoPagina: 200,
    })
    const crear = vi.spyOn(permisosApi, 'crearPermiso')

    renderizar()
    await elegirArea()
    await userEvent.click(screen.getByRole('button', { name: 'Nuevo permiso' }))

    const dialogo = within(screen.getByRole('dialog'))

    await userEvent.click(dialogo.getByRole('radio', { name: 'Unidad organizativa' }))
    await dialogo.findByRole('option', { name: 'Operaciones' })
    await userEvent.selectOptions(
      dialogo.getByRole('combobox', { name: 'Unidad organizativa' }),
      UNIDAD,
    )
    fireEvent.change(dialogo.getByLabelText('Inicio de vigencia'), {
      target: { value: '2026-09-01T08:00' },
    })
    fireEvent.change(dialogo.getByLabelText('Fin de vigencia'), {
      target: { value: '2027-09-01T08:00' },
    })

    // Dos bloques por defecto (08:00–17:00) el mismo lunes: se solapan.
    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el lunes' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el lunes' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(
      await dialogo.findByText('Revise los bloques horarios antes de guardar.'),
    ).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })

  it('crea un permiso de unidad organizativa con el único sujeto de su alcance y vigencia en UTC', async () => {
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [],
      total: 0,
      pagina: 1,
      tamañoPagina: 200,
    })
    const crear = vi.spyOn(permisosApi, 'crearPermiso').mockResolvedValue(permiso())

    renderizar()
    await elegirArea()
    await userEvent.click(screen.getByRole('button', { name: 'Nuevo permiso' }))

    const dialogo = within(screen.getByRole('dialog'))

    await userEvent.click(dialogo.getByRole('radio', { name: 'Unidad organizativa' }))
    await dialogo.findByRole('option', { name: 'Operaciones' })
    await userEvent.selectOptions(
      dialogo.getByRole('combobox', { name: 'Unidad organizativa' }),
      UNIDAD,
    )
    fireEvent.change(dialogo.getByLabelText('Inicio de vigencia'), {
      target: { value: '2026-09-01T08:00' },
    })
    fireEvent.change(dialogo.getByLabelText('Fin de vigencia'), {
      target: { value: '2027-09-01T08:00' },
    })
    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el miércoles' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(crear).toHaveBeenCalledTimes(1)
    expect(crear.mock.calls[0][0]).toEqual({
      areaAccesoId: AREA,
      alcance: 'UNIDAD_ORGANIZATIVA',
      personaId: null,
      unidadOrganizativaId: UNIDAD,
      companiaId: null,
      fechaHoraInicioVigencia: new Date('2026-09-01T08:00').toISOString(),
      fechaHoraFinVigencia: new Date('2027-09-01T08:00').toISOString(),
      estado: 'ACTIVO',
      bloquesHorarios: [{ diaSemana: 'MIERCOLES', horaInicio: '08:00', horaFin: '17:00' }],
    })
  })

  it('al editar no ofrece cambiar el alcance y conserva el sujeto original', async () => {
    const existente = permiso()
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [existente],
      total: 1,
      pagina: 1,
      tamañoPagina: 200,
    })
    const actualizar = vi.spyOn(permisosApi, 'actualizarPermiso').mockResolvedValue(existente)

    renderizar()
    await elegirArea()
    await userEvent.click(
      await screen.findByRole('button', { name: 'Editar permiso de Operaciones' }),
    )

    const dialogo = within(screen.getByRole('dialog'))

    expect(dialogo.queryByRole('radio')).not.toBeInTheDocument()
    expect(dialogo.getByText(/no pueden cambiarse/)).toBeInTheDocument()

    await userEvent.selectOptions(dialogo.getByLabelText('Estado'), 'INACTIVO')
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(actualizar).toHaveBeenCalledTimes(1)
    expect(actualizar.mock.calls[0][0]).toBe(existente.id)
    expect(actualizar.mock.calls[0][1]).toMatchObject({
      alcance: 'UNIDAD_ORGANIZATIVA',
      unidadOrganizativaId: UNIDAD,
      personaId: null,
      companiaId: null,
      estado: 'INACTIVO',
      fechaHoraInicioVigencia: existente.fechaHoraInicioVigencia,
      // El control tiene precisión de minutos: sin tocar la fecha, el fin 23:59:59.999 debe viajar
      // intacto y no truncado a 23:59:00.000.
      fechaHoraFinVigencia: '2027-08-31T23:59:59.999Z',
      bloquesHorarios: [{ diaSemana: 'LUNES', horaInicio: '08:00', horaFin: '17:00' }],
    })
  })

  it('al editar, cambiar el fin de vigencia sí envía la nueva fecha', async () => {
    const existente = permiso()
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [existente],
      total: 1,
      pagina: 1,
      tamañoPagina: 200,
    })
    const actualizar = vi.spyOn(permisosApi, 'actualizarPermiso').mockResolvedValue(existente)

    renderizar()
    await elegirArea()
    await userEvent.click(
      await screen.findByRole('button', { name: 'Editar permiso de Operaciones' }),
    )

    const dialogo = within(screen.getByRole('dialog'))

    fireEvent.change(dialogo.getByLabelText('Fin de vigencia'), {
      target: { value: '2028-01-15T18:30' },
    })
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(actualizar.mock.calls[0][1].fechaHoraFinVigencia).toBe(
      new Date('2028-01-15T18:30').toISOString(),
    )
  })
})
