import { useDeferredValue, useMemo, useState } from 'react'
import { Link } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, Pencil, Plus, Search, Trash2 } from 'lucide-react'
import { errorMessage, fieldErrors } from '@/lib/api'
import { socialApi, type FaqItem } from '@/lib/social'
import { useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { Badge, BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'

/* Yardım: sık sorulan sorular. Herkes okur ve arar; faq.manage (varsayılan: destek personeli) yönetir. */

export function FaqPage() {
  const { can } = useCan()
  const manage = can('faq.manage')
  const [search, setSearch] = useState('')
  const term = useDeferredValue(search.trim())
  const q = useQuery({ queryKey: ['faq', term], queryFn: () => socialApi.faq(term || undefined) })
  const [edit, setEdit] = useState<FaqItem | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<FaqItem | null>(null)
  const qc = useQueryClient()
  const remove = useMutation({
    mutationFn: (f: FaqItem) => socialApi.removeFaq(f.id),
    onSuccess: () => { toast('Soru silindi.'); setToDelete(null); qc.invalidateQueries({ queryKey: ['faq'] }) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const groups = useMemo(() => {
    const m = new Map<string, FaqItem[]>()
    for (const f of q.data ?? []) m.set(f.category, [...(m.get(f.category) ?? []), f])
    return [...m.entries()]
  }, [q.data])

  return (
    <>
      <PageHeader title="Yardım ve sık sorulan sorular" description="Aradığınızı bulamazsanız destek talebi açın; ekibimiz yardımcı olur."
        actions={manage && <button className="btn-violet" onClick={() => setEdit('new')}><Plus className="size-4" aria-hidden="true" /> Soru ekle</button>} />
      <div className="mx-auto max-w-3xl space-y-4">
        <label className="relative block">
          <span className="sr-only">Sorularda ara</span>
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-gray-400" aria-hidden="true" />
          <input className="form-field pl-9" placeholder="Ör. şifre, izin, ekran görüntüsü" value={search} onChange={(e) => setSearch(e.target.value)} />
        </label>
        {q.isLoading ? <BlockSkeleton className="h-64" /> : q.isError ? <div className="card"><ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} /></div> : groups.length === 0 ? (
          <div className="card"><EmptyState title={term ? 'Eşleşen soru yok' : 'Henüz soru yok'} description="Sorunuzu destek ekibine iletin." action={<Link to="/tickets/new" className="btn-violet">Destek talebi aç</Link>} /></div>
        ) : groups.map(([category, items]) => (
          <section key={category} className="card overflow-hidden" aria-label={category}>
            <h2 className="border-b border-gray-100 px-4 py-3 text-sm font-semibold dark:border-gray-700/60">{category}</h2>
            <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
              {items.map((f) => (
                <li key={f.id}>
                  <details className="group">
                    <summary className="flex min-h-11 cursor-pointer list-none items-center gap-2 px-4 py-3 text-sm font-medium text-gray-800 dark:text-gray-100">
                      <ChevronDown className="size-4 shrink-0 text-gray-400 transition-transform group-open:rotate-180" aria-hidden="true" />
                      <span className="flex-1">{f.question}</span>
                      {!f.isPublished && <Badge className="bg-gray-500/15 text-gray-600 dark:text-gray-400">Taslak</Badge>}
                    </summary>
                    <div className="flex items-start gap-2 px-4 pb-4 pl-10">
                      <p className="flex-1 text-sm whitespace-pre-wrap text-gray-700 dark:text-gray-300">{f.answer}</p>
                      {manage && (
                        <span className="flex shrink-0">
                          <IconButton label={`"${f.question}" sorusunu düzenle`} onClick={() => setEdit(f)}><Pencil className="size-4" /></IconButton>
                          <IconButton label={`"${f.question}" sorusunu sil`} tone="danger" onClick={() => setToDelete(f)}><Trash2 className="size-4" /></IconButton>
                        </span>
                      )}
                    </div>
                  </details>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>
      {edit && <FaqForm item={edit === 'new' ? null : edit} categories={groups.map(([c]) => c)} onClose={() => setEdit(null)} />}
      <ConfirmDialog open={!!toDelete} title="Soru silinsin mi?" body="Soru yardım sayfasından kalıcı olarak kaldırılır. Gizlemek için taslağa alın." confirmLabel="Soruyu sil"
        pending={remove.isPending} onConfirm={() => toDelete && remove.mutate(toDelete)} onCancel={() => setToDelete(null)} />
    </>
  )
}

function FaqForm({ item, categories, onClose }: { item: FaqItem | null; categories: string[]; onClose: () => void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({ category: item?.category ?? categories[0] ?? '', question: item?.question ?? '', answer: item?.answer ?? '', sortOrder: String(item?.sortOrder ?? 10), isPublished: item?.isPublished ?? true })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useMutation({
    mutationFn: () => socialApi.saveFaq(item?.id ?? null, { ...f, sortOrder: Number(f.sortOrder) || 0 }),
    onSuccess: () => { toast('Soru kaydedildi.'); qc.invalidateQueries({ queryKey: ['faq'] }); onClose() },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const e = (k: string) => errors[k] ?? errors[k[0]!.toUpperCase() + k.slice(1)]
  return (
    <SlideOver open onClose={onClose} title={item ? 'Soruyu düzenle' : 'Soru ekle'}
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />}Soruyu kaydet</button></div>}>
      <div className="space-y-4">
        <Field label="Kategori" htmlFor="faq-cat" required error={e('category')} hint="Yeni kategori için yeni bir ad yazın.">
          <input id="faq-cat" className="form-field" list="faq-cats" maxLength={60} value={f.category} onChange={(x) => setF((p) => ({ ...p, category: x.target.value }))} />
          <datalist id="faq-cats">{categories.map((c) => <option key={c} value={c} />)}</datalist>
        </Field>
        <Field label="Soru" htmlFor="faq-q" required error={e('question')}><input id="faq-q" className="form-field" maxLength={300} value={f.question} onChange={(x) => setF((p) => ({ ...p, question: x.target.value }))} /></Field>
        <Field label="Cevap" htmlFor="faq-a" required error={e('answer')}><textarea id="faq-a" className="form-field min-h-40" maxLength={5000} value={f.answer} onChange={(x) => setF((p) => ({ ...p, answer: x.target.value }))} /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Sıra" htmlFor="faq-o"><input id="faq-o" type="number" className="form-field" value={f.sortOrder} onChange={(x) => setF((p) => ({ ...p, sortOrder: x.target.value }))} /></Field>
          <label className="flex min-h-11 items-center gap-2 text-sm sm:pt-6"><input type="checkbox" className="size-4 accent-violet-500" checked={f.isPublished} onChange={(x) => setF((p) => ({ ...p, isPublished: x.target.checked }))} /> Yayımla</label>
        </div>
      </div>
    </SlideOver>
  )
}
