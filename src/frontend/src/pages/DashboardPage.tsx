import { Fragment, useDeferredValue, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArcElement, BarElement, CategoryScale, Chart as ChartJS, Filler, Legend, LinearScale, LineElement, PointElement, Tooltip } from 'chart.js'
import { Bar, Doughnut, Line } from 'react-chartjs-2'
import clsx from 'clsx'
import {
  Activity as ActivityIcon, AlertOctagon, CalendarClock, CheckCheck, ClipboardList, Hourglass, Mail, Megaphone, Phone, Pin,
  LayoutGrid, Plus, Search, Settings2, ShieldAlert, Timer, UserRoundSearch, Users,
} from 'lucide-react'
import { api, errorDetails, errorMessage } from '@/lib/api'
import { colorOf, priorityLabels, statusLabels } from '@/lib/labels'
import { formatDate, initials, timeAgo } from '@/lib/format'
import type { Activity, Announcement, AnnouncementLevel, DashboardStats, Home, OnCallShift, TicketPriority, TicketStatus } from '@/lib/types'
import { useCan } from '@/stores/auth'
import { toast, useUi } from '@/stores/ui'
import { Avatar, BlockSkeleton, ErrorState, PriorityText, StatusBadge } from '@/components/ui'
import { NumberTile, Widget, WidgetEmpty } from '@/components/widgets'
import { GoldCard, RatesCard, WeatherCard } from '@/components/LiveCards'
import { AdCard } from '@/components/Ads'
import { siteApi } from '@/lib/site'
import { portalApi, sizeClass } from '@/lib/portal'
import {
  ApprovalInboxWidget, BirthdaysWidget, DashboardEditor, LeaveBalanceWidget, EventsWidget, LayoutError, NewHiresWidget, OpenPositionsWidget, PendingAccountsWidget, ProfileWidget, RecognitionsWidget,
} from '@/components/PortalWidgets'

ChartJS.register(ArcElement, BarElement, CategoryScale, LinearScale, LineElement, PointElement, Tooltip, Legend, Filler)

/*
 * Dashboard kalıbı (portal düzeni): yoğun, eşit aralıklı kutucuk ızgarası.
 *  1. sıra  Karşılama · Duyurular · BT nöbetçi personel
 *  2. sıra  Birlikte geçen süre + durum sayaçları (kur kutucukları gibi büyük rakamlar)
 *  3. sıra  Onayımı bekleyenler · Görevlerim / Taleplerim · Son hareketler
 *  4. sıra  Çalışan ara · son 14 gün grafiği
 *  5. sıra  Dağılım grafikleri
 * Ekran daraldıkça 3 → 2 → 1 sütuna iner; hiçbir kutu yatay kaydırma üretmez.
 */

const STATUS_COLORS = ['#0ea5e9', '#f59e0b', '#8b5cf6', '#10b981', '#9ca3af']
const reducedMotion = () => typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches

function useChartTheme() {
  const dark = useUi((s) => s.theme) === 'dark'
  return { grid: dark ? 'rgba(55,65,81,0.6)' : '#f3f4f6', tick: dark ? '#9ca3af' : '#6b7280', card: dark ? '#1f2937' : '#ffffff' }
}

