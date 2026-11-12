import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import clsx from 'clsx'
import { ChevronRight, Search, X } from 'lucide-react'
import { useSlashFocus } from '@/lib/hooks'

/* =========================================================
   Liste sayfalarının ortak parçaları:
   arama ("/" kısayolu) · satır içi filtreler · etkin filtre çipleri ·
   çerçeveli tablo · tıklanabilir satır
   ========================================================= */

export interface FilterChip {
  key: string
  label: string
}

export function ListToolbar({ searchLabel, searchPlaceholder, search, onSearch, filters, chips, onRemoveChip, onClearAll }: {
  searchLabel: string
  searchPlaceholder: string
  search: string
  onSearch: (value: string) => void
  /** Arama kutusunun yanında görünen filtre alanları */
  filters?: ReactNode
  filterCount?: number
  chips?: FilterChip[]
  onRemoveChip?: (key: string) => void
  onClearAll?: () => void
}) {
  const [text, setText] = useState(search)
  const inputRef = useRef<HTMLInputElement>(null)
  useSlashFocus(inputRef)
  useEffect(() => setText(search), [search])

  return (
    <div className="border-b border-gray-100 p-4 dark:border-gray-700/60">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-6">
        <form role="search" className="relative sm:col-span-2" onSubmit={(e) => { e.preventDefault(); onSearch(text.trim()) }}>
          <label htmlFor="list-search" className="sr-only">{searchLabel}</label>
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-gray-400" aria-hidden="true" />
          <input
            ref={inputRef}
            id="list-search"
            type="search"
            className="form-field pr-10 pl-9"
            placeholder={`${searchLabel}: ${searchPlaceholder}`}
            value={text}
            onChange={(e) => setText(e.target.value)}
          />
          <kbd className="absolute top-1/2 right-3 hidden -translate-y-1/2 rounded border border-gray-200 px-1.5 text-xs text-gray-400 md:block dark:border-gray-700">/</kbd>
        </form>
        {filters}
      </div>

      {chips && chips.length > 0 && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {chips.map((c) => (
            <button key={c.key} type="button" onClick={() => onRemoveChip?.(c.key)} aria-label={`${c.label} filtresini kaldır`}
              className="inline-flex min-h-11 items-center gap-1 rounded-full bg-violet-500/10 px-3 text-xs font-medium text-violet-700 hover:bg-violet-500/20 dark:text-violet-300">
              {c.label} <X className="size-3" aria-hidden="true" />
            </button>
          ))}
          <button type="button" onClick={onClearAll} className="ml-auto inline-flex min-h-11 items-center gap-1 px-2 text-sm font-medium text-violet-500 hover:text-violet-600">
            <X className="size-4" aria-hidden="true" /> Filtreleri temizle
          </button>
        </div>
      )}
    </div>
  )
}

/** Araç çubuğundaki filtre seçim kutusu (etiketi ekran okuyucular için). */
export function FilterSelect({ id, label, value, onChange, options, allLabel }: {
  id: string
  label: string
  value: string
  onChange: (v: string) => void
  options: { value: string; label: string }[]
  allLabel: string
}) {
  return (
    <div>
      <label htmlFor={id} className="sr-only">{label}</label>
      <select id={id} className="form-field" value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">{allLabel}</option>
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </div>
  )
}

export function FilterCheckbox({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex min-h-11 items-center gap-2 rounded-lg px-2 text-sm text-gray-600 dark:text-gray-300">
      <input type="checkbox" className="size-4 accent-violet-500" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      {label}
    </label>
  )
}

export function DataFrame({ toolbar, children, footer }: { toolbar?: ReactNode; children: ReactNode; footer?: ReactNode }) {
  return (
    <section className="card overflow-hidden">
      {toolbar}
      {children}
      {footer}
    </section>
  )
}

export function Th({ children, className }: { children?: ReactNode; className?: string }) {
  return <th scope="col" className={clsx('px-4 py-3', className)}>{children}</th>
}

/** Satırın tamamı tıklanabilir; başlık bağlantı renginde ve sonda ok var. */
export function ClickableRow({ to, children }: { to: string; children: ReactNode }) {
  const navigate = useNavigate()
  return (
    <tr
      onClick={(e) => {
        if ((e.target as HTMLElement).closest('a,button,select,input')) return
        navigate(to)
      }}
      className="group cursor-pointer border-b border-gray-100 last:border-0 hover:bg-gray-50 dark:border-gray-700/60 dark:hover:bg-gray-700/10"
    >
      {children}
      <td className="w-10 pr-4 text-right">
        <ChevronRight className="inline size-4 text-gray-300 group-hover:text-violet-500 dark:text-gray-600" aria-hidden="true" />
      </td>
    </tr>
  )
}
