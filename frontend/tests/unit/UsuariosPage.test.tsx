import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from '../../src/features/users/api'
import type { PaginaUsuarios, Usuario } from '../../src/features/users/api'
import { UsuariosPage } from '../../src/features/users/UsuariosPage'

function usuario(overrides: Partial<Usuario> = {}): Usuario {
  return {
    id: '0199b0d0-0000-7000-8000-000000000001',
    correo: 'admin@empresa.cl',
    estado: 'ACTIVO',
    requiereCambioPassword: false,
    alcanceCompanias: ['0199b0d0-0000-7000-8000-0000000000aa'],
    ...overrides,
  }
}

function pagina(items: Usuario[]): PaginaUsuarios {
  return { items, total: items.length, pagina: 1, tamañoPagina: 20 }
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
    // La tabla existe desde el primer render (con la fila "Cargando…"), así que se espera al dato.
    const tabla = (await screen.findByText('admin@empresa.cl')).closest('table')!

    expect(within(tabla).getByText('admin@empresa.cl')).toBeInTheDocument()
    expect(within(tabla).getByText('ACTIVO')).toBeInTheDocument()
    expect(within(tabla).getByText('BLOQUEADO')).toBeInTheDocument()
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

    expect(await screen.findByText('Todavía no hay usuarios registrados.')).toBeInTheDocument()
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

  it('crea un usuario con el alcance indicado', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearUsuario').mockResolvedValue(usuario())

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo usuario' }))

    await userEvent.type(screen.getByLabelText('Correo'), 'nuevo@empresa.cl')
    await userEvent.type(screen.getByLabelText('Contraseña inicial'), 'Contrasena1Segura')
    await userEvent.type(
      screen.getByLabelText('Compañías administrables'),
      '0199b0d0-0000-7000-8000-0000000000aa, 0199b0d0-0000-7000-8000-0000000000bb',
    )

    await userEvent.click(screen.getByRole('button', { name: 'Crear usuario' }))

    // Se comprueba el primer argumento: TanStack Query añade su propio contexto como segundo.
    expect(crear.mock.calls[0][0]).toEqual({
      correo: 'nuevo@empresa.cl',
      passwordInicial: 'Contrasena1Segura',
      companiaIds: ['0199b0d0-0000-7000-8000-0000000000aa', '0199b0d0-0000-7000-8000-0000000000bb'],
    })
  })

  it('no crea un usuario sin ninguna compañía en el alcance', async () => {
    vi.spyOn(api, 'listarUsuarios').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearUsuario')

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo usuario' }))
    await userEvent.type(screen.getByLabelText('Correo'), 'nuevo@empresa.cl')
    await userEvent.type(screen.getByLabelText('Contraseña inicial'), 'Contrasena1Segura')
    await userEvent.click(screen.getByRole('button', { name: 'Crear usuario' }))

    // contracts/users.yaml declara minItems: 1 para companiaIds.
    expect(await screen.findByText('Indique al menos una compañía.')).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })
})
