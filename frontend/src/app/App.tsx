import { QueryClientProvider } from '@tanstack/react-query'
import type { ReactElement } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { EvaluacionAccesoPage } from '../features/access-evaluation/EvaluacionAccesoPage'
import { AreasAccesoPage } from '../features/area-access/AreasAccesoPage'
import { CambiarPasswordPage } from '../features/auth/CambiarPasswordPage'
import { LoginPage } from '../features/auth/LoginPage'
import { CompaniasPage } from '../features/companies/CompaniasPage'
import { MaestrosPage } from '../features/masters/MaestrosPage'
import { PersonaHistorialPage } from '../features/people/history/PersonaHistorialPage'
import { PersonasPage } from '../features/people/PersonasPage'
import { PermisosPage } from '../features/permissions/PermisosPage'
import { UnidadesOrganizativasPage } from '../features/org-units/UnidadesOrganizativasPage'
import { UsuariosPage } from '../features/users/UsuariosPage'
import { AppShell } from './AppShell'
import { queryClient } from './queryClient'
import { RutaProtegida } from './RutaProtegida'

/**
 * Marcador temporal: cada historia de usuario reemplaza estos destinos por sus pantallas reales
 * (ux-ui.md §7). El shell existe desde la fase Foundational para que las historias solo tengan que
 * registrar su ruta.
 */
function PantallaPendiente({ titulo }: { titulo: string }): ReactElement {
  return (
    <section>
      <h1>{titulo}</h1>
      <p>Pantalla pendiente de implementación en su historia de usuario.</p>
    </section>
  )
}

export function App(): ReactElement {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />

          {/*
            El cambio de contraseña queda fuera del shell y sólo exige sesión: un usuario con la
            contraseña expirada debe poder llegar aquí, pero no navegar al resto de la aplicación.
          */}
          <Route
            path="/cambiar-password"
            element={
              <RutaProtegida>
                <CambiarPasswordPage />
              </RutaProtegida>
            }
          />

          <Route
            element={
              <RutaProtegida>
                <AppShell />
              </RutaProtegida>
            }
          >
            <Route path="/" element={<PantallaPendiente titulo="Dashboard" />} />
            <Route path="/usuarios" element={<UsuariosPage />} />
            <Route path="/companias" element={<CompaniasPage />} />
            <Route path="/unidades-organizativas" element={<UnidadesOrganizativasPage />} />
            <Route path="/areas-acceso" element={<AreasAccesoPage />} />
            <Route path="/permisos" element={<PermisosPage />} />
            <Route path="/evaluacion-acceso" element={<EvaluacionAccesoPage />} />
            {/* Un catálogo por ruta: cada uno conserva su propio enlace compartible. */}
            <Route path="/maestros" element={<Navigate to="/maestros/tipos-documento" replace />} />
            <Route path="/maestros/:catalogo" element={<MaestrosPage />} />
            <Route path="/personas" element={<PersonasPage />} />
            <Route path="/personas/:personaId/historial" element={<PersonaHistorialPage />} />
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
