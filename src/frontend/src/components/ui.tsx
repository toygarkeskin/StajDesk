import { useId, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import clsx from 'clsx'
import { AlertTriangle, Check, ChevronLeft, ChevronRight, Copy, Inbox, Loader2, X } from 'lucide-react'
import { useDelayedFlag, useFocusTrap } from '@/lib/hooks'
import { assetStatusClasses, assetStatusLabels, colorOf, priorityClasses, priorityLabels, statusClasses, statusLabels } from '@/lib/labels'
import { initials } from '@/lib/format'
import type { AssetStatus, TicketPriority, TicketStatus } from '@/lib/types'

/* =========================================================
   Rozetler
   ========================================================= */

export function Badge({ className, children }: { className?: string; children: ReactNode }) {
  return <span className={clsx('inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap', className)}>{children}</span>
}

export const StatusBadge = ({ status }: { status: TicketStatus }) => <Badge className={statusClasses[status]}>{statusLabels[status]}</Badge>
export const PriorityText = ({ priority }: { priority: TicketPriority }) => <Badge className={priorityClasses[priority]}>{priorityLabels[priority]}</Badge>
export const AssetStatusBadge = ({ status }: { status: AssetStatus }) => <Badge className={assetStatusClasses[status]}>{assetStatusLabels[status]}</Badge>
export const CategoryText = ({ name, color }: { name: string; color: string }) => <Badge className={colorOf(color).badge}>{name}</Badge>

/* ---------- Avatar ---------- */

const avatarColors = ['bg-violet-500', 'bg-sky-500', 'bg-emerald-500', 'bg-amber-500', 'bg-rose-500', 'bg-indigo-500']

export function Avatar({ name, size = 'md' }: { name: string; size?: 'sm' | 'md' | 'lg' }) {
  const color = avatarColors[[...name].reduce((a, c) => a + c.charCodeAt(0), 0) % avatarColors.length]
  return (
    <span
      aria-hidden="true"
      className={clsx(
        'inline-flex shrink-0 items-center justify-center rounded-full font-semibold text-white',
        color,
        size === 'sm' && 'size-6 text-xs',
        size === 'md' && 'size-8 text-xs',
        size === 'lg' && 'size-10 text-sm',
      )}
    >
      {initials(name)}
    </span>
  )
}

/* =========================================================
   Sayfa başlığı (+ isteğe bağlı gezinti izi)
   ========================================================= */

export function PageHeader({ title, description, actions, breadcrumb }: {
  title: ReactNode
  description?: ReactNode
  actions?: ReactNode
  breadcrumb?: { label: string; to?: string }[]
}) {
  return (
    <div className="mb-8">
      {breadcrumb && (
        <nav aria-label="Gezinti izi" className="mb-2 flex items-center gap-1 text-sm text-gray-500">
          {breadcrumb.map((b, i) => (
            <span key={i} className="flex items-center gap-1">
              {i > 0 && <ChevronRight className="size-4" aria-hidden="true" />}
              {b.to ? <Link to={b.to} className="font-medium hover:text-violet-600">{b.label}</Link> : <span>{b.label}</span>}
            </span>
          ))}
        </nav>
      )}
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-bold md:text-3xl">{title}</h1>
          {description && <div className="mt-1 text-sm text-gray-500 dark:text-gray-400">{description}</div>}
        </div>
        {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
      </div>
    </div>
  )
}

/* =========================================================
   Simgeli buton: 44px, gerçek tooltip + aria-label
   ========================================================= */

export function Tooltip({ label, shortcut, children }: { label: string; shortcut?: string; children: ReactNode }) {
  return (
    <span className="group/tt relative inline-flex">
      {children}
      <span
        role="tooltip"
        className="pointer-events-none absolute top-full left-1/2 z-50 mt-1 -translate-x-1/2 rounded-md bg-gray-900 px-2 py-1 text-xs font-medium whitespace-nowrap text-white opacity-0 shadow-xs transition-opacity duration-150 group-focus-within/tt:opacity-100 dark:bg-gray-100 dark:text-gray-900 [@media(hover:hover)]:group-hover/tt:opacity-100"
      >
        {label}
        {shortcut && <kbd className="ml-2 rounded border border-white/20 px-1 font-sans dark:border-gray-900/20">{shortcut}</kbd>}
      </span>
    </span>
  )
}

export function IconButton({ label, shortcut, onClick, children, className, tone = 'default', ...rest }: {
  label: string
  shortcut?: string
  onClick?: () => void
  children: ReactNode
  className?: string
  tone?: 'default' | 'danger'
} & Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, 'onClick' | 'children'>) {
  return (
    <Tooltip label={label} shortcut={shortcut}>
      <button
        type="button"
        aria-label={label}
        onClick={onClick}
        className={clsx(
          'inline-flex size-11 shrink-0 items-center justify-center rounded-full transition',
          tone === 'danger'
            ? 'text-gray-400 hover:bg-rose-50 hover:text-rose-500 dark:hover:bg-gray-700'
            : 'text-gray-500 hover:bg-gray-100 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700/50 dark:hover:text-gray-200',
          className,
        )}
        {...rest}
      >
        {children}
      </button>
    </Tooltip>
  )
}

/* =========================================================
   Yükleme: 100 ms altı gösterge yok; içerik için iskelet
   ========================================================= */

export const Spinner = ({ className }: { className?: string }) => <Loader2 aria-hidden="true" className={clsx('animate-spin', className ?? 'size-4')} />

const Bone = ({ className }: { className?: string }) => <span className={clsx('block rounded bg-gray-200 motion-safe:animate-pulse dark:bg-gray-700/60', className)} />

export function TableSkeleton({ rows = 6, cols = 5 }: { rows?: number; cols?: number }) {
  const show = useDelayedFlag(true)
  if (!show) return <div className="h-64" aria-busy="true" />
  return (
    <div aria-busy="true" aria-label="Yükleniyor">
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex items-center gap-4 border-b border-gray-100 px-5 py-4 last:border-0 dark:border-gray-700/60">
          {Array.from({ length: cols }).map((_, c) => <Bone key={c} className={clsx('h-4', c === 0 ? 'w-2/5' : 'w-1/6')} />)}
        </div>
      ))}
    </div>
  )
}