export function DashboardPage() {
  const { can } = useCan()
  const home = useQuery({ queryKey: ['home'], queryFn: api.home, refetchInterval: 120_000 })
  const stats = useQuery({ queryKey: ['stats'], queryFn: api.stats.dashboard, refetchInterval: 120_000 })
  const layout = useQuery({ queryKey: ['dashboard', 'layout'], queryFn: portalApi.layout, staleTime: 5 * 60_000 })
  const [editing, setEditing] = useState(false)

  if (home.isError) return <div className="card"><ErrorState message={errorMessage(home.error)} details={errorDetails(home.error)} onRetry={() => home.refetch()} /></div>
  if (home.isLoading || !home.data) {
    return (
      <div className="grid grid-cols-12 gap-4" aria-busy="true">
        {Array.from({ length: 3 }).map((_, i) => <BlockSkeleton key={i} className="col-span-12 h-44 md:col-span-6 xl:col-span-4" />)}
        {Array.from({ length: 6 }).map((_, i) => <BlockSkeleton key={`t${i}`} className="col-span-6 h-28 md:col-span-4 xl:col-span-2" />)}
        {Array.from({ length: 3 }).map((_, i) => <BlockSkeleton key={`l${i}`} className="col-span-12 h-64 md:col-span-6 xl:col-span-4" />)}
      </div>
    )
  }

  const h = home.data
  const s = stats.data
  const isStaff = can('tickets.manage')
  const widgets = layout.data?.widgets ?? []

  /** Kutucuk anahtarı → bileşen. Görünürlük (izin + rol + kişisel tercih) sunucuda hesaplanır; burada yalnızca çizilir. */
  const render = (key: string, cls: string): ReactNode => {
    switch (key) {
      case 'welcome': return <WelcomeWidget h={h} className={cls} />
      case 'profile': return <ProfileWidget className={cls} />
      case 'leave-balance': return <LeaveBalanceWidget className={cls} />
      case 'approval-inbox': return <ApprovalInboxWidget className={cls} />
      case 'announcements': return <AnnouncementsWidget items={h.announcements} canManage={can('announcements.manage')} className={cls} />
      case 'oncall': return <OnCallWidget shifts={h.onCall} canManage={can('oncall.manage')} className={cls} />
      case 'weather': return <WeatherCard className={cls} />
      case 'rates': return <RatesCard className={cls} />
      case 'gold': return <GoldCard className={cls} />
      case 'portal-ads': return <PortalAds className={cls} />
      case 'ticket-counters': return (
        <div className={clsx(cls, 'space-y-4')}>
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-6">
            <TogetherTile since={h.welcome.memberSince} />
            {s ? (
              <>
                <NumberTile label="Açık" value={s.open} accent="bg-sky-500" hint={`${s.scopeLabel} · toplam ${s.total}`} to="/tickets?status=Open" />
                <NumberTile label="İşlemde" value={s.inProgress} accent="bg-amber-500" hint="Üzerinde çalışılıyor" to="/tickets?status=InProgress" />
                <NumberTile label="Beklemede" value={s.pending} accent="bg-violet-500" hint="Kullanıcı / parça bekleniyor" to="/tickets?status=Pending" />
                <NumberTile label="Çözüldü" value={s.resolved} accent="bg-emerald-500" hint={`Kapanan: ${s.closed}`} to="/tickets?status=Resolved" />
                {isStaff
                  ? <NumberTile label="Atanmamış" value={s.unassigned} accent="bg-rose-500" hint={`Kritik açık: ${s.criticalOpen}`} to="/tickets?unassigned=true&activeOnly=true" />
                  : <NumberTile label="Cihazlarım" value={h.counters.myAssets} accent="bg-teal-500" hint="Size zimmetli" to={can('assets.read.all', 'assets.read.store') ? '/assets' : undefined} />}
              </>
            ) : Array.from({ length: 5 }).map((_, i) => <BlockSkeleton key={i} className="h-28" />)}
          </div>
          {isStaff && s && s.criticalOpen > 0 && (
            <div role="status" className="flex flex-wrap items-center gap-3 rounded-xl bg-rose-500/10 px-4 py-1 text-sm text-rose-700 dark:text-rose-400">
              <AlertOctagon className="size-5 shrink-0" aria-hidden="true" />
              <span className="flex-1">{s.criticalOpen} adet kritik öncelikli açık talep var.</span>
              <Link to="/tickets?priority=Critical&activeOnly=true" className="inline-flex min-h-11 items-center font-medium underline">Kritik talepleri aç</Link>
            </div>
          )}
        </div>
      )
      case 'approvals': return <ApprovalsWidget items={h.awaitingApproval} className={cls} />
      case 'tasks': return isStaff
        ? <TicketListWidget title="Görevlerim" icon={<ClipboardList className="size-4" />} items={h.myTasks} empty="Size atanmış açık talep yok. Atanmamış taleplerden birini üzerinize alabilirsiniz." action={{ label: 'Tümü', to: '/tickets?mine=1' }} className={cls} />
        : <TicketListWidget title="Son taleplerim" icon={<ClipboardList className="size-4" />} items={s?.recent ?? []} empty="Henüz talep açmadınız." action={{ label: 'Tümü', to: '/tickets' }} className={cls} />
      case 'activity': return <ActivityWidget items={h.activity} className={cls} />
      case 'birthdays': return <BirthdaysWidget className={cls} />
      case 'new-hires': return <NewHiresWidget className={cls} />
      case 'recognitions': return <RecognitionsWidget className={cls} />
      case 'events': return <EventsWidget className={cls} />
      case 'open-positions': return <OpenPositionsWidget className={cls} />
      case 'people-search': return <PeopleSearchWidget className={cls} />
      case 'pending-accounts': return <PendingAccountsWidget className={cls} />
      case 'ticket-trend': return s ? <TrendWidget s={s} className={cls} /> : <BlockSkeleton className={clsx(cls, 'h-64')} />
      case 'ticket-charts': return s ? <ChartsRow s={s} isStaff={isStaff} /> : null
      default: return null // bilinmeyen anahtar (ör. yeni sürümde eklenmiş) sessizce atlanır
    }
  }

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold">Ana sayfa</h1>
        <div className="flex gap-2">
          {can('widgets.manage') && <Link to="/admin/widgets" className="btn-secondary"><Settings2 className="size-4" aria-hidden="true" /> Kutucukları yönet</Link>}
          <button type="button" className="btn-secondary" disabled={!layout.data} onClick={() => setEditing(true)}><LayoutGrid className="size-4" aria-hidden="true" /> Panelimi düzenle</button>
        </div>
      </div>
      {layout.isError ? <LayoutError error={layout.error} onRetry={() => layout.refetch()} /> : widgets.length === 0 && layout.data ? (
        <div className="card p-8 text-center text-sm text-gray-500">
          Tüm kutucukları gizlediniz. <button type="button" className="font-medium text-violet-600 dark:text-violet-400" onClick={() => setEditing(true)}>Panelimi düzenle</button>
        </div>
      ) : (
        <div className="grid grid-cols-12 gap-4">
          {widgets.map((w) => <Fragment key={w.key}>{render(w.key, sizeClass[w.size])}</Fragment>)}
        </div>
      )}
      {editing && layout.data && <DashboardEditor layout={layout.data} onClose={() => setEditing(false)} />}
    </>
  )
}

