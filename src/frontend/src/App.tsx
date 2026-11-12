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
import { PublicLayout } from './pages/site/PublicLayout'
import {
  AboutPage, AppsPage, CareersPage, ContactPage, DepartmentsPage, HomePage, JobDetailPage, NewsDetailPage, NewsListPage,
  PublicAnnouncementsPage, PublicNotFoundPage,
} from './pages/site/PublicPages'
import { ContentAdminPage } from './pages/ContentAdminPage'
import { WidgetsAdminPage } from './pages/WidgetsAdminPage'
import { ApprovalsPage } from './pages/ApprovalsPage'
import { LeavePage } from './pages/LeavePage'
import { TasksPage } from './pages/TasksPage'
import { SocialPage } from './pages/SocialPage'
import { MessagesPage } from './pages/MessagesPage'
import { FaqPage } from './pages/FaqPage'
import { WorkflowsAdminPage } from './pages/WorkflowsAdminPage'



/** Veri yönlendiricisi (data router): kaydedilmemiş form uyarısı için useBlocker'ı mümkün kılar. */
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/forgot-password', element: <ForgotPasswordPage /> },
  { path: '/reset-password', element: <ResetPasswordPage /> },
  { path: '/set-password', element: <SetPasswordPage /> },
  { path: '/oauth/authorize', element: <OAuthAuthorizePage /> },
  // Kurumsal karşılama sitesi (giriş gerektirmez, Türkçe adresler)
  {
    element: <PublicLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'hakkimizda', element: <AboutPage /> },
      { path: 'departmanlarimiz', element: <DepartmentsPage /> },
      { path: 'uygulamalar', element: <AppsPage /> },
      { path: 'duyurular', element: <PublicAnnouncementsPage /> },
      { path: 'haberler', element: <NewsListPage /> },
      { path: 'haberler/:slug', element: <NewsDetailPage /> },
      { path: 'kariyer', element: <CareersPage /> },
      { path: 'kariyer/:id', element: <JobDetailPage /> },
      { path: 'iletisim', element: <ContactPage /> },
      { path: '*', element: <PublicNotFoundPage /> },
    ],
  },
  {
    element: <AppLayout />,
    children: [
      { path: 'portal', element: <DashboardPage /> },
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
      { path: 'admin/content', element: <RequireAuth perms={['content.manage', 'ads.manage', 'employees.manage']}><ContentAdminPage /></RequireAuth> },
      { path: 'admin/widgets', element: <RequireAuth perms={['widgets.manage']}><WidgetsAdminPage /></RequireAuth> },
      { path: 'admin/workflows', element: <RequireAuth perms={['workflows.manage']}><WorkflowsAdminPage /></RequireAuth> },
      { path: 'approvals', element: <ApprovalsPage /> },
      { path: 'tasks', element: <TasksPage /> },
      { path: 'social', element: <SocialPage /> },
      { path: 'messages', element: <MessagesPage /> },
      { path: 'faq', element: <FaqPage /> },
      { path: 'leave', element: <RequireAuth perms={['leave.request', 'leave.manage']}><LeavePage /></RequireAuth> },
      { path: 'portal/*', element: <NotFoundPage /> },
    ],
  },
])
