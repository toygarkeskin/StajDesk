import { Navigate, Outlet, useLocation } from 'react-router'
import clsx from 'clsx'
import { CheckCircle2, X, XCircle } from 'lucide-react'
import { useEffect } from 'react'
import { useAuth, useCan } from '@/stores/auth'
import { useUi } from '@/stores/ui'
import { api, refreshAccessToken } from '@/lib/api'
import type { Permission } from '@/lib/types'
import { Header } from './Header'
import { Sidebar } from './Sidebar'

/**
 * Açılışta oturumu geri yükler: httpOnly cookie'deki refresh token ile yeni erişim token'ı alınır,
 * ardından /api/auth/me ile kullanıcının rolleri ve izinleri yüklenir.
 */
export function useSessionBootstrap() {
  const { restoring, setRestoring, setMe } = useAuth()
  useEffect(() => {
    if (!restoring) return
    let cancelled = false
    void (async () => {
      const token = useAuth.getState().accessToken ?? (await refreshAccessToken())
      if (token && !cancelled) {
        try {
          setMe(await api.auth.me())
        } catch {
          useAuth.getState().clear()
        }
      }
      if (!cancelled) setRestoring(false)
    })()
    return () => {
      cancelled = true
    }
  }, [restoring, setRestoring, setMe])
  return restoring
}

/**
 * Oturum yoksa girişe yönlendirir; gerekli izin yoksa ana sayfaya döner.
 * Bu yalnızca kullanıcı deneyimi içindir — asıl yetki kontrolü API'dedir.
 */
export function RequireAuth({ perms, children }: { perms?: Permission[]; children: React.ReactNode }) {
  const me = useAuth((s) => s.me)
  const restoring = useSessionBootstrap()
  const { can } = useCan()
  const location = useLocation()
  if (restoring) return <div className="flex h-dvh items-center justify-center text-sm text-gray-500" aria-busy="true">Oturum yükleniyor…</div>
  if (!me) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  if (perms && !can(...perms)) return <Navigate to="/portal" replace />
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
