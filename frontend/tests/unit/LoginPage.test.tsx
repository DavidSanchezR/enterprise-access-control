import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LoginPage } from '../../src/features/auth/LoginPage'
import { leerSesion } from '../../src/lib/apiClient'
import * as api from '../../src/features/auth/api'
import { ApiError } from '../../src/lib/apiClient'

function renderizar(): ReactElement | void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<p>Inicio</p>} />
          <Route path="/cambiar-password" element={<p>Cambio obligatorio</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('LoginPage', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('exige correo y contraseña antes de llamar a la API', async () => {
    const login = vi.spyOn(api, 'login')
    renderizar()

    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByText('Indique su correo.')).toBeInTheDocument()
    expect(screen.getByText('Indique su contraseña.')).toBeInTheDocument()
    expect(login).not.toHaveBeenCalled()
  })

  it('rechaza un correo con formato inválido sin llamar a la API', async () => {
    const login = vi.spyOn(api, 'login')
    renderizar()

    await userEvent.type(screen.getByLabelText('Correo'), 'no-es-un-correo')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Contrasena1Segura')
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByText('El correo no tiene un formato válido.')).toBeInTheDocument()
    expect(login).not.toHaveBeenCalled()
  })

  it('guarda la sesión y navega al inicio tras un login correcto', async () => {
    vi.spyOn(api, 'login').mockResolvedValue({
      accessToken: 'token-de-prueba',
      expiraEn: '2026-12-31T00:00:00Z',
      requiereCambioPassword: false,
      rol: 'COMPANY_ADMINISTRATOR',
      companiaIds: ['0199b0d0-0000-7000-8000-000000000001'],
    })

    renderizar()

    await userEvent.type(screen.getByLabelText('Correo'), 'admin@empresa.cl')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Contrasena1Segura')
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByText('Inicio')).toBeInTheDocument()

    const sesion = leerSesion()
    expect(sesion?.accessToken).toBe('token-de-prueba')
    expect(sesion?.rol).toBe('COMPANY_ADMINISTRATOR')
    expect(sesion?.companiaIds).toEqual(['0199b0d0-0000-7000-8000-000000000001'])
  })

  it('lleva al cambio de contraseña cuando el servidor lo exige', async () => {
    vi.spyOn(api, 'login').mockResolvedValue({
      accessToken: 'token-de-prueba',
      expiraEn: '2026-12-31T00:00:00Z',
      requiereCambioPassword: true,
      rol: 'GLOBAL_ADMINISTRATOR',
      companiaIds: [],
    })

    renderizar()

    await userEvent.type(screen.getByLabelText('Correo'), 'admin@empresa.cl')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Contrasena1Segura')
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    expect(await screen.findByText('Cambio obligatorio')).toBeInTheDocument()
  })

  it('muestra un mensaje genérico ante credenciales inválidas', async () => {
    vi.spyOn(api, 'login').mockRejectedValue(
      new ApiError(
        {
          status: 401,
          detail: 'Correo o contraseña incorrectos.',
          codigo: 'CREDENCIALES_INVALIDAS',
        },
        401,
      ),
    )

    renderizar()

    await userEvent.type(screen.getByLabelText('Correo'), 'admin@empresa.cl')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Incorrecta9X')
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    const alerta = await screen.findByRole('alert')
    expect(alerta).toHaveTextContent('Correo o contraseña incorrectos.')

    // Un login fallido no deja rastro de sesión.
    await waitFor(() => expect(leerSesion()).toBeNull())
  })

  it('muestra el motivo que da el servidor cuando la cuenta no está habilitada', async () => {
    vi.spyOn(api, 'login').mockRejectedValue(
      new ApiError(
        {
          status: 403,
          detail:
            'El usuario está bloqueado por intentos fallidos. Requiere desbloqueo administrativo.',
          codigo: 'USUARIO_NO_HABILITADO',
        },
        403,
      ),
    )

    renderizar()

    await userEvent.type(screen.getByLabelText('Correo'), 'bloqueado@empresa.cl')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Contrasena1Segura')
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    // A diferencia del 401, aquí sí se muestra el detalle: el usuario necesita saber que debe
    // pedir un desbloqueo, no reintentar la contraseña.
    expect(await screen.findByRole('alert')).toHaveTextContent('desbloqueo administrativo')
  })
})
