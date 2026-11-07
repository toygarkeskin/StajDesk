import { Navigate, Outlet, useLocation } from 'react-router'
import clsx from 'clsx'
import { CheckCircle2, X, XCircle } from 'lucide-react'
import { isTokenValid, useAuth } from '@/stores/auth'
import { useUi } from '@/stores/ui'
import type { UserRole } from '@/lib/types'
import { Header } from './Header'
import { Sidebar } from './Sidebar'

/** Oturum yoksa giriş sayfasına yönlendirir; rol yetmiyorsa ana sayfaya döner (asıl yetki kontrolü API'dedir). */
export function RequireAuth({ roles, children }: { roles?: UserRole[]; children: React.ReactNode }) {
  const { token, expiresAt, user } = useAuth()
  const location = useLocation()
  if (!token || !isTokenValid(expiresAt)) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  if (roles && user && !roles.includes(user.role)) return <Navigate to="/" replace />
  return <>{children}</>
}

export function Toasts() {
  const toasts = useUi((s) => s.toasts)
  const dismiss = useUi((s) => s.dismiss)
  return (
    <div aria-live="polite" className="fixed right-4 bottom-4 z-[60] flex w-[calc(100%-2rem)] max-w-sm flex-col gap-2">
      {toasts.map((t) => (
        <div key={t.id} role="status" className="card flex animate-fade-in items-start gap-2 py-2 pr-1 pl-4 text-sm">
          {t.type === 'success'
            ? <CheckCircle2 className="mt-3 size-5 shrink-0 text-emerald-600" aria-hidden="true" />
            : <XCircle className="mt-3 size-5 shrink-0 text-red-600" aria-hidden="true" />}
          <span className={clsx('flex-1 py-3 text-gray-800 dark:text-gray-200')}>{t.message}</span>
          <button className="inline-flex size-11 shrink-0 items-center justify-center rounded-lg text-gray-400 hover:text-gray-700" onClick={() => dismiss(t.id)} aria-label="Bildirimi kapat">
            <X className="size-4" aria-hidden="true" />
          </button>
        </div>
      ))}
    </div>
  )
}

export function AppLayout() {
  return (
    <RequireAuth>
      <div className="flex h-dvh overflow-hidden">
        <Sidebar />
        <div className="flex min-w-0 flex-1 flex-col overflow-x-hidden overflow-y-auto">
          <Header />
          <main className="mx-auto w-full max-w-page px-4 py-8 md:px-8">
            <Outlet />
          </main>
        </div>
      </div>
      <Toasts />
    </RequireAuth>
  )
}
