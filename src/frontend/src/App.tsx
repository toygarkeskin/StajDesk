import { createBrowserRouter } from 'react-router'
import { AppLayout, RequireAuth } from './components/layout/AppLayout'
import { LoginPage, RegisterPage } from './pages/AuthPages'
import { DashboardPage } from './pages/DashboardPage'
import { TicketsPage } from './pages/TicketsPage'
import { TicketDetailPage } from './pages/TicketDetailPage'
import { NewTicketPage } from './pages/NewTicketPage'
import { AssetsPage } from './pages/AssetsPage'
import { AssetDetailPage } from './pages/AssetDetailPage'
import { AssetFormPage } from './pages/AssetFormPage'
import { CategoriesPage } from './pages/CategoriesPage'
import { UsersPage } from './pages/UsersPage'
import { NotificationsPage } from './pages/NotificationsPage'
import { NotFoundPage } from './pages/NotFoundPage'

const staff = ['Agent', 'Admin'] as const
const admin = ['Admin'] as const

/** Veri yönlendiricisi (data router): kaydedilmemiş form uyarısı için useBlocker'ı mümkün kılar. */
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'tickets', element: <TicketsPage /> },
      { path: 'tickets/new', element: <NewTicketPage /> },
      { path: 'tickets/:id', element: <TicketDetailPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: 'assets', element: <RequireAuth roles={[...staff]}><AssetsPage /></RequireAuth> },
      { path: 'assets/new', element: <RequireAuth roles={[...staff]}><AssetFormPage /></RequireAuth> },
      { path: 'assets/:id', element: <AssetDetailPage /> },
      { path: 'assets/:id/edit', element: <RequireAuth roles={[...staff]}><AssetFormPage /></RequireAuth> },
      { path: 'admin/categories', element: <RequireAuth roles={[...admin]}><CategoriesPage /></RequireAuth> },
      { path: 'admin/users', element: <RequireAuth roles={[...admin]}><UsersPage /></RequireAuth> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