export function BlockSkeleton({ className }: { className?: string }) {
  const show = useDelayedFlag(true)
  return <div aria-busy="true" className={clsx('card p-5', className)}>{show && <div className="space-y-4"><Bone className="h-4 w-1/3" /><Bone className="h-4 w-2/3" /><Bone className="h-20 w-full" /></div>}</div>
}

/* =========================================================
   Boş durum ve hata
   ========================================================= */

export function EmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center px-4 py-16 text-center">
      <div className="mb-4 flex size-14 items-center justify-center rounded-full bg-violet-500/10 text-violet-500">
        <Inbox className="size-6" aria-hidden="true" />
      </div>
      <h3 className="mb-1 text-base font-semibold">{title}</h3>
      <p className="mb-4 max-w-sm text-sm text-gray-500">{description}</p>
      {action}
    </div>
  )
}

export function ErrorState({ message, details, onRetry }: { message: string; details?: string; onRetry?: () => void }) {
  const [copied, setCopied] = useState(false)
  return (
    <div role="alert" className="flex flex-col items-center px-4 py-16 text-center">
      <div className="mb-4 flex size-14 items-center justify-center rounded-full bg-rose-500/10 text-rose-500">
        <AlertTriangle className="size-6" aria-hidden="true" />
      </div>
      <h3 className="mb-1 text-base font-semibold">Bu içerik yüklenemedi</h3>
      <p className="mb-4 max-w-sm text-sm text-gray-500">{message}</p>
      <div className="flex flex-wrap justify-center gap-2">
        {onRetry && <button className="btn-secondary" onClick={onRetry}>Tekrar dene</button>}
        {details && (
          <button className="btn-secondary" onClick={async () => { await navigator.clipboard?.writeText(details); setCopied(true) }}>
            {copied ? <Check className="size-4" aria-hidden="true" /> : <Copy className="size-4" aria-hidden="true" />}
            {copied ? 'Ayrıntılar kopyalandı' : 'Hata ayrıntılarını kopyala'}
          </button>
        )}
      </div>
    </div>
  )
}

/* =========================================================
   Sayfalama: her zaman görünür
   ========================================================= */

export function Pagination({ page, totalPages, totalCount, pageSize, onChange }: {
  page: number
  totalPages: number
  totalCount: number
  pageSize: number
  onChange: (page: number) => void
}) {
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)
  return (
    <nav aria-label="Sayfalama" className="flex flex-col items-center justify-between gap-3 border-t border-gray-100 px-5 py-3 md:flex-row dark:border-gray-700/60">
      <p className="text-sm text-gray-500">
        <span className="font-medium text-gray-800 dark:text-gray-200">{from}</span>–
        <span className="font-medium text-gray-800 dark:text-gray-200">{to}</span> / {totalCount} kayıt
      </p>
      <div className="flex items-center gap-2">
        <button className="btn-secondary" disabled={page <= 1} onClick={() => onChange(page - 1)}>
          <ChevronLeft className="size-4" aria-hidden="true" /> Önceki
        </button>
        <span className="text-sm text-gray-500">{page} / {Math.max(totalPages, 1)}</span>
        <button className="btn-secondary" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>
          Sonraki <ChevronRight className="size-4" aria-hidden="true" />
        </button>
      </div>
    </nav>
  )
}

/* =========================================================
   Form alanı: görünür ve bağlı etiket + hata/yardım
   ========================================================= */

export function Field({ label, htmlFor, error, hint, optional, required, children }: {
  label: string
  htmlFor: string
  error?: string
  hint?: string
  optional?: boolean
  required?: boolean
  children: ReactNode
}) {
  return (
    <div>
      <label htmlFor={htmlFor} className="form-label">
        {label} {required && <span className="text-rose-500">*</span>}
        {optional && <span className="font-normal text-gray-400">(isteğe bağlı)</span>}
      </label>
      {children}
      {error ? (
        <p id={`${htmlFor}-error`} className="mt-1 text-xs text-rose-500">{error}</p>
      ) : hint ? (
        <p className="mt-1 text-xs text-gray-400">{hint}</p>
      ) : null}
    </div>
  )
}

