import { Link, useLocation } from 'react-router'
import clsx from 'clsx'
import { Bell, FolderKanban, LayoutDashboard, Mail, MonitorSmartphone, PlusCircle, ScrollText, ShieldCheck, Store as StoreIcon, Tags, Ticket, Users, X } from 'lucide-react'
import type { ReactNode } from 'react'
import { useFocusTrap } from '@/lib/hooks'
import { useCan } from '@/stores/auth'
import { useUi } from '@/stores/ui'
import { IconButton } from '../ui'
import { Logo } from './Logo'

interface Item {
  to: string
  label: string
  icon: ReactNode
  isActive: (pathname: string, search: URLSearchParams) => boolean
}

function NavGroup({ title, items, onNavigate }: { title: string; items: Item[]; onNavigate: () => void }) {
  const { pathname, search } = useLocation()
  const params = new URLSearchParams(search)
  return (
    <div>
      <h3 className="mb-2 pl-3 text-xs font-semibold text-gray-400 uppercase dark:text-gray-500">{title}</h3>
      <ul className="space-y-1">
        {items.map((it) => {
          const active = it.isActive(pathname, params)
          return (
            <li key={it.to}>
              <Link
                to={it.to}
                onClick={onNavigate}
                aria-current={active ? 'page' : undefined}
                className={clsx(
                  'flex min-h-11 items-center gap-3 rounded-lg px-3 text-sm font-medium transition-colors duration-150',
                  active
                    ? 'bg-linear-to-r from-violet-500/[0.12] to-violet-500/[0.04] text-violet-600 dark:from-violet-500/[0.24] dark:text-violet-300'
                    : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-300 dark:hover:bg-gray-700/40 dark:hover:text-white',
                )}
              >
                <span className={clsx('shrink-0', active ? 'text-violet-500' : 'text-gray-400 dark:text-gray-500')} aria-hidden="true">{it.icon}</span>
                {it.label}
              </Link>
            </li>
          )
        })}
      </ul>
    </div>
  )
}

function NavContent({ onNavigate }: { onNavigate: () => void }) {
  // Menü rol adına değil izinlere göre çizilir: yeni bir rol tanımlansa da doğru ekranlar görünür.
  const { can } = useCan()
  const i = 'size-[18px]'
  const isTicketPage = (p: string) => p === '/tickets' || /^\/tickets\/\d+$/.test(p)
  const ticketsLabel = can('tickets.read.all') ? 'Tüm talepler' : can('tickets.read.store') ? 'Mağaza talepleri' : 'Taleplerim'
  const keep = (items: (Item | false)[]) => items.filter(Boolean) as Item[]

  const main = keep([
    { to: '/', label: 'Özet paneli', icon: <LayoutDashboard className={i} />, isActive: (p) => p === '/' },
    { to: '/tickets', label: ticketsLabel, icon: <Ticket className={i} />, isActive: (p, q) => isTicketPage(p) && q.get('mine') !== '1' },
    can('tickets.create') && { to: '/tickets/new', label: 'Yeni talep', icon: <PlusCircle className={i} />, isActive: (p) => p === '/tickets/new' },
    { to: '/notifications', label: 'Bildirimler', icon: <Bell className={i} />, isActive: (p) => p === '/notifications' },
  ])
  const operations = keep([
    can('tickets.manage') && { to: '/tickets?mine=1', label: 'Bana atananlar', icon: <FolderKanban className={i} />, isActive: (p, q) => p === '/tickets' && q.get('mine') === '1' },
    can('assets.read.all', 'assets.read.store') && { to: '/assets', label: 'Envanter', icon: <MonitorSmartphone className={i} />, isActive: (p) => p.startsWith('/assets') },
    can('stores.read', 'stores.manage') && { to: '/stores', label: can('stores.manage') ? 'Mağazalar' : 'Mağazam', icon: <StoreIcon className={i} />, isActive: (p) => p === '/stores' },
    can('users.read', 'users.manage') && !can('users.manage') && { to: '/admin/users', label: 'Mağaza çalışanları', icon: <Users className={i} />, isActive: (p) => p === '/admin/users' },
  ])
  const admin = keep([
    can('users.manage') && { to: '/admin/users', label: 'Kullanıcılar', icon: <Users className={i} />, isActive: (p) => p === '/admin/users' },
    can('roles.manage') && { to: '/admin/roles', label: 'Roller ve izinler', icon: <ShieldCheck className={i} />, isActive: (p) => p === '/admin/roles' },
    can('categories.manage') && { to: '/admin/categories', label: 'Kategoriler', icon: <Tags className={i} />, isActive: (p) => p === '/admin/categories' },
    can('mail.read') && { to: '/admin/emails', label: 'Gönderilen e-postalar', icon: <Mail className={i} />, isActive: (p) => p === '/admin/emails' },
    can('audit.read') && { to: '/admin/audit', label: 'Denetim kayıtları', icon: <ScrollText className={i} />, isActive: (p) => p === '/admin/audit' },
  ])
  return (
    <nav aria-label="Ana menü" className="space-y-8">
      <NavGroup title="Genel" items={main} onNavigate={onNavigate} />
      {operations.length > 0 && <NavGroup title="Operasyon" items={operations} onNavigate={onNavigate} />}
      {admin.length > 0 && <NavGroup title="Yönetim" items={admin} onNavigate={onNavigate} />}
    </nav>
  )
}

