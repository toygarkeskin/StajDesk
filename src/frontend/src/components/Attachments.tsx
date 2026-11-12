import { useRef, useState, type DragEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Download, FileImage, FileSpreadsheet, FileText, Paperclip, Trash2, Upload } from 'lucide-react'
import { api, errorMessage } from '@/lib/api'
import { timeAgo } from '@/lib/format'
import type { TicketAttachment } from '@/lib/types'
import { toast } from '@/stores/ui'
import { ConfirmDialog, IconButton, Spinner } from './ui'

/** Sunucudaki kuralların arayüz karşılığı (asıl kontrol API'de). */
export const ATTACHMENT_ACCEPT = '.png,.jpg,.jpeg,.gif,.webp,.pdf,.txt,.log,.csv,.docx,.xlsx'
export const ATTACHMENT_MAX_MB = 5

export function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

/** Seçilen dosyayı göndermeden önce hızlı kontrol: kullanıcı 5 MB'lık yüklemeyi beklemeden hatayı görür. */
export function precheck(file: File): string | undefined {
  const ext = '.' + (file.name.split('.').pop() ?? '').toLowerCase()
  if (!ATTACHMENT_ACCEPT.split(',').includes(ext)) return `"${file.name}" türü desteklenmiyor. Resim, PDF, metin, Word veya Excel dosyası seçin.`
  if (file.size > ATTACHMENT_MAX_MB * 1024 * 1024) return `"${file.name}" ${ATTACHMENT_MAX_MB} MB'tan büyük. Dosyayı küçültüp tekrar deneyin.`
  if (file.size === 0) return `"${file.name}" boş görünüyor. Başka bir dosya seçin.`
  return undefined
}

function FileIcon({ type }: { type: string }) {
  const cls = 'size-5'
  if (type.startsWith('image/')) return <FileImage className={cls} aria-hidden="true" />
  if (type.includes('spreadsheet') || type === 'text/csv') return <FileSpreadsheet className={cls} aria-hidden="true" />
  return <FileText className={cls} aria-hidden="true" />
}

/** Dosyayı kimlik doğrulamalı istekle indirir (token başlıkta gider; bağlantı paylaşılamaz). */
async function downloadFile(ticketId: number, a: TicketAttachment) {
  try {
    const blob = await api.tickets.download(ticketId, a.id)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = a.fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (err) {
    toast(errorMessage(err), 'error')
  }
}

export function AttachmentsCard({ ticketId, attachments, canAttach }: { ticketId: number; attachments: TicketAttachment[]; canAttach: boolean }) {
  const qc = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const [progress, setProgress] = useState<number | null>(null)
  const [dragging, setDragging] = useState(false)
  const [toDelete, setToDelete] = useState<TicketAttachment | null>(null)

  const refresh = () => qc.invalidateQueries({ queryKey: ['ticket', ticketId] })

  const upload = useMutation({
    mutationFn: async (files: File[]) => {
      for (const f of files) {
        setProgress(0)
        await api.tickets.upload(ticketId, f, setProgress)
      }
    },
    onSuccess: (_, files) => toast(files.length > 1 ? `${files.length} dosya eklendi.` : 'Dosya eklendi.'),
    onError: (err) => toast(errorMessage(err), 'error'),
    onSettled: () => {
      setProgress(null)
      refresh()
    },
  })

  const remove = useMutation({
    mutationFn: (a: TicketAttachment) => api.tickets.removeAttachment(ticketId, a.id),
    onSuccess: (_, a) => {
      toast(`"${a.fileName}" silindi.`)
      setToDelete(null)
      refresh()
    },
    onError: (err) => toast(errorMessage(err), 'error'),
  })

  const pick = (list: FileList | null) => {
    const files = Array.from(list ?? [])
    if (files.length === 0) return
    const problem = files.map(precheck).find(Boolean)
    if (problem) {
      toast(problem, 'error')
      return
    }
    upload.mutate(files)
  }

  const onDrop = (e: DragEvent) => {
    e.preventDefault()
    setDragging(false)
    if (canAttach && !upload.isPending) pick(e.dataTransfer.files)
  }

  return (
    <section className="card p-5" aria-labelledby="attachments-title">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 id="attachments-title" className="flex items-center gap-2 text-xs font-semibold text-gray-400 uppercase">
          <Paperclip className="size-4" aria-hidden="true" /> Ekler {attachments.length > 0 && <span className="text-gray-500">({attachments.length})</span>}
        </h2>
      </div>

      {attachments.length > 0 && (
        <ul className="mb-3 space-y-2">
          {attachments.map((a) => (
            <li key={a.id} className="flex items-center gap-3 rounded-lg border border-gray-200 py-1 pr-1 pl-3 dark:border-gray-700/60">
              <span className="shrink-0 text-violet-500"><FileIcon type={a.contentType} /></span>
              <div className="min-w-0 flex-1 text-sm">
                <div className="truncate font-medium text-gray-800 dark:text-gray-100" title={a.fileName}>{a.fileName}</div>
                <div className="truncate text-xs text-gray-500">{formatSize(a.size)} · {a.uploadedBy.fullName} · {timeAgo(a.createdAt)}</div>
              </div>
              <IconButton label={`${a.fileName} dosyasını indir`} onClick={() => downloadFile(ticketId, a)}><Download className="size-4" /></IconButton>
              {a.canDelete && (
                <IconButton label={`${a.fileName} dosyasını sil`} tone="danger" onClick={() => setToDelete(a)}><Trash2 className="size-4" /></IconButton>
              )}
            </li>
          ))}
        </ul>
      )}

      {canAttach ? (
        <div
          onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
          className={clsx('rounded-lg border border-dashed p-4 text-center transition-colors', dragging ? 'border-violet-400 bg-violet-500/5' : 'border-gray-300 dark:border-gray-600')}
        >
          <input ref={input} type="file" multiple accept={ATTACHMENT_ACCEPT} className="sr-only" id={`attach-${ticketId}`}
            onChange={(e) => { pick(e.target.files); e.target.value = '' }} />
          {upload.isPending ? (
            <div className="flex items-center justify-center gap-2 text-sm text-gray-600 dark:text-gray-300" aria-live="polite">
              <Spinner /> Yükleniyor… {progress != null && `%${progress}`}
            </div>
          ) : (
            <>
              <button type="button" className="btn-secondary" onClick={() => input.current?.click()}>
                <Upload className="size-4" aria-hidden="true" /> Dosya ekle
              </button>
              <p className="mt-2 text-xs text-gray-500">veya buraya sürükleyin · en fazla {ATTACHMENT_MAX_MB} MB · resim, PDF, metin, Word, Excel</p>
            </>
          )}
        </div>
      ) : attachments.length === 0 ? (
        <p className="text-sm text-gray-500">Bu talebe eklenmiş dosya yok.</p>
      ) : null}

      <ConfirmDialog
        open={toDelete != null}
        title={`"${toDelete?.fileName}" silinsin mi?`}
        body="Dosya talepten kalıcı olarak kaldırılır. Bu işlem geri alınamaz."
        confirmLabel="Dosyayı sil"
        pending={remove.isPending}
        onConfirm={() => toDelete && remove.mutate(toDelete)}
        onCancel={() => setToDelete(null)}
      />
    </section>
  )
}
