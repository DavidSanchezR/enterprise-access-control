import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from '../../src/features/users/api'
import type { AsignacionRol, PaginaUsuarios, Usuario } from '../../src/features/users/api'
import * as companiasApi from '../../src/features/companies/api'
import type { Compania } from '../../src/features/companies/api'
import { UsuariosPage } from '../../src/features/users/UsuariosPage'
import type { SesionAlmacenada } from '../../src/lib/apiClient'

const COMPANIA_PROPIA = '0199b0d0-0000-7000-8000-0000000000aa'

function asignacion(overrides: Partial<AsignacionRol> = {}): AsignacionRol {
  return {
    id: '0199b0d0-0000-7000-8000-00000000aa01',
    usuarioId: '0199b0d0-0000-7000-8000-000000000001',
    rol: 'COMPANY_ADMINISTRATOR',
    companiaId: COMPANIA_PROPIA,
    fechaHoraInicio: '2026-01-01T00:00:00Z',
    fechaHoraFin: '2027-01-01T00:00:00Z',
    vigente: true,
    ...overrides,
  }
}

function usuario(overrides: Partial<Usuario> = {}): Usuario {
  return {
    id: '0199b0d0-0000-7000-8000-000000000001',
    correo: 'admin@empresa.cl',
    estado: 'ACTIVO',
    requiereCambioPassword: false,
    asignacionesRol: [asignacion()],
    ...overrides,
  }
}

function pagina(items: Usuario[]): PaginaUsuarios {
  return { items, total: items.length, pagina: 1, tamañoPagina: 20 }
}

function compania(): Compania {
  return {
    id: COMPANIA_PROPIA,
    nombre: 'Minera Propia',
    tipoDocumentoId: '0199b0d0-0001-7000-8000-000000000001',
    numeroDocumento: '20100000001',
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
    zonaHorariaIana: 'America/Lima',
  }
}

/** Sesión del operador. El rol decide qué ofrece la interfaz (RF-076, ux-ui.md §35). */
function sembrarSesion(rol: SesionAlmacenada['rol'], companiaIds: string[] = []): void {
  const sesion: SesionAlmacenada = {
    accessToken: 'token-de-pruebas',
    expiraEn: '2099-01-01T00:00:00Z',
    rol,
    companiaIds,
    requiereCambioPassword: false,
  }

  localStorage.setItem('eac.sesion', JSON.stringify(sesion))
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <UsuariosPage />
    </QueryClientProvider>,
  )
}

