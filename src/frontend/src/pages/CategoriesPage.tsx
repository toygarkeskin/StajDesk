import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { categoryColors, colorOf } from '@/lib/labels'
import type { Category } from '@/lib/types'
import { toast } from '@/stores/ui'
import { BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'

/* Kategoriler — kart ızgarası. Ekleme/düzenleme yan panelde; silme isimli onayla. */

const colorNames: Record<string, string> = { violet: 'Mor', sky: 'Gök mavisi', emerald: 'Zümrüt', amber: 'Kehribar', rose: 'Gül', indigo: 'Çivit', teal: 'Deniz yeşili', orange: 'Turuncu', slate: 'Arduvaz', pink: 'Pembe' }

export function CategoriesPage() {
  const qc = useQueryClient()
  const [editing, setEditing] = useState<Category | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Category | null>(null)
  const categories = useQuery({ queryKey: ['categories', 'all'], queryFn: () => api.categories.list(true) })

  const remove = useMutation({
    mutationFn: (c: Category) => api.categories.remove(c.id),
    onSuccess: (_, c) => {
      qc.invalidateQueries({ queryKey: ['categories'] })
      toast(`"${c.name}" kategorisi silindi.`)
      setDeleting(null)
    },
    onError: (err) => {
      setDeleting(null)
      toast(errorMessage(err), 'error')
    },
  })

  return (
    <>
      <PageHeader
        title="Kategoriler"
        description="Taleplerin sınıflandırıldığı kategoriler; ada göre sıralı. Pasif kategoriler yeni taleplerde seçilemez."
        actions={<button className="btn-primary" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Kategori ekle</button>}
      />
      {categories.isLoading ? (
        <div className="grid gap-6 md:grid-cols-3">{Array.from({ length: 6 }).map((_, i) => <BlockSkeleton key={i} className="h-40" />)}</div>
      ) : categories.isError ? (
        <div className="card"><ErrorState message={errorMessage(categories.error)} details={errorDetails(categories.error)} onRetry={() => categories.refetch()} /></div>
      ) : categories.data!.length === 0 ? (
        <div className="card"><EmptyState title="Henüz kategori yok" description="Kategori ekleyin; çalışanlar talep açarken doğru ekibe yönlendirilsin." action={<button className="btn-violet" onClick={() => setEditing('new')}>Kategori ekle</button>} /></div>
      ) : (
        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {categories.data!.map((c) => (
            <div key={c.id} className={clsx('card flex flex-col p-5', !c.isActive && 'opacity-60')}>
              <div className="mb-3 flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <span className={clsx('flex size-10 items-center justify-center rounded-lg text-sm font-bold', colorOf(c.color).badge)} aria-hidden="true">{c.name[0]}</span>
                  <div>
                    <h3 className="font-semibold">{c.name}</h3>
                    {!c.isActive && <span className="text-xs text-gray-500">Pasif</span>}
                  </div>
                </div>
                <div className="-mt-2 -mr-2 flex">
                  <IconButton label={`"${c.name}" kategorisini düzenle`} onClick={() => setEditing(c)}><Pencil className="size-4" /></IconButton>
                  <IconButton label={`"${c.name}" kategorisini sil`} tone="danger" onClick={() => setDeleting(c)}><Trash2 className="size-4" /></IconButton>
                </div>
              </div>
              <p className="mb-4 flex-1 text-sm text-gray-500">{c.description || 'Açıklama yok.'}</p>
              <div className="flex gap-4 border-t border-gray-100 pt-3 text-sm dark:border-gray-700/60">
                <span><b className="text-gray-800 dark:text-gray-100">{c.openTicketCount}</b> açık</span>
                <span><b className="text-gray-800 dark:text-gray-100">{c.ticketCount}</b> toplam</span>
              </div>
            </div>
          ))}
        </div>
      )}

      {editing && <CategoryPanel category={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!deleting}
        title={`"${deleting?.name}" kategorisi silinsin mi?`}
        body={deleting?.ticketCount ? `Bu kategoride ${deleting.ticketCount} talep var; bu yüzden silinemez. Bunun yerine kategoriyi pasif yapabilirsiniz.` : 'Kategori kalıcı olarak silinir. Bu işlem geri alınamaz.'}
        confirmLabel="Kategoriyi sil"
        pending={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting)}
        onCancel={() => setDeleting(null)}
      />
    </>
  )
}

function CategoryPanel({ category, onClose }: { category?: Category; onClose: () => void }) {
  const qc = useQueryClient()
  const form = useForm(
    { name: category?.name ?? '', description: category?.description ?? '', color: category?.color ?? 'violet', isActive: category?.isActive ?? true },
    { name: (v) => (!v.trim() ? 'Kategoriye bir ad verin.' : undefined) },
  )
  const save = useMutation({
    mutationFn: () => (category ? api.categories.update(category.id, form.values) : api.categories.create(form.values)),
    onSuccess: (c) => {
      qc.invalidateQueries({ queryKey: ['categories'] })
      toast(category ? `"${c.name}" güncellendi.` : `"${c.name}" kategorisi eklendi.`)
      onClose()
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()

  return (
    <SlideOver
      open
      onClose={onClose}
      title={category ? `"${category.name}" kategorisini düzenle` : 'Kategori ekle'}
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} {category ? 'Değişiklikleri kaydet' : 'Kategoriyi kaydet'}</button></>}
    >
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-6">
        <Field label="Ad" htmlFor="f-name" error={form.errors.name}>
          <input {...form.field('name')} className="form-field" maxLength={80} placeholder="Donanım" />
        </Field>
        <Field label="Açıklama" htmlFor="f-description" optional hint="Talep açan kişiye bu kategorinin neyi kapsadığını anlatır.">
          <textarea {...form.field('description')} className="form-field min-h-24" maxLength={300} />
        </Field>
        <fieldset>
          <legend className="form-label">Renk</legend>
          <div className="flex flex-wrap gap-1">
            {categoryColors.map((c) => (
              <label key={c} className="inline-flex size-11 cursor-pointer items-center justify-center rounded-lg has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-violet-500">
                <input type="radio" name="color" value={c} checked={form.values.color === c} onChange={() => form.setValue('color', c)} className="sr-only" aria-label={colorNames[c]} />
                <span className={clsx('size-6 rounded-full ring-offset-2 dark:ring-offset-gray-900', colorOf(c).dot, form.values.color === c && 'ring-2 ring-gray-900 dark:ring-gray-100')} aria-hidden="true" />
              </label>
            ))}
          </div>
        </fieldset>
        <label className="flex min-h-11 items-center gap-2 text-sm text-gray-800 dark:text-gray-200">
          <input type="checkbox" className="size-4 accent-violet-600" checked={form.values.isActive} onChange={(e) => form.setValue('isActive', e.target.checked)} />
          Aktif — yeni taleplerde seçilebilir
        </label>
      </form>
    </SlideOver>
  )
}
