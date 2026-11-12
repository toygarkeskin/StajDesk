import { useDeferredValue, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api, errorMessage, fieldErrors } from '@/lib/api'
import { portalApi, type CompanyEvent, type Recognition, type RecognitionType } from '@/lib/portal'
import { formatDate } from '@/lib/format'
import { toast } from '@/stores/ui'
import { Badge, BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, SlideOver, Spinner } from '@/components/ui'
import { PersonAvatar } from '@/components/AuthImage'

/* Portal içerikleri: şirket etkinlikleri (content.manage) ve çalışan takdirleri (content.manage veya İK). */

const err = (e: Record<string, string>, key: string) => e[key] ?? e[key[0].toUpperCase() + key.slice(1)]
const toLocalInput = (iso?: string | null) => {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}
const fromLocalInput = (v: string) => (v ? new Date(v).toISOString() : null)
const when = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' })

function Shell({ action, loading, error, onRetry, empty, children }: { action: React.ReactNode; loading: boolean; error: unknown; onRetry: () => void; empty: React.ReactNode | false; children: React.ReactNode }) {
  return (
    <section className="card overflow-hidden">
      <div className="flex justify-end border-b border-gray-100 p-3 dark:border-gray-700/60">{action}</div>
      {loading ? <div className="p-4"><BlockSkeleton className="h-40" /></div> : error ? <ErrorState message={errorMessage(error)} onRetry={onRetry} /> : empty || children}
    </section>
  )
}

/* ---------------- Etkinlikler ---------------- */

