import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import {
  AlertTriangle, ArrowRight, Bell, Briefcase, Building2, CalendarClock, ChevronLeft, ExternalLink, Info, Mail, MapPin, Megaphone,
  MonitorSmartphone, Network, Newspaper, Phone, Search, Store, Ticket, Users, UserCheck,
} from 'lucide-react'
import { siteApi, siteKeys, type JobCard, type NewsCard, type PublicAnnouncement, type PublicDepartment } from '@/lib/site'
import { errorMessage } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { employmentLabels } from '@/lib/labels'
import { AdCard, HeroCarousel } from '@/components/Ads'
import { GoldCard, RatesCard, WeatherCard } from '@/components/LiveCards'
import { usePageTitle, useSiteInfo } from './PublicLayout'

/* =========================================================
   Kurumsal site sayfaları. İçerik metni HTML olarak yorumlanmaz: paragraflar düz metin olarak çizilir.
   ========================================================= */

/* ---------- Ortak parçalar ---------- */

const container = 'mx-auto max-w-7xl px-4 sm:px-6'

function PageBand({ title, description, children }: { title: string; description?: string; children?: ReactNode }) {
  return (
    <div className="border-b border-slate-200 bg-white">
      <div className={clsx(container, 'py-8 sm:py-10')}>
        {children}
        <h1 className="text-2xl font-bold text-slate-900 sm:text-3xl">{title}</h1>
        {description && <p className="mt-2 max-w-2xl text-slate-600">{description}</p>}
      </div>
    </div>
  )
}

function Card({ title, icon, action, children, className }: { title: string; icon?: ReactNode; action?: { label: string; to: string }; children: ReactNode; className?: string }) {
  return (
    <section className={clsx('flex min-w-0 flex-col rounded-xl border border-slate-200 bg-white shadow-xs', className)} aria-label={title}>
      <header className="flex min-h-12 items-center justify-between gap-2 border-b border-slate-100 px-4">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-800">
          {icon && <span className="text-indigo-500" aria-hidden="true">{icon}</span>}{title}
        </h2>
        {action && <Link to={action.to} className="inline-flex min-h-11 items-center text-xs font-medium text-indigo-600 hover:text-indigo-700">{action.label} →</Link>}
      </header>
      <div className="min-w-0 flex-1 p-4">{children}</div>
    </section>
  )
}

/** Düz metni paragraflara böler (HTML yok). */
function Paragraphs({ text, className }: { text: string; className?: string }) {
  return (
    <div className={clsx('space-y-4', className)}>
      {text.split(/\n{2,}/).map((p, i) => <p key={i} className="whitespace-pre-line">{p}</p>)}
    </div>
  )
}

const Empty = ({ text }: { text: string }) => <p className="py-6 text-center text-sm text-slate-500">{text}</p>

function Loading({ rows = 3 }: { rows?: number }) {
  return <div className="space-y-3" aria-busy="true">{Array.from({ length: rows }).map((_, i) => <div key={i} className="h-16 animate-pulse rounded-lg bg-slate-100" />)}</div>
}

