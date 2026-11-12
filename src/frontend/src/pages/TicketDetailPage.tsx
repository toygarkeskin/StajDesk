import { useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { ArrowLeft, CheckCircle2, History, Lock, MonitorSmartphone, Pencil, RotateCcw, Trash2 } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { assetTypeLabels, priorityLabels, statusLabels, typeLabels } from '@/lib/labels'
import { formatDateTime, timeAgo } from '@/lib/format'
import type { TicketDetail, TicketHistory, TicketPriority, TicketStatus, TicketType } from '@/lib/types'
import { useAuth, useRole } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { AttachmentsCard } from '@/components/Attachments'
import { Avatar, BlockSkeleton, CategoryText, ConfirmDialog, ErrorState, Field, PriorityText, SlideOver, Spinner, StatusBadge } from '@/components/ui'

/* Detail / Record kalıbı: ad · iz · durum + üst veri · sekmeler. Düzenleme yan panelde, silme isimli onayla. */

function historyText(h: TicketHistory): ReactNode {
  const s = (v?: string | null) => (v && v in statusLabels ? statusLabels[v as TicketStatus] : v)
  const p = (v?: string | null) => (v && v in priorityLabels ? priorityLabels[v as TicketPriority] : v)
  switch (h.action) {
    case 'Created': return 'talebi oluşturdu'
    case 'StatusChanged': return <>durumu <b className="font-medium">{s(h.oldValue)}</b> → <b className="font-medium">{s(h.newValue)}</b> yaptı</>
    case 'Assigned': return h.newValue ? <>talebi <b className="font-medium">{h.newValue}</b> kişisine atadı</> : 'atamayı kaldırdı'
    case 'PriorityChanged': return <>önceliği <b className="font-medium">{p(h.oldValue)}</b> → <b className="font-medium">{p(h.newValue)}</b> yaptı</>
    case 'AttachmentAdded': return <><b className="font-medium">{h.newValue}</b> dosyasını ekledi</>
    case 'AttachmentRemoved': return <><b className="font-medium">{h.oldValue}</b> dosyasını sildi</>
    default: return 'talep bilgilerini güncelledi'
  }
}

/** Tek değerli satır içi düzenleme: aynı kutuda seçim kutusu, komşular kaymaz. */
function InlineSelect({ id, label, value, disabled, onChange, children }: { id: string; label: string; value: string; disabled?: boolean; onChange: (v: string) => void; children: ReactNode }) {
  return (
    <div className="grid grid-cols-[7rem_1fr] items-center gap-4 py-2">
      <label htmlFor={id} className="text-sm text-gray-500">{label}</label>
      <select id={id} className="form-field" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}>{children}</select>
    </div>
  )
}

function Meta({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid grid-cols-[7rem_1fr] items-center gap-4 py-2 text-sm">
      <dt className="text-gray-500">{label}</dt>
      <dd className="min-h-11 content-center text-right font-medium text-gray-800 dark:text-gray-100">{children}</dd>
    </div>
  )
}