/* =========================================================
   Karşılama
   ========================================================= */

function WelcomeWidget({ h, className }: { h: Home; className?: string }) {
  const w = h.welcome
  const hour = new Date().getHours()
  const greeting = hour < 12 ? 'Günaydın' : hour < 18 ? 'İyi günler' : 'İyi akşamlar'
  const c = h.counters
  return (
    <Widget title="Karşılama" className={className} bodyClassName="flex flex-col">
      <div className="flex items-center gap-3">
        <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-violet-500 text-base font-semibold text-white" aria-hidden="true">{initials(w.fullName)}</span>
        <div className="min-w-0">
          <p className="truncate text-base font-bold text-gray-900 dark:text-gray-50">{greeting}, {w.fullName}!</p>
          <p className="truncate text-xs text-gray-500">{[w.roles.join(', '), w.storeName ?? w.department].filter(Boolean).join(' · ')}</p>
        </div>
      </div>
      <p className="mt-3 text-sm text-gray-600 dark:text-gray-300">StajDesk'e hoş geldiniz. Bir sorun yaşadığınızda talep açın; durumunu buradan takip edin.</p>
      <div className="mt-auto flex flex-wrap gap-2 pt-4">
        <Link to="/tickets/new" className="btn-violet"><Plus className="size-4" aria-hidden="true" /> Yeni talep</Link>
        <Link to="/notifications" className="btn-secondary">Bildirimler{c.unreadNotifications > 0 && <span className="rounded-full bg-rose-500 px-1.5 text-xs text-white">{c.unreadNotifications}</span>}</Link>
      </div>
      <p className="mt-3 flex items-center gap-1.5 border-t border-gray-100 pt-3 text-xs text-gray-500 dark:border-gray-700/60">
        <CheckCheck className="size-4 shrink-0" aria-hidden="true" />
        {c.awaitingMyApproval > 0 ? `${c.awaitingMyApproval} talep onayınızı bekliyor.` : 'Şu anda onayınızı bekleyen talep bulunmuyor.'}
        {c.myOpenTickets > 0 && ` Açık talebiniz: ${c.myOpenTickets}.`}
      </p>
    </Widget>
  )
}