function Failed({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  return (
    <div role="alert" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
      {errorMessage(error, 'İçerik yüklenemedi.')}{' '}
      <button type="button" onClick={onRetry} className="font-medium underline">Tekrar dene</button>
    </div>
  )
}

const levelStyle: Record<PublicAnnouncement['level'], { icon: ReactNode; className: string; label: string }> = {
  Info: { icon: <Info className="size-4" />, className: 'bg-sky-50 text-sky-700', label: 'Bilgi' },
  Warning: { icon: <AlertTriangle className="size-4" />, className: 'bg-amber-50 text-amber-700', label: 'Uyarı' },
  Critical: { icon: <Bell className="size-4" />, className: 'bg-rose-50 text-rose-700', label: 'Önemli' },
}

function AnnouncementItem({ a, full }: { a: PublicAnnouncement; full?: boolean }) {
  const l = levelStyle[a.level]
  return (
    <article className="flex gap-3">
      <span className={clsx('mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg', l.className)} aria-hidden="true">{l.icon}</span>
      <div className="min-w-0">
        <h3 className="font-medium text-slate-800">{a.title}<span className="sr-only"> ({l.label})</span></h3>
        <p className={clsx('mt-0.5 text-sm text-slate-600', !full && 'line-clamp-2')}>{a.body}</p>
        <time dateTime={a.publishAt} className="mt-1 block text-xs text-slate-400">{formatDate(a.publishAt)}</time>
      </div>
    </article>
  )
}

function NewsTile({ n, large }: { n: NewsCard; large?: boolean }) {
  return (
    <Link to={`/haberler/${n.slug}`} className="group flex min-w-0 flex-col overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xs hover:border-indigo-200">
      <div className={clsx('relative bg-linear-to-br from-indigo-100 to-slate-100', large ? 'aspect-[16/9]' : 'aspect-[2/1]')}>
        {n.imageUrl ? <img src={n.imageUrl} alt="" loading="lazy" className="absolute inset-0 size-full object-cover" onError={(e) => { e.currentTarget.style.display = 'none' }} />
          : <Newspaper className="absolute inset-0 m-auto size-10 text-indigo-300" aria-hidden="true" />}
      </div>
      <div className="flex flex-1 flex-col p-4">
        <time dateTime={n.publishedAt} className="text-xs text-slate-400">{formatDate(n.publishedAt)}</time>
        <h3 className="mt-1 font-semibold text-slate-900 group-hover:text-indigo-700">{n.title}</h3>
        <p className="mt-1 line-clamp-3 text-sm text-slate-600">{n.summary}</p>
      </div>
    </Link>
  )
}

function JobRow({ j }: { j: JobCard }) {
  return (
    <li>
      <Link to={`/kariyer/${j.id}`} className="flex min-h-11 items-center justify-between gap-3 rounded-lg px-2 py-2 hover:bg-slate-50">
        <span className="min-w-0">
          <span className="block font-medium text-slate-800">{j.title}</span>
          <span className="block text-xs text-slate-500">{[j.department, j.location, employmentLabels[j.employmentType]].filter(Boolean).join(' · ')}</span>
        </span>
        <ArrowRight className="size-4 shrink-0 text-slate-400" aria-hidden="true" />
      </Link>
    </li>
  )
}

function DepartmentTile({ d }: { d: PublicDepartment }) {
  return (
    <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
      <div className="flex items-start gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-indigo-50 text-indigo-600" aria-hidden="true"><Building2 className="size-5" /></span>
        <div className="min-w-0">
          <h3 className="font-semibold text-slate-900">{d.name}</h3>
          {d.description && <p className="mt-1 text-sm text-slate-600">{d.description}</p>}
          {d.units.length > 0 && <p className="mt-2 text-xs text-slate-500">Birimler: {d.units.join(', ')}</p>}
        </div>
      </div>
    </article>
  )
}

/* =========================================================
   Ana sayfa
   ========================================================= */

const statFmt = new Intl.NumberFormat('tr-TR')

export function HomePage() {
  usePageTitle(undefined)
  const q = useQuery({ queryKey: ['site', 'landing'], queryFn: siteApi.landing, staleTime: 60_000 })
  if (q.isLoading) return <div className={clsx(container, 'py-8')}><div className="aspect-[3/1] animate-pulse rounded-2xl bg-slate-200" /><div className="mt-6"><Loading /></div></div>
  if (q.isError || !q.data) return <div className={clsx(container, 'py-8')}><Failed error={q.error} onRetry={() => q.refetch()} /></div>

  const d = q.data
  const s = d.site.settings
  const hero = d.ads.filter((a) => a.placement === 'Hero')
  const side = d.ads.filter((a) => a.placement === 'Sidebar')
  const inline = d.ads.filter((a) => a.placement === 'Inline')

  const fallbackHero = (
    <section className="rounded-2xl bg-linear-to-br from-slate-900 to-indigo-900 p-8 text-white sm:p-12">
      <h1 className="text-2xl font-bold text-white sm:text-4xl">{s[siteKeys.companyName]}</h1>
      <p className="mt-3 max-w-xl text-indigo-100">{s[siteKeys.tagline]}</p>
    </section>
  )

  return (
    <div className={clsx(container, 'space-y-6 py-6 sm:py-8')}>
      <h1 className="sr-only">{s[siteKeys.companyName] ?? 'StajDesk'} kurumsal ana sayfa</h1>
      <HeroCarousel ads={hero} fallback={fallbackHero} />

      {/* İstatistik şeridi */}
      <section aria-label="Şirket istatistikleri" className="grid grid-cols-2 gap-3 md:grid-cols-4">
        {[
          { label: 'Çalışan', value: d.stats.employees, icon: <Users className="size-5" /> },
          { label: 'Mağaza', value: d.stats.stores, icon: <Store className="size-5" /> },
          { label: 'Departman', value: d.stats.departments, icon: <Building2 className="size-5" /> },
          { label: 'Şehir', value: d.stats.cities, icon: <MapPin className="size-5" /> },
        ].map((x) => (
          <div key={x.label} className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
            <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-indigo-50 text-indigo-600" aria-hidden="true">{x.icon}</span>
            <div><div className="text-xl font-bold text-slate-900 tabular-nums">{statFmt.format(x.value)}</div><div className="text-xs text-slate-500">{x.label}</div></div>
          </div>
        ))}
      </section>

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Sol: içerik */}
        <div className="min-w-0 space-y-6 lg:col-span-2">
          <Card title="Duyurular" icon={<Megaphone className="size-4" />} action={{ label: 'Tümü', to: '/duyurular' }}>
            {d.announcements.length === 0 ? <Empty text="Şu anda yayında duyuru yok." /> : (
              <div className="space-y-4">{d.announcements.slice(0, 3).map((a) => <AnnouncementItem key={a.id} a={a} />)}</div>
            )}
          </Card>

          <section aria-labelledby="haberler-baslik">
            <div className="mb-3 flex items-center justify-between">
              <h2 id="haberler-baslik" className="flex items-center gap-2 text-base font-semibold text-slate-900"><Newspaper className="size-4 text-indigo-500" aria-hidden="true" />Haberler</h2>
              <Link to="/haberler" className="inline-flex min-h-11 items-center text-xs font-medium text-indigo-600 hover:text-indigo-700">Tüm haberler →</Link>
            </div>
            {d.news.length === 0 ? <div className="rounded-xl border border-slate-200 bg-white"><Empty text="Henüz haber yayımlanmadı." /></div> : (
              <div className="grid gap-4 sm:grid-cols-2">{d.news.slice(0, 4).map((n, i) => <NewsTile key={n.id} n={n} large={i === 0} />)}</div>
            )}
          </section>

          {inline.map((a) => <AdCard key={a.id} ad={a} />)}

          <section aria-labelledby="departmanlar-baslik">
            <div className="mb-3 flex items-center justify-between">
              <h2 id="departmanlar-baslik" className="flex items-center gap-2 text-base font-semibold text-slate-900"><Building2 className="size-4 text-indigo-500" aria-hidden="true" />Departmanlarımız</h2>
              <Link to="/departmanlarimiz" className="inline-flex min-h-11 items-center text-xs font-medium text-indigo-600 hover:text-indigo-700">Tümü →</Link>
            </div>
            {d.departments.length === 0 ? <div className="rounded-xl border border-slate-200 bg-white"><Empty text="Tanıtılan departman yok." /></div> : (
              <div className="grid gap-4 sm:grid-cols-2">{d.departments.slice(0, 4).map((x) => <DepartmentTile key={x.id} d={x} />)}</div>
            )}
          </section>
        </div>

        {/* Sağ: bilgi kartları */}
        <aside className="min-w-0 space-y-6" aria-label="Bilgi kartları">
          <WeatherCard tone="light" />
          <RatesCard tone="light" />
          <GoldCard tone="light" />
          <Card title="Açık pozisyonlar" icon={<Briefcase className="size-4" />} action={{ label: 'Kariyer', to: '/kariyer' }}>
            {d.jobs.length === 0 ? <Empty text="Şu anda açık pozisyon yok." /> : <ul className="-mx-2">{d.jobs.slice(0, 4).map((j) => <JobRow key={j.id} j={j} />)}</ul>}
          </Card>
          {side.map((a) => <AdCard key={a.id} ad={a} />)}
          {d.site.links.length > 0 && (
            <Card title="Kurumsal bağlantılar" icon={<ExternalLink className="size-4" />}>
              <ul className="-mx-2">
                {d.site.links.map((l) => (
                  <li key={l.url}><a href={l.url} target="_blank" rel="noopener noreferrer" className="flex min-h-11 items-center justify-between gap-2 rounded-lg px-2 text-sm text-slate-700 hover:bg-slate-50">{l.label}<ExternalLink className="size-3.5 text-slate-400" aria-hidden="true" /></a></li>
                ))}
              </ul>
            </Card>
          )}
        </aside>
      </div>
    </div>
  )
}

