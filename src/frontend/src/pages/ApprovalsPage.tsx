import { useState } from 'react'
import { Link } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, ExternalLink, XCircle } from 'lucide-react'
import { errorMessage } from '@/lib/api'
import { timeAgo } from '@/lib/format'
import { workApi, type ApprovalInboxItem } from '@/lib/work'
import { toast } from '@/stores/ui'
import { EmptyState, ErrorState, Field, PageHeader, Pagination, SlideOver, Spinner, TableSkeleton } from '@/components/ui'
import { WorkflowTimeline } from '@/components/WorkflowTimeline'

/* =========================================================
   Onay kutum: kararımı bekleyen adımlar (izin ve diğer iş akışları).
   Kimin hangi adımı onaylayabileceği sunucuda denetlenir; burada yalnızca bana düşenler listelenir.
   ========================================================= */

export function ApprovalsPage() {
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<ApprovalInboxItem | null>(null)
  const q = useQuery({ queryKey: ['approvals', page], queryFn: () => workApi.inbox(page), placeholderData: keepPreviousData, refetchInterval: 60_000 })
  const items = q.data?.items ?? []
  return (
    <>
      <PageHeader title="Onay kutum" description="Kararınızı bekleyen talepler. Onayladığınız talep zincirdeki bir sonraki adıma geçer; reddederken gerekçe yazmanız gerekir." />
      <section className="card overflow-hidden">
        {q.isLoading ? <TableSkeleton cols={3} /> : q.isError ? <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : items.length === 0 ? (
          <EmptyState title="Onay bekleyen talep yok" description="Size bir onay düştüğünde burada listelenir ve bildirim gelir." />
        ) : (
          <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
            {items.map((a) => (
              <li key={a.approvalId}>
                <button type="button" onClick={() => setOpen(a)} className="flex w-full flex-wrap items-center gap-3 px-4 py-3 text-left hover:bg-gray-50 dark:hover:bg-gray-700/20">
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm font-medium text-gray-800 dark:text-gray-100">{a.workflowName} · {a.requesterName}</span>
                    <span className="block truncate text-xs text-gray-500">{a.summary ?? a.title}</span>
                    <span className="block text-xs text-gray-400">{a.stepName} · {a.requesterDepartment ?? 'Departman yok'} · {timeAgo(a.createdAt)}</span>
                  </span>
                  <span className="rounded-full bg-amber-500/10 px-2 py-0.5 text-xs font-medium text-amber-700 dark:text-amber-300">Kararınız bekleniyor</span>
                </button>
              </li>
            ))}
          </ul>
        )}
        {q.data && q.data.totalPages > 1 && <Pagination page={q.data.page} totalPages={q.data.totalPages} totalCount={q.data.totalCount} pageSize={q.data.pageSize} onChange={setPage} />}
      </section>
      {open && <DecisionPanel item={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function DecisionPanel({ item, onClose }: { item: ApprovalInboxItem; onClose: () => void }) {
  const qc = useQueryClient()
  const detail = useQuery({ queryKey: ['workflow-instance', item.instanceId], queryFn: () => workApi.instance(item.instanceId) })
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)
  const done = (msg: string) => {
    toast(msg)
    for (const k of ['approvals', 'approvals-count', 'leave', 'dashboard']) qc.invalidateQueries({ queryKey: [k] })
    onClose()
  }
  const approve = useMutation({ mutationFn: () => workApi.approve(item.approvalId, comment.trim() || null), onSuccess: () => done('Talep onaylandı.'), onError: (e) => toast(errorMessage(e), 'error') })
  const reject = useMutation({ mutationFn: () => workApi.reject(item.approvalId, comment.trim()), onSuccess: () => done('Talep reddedildi; talep sahibine bildirildi.'), onError: (e) => toast(errorMessage(e), 'error') })
  const busy = approve.isPending || reject.isPending
  return (
    <SlideOver open onClose={onClose} title={item.workflowName} description={item.summary ?? item.title}
      footer={
        <div className="flex flex-wrap justify-end gap-2">
          <button className="btn-secondary" onClick={onClose} disabled={busy}>Vazgeç</button>
          <button className="btn-danger" disabled={busy} onClick={() => { if (!comment.trim()) { setError('Reddetmek için gerekçe yazın; talep sahibi bu açıklamayı görür.'); return } reject.mutate() }}>
            {reject.isPending ? <Spinner /> : <XCircle className="size-4" aria-hidden="true" />}Reddet
          </button>
          <button className="btn-violet" disabled={busy} onClick={() => approve.mutate()}>{approve.isPending ? <Spinner /> : <CheckCircle2 className="size-4" aria-hidden="true" />}Onayla</button>
        </div>
      }>
      <div className="space-y-5">
        <dl className="grid grid-cols-2 gap-3 text-sm">
          <div><dt className="text-xs text-gray-500">Talep sahibi</dt><dd className="font-medium text-gray-800 dark:text-gray-100">{item.requesterName}</dd></div>
          <div><dt className="text-xs text-gray-500">Departman</dt><dd className="font-medium text-gray-800 dark:text-gray-100">{item.requesterDepartment ?? '—'}</dd></div>
          <div className="col-span-2"><dt className="text-xs text-gray-500">Sizin adımınız</dt><dd className="font-medium text-gray-800 dark:text-gray-100">{item.stepName}</dd></div>
        </dl>
        {item.link && <Link to={item.link} onClick={onClose} className="inline-flex min-h-11 items-center gap-1 text-sm font-medium text-violet-600 dark:text-violet-400">Talebin ayrıntısını aç <ExternalLink className="size-4" aria-hidden="true" /></Link>}
        <div>
          <h3 className="mb-3 text-sm font-semibold">Onay zinciri</h3>
          {detail.isLoading ? <Spinner /> : detail.data ? <WorkflowTimeline instance={detail.data} /> : <p className="text-sm text-gray-500">Geçmiş alınamadı.</p>}
        </div>
        <Field label="Açıklama" htmlFor="decision-comment" hint="Onayda isteğe bağlı, retde zorunlu. Talep sahibi görür." error={error ?? undefined}>
          <textarea id="decision-comment" className="form-field" rows={3} maxLength={1000} value={comment} aria-invalid={!!error}
            onChange={(e) => { setComment(e.target.value); if (error) setError(null) }} />
        </Field>
      </div>
    </SlideOver>
  )
}
