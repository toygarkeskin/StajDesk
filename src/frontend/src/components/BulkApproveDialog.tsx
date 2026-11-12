import { useId, useRef, useState } from 'react'
import { CheckCircle2, XCircle } from 'lucide-react'
import { api, errorMessage } from '@/lib/api'
import { useFocusTrap } from '@/lib/hooks'
import type { BulkApproveResult } from '@/lib/types'
import { Spinner } from './ui'

/** Sunucu tek istekte en fazla 100 talep kabul eder; büyük seçimler 100'erli parçalarla gönderilir. */
const CHUNK = 100

export interface BulkTarget { id: number; fullName: string; type: 'NewAccount' | 'Deactivation' }

/**
 * Toplu onay penceresi. Seçilen kişilerin adlarını ve toplam sayıyı gösterir; kullanıcı "Onayla" demeden hiçbir istek gitmez.
 * Gönderim parça parça yapılır ve her parçadan sonra liste yenilenir (onaylananlar bekleyenlerden anında düşer).
 */
export function BulkApproveDialog({ targets, onClose, onProgress, onDone }: {
  targets: BulkTarget[]
  onClose: () => void
  /** Her parça bittiğinde: onaylanan kimlikler (liste yenilemesi için). */
  onProgress: (approvedIds: number[]) => void
  onDone: (result: BulkApproveResult) => void
}) {
  const titleId = useId()
  const cancelRef = useRef<HTMLButtonElement>(null)
  const [running, setRunning] = useState(false)
  const [processed, setProcessed] = useState(0)
  const [result, setResult] = useState<BulkApproveResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const ref = useFocusTrap<HTMLDivElement>(true, () => { if (!running) onClose() }, cancelRef)

  const newCount = targets.filter((t) => t.type === 'NewAccount').length
  const closeCount = targets.length - newCount

  const run = async () => {
    setRunning(true)
    setError(null)
    const total: BulkApproveResult = { requested: targets.length, approved: [], failed: [] }
    try {
      for (let i = 0; i < targets.length; i += CHUNK) {
        const ids = targets.slice(i, i + CHUNK).map((t) => t.id)
        const r = await api.accountRequests.bulkApprove(ids)
        total.approved.push(...r.approved)
        total.failed.push(...r.failed)
        setProcessed(Math.min(i + CHUNK, targets.length))
        onProgress(r.approved)
      }
    } catch (e) {
      // Ağ kesilirse o ana kadar onaylananlar kalır; kalanlar gönderilmemiştir.
      setError(`${errorMessage(e)} ${total.approved.length} talep onaylandı; kalan talepler gönderilmedi.`)
    } finally {
      setRunning(false)
      setResult(total)
      onDone(total)
    }
  }

  const percent = targets.length ? Math.round((processed / targets.length) * 100) : 0

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 animate-fade-in bg-gray-900/50" onClick={() => !running && onClose()} aria-hidden="true" />
      <div ref={ref} role="alertdialog" aria-modal="true" aria-labelledby={titleId} className="card relative flex max-h-[85dvh] w-full max-w-lg animate-fade-in flex-col p-6">
        {!result ? (
          <>
            <h2 id={titleId} className="text-lg font-semibold">Seçilen {targets.length} talep onaylansın mı?</h2>
            <p className="mt-1 text-sm text-gray-500">
              {newCount > 0 && <>{newCount} yeni hesap açılır, her çalışana ilk şifre bağlantısı e-postayla gider ve varsayılan <strong>Kullanıcı</strong> rolü verilir. </>}
              {closeCount > 0 && <>{closeCount} çalışanın hesabı kapatılır. </>}
              Kullanıcı adı ve ek roller sonradan Kullanıcı hesapları ekranından verilebilir.
            </p>
            <ul aria-label="Onaylanacak çalışanlar" className="mt-4 min-h-0 flex-1 divide-y divide-gray-100 overflow-y-auto rounded-lg border border-gray-200 text-sm dark:divide-gray-700/60 dark:border-gray-700/60">
              {targets.map((t) => (
                <li key={t.id} className="flex items-center justify-between gap-2 px-3 py-1.5">
                  <span className="truncate text-gray-800 dark:text-gray-100">{t.fullName}</span>
                  <span className={t.type === 'NewAccount' ? 'shrink-0 text-xs text-violet-600 dark:text-violet-300' : 'shrink-0 text-xs text-rose-600 dark:text-rose-400'}>
                    {t.type === 'NewAccount' ? 'Yeni hesap' : 'Hesap kapatma'}
                  </span>
                </li>
              ))}
            </ul>
            {running && (
              <div className="mt-4" role="status" aria-live="polite">
                <div className="mb-1 flex justify-between text-xs text-gray-500"><span>Onaylanıyor…</span><span className="tabular-nums">{processed} / {targets.length}</span></div>
                <div className="h-2 overflow-hidden rounded-full bg-gray-100 dark:bg-gray-700/60">
                  <div className="h-full rounded-full bg-violet-500 transition-[width]" style={{ width: `${percent}%` }} />
                </div>
              </div>
            )}
            <div className="mt-5 flex flex-wrap justify-end gap-2">
              <button ref={cancelRef} className="btn-secondary" disabled={running} onClick={onClose}>Vazgeç</button>
              <button className="btn-violet" disabled={running} onClick={run}>
                {running && <Spinner />}{targets.length} talebi onayla
              </button>
            </div>
          </>
        ) : (
          <>
            <h2 id={titleId} className="flex items-center gap-2 text-lg font-semibold">
              <CheckCircle2 className="size-5 text-emerald-500" aria-hidden="true" />
              {result.approved.length} talep onaylandı
            </h2>
            {error && <p role="alert" className="mt-2 rounded-lg bg-rose-500/10 px-3 py-2 text-sm text-rose-600 dark:text-rose-400">{error}</p>}
            {result.failed.length > 0 ? (
              <>
                <p className="mt-2 text-sm text-gray-500">{result.failed.length} talep onaylanamadı ve bekleyenlerde kaldı:</p>
                <ul className="mt-3 min-h-0 flex-1 divide-y divide-gray-100 overflow-y-auto rounded-lg border border-gray-200 text-sm dark:divide-gray-700/60 dark:border-gray-700/60">
                  {result.failed.map((f) => (
                    <li key={f.id} className="flex gap-2 px-3 py-2">
                      <XCircle className="mt-0.5 size-4 shrink-0 text-rose-500" aria-hidden="true" />
                      <span><span className="font-medium text-gray-800 dark:text-gray-100">{f.fullName ?? `#${f.id}`}</span><span className="block text-xs text-gray-500">{f.reason}</span></span>
                    </li>
                  ))}
                </ul>
              </>
            ) : <p className="mt-2 text-sm text-gray-500">Seçilen taleplerin hepsi onaylandı ve bekleyenler listesinden kaldırıldı.</p>}
            <div className="mt-5 flex justify-end"><button className="btn-violet" onClick={onClose}>Kapat</button></div>
          </>
        )}
      </div>
    </div>
  )
}