describe('UsuariosPage', () => {
  beforeEach(() => {
    localStorage.clear()
    sembrarSesion('GLOBAL_ADMINISTRATOR')

    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue({
      items: [compania()],
      total: 1,
      pagina: 1,
      tamañoPagina: 200,
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('lista los usuarios con su estado en texto, no sólo por color', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(
      pagina([
        usuario(),
        usuario({ id: 'b', correo: 'bloqueado@empresa.cl', estado: 'BLOQUEADO' }),
      ]),
    )

    renderizar()

    // Se consulta dentro de la tabla: el selector de filtro también contiene esos literales.
    const tabla = (await screen.findByText('admin@empresa.cl')).closest('table')!

    expect(within(tabla).getByText('admin@empresa.cl')).toBeInTheDocument()
    expect(within(tabla).getByText('ACTIVO')).toBeInTheDocument()
    expect(within(tabla).getByText('BLOQUEADO')).toBeInTheDocument()
  })

  it('muestra rol y compañía como etiquetas distintas, sin fusionarlas', async () => {
    // ux-ui.md §35: rol, compañía y vigencia son tres dimensiones independientes.
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([usuario()]))

    renderizar()

    const fila = (await screen.findByText('admin@empresa.cl')).closest('tr')!

    expect(within(fila).getByText('Administrador de compañía')).toBeInTheDocument()
    expect(within(fila).getByText('Minera Propia')).toBeInTheDocument()
  })

  it('un alcance global se muestra como "Todas" y no como una compañía en blanco', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(
      pagina([
        usuario({
          asignacionesRol: [asignacion({ rol: 'GLOBAL_ADMINISTRATOR', companiaId: null })],
        }),
      ]),
    )

    renderizar()

    const fila = (await screen.findByText('admin@empresa.cl')).closest('tr')!

    expect(within(fila).getByText('Administrador global')).toBeInTheDocument()
    expect(within(fila).getByText('Todas')).toBeInTheDocument()
  })

  it('ofrece desbloquear sólo a los usuarios BLOQUEADOS', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(
      pagina([
        usuario({ id: 'a', correo: 'activo@empresa.cl', estado: 'ACTIVO' }),
        usuario({ id: 'b', correo: 'bloqueado@empresa.cl', estado: 'BLOQUEADO' }),
      ]),
    )

    renderizar()

    const filaBloqueado = (await screen.findByText('bloqueado@empresa.cl')).closest('tr')!
    const filaActivo = screen.getByText('activo@empresa.cl').closest('tr')!

    expect(within(filaBloqueado).getByRole('button', { name: 'Desbloquear' })).toBeInTheDocument()
    expect(within(filaActivo).queryByRole('button', { name: 'Desbloquear' })).toBeNull()
  })

  it('desbloquea al pulsar el botón de la fila', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(
      pagina([usuario({ id: 'b', correo: 'bloqueado@empresa.cl', estado: 'BLOQUEADO' })]),
    )
    const desbloquear = vi.spyOn(api, 'desbloquearUsuario').mockResolvedValue()

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Desbloquear' }))

    expect(desbloquear.mock.calls[0][0]).toBe('b')
  })

  it('señala quién debe cambiar su contraseña', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(
      pagina([usuario({ requiereCambioPassword: true })]),
    )

    renderizar()

    expect(await screen.findByText(/debe cambiar su contraseña/)).toBeInTheDocument()
  })

  it('muestra un estado vacío explicativo cuando no hay resultados', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([]))

    renderizar()

    expect(
      await screen.findByText('No hay usuarios que coincidan con los filtros aplicados.'),
    ).toBeInTheDocument()
  })

  it('filtra por estado y vuelve a la primera página', async () => {
    const listar = vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([usuario()]))

    renderizar()
    await screen.findByText('admin@empresa.cl')

    await userEvent.selectOptions(screen.getByLabelText('Estado'), 'BLOQUEADO')

    expect(listar).toHaveBeenLastCalledWith(
      expect.objectContaining({ estado: 'BLOQUEADO', pagina: 1 }),
    )
  })

  it('crea un usuario con su primera asignación de rol', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearUsuario').mockResolvedValue(usuario())

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo usuario' }))

    // Las consultas se acotan al diálogo: la paginación del listado también tiene "Siguiente".
    const wizard = within(screen.getByRole('dialog'))

    // Paso 1 — identidad.
    await userEvent.type(wizard.getByLabelText('Correo'), 'nuevo@empresa.cl')
    await userEvent.type(wizard.getByLabelText('Contraseña inicial'), 'Contrasena1Segura')
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    // Paso 2 — rol: un GLOBAL_ADMINISTRATOR puede elegir cualquiera de los dos (RF-076).
    await userEvent.click(wizard.getByLabelText('Administrador global'))
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    // Paso 3 — el alcance global no admite compañía: el campo no se ofrece (RF-074).
    expect(wizard.queryByLabelText('Compañía')).toBeNull()
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    // Paso 4 — vigencia obligatoria por ambos extremos (RF-075).
    await userEvent.type(wizard.getByLabelText('Inicio de vigencia'), '2026-01-01T08:00')
    await userEvent.type(wizard.getByLabelText('Fin de vigencia'), '2027-01-01T08:00')
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    await userEvent.click(wizard.getByRole('button', { name: 'Crear usuario' }))

    // Se comprueba el primer argumento: TanStack Query añade su propio contexto como segundo.
    const enviado = crear.mock.calls[0][0]

    expect(enviado.correo).toBe('nuevo@empresa.cl')
    expect(enviado.rol).toBe('GLOBAL_ADMINISTRATOR')
    expect(enviado.companiaId).toBeNull()
    expect(enviado.fechaHoraInicio).not.toBe('')
    expect(enviado.fechaHoraFin).not.toBe('')
  })

  // --- VF-011 (post-Baseline): la confirmación muestra el nombre de la compañía, nunca su id ----------

  /** Recorre el alta hasta la confirmación. `elegirCompania` indica si el paso 3 ofrece selección. */
  async function avanzarHastaConfirmacion(
    wizard: ReturnType<typeof within>,
    { elegirRolCompania, elegirCompania }: { elegirRolCompania: boolean; elegirCompania: boolean },
  ): Promise<void> {
    await userEvent.type(wizard.getByLabelText('Correo'), 'nuevo@empresa.cl')
    await userEvent.type(wizard.getByLabelText('Contraseña inicial'), 'Contrasena1Segura')
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    if (elegirRolCompania) {
      await userEvent.click(wizard.getByLabelText('Administrador de compañía'))
    }
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    if (elegirCompania) {
      await wizard.findByRole('option', { name: 'Minera Propia' })
      await userEvent.selectOptions(wizard.getByLabelText('Compañía'), COMPANIA_PROPIA)
    }
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    await userEvent.type(wizard.getByLabelText('Inicio de vigencia'), '2026-01-01T08:00')
    await userEvent.type(wizard.getByLabelText('Fin de vigencia'), '2027-01-01T08:00')
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))
  }

  it('la confirmación muestra el nombre de la compañía y no su identificador', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearUsuario').mockResolvedValue(usuario())

    renderizar()
    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo usuario' }))

    const wizard = within(screen.getByRole('dialog'))
    await avanzarHastaConfirmacion(wizard, { elegirRolCompania: true, elegirCompania: true })

    // RF-013 y ux-ui.md §35 paso 5: se presenta la compañía, no su UUID.
    expect(wizard.getByText('Minera Propia')).toBeInTheDocument()
    expect(wizard.queryByText(COMPANIA_PROPIA)).not.toBeInTheDocument()

    // El dato interno no cambia: el alta sigue enviando el identificador.
    await userEvent.click(wizard.getByRole('button', { name: 'Crear usuario' }))
    expect(crear.mock.calls[0][0].companiaId).toBe(COMPANIA_PROPIA)
  })

  it('si la compañía no puede resolverse, la confirmación nunca muestra su identificador', async () => {
    // Un COMPANY_ADMINISTRATOR con una sola compañía la tiene preseleccionada, pero la lista no la
    // incluye (p. ej. dejó de estar activa): el resumen muestra un texto, no el UUID.
    const OTRA = '0199b0d0-0000-7000-8000-0000000000bb'
    sembrarSesion('COMPANY_ADMINISTRATOR', [OTRA])
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([]))

    renderizar()
    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo usuario' }))

    const wizard = within(screen.getByRole('dialog'))
    await avanzarHastaConfirmacion(wizard, { elegirRolCompania: false, elegirCompania: false })

    expect(wizard.getByText('Compañía no disponible')).toBeInTheDocument()
    expect(wizard.queryByText(OTRA)).not.toBeInTheDocument()
  })

  it('no avanza del primer paso sin correo ni contraseña', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearUsuario')

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo usuario' }))

    const wizard = within(screen.getByRole('dialog'))
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    expect(await wizard.findByText('Indique el correo.')).toBeInTheDocument()
    expect(wizard.getByText('Indique la contraseña inicial.')).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })

  it('un COMPANY_ADMINISTRATOR no puede elegir el rol global', async () => {
    // RF-076: la opción no disponible no se ofrece, ni siquiera deshabilitada (ux-ui.md §35).
    sembrarSesion('COMPANY_ADMINISTRATOR', [COMPANIA_PROPIA])

    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([]))

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo usuario' }))

    const wizard = within(screen.getByRole('dialog'))

    await userEvent.type(wizard.getByLabelText('Correo'), 'nuevo@empresa.cl')
    await userEvent.type(wizard.getByLabelText('Contraseña inicial'), 'Contrasena1Segura')
    await userEvent.click(wizard.getByRole('button', { name: 'Siguiente' }))

    expect(wizard.queryByLabelText('Administrador global')).toBeNull()
    expect(wizard.getByLabelText('Administrador de compañía')).toBeInTheDocument()
  })

  it('la asignación de rol declara que agrega y no reemplaza', async () => {
    // UX-19: el efecto sobre el alcance es acumulativo mientras varias estén vigentes (RF-077).
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([usuario()]))

    renderizar()

    const fila = (await screen.findByText('admin@empresa.cl')).closest('tr')!
    await userEvent.click(within(fila).getByRole('button', { name: 'Asignar rol' }))

    const dialogo = within(await screen.findByRole('dialog'))
    expect(dialogo.getByText(/Esta operación/i)).toBeInTheDocument()
    expect(dialogo.getByRole('button', { name: 'Agregar asignación' })).toBeInTheDocument()
  })

  // --- UX-22: búsqueda server-side (cierre de la desviación D-4) ----------------------------

  it('envía la búsqueda por correo al servidor y vuelve a la primera página', async () => {
    const listar = vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([usuario()]))

    renderizar()
    await screen.findByText('admin@empresa.cl')

    await userEvent.type(screen.getByLabelText('Correo'), 'buscado')

    // Lo decisivo es que el texto viaje al backend: filtrarlo en el cliente solo alcanzaría a la
    // página ya recibida, que es exactamente lo que D-4 vino a corregir.
    expect(listar).toHaveBeenLastCalledWith(
      expect.objectContaining({ texto: 'buscado', pagina: 1 }),
    )
  })

  it('no filtra por correo en el cliente: muestra lo que el servidor devuelve', async () => {
    // El servidor ya aplicó la búsqueda. Si la página volviera a filtrar, este usuario —que no
    // contiene el texto buscado— desaparecería, y el resultado del servidor quedaría contradicho.
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(
      pagina([usuario({ correo: 'resultado.del.servidor@empresa.cl' })]),
    )

    renderizar()
    await screen.findByText('resultado.del.servidor@empresa.cl')

    await userEvent.type(screen.getByLabelText('Correo'), 'zzz-no-coincide')

    expect(screen.getByText('resultado.del.servidor@empresa.cl')).toBeInTheDocument()
  })

  it('omite el parámetro de búsqueda cuando el campo queda vacío', async () => {
    const listar = vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([usuario()]))

    renderizar()
    await screen.findByText('admin@empresa.cl')

    await userEvent.type(screen.getByLabelText('Correo'), 'a')
    await userEvent.clear(screen.getByLabelText('Correo'))

    // Un `texto` vacío no es un filtro: se omite para no consultar con una cadena en blanco.
    expect(listar).toHaveBeenLastCalledWith(expect.objectContaining({ texto: undefined }))
  })
})