function HelpCard({ onNavigate }: { onNavigate: () => void }) {
  return (
    <div className="mt-auto rounded-xl bg-linear-to-br from-violet-500 to-violet-600 p-4 text-white">
      <div className="mb-1 text-sm font-semibold">Yardıma mı ihtiyacınız var?</div>
      <p className="mb-3 text-xs text-violet-100">Sorununuzu birkaç adımda bize iletin, ekibimiz hemen ilgilensin.</p>
      <Link to="/tickets/new" onClick={onNavigate} className="inline-flex min-h-11 items-center rounded-lg bg-white/15 px-3 text-xs font-medium hover:bg-white/25">
        Talep oluştur →
      </Link>
    </div>
  )
}

/** Medium ve üstünde sabit kenar çubuğu; narrow'da aynı menü bir çekmecede açılır. */
export function Sidebar() {
  const open = useUi((s) => s.sidebarOpen)
  const setOpen = useUi((s) => s.setSidebarOpen)
  const drawerRef = useFocusTrap<HTMLDivElement>(open, () => setOpen(false))

  return (
    <>
      <aside className="hidden w-64 shrink-0 flex-col overflow-y-auto border-r border-gray-200 bg-white p-4 md:flex dark:border-gray-700/60 dark:bg-gray-800">
        <div className="mb-10 px-2"><Logo /></div>
        <NavContent onNavigate={() => {}} />
        <div className="mt-8 flex flex-1 flex-col"><HelpCard onNavigate={() => {}} /></div>
      </aside>

      {open && (
        <div className="fixed inset-0 z-50 md:hidden">
          <div className="absolute inset-0 animate-fade-in bg-gray-900/30" onClick={() => setOpen(false)} aria-hidden="true" />
          <div
            ref={drawerRef}
            role="dialog"
            aria-modal="true"
            aria-label="Ana menü"
            className="absolute inset-y-0 left-0 flex w-72 max-w-[85vw] flex-col overflow-y-auto border-r border-gray-200 bg-white p-4 dark:border-gray-700/60 dark:bg-gray-800"
          >
            <div className="mb-8 flex items-center justify-between pl-2">
              <Logo />
              <IconButton label="Menüyü kapat" shortcut="Esc" onClick={() => setOpen(false)}><X className="size-5" /></IconButton>
            </div>
            <NavContent onNavigate={() => setOpen(false)} />
            <div className="mt-8 flex flex-1 flex-col"><HelpCard onNavigate={() => setOpen(false)} /></div>
          </div>
        </div>
      )}
    </>
  )
}
