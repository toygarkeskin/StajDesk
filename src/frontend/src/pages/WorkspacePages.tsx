import { useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { CalendarClock, Check, Eye, Megaphone, Paperclip, Pencil, Plus, Trash2, Users2 } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { formatDateTime } from '@/lib/format'
import type { Announcement, AnnouncementLevel, OnCallShift } from '@/lib/types'
import { useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'
import { AnnouncementItem, OnCallCard, formatDay } from './DashboardPage'

/* =========================================================
   Duyurular — herkes okur; announcements.manage izni olan yayınlar.
   ========================================================= */

const toLocalInput = (iso?: string | null) => {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

export function AnnouncementsPage() {
  const { can } = useCan()
  const canManage = can('announcements.manage')
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['announcements', canManage], queryFn: () => api.announcements.list(canManage) })
  const [editing, setEditing] = useState<Announcement | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<Announcement | null>(null)
  // Genel aramadan #duyuru-ID ile gelindiyse o duyuruya kaydır
  const { hash } = useLocation()
  useEffect(() => { if (hash && list.data) document.getElementById(hash.slice(1))?.scrollIntoView({ block: 'start' }) }, [hash, list.data])
  const now = Date.now()
  const [readersOf, setReadersOf] = useState<Announcement | null>(null)

  // Okundu bilgisi: sayfada görünen, yayındaki ve henüz okunmamış duyurular bir kez işaretlenir.
  const marked = useRef(new Set<number>())
  const [readNow, setReadNow] = useState<Set<number>>(new Set())
  useEffect(() => {
    if (!list.data) return
    const due = list.data.filter((a) => !a.isRead && !marked.current.has(a.id) && new Date(a.publishAt).getTime() <= Date.now() && (!a.expiresAt || new Date(a.expiresAt).getTime() > Date.now()))
    if (!due.length) return
    due.forEach((a) => marked.current.add(a.id))
    void Promise.allSettled(due.map((a) => api.announcements.markRead(a.id))).then((rs) => {
      const ok = due.filter((_, i) => rs[i]?.status === 'fulfilled').map((a) => a.id)
      setReadNow((s) => new Set([...s, ...ok]))
      qc.invalidateQueries({ queryKey: ['home'] })
      if (ok.length) qc.invalidateQueries({ queryKey: ['announcements'] }) // okuyan sayıları güncellensin
    })
  }, [list.data, qc])

  const remove = useMutation({
    mutationFn: (a: Announcement) => api.announcements.remove(a.id),
    onSuccess: (_, a) => {
      toast(`"${a.title}" duyurusu silindi.`)
      setToDelete(null)
      qc.invalidateQueries({ queryKey: ['announcements'] })
      qc.invalidateQueries({ queryKey: ['home'] })
    },
    onError: (err) => toast(errorMessage(err), 'error'),
  })

  const stateOf = (a: Announcement) =>
    new Date(a.publishAt).getTime() > now ? { label: 'Zamanlandı', cls: 'bg-sky-500/10 text-sky-700 dark:text-sky-300' }
      : a.expiresAt && new Date(a.expiresAt).getTime() <= now ? { label: 'Süresi doldu', cls: 'bg-gray-500/10 text-gray-600 dark:text-gray-400' }
      : { label: 'Yayında', cls: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300' }

  return (
    <>
      <PageHeader
        title="Duyurular"
        description={canManage ? 'Panelde herkesin göreceği duyuruları yayınlayın; mağazaya özel duyurular yalnızca o mağazaya görünür.' : 'Şirket ve mağazanızla ilgili güncel duyurular.'}
        actions={canManage && <button className="btn-violet" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Duyuru yayınla</button>}
      />
      <section className="card divide-y divide-gray-100 dark:divide-gray-700/60">
        {list.isLoading ? <div className="space-y-3 p-5"><BlockSkeleton className="h-16" /><BlockSkeleton className="h-16" /></div>
          : list.isError ? <ErrorState message={errorMessage(list.error)} details={errorDetails(list.error)} onRetry={() => list.refetch()} />
          : list.data!.length === 0 ? (
            <EmptyState title="Yayında duyuru yok" description={canManage ? 'İlk duyuruyu yayınlayarak tüm çalışanları bilgilendirin.' : 'Yeni bir duyuru yayınlandığında burada görünür.'}
              action={canManage && <button className="btn-violet" onClick={() => setEditing('new')}>Duyuru yayınla</button>} />
          ) : list.data!.map((a) => {
            const st = stateOf(a)
            return (
              <div key={a.id} id={`duyuru-${a.id}`} className="flex scroll-mt-20 flex-col gap-3 p-5 target:bg-violet-500/5 sm:flex-row sm:items-start">
                <div className="min-w-0 flex-1">
                  <AnnouncementItem a={a} clamp={false} />
                  <div className="mt-2 flex flex-wrap items-center gap-2 text-xs">
                    {a.targetDepartment && <span className="rounded bg-violet-500/10 px-2 py-0.5 font-medium text-violet-700 dark:text-violet-300">Hedef: {a.targetDepartment}</span>}
                    {a.attachmentName && <AttachmentLink a={a} />}
                    {(a.isRead || readNow.has(a.id)) && <span className="inline-flex items-center gap-1 text-emerald-600 dark:text-emerald-400"><Check className="size-3.5" aria-hidden="true" />Okundu</span>}
                    {canManage && a.audienceCount != null && (
                      <button type="button" onClick={() => setReadersOf(a)} className="inline-flex min-h-9 items-center gap-1 rounded px-1 font-medium text-gray-600 hover:text-violet-700 dark:text-gray-300">
                        <Users2 className="size-3.5" aria-hidden="true" />{a.readCount ?? 0} / {a.audienceCount} kişi okudu
                      </button>
                    )}
                  </div>
                </div>
                {canManage && (
                  <div className="flex shrink-0 items-center gap-1 sm:flex-col sm:items-end">
                    <span className={clsx('rounded px-2 py-0.5 text-xs font-medium', st.cls)}>{st.label}</span>
                    <div className="flex">
                      <IconButton label={`"${a.title}" duyurusunu düzenle`} onClick={() => setEditing(a)}><Pencil className="size-4" /></IconButton>
                      <IconButton label={`"${a.title}" duyurusunu sil`} tone="danger" onClick={() => setToDelete(a)}><Trash2 className="size-4" /></IconButton>
                    </div>
                  </div>
                )}
              </div>
            )
          })}
      </section>

      {editing && <AnnouncementPanel item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {readersOf && <ReadersPanel a={readersOf} onClose={() => setReadersOf(null)} />}
      <ConfirmDialog
        open={toDelete != null}
        title={`"${toDelete?.title}" duyurusu silinsin mi?`}
        body="Duyuru panelden ve listeden kaldırılır. Bu işlem geri alınamaz."
        confirmLabel="Duyuruyu sil"
        pending={remove.isPending}
        onConfirm={() => toDelete && remove.mutate(toDelete)}
        onCancel={() => setToDelete(null)}
      />
    </>
  )
}

function AnnouncementPanel({ item, onClose }: { item: Announcement | null; onClose: () => void }) {
  const qc = useQueryClient()
  const stores = useQuery({ queryKey: ['stores'], queryFn: api.stores.list })
  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const [file, setFile] = useState<File | null>(null)
  const form = useForm(
    {
      title: item?.title ?? '', body: item?.body ?? '', level: (item?.level ?? 'Info') as string, isPinned: item?.isPinned ? 'true' : 'false',
      publishAt: toLocalInput(item?.publishAt), expiresAt: toLocalInput(item?.expiresAt), storeId: item?.storeId ? String(item.storeId) : '',
      targetDepartmentId: item?.targetDepartmentId ? String(item.targetDepartmentId) : '',
    },
    {
      title: (v) => (v.trim().length < 3 ? 'En az 3 karakterlik bir başlık yazın.' : undefined),
      body: (v) => (!v.trim() ? 'Duyuru metnini yazın.' : undefined),
      expiresAt: (v, all) => (v && all.publishAt && v <= all.publishAt ? 'Bitiş, yayın tarihinden sonra olmalı.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: async () => {
      const v = form.values
      const body = {
        title: v.title, body: v.body, level: v.level as AnnouncementLevel, isPinned: v.isPinned === 'true',
        publishAt: v.publishAt ? new Date(v.publishAt).toISOString() : null, expiresAt: v.expiresAt ? new Date(v.expiresAt).toISOString() : null,
        storeId: v.storeId ? Number(v.storeId) : null,
        targetDepartmentId: v.targetDepartmentId ? Number(v.targetDepartmentId) : null,
      }
      const saved = item ? await api.announcements.update(item.id, body) : await api.announcements.create(body)
      // Ek dosya duyuru kaydedildikten sonra yüklenir (yeni duyuruda kimlik o an oluşur)
      if (file) await api.announcements.uploadAttachment(saved.id, file)
      return saved
    },
    onSuccess: () => {
      toast(item ? 'Duyuru güncellendi.' : 'Duyuru yayınlandı.')
      qc.invalidateQueries({ queryKey: ['announcements'] })
      qc.invalidateQueries({ queryKey: ['home'] })
      onClose()
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()

  return (
    <SlideOver
      open
      onClose={onClose}
      title={item ? 'Duyuruyu düzenle' : 'Yeni duyuru'}
      description="Kısa ve net yazın; ilk iki satır panelde görünür."
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} {item ? 'Değişiklikleri kaydet' : 'Duyuruyu yayınla'}</button></>}
    >
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-4">
        <Field label="Başlık" htmlFor="f-title" error={form.errors.title} required><input {...form.field('title')} className="form-field" maxLength={150} /></Field>
        <Field label="Metin" htmlFor="f-body" error={form.errors.body} required><textarea {...form.field('body')} className="form-field min-h-32" maxLength={2000} /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Önem" htmlFor="f-level">
            <select {...form.field('level')} className="form-field"><option value="Info">Bilgi</option><option value="Warning">Önemli</option><option value="Critical">Acil</option></select>
          </Field>
          <Field label="Kimler görsün?" htmlFor="f-storeId">
            <select {...form.field('storeId')} className="form-field">
              <option value="">Herkes</option>
              {stores.data?.map((s) => <option key={s.id} value={s.id}>Yalnızca {s.name}</option>)}
            </select>
          </Field>
          <Field label="Hedef departman" htmlFor="f-targetDepartmentId" hint="Seçilirse yalnızca o departmandaki çalışanlar görür; mağaza seçimiyle birlikte uygulanır.">
            <select {...form.field('targetDepartmentId')} className="form-field">
              <option value="">Tüm departmanlar</option>
              {departments.data?.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label="Yayın zamanı" htmlFor="f-publishAt" optional hint="Boş bırakılırsa hemen yayınlanır."><input type="datetime-local" {...form.field('publishAt')} className="form-field" /></Field>
          <Field label="Bitiş zamanı" htmlFor="f-expiresAt" optional error={form.errors.expiresAt} hint="Boşsa siz kaldırana kadar kalır."><input type="datetime-local" {...form.field('expiresAt')} className="form-field" /></Field>
        </div>
        <label className="flex min-h-11 items-center gap-2 text-sm text-gray-800 dark:text-gray-200">
          <input type="checkbox" className="size-4 accent-violet-500" checked={form.values.isPinned === 'true'} onChange={(e) => form.setValue('isPinned', e.target.checked ? 'true' : 'false')} />
          Panelde en üstte sabitle
        </label>
        <div>
          <div className="form-label flex items-center gap-2"><Paperclip className="size-4" aria-hidden="true" /> Ek dosya veya görsel <span className="font-normal text-gray-400">(isteğe bağlı)</span></div>
          {item?.attachmentName && !file && <AttachmentEditor item={item} />}
          <label className="btn-secondary mt-2">
            {file ? 'Başka dosya seç' : item?.attachmentName ? 'Eki değiştir' : 'Dosya seç'}
            <input type="file" className="sr-only" accept=".pdf,.png,.jpg,.jpeg,.webp,.docx,.xlsx,.txt" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          </label>
          {file && <p className="mt-1 text-xs text-gray-600 dark:text-gray-300">Kaydedince yüklenecek: {file.name}</p>}
          <p className="mt-1 text-xs text-gray-400">PDF, görsel, Word, Excel veya metin · en fazla 5 MB. Dosya içeriği uzantısıyla doğrulanır.</p>
        </div>
      </form>
    </SlideOver>
  )
}

/** Ek dosya: oturum bilgisiyle indirilir (doğrudan bağlantı kimlik doğrulamasız çalışmaz). Görseller yeni sekmede açılır. */
function AttachmentLink({ a }: { a: Announcement }) {
  const [busy, setBusy] = useState(false)
  const open = async () => {
    setBusy(true)
    try {
      const blob = await api.announcements.attachment(a.id)
      const url = URL.createObjectURL(blob)
      if (a.attachmentContentType?.startsWith('image/') || a.attachmentContentType === 'application/pdf') window.open(url, '_blank', 'noopener')
      else { const link = document.createElement('a'); link.href = url; link.download = a.attachmentName ?? 'ek'; link.click() }
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (e) {
      toast(errorMessage(e), 'error')
    } finally {
      setBusy(false)
    }
  }
  const kb = a.attachmentSize ? Math.max(1, Math.round(a.attachmentSize / 1024)) : null
  return (
    <button type="button" onClick={open} disabled={busy} className="inline-flex min-h-9 items-center gap-1 rounded px-1 font-medium text-violet-700 hover:underline dark:text-violet-300">
      {busy ? <Spinner /> : <Paperclip className="size-3.5" aria-hidden="true" />}{a.attachmentName}{kb && <span className="font-normal text-gray-500"> ({kb} KB)</span>}
    </button>
  )
}

function AttachmentEditor({ item }: { item: Announcement }) {
  const qc = useQueryClient()
  const remove = useMutation({
    mutationFn: () => api.announcements.removeAttachment(item.id),
    onSuccess: () => { toast('Ek dosya kaldırıldı.'); qc.invalidateQueries({ queryKey: ['announcements'] }) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <div className="flex items-center justify-between gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm dark:border-gray-700/60">
      <AttachmentLink a={item} />
      <button type="button" className="text-xs font-medium text-rose-600" disabled={remove.isPending} onClick={() => remove.mutate()}>Eki kaldır</button>
    </div>
  )
}

/** Okuyanlar (yalnızca duyuru yöneticisi): hedef kitle büyüklüğü ve okuyanlar listesi. */
function ReadersPanel({ a, onClose }: { a: Announcement; onClose: () => void }) {
  const q = useQuery({ queryKey: ['announcements', 'readers', a.id], queryFn: () => api.announcements.readers(a.id) })
  return (
    <SlideOver open onClose={onClose} title={`"${a.title}" · okuyanlar`} description={a.targetDepartment ? `Hedef: ${a.targetDepartment} departmanı` : 'Hedef: tüm çalışanlar'}
      footer={<div className="flex justify-end"><button className="btn-secondary" onClick={onClose}>Kapat</button></div>}>
      {q.isLoading ? <BlockSkeleton className="h-40" /> : q.isError ? <ErrorState message={errorMessage(q.error)} /> : (
        <>
          <p className="mb-4 flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300"><Eye className="size-4" aria-hidden="true" />
            <strong>{q.data!.readers.length}</strong> / {q.data!.audienceCount} kişi okudu
            {q.data!.audienceCount > 0 && <span className="text-gray-500">(%{Math.round((q.data!.readers.length / q.data!.audienceCount) * 100)})</span>}
          </p>
          {q.data!.readers.length === 0 ? <p className="text-sm text-gray-500">Henüz okuyan yok.</p> : (
            <ul className="divide-y divide-gray-100 text-sm dark:divide-gray-700/60">
              {q.data!.readers.map((r) => (
                <li key={r.userId} className="flex items-center justify-between gap-2 py-2">
                  <span><span className="font-medium text-gray-800 dark:text-gray-100">{r.fullName}</span><span className="block text-xs text-gray-500">{r.department ?? '—'}</span></span>
                  <time className="text-xs text-gray-500" dateTime={r.readAt}>{formatDateTime(r.readAt)}</time>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </SlideOver>
  )
}

/* =========================================================
   Bilgi işlem nöbet çizelgesi
   ========================================================= */

export function OnCallPage() {
  const { can } = useCan()
  const canManage = can('oncall.manage')
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['oncall'], queryFn: api.onCall.list })
  const [adding, setAdding] = useState(false)
  const [toDelete, setToDelete] = useState<OnCallShift | null>(null)
  const remove = useMutation({
    mutationFn: (s: OnCallShift) => api.onCall.remove(s.id),
    onSuccess: () => {
      toast('Nöbet silindi.')
      setToDelete(null)
      qc.invalidateQueries({ queryKey: ['oncall'] })
      qc.invalidateQueries({ queryKey: ['home'] })
    },
    onError: (err) => toast(errorMessage(err), 'error'),
  })
  const current = list.data?.find((s) => s.isCurrent)

  return (
    <>
      <PageHeader
        title="Nöbet çizelgesi"
        description="Mesai dışında acil bir sorun olduğunda aranacak bilgi işlem personeli."
        actions={canManage && <button className="btn-violet" onClick={() => setAdding(true)}><Plus className="size-4" aria-hidden="true" /> Nöbet ekle</button>}
      />
      {current && <div className="card mb-6 p-5"><OnCallCard s={current} caption="Şu anki nöbetçi" /></div>}
      <section className="card overflow-hidden">
        {list.isLoading ? <div className="p-5"><BlockSkeleton className="h-24" /></div>
          : list.isError ? <ErrorState message={errorMessage(list.error)} details={errorDetails(list.error)} onRetry={() => list.refetch()} />
          : list.data!.length === 0 ? (
            <EmptyState title="Planlanmış nöbet yok" description={canManage ? 'Önümüzdeki haftalar için nöbetçi ekleyin.' : 'Nöbet planlandığında burada görünür.'}
              action={canManage && <button className="btn-violet" onClick={() => setAdding(true)}>Nöbet ekle</button>} />
          ) : (
            <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
              {list.data!.map((s) => (
                <li key={s.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
                  <CalendarClock className={clsx('size-5 shrink-0', s.isCurrent ? 'text-emerald-500' : 'text-gray-400')} aria-hidden="true" />
                  <div className="min-w-0 flex-1 text-sm">
                    <p className="font-medium text-gray-800 dark:text-gray-100">{s.fullName} {s.isCurrent && <span className="ml-1 rounded bg-emerald-500/10 px-1.5 text-xs text-emerald-700 dark:text-emerald-300">Şu an nöbette</span>}</p>
                    <p className="text-gray-500">{formatDay(s.startsOn)} – {formatDay(s.endsOn)}{s.phone && ` · ${s.phone}`}{s.note && ` · ${s.note}`}</p>
                  </div>
                  {canManage && <IconButton label={`${s.fullName} nöbetini sil`} tone="danger" onClick={() => setToDelete(s)}><Trash2 className="size-4" /></IconButton>}
                </li>
              ))}
            </ul>
          )}
      </section>

      {adding && <OnCallPanel onClose={() => setAdding(false)} />}
      <ConfirmDialog
        open={toDelete != null}
        title={`${toDelete?.fullName} nöbeti silinsin mi?`}
        body={toDelete ? `${formatDay(toDelete.startsOn)} – ${formatDay(toDelete.endsOn)} nöbeti çizelgeden kaldırılır.` : ''}
        confirmLabel="Nöbeti sil"
        pending={remove.isPending}
        onConfirm={() => toDelete && remove.mutate(toDelete)}
        onCancel={() => setToDelete(null)}
      />
    </>
  )
}

function OnCallPanel({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const agents = useQuery({ queryKey: ['agents'], queryFn: api.users.agents })
  const today = new Date()
  const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
  const monday = new Date(today)
  monday.setDate(today.getDate() - ((today.getDay() + 6) % 7) + 7)
  const sunday = new Date(monday)
  sunday.setDate(monday.getDate() + 6)
  const form = useForm(
    { userId: '', startsOn: iso(monday), endsOn: iso(sunday), note: '' },
    {
      userId: (v) => (!v ? 'Nöbetçi kişiyi seçin.' : undefined),
      endsOn: (v, all) => (v < all.startsOn ? 'Bitiş, başlangıçtan önce olamaz.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: () => api.onCall.create({ ...form.values, userId: Number(form.values.userId), note: form.values.note || null }),
    onSuccess: () => {
      toast('Nöbet eklendi.')
      qc.invalidateQueries({ queryKey: ['oncall'] })
      qc.invalidateQueries({ queryKey: ['home'] })
      onClose()
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()
  return (
    <SlideOver
      open
      onClose={onClose}
      title="Nöbet ekle"
      description="Varsayılan olarak gelecek hafta (pazartesi–pazar) seçilidir."
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} Nöbeti ekle</button></>}
    >
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-4">
        <Field label="Nöbetçi" htmlFor="f-userId" error={form.errors.userId} required>
          <select {...form.field('userId')} className="form-field">
            <option value="">Seçin…</option>
            {agents.data?.map((a) => <option key={a.id} value={a.id}>{a.fullName}</option>)}
          </select>
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Başlangıç" htmlFor="f-startsOn"><input type="date" {...form.field('startsOn')} className="form-field" /></Field>
          <Field label="Bitiş" htmlFor="f-endsOn" error={form.errors.endsOn}><input type="date" {...form.field('endsOn')} className="form-field" /></Field>
        </div>
        <Field label="Not" htmlFor="f-note" optional><input {...form.field('note')} className="form-field" maxLength={200} placeholder="ör. Mesai dışı acil durumlar için" /></Field>
        <p className="flex items-center gap-2 text-xs text-gray-500"><Megaphone className="size-4" aria-hidden="true" /> Nöbetçi, ana sayfadaki "BT Nöbetçi Personel" kutusunda herkese görünür.</p>
      </form>
    </SlideOver>
  )
}
