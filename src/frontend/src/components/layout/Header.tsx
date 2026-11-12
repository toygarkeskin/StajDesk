import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { Link } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Bell, ChevronDown, LogOut, Menu, Moon, Search, Sun } from 'lucide-react'
import { api } from '@/lib/api'
import { usePopover } from '@/lib/hooks'
import { timeAgo } from '@/lib/format'
import { revoke } from '@/lib/oauth'
import { useRealtime, type RealtimeStatus } from '@/lib/realtime'
import { useAuth } from '@/stores/auth'
import { useUi } from '@/stores/ui'
import { Avatar, IconButton, Tooltip } from '../ui'

function NotificationsMenu({ live }: { live: boolean }) {
  const pop = usePopover()
  const qc = useQueryClient()
  const navigate = useNavigate()

  // Anlık bağlantı açıkken sinyal gelince yenilenir; bağlantı yoksa 15 sn'de bir sorulur (yedek yol).
  const unread = useQuery({ queryKey: ['notifications', 'unread'], queryFn: api.notifications.unreadCount, refetchInterval: live ? 60_000 : 15_000 })
  const list = useQuery({ queryKey: ['notifications', 'menu'], queryFn: () => api.notifications.list({ pageSize: 6 }), enabled: pop.open })
  const markAll = useMutation({ mutationFn: api.notifications.markAllRead, onSuccess: () => qc.invalidateQueries({ queryKey: ['notifications'] }) })

  const count = unread.data ?? 0
  const label = count > 0 ? `Bildirimleri aç (${count} okunmamış)` : 'Bildirimleri aç'
  return (
    <div className="relative" ref={pop.rootRef}>
      <Tooltip label={label}>
        <button
          ref={pop.triggerRef}
          type="button"
          aria-label={label}
          aria-expanded={pop.open}
          onClick={() => pop.setOpen(!pop.open)}
          className={clsx('relative inline-flex size-11 items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-700/50', pop.open && 'bg-gray-200 dark:bg-gray-800')}
        >
          <Bell className="size-[18px]" aria-hidden="true" />
          {count > 0 && (
            <span className="absolute top-1 right-1 flex min-w-5 items-center justify-center rounded-full border-2 border-gray-100 bg-rose-500 px-1 text-xs leading-4 font-semibold text-white dark:border-gray-900" aria-hidden="true">
              {count > 9 ? '9+' : count}
            </span>
          )}
        </button>
      </Tooltip>
      {pop.open && (
        <div role="dialog" aria-label="Bildirimler" className="card absolute right-0 z-40 mt-2 w-[min(24rem,calc(100vw-2rem))] animate-fade-in overflow-hidden">
          <div className="flex items-center justify-between border-b border-gray-200 px-4 py-2 dark:border-gray-700/60">
            <span className="text-sm font-medium text-gray-900 dark:text-gray-100">Bildirimler</span>
            {count > 0 && (
              <button className="min-h-11 text-sm font-medium text-gray-800 hover:text-violet-600 dark:text-gray-100" onClick={() => markAll.mutate()}>
                Tümünü okundu yap
              </button>
            )}
          </div>
          <ul className="max-h-96 overflow-y-auto">
            {list.data?.items.length === 0 && <li className="px-4 py-8 text-center text-sm text-gray-600 dark:text-gray-400">Yeni bildirim yok. Talepleriniz güncellendiğinde burada görünür.</li>}
            {list.data?.items.map((n) => (
              <li key={n.id} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                <button
                  className={clsx('flex w-full gap-2 border-l-2 px-4 py-2 text-left hover:bg-gray-50 dark:hover:bg-gray-700/20', n.isRead ? 'border-transparent' : 'border-violet-500')}
                  onClick={async () => {
                    pop.close(false)
                    if (!n.isRead) await api.notifications.markRead(n.id)
                    qc.invalidateQueries({ queryKey: ['notifications'] })
                    if (n.ticketId) navigate(`/tickets/${n.ticketId}`)
                  }}
                >
                  <span className="min-w-0 flex-1">
                    <span className={clsx('block text-sm', n.isRead ? 'text-gray-700 dark:text-gray-300' : 'font-semibold text-gray-900 dark:text-gray-100')}>{n.title}</span>
                    <span className="block text-sm text-gray-600 dark:text-gray-400">{n.message}</span>
                    <span className="mt-1 block text-xs text-gray-500">{timeAgo(n.createdAt)}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
          <Link to="/notifications" onClick={() => pop.close(false)} className="flex min-h-11 items-center justify-center border-t border-gray-200 text-sm font-medium text-violet-700 hover:bg-gray-50 dark:border-gray-700/60 dark:text-violet-300 dark:hover:bg-gray-700/20">
            Tüm bildirimleri gör
          </Link>
        </div>
      )}
    </div>
  )
}

function UserMenu() {
  const pop = usePopover()
  const user = useAuth((s) => s.me)
  const clear = useAuth((s) => s.clear)
  const navigate = useNavigate()
  const qc = useQueryClient()
  if (!user) return null

  return (
    <div className="relative" ref={pop.rootRef}>
      <button ref={pop.triggerRef} type="button" aria-expanded={pop.open} aria-label={`Hesap menüsünü aç: ${user.fullName}`} className="flex min-h-11 items-center gap-2 rounded-lg px-2 hover:bg-gray-100 dark:hover:bg-gray-700/50" onClick={() => pop.setOpen(!pop.open)}>
        <Avatar name={user.fullName} />
        <span className="hidden max-w-40 truncate text-sm font-medium text-gray-800 md:block dark:text-gray-200">{user.fullName}</span>
        <ChevronDown className="hidden size-4 text-gray-400 md:block" aria-hidden="true" />
      </button>
      {pop.open && (
        <div role="menu" className="card absolute right-0 z-40 mt-2 w-64 animate-fade-in py-2">
          <div className="border-b border-gray-200 px-4 pb-2 dark:border-gray-700/60">
            <div className="text-sm font-medium text-gray-900 dark:text-gray-100">{user.fullName}</div>
            <div className="text-xs text-gray-500">{user.email} · {user.roles.join(', ')}</div>
          </div>
          <button
            role="menuitem"
            className="mt-2 flex min-h-11 w-full items-center gap-2 px-4 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-700/20"
            onClick={async () => {
              await revoke() // refresh token sunucuda iptal edilir, cookie silinir
              clear()
              qc.clear()
              navigate('/login')
            }}
          >
            <LogOut className="size-4" aria-hidden="true" /> Oturumu kapat
          </button>
        </div>
      )}
    </div>
  )
}

const liveText: Record<RealtimeStatus, string> = {
  live: 'Anlık bildirimler açık',
  connecting: 'Anlık bildirim bağlantısı kuruluyor',
  offline: 'Anlık bağlantı yok; bildirimler 15 saniyede bir yenileniyor',
}

function LiveDot({ status }: { status: RealtimeStatus }) {
  return (
    <Tooltip label={liveText[status]}>
      <span role="status" aria-label={liveText[status]} tabIndex={0} className="inline-flex size-11 items-center justify-center rounded-full">
        <span className={clsx('size-2.5 rounded-full', status === 'live' ? 'bg-emerald-500' : status === 'connecting' ? 'bg-amber-400' : 'bg-gray-400')} />
      </span>
    </Tooltip>
  )
}

export function Header() {
  const setSidebarOpen = useUi((s) => s.setSidebarOpen)
  const theme = useUi((s) => s.theme)
  const toggleTheme = useUi((s) => s.toggleTheme)
  const navigate = useNavigate()
  const [q, setQ] = useState("")
  const searchRef = useRef<HTMLInputElement>(null)
  const realtime = useRealtime()

  // Ctrl+K / ⌘K: aramaya odaklan
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        if (searchRef.current?.offsetParent) searchRef.current.focus()
        else navigate('/tickets')
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [navigate])

  const onSearch = (e: FormEvent) => {
    e.preventDefault()
    navigate(`/tickets?search=${encodeURIComponent(q.trim())}`)
  }

  return (
    <header className="sticky top-0 z-30 border-b border-gray-200 bg-white/80 backdrop-blur-md dark:border-gray-700/60 dark:bg-gray-900/80">
      <div className="flex h-16 items-center justify-between gap-3 px-4 md:px-8">
        <div className="flex flex-1 items-center gap-3">
          <div className="md:hidden">
            <IconButton label="Menüyü aç" onClick={() => setSidebarOpen(true)}><Menu className="size-6" /></IconButton>
          </div>
          <form role="search" onSubmit={onSearch} className="relative hidden w-full max-w-sm md:block">
            <label htmlFor="header-search" className="sr-only">Talep ara</label>
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-gray-400" aria-hidden="true" />
            <input ref={searchRef} id="header-search" type="search" value={q} onChange={(e) => setQ(e.target.value)} className="form-field pr-16 pl-9" placeholder="Talep ara (başlık, açıklama veya #numara)…" />
            <kbd className="pointer-events-none absolute top-1/2 right-3 hidden -translate-y-1/2 rounded border border-gray-200 px-1.5 text-[11px] font-medium text-gray-400 lg:block dark:border-gray-700">Ctrl K</kbd>
          </form>
        </div>
        <div className="flex items-center gap-1">
          <div className="md:hidden">
            <IconButton label="Taleplerde ara" onClick={() => navigate('/tickets')}><Search className="size-[18px]" /></IconButton>
          </div>
          <LiveDot status={realtime} />
          <NotificationsMenu live={realtime === 'live'} />
          <IconButton label={theme === "dark" ? "Açık temaya geç" : "Koyu temaya geç"} onClick={toggleTheme}>
            {theme === "dark" ? <Sun className="size-[18px]" /> : <Moon className="size-[18px]" />}
          </IconButton>
          <hr className="mx-2 h-6 w-px border-none bg-gray-200 dark:bg-gray-700/60" />
          <UserMenu />
        </div>
      </div>
    </header>
  )
}