export function TicketDetailPage() {
  const id = Number(useParams().id)
  const navigate = useNavigate()
  const qc = useQueryClient()
  const me = useAuth((s) => s.me)
  const { isStaff } = useRole()
  const [editOpen, setEditOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)

  const ticket = useQuery({ queryKey: ['ticket', id], queryFn: () => api.tickets.get(id), enabled: Number.isFinite(id) })
  const agents = useQuery({ queryKey: ['agents'], queryFn: api.users.agents, enabled: isStaff })

  const onUpdated = (t: TicketDetail, msg: string) => {
    qc.setQueryData(['ticket', id], t)
    qc.invalidateQueries({ queryKey: ['tickets'] })
    qc.invalidateQueries({ queryKey: ['stats'] })
    toast(msg)
  }
  const onError = (err: unknown) => toast(errorMessage(err), 'error')

  const setStatus = useMutation({ mutationFn: (s: TicketStatus) => api.tickets.setStatus(id, s), onSuccess: (t) => onUpdated(t, `Durum "${statusLabels[t.status]}" olarak güncellendi.`), onError })
  const [rejecting, setRejecting] = useState(false)
  const [rejectReason, setRejectReason] = useState('')
  const reject = useMutation({
    mutationFn: () => api.tickets.setStatus(id, 'Rejected', rejectReason.trim()),
    onSuccess: (t) => { onUpdated(t, 'Talep reddedildi; gerekçe talep sahibine iletildi.'); setRejecting(false); setRejectReason('') },
    onError,
  })
  const setPriority = useMutation({ mutationFn: (p: TicketPriority) => api.tickets.setPriority(id, p), onSuccess: (t) => onUpdated(t, 'Öncelik güncellendi.'), onError })
  const assign = useMutation({ mutationFn: (a: number | null) => api.tickets.assign(id, a), onSuccess: (t) => onUpdated(t, t.assignee ? `Talep ${t.assignee.fullName} kişisine atandı.` : 'Atama kaldırıldı.'), onError })
  const remove = useMutation({
    mutationFn: () => api.tickets.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['tickets'] })
      toast(`#${id} numaralı talep silindi.`)
      navigate('/tickets')
    },
    onError,
  })

  const timeline = useMemo(() => {
    const t = ticket.data
    if (!t) return []
    return [
      ...t.comments.map((c) => ({ kind: 'comment' as const, at: c.createdAt, c })),
      ...t.history.map((h) => ({ kind: 'history' as const, at: h.createdAt, h })),
    ].sort((a, b) => a.at.localeCompare(b.at))
  }, [ticket.data])

  if (ticket.isLoading) return <div className="space-y-6"><BlockSkeleton className="h-24" /><BlockSkeleton className="h-96" /></div>
  if (ticket.isError) return <div className="card"><ErrorState message={errorMessage(ticket.error)} details={errorDetails(ticket.error)} onRetry={() => ticket.refetch()} /></div>
  const t = ticket.data!
  const isRequester = me?.id === t.requester.id
  const canEdit = t.permissions.canEdit
  const closed = t.status === 'Closed' || t.status === 'Rejected'

  return (
    <>
      <Link to="/tickets" className="mb-4 inline-flex min-h-11 items-center gap-1 text-sm font-medium text-gray-500 hover:text-violet-600">
        <ArrowLeft className="size-4" aria-hidden="true" /> Taleplere dön
      </Link>

      <div className="mb-6 flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
        <div>
          <div className="mb-2 flex flex-wrap items-center gap-2">
            <span className="text-sm font-semibold text-gray-400">#{t.id}</span>
            <StatusBadge status={t.status} />
            <PriorityText priority={t.priority} />
            <CategoryText name={t.category.name} color={t.category.color} />
          </div>
          <h1 className="text-2xl font-bold md:text-3xl">{t.title}</h1>
          <p className="mt-1 text-sm text-gray-500">{t.requester.fullName} tarafından {formatDateTime(t.createdAt)} tarihinde açıldı</p>
        </div>
        <div className="flex shrink-0 flex-wrap gap-2">
          {canEdit && <button className="btn-secondary" onClick={() => setEditOpen(true)}><Pencil className="size-4" aria-hidden="true" /> Düzenle</button>}
          {t.permissions.canDelete && <button className="btn-secondary text-rose-500" onClick={() => setDeleteOpen(true)}><Trash2 className="size-4" aria-hidden="true" /> Sil</button>}
        </div>
      </div>

      {!isStaff && isRequester && t.status === 'Resolved' && (
        <div role="status" className="card mb-6 flex flex-col gap-3 border border-emerald-200 p-4 md:flex-row md:items-center dark:border-emerald-500/30">
          <CheckCircle2 className="size-6 shrink-0 text-emerald-500" aria-hidden="true" />
          <div className="flex-1 text-sm">
            <div className="font-semibold text-gray-800 dark:text-gray-100">Destek ekibi talebinizi çözüldü olarak işaretledi.</div>
            <div className="text-gray-500">Sorununuz giderildiyse talebi kapatabilir, devam ediyorsa yeniden açabilirsiniz.</div>
          </div>
          <div className="flex gap-2">
            <button className="btn-secondary" aria-busy={setStatus.isPending} onClick={() => setStatus.mutate('InProgress')}><RotateCcw className="size-4" aria-hidden="true" /> Çözülmedi</button>
            <button className="btn-violet" aria-busy={setStatus.isPending} onClick={() => setStatus.mutate('Closed')}>Onayla ve kapat</button>
          </div>
        </div>
      )}

      <div className="grid grid-cols-12 gap-6">
        <div className="col-span-12 space-y-6 md:col-span-8">
          <div className="card p-6">
            <h2 className="mb-3 text-xs font-semibold text-gray-400 uppercase">Açıklama</h2>
            <p className="whitespace-pre-wrap text-gray-700 dark:text-gray-300">{t.description}</p>
          </div>

          <div className="card p-6">
            <h2 className="mb-5 text-xs font-semibold text-gray-400 uppercase">Zaman çizelgesi</h2>
            <ol className="relative space-y-5 before:absolute before:top-2 before:bottom-2 before:left-4 before:w-px before:bg-gray-200 dark:before:bg-gray-700/60">
              {timeline.map((item) =>
                item.kind === 'comment' ? (
                  <li key={`c${item.c.id}`} className="relative flex gap-3">
                    <div className="relative z-10"><Avatar name={item.c.author.fullName} /></div>
                    <div className={clsx('flex-1 rounded-lg border px-4 py-3', item.c.isInternal ? 'border-amber-200 bg-amber-50 dark:border-amber-500/30 dark:bg-amber-500/5' : 'border-gray-200 dark:border-gray-700/60')}>
                      <div className="mb-1 flex flex-wrap items-center gap-2 text-sm">
                        <span className="font-semibold text-gray-800 dark:text-gray-100">{item.c.author.fullName}</span>
                        {item.c.author.isStaff && <span className="rounded bg-violet-500/10 px-1.5 text-xs text-violet-600 dark:text-violet-400">Destek</span>}
                        {item.c.isInternal && <span className="flex items-center gap-1 text-xs text-amber-600"><Lock className="size-3" aria-hidden="true" /> Dahili not</span>}
                        <time className="text-xs text-gray-400" dateTime={item.c.createdAt} title={formatDateTime(item.c.createdAt)}>{timeAgo(item.c.createdAt)}</time>
                      </div>
                      <p className="text-sm whitespace-pre-wrap text-gray-700 dark:text-gray-300">{item.c.body}</p>
                    </div>
                  </li>
                ) : (
                  <li key={`h${item.h.id}`} className="relative flex items-center gap-3 text-sm">
                    <div className="relative z-10 flex size-8 items-center justify-center rounded-full bg-gray-100 text-gray-400 dark:bg-gray-700"><History className="size-4" aria-hidden="true" /></div>
                    <div className="text-gray-500">
                      <span className="font-medium text-gray-700 dark:text-gray-300">{item.h.actor?.fullName ?? 'Sistem'}</span> {historyText(item.h)}
                      <time className="ml-2 text-xs text-gray-400" dateTime={item.h.createdAt}>{timeAgo(item.h.createdAt)}</time>
                    </div>
                  </li>
                ),
              )}
            </ol>
            {closed ? (
              <p className="mt-6 border-t border-gray-100 pt-5 text-sm text-gray-500 dark:border-gray-700/60">Bu talep kapatıldığı veya reddedildiği için yeni yorum eklenemez; yanıt yazmak için önce talebi yeniden açın.</p>
            ) : (
              <CommentForm ticketId={t.id} isStaff={isStaff} />
            )}
          </div>
        </div>

        <aside className="col-span-12 space-y-6 md:col-span-4">
          <div className="card p-5">
            <h2 className="mb-2 text-xs font-semibold text-gray-400 uppercase">Talep bilgileri</h2>
            <div className="divide-y divide-gray-100 dark:divide-gray-700/60">
              {isStaff && !closed ? (
                <>
                  <InlineSelect id="ctl-status" label="Durum" value={t.status} disabled={setStatus.isPending || t.allowedStatuses.length === 0} onChange={(v) => (v === 'Rejected' ? setRejecting(true) : setStatus.mutate(v as TicketStatus))}>
                    <option value={t.status}>{statusLabels[t.status]}</option>
                    {t.allowedStatuses.map((s) => <option key={s} value={s}>{statusLabels[s]}</option>)}
                  </InlineSelect>
                  <InlineSelect id="ctl-priority" label="Öncelik" value={t.priority} disabled={setPriority.isPending} onChange={(v) => setPriority.mutate(v as TicketPriority)}>
                    {Object.entries(priorityLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                  </InlineSelect>
                  <InlineSelect id="ctl-assignee" label="Atanan" value={String(t.assignee?.id ?? '')} disabled={assign.isPending} onChange={(v) => assign.mutate(v ? Number(v) : null)}>
                    <option value="">Atanmamış</option>
                    {agents.data?.map((a) => <option key={a.id} value={a.id}>{a.fullName}</option>)}
                  </InlineSelect>
                  {!t.assignee && me && <div className="py-2"><button className="btn-secondary w-full" onClick={() => assign.mutate(me.id)}>Üzerime al</button></div>}
                </>
              ) : (
                <>
                  <Meta label="Durum"><StatusBadge status={t.status} /></Meta>
                  <Meta label="Öncelik"><PriorityText priority={t.priority} /></Meta>
                  <Meta label="Atanan">{t.assignee ? <span className="inline-flex items-center gap-2"><Avatar name={t.assignee.fullName} size="sm" /> {t.assignee.fullName}</span> : <span className="text-gray-400">Henüz atanmadı</span>}</Meta>
                  {isStaff && closed && <div className="py-2"><button className="btn-secondary w-full" onClick={() => setStatus.mutate('Open')}>Talebi yeniden aç</button></div>}
                </>
              )}
              <Meta label="Tür">{typeLabels[t.type]}</Meta>
              <Meta label="İlgili departman">{t.department?.name ?? <span className="text-gray-400">Belirtilmedi</span>}</Meta>
              <Meta label="Talep sahibi"><span className="inline-flex items-center gap-2"><Avatar name={t.requester.fullName} size="sm" /> {t.requester.fullName}</span></Meta>
              <Meta label="Oluşturma">{formatDateTime(t.createdAt)}</Meta>
              {t.updatedAt && <Meta label="Son güncelleme">{timeAgo(t.updatedAt)}</Meta>}
              {t.resolvedAt && <Meta label="Çözülme">{formatDateTime(t.resolvedAt)}</Meta>}
              {t.closedAt && <Meta label="Kapanma">{formatDateTime(t.closedAt)}</Meta>}
            </div>
          </div>

          {rejecting && (
            <SlideOver open onClose={() => setRejecting(false)} title={`#${t.id} talebini reddet`} description="Gerekçe talep sahibine görünen bir yorum olarak eklenir. Talep daha sonra yeniden açılabilir."
              footer={<><button className="btn-secondary" onClick={() => setRejecting(false)}>Vazgeç</button><button className="btn-danger" disabled={reject.isPending || rejectReason.trim().length < 5} onClick={() => reject.mutate()}>{reject.isPending && <Spinner />}Talebi reddet</button></>}>
              <Field label="Ret gerekçesi" htmlFor="reject-reason" hint="Ör. kapsam dışı, mükerrer talep (#123), kişisel cihaz. En az 5 karakter.">
                <textarea id="reject-reason" className="form-field min-h-32" maxLength={1000} value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} />
              </Field>
            </SlideOver>
          )}
          <AttachmentsCard ticketId={t.id} attachments={t.attachments} canAttach={t.permissions.canAttach} />

          {t.asset && (
            <div className="card p-5">
              <h2 className="mb-3 text-xs font-semibold text-gray-400 uppercase">İlgili cihaz</h2>
              <Link to={`/assets/${t.asset.id}`} className="flex items-center gap-3 rounded-lg border border-gray-200 p-3 hover:border-violet-300 dark:border-gray-700/60">
                <div className="flex size-10 items-center justify-center rounded-lg bg-violet-500/10 text-violet-500"><MonitorSmartphone className="size-5" aria-hidden="true" /></div>
                <div className="text-sm">
                  <div className="font-medium text-gray-800 dark:text-gray-100">{t.asset.name}</div>
                  <div className="text-gray-500">{assetTypeLabels[t.asset.type]} · {t.asset.inventoryNumber}</div>
                </div>
              </Link>
            </div>
          )}
        </aside>
      </div>

      {editOpen && <EditTicketPanel ticket={t} onClose={() => setEditOpen(false)} onSaved={(nt) => onUpdated(nt, 'Talep bilgileri güncellendi.')} />}
      <ConfirmDialog
        open={deleteOpen}
        title={`"#${t.id} ${t.title}" talebi silinsin mi?`}
        body={`Talep ile birlikte ${t.comments.length} yorum ve tüm değişiklik geçmişi kalıcı olarak silinir. Bu işlem geri alınamaz.`}
        confirmLabel="Talebi sil"
        pending={remove.isPending}
        onConfirm={() => remove.mutate()}
        onCancel={() => setDeleteOpen(false)}
      />
    </>
  )
}

