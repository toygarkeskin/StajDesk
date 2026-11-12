import { useState } from 'react'
import { Link, useLocation } from 'react-router'
import clsx from 'clsx'
import { ChevronDown, PanelLeftClose, PanelLeftOpen, X } from 'lucide-react'
import { useFocusTrap } from '@/lib/hooks'
import { useNavGroups, type NavGroup } from '@/lib/nav'
import { useUi } from '@/stores/ui'
import { IconButton } from '../ui'
import { Logo } from './Logo'

/* =========================================================
   Kenar çubuğu: açılır/kapanır menü grupları (alt menü), masaüstünde ikonlara daraltma.
   Tercihler tarayıcıda saklanır (yalnızca kolaylık; depolama kapalıysa varsayılan kullanılır).
   ========================================================= */

const read = <T,>(key: string, fallback: T): T => {
  try { const v = localStorage.getItem(key); return v ? (JSON.parse(v) as T) : fallback } catch { return fallback }
}
const write = (key: string, value: unknown) => {
  try { localStorage.setItem(key, JSON.stringify(value)) } catch { /* depolama kapalı */ }
}

const itemClass = (active: boolean, compact: boolean) => clsx(
  'flex min-h-11 items-center gap-3 rounded-lg text-sm font-medium transition-colors duration-150',
  compact ? 'justify-center px-0' : 'px-3',
  active
    ? 'bg-linear-to-r from-violet-500/[0.12] to-violet-500/[0.04] text-violet-600 dark:from-violet-500/[0.24] dark:text-violet-300'
    : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-300 dark:hover:bg-gray-700/40 dark:hover:text-white',
)

function NavContent({ onNavigate, compact = false }: { onNavigate: () => void; compact?: boolean }) {
  const groups = useNavGroups()
  const { pathname, search } = useLocation()
  const params = new URLSearchParams(search)
  const [closed, setClosed] = useState<string[]>(() => read('stajdesk-nav-closed', []))
  const toggle = (id: string) => setClosed((c) => { const n = c.includes(id) ? c.filter((x) => x !== id) : [...c, id]; write('stajdesk-nav-closed', n); return n })
  const hasActive = (g: NavGroup) => g.items.some((it) => it.isActive(pathname, params))

  return (
    <nav aria-label="Ana menü" className={compact ? 'space-y-2' : 'space-y-4'}>
      {groups.map((g) => {
        // Etkin sayfanın grubu her zaman açık görünür
        const open = compact || !closed.includes(g.id) || hasActive(g)
        const listId = `nav-${g.id}`
        return (
          <div key={g.id}>
            {compact ? <hr className="mx-3 mb-2 border-gray-200 first:hidden dark:border-gray-700/60" aria-hidden="true" /> : (
              <button type="button" onClick={() => toggle(g.id)} aria-expanded={open} aria-controls={listId}
                className="flex min-h-9 w-full items-center justify-between rounded-md pr-2 pl-3 text-xs font-semibold text-gray-400 uppercase hover:text-gray-600 dark:text-gray-500 dark:hover:text-gray-300">
                {g.title}
                <ChevronDown className={clsx('size-4 transition-transform', !open && '-rotate-90')} aria-hidden="true" />
              </button>
            )}
            {open && (
              <ul id={listId} className="mt-1 space-y-1">
                {g.items.map((it) => {
                  const active = it.isActive(pathname, params)
                  return (
                    <li key={it.to}>
                      <Link to={it.to} onClick={onNavigate} aria-current={active ? 'page' : undefined} className={itemClass(active, compact)}
                        title={compact ? it.label : undefined} aria-label={compact ? it.label : undefined}>
                        <span className={clsx('shrink-0', active ? 'text-violet-500' : 'text-gray-400 dark:text-gray-500')} aria-hidden="true">{it.icon}</span>
                        {!compact && <span className="truncate">{it.label}</span>}
                      </Link>
                    </li>
                  )
                })}
              </ul>
            )}
          </div>
        )
      })}
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

/** Medium ve üstünde sabit (daraltılabilir) kenar çubuğu; dar ekranda aynı menü bir çekmecede açılır. */
export function Sidebar() {
  const open = useUi((s) => s.sidebarOpen)
  const setOpen = useUi((s) => s.setSidebarOpen)
  const drawerRef = useFocusTrap<HTMLDivElement>(open, () => setOpen(false))
  const [compact, setCompact] = useState<boolean>(() => read('stajdesk-sidebar-compact', false))
  const toggleCompact = () => setCompact((c) => { write('stajdesk-sidebar-compact', !c); return !c })

  return (
    <>
      <aside className={clsx('hidden shrink-0 flex-col overflow-y-auto border-r border-gray-200 bg-white py-4 md:flex dark:border-gray-700/60 dark:bg-gray-800', compact ? 'w-20 px-3' : 'w-64 px-4')}>
        <div className={clsx('mb-8', compact ? 'flex justify-center' : 'px-2')}><Logo withText={!compact} /></div>
        <NavContent onNavigate={() => {}} compact={compact} />
        <div className="mt-8 flex flex-1 flex-col">
          {!compact && <HelpCard onNavigate={() => {}} />}
          <button type="button" onClick={toggleCompact} className={clsx('mt-4 flex min-h-11 items-center gap-3 rounded-lg text-sm text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700/40', compact ? 'justify-center' : 'px-3', compact && 'mt-auto')}
            aria-label={compact ? 'Menüyü genişlet' : 'Menüyü daralt'} title={compact ? 'Menüyü genişlet' : undefined}>
            {compact ? <PanelLeftOpen className="size-[18px]" aria-hidden="true" /> : <><PanelLeftClose className="size-[18px]" aria-hidden="true" />Menüyü daralt</>}
          </button>
        </div>
      </aside>

      {open && (
        <div className="fixed inset-0 z-50 md:hidden">
          <div className="absolute inset-0 animate-fade-in bg-gray-900/30" onClick={() => setOpen(false)} aria-hidden="true" />
          <div ref={drawerRef} role="dialog" aria-modal="true" aria-label="Ana menü"
            className="absolute inset-y-0 left-0 flex w-72 max-w-[85vw] flex-col overflow-y-auto border-r border-gray-200 bg-white p-4 dark:border-gray-700/60 dark:bg-gray-800">
            <div className="mb-6 flex items-center justify-between pl-2">
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