/* =========================================================
   Hakkımızda
   ========================================================= */

export function AboutPage() {
  usePageTitle('Hakkımızda')
  const info = useSiteInfo()
  const s = info.data?.settings ?? {}
  return (
    <>
      <PageBand title={s[siteKeys.aboutTitle] ?? 'Hakkımızda'} description={s[siteKeys.tagline]} />
      <div className={clsx(container, 'grid gap-6 py-8 lg:grid-cols-3')}>
        <div className="rounded-xl border border-slate-200 bg-white p-6 text-slate-700 shadow-xs lg:col-span-2">
          {info.isLoading ? <Loading /> : info.isError ? <Failed error={info.error} onRetry={() => info.refetch()} /> : <Paragraphs text={s[siteKeys.aboutBody] ?? ''} />}
        </div>
        <div className="space-y-4">
          {s[siteKeys.mission] && <Card title="Misyonumuz"><p className="text-sm text-slate-700">{s[siteKeys.mission]}</p></Card>}
          {s[siteKeys.vision] && <Card title="Vizyonumuz"><p className="text-sm text-slate-700">{s[siteKeys.vision]}</p></Card>}
        </div>
      </div>
    </>
  )
}

/* =========================================================
   Departmanlarımız
   ========================================================= */

export function DepartmentsPage() {
  usePageTitle('Departmanlarımız')
  const q = useQuery({ queryKey: ['site', 'departments'], queryFn: siteApi.departments, staleTime: 5 * 60_000 })
  return (
    <>
      <PageBand title="Departmanlarımız" description="Şirketimizi oluşturan birimler ve sorumluluk alanları." />
      <div className={clsx(container, 'py-8')}>
        {q.isLoading ? <Loading rows={4} /> : q.isError ? <Failed error={q.error} onRetry={() => q.refetch()} /> : q.data!.length === 0 ? <Empty text="Tanıtılan departman yok." /> : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{q.data!.map((d) => <DepartmentTile key={d.id} d={d} />)}</div>
        )}
      </div>
    </>
  )
}

