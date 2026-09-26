import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../src/lib/apiClient'
import { fechaDeclarada } from '../../src/lib/fechas'
import * as historialApi from '../../src/features/people/history/api'
import type { AsignacionCompania } from '../../src/features/people/history/api'
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
    // v2.0.0 (VF-004): días completos en America/Lima y las fechas civiles que calcula el servidor.
    fechaHoraInicioVigencia: '2026-09-01T05:00:00.000Z',
    fechaHoraFinVigencia: '2027-09-01T04:59:59.999Z',
    fechaInicioVigencia: '2026-09-01',
    fechaFinVigencia: '2027-08-31',
    vigenciaEnDiasCompletos: true,
    zonaHorariaIana: 'America/Lima',
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

    // VF-004 (F-5): una vigencia de días completos se muestra solo con fechas.
    const fila = screen.getByText('Operaciones').closest('tr')!
    expect(within(fila).getByText(/01\/09\/2026/)).toBeInTheDocument()
    expect(within(fila).getByText(/hasta 31\/08\/2027/)).toBeInTheDocument()
    expect(fila).not.toHaveTextContent(/\d{1,2}:\d{2}.*hasta/)
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
      target: { value: '2026-09-01' },
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
      target: { value: '2026-09-01' },
    })
    fireEvent.change(dialogo.getByLabelText('Fin de vigencia'), {
      target: { value: '2027-09-01' },
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

  it('crea un permiso de unidad organizativa con el único sujeto de su alcance y vigencia en fechas civiles', async () => {
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
      target: { value: '2026-09-01' },
    })
    fireEvent.change(dialogo.getByLabelText('Fin de vigencia'), {
      target: { value: '2027-09-01' },
    })
    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el miércoles' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    // v2.0.0 (VF-004, F-3): fechas civiles AAAA-MM-DD; ya no viajan los campos fechaHora* de v1.x.
    expect(crear).toHaveBeenCalledTimes(1)
    expect(crear.mock.calls[0][0]).toEqual({
      areaAccesoId: AREA,
      alcance: 'UNIDAD_ORGANIZATIVA',
      personaId: null,
      unidadOrganizativaId: UNIDAD,
      companiaId: null,
      fechaInicioVigencia: '2026-09-01',
      fechaFinVigencia: '2027-09-01',
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
      // Sin tocar las fechas, se reenvían las mismas fechas civiles; el servidor conserva los instantes (F-6).
      fechaInicioVigencia: existente.fechaInicioVigencia,
      fechaFinVigencia: existente.fechaFinVigencia,
      bloquesHorarios: [{ diaSemana: 'LUNES', horaInicio: '08:00', horaFin: '17:00' }],
    })
    expect(actualizar.mock.calls[0][1]).not.toHaveProperty('fechaHoraInicioVigencia')
    expect(actualizar.mock.calls[0][1]).not.toHaveProperty('fechaHoraFinVigencia')
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
      target: { value: '2028-01-15' },
    })
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(actualizar.mock.calls[0][1]).toMatchObject({
      fechaInicioVigencia: existente.fechaInicioVigencia,
      fechaFinVigencia: '2028-01-15',
    })
  })

  // --- VF-005 (post-Baseline): placeholder del buscador de persona --------------------------------
  //
  // Gap de UX: el placeholder orienta, pero la etiqueta se conserva (ux-ui.md §23) y la búsqueda sigue
  // siendo la misma (texto libre por nombres, apellidos o documento, RF-035).

  async function abrirAlta(): Promise<ReturnType<typeof within>> {
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [],
      total: 0,
      pagina: 1,
      tamañoPagina: 200,
    })

    renderizar()
    await elegirArea()
    await userEvent.click(screen.getByRole('button', { name: 'Nuevo permiso' }))

    return within(screen.getByRole('dialog'))
  }

  it('el buscador de persona conserva su etiqueta, orienta con el placeholder y sigue buscando por texto', async () => {
    const dialogo = await abrirAlta()

    // Localizarlo por la etiqueta prueba que "Buscar persona" sigue siendo su nombre accesible.
    const buscador = dialogo.getByLabelText('Buscar persona')
    expect(buscador).toHaveAttribute('placeholder', 'Ingrese su nro. de documento')

    await userEvent.type(buscador, '12345678')

    await waitFor(() =>
      expect(personasApi.buscarPersonas).toHaveBeenLastCalledWith(
        expect.objectContaining({ texto: '12345678' }),
      ),
    )
  })

  it('el buscador de persona solo aparece con alcance persona', async () => {
    const dialogo = await abrirAlta()

    expect(dialogo.getByLabelText('Buscar persona')).toBeInTheDocument()

    await userEvent.click(dialogo.getByRole('radio', { name: 'Unidad organizativa' }))

    expect(dialogo.queryByLabelText('Buscar persona')).not.toBeInTheDocument()
  })

  // --- RF-082 (cambio post-Baseline VF-007): contención de permisos por persona -------------------
  //
  // La interfaz solo orienta. Nunca bloquea desactivar ni cambiar solo bloques, y si no puede leer la
  // pertenencia sigue funcionando: decide el servidor (research.md §35.5).

  const DIA = 24 * 60 * 60 * 1000

  function pertenenciaVigente(): AsignacionCompania {
    const inicio = new Date(Date.now() - 30 * DIA).toISOString().slice(0, 10)
    const fin = new Date(Date.now() + 365 * DIA).toISOString().slice(0, 10)

    return {
      id: '0199b0d0-0000-7000-8000-0000000000b1',
      personaId: PERSONA,
      companiaId: P1,
      fechaHoraInicio: `${inicio}T00:00:00.000Z`,
      fechaHoraFin: `${fin}T23:59:59.999Z`,
      estado: 'ACTIVA',
      motivoFin: null,
    }
  }

  /**
   * Permiso por persona anterior a RF-082 y a VF-004: su vigencia excede cualquier pertenencia actual y
   * se guardó con hora (08:00 y 17:00 de Lima), así que no es de días completos.
   */
  function permisoPersonaAnterior(): PermisoAcceso {
    return permiso({
      alcance: 'PERSONA',
      personaId: PERSONA,
      unidadOrganizativaId: null,
      fechaHoraInicioVigencia: '2020-01-01T13:00:00.000Z',
      fechaHoraFinVigencia: '2035-12-31T22:00:00.000Z',
      fechaInicioVigencia: '2020-01-01',
      fechaFinVigencia: '2035-12-31',
      vigenciaEnDiasCompletos: false,
    })
  }

  async function abrirAltaPorPersona(): Promise<ReturnType<typeof within>> {
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [],
      total: 0,
      pagina: 1,
      tamañoPagina: 200,
    })

    renderizar()
    await elegirArea()
    await userEvent.click(screen.getByRole('button', { name: 'Nuevo permiso' }))

    const dialogo = within(screen.getByRole('dialog'))

    await dialogo.findByRole('option', { name: 'Pérez, Ana — 12345678' })
    await userEvent.selectOptions(dialogo.getByRole('combobox', { name: 'Persona' }), PERSONA)

    return dialogo
  }

  async function abrirEdicion(existente: PermisoAcceso): Promise<ReturnType<typeof within>> {
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [existente],
      total: 1,
      pagina: 1,
      tamañoPagina: 200,
    })

    renderizar()
    await elegirArea()
    await userEvent.click(
      await screen.findByRole('button', { name: `Editar permiso de ${PERSONA}` }),
    )

    return within(screen.getByRole('dialog'))
  }

  it('en alcance persona limita las fechas a las que declara la pertenencia vigente', async () => {
    const vigente = pertenenciaVigente()
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockResolvedValue([vigente])

    const dialogo = await abrirAltaPorPersona()

    // VF-004 (RF-083 (c)): la contención es por fecha civil, así que el límite son los días declarados
    // por la pertenencia, sin convertirlos a la zona del navegador (research.md §36.7).
    await waitFor(() =>
      expect(dialogo.getByLabelText('Fin de vigencia')).toHaveAttribute(
        'max',
        fechaDeclarada(vigente.fechaHoraFin),
      ),
    )
    expect(dialogo.getByLabelText('Inicio de vigencia')).toHaveAttribute(
      'min',
      fechaDeclarada(vigente.fechaHoraInicio),
    )
    expect(dialogo.getByLabelText('Fin de vigencia')).toHaveAttribute(
      'max',
      vigente.fechaHoraFin.slice(0, 10),
    )
  })

  it.each([
    [
      'FUERA_DE_CONTENCION_TEMPORAL',
      409,
      'La vigencia de un permiso por persona debe quedar dentro de la pertenencia vigente de esa persona.',
    ],
    [
      'SIN_PERTENENCIA_VIGENTE',
      400,
      'La persona no tiene una pertenencia vigente: no se le puede otorgar, ampliar ni reactivar un permiso por persona.',
    ],
  ])('en alcance persona muestra un mensaje propio para %s', async (codigo, status, mensaje) => {
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockResolvedValue([pertenenciaVigente()])
    vi.spyOn(permisosApi, 'crearPermiso').mockRejectedValue(
      new ApiError({ status, codigo, detail: 'texto técnico del servidor' }, status),
    )

    const dialogo = await abrirAltaPorPersona()

    fireEvent.change(dialogo.getByLabelText('Inicio de vigencia'), {
      target: { value: '2026-10-01' },
    })
    fireEvent.change(dialogo.getByLabelText('Fin de vigencia'), {
      target: { value: '2026-12-01' },
    })
    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el lunes' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(await dialogo.findByText(mensaje)).toBeInTheDocument()
  })

  it('al editar un permiso por persona anterior a RF-082, cambiar solo bloques envía sus fechas intactas', async () => {
    // Aunque la pertenencia vigente fije límites, la interfaz no bloquea una edición que no amplía acceso.
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockResolvedValue([pertenenciaVigente()])
    const existente = permisoPersonaAnterior()
    const actualizar = vi.spyOn(permisosApi, 'actualizarPermiso').mockResolvedValue(existente)

    const dialogo = await abrirEdicion(existente)

    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el martes' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(actualizar).toHaveBeenCalledTimes(1)
    expect(actualizar.mock.calls[0][1]).toMatchObject({
      alcance: 'PERSONA',
      personaId: PERSONA,
      estado: 'ACTIVO',
      fechaInicioVigencia: existente.fechaInicioVigencia,
      fechaFinVigencia: existente.fechaFinVigencia,
    })
    expect(actualizar.mock.calls[0][1].bloquesHorarios).toHaveLength(2)
  })

  it('al editar un permiso por persona, desactivarlo se envía aunque no pueda leerse la pertenencia', async () => {
    // 404: se administra el área, pero no la persona. El formulario sigue funcionando sin límites.
    const historial = vi
      .spyOn(historialApi, 'listarHistorialCompanias')
      .mockRejectedValue(new ApiError({ status: 404, codigo: 'RECURSO_NO_ENCONTRADO' }, 404))
    const existente = permisoPersonaAnterior()
    const actualizar = vi.spyOn(permisosApi, 'actualizarPermiso').mockResolvedValue(existente)

    const dialogo = await abrirEdicion(existente)

    await waitFor(() => expect(historial).toHaveBeenCalled())
    expect(dialogo.getByLabelText('Fin de vigencia')).not.toHaveAttribute('max')

    await userEvent.selectOptions(dialogo.getByLabelText('Estado'), 'INACTIVO')
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(actualizar).toHaveBeenCalledTimes(1)
    expect(actualizar.mock.calls[0][1]).toMatchObject({
      estado: 'INACTIVO',
      fechaInicioVigencia: existente.fechaInicioVigencia,
      fechaFinVigencia: existente.fechaFinVigencia,
    })
  })

  it('en alcance unidad organizativa no consulta la pertenencia ni pone límites', async () => {
    const historial = vi.spyOn(historialApi, 'listarHistorialCompanias')
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [],
      total: 0,
      pagina: 1,
      tamañoPagina: 200,
    })

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

    expect(historial).not.toHaveBeenCalled()
    expect(dialogo.getByLabelText('Fin de vigencia')).not.toHaveAttribute('max')
  })

  // --- VF-004 (post-Baseline, RF-083): vigencia en días civiles completos ---------------------------

  function formatoEnZona(iso: string, zona: string): string {
    return new Intl.DateTimeFormat('es', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: zona,
    }).format(new Date(iso))
  }

  it('los controles de vigencia son de fecha, sin hora', async () => {
    const dialogo = await abrirAlta()

    expect(dialogo.getByLabelText('Inicio de vigencia')).toHaveAttribute('type', 'date')
    expect(dialogo.getByLabelText('Fin de vigencia')).toHaveAttribute('type', 'date')
  })

  describe('con el navegador en otra zona horaria', () => {
    const zonaOriginal = process.env.TZ

    beforeEach(() => {
      process.env.TZ = 'Asia/Tokyo'
    })

    afterEach(() => {
      process.env.TZ = zonaOriginal
    })

    it('un permiso anterior con hora se lista con fecha y hora en la zona de su Principal', async () => {
      const anterior = permisoPersonaAnterior()
      vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
        items: [anterior],
        total: 1,
        pagina: 1,
        tamañoPagina: 200,
      })

      renderizar()
      await elegirArea()

      const fila = (await screen.findByText(PERSONA)).closest('tr')!
      const enLima = formatoEnZona(anterior.fechaHoraInicioVigencia, 'America/Lima')

      // F-5: con hora, porque no es un día completo, y en la zona de la Principal (RF-080), no en la del
      // navegador.
      expect(enLima).not.toBe(new Date(anterior.fechaHoraInicioVigencia).toLocaleString())
      expect(fila).toHaveTextContent(enLima)
      expect(fila).toHaveTextContent(
        `hasta ${formatoEnZona(anterior.fechaHoraFinVigencia, 'America/Lima')}`,
      )
    })
  })

  it('al editar un permiso anterior con hora muestra sus instantes y avisa que conservar una fecha conserva su hora', async () => {
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockResolvedValue([pertenenciaVigente()])
    const existente = permisoPersonaAnterior()

    const dialogo = await abrirEdicion(existente)

    const nota = dialogo.getByRole('note')
    expect(nota).toHaveTextContent('Este permiso tiene una vigencia con hora')
    expect(nota).toHaveTextContent(formatoEnZona(existente.fechaHoraInicioVigencia, 'America/Lima'))
    expect(nota).toHaveTextContent('Si conserva una fecha, se conserva también su hora')
    expect(dialogo.getByLabelText('Inicio de vigencia')).toHaveValue('2020-01-01')
    expect(dialogo.getByLabelText('Fin de vigencia')).toHaveValue('2035-12-31')
  })

  it('al editar un permiso de días completos no muestra la nota de vigencia con hora', async () => {
    vi.spyOn(permisosApi, 'listarPermisos').mockResolvedValue({
      items: [permiso()],
      total: 1,
      pagina: 1,
      tamañoPagina: 200,
    })

    renderizar()
    await elegirArea()
    await userEvent.click(
      await screen.findByRole('button', { name: 'Editar permiso de Operaciones' }),
    )

    expect(within(screen.getByRole('dialog')).queryByRole('note')).not.toBeInTheDocument()
  })

  it('al editar sin cambiar nada reenvía las mismas fechas civiles', async () => {
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
    await userEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Guardar permiso' }),
    )

    expect(actualizar.mock.calls[0][1]).toMatchObject({
      fechaInicioVigencia: '2026-09-01',
      fechaFinVigencia: '2027-08-31',
    })
  })

  it('envía un permiso de un solo día', async () => {
    const crear = vi.spyOn(permisosApi, 'crearPermiso').mockResolvedValue(permiso())
    const dialogo = await abrirAlta()

    await userEvent.click(dialogo.getByRole('radio', { name: 'Unidad organizativa' }))
    await dialogo.findByRole('option', { name: 'Operaciones' })
    await userEvent.selectOptions(
      dialogo.getByRole('combobox', { name: 'Unidad organizativa' }),
      UNIDAD,
    )
    fireEvent.change(dialogo.getByLabelText('Inicio de vigencia'), {
      target: { value: '2026-09-25' },
    })
    fireEvent.change(dialogo.getByLabelText('Fin de vigencia'), { target: { value: '2026-09-25' } })
    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el lunes' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(crear).toHaveBeenCalledTimes(1)
    expect(crear.mock.calls[0][0]).toMatchObject({
      fechaInicioVigencia: '2026-09-25',
      fechaFinVigencia: '2026-09-25',
    })
  })

  it('no envía un permiso con fecha de fin anterior al inicio', async () => {
    const crear = vi.spyOn(permisosApi, 'crearPermiso')
    const dialogo = await abrirAlta()

    await userEvent.click(dialogo.getByRole('radio', { name: 'Unidad organizativa' }))
    await dialogo.findByRole('option', { name: 'Operaciones' })
    await userEvent.selectOptions(
      dialogo.getByRole('combobox', { name: 'Unidad organizativa' }),
      UNIDAD,
    )
    fireEvent.change(dialogo.getByLabelText('Inicio de vigencia'), {
      target: { value: '2026-09-25' },
    })
    fireEvent.change(dialogo.getByLabelText('Fin de vigencia'), { target: { value: '2026-09-24' } })
    await userEvent.click(dialogo.getByRole('button', { name: 'Agregar bloque el lunes' }))
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar permiso' }))

    expect(
      await dialogo.findByText('El fin de vigencia no puede ser anterior al inicio.'),
    ).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })
})
