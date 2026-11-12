import { useState } from 'react'
import { Link } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Award, Briefcase, Cake, CalendarDays, CheckSquare, ListTodo, ChevronDown, ChevronUp, Eye, EyeOff, IdCard, MapPin, PartyPopper, Trophy, UserCheck } from 'lucide-react'
import { api, errorMessage } from '@/lib/api'
import { portalApi, type DashboardLayout, type DashboardWidget, type PersonRef } from '@/lib/portal'
import { siteApi } from '@/lib/site'
import { workApi } from '@/lib/work'
import { employmentLabels } from '@/lib/labels'
import { formatDate } from '@/lib/format'
import { useAuth } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { PersonAvatar } from './AuthImage'
import { BlockSkeleton, ErrorState, SlideOver, Spinner } from './ui'
import { Widget, WidgetEmpty } from './widgets'

/* =========================================================
   Aşama 5 portal kutucukları. Her biri kendi verisini çeker; sunucu yetki dışı veriyi zaten döndürmez.
   ========================================================= */

const monthDay = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long' })
const eventFmt = new Intl.DateTimeFormat('tr-TR', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

function Loading() {
  return <div className="space-y-2" aria-busy="true">{[0, 1, 2].map((i) => <BlockSkeleton key={i} className="h-10" />)}</div>
}

function PersonRow({ p, meta }: { p: PersonRef; meta?: string }) {
  return (
    <Link to={`/employees/${p.id}`} className="-mx-2 flex min-h-11 items-center gap-3 rounded-lg px-2 py-1.5 hover:bg-gray-50 dark:hover:bg-gray-700/30">
      <PersonAvatar id={p.id} name={p.fullName} photoVersion={p.hasPhoto ? p.photoVersion : null} size="sm" />
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-medium text-gray-800 dark:text-gray-100">{p.fullName}</span>
        <span className="block truncate text-xs text-gray-500">{[p.title, p.department].filter(Boolean).join(' · ') || '—'}</span>
      </span>
      {meta && <span className="shrink-0 text-xs text-gray-500">{meta}</span>}
    </Link>
  )
}

/** Kıdem: "3 yıl 2 ay" */
function seniority(hire: string) {
  const h = new Date(hire + 'T00:00:00')
  const now = new Date()
  let months = (now.getFullYear() - h.getFullYear()) * 12 + now.getMonth() - h.getMonth() - (now.getDate() < h.getDate() ? 1 : 0)
  if (months < 0) return 'Henüz başlamadı'
  const y = Math.floor(months / 12)
  months %= 12
  if (y === 0 && months === 0) return 'Bu ay başladı'
  return [y && `${y} yıl`, months && `${months} ay`].filter(Boolean).join(' ')
}

export function ProfileWidget({ className }: { className?: string }) {
  const me = useAuth((s) => s.me)
  const q = useQuery({ queryKey: ['employee', me?.id], queryFn: () => api.employees.get(me!.id), enabled: !!me, staleTime: 5 * 60_000 })
  const e = q.data
  return (
    <Widget title="Profil özeti" icon={<IdCard className="size-4" />} action={{ label: 'Profilim', to: '/profile' }} className={className}>
      {q.isLoading ? <Loading /> : q.isError || !e ? <WidgetEmpty text="Profil bilgisi alınamadı." /> : (
        <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
          {[
            ['Departman', e.department?.name],
            ['Unvan', e.position?.name],
            ['Yönetici', e.manager?.fullName],
            ['Çalışma türü', employmentLabels[e.employmentType]],
            ['İşe başlama', e.hireDate ? formatDate(e.hireDate) : null],
            ['Kıdem', e.hireDate ? seniority(e.hireDate) : null],
          ].map(([k, v]) => (
            <div key={k} className="min-w-0"><dt className="text-xs text-gray-500">{k}</dt><dd className="truncate font-medium text-gray-800 dark:text-gray-100">{v || '—'}</dd></div>
          ))}
        </dl>
      )}
    </Widget>
  )
}

export function BirthdaysWidget({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['portal', 'birthdays'], queryFn: portalApi.birthdays, staleTime: 30 * 60_000 })
  return (
    <Widget title="Doğum günleri" icon={<Cake className="size-4" />} className={className}>
      {q.isLoading ? <Loading /> : q.isError ? <WidgetEmpty text={errorMessage(q.error)} /> : q.data!.length === 0 ? (
        <WidgetEmpty icon={<Cake className="size-8" />} text="Önümüzdeki 7 günde doğum günü yok." action={<Link to="/profile?tab=preferences" className="text-xs font-medium text-violet-600 dark:text-violet-400">Doğum günümü paylaş</Link>} />
      ) : (
        <ul>{q.data!.map((b) => <li key={b.person.id}><PersonRow p={b.person} meta={b.isToday ? 'Bugün 🎉' : monthDay.format(new Date(2000, b.month - 1, b.day))} /></li>)}</ul>
      )}
      <p className="mt-2 text-[11px] text-gray-400">Yalnızca doğum gününü paylaşmayı seçenler görünür; yaş gösterilmez.</p>
    </Widget>
  )
}

