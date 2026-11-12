import { useEffect, useLayoutEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { ArrowRight, Mail, MapPin, Menu, Phone, X } from 'lucide-react'
import { siteApi, siteKeys } from '@/lib/site'
import { useUi } from '@/stores/ui'
import { useFocusTrap } from '@/lib/hooks'

/* =========================================================
   Kurumsal karşılama sitesi (giriş öncesi). Açık tema sabittir; portalın koyu tema tercihi burada uygulanmaz.
   Lacivert üst menü, açık zemin, kart düzeni. Kişisel veri gösterilmez (KVKK kararı).
   ========================================================= */

export const publicNav = [
  { to: '/', label: 'Ana Sayfa', end: true },
  { to: '/hakkimizda', label: 'Hakkımızda' },
  { to: '/departmanlarimiz', label: 'Departmanlarımız' },
  { to: '/uygulamalar', label: 'Uygulamalar' },
  { to: '/duyurular', label: 'Duyurular' },
  { to: '/haberler', label: 'Haberler' },
  { to: '/kariyer', label: 'Kariyer' },
  { to: '/iletisim', label: 'İletişim' },
]

export const useSiteInfo = () => useQuery({ queryKey: ['site', 'info'], queryFn: siteApi.info, staleTime: 5 * 60_000 })

/** Sekme başlığı: "Sayfa · Şirket adı". */
export function usePageTitle(title: string | undefined) {
  const company = useSiteInfo().data?.settings[siteKeys.companyName] ?? 'StajDesk'
  useEffect(() => {
    document.title = title ? `${title} · ${company}` : company
  }, [title, company])
}

function Brand({ name, light = true }: { name: string; light?: boolean }) {
  return (
    <Link to="/" className="inline-flex min-h-11 items-center gap-2" aria-label={`${name} ana sayfa`}>
      <svg width="30" height="30" viewBox="0 0 32 32" aria-hidden="true">
        <rect width="32" height="32" rx="8" fill="#6366f1" />
        <path d="M9 11h14v3H9zM9 16.5h10v3H9zM9 22h6v3H9z" fill="#fff" />
      </svg>
      <span className={clsx('text-base font-bold whitespace-nowrap', light ? 'text-white' : 'text-slate-900')}>StajDesk</span>
    </Link>
  )
}

export function PublicLayout() {
  const theme = useUi((s) => s.theme)
  const [open, setOpen] = useState(false)
  const location = useLocation()
  const drawerRef = useFocusTrap<HTMLDivElement>(open, () => setOpen(false))
  const info = useSiteInfo().data
  const s = info?.settings ?? {}
  const company = s[siteKeys.companyName] ?? 'StajDesk'

  // Kurumsal site her zaman açık tema; portala dönünce kullanıcının tercihi geri gelir.
  useLayoutEffect(() => {
    document.documentElement.classList.remove('dark')
    return () => { document.documentElement.classList.toggle('dark', useUi.getState().theme === 'dark') }
  }, [theme])

  useEffect(() => { setOpen(false); window.scrollTo(0, 0) }, [location.pathname])

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    clsx('inline-flex min-h-11 items-center rounded-md px-3 text-sm font-medium whitespace-nowrap transition-colors',
      isActive ? 'bg-white/10 text-white' : 'text-slate-300 hover:bg-white/5 hover:text-white')

  return (
    <div className="flex min-h-dvh flex-col bg-slate-50 text-slate-600">
      <a href="#icerik" className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded focus:bg-white focus:px-3 focus:py-2 focus:text-slate-900">İçeriğe geç</a>
      <header className="sticky top-0 z-40 bg-slate-900 shadow-sm">
        <div className="mx-auto flex h-16 max-w-7xl items-center gap-4 px-4 sm:px-6">
          <Brand name={company} />
          <nav aria-label="Site menüsü" className="ml-4 hidden flex-1 items-center gap-0.5 xl:flex">
            {publicNav.map((n) => <NavLink key={n.to} to={n.to} end={n.end} className={linkClass}>{n.label}</NavLink>)}
          </nav>
          <div className="ml-auto flex items-center gap-2">
            <Link to="/portal" className="inline-flex min-h-11 items-center gap-1.5 rounded-lg bg-indigo-500 px-4 text-sm font-semibold whitespace-nowrap text-white hover:bg-indigo-400">
              Portala Git <ArrowRight className="size-4" aria-hidden="true" />
            </Link>
            <button type="button" className="grid size-11 place-items-center rounded-lg text-slate-200 hover:bg-white/10 xl:hidden" onClick={() => setOpen(true)} aria-label="Menüyü aç" aria-expanded={open}>
              <Menu className="size-5" aria-hidden="true" />
            </button>
          </div>
        </div>
      </header>

      {open && (
        <div className="fixed inset-0 z-50 xl:hidden">
          <div className="absolute inset-0 bg-slate-950/50" onClick={() => setOpen(false)} aria-hidden="true" />
          <div ref={drawerRef} role="dialog" aria-modal="true" aria-label="Site menüsü" className="absolute inset-y-0 right-0 flex w-72 max-w-[85%] flex-col bg-slate-900 p-4">
            <div className="mb-4 flex items-center justify-between">
              <Brand name={company} />
              <button type="button" onClick={() => setOpen(false)} className="grid size-11 place-items-center rounded-lg text-slate-200 hover:bg-white/10" aria-label="Menüyü kapat"><X className="size-5" aria-hidden="true" /></button>
            </div>
            <nav aria-label="Site menüsü (mobil)" className="flex flex-col gap-1">
              {publicNav.map((n) => <NavLink key={n.to} to={n.to} end={n.end} className={linkClass}>{n.label}</NavLink>)}
            </nav>
          </div>
        </div>
      )}

      <main id="icerik" className="flex-1">
        <Outlet />
      </main>

      <footer className="mt-12 bg-slate-900 text-slate-400">
        <div className="mx-auto grid max-w-7xl gap-8 px-4 py-10 sm:px-6 md:grid-cols-3">
          <div>
            <Brand name={company} />
            <p className="mt-3 max-w-xs text-sm">{s[siteKeys.tagline]}</p>
          </div>
          <div>
            <h2 className="mb-3 text-sm font-semibold text-white">Site haritası</h2>
            <ul className="grid grid-cols-2 gap-x-4 text-sm">
              {publicNav.map((n) => <li key={n.to}><Link to={n.to} className="inline-flex min-h-9 items-center hover:text-white">{n.label}</Link></li>)}
            </ul>
          </div>
          <div>
            <h2 className="mb-3 text-sm font-semibold text-white">İletişim</h2>
            <ul className="space-y-2 text-sm">
              {s[siteKeys.address] && <li className="flex gap-2"><MapPin className="mt-0.5 size-4 shrink-0" aria-hidden="true" />{s[siteKeys.address]}</li>}
              {s[siteKeys.phone] && <li className="flex gap-2"><Phone className="mt-0.5 size-4 shrink-0" aria-hidden="true" /><a href={`tel:${s[siteKeys.phone].replace(/\s/g, '')}`} className="hover:text-white">{s[siteKeys.phone]}</a></li>}
              {s[siteKeys.email] && <li className="flex gap-2"><Mail className="mt-0.5 size-4 shrink-0" aria-hidden="true" /><a href={`mailto:${s[siteKeys.email]}`} className="break-all hover:text-white">{s[siteKeys.email]}</a></li>}
            </ul>
          </div>
        </div>
        <div className="border-t border-white/10">
          <div className="mx-auto flex max-w-7xl flex-wrap justify-between gap-2 px-4 py-4 text-xs sm:px-6">
            <span>© {new Date().getFullYear()} {company}</span>
            <span>Portal yalnızca şirket çalışanlarına açıktır.</span>
          </div>
        </div>
      </footer>
    </div>
  )
}
