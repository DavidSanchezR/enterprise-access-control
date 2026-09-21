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
          {/* El alcance GLOBAL no se enumera: mostrar "0 compañías" sería falso (RF-074). */}
          <span className="shell-alcance">
            {sesion?.rol === 'GLOBAL_ADMINISTRATOR'
              ? 'Alcance global: todas las compañías'
              : `${sesion?.companiaIds.length ?? 0} compañía${
                  sesion?.companiaIds.length === 1 ? '' : 's'
                } en su alcance`}
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

            {/* Configuración → Usuarios y roles administrativos (ux-ui.md §7 y §35). La entrada solo
                se ofrece a quien tiene una asignación vigente: sin rol, la API responde 403 y
                mostrarla sería ofrecer una acción que la autorización va a rechazar (§32). */}
            {sesion?.rol !== null && sesion?.rol !== undefined && (
              <li>
                <NavLink to="/usuarios">Usuarios y roles administrativos</NavLink>
              </li>
            )}
          </ul>
        </nav>

        <main className="shell-contenido" id="contenido" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
    </div>
  )
}
