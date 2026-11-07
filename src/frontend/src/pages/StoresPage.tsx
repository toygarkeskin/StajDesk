import { useState } from 'react'
import { Link } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MapPin, Pencil, Phone, Plus, Store as StoreIcon } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import type { Store } from '@/lib/types'
import { useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { BlockSkeleton, EmptyState, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'

/** Mağazalar (GLPI'daki "entity"): kullanıcılar, talepler ve cihazlar bir mağazaya bağlıdır. */
export function StoresPage() {
  const { can } = useCan()
  const [editing, setEditing] = useState<Store | 'new' | null>(null)
  const stores = useQuery({ queryKey: ['stores'], queryFn: api.stores.list })
  const canManage = can('stores.manage')

  return (
    <>
      <PageHeader
        title="Mağazalar"
        description={canManage ? 'Şirketin mağaza ve şubeleri; talepler ve cihazlar mağazaya göre izlenir.' : 'Mağazanızın özet bilgileri.'}
        actions={canManage && <button className="btn-primary" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Mağaza ekle</button>}
      />
      {stores.isLoading ? (
        <div className="grid gap-6 md:grid-cols-3">{Array.from({ length: 3 }).map((_, i) => <BlockSkeleton key={i} className="h-48" />)}</div>
      ) : stores.isError ? (
        <div className="card"><ErrorState message={errorMessage(stores.error)} details={errorDetails(stores.error)} onRetry={() => stores.refetch()} /></div>
      ) : stores.data!.length === 0 ? (
        <div className="card"><EmptyState title="Henüz mağaza yok" description="Mağaza ekleyin; kullanıcıları ve cihazları mağazalara bağlayın." action={canManage && <button className="btn-violet" onClick={() => setEditing('new')}>Mağaza ekle</button>} /></div>
      ) : (
        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {stores.data!.map((s) => (
            <div key={s.id} className={`card flex flex-col p-5 ${s.isActive ? '' : 'opacity-60'}`}>
              <div className="mb-3 flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <span className="flex size-10 items-center justify-center rounded-lg bg-violet-500/10 text-violet-500"><StoreIcon className="size-5" aria-hidden="true" /></span>
                  <div>
                    <h3 className="font-semibold">{s.name}</h3>
                    <div className="text-xs text-gray-500">{s.code}{!s.isActive && ' · Pasif'}</div>
                  </div>
                </div>
                {canManage && <div className="-mt-2 -mr-2"><IconButton label={`"${s.name}" mağazasını düzenle`} onClick={() => setEditing(s)}><Pencil className="size-4" /></IconButton></div>}
              </div>
              <div className="mb-4 flex-1 space-y-1 text-sm text-gray-500">
                {(s.city || s.address) && <div className="flex items-center gap-2"><MapPin className="size-4" aria-hidden="true" /> {[s.address, s.city].filter(Boolean).join(', ')}</div>}
                {s.phone && <div className="flex items-center gap-2"><Phone className="size-4" aria-hidden="true" /> {s.phone}</div>}
                <div>Mağaza sahibi: <span className="font-medium text-gray-700 dark:text-gray-300">{s.owners.length ? s.owners.join(', ') : 'atanmadı'}</span></div>
              </div>
              <div className="grid grid-cols-3 gap-2 border-t border-gray-100 pt-3 text-center text-sm dark:border-gray-700/60">
                <Link to={`/tickets?storeId=${s.id}&activeOnly=true`} className="rounded-lg py-1 hover:bg-gray-50 dark:hover:bg-gray-700/20"><b className="block text-lg text-gray-800 dark:text-gray-100">{s.openTicketCount}</b>açık talep</Link>
                <div className="py-1"><b className="block text-lg text-gray-800 dark:text-gray-100">{s.userCount}</b>kullanıcı</div>
                <div className="py-1"><b className="block text-lg text-gray-800 dark:text-gray-100">{s.assetCount}</b>cihaz</div>
              </div>
            </div>
          ))}
        </div>
      )}
      {editing && <StorePanel store={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function StorePanel({ store, onClose }: { store?: Store; onClose: () => void }) {
  const qc = useQueryClient()
  const form = useForm(
    { code: store?.code ?? '', name: store?.name ?? '', city: store?.city ?? '', address: store?.address ?? '', phone: store?.phone ?? '', isActive: store?.isActive ?? true },
    { code: (v) => (!v.trim() ? 'Mağaza kodunu girin (ör. IST-03).' : undefined), name: (v) => (!v.trim() ? 'Mağaza adını girin.' : undefined) },
  )
  const save = useMutation({
    mutationFn: () => (store ? api.stores.update(store.id, form.values) : api.stores.create(form.values)),
    onSuccess: (s) => {
      qc.invalidateQueries({ queryKey: ['stores'] })
      toast(store ? `"${s.name}" güncellendi.` : `"${s.name}" mağazası eklendi.`)
      onClose()
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()
  const input = (k: 'code' | 'name' | 'city' | 'address' | 'phone', ph?: string) => <input {...form.field(k)} className="form-field" placeholder={ph} />

  return (
    <SlideOver open onClose={onClose} title={store ? `"${store.name}" mağazasını düzenle` : 'Mağaza ekle'}
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} {store ? 'Değişiklikleri kaydet' : 'Mağazayı kaydet'}</button></>}>
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-5">
        <Field label="Mağaza kodu" htmlFor="f-code" error={form.errors.code}>{input('code', 'IST-03')}</Field>
        <Field label="Mağaza adı" htmlFor="f-name" error={form.errors.name}>{input('name', 'Üsküdar Mağazası')}</Field>
        <Field label="Şehir" htmlFor="f-city" optional>{input('city')}</Field>
        <Field label="Adres" htmlFor="f-address" optional>{input('address')}</Field>
        <Field label="Telefon" htmlFor="f-phone" optional>{input('phone')}</Field>
        <label className="flex min-h-11 items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
          <input type="checkbox" className="size-4 accent-violet-500" checked={form.values.isActive} onChange={(e) => form.setValue('isActive', e.target.checked)} />
          Aktif mağaza
        </label>
      </form>
    </SlideOver>
  )
}
