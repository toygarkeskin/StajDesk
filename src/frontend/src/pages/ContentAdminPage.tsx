import { useState, type FormEvent, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { BarChart3, ImageUp, Pencil, Plus, Trash2 } from 'lucide-react'
import { api, errorMessage, fieldErrors } from '@/lib/api'
import { contentApi, siteApi, siteKeys, type AdAdmin, type AdPage, type AdPlacement, type JobAdmin, type LinkItem, type NewsAdmin } from '@/lib/site'
import { employmentLabels } from '@/lib/labels'
import { formatDate, formatDateTime } from '@/lib/format'
import type { EmploymentType } from '@/lib/types'
import { useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { EventsAdmin, RecognitionsAdmin } from './PortalContentTabs'
import { Badge, BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'

/* =========================================================
   Kurumsal site içerik yönetimi: reklam/banner (ads.manage), haber, açık pozisyon, site metinleri (content.manage).
   Sekmeler izne göre görünür; asıl denetim API'dedir.
   ========================================================= */

type Tab = 'ads' | 'news' | 'jobs' | 'events' | 'recognitions' | 'settings'

/** API doğrulama hatası anahtarları "Title" veya "title" biçiminde gelebilir. */
const err = (e: Record<string, string>, key: string) => e[key] ?? e[key[0].toUpperCase() + key.slice(1)]

/** <input type="datetime-local"> ↔ ISO (yerel saat). */
const toLocalInput = (iso?: string | null) => {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}
const fromLocalInput = (v: string) => (v ? new Date(v).toISOString() : null)

export function ContentAdminPage() {
  const { can } = useCan()
  const [params, setParams] = useSearchParams()
  const tabs = ([
    can('ads.manage') && { id: 'ads', label: 'Reklam ve bannerlar' },
    can('content.manage') && { id: 'news', label: 'Haberler' },
    can('content.manage') && { id: 'jobs', label: 'Açık pozisyonlar' },
    can('content.manage') && { id: 'events', label: 'Etkinlikler' },
    can('content.manage', 'employees.manage') && { id: 'recognitions', label: 'Takdirler' },
    can('content.manage') && { id: 'settings', label: 'Site metinleri' },
  ].filter(Boolean)) as { id: Tab; label: string }[]
  const tab = (tabs.find((t) => t.id === params.get('tab'))?.id ?? tabs[0]?.id) as Tab

  return (
    <>
      <PageHeader title="Kurumsal site ve içerik" description="Kurumsal sitenin içerikleri (en geç 2 dakika içinde yayına girer) ve portal içerikleri: etkinlikler, takdirler."
        actions={<a href="/" target="_blank" rel="noopener" className="btn-secondary">Siteyi görüntüle</a>} />
      <div role="tablist" className="mb-6 flex gap-1 overflow-x-auto border-b border-gray-200 dark:border-gray-700/60">
        {tabs.map((t) => (
          <button key={t.id} role="tab" aria-selected={tab === t.id} onClick={() => setParams({ tab: t.id })}
            className={clsx('min-h-11 shrink-0 border-b-2 px-3 text-sm font-medium', tab === t.id ? 'border-violet-500 text-violet-600 dark:text-violet-300' : 'border-transparent text-gray-500 hover:text-gray-800 dark:hover:text-gray-200')}>
            {t.label}
          </button>
        ))}
      </div>
      {tab === 'ads' && <AdsAdmin />}
      {tab === 'news' && <NewsAdminTab />}
      {tab === 'jobs' && <JobsAdmin />}
      {tab === 'events' && <EventsAdmin />}
      {tab === 'recognitions' && <RecognitionsAdmin />}
      {tab === 'settings' && <SettingsAdmin />}
    </>
  )
}

/* ---------------- Ortak: görsel yükleme ---------------- */

function ImageUpload({ value, url, onChange, error, id }: { value: string; url?: string | null; onChange: (key: string, url: string) => void; error?: string; id: string }) {
  const upload = useMutation({
    mutationFn: contentApi.upload,
    onSuccess: (r) => onChange(r.key, r.url),
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <div>
      <div className="relative mb-2 aspect-[3/1] overflow-hidden rounded-lg border border-dashed border-gray-300 bg-gray-50 dark:border-gray-600 dark:bg-gray-900/40">
        {url ? <img src={url} alt="Seçilen görsel önizlemesi" className="absolute inset-0 size-full object-cover" />
          : <span className="absolute inset-0 grid place-items-center text-xs text-gray-400">Görsel seçilmedi</span>}
        {upload.isPending && <span className="absolute inset-0 grid place-items-center bg-white/60 dark:bg-gray-900/60"><Spinner className="size-6" /></span>}
      </div>
      <label className="btn-secondary">
        <ImageUp className="size-4" aria-hidden="true" /> {value ? 'Görseli değiştir' : 'Görsel yükle'}
        <input id={id} type="file" accept="image/png,image/jpeg,image/webp" className="sr-only" aria-invalid={!!error}
          onChange={(e) => { const f = e.target.files?.[0]; if (f) upload.mutate(f); e.target.value = '' }} />
      </label>
      <p className="mt-1 text-xs text-gray-400">PNG, JPG veya WEBP · en fazla 3 MB · önerilen oran 3:1 (ör. 1200×400)</p>
      {error && <p className="mt-1 text-xs text-rose-500">{error}</p>}
    </div>
  )
}

function Toggle({ id, label, checked, onChange, hint }: { id: string; label: string; checked: boolean; onChange: (v: boolean) => void; hint?: string }) {
  return (
    <label htmlFor={id} className="flex min-h-11 cursor-pointer items-start gap-3">
      <input id={id} type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="mt-1 size-4 accent-violet-500" />
      <span><span className="text-sm font-medium text-gray-800 dark:text-gray-200">{label}</span>{hint && <span className="block text-xs text-gray-500">{hint}</span>}</span>
    </label>
  )
}

function ListShell({ action, loading, error, onRetry, empty, children }: { action: ReactNode; loading: boolean; error: unknown; onRetry: () => void; empty: ReactNode | false; children: ReactNode }) {
  return (
    <section className="card overflow-hidden">
      <div className="flex justify-end border-b border-gray-100 p-3 dark:border-gray-700/60">{action}</div>
      {loading ? <div className="p-4"><BlockSkeleton className="h-40" /></div> : error ? <ErrorState message={errorMessage(error)} onRetry={onRetry} /> : empty || children}
    </section>
  )
}

/* =========================================================
   Reklamlar
   ========================================================= */

const statusBadge: Record<AdAdmin['status'], { label: string; className: string }> = {
  live: { label: 'Yayında', className: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400' },
  scheduled: { label: 'Zamanlandı', className: 'bg-sky-500/15 text-sky-700 dark:text-sky-400' },
  expired: { label: 'Süresi doldu', className: 'bg-gray-500/15 text-gray-600 dark:text-gray-400' },
  inactive: { label: 'Pasif', className: 'bg-gray-500/15 text-gray-600 dark:text-gray-400' },
}
const pageLabels: Record<AdPage, string> = { Landing: 'Kurumsal site', Portal: 'Portal' }
const placementLabels: Record<AdPlacement, string> = { Hero: 'Üst banner (dönen)', Sidebar: 'Yan sütun', Inline: 'İçerik arası' }
const nf = new Intl.NumberFormat('tr-TR')

function AdsAdmin() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['content', 'ads'], queryFn: contentApi.ads })
  const [editing, setEditing] = useState<AdAdmin | 'new' | null>(null)
  const [stats, setStats] = useState<AdAdmin | null>(null)
  const [toDelete, setToDelete] = useState<AdAdmin | null>(null)
  const remove = useMutation({
    mutationFn: (a: AdAdmin) => contentApi.removeAd(a.id),
    onSuccess: () => { toast('Reklam silindi.'); setToDelete(null); qc.invalidateQueries({ queryKey: ['content', 'ads'] }) },
    onError: (e) => { toast(errorMessage(e), 'error'); setToDelete(null) },
  })
  const rows = list.data ?? []
  return (
    <>
      <ListShell action={<button className="btn-violet" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Reklam ekle</button>}
        loading={list.isLoading} error={list.error} onRetry={() => list.refetch()}
        empty={rows.length === 0 && <EmptyState title="Henüz reklam yok" description="Kurumsal sitede veya portalda gösterilecek ilk banner'ı ekleyin." />}>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="table-head"><tr>
              <th className="px-4 py-3">Reklam</th><th className="px-4 py-3">Yer</th><th className="px-4 py-3">Yayın</th><th className="px-4 py-3">Durum</th>
              <th className="px-4 py-3 text-right">Gösterim</th><th className="px-4 py-3 text-right">Tıklama</th><th className="px-4 py-3 text-right">Oran</th><th className="px-4 py-3"><span className="sr-only">İşlemler</span></th>
            </tr></thead>
            <tbody>
              {rows.map((a) => (
                <tr key={a.id} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                  <td className="px-4 py-2">
                    <div className="flex items-center gap-3">
                      <img src={a.imageUrl} alt="" className="h-10 w-24 shrink-0 rounded object-cover" />
                      <div className="min-w-0"><div className="font-medium text-gray-800 dark:text-gray-100">{a.title}</div><div className="text-xs text-gray-500">Sıra {a.sortOrder}{a.linkUrl ? ' · bağlantılı' : ''}</div></div>
                    </div>
                  </td>
                  <td className="px-4 py-2 text-xs whitespace-nowrap">{pageLabels[a.page]}<div className="text-gray-500">{placementLabels[a.placement]}</div></td>
                  <td className="px-4 py-2 text-xs whitespace-nowrap">{formatDate(a.startsAt)}<div className="text-gray-500">{a.endsAt ? `→ ${formatDate(a.endsAt)}` : 'Süresiz'}</div></td>
                  <td className="px-4 py-2"><Badge className={statusBadge[a.status].className}>{statusBadge[a.status].label}</Badge></td>
                  <td className="px-4 py-2 text-right tabular-nums">{nf.format(a.impressions)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{nf.format(a.clicks)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">%{a.clickRate.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2">
                    <div className="flex justify-end gap-1">
                      <IconButton label={`${a.title} istatistikleri`} onClick={() => setStats(a)}><BarChart3 className="size-4" /></IconButton>
                      <IconButton label={`${a.title} reklamını düzenle`} onClick={() => setEditing(a)}><Pencil className="size-4" /></IconButton>
                      <IconButton label={`${a.title} reklamını sil`} tone="danger" onClick={() => setToDelete(a)}><Trash2 className="size-4" /></IconButton>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="border-t border-gray-100 px-4 py-3 text-xs text-gray-500 dark:border-gray-700/60">Süresi dolan reklam otomatik olarak yayından kalkar. Gösterim, reklam ekranda gerçekten göründüğünde ziyaretçi başına bir kez sayılır.</p>
      </ListShell>
      {editing && <AdForm ad={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {stats && <AdStatsPanel ad={stats} onClose={() => setStats(null)} />}
      <ConfirmDialog open={!!toDelete} title={`"${toDelete?.title}" silinsin mi?`} body="Reklam ve istatistikleri kalıcı olarak silinir. Yalnızca yayından kaldırmak istiyorsanız pasif yapın."
        confirmLabel="Reklamı sil" pending={remove.isPending} onConfirm={() => toDelete && remove.mutate(toDelete)} onCancel={() => setToDelete(null)} />
    </>
  )
}

function AdForm({ ad, onClose }: { ad: AdAdmin | null; onClose: () => void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({
    title: ad?.title ?? '', description: ad?.description ?? '', imageKey: ad?.imageKey ?? '', imageUrl: ad?.imageUrl ?? '',
    linkUrl: ad?.linkUrl ?? '', page: ad?.page ?? 'Landing' as AdPage, placement: ad?.placement ?? 'Hero' as AdPlacement,
    sortOrder: ad?.sortOrder ?? 1, startsAt: toLocalInput(ad?.startsAt ?? new Date().toISOString()), endsAt: toLocalInput(ad?.endsAt), isActive: ad?.isActive ?? true,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((p) => ({ ...p, [k]: v }))
  const save = useMutation({
    mutationFn: () => contentApi.saveAd(ad?.id ?? null, {
      title: f.title, description: f.description || null, imageKey: f.imageKey, linkUrl: f.linkUrl.trim() || null, page: f.page, placement: f.placement,
      sortOrder: Number(f.sortOrder) || 0, startsAt: fromLocalInput(f.startsAt), endsAt: fromLocalInput(f.endsAt), isActive: f.isActive,
    }),
    onSuccess: () => { toast(ad ? 'Reklam güncellendi.' : 'Reklam eklendi.'); qc.invalidateQueries({ queryKey: ['content', 'ads'] }); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!f.title.trim()) local.title = 'Reklam başlığı zorunludur.'
    if (!f.imageKey) local.imageKey = 'Reklam görselini yükleyin.'
    if (f.linkUrl.trim() && !/^https?:\/\/\S+$/i.test(f.linkUrl.trim())) local.linkUrl = 'Bağlantı https:// ile başlamalıdır.'
    if (f.endsAt && f.startsAt && new Date(f.endsAt) <= new Date(f.startsAt)) local.endsAt = 'Bitiş tarihi başlangıçtan sonra olmalıdır.'
    setErrors(local)
    if (Object.keys(local).length === 0) save.mutate()
  }
  return (
    <SlideOver open onClose={onClose} title={ad ? 'Reklamı düzenle' : 'Reklam ekle'} description="Görsel, metin, yayın aralığı ve gösterileceği yer."
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" form="ad-form" disabled={save.isPending}>{save.isPending && <Spinner />}Reklamı kaydet</button></div>}>
      <form id="ad-form" onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Görsel" htmlFor="ad-image" required error={err(errors, 'imageKey')}>
          <ImageUpload id="ad-image" value={f.imageKey} url={f.imageUrl} onChange={(k, u) => setF((p) => ({ ...p, imageKey: k, imageUrl: u }))} />
        </Field>
        <Field label="Başlık" htmlFor="ad-title" required error={err(errors, 'title')}>
          <input id="ad-title" className="form-field" value={f.title} maxLength={150} onChange={(e) => set('title', e.target.value)} aria-invalid={!!err(errors, 'title')} />
        </Field>
        <Field label="Açıklama" htmlFor="ad-desc" optional error={err(errors, 'description')}>
          <textarea id="ad-desc" className="form-field" rows={2} maxLength={300} value={f.description} onChange={(e) => set('description', e.target.value)} />
        </Field>
        <Field label="Tıklama bağlantısı" htmlFor="ad-link" optional error={err(errors, 'linkUrl')} hint="Boş bırakılırsa reklam tıklanamaz. Yalnızca https:// veya http:// adresleri.">
          <input id="ad-link" className="form-field" inputMode="url" placeholder="https://" value={f.linkUrl} onChange={(e) => set('linkUrl', e.target.value)} aria-invalid={!!err(errors, 'linkUrl')} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Sayfa" htmlFor="ad-page">
            <select id="ad-page" className="form-field" value={f.page} onChange={(e) => set('page', e.target.value as AdPage)}>
              {Object.entries(pageLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </Field>
          <Field label="Konum" htmlFor="ad-place">
            <select id="ad-place" className="form-field" value={f.placement} onChange={(e) => set('placement', e.target.value as AdPlacement)}>
              {Object.entries(placementLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </Field>
          <Field label="Başlangıç" htmlFor="ad-start" required error={err(errors, 'startsAt')}>
            <input id="ad-start" type="datetime-local" className="form-field" value={f.startsAt} onChange={(e) => set('startsAt', e.target.value)} />
          </Field>
          <Field label="Bitiş" htmlFor="ad-end" optional error={err(errors, 'endsAt')} hint="Boşsa süresiz yayında kalır.">
            <input id="ad-end" type="datetime-local" className="form-field" value={f.endsAt} onChange={(e) => set('endsAt', e.target.value)} aria-invalid={!!err(errors, 'endsAt')} />
          </Field>
          <Field label="Sıra" htmlFor="ad-order" hint="Küçük sayı önce gösterilir.">
            <input id="ad-order" type="number" min={0} max={1000} className="form-field" value={f.sortOrder} onChange={(e) => set('sortOrder', Number(e.target.value))} />
          </Field>
        </div>
        <Toggle id="ad-active" label="Aktif" checked={f.isActive} onChange={(v) => set('isActive', v)} hint="Pasif reklam tarih aralığından bağımsız olarak gösterilmez." />
      </form>
    </SlideOver>
  )
}

function AdStatsPanel({ ad, onClose }: { ad: AdAdmin; onClose: () => void }) {
  const q = useQuery({ queryKey: ['content', 'ads', ad.id, 'stats'], queryFn: () => contentApi.adStats(ad.id) })
  const rows = q.data ?? []
  const max = Math.max(1, ...rows.map((r) => r.impressions))
  return (
    <SlideOver open onClose={onClose} title={`${ad.title} · istatistikler`} description="Son 30 günün günlük gösterim ve tıklamaları."
      footer={<div className="flex justify-end"><button className="btn-secondary" onClick={onClose}>Kapat</button></div>}>
      <dl className="mb-4 grid grid-cols-3 gap-3 text-center">
        {[['Gösterim', nf.format(ad.impressions)], ['Tıklama', nf.format(ad.clicks)], ['Tıklama oranı', `%${ad.clickRate.toLocaleString('tr-TR')}`]].map(([k, v]) => (
          <div key={k} className="rounded-lg bg-gray-50 p-3 dark:bg-gray-900/40"><dt className="text-xs text-gray-500">{k}</dt><dd className="text-lg font-semibold text-gray-800 tabular-nums dark:text-gray-100">{v}</dd></div>
        ))}
      </dl>
      {q.isLoading ? <BlockSkeleton className="h-40" /> : q.isError ? <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : rows.length === 0 ? <p className="text-sm text-gray-500">Son 30 günde gösterim yok.</p> : (
        <table className="w-full text-sm">
          <thead className="table-head"><tr><th className="px-2 py-2">Gün</th><th className="px-2 py-2">Gösterim</th><th className="px-2 py-2 text-right">Tıklama</th></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.day} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                <td className="px-2 py-1.5 whitespace-nowrap">{formatDate(r.day)}</td>
                <td className="px-2 py-1.5"><div className="flex items-center gap-2"><span className="h-2 rounded bg-violet-500" style={{ width: `${(r.impressions / max) * 100}%`, minWidth: 2 }} aria-hidden="true" /><span className="tabular-nums">{nf.format(r.impressions)}</span></div></td>
                <td className="px-2 py-1.5 text-right tabular-nums">{nf.format(r.clicks)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </SlideOver>
  )
}

/* =========================================================
   Haberler
   ========================================================= */

function NewsAdminTab() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['content', 'news'], queryFn: contentApi.news })
  const [editing, setEditing] = useState<NewsAdmin | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<NewsAdmin | null>(null)
  const remove = useMutation({
    mutationFn: (n: NewsAdmin) => contentApi.removeNews(n.id),
    onSuccess: () => { toast('Haber silindi.'); setToDelete(null); qc.invalidateQueries({ queryKey: ['content', 'news'] }) },
    onError: (e) => { toast(errorMessage(e), 'error'); setToDelete(null) },
  })
  const rows = list.data ?? []
  return (
    <>
      <ListShell action={<button className="btn-violet" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Haber ekle</button>}
        loading={list.isLoading} error={list.error} onRetry={() => list.refetch()}
        empty={rows.length === 0 && <EmptyState title="Henüz haber yok" description="Kurumsal sitede yayımlanacak ilk haberi ekleyin." />}>
        <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {rows.map((n) => (
            <li key={n.id} className="flex items-center gap-3 px-4 py-3">
              {n.imageUrl ? <img src={n.imageUrl} alt="" className="h-12 w-20 shrink-0 rounded object-cover" /> : <span className="h-12 w-20 shrink-0 rounded bg-gray-100 dark:bg-gray-700/40" aria-hidden="true" />}
              <div className="min-w-0 flex-1">
                <div className="font-medium text-gray-800 dark:text-gray-100">{n.title}</div>
                <div className="text-xs text-gray-500">/haberler/{n.slug} · {formatDateTime(n.publishedAt)} · {n.authorName}</div>
              </div>
              <Badge className={n.isPublished ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400' : 'bg-gray-500/15 text-gray-600 dark:text-gray-400'}>{n.isPublished ? 'Yayında' : 'Taslak'}</Badge>
              <IconButton label={`${n.title} haberini düzenle`} onClick={() => setEditing(n)}><Pencil className="size-4" /></IconButton>
              <IconButton label={`${n.title} haberini sil`} tone="danger" onClick={() => setToDelete(n)}><Trash2 className="size-4" /></IconButton>
            </li>
          ))}
        </ul>
      </ListShell>
      {editing && <NewsForm news={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog open={!!toDelete} title={`"${toDelete?.title}" silinsin mi?`} body="Haber kalıcı olarak silinir ve adresi çalışmaz. Yalnızca gizlemek için taslağa alın."
        confirmLabel="Haberi sil" pending={remove.isPending} onConfirm={() => toDelete && remove.mutate(toDelete)} onCancel={() => setToDelete(null)} />
    </>
  )
}

function NewsForm({ news, onClose }: { news: NewsAdmin | null; onClose: () => void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({
    title: news?.title ?? '', summary: news?.summary ?? '', body: news?.body ?? '', imageKey: news?.imageKey ?? '', imageUrl: news?.imageUrl ?? '',
    isPublished: news?.isPublished ?? false, publishedAt: toLocalInput(news?.publishedAt ?? new Date().toISOString()),
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((p) => ({ ...p, [k]: v }))
  const save = useMutation({
    mutationFn: () => contentApi.saveNews(news?.id ?? null, { title: f.title, summary: f.summary, body: f.body, imageKey: f.imageKey || null, isPublished: f.isPublished, publishedAt: fromLocalInput(f.publishedAt) }),
    onSuccess: () => { toast(news ? 'Haber güncellendi.' : 'Haber eklendi.'); qc.invalidateQueries({ queryKey: ['content', 'news'] }); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!f.title.trim()) local.title = 'Haber başlığı zorunludur.'
    if (!f.summary.trim()) local.summary = 'Kısa özet zorunludur.'
    if (!f.body.trim()) local.body = 'Haber metni zorunludur.'
    setErrors(local)
    if (Object.keys(local).length === 0) save.mutate()
  }
  return (
    <SlideOver wide open onClose={onClose} title={news ? 'Haberi düzenle' : 'Haber ekle'} description="Metin düz yazıdır; paragrafları boş satırla ayırın."
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" form="news-form" disabled={save.isPending}>{save.isPending && <Spinner />}Haberi kaydet</button></div>}>
      <form id="news-form" onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Başlık" htmlFor="n-title" required error={err(errors, 'title')}>
          <input id="n-title" className="form-field" maxLength={200} value={f.title} onChange={(e) => set('title', e.target.value)} aria-invalid={!!err(errors, 'title')} />
        </Field>
        <Field label="Kısa özet" htmlFor="n-sum" required error={err(errors, 'summary')}>
          <textarea id="n-sum" className="form-field" rows={2} maxLength={400} value={f.summary} onChange={(e) => set('summary', e.target.value)} aria-invalid={!!err(errors, 'summary')} />
        </Field>
        <Field label="Haber metni" htmlFor="n-body" required error={err(errors, 'body')}>
          <textarea id="n-body" className="form-field" rows={10} value={f.body} onChange={(e) => set('body', e.target.value)} aria-invalid={!!err(errors, 'body')} />
        </Field>
        <Field label="Kapak görseli" htmlFor="n-image" optional>
          <ImageUpload id="n-image" value={f.imageKey} url={f.imageUrl} onChange={(k, u) => setF((p) => ({ ...p, imageKey: k, imageUrl: u }))} />
          {f.imageKey && <button type="button" className="mt-2 text-xs font-medium text-rose-600" onClick={() => setF((p) => ({ ...p, imageKey: '', imageUrl: '' }))}>Görseli kaldır</button>}
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Yayın tarihi" htmlFor="n-date" hint="İleri bir tarih seçilirse haber o zaman görünür.">
            <input id="n-date" type="datetime-local" className="form-field" value={f.publishedAt} onChange={(e) => set('publishedAt', e.target.value)} />
          </Field>
          <div className="sm:pt-6"><Toggle id="n-pub" label="Yayımla" checked={f.isPublished} onChange={(v) => set('isPublished', v)} hint="Kapalıysa taslak olarak kalır." /></div>
        </div>
      </form>
    </SlideOver>
  )
}

/* =========================================================
   Açık pozisyonlar
   ========================================================= */

function JobsAdmin() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['content', 'jobs'], queryFn: contentApi.jobs })
  const [editing, setEditing] = useState<JobAdmin | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<JobAdmin | null>(null)
  const remove = useMutation({
    mutationFn: (j: JobAdmin) => contentApi.removeJob(j.id),
    onSuccess: () => { toast('İlan silindi.'); setToDelete(null); qc.invalidateQueries({ queryKey: ['content', 'jobs'] }) },
    onError: (e) => { toast(errorMessage(e), 'error'); setToDelete(null) },
  })
  const rows = list.data ?? []
  return (
    <>
      <ListShell action={<button className="btn-violet" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> İlan ekle</button>}
        loading={list.isLoading} error={list.error} onRetry={() => list.refetch()}
        empty={rows.length === 0 && <EmptyState title="Henüz ilan yok" description="Kariyer sayfasında yayımlanacak ilk açık pozisyonu ekleyin." />}>
        <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {rows.map((j) => (
            <li key={j.id} className="flex items-center gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <div className="font-medium text-gray-800 dark:text-gray-100">{j.title}</div>
                <div className="text-xs text-gray-500">{[j.department, j.location, employmentLabels[j.employmentType], j.closesOn && `son başvuru ${formatDate(j.closesOn)}`].filter(Boolean).join(' · ')}</div>
              </div>
              <Badge className={j.isPublished ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400' : 'bg-gray-500/15 text-gray-600 dark:text-gray-400'}>{j.isPublished ? 'Yayında' : 'Taslak'}</Badge>
              <IconButton label={`${j.title} ilanını düzenle`} onClick={() => setEditing(j)}><Pencil className="size-4" /></IconButton>
              <IconButton label={`${j.title} ilanını sil`} tone="danger" onClick={() => setToDelete(j)}><Trash2 className="size-4" /></IconButton>
            </li>
          ))}
        </ul>
      </ListShell>
      {editing && <JobForm job={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog open={!!toDelete} title={`"${toDelete?.title}" silinsin mi?`} body="İlan kalıcı olarak silinir. Başvuruları durdurmak için taslağa almanız yeterlidir."
        confirmLabel="İlanı sil" pending={remove.isPending} onConfirm={() => toDelete && remove.mutate(toDelete)} onCancel={() => setToDelete(null)} />
    </>
  )
}

function JobForm({ job, onClose }: { job: JobAdmin | null; onClose: () => void }) {
  const qc = useQueryClient()
  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const [f, setF] = useState({
    title: job?.title ?? '', departmentId: job?.departmentId ? String(job.departmentId) : '', location: job?.location ?? '',
    employmentType: job?.employmentType ?? 'FullTime' as EmploymentType, description: job?.description ?? '', requirements: job?.requirements ?? '',
    isPublished: job?.isPublished ?? true, closesOn: job?.closesOn ?? '',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((p) => ({ ...p, [k]: v }))
  const save = useMutation({
    mutationFn: () => contentApi.saveJob(job?.id ?? null, {
      title: f.title, departmentId: f.departmentId ? Number(f.departmentId) : null, location: f.location, employmentType: f.employmentType,
      description: f.description, requirements: f.requirements || null, isPublished: f.isPublished, closesOn: f.closesOn || null,
    }),
    onSuccess: () => { toast(job ? 'İlan güncellendi.' : 'İlan eklendi.'); qc.invalidateQueries({ queryKey: ['content', 'jobs'] }); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!f.title.trim()) local.title = 'Pozisyon adı zorunludur.'
    if (!f.location.trim()) local.location = 'Çalışma yerini girin.'
    if (!f.description.trim()) local.description = 'Pozisyon açıklaması zorunludur.'
    setErrors(local)
    if (Object.keys(local).length === 0) save.mutate()
  }
  return (
    <SlideOver wide open onClose={onClose} title={job ? 'İlanı düzenle' : 'İlan ekle'} description="Kariyer sayfasında görünen açık pozisyon."
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" form="job-form" disabled={save.isPending}>{save.isPending && <Spinner />}İlanı kaydet</button></div>}>
      <form id="job-form" onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Pozisyon" htmlFor="j-title" required error={err(errors, 'title')}>
          <input id="j-title" className="form-field" maxLength={150} value={f.title} onChange={(e) => set('title', e.target.value)} aria-invalid={!!err(errors, 'title')} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Departman" htmlFor="j-dept" optional>
            <select id="j-dept" className="form-field" value={f.departmentId} onChange={(e) => set('departmentId', e.target.value)}>
              <option value="">Belirtilmedi</option>
              {departments.data?.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label="Çalışma yeri" htmlFor="j-loc" required error={err(errors, 'location')}>
            <input id="j-loc" className="form-field" maxLength={100} placeholder="İstanbul / Merkez" value={f.location} onChange={(e) => set('location', e.target.value)} aria-invalid={!!err(errors, 'location')} />
          </Field>
          <Field label="Çalışma türü" htmlFor="j-type">
            <select id="j-type" className="form-field" value={f.employmentType} onChange={(e) => set('employmentType', e.target.value as EmploymentType)}>
              {Object.entries(employmentLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </Field>
          <Field label="Son başvuru" htmlFor="j-close" optional error={err(errors, 'closesOn')} hint="Bu tarihten sonra ilan otomatik gizlenir.">
            <input id="j-close" type="date" className="form-field" value={f.closesOn} onChange={(e) => set('closesOn', e.target.value)} />
          </Field>
        </div>
        <Field label="Açıklama" htmlFor="j-desc" required error={err(errors, 'description')}>
          <textarea id="j-desc" className="form-field" rows={6} value={f.description} onChange={(e) => set('description', e.target.value)} aria-invalid={!!err(errors, 'description')} />
        </Field>
        <Field label="Aranan nitelikler" htmlFor="j-req" optional hint="Her satır bir madde olarak gösterilir.">
          <textarea id="j-req" className="form-field" rows={5} value={f.requirements} onChange={(e) => set('requirements', e.target.value)} />
        </Field>
        <Toggle id="j-pub" label="Yayımla" checked={f.isPublished} onChange={(v) => set('isPublished', v)} />
      </form>
    </SlideOver>
  )
}

/* =========================================================
   Site metinleri ve kurumsal bağlantılar
   ========================================================= */

const settingFields: { key: string; label: string; rows?: number; hint?: string }[] = [
  { key: siteKeys.companyName, label: 'Şirket adı' },
  { key: siteKeys.tagline, label: 'Kısa tanıtım cümlesi', rows: 2 },
  { key: siteKeys.aboutTitle, label: 'Hakkımızda başlığı' },
  { key: siteKeys.aboutBody, label: 'Hakkımızda metni', rows: 8, hint: 'Paragrafları boş satırla ayırın.' },
  { key: siteKeys.mission, label: 'Misyon', rows: 3 },
  { key: siteKeys.vision, label: 'Vizyon', rows: 3 },
  { key: siteKeys.address, label: 'Adres', rows: 2 },
  { key: siteKeys.phone, label: 'Telefon' },
  { key: siteKeys.email, label: 'İletişim e-postası' },
  { key: siteKeys.careerEmail, label: 'Kariyer başvuru e-postası' },
]

function SettingsAdmin() {
  const info = useQuery({ queryKey: ['site', 'info'], queryFn: siteApi.info })
  const cities = useQuery({ queryKey: ['live', 'cities'], queryFn: siteApi.cities, staleTime: Infinity })
  if (info.isLoading) return <BlockSkeleton className="h-96" />
  if (info.isError || !info.data) return <ErrorState message={errorMessage(info.error)} onRetry={() => info.refetch()} />
  return <SettingsForm initial={info.data.settings} initialLinks={info.data.links} cities={cities.data?.map((c) => c.name) ?? []} />
}

function SettingsForm({ initial, initialLinks, cities }: { initial: Record<string, string>; initialLinks: LinkItem[]; cities: string[] }) {
  const qc = useQueryClient()
  const [values, setValues] = useState<Record<string, string>>(initial)
  const [links, setLinks] = useState<LinkItem[]>(initialLinks)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useMutation({
    mutationFn: () => contentApi.saveSettings({ settings: values, links: links.filter((l) => l.label.trim() || l.url.trim()) }),
    onSuccess: (r) => { toast('Site metinleri kaydedildi.'); setErrors({}); qc.setQueryData(['site', 'info'], r); qc.invalidateQueries({ queryKey: ['site'] }) },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const bad = links.findIndex((l) => (l.label.trim() || l.url.trim()) && (!l.label.trim() || !/^https?:\/\/\S+$/i.test(l.url.trim())))
    if (bad >= 0) { setErrors({ links: `${bad + 1}. bağlantı için ad ve https:// ile başlayan adres girin.` }); return }
    save.mutate()
  }
  return (
    <form onSubmit={submit} className="grid gap-6 lg:grid-cols-3" noValidate>
      <section className="card space-y-4 p-4 sm:p-6 lg:col-span-2">
        <h2 className="text-base font-semibold">Metinler ve iletişim</h2>
        {settingFields.map((s) => (
          <Field key={s.key} label={s.label} htmlFor={`s-${s.key}`} hint={s.hint} error={errors[`Settings[${s.key}]`]}>
            {s.rows
              ? <textarea id={`s-${s.key}`} className="form-field" rows={s.rows} value={values[s.key] ?? ''} onChange={(e) => setValues((v) => ({ ...v, [s.key]: e.target.value }))} />
              : <input id={`s-${s.key}`} className="form-field" value={values[s.key] ?? ''} onChange={(e) => setValues((v) => ({ ...v, [s.key]: e.target.value }))} />}
          </Field>
        ))}
      </section>
      <div className="space-y-6">
        <section className="card space-y-4 p-4 sm:p-6">
          <h2 className="text-base font-semibold">Hava durumu</h2>
          <Field label="Varsayılan şehir" htmlFor="s-city" hint="Ziyaretçi kartta şehri değiştirebilir; seçimi kendi tarayıcısında saklanır.">
            <select id="s-city" className="form-field" value={values[siteKeys.weatherCity] ?? 'İstanbul'} onChange={(e) => setValues((v) => ({ ...v, [siteKeys.weatherCity]: e.target.value }))}>
              {cities.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </Field>
        </section>
        <section className="card space-y-3 p-4 sm:p-6">
          <h2 className="text-base font-semibold">Kurumsal bağlantılar</h2>
          <p className="text-xs text-gray-500">Ana sayfada "Kurumsal bağlantılar" kartında görünür.</p>
          {links.map((l, i) => (
            <div key={i} className="space-y-2 rounded-lg border border-gray-200 p-3 dark:border-gray-700/60">
              <label className="sr-only" htmlFor={`l-label-${i}`}>{i + 1}. bağlantı adı</label>
              <input id={`l-label-${i}`} className="form-field" placeholder="Bağlantı adı" value={l.label} onChange={(e) => setLinks((ls) => ls.map((x, j) => j === i ? { ...x, label: e.target.value } : x))} />
              <label className="sr-only" htmlFor={`l-url-${i}`}>{i + 1}. bağlantı adresi</label>
              <input id={`l-url-${i}`} className="form-field" placeholder="https://" inputMode="url" value={l.url} onChange={(e) => setLinks((ls) => ls.map((x, j) => j === i ? { ...x, url: e.target.value } : x))} />
              <button type="button" className="text-xs font-medium text-rose-600" onClick={() => setLinks((ls) => ls.filter((_, j) => j !== i))}>Bağlantıyı kaldır</button>
            </div>
          ))}
          {errors.links && <p className="text-xs text-rose-500">{errors.links}</p>}
          <button type="button" className="btn-secondary w-full" disabled={links.length >= 12} onClick={() => setLinks((ls) => [...ls, { label: '', url: '' }])}><Plus className="size-4" aria-hidden="true" /> Bağlantı ekle</button>
        </section>
        <button className="btn-violet w-full" disabled={save.isPending}>{save.isPending && <Spinner />}Site metinlerini kaydet</button>
      </div>
    </form>
  )
}