/* =========================================================
   Uygulamalar: portalda gerçekten çalışan modüller. Henüz yapılmamış olanlar listelenmez.
   ========================================================= */

const apps = [
  { icon: <Ticket className="size-5" />, title: 'Destek Talep Sistemi', text: 'BT, donanım ve mağaza sorunları için talep açma, dosya ekleme, durum ve yorum takibi.' },
  { icon: <Megaphone className="size-5" />, title: 'Duyurular', text: 'Şirket ve mağaza duyuruları; önemli duyurular ana sayfada sabitlenir.' },
  { icon: <CalendarClock className="size-5" />, title: 'Nöbetçi Personel', text: 'BT nöbet çizelgesi: mesai dışında kime ulaşılacağı.' },
  { icon: <Users className="size-5" />, title: 'Çalışan Rehberi', text: 'Çalışan arama, profil kartları ve iletişim bilgileri (yetki kapsamında).' },
  { icon: <Network className="size-5" />, title: 'Organizasyon Şeması', text: 'Departmanlar ve yönetici ilişkileri.' },
  { icon: <UserCheck className="size-5" />, title: 'Hesap Talepleri', text: 'İK yeni çalışan hesabı ister, BT onaylar; ilk şifreyi çalışan belirler.' },
  { icon: <MonitorSmartphone className="size-5" />, title: 'Envanter', text: 'Mağaza ve ofis cihazları, zimmet ve arıza geçmişi.' },
  { icon: <Store className="size-5" />, title: 'Mağazalar', text: 'Mağaza bilgileri ve mağaza bazlı talep görünümü.' },
]

