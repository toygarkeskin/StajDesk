import { useDeferredValue, useEffect, useId, useMemo, useRef, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { Building2, CornerDownLeft, FileText, Megaphone, Search, Ticket } from 'lucide-react'
import { portalApi } from '@/lib/portal'
import { useNavGroups } from '@/lib/nav'
import { useFocusTrap } from '@/lib/hooks'
import { PersonAvatar } from '../AuthImage'
import { Spinner } from '../ui'

/* =========================================================
   Genel arama (Ctrl+K): sayfalar (menü kaydı, izne göre), kişiler, departmanlar, duyurular, talepler.
   Sunucu yalnızca kullanıcının zaten görebildiği kayıtları döndürür.
   ========================================================= */

interface Result { key: string; group: string; label: string; sub?: string | null; icon: ReactNode; go: () => void }

const fold = (s: string) => s.toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/ı/g, 'i')

export function GlobalSearch() {
  const [open, setOpen] = useState(false)
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); setOpen(true) }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} aria-haspopup="dialog"
        className="hidden min-h-11 w-full max-w-sm items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 text-left text-sm text-gray-400 shadow-xs hover:border-gray-300 md:flex dark:border-gray-700/60 dark:bg-gray-900/40 dark:hover:border-gray-600">
        <Search className="size-4 shrink-0" aria-hidden="true" />
        <span className="flex-1 truncate">Ara: kişi, departman, duyuru, talep, sayfa…</span>
        <kbd className="hidden rounded border border-gray-200 px-1.5 text-[11px] font-medium lg:block dark:border-gray-700">Ctrl K</kbd>
      </button>
      <button type="button" onClick={() => setOpen(true)} aria-label="Ara" className="grid size-11 place-items-center rounded-lg text-gray-500 hover:bg-gray-100 md:hidden dark:hover:bg-gray-700/40">
        <Search className="size-[18px]" aria-hidden="true" />
      </button>
      {open && <SearchDialog onClose={() => setOpen(false)} />}
    </>
  )
}