/* =========================================================
   Duyurular
   ========================================================= */

const levelStyle: Record<AnnouncementLevel, { bar: string; label: string; badge: string }> = {
  Info: { bar: 'bg-sky-500', label: 'Bilgi', badge: 'bg-sky-500/10 text-sky-700 dark:text-sky-300' },
  Warning: { bar: 'bg-amber-500', label: 'Önemli', badge: 'bg-amber-500/10 text-amber-700 dark:text-amber-300' },
  Critical: { bar: 'bg-rose-500', label: 'Acil', badge: 'bg-rose-500/10 text-rose-700 dark:text-rose-300' },
}

export function AnnouncementItem({ a, clamp = true }: { a: Announcement; clamp?: boolean }) {
  const lv = levelStyle[a.level]
  return (
    <article className="relative flex gap-3">
      <span className={clsx('w-1 shrink-0 rounded-full', lv.bar)} aria-hidden="true" />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-1.5">
          {a.isPinned && <Pin className="size-3.5 text-gray-400" aria-label="Sabitlenmiş" />}
          <h3 className="text-sm font-semibold text-gray-800 dark:text-gray-100">{a.title}</h3>
          {a.level !== 'Info' && <span className={clsx('rounded px-1.5 text-[11px] font-medium', lv.badge)}>{lv.label}</span>}
        </div>
        <p className={clsx('mt-0.5 text-sm whitespace-pre-line text-gray-600 dark:text-gray-300', clamp && 'line-clamp-2')}>{a.body}</p>
        <p className="mt-1 text-xs text-gray-500">{a.authorName} · {timeAgo(a.publishAt)}{a.storeName && ` · ${a.storeName}`}</p>
      </div>
    </article>
  )
}

function AnnouncementsWidget({ items, canManage, className }: { items: Announcement[]; canManage: boolean; className?: string }) {
  return (
    <Widget title="Duyurular" icon={<Megaphone className="size-4" />} action={{ label: canManage ? 'Yönet' : 'Tümü', to: '/announcements' }} className={className}>
      {items.length === 0 ? (
        <WidgetEmpty icon={<Megaphone className="size-8" />} text="Şu anda yayında duyuru bulunmuyor." />
      ) : (
        <ul className="space-y-4">
          {items.slice(0, 3).map((a) => <li key={a.id}><AnnouncementItem a={a} /></li>)}
        </ul>
      )}
    </Widget>
  )
}

/* =========================================================
   BT nöbetçi personel
   ========================================================= */

const dayFmt = new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' })
export const formatDay = (d: string) => dayFmt.format(new Date(`${d}T00:00:00`))

export function OnCallCard({ s, caption }: { s: OnCallShift; caption: string }) {
  return (
    <div className="min-w-0">
      <p className="mb-2 text-xs text-gray-500">{caption}</p>
      <div className="flex items-start gap-3">
        <Avatar name={s.fullName} size="lg" />
        <div className="min-w-0 text-sm">
          <p className="truncate font-semibold text-gray-800 uppercase dark:text-gray-100">{s.fullName}</p>
          {s.phone && (
            <a href={`tel:${s.phone.replace(/\s/g, '')}`} className="flex min-h-8 items-center gap-1.5 whitespace-nowrap text-gray-600 hover:text-violet-600 dark:text-gray-300">
              <Phone className="size-3.5 shrink-0" aria-hidden="true" /> {s.phone}
            </a>
          )}
          <a href={`mailto:${s.email}`} className="flex min-h-8 min-w-0 items-center gap-1.5 text-gray-600 hover:text-violet-600 dark:text-gray-300">
            <Mail className="size-3.5 shrink-0" aria-hidden="true" /> <span className="truncate">{s.email}</span>
          </a>
          <p className="text-xs text-gray-500">{formatDay(s.startsOn)} – {formatDay(s.endsOn)}{s.note && ` · ${s.note}`}</p>
        </div>
      </div>
    </div>
  )
}