export function NewHiresWidget({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['portal', 'new-hires'], queryFn: portalApi.newHires, staleTime: 30 * 60_000 })
  return (
    <Widget title="Aramıza katılanlar" icon={<PartyPopper className="size-4" />} className={className}>
      {q.isLoading ? <Loading /> : q.isError ? <WidgetEmpty text={errorMessage(q.error)} /> : q.data!.length === 0
        ? <WidgetEmpty icon={<PartyPopper className="size-8" />} text="Son 30 günde işe başlayan çalışan yok." />
        : <ul>{q.data!.map((n) => <li key={n.person.id}><PersonRow p={n.person} meta={formatDate(n.hireDate)} /></li>)}</ul>}
    </Widget>
  )
}

export function RecognitionsWidget({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['portal', 'recognitions'], queryFn: portalApi.recognitions, staleTime: 10 * 60_000 })
  const week = q.data?.find((r) => r.type === 'EmployeeOfWeek')
  const others = q.data?.filter((r) => r !== week) ?? []
  return (
    <Widget title="Takdirler" icon={<Trophy className="size-4" />} className={className}>
      {q.isLoading ? <Loading /> : q.isError ? <WidgetEmpty text={errorMessage(q.error)} /> : !q.data!.length ? <WidgetEmpty icon={<Trophy className="size-8" />} text="Henüz takdir paylaşılmadı." /> : (
        <div className="space-y-3">
          {week && (
            <Link to={`/employees/${week.person.id}`} className="flex items-center gap-3 rounded-xl bg-linear-to-br from-violet-500/15 to-amber-500/10 p-3">
              <PersonAvatar id={week.person.id} name={week.person.fullName} photoVersion={week.person.hasPhoto ? week.person.photoVersion : null} size="lg" />
              <span className="min-w-0">
                <span className="flex items-center gap-1 text-[11px] font-semibold tracking-wide text-violet-600 uppercase dark:text-violet-300"><Award className="size-3.5" aria-hidden="true" />Haftanın çalışanı</span>
                <span className="block truncate font-semibold text-gray-800 dark:text-gray-100">{week.person.fullName}</span>
                {week.description && <span className="line-clamp-2 text-xs text-gray-500">{week.description}</span>}
              </span>
            </Link>
          )}
          {others.length > 0 && (
            <ul className="space-y-2">
              {others.map((r) => (
                <li key={r.id} className="text-sm">
                  <span className="font-medium text-gray-800 dark:text-gray-100">{r.person.fullName}</span>
                  <span className="text-gray-500"> · {r.title}</span>
                  <span className="block text-xs text-gray-400">{formatDate(r.date)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Widget>
  )
}

export function EventsWidget({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['portal', 'events'], queryFn: portalApi.events, staleTime: 10 * 60_000 })
  return (
    <Widget title="Şirket etkinlikleri" icon={<CalendarDays className="size-4" />} className={className}>
      {q.isLoading ? <Loading /> : q.isError ? <WidgetEmpty text={errorMessage(q.error)} /> : q.data!.length === 0
        ? <WidgetEmpty icon={<CalendarDays className="size-8" />} text="Yaklaşan etkinlik yok." />
        : (
          <ul className="space-y-3">
            {q.data!.map((e) => {
              const d = new Date(e.startsAt)
              return (
                <li key={e.id} className="flex gap-3">
                  <span className="flex w-11 shrink-0 flex-col items-center rounded-lg bg-violet-500/10 py-1 text-violet-700 dark:text-violet-300" aria-hidden="true">
                    <span className="text-[10px] font-semibold uppercase">{d.toLocaleDateString('tr-TR', { month: 'short' })}</span>
                    <span className="text-lg leading-tight font-bold">{d.getDate()}</span>
                  </span>
                  <span className="min-w-0">
                    <span className="block font-medium text-gray-800 dark:text-gray-100">{e.title}</span>
                    <span className="block text-xs text-gray-500">{eventFmt.format(d)}{e.location && <> · <MapPin className="inline size-3" aria-hidden="true" /> {e.location}</>}</span>
                    {e.description && <span className="line-clamp-2 text-xs text-gray-500">{e.description}</span>}
                  </span>
                </li>
              )
            })}
          </ul>
        )}
    </Widget>
  )
}

export function OpenPositionsWidget({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['site', 'jobs'], queryFn: siteApi.jobs, staleTime: 10 * 60_000 })
  return (
    <Widget title="Açık pozisyonlar" icon={<Briefcase className="size-4" />} className={className}>
      {q.isLoading ? <Loading /> : q.isError ? <WidgetEmpty text={errorMessage(q.error)} /> : q.data!.length === 0
        ? <WidgetEmpty icon={<Briefcase className="size-8" />} text="Şu anda açık pozisyon yok." />
        : (
          <ul>
            {q.data!.slice(0, 5).map((j) => (
              <li key={j.id}>
                <a href={`/kariyer/${j.id}`} target="_blank" rel="noopener" className="-mx-2 flex min-h-11 flex-col justify-center rounded-lg px-2 py-1.5 hover:bg-gray-50 dark:hover:bg-gray-700/30">
                  <span className="text-sm font-medium text-gray-800 dark:text-gray-100">{j.title}</span>
                  <span className="text-xs text-gray-500">{[j.department, j.location, employmentLabels[j.employmentType]].filter(Boolean).join(' · ')}</span>
                </a>
              </li>
            ))}
          </ul>
        )}
      <p className="mt-2 text-[11px] text-gray-400">Tanıdığınız uygun adayları kariyer sayfasına yönlendirebilirsiniz.</p>
    </Widget>
  )
}

export function PendingAccountsWidget({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['account-requests', 'pending-count'], queryFn: api.accountRequests.pendingCount, refetchInterval: 120_000 })
  return (
    <Widget title="Bekleyen hesap talepleri" icon={<UserCheck className="size-4" />} action={{ label: 'Aç', to: '/account-requests' }} className={className}>
      {q.isLoading ? <Loading /> : q.isError ? <WidgetEmpty text={errorMessage(q.error)} /> : (
        <div className="flex h-full min-h-28 flex-col justify-center">
          <div className="text-4xl font-bold text-gray-900 tabular-nums dark:text-gray-50">{q.data}</div>
          <p className="text-sm text-gray-500">{q.data ? 'talep inceleme bekliyor' : 'Bekleyen talep yok.'}</p>
        </div>
      )}
    </Widget>
  )
}

/* =========================================================
   Kişisel panel düzenleyici: sıra, gizle/göster, varsayılana dön.
   ========================================================= */

export function DashboardEditor({ layout, onClose }: { layout: DashboardLayout; onClose: () => void }) {
  const qc = useQueryClient()
  const [items, setItems] = useState<(DashboardWidget & { hidden: boolean })[]>(() => [
    ...layout.widgets.map((w) => ({ ...w, hidden: false })),
    ...layout.hidden.map((w) => ({ ...w, hidden: true })),
  ])
  const move = (i: number, d: -1 | 1) => setItems((list) => {
    const j = i + d
    if (j < 0 || j >= list.length) return list
    const next = [...list]
    ;[next[i], next[j]] = [next[j]!, next[i]!]
    return next
  })
  const save = useMutation({
    mutationFn: () => portalApi.savePreferences(items.map((w) => w.key), items.filter((w) => w.hidden).map((w) => w.key)),
    onSuccess: (l) => { qc.setQueryData(['dashboard', 'layout'], l); toast('Panel düzeniniz kaydedildi.'); onClose() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const reset = useMutation({
    mutationFn: portalApi.resetPreferences,
    onSuccess: (l) => { qc.setQueryData(['dashboard', 'layout'], l); toast('Panel varsayılan düzene döndü.'); onClose() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <SlideOver open onClose={onClose} title="Panelimi düzenle" description="Kutucukların sırasını değiştirin veya gizleyin. Bu düzen yalnızca sizin içindir."
      footer={
        <div className="flex flex-wrap justify-between gap-2">
          <button className="btn-secondary" disabled={reset.isPending || !layout.customized} onClick={() => reset.mutate()}>{reset.isPending && <Spinner />}Varsayılana dön</button>
          <div className="flex gap-2">
            <button className="btn-secondary" onClick={onClose}>Vazgeç</button>
            <button className="btn-violet" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />}Düzeni kaydet</button>
          </div>
        </div>
      }>
      <ol className="space-y-2">
        {items.map((w, i) => (
          <li key={w.key} className={clsx('flex items-center gap-2 rounded-lg border p-2 pl-3', w.hidden ? 'border-dashed border-gray-300 opacity-60 dark:border-gray-600' : 'border-gray-200 dark:border-gray-700/60')}>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-medium text-gray-800 dark:text-gray-100">{w.title}</span>
              <span className="block truncate text-xs text-gray-500">{w.hidden ? 'Gizli' : w.description}</span>
            </span>
            <button type="button" className="grid size-11 place-items-center rounded-lg text-gray-500 hover:bg-gray-100 disabled:opacity-30 dark:hover:bg-gray-700/50" disabled={i === 0} onClick={() => move(i, -1)} aria-label={`${w.title} yukarı taşı`}><ChevronUp className="size-4" aria-hidden="true" /></button>
            <button type="button" className="grid size-11 place-items-center rounded-lg text-gray-500 hover:bg-gray-100 disabled:opacity-30 dark:hover:bg-gray-700/50" disabled={i === items.length - 1} onClick={() => move(i, 1)} aria-label={`${w.title} aşağı taşı`}><ChevronDown className="size-4" aria-hidden="true" /></button>
            <button type="button" className="grid size-11 place-items-center rounded-lg text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700/50" aria-pressed={!w.hidden}
              onClick={() => setItems((list) => list.map((x) => (x.key === w.key ? { ...x, hidden: !x.hidden } : x)))} aria-label={w.hidden ? `${w.title} göster` : `${w.title} gizle`}>
              {w.hidden ? <EyeOff className="size-4" aria-hidden="true" /> : <Eye className="size-4" aria-hidden="true" />}
            </button>
          </li>
        ))}
      </ol>
    </SlideOver>
  )
}

export function LayoutError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  return <div className="card"><ErrorState message={errorMessage(error)} onRetry={onRetry} /></div>
}

export function LeaveBalanceWidget({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['leave', 'balances', 'me'], queryFn: () => workApi.balances(), staleTime: 5 * 60_000 })
  return (
    <Widget title="İzin bakiyem" icon={<CalendarDays className="size-4" />} action={{ label: 'İzin talebi', to: '/leave' }} className={className}>
      {q.isLoading ? <Loading /> : q.isError ? <WidgetEmpty text={errorMessage(q.error)} /> : q.data!.length === 0 ? <WidgetEmpty text="Bakiye takibi yapılan izin türü yok." /> : (
        <ul className="space-y-3">
          {q.data!.map((b) => (
            <li key={b.leaveTypeId}>
              <div className="flex items-baseline justify-between text-sm">
                <span className="font-medium text-gray-800 dark:text-gray-100">{b.leaveType}</span>
                <span className="tabular-nums"><strong className="text-gray-900 dark:text-gray-50">{b.remaining.toLocaleString('tr-TR')}</strong><span className="text-gray-500"> / {b.entitled.toLocaleString('tr-TR')} gün</span></span>
              </div>
              <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-gray-100 dark:bg-gray-700/60" aria-hidden="true">
                <div className="h-full rounded-full bg-violet-500" style={{ width: `${b.entitled ? Math.min(100, ((b.used + b.pending) / b.entitled) * 100) : 0}%` }} />
              </div>
              {b.pending > 0 && <div className="mt-0.5 text-xs text-gray-500">{b.pending.toLocaleString('tr-TR')} gün onay bekliyor</div>}
            </li>
          ))}
        </ul>
      )}
    </Widget>
  )
}

export function ApprovalInboxWidget({ className }: { className?: string }) {
  const q = useQuery({ queryKey: ['approvals', 1], queryFn: () => workApi.inbox(1), refetchInterval: 120_000 })
  const items = q.data?.items ?? []
  return (
    <Widget title="Onay kutum" icon={<CheckSquare className="size-4" />} action={{ label: 'Tümü', to: '/approvals' }} className={className}>
      {q.isLoading ? <Loading /> : q.isError ? <WidgetEmpty text={errorMessage(q.error)} /> : items.length === 0 ? <WidgetEmpty icon={<CheckSquare className="size-8" />} text="Kararınızı bekleyen onay yok." /> : (
        <ul className="space-y-2">
          {items.slice(0, 5).map((a) => (
            <li key={a.approvalId}>
              <Link to="/approvals" className="-mx-2 block rounded-lg px-2 py-1.5 hover:bg-gray-50 dark:hover:bg-gray-700/30">
                <span className="block truncate text-sm font-medium text-gray-800 dark:text-gray-100">{a.workflowName} · {a.requesterName}</span>
                <span className="block truncate text-xs text-gray-500">{a.summary ?? a.title}</span>
              </Link>
            </li>
          ))}
          {q.data!.totalCount > 5 && <li className="text-xs text-gray-500">ve {q.data!.totalCount - 5} talep daha</li>}
        </ul>
      )}
    </Widget>
  )
}

export function MyTasksWidget({ className }: { className?: string }) {
  const summary = useQuery({ queryKey: ['tasks', 'summary'], queryFn: workApi.taskSummary, refetchInterval: 120_000 })
  const list = useQuery({ queryKey: ['tasks', 'mine', true, 1], queryFn: () => workApi.tasks({ scope: 'mine', openOnly: true, page: 1, pageSize: 5 }) })
  const s = summary.data
  return (
    <Widget title="Yapılacaklarım" icon={<ListTodo className="size-4" />} action={{ label: 'Görevler', to: '/tasks' }} className={className}>
      {list.isLoading ? <Loading /> : list.isError ? <WidgetEmpty text={errorMessage(list.error)} /> : (
        <>
          {s && (
            <div className="mb-3 grid grid-cols-3 gap-2 text-center">
              {[['Açık', s.open, 'text-gray-900 dark:text-gray-50'], ['Geciken', s.overdue, s.overdue ? 'text-rose-600 dark:text-rose-400' : 'text-gray-900 dark:text-gray-50'], ['Bugün', s.dueToday, 'text-gray-900 dark:text-gray-50']].map(([k, v, c]) => (
                <div key={k as string} className="rounded-lg bg-gray-50 py-2 dark:bg-gray-900/40"><div className={`text-xl font-bold tabular-nums ${c}`}>{v}</div><div className="text-[11px] text-gray-500">{k}</div></div>
              ))}
            </div>
          )}
          {list.data!.items.length === 0 ? <WidgetEmpty icon={<ListTodo className="size-8" />} text="Açık göreviniz yok." /> : (
            <ul className="space-y-1">
              {list.data!.items.map((t) => (
                <li key={t.id}>
                  <Link to={`/tasks?task=${t.id}`} className="-mx-2 flex min-h-11 items-center justify-between gap-2 rounded-lg px-2 hover:bg-gray-50 dark:hover:bg-gray-700/30">
                    <span className="truncate text-sm text-gray-800 dark:text-gray-100">{t.title}</span>
                    {t.dueDate && <span className={`shrink-0 text-xs ${t.isOverdue ? 'font-medium text-rose-600 dark:text-rose-400' : 'text-gray-500'}`}>{formatDate(t.dueDate)}</span>}
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </Widget>
  )
}