export function EventsAdmin() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['content', 'events'], queryFn: portalApi.allEvents })
  const [editing, setEditing] = useState<CompanyEvent | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<CompanyEvent | null>(null)
  const remove = useMutation({
    mutationFn: (e: CompanyEvent) => portalApi.removeEvent(e.id),
    onSuccess: () => { toast('Etkinlik silindi.'); setToDelete(null); qc.invalidateQueries({ queryKey: ['content', 'events'] }); qc.invalidateQueries({ queryKey: ['portal', 'events'] }) },
    onError: (e) => { toast(errorMessage(e), 'error'); setToDelete(null) },
  })
  const rows = list.data ?? []
  const now = Date.now()
  return (
    <>
      <Shell action={<button className="btn-violet" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Etkinlik ekle</button>}
        loading={list.isLoading} error={list.error} onRetry={() => list.refetch()}
        empty={rows.length === 0 && <EmptyState title="Henüz etkinlik yok" description="Eğitim, toplantı veya kutlamaları ekleyin; portal panelinde yaklaşanlar görünür." />}>
        <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {rows.map((e) => {
            const past = new Date(e.endsAt ?? e.startsAt).getTime() < now
            return (
              <li key={e.id} className="flex items-center gap-3 px-4 py-3">
                <div className="min-w-0 flex-1">
                  <div className="font-medium text-gray-800 dark:text-gray-100">{e.title}</div>
                  <div className="text-xs text-gray-500">{when.format(new Date(e.startsAt))}{e.location && ` · ${e.location}`}</div>
                </div>
                <Badge className={!e.isPublished ? 'bg-gray-500/15 text-gray-600 dark:text-gray-400' : past ? 'bg-gray-500/15 text-gray-600 dark:text-gray-400' : 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400'}>
                  {!e.isPublished ? 'Taslak' : past ? 'Geçti' : 'Yayında'}
                </Badge>
                <IconButton label={`${e.title} etkinliğini düzenle`} onClick={() => setEditing(e)}><Pencil className="size-4" /></IconButton>
                <IconButton label={`${e.title} etkinliğini sil`} tone="danger" onClick={() => setToDelete(e)}><Trash2 className="size-4" /></IconButton>
              </li>
            )
          })}
        </ul>
      </Shell>
      {editing && <EventForm event={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog open={!!toDelete} title={`"${toDelete?.title}" silinsin mi?`} body="Etkinlik kalıcı olarak silinir. Yalnızca gizlemek için taslağa alın."
        confirmLabel="Etkinliği sil" pending={remove.isPending} onConfirm={() => toDelete && remove.mutate(toDelete)} onCancel={() => setToDelete(null)} />
    </>
  )
}

function EventForm({ event, onClose }: { event: CompanyEvent | null; onClose: () => void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({
    title: event?.title ?? '', description: event?.description ?? '', location: event?.location ?? '',
    startsAt: toLocalInput(event?.startsAt), endsAt: toLocalInput(event?.endsAt), isPublished: event?.isPublished ?? true,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useMutation({
    mutationFn: () => portalApi.saveEvent(event?.id ?? null, { ...f, description: f.description || null, location: f.location || null, startsAt: fromLocalInput(f.startsAt), endsAt: fromLocalInput(f.endsAt) }),
    onSuccess: () => { toast(event ? 'Etkinlik güncellendi.' : 'Etkinlik eklendi.'); qc.invalidateQueries({ queryKey: ['content', 'events'] }); qc.invalidateQueries({ queryKey: ['portal', 'events'] }); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!f.title.trim()) local.title = 'Etkinlik adı zorunludur.'
    if (!f.startsAt) local.startsAt = 'Başlangıç zamanını seçin.'
    if (f.endsAt && f.startsAt && new Date(f.endsAt) < new Date(f.startsAt)) local.endsAt = 'Bitiş, başlangıçtan önce olamaz.'
    setErrors(local)
    if (!Object.keys(local).length) save.mutate()
  }
  return (
    <SlideOver open onClose={onClose} title={event ? 'Etkinliği düzenle' : 'Etkinlik ekle'} description="Portal panelindeki Şirket etkinlikleri kutucuğunda görünür."
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" form="event-form" disabled={save.isPending}>{save.isPending && <Spinner />}Etkinliği kaydet</button></div>}>
      <form id="event-form" onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Etkinlik adı" htmlFor="e-title" required error={err(errors, 'title')}>
          <input id="e-title" className="form-field" maxLength={150} value={f.title} onChange={(e) => setF((p) => ({ ...p, title: e.target.value }))} aria-invalid={!!err(errors, 'title')} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Başlangıç" htmlFor="e-start" required error={err(errors, 'startsAt')}>
            <input id="e-start" type="datetime-local" className="form-field" value={f.startsAt} onChange={(e) => setF((p) => ({ ...p, startsAt: e.target.value }))} />
          </Field>
          <Field label="Bitiş" htmlFor="e-end" optional error={err(errors, 'endsAt')}>
            <input id="e-end" type="datetime-local" className="form-field" value={f.endsAt} onChange={(e) => setF((p) => ({ ...p, endsAt: e.target.value }))} />
          </Field>
        </div>
        <Field label="Yer" htmlFor="e-loc" optional>
          <input id="e-loc" className="form-field" maxLength={150} value={f.location} onChange={(e) => setF((p) => ({ ...p, location: e.target.value }))} />
        </Field>
        <Field label="Açıklama" htmlFor="e-desc" optional error={err(errors, 'description')}>
          <textarea id="e-desc" className="form-field" rows={4} maxLength={2000} value={f.description} onChange={(e) => setF((p) => ({ ...p, description: e.target.value }))} />
        </Field>
        <label className="flex min-h-11 cursor-pointer items-center gap-3 text-sm font-medium text-gray-800 dark:text-gray-200">
          <input type="checkbox" className="size-4 accent-violet-500" checked={f.isPublished} onChange={(e) => setF((p) => ({ ...p, isPublished: e.target.checked }))} /> Yayımla
        </label>
      </form>
    </SlideOver>
  )
}

/* ---------------- Takdirler ---------------- */

const typeLabels: Record<RecognitionType, string> = { EmployeeOfWeek: 'Haftanın çalışanı', Achievement: 'Çalışan başarısı' }

export function RecognitionsAdmin() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['content', 'recognitions'], queryFn: portalApi.allRecognitions })
  const [editing, setEditing] = useState<Recognition | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<Recognition | null>(null)
  const remove = useMutation({
    mutationFn: (r: Recognition) => portalApi.removeRecognition(r.id),
    onSuccess: () => { toast('Takdir silindi.'); setToDelete(null); qc.invalidateQueries({ queryKey: ['content', 'recognitions'] }); qc.invalidateQueries({ queryKey: ['portal', 'recognitions'] }) },
    onError: (e) => { toast(errorMessage(e), 'error'); setToDelete(null) },
  })
  const rows = list.data ?? []
  return (
    <>
      <Shell action={<button className="btn-violet" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Takdir ekle</button>}
        loading={list.isLoading} error={list.error} onRetry={() => list.refetch()}
        empty={rows.length === 0 && <EmptyState title="Henüz takdir yok" description="Haftanın çalışanını veya bir çalışan başarısını paylaşın; yalnızca giriş yapmış çalışanlar görür." />}>
        <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {rows.map((r) => (
            <li key={r.id} className="flex items-center gap-3 px-4 py-3">
              <PersonAvatar id={r.person.id} name={r.person.fullName} photoVersion={r.person.hasPhoto ? r.person.photoVersion : null} size="sm" />
              <div className="min-w-0 flex-1">
                <div className="font-medium text-gray-800 dark:text-gray-100">{r.person.fullName} · {r.title}</div>
                <div className="text-xs text-gray-500">{typeLabels[r.type]} · {formatDate(r.date)}</div>
              </div>
              <IconButton label={`${r.person.fullName} takdirini düzenle`} onClick={() => setEditing(r)}><Pencil className="size-4" /></IconButton>
              <IconButton label={`${r.person.fullName} takdirini sil`} tone="danger" onClick={() => setToDelete(r)}><Trash2 className="size-4" /></IconButton>
            </li>
          ))}
        </ul>
      </Shell>
      {editing && <RecognitionForm item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog open={!!toDelete} title="Takdir silinsin mi?" body="Kayıt kalıcı olarak silinir ve panelden kalkar."
        confirmLabel="Takdiri sil" pending={remove.isPending} onConfirm={() => toDelete && remove.mutate(toDelete)} onCancel={() => setToDelete(null)} />
    </>
  )
}

function RecognitionForm({ item, onClose }: { item: Recognition | null; onClose: () => void }) {
  const qc = useQueryClient()
  const [person, setPerson] = useState(item ? { id: item.person.id, fullName: item.person.fullName } : null)
  const [search, setSearch] = useState('')
  const term = useDeferredValue(search.trim())
  const people = useQuery({ queryKey: ['employees', 'pick', term], queryFn: () => api.employees.list({ search: term, pageSize: 8 }), enabled: term.length >= 2 })
  const [f, setF] = useState({ type: item?.type ?? 'EmployeeOfWeek' as RecognitionType, title: item?.title ?? 'Haftanın çalışanı', description: item?.description ?? '', date: item?.date ?? new Date().toISOString().slice(0, 10) })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useMutation({
    mutationFn: () => portalApi.saveRecognition(item?.id ?? null, { userId: person!.id, ...f, description: f.description || null }),
    onSuccess: () => { toast(item ? 'Takdir güncellendi.' : 'Takdir eklendi.'); qc.invalidateQueries({ queryKey: ['content', 'recognitions'] }); qc.invalidateQueries({ queryKey: ['portal', 'recognitions'] }); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!person) local.userId = 'Çalışan seçin.'
    if (!f.title.trim()) local.title = 'Başlık zorunludur.'
    setErrors(local)
    if (!Object.keys(local).length) save.mutate()
  }
  return (
    <SlideOver open onClose={onClose} title={item ? 'Takdiri düzenle' : 'Takdir ekle'} description="Portal panelindeki Takdirler kutucuğunda görünür (yalnızca çalışanlara)."
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" form="rec-form" disabled={save.isPending}>{save.isPending && <Spinner />}Takdiri kaydet</button></div>}>
      <form id="rec-form" onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Çalışan" htmlFor="r-person" required error={err(errors, 'userId')}>
          {person ? (
            <div className="flex min-h-11 items-center justify-between rounded-lg border border-gray-200 px-3 dark:border-gray-700/60">
              <span className="text-sm font-medium text-gray-800 dark:text-gray-100">{person.fullName}</span>
              <button type="button" className="text-xs font-medium text-violet-600 dark:text-violet-400" onClick={() => setPerson(null)}>Değiştir</button>
            </div>
          ) : (
            <>
              <input id="r-person" className="form-field" placeholder="Ad, e-posta veya personel kodu" value={search} onChange={(e) => setSearch(e.target.value)} aria-invalid={!!err(errors, 'userId')} />
              {people.data && (
                <ul className="mt-1 max-h-48 overflow-y-auto rounded-lg border border-gray-200 dark:border-gray-700/60">
                  {people.data.items.length === 0 ? <li className="px-3 py-2 text-sm text-gray-500">Eşleşen çalışan yok.</li> : people.data.items.map((p) => (
                    <li key={p.id}><button type="button" className="flex min-h-11 w-full flex-col justify-center px-3 text-left hover:bg-gray-50 dark:hover:bg-gray-700/30" onClick={() => setPerson({ id: p.id, fullName: p.fullName })}>
                      <span className="text-sm font-medium text-gray-800 dark:text-gray-100">{p.fullName}</span>
                      <span className="text-xs text-gray-500">{[p.title, p.department?.name].filter(Boolean).join(' · ')}</span>
                    </button></li>
                  ))}
                </ul>
              )}
            </>
          )}
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Tür" htmlFor="r-type">
            <select id="r-type" className="form-field" value={f.type} onChange={(e) => setF((p) => ({ ...p, type: e.target.value as RecognitionType }))}>
              {Object.entries(typeLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </Field>
          <Field label="Tarih" htmlFor="r-date">
            <input id="r-date" type="date" className="form-field" value={f.date} onChange={(e) => setF((p) => ({ ...p, date: e.target.value }))} />
          </Field>
        </div>
        <Field label="Başlık" htmlFor="r-title" required error={err(errors, 'title')}>
          <input id="r-title" className="form-field" maxLength={150} value={f.title} onChange={(e) => setF((p) => ({ ...p, title: e.target.value }))} />
        </Field>
        <Field label="Açıklama" htmlFor="r-desc" optional>
          <textarea id="r-desc" className="form-field" rows={3} maxLength={1000} value={f.description} onChange={(e) => setF((p) => ({ ...p, description: e.target.value }))} />
        </Field>
      </form>
    </SlideOver>
  )
}