function SearchDialog({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const inputRef = useRef<HTMLInputElement>(null)
  const ref = useFocusTrap<HTMLDivElement>(true, onClose, inputRef)
  const listId = useId()
  const [q, setQ] = useState('')
  const [active, setActive] = useState(0)
  const term = useDeferredValue(q.trim())
  const groups = useNavGroups()
  const remote = useQuery({ queryKey: ['search', term], queryFn: () => portalApi.search(term), enabled: term.length >= 2, staleTime: 30_000 })

  const go = (to: string) => () => { onClose(); navigate(to) }
  const results = useMemo<Result[]>(() => {
    const out: Result[] = []
    const f = fold(term)
    if (f.length >= 1) {
      for (const g of groups) for (const it of g.items)
        if (fold(`${it.label} ${it.keywords ?? ''} ${g.title}`).includes(f))
          out.push({ key: `p${it.to}`, group: 'Sayfalar', label: it.label, sub: g.title, icon: it.icon, go: go(it.to) })
    }
    const d = remote.data
    if (d && term.length >= 2) {
      d.people.forEach((p) => out.push({ key: `u${p.id}`, group: 'Kişiler', label: p.fullName, sub: [p.title, p.department].filter(Boolean).join(' · '),
        icon: <PersonAvatar id={p.id} name={p.fullName} photoVersion={p.hasPhoto ? p.photoVersion : null} size="sm" />, go: go(`/employees/${p.id}`) }))
      d.departments.forEach((x) => out.push({ key: `d${x.id}`, group: 'Departmanlar', label: x.title, sub: x.subtitle, icon: <Building2 className="size-[18px]" />, go: go(`/employees?departmentId=${x.id}`) }))
      d.announcements.forEach((x) => out.push({ key: `a${x.id}`, group: 'Duyurular', label: x.title, sub: x.subtitle, icon: <Megaphone className="size-[18px]" />, go: go(`/announcements#duyuru-${x.id}`) }))
      d.tickets.forEach((x) => out.push({ key: `t${x.id}`, group: 'Talepler', label: x.title, sub: x.subtitle, icon: <Ticket className="size-[18px]" />, go: go(`/tickets/${x.id}`) }))
    }
    if (term.length >= 2)
      out.push({ key: 'fulltext', group: 'Diğer', label: `Taleplerde tam metin ara: "${term}"`, icon: <FileText className="size-[18px]" />, go: go(`/tickets?search=${encodeURIComponent(term)}`) })
    return out
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [term, remote.data, groups])

  useEffect(() => setActive(0), [term])
  const pick = (i: number) => results[i]?.go()

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => Math.min(a + 1, results.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)) }
    else if (e.key === 'Enter') { e.preventDefault(); pick(active) }
  }
  useEffect(() => { document.getElementById(`${listId}-${active}`)?.scrollIntoView({ block: 'nearest' }) }, [active, listId])

  let lastGroup = ''
  return (
    <div className="fixed inset-0 z-[60] flex items-start justify-center p-4 pt-[10vh]">
      <div className="absolute inset-0 animate-fade-in bg-gray-900/50" onClick={onClose} aria-hidden="true" />
      <div ref={ref} role="dialog" aria-modal="true" aria-label="Genel arama" className="card relative flex max-h-[75dvh] w-full max-w-xl animate-fade-in flex-col overflow-hidden">
        <div className="flex items-center gap-2 border-b border-gray-100 px-4 dark:border-gray-700/60">
          <Search className="size-4 shrink-0 text-gray-400" aria-hidden="true" />
          <input ref={inputRef} value={q} onChange={(e) => setQ(e.target.value)} onKeyDown={onKeyDown}
            role="combobox" aria-expanded={results.length > 0} aria-controls={listId} aria-activedescendant={results.length ? `${listId}-${active}` : undefined}
            aria-label="Ara" placeholder="Kişi, departman, duyuru, talep veya sayfa ara…"
            className="min-h-14 flex-1 bg-transparent text-sm text-gray-800 outline-none placeholder:text-gray-400 dark:text-gray-100" />
          {remote.isFetching && <Spinner />}
          <kbd className="rounded border border-gray-200 px-1.5 text-[11px] text-gray-400 dark:border-gray-700">Esc</kbd>
        </div>
        <ul id={listId} role="listbox" aria-label="Arama sonuçları" className="min-h-0 flex-1 overflow-y-auto p-2">
          {term.length === 0 && <li className="px-3 py-6 text-center text-sm text-gray-500">Yazmaya başlayın. Sonuçlar yalnızca erişiminiz olan kayıtlardan gelir.</li>}
          {term.length >= 2 && !remote.isFetching && results.length <= 1 && (
            <li className="px-3 py-4 text-center text-sm text-gray-500">"{term}" için kişi, departman, duyuru veya talep bulunamadı.</li>
          )}
          {results.map((r, i) => {
            const header = r.group !== lastGroup ? r.group : null
            lastGroup = r.group
            return (
              <li key={r.key} role="presentation">
                {header && <div className="px-3 pt-3 pb-1 text-[11px] font-semibold tracking-wide text-gray-400 uppercase" role="presentation">{header}</div>}
                <div id={`${listId}-${i}`} role="option" aria-selected={i === active} onMouseMove={() => setActive(i)} onClick={() => pick(i)}
                  className={clsx('flex min-h-11 cursor-pointer items-center gap-3 rounded-lg px-3 py-1.5', i === active ? 'bg-violet-500/10 text-violet-700 dark:text-violet-200' : 'text-gray-700 dark:text-gray-200')}>
                  <span className="shrink-0 text-gray-400" aria-hidden="true">{r.icon}</span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium">{r.label}</span>
                    {r.sub && <span className="block truncate text-xs text-gray-500">{r.sub}</span>}
                  </span>
                  {i === active && <CornerDownLeft className="size-4 shrink-0 text-gray-400" aria-hidden="true" />}
                </div>
              </li>
            )
          })}
        </ul>
        <div className="flex gap-4 border-t border-gray-100 px-4 py-2 text-[11px] text-gray-400 dark:border-gray-700/60">
          <span>↑ ↓ gez</span><span>Enter aç</span><span>Esc kapat</span>
        </div>
      </div>
    </div>
  )
}
