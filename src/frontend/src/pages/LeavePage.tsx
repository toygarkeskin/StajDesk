import { useDeferredValue, useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { CalendarPlus, Pencil, Plus } from 'lucide-react'
import { api, errorMessage, fieldErrors } from '@/lib/api'
import { formatDate } from '@/lib/format'
import { workApi, workflowStatusClasses, workflowStatusLabels, type LeaveBalance, type LeaveRequest, type LeaveStatus, type LeaveType } from '@/lib/work'
import { useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { Badge, BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, PageHeader, Pagination, SlideOver, Spinner, TableSkeleton } from '@/components/ui'
import { WorkflowTimeline } from '@/components/WorkflowTimeline'

/* =========================================================
   İzin Talep Yönetimi. Çalışan: bakiye, talep, onay durumu. İzin yöneticisi (leave.manage): tüm talepler, türler, haklar.
   ========================================================= */

const d1 = (n: number) => n.toLocaleString('tr-TR', { maximumFractionDigits: 1 })
const statusBadge = (s: LeaveStatus) => <Badge className={workflowStatusClasses[s]}>{workflowStatusLabels[s]}</Badge>
const invalidate = (qc: ReturnType<typeof useQueryClient>) => { for (const k of ['leave', 'approvals', 'approvals-count', 'dashboard']) qc.invalidateQueries({ queryKey: [k] }) }

export function LeavePage() {
  const { can } = useCan()
  const manage = can('leave.manage')
  const [params, setParams] = useSearchParams()
  const tabs = [
    { id: 'mine', label: 'İzinlerim' },
    ...(manage ? [{ id: 'all', label: 'Tüm talepler' }, { id: 'entitlements', label: 'Haklar' }, { id: 'types', label: 'İzin türleri' }] : []),
  ]
  const tab = tabs.find((t) => t.id === params.get('tab'))?.id ?? 'mine'
  const requestId = Number(params.get('request')) || null
  const [creating, setCreating] = useState(false)

  return (
    <>
      <PageHeader title="İzin talepleri" description="İzin talebiniz önce yöneticinize, ardından izin yöneticisine gider. Onay zinciri, Yönetim → Onay zincirleri ekranından değiştirilebilir."
        actions={can('leave.request') && <button className="btn-violet" onClick={() => setCreating(true)}><CalendarPlus className="size-4" aria-hidden="true" /> İzin talebi oluştur</button>} />
      {tabs.length > 1 && (
        <div role="tablist" className="mb-6 flex gap-1 overflow-x-auto border-b border-gray-200 dark:border-gray-700/60">
          {tabs.map((t) => (
            <button key={t.id} role="tab" aria-selected={tab === t.id} onClick={() => setParams(t.id === 'mine' ? {} : { tab: t.id })}
              className={clsx('min-h-11 shrink-0 border-b-2 px-3 text-sm font-medium', tab === t.id ? 'border-violet-500 text-violet-600 dark:text-violet-300' : 'border-transparent text-gray-500 hover:text-gray-800 dark:hover:text-gray-200')}>
              {t.label}
            </button>
          ))}
        </div>
      )}
      {tab === 'mine' && <MyLeave />}
      {tab === 'all' && <AllRequests />}
      {tab === 'entitlements' && <Entitlements />}
      {tab === 'types' && <TypesAdmin />}
      {creating && <NewLeaveForm onClose={() => setCreating(false)} />}
      {requestId && <RequestDetail id={requestId} onClose={() => { params.delete('request'); setParams(params) }} />}
    </>
  )
}

/* ---------------- Bakiye kartları ---------------- */

export function BalanceCards({ balances }: { balances: LeaveBalance[] }) {
  if (balances.length === 0) return <p className="text-sm text-gray-500">Bakiye takibi yapılan izin türü yok.</p>
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {balances.map((b) => {
        const usedPct = b.entitled > 0 ? Math.min(100, ((b.used + b.pending) / b.entitled) * 100) : 0
        return (
          <section key={b.leaveTypeId} className="card p-4" aria-label={`${b.leaveType} bakiyesi`}>
            <div className="text-sm font-semibold text-gray-800 dark:text-gray-100">{b.leaveType}</div>
            <div className="mt-1 text-3xl font-bold text-gray-900 tabular-nums dark:text-gray-50">{d1(b.remaining)}<span className="ml-1 text-sm font-normal text-gray-500">gün kaldı</span></div>
            <div className="mt-3 h-2 overflow-hidden rounded-full bg-gray-100 dark:bg-gray-700/60" aria-hidden="true">
              <div className="h-full rounded-full bg-violet-500" style={{ width: `${usedPct}%` }} />
            </div>
            <dl className="mt-2 flex justify-between text-xs text-gray-500">
              <div>Hak <dd className="inline font-medium text-gray-700 dark:text-gray-300">{d1(b.entitled)}</dd>{b.isCustom && ' (kişisel)'}</div>
              <div>Kullanılan <dd className="inline font-medium text-gray-700 dark:text-gray-300">{d1(b.used)}</dd></div>
              <div>Bekleyen <dd className="inline font-medium text-gray-700 dark:text-gray-300">{d1(b.pending)}</dd></div>
            </dl>
          </section>
        )
      })}
    </div>
  )
}

function RequestTable({ items, showUser, onOpen }: { items: LeaveRequest[]; showUser?: boolean; onOpen: (r: LeaveRequest) => void }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="table-head"><tr>
          {showUser && <th className="px-4 py-3">Çalışan</th>}
          <th className="px-4 py-3">Tür</th><th className="px-4 py-3">Tarih</th><th className="px-4 py-3 text-right">Gün</th><th className="px-4 py-3">Durum</th>
        </tr></thead>
        <tbody>
          {items.map((r) => (
            <tr key={r.id} className="cursor-pointer border-b border-gray-100 last:border-0 hover:bg-gray-50 dark:border-gray-700/60 dark:hover:bg-gray-700/20" onClick={() => onOpen(r)}>
              {showUser && <td className="px-4 py-2"><div className="font-medium text-gray-800 dark:text-gray-100">{r.userName}</div><div className="text-xs text-gray-500">{r.department ?? '—'}</div></td>}
              <td className="px-4 py-2"><button type="button" className="text-left font-medium text-gray-800 dark:text-gray-100" onClick={(e) => { e.stopPropagation(); onOpen(r) }}>{r.leaveType}</button></td>
              <td className="px-4 py-2 whitespace-nowrap">{formatDate(r.startDate)} – {formatDate(r.endDate)}</td>
              <td className="px-4 py-2 text-right tabular-nums">{d1(r.days)}</td>
              <td className="px-4 py-2">{statusBadge(r.status)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function MyLeave() {
  const [, setParams] = useSearchParams()
  const [page, setPage] = useState(1)
  const balances = useQuery({ queryKey: ['leave', 'balances', 'me'], queryFn: () => workApi.balances() })
  const list = useQuery({ queryKey: ['leave', 'mine', page], queryFn: () => workApi.leaveRequests({ page, pageSize: 20 }), placeholderData: keepPreviousData })
  return (
    <div className="space-y-6">
      {balances.isLoading ? <BlockSkeleton className="h-32" /> : balances.isError ? <ErrorState message={errorMessage(balances.error)} onRetry={() => balances.refetch()} /> : <BalanceCards balances={balances.data!} />}
      <section className="card overflow-hidden">
        <h2 className="border-b border-gray-100 px-4 py-3 text-sm font-semibold dark:border-gray-700/60">Taleplerim</h2>
        {list.isLoading ? <TableSkeleton cols={4} /> : list.isError ? <ErrorState message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data!.items.length === 0
          ? <EmptyState title="Henüz izin talebiniz yok" description="İzin talebi oluşturduğunuzda burada onay durumunu takip edebilirsiniz." />
          : <RequestTable items={list.data!.items} onOpen={(r) => setParams({ request: String(r.id) })} />}
        {list.data && list.data.totalPages > 1 && <Pagination page={list.data.page} totalPages={list.data.totalPages} totalCount={list.data.totalCount} pageSize={list.data.pageSize} onChange={setPage} />}
      </section>
    </div>
  )
}

function AllRequests() {
  const [params, setParams] = useSearchParams()
  const [status, setStatus] = useState<LeaveStatus | ''>('Pending')
  const [page, setPage] = useState(1)
  const list = useQuery({ queryKey: ['leave', 'all', status, page], queryFn: () => workApi.leaveRequests({ all: true, status: status || undefined, page, pageSize: 20 }), placeholderData: keepPreviousData })
  return (
    <section className="card overflow-hidden">
      <div className="flex flex-wrap items-center gap-2 border-b border-gray-100 p-3 dark:border-gray-700/60">
        <label htmlFor="leave-status" className="text-sm text-gray-500">Durum</label>
        <select id="leave-status" className="form-field w-auto" value={status} onChange={(e) => { setStatus(e.target.value as LeaveStatus | ''); setPage(1) }}>
          <option value="">Tümü</option>
          {(Object.keys(workflowStatusLabels) as LeaveStatus[]).map((s) => <option key={s} value={s}>{workflowStatusLabels[s]}</option>)}
        </select>
      </div>
      {list.isLoading ? <TableSkeleton cols={5} /> : list.isError ? <ErrorState message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : list.data!.items.length === 0
        ? <EmptyState title="Talep yok" description="Seçilen durumda izin talebi bulunmuyor." />
        : <RequestTable showUser items={list.data!.items} onOpen={(r) => { params.set('request', String(r.id)); setParams(params) }} />}
      {list.data && list.data.totalPages > 1 && <Pagination page={list.data.page} totalPages={list.data.totalPages} totalCount={list.data.totalCount} pageSize={list.data.pageSize} onChange={setPage} />}
    </section>
  )
}

/* ---------------- Yeni talep ---------------- */

function NewLeaveForm({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const types = useQuery({ queryKey: ['leave', 'types'], queryFn: () => workApi.leaveTypes() })
  const balances = useQuery({ queryKey: ['leave', 'balances', 'me'], queryFn: () => workApi.balances() })
  const today = new Date().toISOString().slice(0, 10)
  const [f, setF] = useState({ leaveTypeId: '', startDate: today, endDate: today, reason: '' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const range = useDeferredValue(`${f.startDate}|${f.endDate}`)
  const [start, end] = range.split('|')
  const days = useQuery({ queryKey: ['leave', 'days', start, end], queryFn: () => workApi.countDays(start!, end!), enabled: !!start && !!end && end >= start })
  useEffect(() => { if (!f.leaveTypeId && types.data?.[0]) setF((p) => ({ ...p, leaveTypeId: String(types.data![0]!.id) })) }, [types.data, f.leaveTypeId])
  const type = types.data?.find((t) => t.id === Number(f.leaveTypeId))
  const balance = balances.data?.find((b) => b.leaveTypeId === Number(f.leaveTypeId))
  const save = useMutation({
    mutationFn: () => workApi.createLeave({ leaveTypeId: Number(f.leaveTypeId), startDate: f.startDate, endDate: f.endDate, reason: f.reason.trim() || null }),
    onSuccess: () => { toast('İzin talebiniz onaya gönderildi.'); invalidate(qc); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!f.leaveTypeId) local.leaveTypeId = 'İzin türünü seçin.'
    if (!f.startDate || !f.endDate) local.startDate = 'Tarihleri seçin.'
    else if (f.endDate < f.startDate) local.endDate = 'Bitiş tarihi başlangıçtan önce olamaz.'
    setErrors(local)
    if (!Object.keys(local).length) save.mutate()
  }
  const n = days.data?.days ?? 0
  const over = type?.tracksBalance && balance && n > balance.remaining
  return (
    <SlideOver open onClose={onClose} title="İzin talebi oluştur" description="Hafta sonları ve sabit tarihli resmî tatiller iş gününe sayılmaz."
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" form="leave-form" disabled={save.isPending || !!over}>{save.isPending && <Spinner />}Talebi gönder</button></div>}>
      <form id="leave-form" onSubmit={submit} className="space-y-4" noValidate>
        <Field label="İzin türü" htmlFor="l-type" required error={errors.leaveTypeId ?? errors.LeaveTypeId}>
          <select id="l-type" className="form-field" value={f.leaveTypeId} onChange={(e) => setF((p) => ({ ...p, leaveTypeId: e.target.value }))}>
            {types.data?.map((t) => <option key={t.id} value={t.id}>{t.name}{!t.isPaid ? ' (ücretsiz)' : ''}</option>)}
          </select>
        </Field>
        {type?.description && <p className="-mt-2 text-xs text-gray-500">{type.description}</p>}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Başlangıç" htmlFor="l-start" required error={errors.startDate ?? errors.StartDate}>
            <input id="l-start" type="date" className="form-field" value={f.startDate} onChange={(e) => setF((p) => ({ ...p, startDate: e.target.value, endDate: p.endDate < e.target.value ? e.target.value : p.endDate }))} />
          </Field>
          <Field label="Bitiş" htmlFor="l-end" required error={errors.endDate ?? errors.EndDate}>
            <input id="l-end" type="date" className="form-field" min={f.startDate} value={f.endDate} onChange={(e) => setF((p) => ({ ...p, endDate: e.target.value }))} />
          </Field>
        </div>
        <div className={clsx('rounded-lg p-3 text-sm', over ? 'bg-rose-500/10 text-rose-700 dark:text-rose-300' : 'bg-gray-50 text-gray-700 dark:bg-gray-900/40 dark:text-gray-300')} role="status" aria-live="polite">
          {days.isFetching ? 'Hesaplanıyor…' : <><strong>{d1(n)}</strong> iş günü{days.data?.excludedHolidays.length ? ` (resmî tatil hariç: ${days.data.excludedHolidays.map(formatDate).join(', ')})` : ''}.</>}
          {type?.tracksBalance && balance && <div className="mt-1">{balance.year} için kalan bakiye: <strong>{d1(balance.remaining)}</strong> gün{over && ' — bakiye yetersiz.'}</div>}
        </div>
        <Field label="Açıklama" htmlFor="l-reason" optional error={errors.reason ?? errors.Reason}>
          <textarea id="l-reason" className="form-field" rows={3} maxLength={500} value={f.reason} onChange={(e) => setF((p) => ({ ...p, reason: e.target.value }))} />
        </Field>
      </form>
    </SlideOver>
  )
}

/* ---------------- Talep ayrıntısı ---------------- */

function RequestDetail({ id, onClose }: { id: number; onClose: () => void }) {
  const qc = useQueryClient()
  const me = useQuery({ queryKey: ['leave', 'request', id], queryFn: () => workApi.leaveRequest(id), retry: false })
  const wf = useQuery({ queryKey: ['leave', 'request', id, 'workflow'], queryFn: () => workApi.leaveWorkflow(id), enabled: me.isSuccess })
  const [confirm, setConfirm] = useState(false)
  const cancel = useMutation({
    mutationFn: () => workApi.cancelLeave(id),
    onSuccess: () => { toast('İzin talebi geri çekildi.'); invalidate(qc); setConfirm(false); onClose() },
    onError: (e) => { toast(errorMessage(e), 'error'); setConfirm(false) },
  })
  const r = me.data
  const today = new Date().toISOString().slice(0, 10)
  const canCancel = r && (r.status === 'Pending' || (r.status === 'Approved' && r.startDate > today))
  return (
    <SlideOver open onClose={onClose} title={r ? `${r.leaveType} · ${r.userName}` : 'İzin talebi'} description={r ? `${formatDate(r.startDate)} – ${formatDate(r.endDate)} · ${d1(r.days)} iş günü` : undefined}
      footer={<div className="flex justify-end gap-2">{canCancel && <button className="btn-secondary" onClick={() => setConfirm(true)}>Talebi geri çek</button>}<button className="btn-violet" onClick={onClose}>Kapat</button></div>}>
      {me.isLoading ? <BlockSkeleton className="h-40" /> : me.isError ? <ErrorState message={errorMessage(me.error)} /> : (
        <div className="space-y-5">
          <div className="flex items-center gap-2">{statusBadge(r!.status)}</div>
          {r!.reason && <p className="text-sm text-gray-700 dark:text-gray-300">{r!.reason}</p>}
          <div>
            <h3 className="mb-3 text-sm font-semibold">Onay zinciri</h3>
            {wf.isLoading ? <Spinner /> : wf.data ? <WorkflowTimeline instance={wf.data} /> : <p className="text-sm text-gray-500">Onay geçmişi yok.</p>}
          </div>
        </div>
      )}
      <ConfirmDialog open={confirm} title="İzin talebi geri çekilsin mi?" body="Talep iptal edilir ve onay zincirindeki bekleyen adımlar kapanır. Ayrılan bakiye geri döner."
        confirmLabel="Talebi geri çek" pending={cancel.isPending} onConfirm={() => cancel.mutate()} onCancel={() => setConfirm(false)} />
    </SlideOver>
  )
}

/* ---------------- Haklar (izin yöneticisi) ---------------- */

function Entitlements() {
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const term = useDeferredValue(search.trim())
  const [person, setPerson] = useState<{ id: number; fullName: string } | null>(null)
  const [year, setYear] = useState(new Date().getFullYear())
  const people = useQuery({ queryKey: ['employees', 'pick', term], queryFn: () => api.employees.list({ search: term, pageSize: 8 }), enabled: term.length >= 2 && !person })
  const balances = useQuery({ queryKey: ['leave', 'balances', person?.id, year], queryFn: () => workApi.balances(person!.id, year), enabled: !!person })
  const [edit, setEdit] = useState<LeaveBalance | null>(null)
  const [days, setDays] = useState('')
  const [note, setNote] = useState('')
  const save = useMutation({
    mutationFn: () => workApi.setEntitlement({ userId: person!.id, leaveTypeId: edit!.leaveTypeId, year, days: Number(days), note: note.trim() || null }),
    onSuccess: () => { toast('İzin hakkı kaydedildi.'); qc.invalidateQueries({ queryKey: ['leave'] }); setEdit(null) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <div className="space-y-4">
      <section className="card flex flex-wrap items-end gap-3 p-4">
        <Field label="Çalışan" htmlFor="ent-person">
          {person ? (
            <div className="flex min-h-11 items-center gap-3 rounded-lg border border-gray-200 px-3 dark:border-gray-700/60">
              <span className="text-sm font-medium">{person.fullName}</span>
              <button type="button" className="text-xs font-medium text-violet-600 dark:text-violet-400" onClick={() => { setPerson(null); setSearch('') }}>Değiştir</button>
            </div>
          ) : (
            <div className="relative w-72 max-w-full">
              <input id="ent-person" className="form-field" placeholder="Ad, e-posta veya personel kodu" value={search} onChange={(e) => setSearch(e.target.value)} />
              {people.data && (
                <ul className="absolute z-10 mt-1 max-h-60 w-full overflow-y-auto rounded-lg border border-gray-200 bg-white shadow-lg dark:border-gray-700 dark:bg-gray-800">
                  {people.data.items.length === 0 ? <li className="px-3 py-2 text-sm text-gray-500">Eşleşen çalışan yok.</li> : people.data.items.map((p) => (
                    <li key={p.id}><button type="button" className="flex min-h-11 w-full flex-col justify-center px-3 text-left hover:bg-gray-50 dark:hover:bg-gray-700/30" onClick={() => setPerson({ id: p.id, fullName: p.fullName })}>
                      <span className="text-sm font-medium">{p.fullName}</span><span className="text-xs text-gray-500">{[p.title, p.department?.name].filter(Boolean).join(' · ')}</span>
                    </button></li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </Field>
        <Field label="Yıl" htmlFor="ent-year">
          <select id="ent-year" className="form-field w-28" value={year} onChange={(e) => setYear(Number(e.target.value))}>
            {[0, 1, -1].map((d) => new Date().getFullYear() + d).sort().map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
        </Field>
      </section>
      {!person ? <p className="text-sm text-gray-500">Hakkını görmek veya düzenlemek istediğiniz çalışanı arayın. Kişiye özel hak tanımlanmadıysa izin türünün yıllık varsayılanı geçerlidir.</p>
        : balances.isLoading ? <BlockSkeleton className="h-32" /> : balances.isError ? <ErrorState message={errorMessage(balances.error)} /> : (
          <section className="card overflow-hidden">
            <table className="w-full text-sm">
              <thead className="table-head"><tr><th className="px-4 py-3">İzin türü</th><th className="px-4 py-3 text-right">Hak</th><th className="px-4 py-3 text-right">Kullanılan</th><th className="px-4 py-3 text-right">Bekleyen</th><th className="px-4 py-3 text-right">Kalan</th><th className="px-4 py-3"><span className="sr-only">İşlem</span></th></tr></thead>
              <tbody>
                {balances.data!.map((b) => (
                  <tr key={b.leaveTypeId} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                    <td className="px-4 py-2 font-medium">{b.leaveType}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{d1(b.entitled)}{b.isCustom && <span className="ml-1 text-xs text-violet-600 dark:text-violet-400">kişisel</span>}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{d1(b.used)}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{d1(b.pending)}</td>
                    <td className="px-4 py-2 text-right font-semibold tabular-nums">{d1(b.remaining)}</td>
                    <td className="px-4 py-2 text-right"><IconButton label={`${b.leaveType} hakkını düzenle`} onClick={() => { setEdit(b); setDays(String(b.entitled)); setNote('') }}><Pencil className="size-4" /></IconButton></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        )}
      {edit && person && (
        <SlideOver open onClose={() => setEdit(null)} title={`${edit.leaveType} hakkı · ${person.fullName}`} description={`${year} yılı için kişiye özel hak. Kullanılan: ${d1(edit.used)}, bekleyen: ${d1(edit.pending)} gün.`}
          footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={() => setEdit(null)}>Vazgeç</button><button className="btn-violet" disabled={save.isPending || days === '' || Number(days) < 0} onClick={() => save.mutate()}>{save.isPending && <Spinner />}Hakkı kaydet</button></div>}>
          <div className="space-y-4">
            <Field label="Hak (gün)" htmlFor="ent-days" hint="Kıdem, devreden izin veya denkleştirme gibi durumlar için.">
              <input id="ent-days" type="number" min={0} max={365} step={0.5} className="form-field" value={days} onChange={(e) => setDays(e.target.value)} />
            </Field>
            <Field label="Not" htmlFor="ent-note" optional>
              <input id="ent-note" className="form-field" maxLength={300} value={note} onChange={(e) => setNote(e.target.value)} placeholder="Ör. 5 yıl kıdem" />
            </Field>
          </div>
        </SlideOver>
      )}
    </div>
  )
}

/* ---------------- İzin türleri (izin yöneticisi) ---------------- */

function TypesAdmin() {
  const types = useQuery({ queryKey: ['leave', 'types', 'all'], queryFn: () => workApi.leaveTypes(true) })
  const [edit, setEdit] = useState<LeaveType | 'new' | null>(null)
  return (
    <section className="card overflow-hidden">
      <div className="flex justify-end border-b border-gray-100 p-3 dark:border-gray-700/60"><button className="btn-violet" onClick={() => setEdit('new')}><Plus className="size-4" aria-hidden="true" /> İzin türü ekle</button></div>
      {types.isLoading ? <TableSkeleton cols={4} /> : types.isError ? <ErrorState message={errorMessage(types.error)} onRetry={() => types.refetch()} /> : (
        <table className="w-full text-sm">
          <thead className="table-head"><tr><th className="px-4 py-3">Kod</th><th className="px-4 py-3">Ad</th><th className="px-4 py-3">Bakiye</th><th className="px-4 py-3">Durum</th><th className="px-4 py-3"><span className="sr-only">İşlem</span></th></tr></thead>
          <tbody>
            {types.data!.map((t) => (
              <tr key={t.id} className={clsx('border-b border-gray-100 last:border-0 dark:border-gray-700/60', !t.isActive && 'opacity-60')}>
                <td className="px-4 py-2 font-mono text-xs">{t.code}</td>
                <td className="px-4 py-2"><div className="font-medium">{t.name}</div><div className="text-xs text-gray-500">{t.isPaid ? 'Ücretli' : 'Ücretsiz'}</div></td>
                <td className="px-4 py-2 text-xs">{t.tracksBalance ? `Takip ediliyor · yıllık ${d1(t.defaultDaysPerYear)} gün` : 'Takip edilmiyor'}</td>
                <td className="px-4 py-2">{t.isActive ? 'Aktif' : 'Pasif'}</td>
                <td className="px-4 py-2 text-right"><IconButton label={`${t.name} türünü düzenle`} onClick={() => setEdit(t)}><Pencil className="size-4" /></IconButton></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {edit && <TypeForm type={edit === 'new' ? null : edit} onClose={() => setEdit(null)} />}
    </section>
  )
}

function TypeForm({ type, onClose }: { type: LeaveType | null; onClose: () => void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({
    code: type?.code ?? '', name: type?.name ?? '', description: type?.description ?? '', tracksBalance: type?.tracksBalance ?? false,
    defaultDaysPerYear: String(type?.defaultDaysPerYear ?? 0), isPaid: type?.isPaid ?? true, isActive: type?.isActive ?? true, sortOrder: String(type?.sortOrder ?? 10),
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useMutation({
    mutationFn: () => workApi.saveLeaveType(type?.id ?? null, { ...f, description: f.description || null, defaultDaysPerYear: Number(f.defaultDaysPerYear) || 0, sortOrder: Number(f.sortOrder) || 0 }),
    onSuccess: () => { toast('İzin türü kaydedildi.'); qc.invalidateQueries({ queryKey: ['leave'] }); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const e = (k: string) => errors[k] ?? errors[k[0]!.toUpperCase() + k.slice(1)]
  return (
    <SlideOver open onClose={onClose} title={type ? 'İzin türünü düzenle' : 'İzin türü ekle'}
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />}Türü kaydet</button></div>}>
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Kod" htmlFor="t-code" required error={e('code')}><input id="t-code" className="form-field font-mono" maxLength={20} value={f.code} onChange={(x) => setF((p) => ({ ...p, code: x.target.value }))} /></Field>
          <div className="sm:col-span-2"><Field label="Ad" htmlFor="t-name" required error={e('name')}><input id="t-name" className="form-field" maxLength={100} value={f.name} onChange={(x) => setF((p) => ({ ...p, name: x.target.value }))} /></Field></div>
        </div>
        <Field label="Açıklama" htmlFor="t-desc" optional><textarea id="t-desc" className="form-field" rows={2} maxLength={500} value={f.description} onChange={(x) => setF((p) => ({ ...p, description: x.target.value }))} /></Field>
        <label className="flex min-h-11 items-center gap-3 text-sm"><input type="checkbox" className="size-4 accent-violet-500" checked={f.tracksBalance} onChange={(x) => setF((p) => ({ ...p, tracksBalance: x.target.checked }))} /> Bakiye takibi yap</label>
        {f.tracksBalance && <Field label="Yıllık varsayılan hak (gün)" htmlFor="t-days" error={e('defaultDaysPerYear')}><input id="t-days" type="number" min={0} max={365} step={0.5} className="form-field" value={f.defaultDaysPerYear} onChange={(x) => setF((p) => ({ ...p, defaultDaysPerYear: x.target.value }))} /></Field>}
        <label className="flex min-h-11 items-center gap-3 text-sm"><input type="checkbox" className="size-4 accent-violet-500" checked={f.isPaid} onChange={(x) => setF((p) => ({ ...p, isPaid: x.target.checked }))} /> Ücretli izin</label>
        <label className="flex min-h-11 items-center gap-3 text-sm"><input type="checkbox" className="size-4 accent-violet-500" checked={f.isActive} onChange={(x) => setF((p) => ({ ...p, isActive: x.target.checked }))} /> Aktif (talep formunda görünür)</label>
        <Field label="Sıra" htmlFor="t-order"><input id="t-order" type="number" className="form-field" value={f.sortOrder} onChange={(x) => setF((p) => ({ ...p, sortOrder: x.target.value }))} /></Field>
      </div>
    </SlideOver>
  )
}
