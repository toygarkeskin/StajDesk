import { createBrowserRouter } from 'react-router'
import { AnnouncementsPage, OnCallPage } from './pages/WorkspacePages'
import { AppLayout, RequireAuth } from './components/layout/AppLayout'
import { ForgotPasswordPage, LoginPage, OAuthAuthorizePage, ResetPasswordPage, SetPasswordPage } from './pages/AuthPages'
import { AccountRequestsPage } from './pages/AccountRequestsPage'
import { EmployeesPage, MyProfilePage, ProfilePage } from './pages/PeoplePages'
import { OrgAdminPage, OrgChartPage } from './pages/OrgPages'
import { StoresPage } from './pages/StoresPage'
import { RolesPage } from './pages/RolesPage'
import { AuditPage, EmailsPage } from './pages/SystemPages'
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



/** Veri yönlendiricisi (data router): kaydedilmemiş form uyarısı için useBlocker'ı mümkün kılar. */
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/forgot-password', element: <ForgotPasswordPage /> },
  { path: '/reset-password', element: <ResetPasswordPage /> },
  { path: '/set-password', element: <SetPasswordPage /> },
  { path: '/oauth/authorize', element: <OAuthAuthorizePage /> },
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'tickets', element: <TicketsPage /> },
      { path: 'tickets/new', element: <NewTicketPage /> },
      { path: 'tickets/:id', element: <TicketDetailPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: 'announcements', element: <AnnouncementsPage /> },
      { path: 'oncall', element: <OnCallPage /> },
      { path: 'employees', element: <EmployeesPage /> },
      { path: 'employees/:id', element: <ProfilePage /> },
      { path: 'profile', element: <MyProfilePage /> },
      { path: 'org', element: <OrgChartPage /> },
      { path: 'account-requests', element: <RequireAuth perms={['accounts.request', 'users.manage']}><AccountRequestsPage /></RequireAuth> },
      { path: 'admin/org', element: <RequireAuth perms={['org.manage']}><OrgAdminPage /></RequireAuth> },
      { path: 'assets', element: <RequireAuth perms={['assets.read.all', 'assets.read.store']}><AssetsPage /></RequireAuth> },
      { path: 'assets/new', element: <RequireAuth perms={['assets.manage']}><AssetFormPage /></RequireAuth> },
      { path: 'assets/:id', element: <AssetDetailPage /> },
      { path: 'assets/:id/edit', element: <RequireAuth perms={['assets.manage']}><AssetFormPage /></RequireAuth> },
      { path: 'admin/categories', element: <RequireAuth perms={['categories.manage']}><CategoriesPage /></RequireAuth> },
      { path: 'stores', element: <RequireAuth perms={['stores.read', 'stores.manage']}><StoresPage /></RequireAuth> },
      { path: 'admin/roles', element: <RequireAuth perms={['roles.manage']}><RolesPage /></RequireAuth> },
      { path: 'admin/audit', element: <RequireAuth perms={['audit.read']}><AuditPage /></RequireAuth> },
      { path: 'admin/emails', element: <RequireAuth perms={['mail.read']}><EmailsPage /></RequireAuth> },
      { path: 'admin/users', element: <RequireAuth perms={['users.read', 'users.manage']}><UsersPage /></RequireAuth> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
