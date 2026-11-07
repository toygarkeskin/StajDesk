import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { ArcElement, BarElement, CategoryScale, Chart as ChartJS, Filler, Legend, LinearScale, LineElement, PointElement, Tooltip } from 'chart.js'
import { Bar, Doughnut, Line } from 'react-chartjs-2'
import { AlertOctagon, CheckCircle2, Clock, Inbox, MonitorSmartphone, Plus, Timer, UserX } from 'lucide-react'
import { api, errorDetails, errorMessage } from '@/lib/api'
import { colorOf } from '@/lib/labels'
import { timeAgo } from '@/lib/format'
import { useAuth, useRole } from '@/stores/auth'
import { useUi } from '@/stores/ui'
import { Avatar, BlockSkeleton, CategoryText, EmptyState, ErrorState, PageHeader, PriorityText, StatCard, StatusBadge } from '@/components/ui'
import { ClickableRow, Th } from '@/components/list'

ChartJS.register(ArcElement, BarElement, CategoryScale, LinearScale, LineElement, PointElement, Tooltip, Legend, Filler)

const STATUS_COLORS = ['#0ea5e9', '#f59e0b', '#10b981', '#9ca3af']
const reducedMotion = () => typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches

function useChartTheme() {
  const dark = useUi((s) => s.theme) === 'dark'
  return { grid: dark ? 'rgba(55,65,81,0.6)' : '#f3f4f6', tick: dark ? '#9ca3af' : '#6b7280', card: dark ? '#1f2937' : '#ffffff' }
}

