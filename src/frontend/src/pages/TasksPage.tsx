import { useDeferredValue, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { CalendarClock, CheckCircle2, Circle, Pencil, Plus, Trash2 } from 'lucide-react'
import { api, errorMessage, fieldErrors } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { taskPriorityLabels, taskStatusClasses, taskStatusLabels, workApi, type WorkTask, type WorkTaskPriority, type WorkTaskStatus } from '@/lib/work'
import { useAuth, useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { Badge, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, PageHeader, Pagination, SlideOver, Spinner, TableSkeleton } from '@/components/ui'

/* =========================================================
   Görev yönetimi: bana atananlar, verdiklerim, (tasks.manage) tümü.
   Atama kuralı sunucuda: kendine, organizasyonda altındakilere veya tasks.manage ile herkese.
   ========================================================= */

const invalidate = (qc: ReturnType<typeof useQueryClient>) => { qc.invalidateQueries({ queryKey: ['tasks'] }) }

export function TasksPage() {
  const { can } = useCan()
  const [params, setParams] = useSearchParams()
  const tabs = [{ id: 'mine', label: 'Bana atananlar' }, { id: 'created', label: 'Verdiğim görevler' }, ...(can('tasks.manage') ? [{ id: 'all', label: 'Tüm görevler' }] : [])]
  const scope = tabs.find((t) => t.id === params.get('scope'))?.id ?? 'mine'
  const [openOnly, setOpenOnly] = useState(true)
  const [page, setPage] = useState(1)
  const [edit, setEdit] = useState<WorkTask | 'new' | null>(null)
  const openId = Number(params.get('task')) || null
  const list = useQuery({ queryKey: ['tasks', scope, openOnly, page], queryFn: () => workApi.tasks({ scope, openOnly: openOnly || undefined, page, pageSize: 20 }), placeholderData: keepPreviousData })

  return (
    <>
      <PageHeader title="Görevler" description="Size atanan işler ve verdiğiniz görevler. Görevi kendinize veya organizasyonda size bağlı çalışanlara atayabilirsiniz."
        actions={<button className="btn-violet" onClick={() => setEdit('new')}><Plus className="size-4" aria-hidden="true" /> Görev oluştur</button>} />
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3 border-b border-gray-200 dark:border-gray-700/60">
        <div role="tablist" className="flex gap-1 overflow-x-auto">
          {tabs.map((t) => (
            <button key={t.id} role="tab" aria-selected={scope === t.id} onClick={() => { setPage(1); setParams(t.id === 'mine' ? {} : { scope: t.id }) }}
              className={clsx('min-h-11 shrink-0 border-b-2 px-3 text-sm font-medium', scope === t.id ? 'border-violet-500 text-violet-600 dark:text-violet-300' : 'border-transparent text-gray-500 hover:text-gray-800 dark:hover:text-gray-200')}>
              {t.label}
            </button>
          ))}
        </div>
        <label className="flex min-h-11 items-center gap-2 text-sm text-gray-600 dark:text-gray-300">
          <input type="checkbox" className="size-4 accent-violet-500" checked={openOnly} onChange={(e) => { setOpenOnly(e.target.checked); setPage(1) }} /> Yalnızca açık görevler
        </label>
      </div>
      <section className="card overflow-hidden">
        {list.isLoading ? <TableSkeleton cols={4} /> : list.isError ? <ErrorState message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data!.items.length === 0 ? (
          <EmptyState title={openOnly ? 'Açık görev yok' : 'Görev yok'} description={scope === 'created' ? 'Verdiğiniz görevler burada listelenir.' : 'Size bir görev atandığında burada görünür ve bildirim gelir.'} />
        ) : (
          <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
            {list.data!.items.map((t) => <TaskRow key={t.id} t={t} onEdit={() => setEdit(t)} onOpen={() => setParams({ ...(scope !== 'mine' ? { scope } : {}), task: String(t.id) })} />)}
          </ul>
        )}
        {list.data && list.data.totalPages > 1 && <Pagination page={list.data.page} totalPages={list.data.totalPages} totalCount={list.data.totalCount} pageSize={list.data.pageSize} onChange={setPage} />}
      </section>
      {edit && <TaskForm task={edit === 'new' ? null : edit} onClose={() => setEdit(null)} />}
      {openId && <TaskDetail id={openId} onClose={() => { params.delete('task'); setParams(params) }} onEdit={(t) => setEdit(t)} />}
    </>
  )
}

function StatusToggle({ t }: { t: WorkTask }) {
  const qc = useQueryClient()
  const done = t.status === 'Done'
  const toggle = useMutation({
    mutationFn: () => workApi.setTaskStatus(t.id, done ? 'Todo' : 'Done'),
    onSuccess: (r) => { toast(r.status === 'Done' ? 'Görev tamamlandı.' : 'Görev yeniden açıldı.'); invalidate(qc) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <button type="button" disabled={!t.canChangeStatus || toggle.isPending} onClick={() => toggle.mutate()} aria-pressed={done}
      aria-label={done ? `${t.title} görevini yeniden aç` : `${t.title} görevini tamamlandı işaretle`}
      className="grid size-11 shrink-0 place-items-center rounded-lg text-gray-400 hover:text-emerald-600 disabled:opacity-50">
      {toggle.isPending ? <Spinner /> : done ? <CheckCircle2 className="size-5 text-emerald-500" aria-hidden="true" /> : <Circle className="size-5" aria-hidden="true" />}
    </button>
  )
}

function TaskRow({ t, onEdit, onOpen }: { t: WorkTask; onEdit: () => void; onOpen: () => void }) {
  const me = useAuth((s) => s.me?.id)
  return (
    <li className="flex items-center gap-2 px-2 py-2 sm:px-3">
      <StatusToggle t={t} />
      <button type="button" onClick={onOpen} className="min-w-0 flex-1 text-left">
        <span className={clsx('block truncate text-sm font-medium', t.status === 'Done' || t.status === 'Cancelled' ? 'text-gray-400 line-through' : 'text-gray-800 dark:text-gray-100')}>{t.title}</span>
        <span className="block truncate text-xs text-gray-500">
          {t.assignee.id === me ? `${t.createdBy.fullName} verdi` : `Atanan: ${t.assignee.fullName}`}
          {t.dueDate && <span className={clsx(t.isOverdue && 'font-medium text-rose-600 dark:text-rose-400')}> · <CalendarClock className="inline size-3" aria-hidden="true" /> {formatDate(t.dueDate)}{t.isOverdue && ' (gecikti)'}</span>}
        </span>
      </button>
      {t.priority === 'High' && <Badge className="bg-rose-500/10 text-rose-700 dark:text-rose-300">Yüksek</Badge>}
      <Badge className={taskStatusClasses[t.status]}>{taskStatusLabels[t.status]}</Badge>
      {t.canEdit && <IconButton label={`${t.title} görevini düzenle`} onClick={onEdit}><Pencil className="size-4" /></IconButton>}
    </li>
  )
}

function TaskDetail({ id, onClose, onEdit }: { id: number; onClose: () => void; onEdit: (t: WorkTask) => void }) {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['tasks', 'detail', id], queryFn: () => workApi.task(id), retry: false })
  const [confirm, setConfirm] = useState(false)
  const setStatus = useMutation({
    mutationFn: (s: WorkTaskStatus) => workApi.setTaskStatus(id, s),
    onSuccess: () => { toast('Görev durumu güncellendi.'); invalidate(qc) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const remove = useMutation({
    mutationFn: () => workApi.removeTask(id),
    onSuccess: () => { toast('Görev silindi.'); invalidate(qc); onClose() },
    onError: (e) => { toast(errorMessage(e), 'error'); setConfirm(false) },
  })
  const t = q.data
  return (
    <SlideOver open onClose={onClose} title={t?.title ?? 'Görev'} description={t ? `${t.createdBy.fullName} → ${t.assignee.fullName}` : undefined}
      footer={<div className="flex flex-wrap justify-end gap-2">
        {t?.canEdit && <button className="btn-secondary text-rose-600" onClick={() => setConfirm(true)}><Trash2 className="size-4" aria-hidden="true" /> Sil</button>}
        {t?.canEdit && <button className="btn-secondary" onClick={() => { onEdit(t); onClose() }}><Pencil className="size-4" aria-hidden="true" /> Düzenle</button>}
        <button className="btn-violet" onClick={onClose}>Kapat</button>
      </div>}>
      {q.isLoading ? <Spinner /> : q.isError || !t ? <ErrorState message={errorMessage(q.error)} /> : (
        <div className="space-y-4">
          <dl className="grid grid-cols-2 gap-3 text-sm">
            <div><dt className="text-xs text-gray-500">Teslim</dt><dd className={clsx('font-medium', t.isOverdue ? 'text-rose-600' : 'text-gray-800 dark:text-gray-100')}>{t.dueDate ? formatDate(t.dueDate) : '—'}{t.isOverdue && ' · gecikti'}</dd></div>
            <div><dt className="text-xs text-gray-500">Öncelik</dt><dd className="font-medium text-gray-800 dark:text-gray-100">{taskPriorityLabels[t.priority]}</dd></div>
            <div><dt className="text-xs text-gray-500">Oluşturma</dt><dd className="font-medium text-gray-800 dark:text-gray-100">{formatDate(t.createdAt)}</dd></div>
            <div><dt className="text-xs text-gray-500">Tamamlanma</dt><dd className="font-medium text-gray-800 dark:text-gray-100">{t.completedAt ? formatDate(t.completedAt) : '—'}</dd></div>
          </dl>
          {t.description && <p className="text-sm whitespace-pre-wrap text-gray-700 dark:text-gray-300">{t.description}</p>}
          {t.canChangeStatus && (
            <Field label="Durum" htmlFor="task-status">
              <select id="task-status" className="form-field" value={t.status} disabled={setStatus.isPending} onChange={(e) => setStatus.mutate(e.target.value as WorkTaskStatus)}>
                {(Object.keys(taskStatusLabels) as WorkTaskStatus[]).map((s) => <option key={s} value={s}>{taskStatusLabels[s]}</option>)}
              </select>
            </Field>
          )}
        </div>
      )}
      <ConfirmDialog open={confirm} title="Görev silinsin mi?" body="Görev kalıcı olarak silinir. Yalnızca iptal etmek için durumu İptal yapın." confirmLabel="Görevi sil"
        pending={remove.isPending} onConfirm={() => remove.mutate()} onCancel={() => setConfirm(false)} />
    </SlideOver>
  )
}

function TaskForm({ task, onClose }: { task: WorkTask | null; onClose: () => void }) {
  const qc = useQueryClient()
  const me = useAuth((s) => s.me)
  const [f, setF] = useState({ title: task?.title ?? '', description: task?.description ?? '', dueDate: task?.dueDate ?? '', priority: (task?.priority ?? 'Normal') as WorkTaskPriority })
  const [assignee, setAssignee] = useState<{ id: number; fullName: string } | null>(task ? task.assignee : me ? { id: me.id, fullName: `${me.fullName} (ben)` } : null)
  const [search, setSearch] = useState('')
  const term = useDeferredValue(search.trim())
  const people = useQuery({ queryKey: ['employees', 'pick', term], queryFn: () => api.employees.list({ search: term, pageSize: 8 }), enabled: term.length >= 2 && !assignee })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useMutation({
    mutationFn: () => workApi.saveTask(task?.id ?? null, { title: f.title, description: f.description || null, dueDate: f.dueDate || null, priority: f.priority, assigneeId: assignee?.id ?? null }),
    onSuccess: () => { toast(task ? 'Görev güncellendi.' : 'Görev oluşturuldu.'); invalidate(qc); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (!f.title.trim()) { setErrors({ title: 'Görev başlığı zorunludur.' }); return }
    save.mutate()
  }
  return (
    <SlideOver open onClose={onClose} title={task ? 'Görevi düzenle' : 'Görev oluştur'} description="Atanan kişiye bildirim gider."
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" form="task-form" disabled={save.isPending}>{save.isPending && <Spinner />}Görevi kaydet</button></div>}>
      <form id="task-form" onSubmit={submit} className="space-y-4" noValidate>
        <Field label="Başlık" htmlFor="tk-title" required error={errors.title ?? errors.Title}>
          <input id="tk-title" className="form-field" maxLength={200} value={f.title} onChange={(e) => setF((p) => ({ ...p, title: e.target.value }))} aria-invalid={!!(errors.title ?? errors.Title)} />
        </Field>
        <Field label="Açıklama" htmlFor="tk-desc" optional>
          <textarea id="tk-desc" className="form-field" rows={4} maxLength={4000} value={f.description} onChange={(e) => setF((p) => ({ ...p, description: e.target.value }))} />
        </Field>
        <Field label="Atanan" htmlFor="tk-assignee" hint="Kendinize, size bağlı çalışanlara veya (yetkiniz varsa) herkese atayabilirsiniz.">
          {assignee ? (
            <div className="flex min-h-11 items-center justify-between rounded-lg border border-gray-200 px-3 dark:border-gray-700/60">
              <span className="text-sm font-medium">{assignee.fullName}</span>
              <button type="button" className="text-xs font-medium text-violet-600 dark:text-violet-400" onClick={() => { setAssignee(null); setSearch('') }}>Değiştir</button>
            </div>
          ) : (
            <div className="relative">
              <input id="tk-assignee" className="form-field" placeholder="Ad veya e-posta" value={search} onChange={(e) => setSearch(e.target.value)} />
              {people.data && (
                <ul className="absolute z-10 mt-1 max-h-56 w-full overflow-y-auto rounded-lg border border-gray-200 bg-white shadow-lg dark:border-gray-700 dark:bg-gray-800">
                  {people.data.items.length === 0 ? <li className="px-3 py-2 text-sm text-gray-500">Eşleşen çalışan yok.</li> : people.data.items.map((p) => (
                    <li key={p.id}><button type="button" className="flex min-h-11 w-full flex-col justify-center px-3 text-left hover:bg-gray-50 dark:hover:bg-gray-700/30" onClick={() => setAssignee({ id: p.id, fullName: p.fullName })}>
                      <span className="text-sm font-medium">{p.fullName}</span><span className="text-xs text-gray-500">{[p.title, p.department?.name].filter(Boolean).join(' · ')}</span>
                    </button></li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Teslim tarihi" htmlFor="tk-due" optional>
            <input id="tk-due" type="date" className="form-field" value={f.dueDate} onChange={(e) => setF((p) => ({ ...p, dueDate: e.target.value }))} />
          </Field>
          <Field label="Öncelik" htmlFor="tk-prio">
            <select id="tk-prio" className="form-field" value={f.priority} onChange={(e) => setF((p) => ({ ...p, priority: e.target.value as WorkTaskPriority }))}>
              {(Object.keys(taskPriorityLabels) as WorkTaskPriority[]).map((k) => <option key={k} value={k}>{taskPriorityLabels[k]}</option>)}
            </select>
          </Field>
        </div>
      </form>
    </SlideOver>
  )
}