function CommentForm({ ticketId, isStaff }: { ticketId: number; isStaff: boolean }) {
  const qc = useQueryClient()
  const [body, setBody] = useState('')
  const [internal, setInternal] = useState(false)
  const [error, setError] = useState<string>()
  const add = useMutation({
    mutationFn: () => api.tickets.comment(ticketId, body.trim(), internal),
    onSuccess: () => {
      setBody('')
      setInternal(false)
      qc.invalidateQueries({ queryKey: ['ticket', ticketId] })
      toast(internal ? 'Dahili not eklendi.' : 'Yanıtınız eklendi.')
    },
    onError: (err) => toast(errorMessage(err), 'error'),
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (add.isPending) return
    if (!body.trim()) {
      setError('Göndermeden önce bir yanıt yazın.')
      document.getElementById('comment-body')?.focus()
      return
    }
    add.mutate()
  }
  return (
    <form onSubmit={submit} noValidate className="mt-6 border-t border-gray-100 pt-5 dark:border-gray-700/60">
      <Field label={isStaff ? 'Yanıt veya dahili not' : 'Yanıtınız'} htmlFor="comment-body" error={error}>
        <textarea
          id="comment-body"
          className="form-field min-h-24"
          value={body}
          maxLength={5000}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'comment-body-error' : undefined}
          onChange={(e) => {
            setBody(e.target.value)
            setError(undefined)
          }}
        />
      </Field>
      <div className="mt-4 flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
        {isStaff ? (
          <label className="flex min-h-11 items-center gap-2 text-sm text-gray-800 dark:text-gray-200">
            <input type="checkbox" className="size-4 accent-amber-500" checked={internal} onChange={(e) => setInternal(e.target.checked)} />
            Dahili not olarak ekle (talep sahibi görmez)
          </label>
        ) : <span />}
        <button type="submit" className="btn-violet" aria-busy={add.isPending}>
          {add.isPending && <Spinner />} {internal ? 'Dahili notu ekle' : 'Yanıtı gönder'}
        </button>
      </div>
    </form>
  )
}