export function AppsPage() {
  usePageTitle('Uygulamalar')
  return (
    <>
      <PageBand title="Uygulamalar" description="Portal içindeki uygulamalar. Her çalışan yalnızca yetkisi olan uygulamaları görür." />
      <div className={clsx(container, 'py-8')}>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {apps.map((a) => (
            <article key={a.title} className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
              <span className="grid size-10 place-items-center rounded-lg bg-indigo-50 text-indigo-600" aria-hidden="true">{a.icon}</span>
              <h2 className="mt-3 font-semibold text-slate-900">{a.title}</h2>
              <p className="mt-1 text-sm text-slate-600">{a.text}</p>
            </article>
          ))}
        </div>
        <div className="mt-6 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-indigo-100 bg-indigo-50 p-4">
          <p className="text-sm text-indigo-900">Departman uygulamaları (izin, masraf, satın alma ve diğerleri) sonraki aşamalarda portala eklenecek.</p>
          <Link to="/portal" className="inline-flex min-h-11 items-center gap-1.5 rounded-lg bg-indigo-600 px-4 text-sm font-semibold text-white hover:bg-indigo-500">Portala git <ArrowRight className="size-4" aria-hidden="true" /></Link>
        </div>
      </div>
    </>
  )
}

/* =========================================================
   Duyurular
   ========================================================= */

export function PublicAnnouncementsPage() {
  usePageTitle('Duyurular')
  const q = useQuery({ queryKey: ['site', 'announcements'], queryFn: siteApi.announcements, staleTime: 60_000 })
  return (
    <>
      <PageBand title="Duyurular" description="Herkese açık şirket duyuruları. Çalışanlara özel duyurular portalda yer alır." />
      <div className={clsx(container, 'py-8')}>
        <div className="max-w-3xl rounded-xl border border-slate-200 bg-white p-4 shadow-xs sm:p-6">
          {q.isLoading ? <Loading /> : q.isError ? <Failed error={q.error} onRetry={() => q.refetch()} /> : q.data!.length === 0 ? <Empty text="Şu anda yayında duyuru yok." /> : (
            <div className="divide-y divide-slate-100">{q.data!.map((a) => <div key={a.id} className="py-4 first:pt-0 last:pb-0"><AnnouncementItem a={a} full /></div>)}</div>
          )}
        </div>
      </div>
    </>
  )
}

/* =========================================================
   Haberler
   ========================================================= */

export function NewsListPage() {
  usePageTitle('Haberler')
  const [page, setPage] = useState(1)
  const q = useQuery({ queryKey: ['site', 'news', page], queryFn: () => siteApi.news(page, 9), staleTime: 60_000, placeholderData: (p) => p })
  const data = q.data
  return (
    <>
      <PageBand title="Haberler" description="Şirketimizden gelişmeler." />
      <div className={clsx(container, 'py-8')}>
        {q.isLoading ? <Loading rows={3} /> : q.isError ? <Failed error={q.error} onRetry={() => q.refetch()} /> : data!.items.length === 0 ? <Empty text="Henüz haber yayımlanmadı." /> : (
          <>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{data!.items.map((n) => <NewsTile key={n.id} n={n} />)}</div>
            {data!.totalPages > 1 && (
              <nav aria-label="Sayfalama" className="mt-6 flex items-center justify-center gap-2">
                <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="min-h-11 rounded-lg border border-slate-200 bg-white px-4 text-sm disabled:opacity-50">Önceki sayfa</button>
                <span className="text-sm text-slate-500">{page} / {data!.totalPages}</span>
                <button type="button" disabled={page >= data!.totalPages} onClick={() => setPage((p) => p + 1)} className="min-h-11 rounded-lg border border-slate-200 bg-white px-4 text-sm disabled:opacity-50">Sonraki sayfa</button>
              </nav>
            )}
          </>
        )}
      </div>
    </>
  )
}

