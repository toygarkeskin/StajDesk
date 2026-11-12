import clsx from 'clsx'
import { CheckCircle2, Circle, CircleDashed, Clock, XCircle } from 'lucide-react'
import { formatDateTime } from '@/lib/format'
import type { ApprovalDecision, WorkflowInstance } from '@/lib/work'

const decisionText: Record<ApprovalDecision, string> = { Pending: 'Bekliyor', Approved: 'Onayladı', Rejected: 'Reddetti', Skipped: 'Atlandı' }

/** Onay zinciri: her adım, onaylayıcı, karar, açıklama ve zaman. Sıradaki adım vurgulanır. */
export function WorkflowTimeline({ instance, roleName }: { instance: WorkflowInstance; roleName?: (name: string) => string }) {
  return (
    <ol aria-label="Onay adımları" className="relative space-y-4 border-l border-gray-200 pl-5 dark:border-gray-700/60">
      {instance.steps.map((s) => {
        const who = s.approverName ?? (s.approverRole ? `${roleName?.(s.approverRole) ?? s.approverRole} rolündeki herkes` : '—')
        const icon = s.decision === 'Approved' ? <CheckCircle2 className="size-4 text-emerald-500" />
          : s.decision === 'Rejected' ? <XCircle className="size-4 text-rose-500" />
          : s.decision === 'Skipped' ? <CircleDashed className="size-4 text-gray-400" />
          : s.isCurrent ? <Clock className="size-4 text-amber-500" /> : <Circle className="size-4 text-gray-300 dark:text-gray-600" />
        return (
          <li key={s.id} className="relative">
            <span className="absolute top-0.5 -left-[27px] rounded-full bg-white dark:bg-gray-800" aria-hidden="true">{icon}</span>
            <div className={clsx('text-sm font-medium', s.isCurrent ? 'text-amber-700 dark:text-amber-300' : 'text-gray-800 dark:text-gray-100')}>
              {s.stepOrder}. {s.stepName}{s.isCurrent && ' · sırada'}
            </div>
            <div className="text-xs text-gray-500">
              {s.decision === 'Pending' ? `Onaylayıcı: ${who}` : `${s.decidedByName ?? who} · ${decisionText[s.decision]}`}
              {s.decidedAt && ` · ${formatDateTime(s.decidedAt)}`}
            </div>
            {s.comment && <p className="mt-1 rounded-lg bg-gray-50 px-2 py-1 text-xs text-gray-600 dark:bg-gray-900/40 dark:text-gray-300">{s.comment}</p>}
          </li>
        )
      })}
      {instance.steps.length === 0 && <li className="text-sm text-gray-500">Bu talep türü için onay adımı tanımlı değil; talep doğrudan onaylandı.</li>}
    </ol>
  )
}