function OnCallWidget({ shifts, canManage, className }: { shifts: OnCallShift[]; canManage: boolean; className?: string }) {
  const current = shifts.find((s) => s.isCurrent)
  const next = shifts.find((s) => !s.isCurrent && s !== current)
  return (
    <Widget title="BT Nöbetçi Personel" icon={<ShieldAlert className="size-4" />} action={{ label: canManage ? 'Çizelgeyi düzenle' : 'Çizelge', to: '/oncall' }} className={className}>
      {!current && !next ? (
        <WidgetEmpty icon={<CalendarClock className="size-8" />} text="Bu hafta için nöbetçi atanmadı. Acil durumda talep açın." />
      ) : (
        <div className="space-y-3">
          {current ? <OnCallCard s={current} caption="Bu haftaki nöbetçi bilgi işlem sorumlunuz" /> : <p className="text-sm text-gray-500">Bu hafta nöbetçi yok.</p>}
          {next && (
            <p className="flex min-w-0 items-center gap-2 border-t border-gray-100 pt-3 text-xs text-gray-500 dark:border-gray-700/60">
              <CalendarClock className="size-4 shrink-0" aria-hidden="true" />
              <span className="truncate">Sonraki nöbet: <b className="font-medium text-gray-700 dark:text-gray-200">{next.fullName}</b> · {formatDay(next.startsOn)} – {formatDay(next.endsOn)}</span>
            </p>
          )}
        </div>
      )}
    </Widget>
  )
}

/* =========================================================
   Birlikte geçen süre
   ========================================================= */

function TogetherTile({ since }: { since: string }) {
  const start = new Date(since)
  const now = new Date()
  let months = (now.getFullYear() - start.getFullYear()) * 12 + (now.getMonth() - start.getMonth())
  if (now.getDate() < start.getDate()) months--
  const anchor = new Date(start)
  anchor.setMonth(start.getMonth() + months)
  const days = Math.max(0, Math.floor((now.getTime() - anchor.getTime()) / 86_400_000))
  return (
    <NumberTile
      label="Birlikte geçen süre"
      accent="bg-gray-400"
      value={<>{months > 0 && <>{months}<span className="mx-1 text-sm font-normal text-gray-500">Ay</span></>}{days}</>}
      unit="Gün"
      hint={`Başlangıç: ${formatDate(since)}`}
    />
  )
}

/* =========================================================
   Onayımı bekleyenler (çözülen talepler) — tek tıkla kapatma
   ========================================================= */