function EditTicketPanel({ ticket, onClose, onSaved }: { ticket: TicketDetail; onClose: () => void; onSaved: (t: TicketDetail) => void }) {
  const categories = useQuery({ queryKey: ['categories'], queryFn: () => api.categories.list() })
  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const form = useForm(
    { title: ticket.title, description: ticket.description, categoryId: String(ticket.category.id), type: ticket.type as string, departmentId: String(ticket.department?.id ?? '') },
    {
      title: (v) => (v.trim().length < 5 ? 'En az 5 karakterlik bir başlık yazın.' : undefined),
      description: (v) => (v.trim().length < 10 ? 'Sorunu en az 10 karakterle açıklayın.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: () => api.tickets.update(ticket.id, { ...form.values, categoryId: Number(form.values.categoryId), type: form.values.type as TicketType, assetId: ticket.asset?.id ?? null, departmentId: form.values.departmentId ? Number(form.values.departmentId) : null }),
    onSuccess: (t) => {
      onSaved(t)
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
      title={`#${ticket.id} talebini düzenle`}
      description="Başlık, açıklama, kategori ve türü güncelleyin."
      footer={
        <>
          <button className="btn-secondary" onClick={onClose}>Vazgeç</button>
          <button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} Değişiklikleri kaydet</button>
        </>
      }
    >
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-4">
        <Field label="Başlık" htmlFor="f-title" error={form.errors.title}>
          <input {...form.field('title')} className="form-field" maxLength={200} />
        </Field>
        <Field label="Açıklama" htmlFor="f-description" error={form.errors.description}>
          <textarea {...form.field('description')} className="form-field min-h-40" maxLength={5000} />
        </Field>
        <Field label="Kategori" htmlFor="f-categoryId">
          <select {...form.field('categoryId')} className="form-field">
            {categories.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </Field>
        <Field label="İlgili departman" htmlFor="f-departmentId" optional>
          <select {...form.field('departmentId')} className="form-field">
            <option value="">Belirtilmedi</option>
            {departments.data?.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        </Field>
        <Field label="Tür" htmlFor="f-type">
          <select {...form.field('type')} className="form-field">
            {Object.entries(typeLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </Field>
      </form>
    </SlideOver>
  )
}