export function NewsDetailPage() {
  const { slug = '' } = useParams()
  const q = useQuery({ queryKey: ['site', 'news', slug], queryFn: () => siteApi.newsDetail(slug), retry: false })
  usePageTitle(q.data?.title ?? 'Haber')
  const n = q.data
  return (
    <div className={clsx(container, 'py-8')}>
      <Link to="/haberler" className="mb-4 inline-flex min-h-11 items-center gap-1 text-sm font-medium text-indigo-600"><ChevronLeft className="size-4" aria-hidden="true" />Tüm haberler</Link>
      {q.isLoading ? <Loading /> : q.isError || !n ? (
        <div className="rounded-xl border border-slate-200 bg-white p-8 text-center">
          <h1 className="text-xl font-semibold text-slate-900">Haber bulunamadı</h1>
          <p className="mt-2 text-sm text-slate-600">Bu haber yayından kaldırılmış veya adres hatalı olabilir.</p>
        </div>
      ) : (
        <article className="mx-auto max-w-3xl overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xs">
          {n.imageUrl && <img src={n.imageUrl} alt="" className="aspect-[16/7] w-full object-cover" onError={(e) => { e.currentTarget.style.display = 'none' }} />}
          <div className="p-6 sm:p-8">
            <time dateTime={n.publishedAt} className="text-sm text-slate-400">{formatDate(n.publishedAt)}</time>
            <h1 className="mt-1 text-2xl font-bold text-slate-900 sm:text-3xl">{n.title}</h1>
            <p className="mt-3 text-lg text-slate-600">{n.summary}</p>
            <Paragraphs text={n.body} className="mt-6 text-slate-700" />
          </div>
        </article>
      )}
    </div>
  )
}

/* =========================================================
   Kariyer
   ========================================================= */

export function CareersPage() {
  usePageTitle('Kariyer')
  const q = useQuery({ queryKey: ['site', 'jobs'], queryFn: siteApi.jobs, staleTime: 60_000 })
  const [term, setTerm] = useState('')
  const list = (q.data ?? []).filter((j) => [j.title, j.department, j.location].join(' ').toLocaleLowerCase('tr').includes(term.trim().toLocaleLowerCase('tr')))
  return (
    <>
      <PageBand title="Kariyer" description="Ekibimize katılın. Açık pozisyonlarımız aşağıdadır." />
      <div className={clsx(container, 'py-8')}>
        <div className="max-w-3xl rounded-xl border border-slate-200 bg-white p-4 shadow-xs sm:p-6">
          <label className="relative mb-4 block">
            <span className="sr-only">Pozisyon ara</span>
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" aria-hidden="true" />
            <input value={term} onChange={(e) => setTerm(e.target.value)} placeholder="Pozisyon, departman veya şehir" className="min-h-11 w-full rounded-lg border border-slate-200 pr-3 pl-9 text-sm text-slate-800 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-500/20" />
          </label>
          {q.isLoading ? <Loading /> : q.isError ? <Failed error={q.error} onRetry={() => q.refetch()} /> : list.length === 0
            ? <Empty text={term ? 'Aramanızla eşleşen pozisyon yok.' : 'Şu anda açık pozisyon yok.'} />
            : <ul className="-mx-2 divide-y divide-slate-100">{list.map((j) => <JobRow key={j.id} j={j} />)}</ul>}
        </div>
      </div>
    </>
  )
}

export function JobDetailPage() {
  const { id = '' } = useParams()
  const q = useQuery({ queryKey: ['site', 'job', id], queryFn: () => siteApi.job(Number(id)), retry: false, enabled: /^\d+$/.test(id) })
  usePageTitle(q.data?.title ?? 'Açık pozisyon')
  const j = q.data
  return (
    <div className={clsx(container, 'py-8')}>
      <Link to="/kariyer" className="mb-4 inline-flex min-h-11 items-center gap-1 text-sm font-medium text-indigo-600"><ChevronLeft className="size-4" aria-hidden="true" />Tüm pozisyonlar</Link>
      {q.isLoading ? <Loading /> : !j ? (
        <div className="rounded-xl border border-slate-200 bg-white p-8 text-center">
          <h1 className="text-xl font-semibold text-slate-900">İlan bulunamadı</h1>
          <p className="mt-2 text-sm text-slate-600">Bu pozisyon kapanmış veya yayından kaldırılmış olabilir.</p>
        </div>
      ) : (
        <article className="mx-auto max-w-3xl rounded-xl border border-slate-200 bg-white p-6 shadow-xs sm:p-8">
          <h1 className="text-2xl font-bold text-slate-900">{j.title}</h1>
          <p className="mt-2 text-sm text-slate-500">{[j.department, j.location, employmentLabels[j.employmentType]].filter(Boolean).join(' · ')}</p>
          <p className="mt-1 text-xs text-slate-400">Yayın: {formatDate(j.publishedAt)}{j.closesOn && ` · Son başvuru: ${formatDate(j.closesOn)}`}</p>
          <h2 className="mt-6 font-semibold text-slate-900">Pozisyon hakkında</h2>
          <Paragraphs text={j.description} className="mt-2 text-slate-700" />
          {j.requirements && (
            <>
              <h2 className="mt-6 font-semibold text-slate-900">Aradığımız nitelikler</h2>
              <ul className="mt-2 list-disc space-y-1 pl-5 text-slate-700">{j.requirements.split('\n').filter((r) => r.trim()).map((r) => <li key={r}>{r}</li>)}</ul>
            </>
          )}
          {j.applyEmail && (
            <a href={`mailto:${j.applyEmail}?subject=${encodeURIComponent(`Başvuru: ${j.title}`)}`} className="mt-8 inline-flex min-h-11 items-center gap-2 rounded-lg bg-indigo-600 px-4 text-sm font-semibold text-white hover:bg-indigo-500">
              <Mail className="size-4" aria-hidden="true" />E-posta ile başvur
            </a>
          )}
        </article>
      )}
    </div>
  )
}