function ApprovalsWidget({ items, className }: { items: Home['awaitingApproval']; className?: string }) {
  const qc = useQueryClient()
  const close = useMutation({
    mutationFn: (id: number) => api.tickets.setStatus(id, 'Closed'),
    onSuccess: (t) => {
      toast(`#${t.id} onaylandı ve kapatıldı.`)
      qc.invalidateQueries({ queryKey: ['home'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
      qc.invalidateQueries({ queryKey: ['tickets'] })
    },
    onError: (err) => toast(errorMessage(err), 'error'),
  })
  return (
    <Widget title="Onayımı bekleyenler" icon={<Hourglass className="size-4" />} className={className}>
      {items.length === 0 ? (
        <WidgetEmpty icon={<CheckCheck className="size-8" />} text="Onayınızı bekleyen talep bulunmuyor." />
      ) : (
        <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {items.map((t) => (
            <li key={t.id} className="flex flex-wrap items-center gap-2 py-2 first:pt-0 last:pb-0">
              <Link to={`/tickets/${t.id}`} className="min-w-0 flex-1 text-sm">
                <span className="block truncate font-medium text-gray-800 hover:text-violet-600 dark:text-gray-100"><span className="text-gray-400">#{t.id}</span> {t.title}</span>
                <span className="block text-xs text-gray-500">{t.assignee?.fullName ?? 'Destek ekibi'} çözdü · {timeAgo(t.updatedAt ?? t.createdAt)}</span>
              </Link>
              <button className="btn-secondary" aria-busy={close.isPending && close.variables === t.id} onClick={() => close.mutate(t.id)}>Onayla ve kapat</button>
            </li>
          ))}
        </ul>
      )}
    </Widget>
  )
}

/* =========================================================
   Talep listesi kutucuğu (Görevlerim / Son taleplerim)
   ========================================================= */

function TicketListWidget({ title, icon, items, empty, action, className }: {
  title: string; icon: ReactNode; items: Home['myTasks']; empty: string; action: { label: string; to: string }; className?: string
}) {
  return (
    <Widget title={title} icon={icon} action={action} className={className}>
      {items.length === 0 ? (
        <WidgetEmpty icon={<ClipboardList className="size-8" />} text={empty} />
      ) : (
        <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {items.slice(0, 6).map((t) => (
            <li key={t.id}>
              <Link to={`/tickets/${t.id}`} className="flex min-h-11 items-center gap-2 py-1.5 hover:text-violet-600">
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-gray-800 dark:text-gray-100"><span className="text-gray-400">#{t.id}</span> {t.title}</span>
                  <span className="block truncate text-xs text-gray-500">{t.store?.name ?? t.requester.fullName} · {timeAgo(t.createdAt)}</span>
                </span>
                <span className="hidden shrink-0 sm:inline"><PriorityText priority={t.priority} /></span>
                <span className="shrink-0"><StatusBadge status={t.status} /></span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Widget>
  )
}

/* =========================================================
   Son hareketler
   ========================================================= */

function activityText(a: Activity): ReactNode {
  const st = (v?: string | null) => (v && v in statusLabels ? statusLabels[v as TicketStatus] : v)
  const pr = (v?: string | null) => (v && v in priorityLabels ? priorityLabels[v as TicketPriority] : v)
  switch (a.action) {
    case 'Created': return 'talep açtı'
    case 'StatusChanged': return <>durumu <b className="font-medium">{st(a.newValue)}</b> yaptı</>
    case 'Assigned': return a.newValue ? <><b className="font-medium">{a.newValue}</b> kişisine atadı</> : 'atamayı kaldırdı'
    case 'PriorityChanged': return <>önceliği <b className="font-medium">{pr(a.newValue)}</b> yaptı</>
    case 'AttachmentAdded': return 'dosya ekledi'
    case 'AttachmentRemoved': return 'dosya sildi'
    default: return 'talebi güncelledi'
  }
}

function ActivityWidget({ items, className }: { items: Activity[]; className?: string }) {
  return (
    <Widget title="Son hareketler" icon={<ActivityIcon className="size-4" />} className={className}>
      {items.length === 0 ? (
        <WidgetEmpty icon={<ActivityIcon className="size-8" />} text="Henüz bir hareket yok." />
      ) : (
        <ol className="space-y-3">
          {items.map((a, i) => (
            <li key={`${a.ticketId}-${i}`} className="flex gap-3 text-sm">
              <Avatar name={a.actorName ?? 'Sistem'} size="sm" />
              <div className="min-w-0 flex-1">
                <p className="text-gray-600 dark:text-gray-300">
                  <span className="font-medium text-gray-800 dark:text-gray-100">{a.actorName ?? 'Sistem'}</span> {activityText(a)}
                </p>
                <Link to={`/tickets/${a.ticketId}`} className="block truncate text-xs text-gray-500 hover:text-violet-600">#{a.ticketId} {a.ticketTitle} · {timeAgo(a.createdAt)}</Link>
              </div>
            </li>
          ))}
        </ol>
      )}
    </Widget>
  )
}

/* =========================================================
   Çalışan ara
   ========================================================= */

function PeopleSearchWidget({ className }: { className?: string }) {
  const [q, setQ] = useState('')
  const term = useDeferredValue(q.trim())
  const people = useQuery({ queryKey: ['users', 'lookup', term], queryFn: () => api.users.lookup(term), enabled: term.length >= 2 })
  return (
    <Widget title="Çalışan ara" icon={<Users className="size-4" />} className={className}>
      <label htmlFor="people-search" className="sr-only">Ad veya e-posta</label>
      <div className="relative">
        <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-gray-400" aria-hidden="true" />
        <input id="people-search" type="search" value={q} onChange={(e) => setQ(e.target.value)} className="form-field pl-9" placeholder="Ad veya e-posta (en az 2 harf)" autoComplete="off" />
      </div>
      <div className="mt-3" aria-live="polite">
        {term.length < 2 ? (
          <WidgetEmpty icon={<UserRoundSearch className="size-8" />} text="Aramak için yazmaya başlayın." />
        ) : people.isLoading ? (
          <p className="py-6 text-center text-sm text-gray-500">Aranıyor…</p>
        ) : people.data?.length === 0 ? (
          <p className="py-6 text-center text-sm text-gray-500">"{term}" ile eşleşen çalışan bulunamadı.</p>
        ) : (
          <ul className="space-y-1">
            {people.data?.slice(0, 6).map((p) => (
              <li key={p.id} className="flex min-h-11 items-center gap-3">
                <Avatar name={p.fullName} />
                <div className="min-w-0 text-sm">
                  <p className="truncate font-medium text-gray-800 dark:text-gray-100">{p.fullName}</p>
                  <a href={`mailto:${p.email}`} className="block truncate text-xs text-gray-500 hover:text-violet-600">{p.email}</a>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Widget>
  )
}

/* =========================================================
   Grafikler
   ========================================================= */

type Stats = DashboardStats

function TrendWidget({ s, className }: { s: Stats; className?: string }) {
  const theme = useChartTheme()
  const animation = reducedMotion() ? (false as const) : undefined
  return (
    <section className={clsx('card flex min-w-0 flex-col', className)} aria-labelledby="w-trend">
      <header className="flex min-h-11 flex-wrap items-center justify-between gap-2 border-b border-gray-100 px-4 dark:border-gray-700/60">
        <h2 id="w-trend" className="text-[13px] font-semibold text-gray-800 dark:text-gray-100">Son 14 gün — {s.scopeLabel}</h2>
        <span className="flex items-center gap-1 text-xs text-gray-500"><Timer className="size-4" aria-hidden="true" /> Ort. çözüm: {s.avgResolutionHours != null ? `${s.avgResolutionHours} saat` : '—'}</span>
      </header>
      <div className="h-64 p-3">
        <Line
          aria-label="Son 14 günde açılan ve çözülen talepler"
          data={{
            labels: s.last14Days.map((d) => new Date(d.date).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short' })),
            datasets: [
              { label: 'Açılan', data: s.last14Days.map((d) => d.created), borderColor: '#8b5cf6', backgroundColor: 'rgba(139,92,246,0.12)', fill: true, cubicInterpolationMode: 'monotone', pointRadius: 0, pointHoverRadius: 4, borderWidth: 2 },
              { label: 'Çözülen', data: s.last14Days.map((d) => d.resolved), borderColor: '#10b981', backgroundColor: 'transparent', cubicInterpolationMode: 'monotone', pointRadius: 0, pointHoverRadius: 4, borderWidth: 2 },
            ],
          }}
          options={{
            animation,
            maintainAspectRatio: false,
            interaction: { intersect: false, mode: 'index' },
            plugins: { legend: { position: 'top', align: 'end', labels: { color: theme.tick, boxWidth: 10, usePointStyle: true } } },
            scales: {
              x: { grid: { display: false }, ticks: { color: theme.tick, maxRotation: 0, autoSkipPadding: 12 } },
              y: { beginAtZero: true, grid: { color: theme.grid }, border: { display: false }, ticks: { color: theme.tick, precision: 0 } },
            },
          }}
        />
      </div>
    </section>
  )
}

function ChartsRow({ s, isStaff }: { s: Stats; isStaff: boolean }) {
  const theme = useChartTheme()
  const animation = reducedMotion() ? (false as const) : undefined
  const byStore = s.scope === 'all' && s.byStore.length > 0
  return (
    <>
      <Widget title="Durum dağılımı" className="col-span-12 md:col-span-6 xl:col-span-4" bodyClassName="h-64 p-3">
        {s.total === 0 ? <WidgetEmpty text="İlk talep açıldığında dağılım burada görünür." /> : (
          <Doughnut
            aria-label="Durumlara göre talep dağılımı"
            data={{ labels: s.byStatus.map((x) => x.label), datasets: [{ data: s.byStatus.map((x) => x.count), backgroundColor: STATUS_COLORS, borderColor: theme.card, borderWidth: 3, hoverOffset: 4 }] }}
            options={{ animation, maintainAspectRatio: false, cutout: '68%', plugins: { legend: { position: 'right', labels: { color: theme.tick, boxWidth: 10, usePointStyle: true, padding: 12 } } } }}
          />
        )}
      </Widget>

      <Widget title={byStore ? 'Mağazalara göre açık talepler' : 'Kategorilere göre talepler'} className="col-span-12 md:col-span-6 xl:col-span-4" bodyClassName="h-64 p-3">
        <Bar
          aria-label={byStore ? 'Mağazalara göre açık talep sayıları' : 'Kategorilere göre talep sayıları'}
          data={byStore
            ? { labels: s.byStore.map((c) => c.label), datasets: [{ label: 'Açık talep', data: s.byStore.map((c) => c.count), backgroundColor: '#8b5cf6', borderRadius: 6, barThickness: 16 }] }
            : { labels: s.byCategory.map((c) => c.label), datasets: [{ label: 'Talep', data: s.byCategory.map((c) => c.count), backgroundColor: s.byCategory.map((c) => colorOf(c.color).hex), borderRadius: 6, barThickness: 16 }] }}
          options={{
            animation, indexAxis: 'y', maintainAspectRatio: false, plugins: { legend: { display: false } },
            scales: {
              x: { beginAtZero: true, grid: { color: theme.grid }, border: { display: false }, ticks: { color: theme.tick, precision: 0 } },
              y: { grid: { display: false }, ticks: { color: theme.tick } },
            },
          }}
        />
      </Widget>

      <Widget title="Açık talepler — öncelik" className="col-span-12 xl:col-span-4">
        <div className="space-y-3">
          {s.byPriority.map((p) => {
            const max = Math.max(1, ...s.byPriority.map((x) => x.count))
            const bar = { Low: 'bg-gray-400', Medium: 'bg-sky-500', High: 'bg-orange-500', Critical: 'bg-rose-500' }[p.key] ?? 'bg-violet-500'
            return (
              <div key={p.key}>
                <div className="mb-1 flex justify-between text-sm"><span className="font-medium text-gray-700 dark:text-gray-300">{p.label}</span><span className="text-gray-500 tabular-nums">{p.count}</span></div>
                <div className="h-2 rounded-full bg-gray-100 dark:bg-gray-700/60"><div className={`h-2 rounded-full ${bar}`} style={{ width: `${(p.count / max) * 100}%` }} /></div>
              </div>
            )
          })}
        </div>
        {isStaff && s.topAgents.length > 0 && (
          <div className="mt-4 border-t border-gray-100 pt-3 dark:border-gray-700/60">
            <p className="mb-2 text-xs font-semibold text-gray-500">Ekip iş yükü (açık)</p>
            <ul className="space-y-1.5">
              {s.topAgents.map((a) => (
                <li key={a.key} className="flex items-center justify-between gap-2 text-sm">
                  <span className="flex min-w-0 items-center gap-2"><Avatar name={a.label} size="sm" /> <span className="truncate">{a.label}</span></span>
                  <span className="font-medium text-gray-800 tabular-nums dark:text-gray-100">{a.count}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </Widget>
    </>
  )
}

/* =========================================================
   Portal reklam/duyuru bannerları (Kurumsal site → Reklam ve bannerlar, sayfa: Portal)
   ========================================================= */

function PortalAds({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['site', 'ads', 'Portal'], queryFn: () => siteApi.ads('Portal'), staleTime: 5 * 60_000 })
  if (!q.data?.length) return null // yayında banner yoksa yer kaplamaz
  return <div className={clsx(className, 'flex flex-col gap-4')}>{q.data.slice(0, 2).map((a) => <AdCard key={a.id} ad={a} tone="auto" />)}</div>
}
