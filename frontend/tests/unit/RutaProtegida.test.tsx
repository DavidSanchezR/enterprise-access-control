import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { RutaProtegida } from '../../src/app/RutaProtegida'
import { guardarSesion, type SesionAlmacenada } from '../../src/lib/apiClient'

function sesionDe(overrides: Partial<SesionAlmacenada> = {}): SesionAlmacenada {
  return {
    accessToken: 'token',
    expiraEn: '2026-12-31T00:00:00Z',
    rol: 'GLOBAL_ADMINISTRATOR' as const,
    companiaIds: [],
    requiereCambioPassword: false,
    ...overrides,
  }
}

function renderizarEn(ruta: string): void {
  render(
    <MemoryRouter initialEntries={[ruta]}>
      <Routes>
        <Route path="/login" element={<p>Pantalla de login</p>} />
        <Route
          path="/cambiar-password"
          element={
            <RutaProtegida>
              <p>Cambio de contraseña</p>
            </RutaProtegida>
          }
        />
        <Route
          path="/usuarios"
          element={
            <RutaProtegida>
              <p>Pantalla protegida</p>
            </RutaProtegida>
          }
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('RutaProtegida', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('envía al login a quien no tiene sesión', () => {
    renderizarEn('/usuarios')

    expect(screen.getByText('Pantalla de login')).toBeInTheDocument()
    expect(screen.queryByText('Pantalla protegida')).not.toBeInTheDocument()
  })

  it('deja pasar a quien tiene sesión vigente', () => {
    guardarSesion(sesionDe())
    renderizarEn('/usuarios')

    expect(screen.getByText('Pantalla protegida')).toBeInTheDocument()
  })

  it('desvía al cambio de contraseña a quien lo tiene pendiente', () => {
    guardarSesion(sesionDe({ requiereCambioPassword: true }))
    renderizarEn('/usuarios')

    expect(screen.getByText('Cambio de contraseña')).toBeInTheDocument()
    expect(screen.queryByText('Pantalla protegida')).not.toBeInTheDocument()
  })

  it('no entra en bucle cuando ya se está en el cambio de contraseña', () => {
    guardarSesion(sesionDe({ requiereCambioPassword: true }))
    renderizarEn('/cambiar-password')

    expect(screen.getByText('Cambio de contraseña')).toBeInTheDocument()
  })
})
