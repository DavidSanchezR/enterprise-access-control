import type { ReactElement } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useSesion } from '../features/auth/useSesion'
import './appShell.css'

/**
 * Marco común de las pantallas autenticadas (ux-ui.md §7): cabecera con identidad y navegación
 * lateral. Cada historia de usuario añade su entrada al menú.
 */
export function AppShell(): ReactElement {
  const { sesion, cerrar } = useSesion()
  const navegar = useNavigate()

  return (
    <div className="shell">
      {/* Salto al contenido: primer elemento enfocable de la página (WCAG 2.2 AA). */}
      <a className="shell-salto" href="#contenido">
        Saltar al contenido
      </a>

      <header className="shell-cabecera">
        <span className="shell-marca">Control de Acceso Empresarial</span>

        <div className="shell-sesion">
          <span className="shell-alcance">
            {sesion?.alcanceCompanias.length ?? 0} compañía
            {sesion?.alcanceCompanias.length === 1 ? '' : 's'} en su alcance
          </span>

          <button
            type="button"
            onClick={() => {
              cerrar()
              navegar('/login', { replace: true })
            }}
          >
            Cerrar sesión
          </button>
        </div>
      </header>

      <div className="shell-cuerpo">
        <nav className="shell-menu" aria-label="Navegación principal">
          <ul>
            <li>
              <NavLink to="/" end>
                Inicio
              </NavLink>
            </li>
            <li>
              <NavLink to="/companias">Compañías</NavLink>
            </li>
            <li>
              <NavLink to="/unidades-organizativas">Unidades organizativas</NavLink>
            </li>
            <li>
              <NavLink to="/areas-acceso">Áreas de acceso</NavLink>
            </li>
            <li>
              <NavLink to="/permisos">Permisos</NavLink>
            </li>
            <li>
              <NavLink to="/evaluacion-acceso">Evaluación de acceso</NavLink>
            </li>
            <li>
              <NavLink to="/personas">Personas</NavLink>
            </li>
            <li>
              <NavLink to="/maestros">Datos maestros</NavLink>
            </li>
            <li>
              <NavLink to="/usuarios">Usuarios</NavLink>
            </li>
          </ul>
        </nav>

        <main className="shell-contenido" id="contenido" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
    </div>
  )
}