/* =========================================================
   İletişim
   ========================================================= */

export function ContactPage() {
  usePageTitle('İletişim')
  const info = useSiteInfo()
  const s = info.data?.settings ?? {}
  const rows = [
    s[siteKeys.address] && { icon: <MapPin className="size-5" />, label: 'Adres', value: s[siteKeys.address] },
    s[siteKeys.phone] && { icon: <Phone className="size-5" />, label: 'Telefon', value: s[siteKeys.phone], href: `tel:${s[siteKeys.phone].replace(/\s/g, '')}` },
    s[siteKeys.email] && { icon: <Mail className="size-5" />, label: 'E-posta', value: s[siteKeys.email], href: `mailto:${s[siteKeys.email]}` },
    s[siteKeys.careerEmail] && { icon: <Briefcase className="size-5" />, label: 'Kariyer', value: s[siteKeys.careerEmail], href: `mailto:${s[siteKeys.careerEmail]}` },
  ].filter(Boolean) as { icon: ReactNode; label: string; value: string; href?: string }[]
  return (
    <>
      <PageBand title="İletişim" description="Bize aşağıdaki kanallardan ulaşabilirsiniz." />
      <div className={clsx(container, 'grid gap-6 py-8 lg:grid-cols-3')}>
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs lg:col-span-2">
          {info.isLoading ? <Loading /> : info.isError ? <Failed error={info.error} onRetry={() => info.refetch()} /> : (
            <dl className="grid gap-5 sm:grid-cols-2">
              {rows.map((r) => (
                <div key={r.label} className="flex gap-3">
                  <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-indigo-50 text-indigo-600" aria-hidden="true">{r.icon}</span>
                  <div className="min-w-0"><dt className="text-xs text-slate-500">{r.label}</dt><dd className="font-medium break-words text-slate-800">{r.href ? <a href={r.href} className="hover:text-indigo-700">{r.value}</a> : r.value}</dd></div>
                </div>
              ))}
            </dl>
          )}
        </div>
        <Card title="Çalışanlar için">
          <p className="text-sm text-slate-700">BT, donanım veya mağaza sorunlarınız için portaldan destek talebi açın; talebiniz doğru ekibe iletilir ve durumunu takip edebilirsiniz.</p>
          <Link to="/portal" className="mt-4 inline-flex min-h-11 items-center gap-1.5 rounded-lg bg-slate-900 px-4 text-sm font-semibold text-white hover:bg-slate-800">Portala git <ArrowRight className="size-4" aria-hidden="true" /></Link>
        </Card>
      </div>
    </>
  )
}

export function PublicNotFoundPage() {
  usePageTitle('Sayfa bulunamadı')
  return (
    <div className={clsx(container, 'py-16 text-center')}>
      <p className="text-sm font-semibold text-indigo-600">404</p>
      <h1 className="mt-2 text-2xl font-bold text-slate-900">Sayfa bulunamadı</h1>
      <p className="mt-2 text-slate-600">Aradığınız sayfa taşınmış veya kaldırılmış olabilir.</p>
      <div className="mt-6 flex flex-wrap justify-center gap-3">
        <Link to="/" className="inline-flex min-h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-semibold text-white">Ana sayfaya dön</Link>
        <Link to="/portal" className="inline-flex min-h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-semibold text-slate-800">Portala git</Link>
      </div>
    </div>
  )
}