/* =========================================================
   Yandan açılan panel (slide-over)
   ========================================================= */

export function SlideOver({ open, onClose, title, description, children, footer, wide }: {
  open: boolean
  onClose: () => void
  title: string
  description?: string
  children: ReactNode
  footer: ReactNode
  /** Tablo içeren paneller için daha geniş görünüm (telefonda yine tam genişlik). */
  wide?: boolean
}) {
  const ref = useFocusTrap<HTMLDivElement>(open, onClose)
  const titleId = useId()
  if (!open) return null
  return (
    <div className="fixed inset-0 z-50">
      <div className="absolute inset-0 animate-fade-in bg-gray-900/40" onClick={onClose} aria-hidden="true" />
      <div ref={ref} role="dialog" aria-modal="true" aria-labelledby={titleId}
        className={clsx('absolute inset-y-0 right-0 flex w-full animate-slide-in flex-col bg-white shadow-xl dark:bg-gray-800', wide ? 'max-w-3xl' : 'max-w-md')}>
        <div className="flex items-start justify-between gap-4 border-b border-gray-100 px-5 py-3 dark:border-gray-700/60">
          <div className="py-2">
            <h2 id={titleId} className="font-semibold">{title}</h2>
            {description && <p className="mt-1 text-sm text-gray-500">{description}</p>}
          </div>
          <IconButton label="Paneli kapat" shortcut="Esc" onClick={onClose}><X className="size-5" /></IconButton>
        </div>
        <div className="flex-1 overflow-y-auto px-5 py-5">{children}</div>
        <div className="flex justify-end gap-2 border-t border-gray-100 px-5 py-3 dark:border-gray-700/60">{footer}</div>
      </div>
    </div>
  )
}

/* =========================================================
   Silme onayı: kaydın adını söyler, odak "Vazgeç"te
   ========================================================= */

export function ConfirmDialog({ open, title, body, confirmLabel, pending, onConfirm, onCancel }: {
  open: boolean
  title: string
  body: string
  confirmLabel: string
  pending?: boolean
  onConfirm: () => void
  onCancel: () => void
}) {
  const cancelRef = useRef<HTMLButtonElement>(null)
  const ref = useFocusTrap<HTMLDivElement>(open, onCancel, cancelRef)
  const titleId = useId()
  if (!open) return null
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4">
      <div className="absolute inset-0 animate-fade-in bg-gray-900/40" onClick={onCancel} aria-hidden="true" />
      <div ref={ref} role="alertdialog" aria-modal="true" aria-labelledby={titleId} className="card relative w-full max-w-md animate-fade-in p-6">
        <div className="flex gap-4">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-full bg-rose-500/10 text-rose-500"><AlertTriangle className="size-5" aria-hidden="true" /></div>
          <div>
            <h2 id={titleId} className="font-semibold">{title}</h2>
            <p className="mt-2 text-sm text-gray-500">{body}</p>
          </div>
        </div>
        <div className="mt-6 flex justify-end gap-2">
          <button ref={cancelRef} className="btn-secondary" onClick={onCancel}>Vazgeç</button>
          <button className="btn-danger" onClick={onConfirm} aria-busy={pending}>{pending && <Spinner />} {confirmLabel}</button>
        </div>
      </div>
    </div>
  )
}

/* =========================================================
   İstatistik kartı (simgeli, renkli)
   ========================================================= */

const tones = {
  violet: 'bg-violet-500/10 text-violet-500',
  sky: 'bg-sky-500/10 text-sky-500',
  amber: 'bg-amber-500/10 text-amber-500',
  emerald: 'bg-emerald-500/10 text-emerald-500',
  rose: 'bg-rose-500/10 text-rose-500',
  gray: 'bg-gray-500/10 text-gray-500',
}

export function StatCard({ label, value, icon, tone = 'violet', hint, to }: {
  label: string
  value: ReactNode
  icon: ReactNode
  tone?: keyof typeof tones
  hint?: ReactNode
  to?: string
}) {
  const body = (
    <>
      <div className="flex items-start justify-between">
        <div>
          <div className="mb-1 text-xs font-semibold tracking-wide text-gray-400 uppercase dark:text-gray-500">{label}</div>
          <div className="text-3xl font-bold text-gray-800 dark:text-gray-100">{value}</div>
        </div>
        <div className={clsx('flex size-10 items-center justify-center rounded-lg', tones[tone])} aria-hidden="true">{icon}</div>
      </div>
      {hint && <div className="mt-2 text-xs text-gray-500">{hint}</div>}
    </>
  )
  return to
    ? <Link to={to} className="card block p-5 transition hover:ring-2 hover:ring-violet-500/30">{body}</Link>
    : <div className="card p-5">{body}</div>
}
