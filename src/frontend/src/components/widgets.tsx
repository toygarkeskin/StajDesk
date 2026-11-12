import type { ReactNode } from 'react'
import { Link } from 'react-router'
import clsx from 'clsx'

/**
 * Panel kutucuğu (widget): ince başlık şeridi + gövde. Ana sayfadaki tüm kutular aynı iskeleti kullanır;
 * böylece farklı içerikler (liste, sayı, grafik) aynı ritimde hizalanır.
 */
export function Widget({ title, icon, action, children, className, bodyClassName, id }: {
  title: string
  icon?: ReactNode
  action?: { label: string; to: string }
  children: ReactNode
  className?: string
  bodyClassName?: string
  id?: string
}) {
  const headingId = id ?? `w-${title.toLowerCase().replace(/[^a-z0-9ğüşıöç]+/gi, '-')}`
  return (
    <section aria-labelledby={headingId} className={clsx('card flex min-w-0 flex-col overflow-hidden', className)}>
      <header className="flex min-h-11 items-center justify-between gap-2 border-b border-gray-100 px-4 dark:border-gray-700/60">
        <h2 id={headingId} className="flex min-w-0 items-center gap-2 truncate text-[13px] font-semibold text-gray-800 dark:text-gray-100">
          {icon && <span className="shrink-0 text-gray-400" aria-hidden="true">{icon}</span>}
          {title}
        </h2>
        {action && (
          <Link to={action.to} className="inline-flex min-h-11 shrink-0 items-center text-xs font-medium text-violet-600 hover:text-violet-700 dark:text-violet-400">
            {action.label} →
          </Link>
        )}
      </header>
      <div className={clsx('min-w-0 flex-1 p-4', bodyClassName)}>{children}</div>
    </section>
  )
}

/** Kutucuk içi boş durum: tek satır açıklama + isteğe bağlı eylem (süs görseli yok). */
export function WidgetEmpty({ icon, text, action }: { icon?: ReactNode; text: string; action?: ReactNode }) {
  return (
    <div className="flex h-full min-h-28 flex-col items-center justify-center gap-2 text-center text-sm text-gray-500">
      {icon && <span className="text-gray-300 dark:text-gray-600" aria-hidden="true">{icon}</span>}
      <p>{text}</p>
      {action}
    </div>
  )
}

/** Büyük sayı kutucuğu (döviz kurları kutucuklarının karşılığı): etiket, değer, alt not. */
export function NumberTile({ label, value, unit, hint, to, accent }: {
  label: string
  value: ReactNode
  unit?: string
  hint?: ReactNode
  to?: string
  accent: string
}) {
  const body = (
    <>
      <div className="flex items-center gap-2 text-[13px] font-semibold text-gray-800 dark:text-gray-100">
        <span className={clsx('size-2 shrink-0 rounded-full', accent)} aria-hidden="true" />
        <span className="truncate">{label}</span>
      </div>
      <div className="mt-3 flex items-baseline gap-1">
        <span className="text-3xl font-bold text-gray-900 tabular-nums dark:text-gray-50">{value}</span>
        {unit && <span className="text-sm text-gray-500">{unit}</span>}
      </div>
      {hint && <div className="mt-1 truncate text-xs text-gray-500">{hint}</div>}
    </>
  )
  return to
    ? <Link to={to} className="card block min-w-0 p-4 transition hover:ring-2 hover:ring-violet-500/30">{body}</Link>
    : <div className="card min-w-0 p-4">{body}</div>
}