export function DashboardPage() {
  const user = useAuth((s) => s.me)
  const { isStaff } = useRole()
  const theme = useChartTheme()
  const { data, isLoading, isError, error, refetch } = useQuery({ queryKey: ['stats'], queryFn: api.stats.dashboard, refetchInterval: 60_000 })
  const animation = reducedMotion() ? (false as const) : undefined

  const hour = new Date().getHours()
  const greeting = hour < 12 ? 'Günaydın' : hour < 18 ? 'İyi günler' : 'İyi akşamlar'

  return (
    <>
      {/* Karşılama bandı */}
      <div className="relative mb-8 overflow-hidden rounded-xl bg-violet-100 p-4 md:p-6 dark:bg-violet-500/20">
        <div className="pointer-events-none absolute -top-16 -right-10 size-56 rounded-full bg-violet-300/40 blur-2xl dark:bg-violet-500/20" aria-hidden="true" />
        <div className="pointer-events-none absolute right-40 -bottom-20 size-40 rounded-full bg-indigo-300/40 blur-2xl dark:bg-indigo-500/20" aria-hidden="true" />
        <div className="relative">
          <h1 className="mb-1 text-2xl font-bold md:text-3xl">{greeting}, {user?.fullName.split(' ')[0]} 👋</h1>
          <p className="text-gray-600 dark:text-gray-300">{isStaff ? 'Destek ekibinin bugünkü durumuna göz atın.' : 'Taleplerinizin güncel durumu aşağıdadır.'}</p>
        </div>
      </div>

      <PageHeader
        title={`Özet Paneli — ${data?.scopeLabel ?? ''}`}
        description={data ? `Son güncelleme: ${timeAgo(data.generatedAt)}` : undefined}
        actions={<Link to="/tickets/new" className="btn-primary"><Plus className="size-4" aria-hidden="true" /> Yeni Talep</Link>}
      />

      {isError && <div className="card"><ErrorState message={errorMessage(error)} details={errorDetails(error)} onRetry={() => refetch()} /></div>}
      {isLoading && (
        <div className="grid grid-cols-12 gap-6">
          {Array.from({ length: 4 }).map((_, i) => <BlockSkeleton key={i} className="col-span-6 h-28 md:col-span-3" />)}
          <BlockSkeleton className="col-span-12 h-80" />
        </div>
      )}

      {data && (
        <div className="grid grid-cols-12 gap-6">
          <div className="col-span-12 grid grid-cols-2 gap-6 md:grid-cols-5">
            <StatCard label="Açık" value={data.open} tone="sky" icon={<Inbox className="size-5" />} hint={<>Toplam {data.total} talep</>} to="/tickets?status=Open" />
            <StatCard label="İşlemde" value={data.inProgress} tone="amber" icon={<Clock className="size-5" />} to="/tickets?status=InProgress" />
            <StatCard label="Beklemede" value={data.pending} tone="violet" icon={<Clock className="size-5" />} hint="Kullanıcı / parça bekleniyor" to="/tickets?status=Pending" />
            <StatCard label="Çözüldü" value={data.resolved} tone="emerald" icon={<CheckCircle2 className="size-5" />} hint={<>Kapanan: {data.closed}</>} to="/tickets?status=Resolved" />
            {isStaff ? (
              <StatCard label="Atanmamış" value={data.unassigned} tone="rose" icon={<UserX className="size-5" />} hint={<>Kritik açık: {data.criticalOpen}</>} to="/tickets?unassigned=true&activeOnly=true" />
            ) : (
              <StatCard label="Cihazlarım" value={data.assetCount} tone="violet" icon={<MonitorSmartphone className="size-5" />} />
            )}
          </div>

          {/* Son 14 gün */}
          <section className="card col-span-12 md:col-span-8">
            <header className="flex items-center justify-between border-b border-gray-100 px-5 py-4 dark:border-gray-700/60">
              <h2 className="font-semibold">Son 14 Gün</h2>
              <span className="flex items-center gap-1 text-xs text-gray-500">
                <Timer className="size-4" aria-hidden="true" /> Ort. çözüm: {data.avgResolutionHours != null ? `${data.avgResolutionHours} saat` : '—'}
              </span>
            </header>
            <div className="h-72 p-4">
              <Line
                aria-label="Son 14 günde açılan ve çözülen talepler"
                data={{
                  labels: data.last14Days.map((d) => new Date(d.date).toLocaleDateString('tr-TR', { day: '2-digit', month: 'short' })),
                  datasets: [
                    { label: 'Açılan', data: data.last14Days.map((d) => d.created), borderColor: '#8b5cf6', backgroundColor: 'rgba(139,92,246,0.12)', fill: true, cubicInterpolationMode: 'monotone', pointRadius: 0, pointHoverRadius: 4, borderWidth: 2 },
                    { label: 'Çözülen', data: data.last14Days.map((d) => d.resolved), borderColor: '#10b981', backgroundColor: 'transparent', cubicInterpolationMode: 'monotone', pointRadius: 0, pointHoverRadius: 4, borderWidth: 2 },
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

          {/* Durum dağılımı */}
          <section className="card col-span-12 md:col-span-4">
            <header className="border-b border-gray-100 px-5 py-4 dark:border-gray-700/60"><h2 className="font-semibold">Durum Dağılımı</h2></header>
            <div className="h-72 p-4">
              {data.total === 0 ? (
                <EmptyState title="Henüz talep yok" description="İlk talep açıldığında dağılım burada görünür." />
              ) : (
                <Doughnut
                  aria-label="Durumlara göre talep dağılımı"
                  data={{ labels: data.byStatus.map((s) => s.label), datasets: [{ data: data.byStatus.map((s) => s.count), backgroundColor: STATUS_COLORS, borderColor: theme.card, borderWidth: 3, hoverOffset: 4 }] }}
                  options={{ animation, maintainAspectRatio: false, cutout: '70%', plugins: { legend: { position: 'bottom', labels: { color: theme.tick, boxWidth: 10, usePointStyle: true, padding: 16 } } } }}
                />
              )}
            </div>
          </section>

          {/* Kategoriler */}
          <section className="card col-span-12 md:col-span-6">
            <header className="border-b border-gray-100 px-5 py-4 dark:border-gray-700/60"><h2 className="font-semibold">{data.scope === 'all' && data.byStore.length ? 'Mağazalara Göre Açık Talepler' : 'Kategorilere Göre Talepler'}</h2></header>
            <div className="h-72 p-4">
              <Bar
                aria-label="Kategorilere göre talep sayıları"
                data={data.scope === 'all' && data.byStore.length
                  ? { labels: data.byStore.map((c) => c.label), datasets: [{ label: 'Açık talep', data: data.byStore.map((c) => c.count), backgroundColor: '#8b5cf6', borderRadius: 6, barThickness: 18 }] }
                  : { labels: data.byCategory.map((c) => c.label), datasets: [{ label: 'Talep', data: data.byCategory.map((c) => c.count), backgroundColor: data.byCategory.map((c) => colorOf(c.color).hex), borderRadius: 6, barThickness: 18 }] }}
                options={{
                  animation,
                  indexAxis: 'y',
                  maintainAspectRatio: false,
                  plugins: { legend: { display: false } },
                  scales: {
                    x: { beginAtZero: true, grid: { color: theme.grid }, border: { display: false }, ticks: { color: theme.tick, precision: 0 } },
                    y: { grid: { display: false }, ticks: { color: theme.tick } },
                  },
                }}
              />
            </div>
          </section>

          {/* Öncelik + ekip yükü */}
          <section className="card col-span-12 md:col-span-6">
            <header className="border-b border-gray-100 px-5 py-4 dark:border-gray-700/60"><h2 className="font-semibold">Açık Talepler — Öncelik</h2></header>
            <div className="space-y-4 p-5">
              {data.byPriority.map((p) => {
                const max = Math.max(1, ...data.byPriority.map((x) => x.count))
                const bar = { Low: 'bg-gray-400', Medium: 'bg-sky-500', High: 'bg-orange-500', Critical: 'bg-rose-500' }[p.key] ?? 'bg-violet-500'
                return (
                  <div key={p.key}>
                    <div className="mb-1 flex justify-between text-sm">
                      <span className="font-medium text-gray-700 dark:text-gray-300">{p.label}</span>
                      <span className="text-gray-500">{p.count}</span>
                    </div>
                    <div className="h-2 rounded-full bg-gray-100 dark:bg-gray-700/60">
                      <div className={`h-2 rounded-full ${bar}`} style={{ width: `${(p.count / max) * 100}%` }} />
                    </div>
                  </div>
                )
              })}
              {isStaff && data.topAgents.length > 0 && (
                <div className="border-t border-gray-100 pt-4 dark:border-gray-700/60">
                  <div className="mb-3 text-xs font-semibold text-gray-400 uppercase">Ekip iş yükü (açık)</div>
                  <ul className="space-y-2">
                    {data.topAgents.map((a) => (
                      <li key={a.key} className="flex items-center justify-between text-sm">
                        <span className="flex items-center gap-2"><Avatar name={a.label} size="sm" /> {a.label}</span>
                        <span className="font-medium text-gray-800 dark:text-gray-100">{a.count}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          </section>

          {/* Son talepler */}
          <section className="card col-span-12">
            <header className="flex items-center justify-between border-b border-gray-100 px-5 py-4 dark:border-gray-700/60">
              <h2 className="font-semibold">Son Talepler</h2>
              <Link to="/tickets" className="inline-flex min-h-11 items-center text-sm font-medium text-violet-500 hover:text-violet-600">Tümünü gör →</Link>
            </header>
            {data.recent.length === 0 ? (
              <EmptyState title="Henüz talep yok" description="İlk talebinizi oluşturarak başlayabilirsiniz." action={<Link to="/tickets/new" className="btn-violet">Talep oluştur</Link>} />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full table-auto">
                  <thead className="table-head"><tr><Th className="pl-5">Talep</Th><Th>Kategori</Th><Th>Öncelik</Th><Th>Durum</Th><Th className="text-right">Oluşturma</Th><Th /></tr></thead>
                  <tbody className="text-sm">
                    {data.recent.map((t) => (
                      <ClickableRow key={t.id} to={`/tickets/${t.id}`}>
                        <td className="px-5 py-3">
                          <Link to={`/tickets/${t.id}`} className="font-medium text-gray-800 hover:text-violet-600 dark:text-gray-100"><span className="text-gray-400">#{t.id}</span> {t.title}</Link>
                          <div className="text-xs text-gray-500">{t.requester.fullName}</div>
                        </td>
                        <td className="px-4 py-3"><CategoryText name={t.category.name} color={t.category.color} /></td>
                        <td className="px-4 py-3"><PriorityText priority={t.priority} /></td>
                        <td className="px-4 py-3"><StatusBadge status={t.status} /></td>
                        <td className="px-4 py-3 text-right whitespace-nowrap text-gray-500">{timeAgo(t.createdAt)}</td>
                      </ClickableRow>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {isStaff && data.criticalOpen > 0 && (
            <div className="col-span-12 flex items-center gap-3 rounded-xl bg-rose-500/10 px-5 py-3 text-sm text-rose-700 dark:text-rose-400">
              <AlertOctagon className="size-5" aria-hidden="true" />
              {data.criticalOpen} adet kritik öncelikli açık talep var.
              <Link to="/tickets?priority=Critical&activeOnly=true" className="ml-auto inline-flex min-h-11 items-center font-medium underline">Görüntüle</Link>
            </div>
          )}
        </div>
      )}
    </>
  )
}
