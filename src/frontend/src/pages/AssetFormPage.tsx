import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { assetStatusLabels, assetTypeLabels } from '@/lib/labels'
import type { AssetDetail } from '@/lib/types'
import { toast } from '@/stores/ui'
import { BlockSkeleton, ErrorState, Field, PageHeader, Spinner } from '@/components/ui'
import { UnsavedChangesGuard } from '@/components/UnsavedChangesGuard'

/* Form (Page) kalıbı: çok alanlı ve 10 saniyeden uzun süren iş → kendi adresi olan sayfa. */

const toForm = (a?: AssetDetail) => ({
  name: a?.name ?? '',
  inventoryNumber: a?.inventoryNumber ?? '',
  type: (a?.type ?? 'Laptop') as string,
  status: (a?.status ?? 'InStock') as string,
  manufacturer: a?.manufacturer ?? '',
  model: a?.model ?? '',
  serialNumber: a?.serialNumber ?? '',
  location: a?.location ?? '',
  purchaseDate: a?.purchaseDate ?? '',
  warrantyEndDate: a?.warrantyEndDate ?? '',
  assignedUserId: a?.assignedUser ? String(a.assignedUser.id) : '',
  storeId: a?.store ? String(a.store.id) : '',
  notes: a?.notes ?? '',
})

export function AssetFormPage() {
  const { id } = useParams()
  const editing = id !== undefined
  const asset = useQuery({ queryKey: ['asset', Number(id)], queryFn: () => api.assets.get(Number(id)), enabled: editing })

  if (editing && asset.isLoading) return <BlockSkeleton className="mx-auto h-96 max-w-2xl" />
  if (editing && asset.isError) return <div className="card"><ErrorState message={errorMessage(asset.error)} details={errorDetails(asset.error)} onRetry={() => asset.refetch()} /></div>
  return <AssetForm asset={asset.data} />
}

function AssetForm({ asset }: { asset?: AssetDetail }) {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [saved, setSaved] = useState(false)
  const users = useQuery({ queryKey: ['users', 'lookup'], queryFn: () => api.users.lookup('') })
  const stores = useQuery({ queryKey: ['stores'], queryFn: api.stores.list })
  const form = useForm(toForm(asset), {
    name: (v) => (!v.trim() ? 'Cihaza tanınır bir ad verin.' : undefined),
    inventoryNumber: (v) => (!v.trim() ? 'Envanter numarasını yazın (örnek: INV-0042).' : undefined),
    warrantyEndDate: (v, all) => (v && all.purchaseDate && v < all.purchaseDate ? 'Garanti bitişi, satın alma tarihinden sonra olmalı.' : undefined),
  })

  useEffect(() => { if (saved) navigate(asset ? `/assets/${asset.id}` : '/assets') }, [saved, asset, navigate])

  const save = useMutation({
    mutationFn: () => {
      const v = form.values
      const body = { ...v, purchaseDate: v.purchaseDate || null, warrantyEndDate: v.warrantyEndDate || null, assignedUserId: v.assignedUserId ? Number(v.assignedUserId) : null, storeId: v.storeId ? Number(v.storeId) : null }
      return asset ? api.assets.update(asset.id, body) : api.assets.create(body)
    },
    onSuccess: (a) => {
      qc.invalidateQueries({ queryKey: ['assets'] })
      qc.setQueryData(['asset', a.id], a)
      toast(asset ? `"${a.name}" güncellendi.` : `"${a.name}" envantere eklendi.`)
      setSaved(true)
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (!save.isPending && form.validateAll()) save.mutate()
  }

  const text = (name: 'name' | 'inventoryNumber' | 'manufacturer' | 'model' | 'serialNumber' | 'location', props: object = {}) => (
    <input {...form.field(name)} className="form-field" {...props} />
  )

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader
        breadcrumb={[{ label: 'Envanter', to: '/assets' }, ...(asset ? [{ label: asset.name, to: `/assets/${asset.id}` }] : []), { label: asset ? 'Düzenle' : 'Yeni cihaz' }]}
        title={asset ? `"${asset.name}" cihazını düzenle` : 'Cihaz ekle'}
        description="Zorunlu alanlar ad ve envanter numarasıdır; diğerleri sonradan da girilebilir."
      />
      <form onSubmit={submit} noValidate className="space-y-6 pb-24">
        <div className="grid gap-6 md:grid-cols-2">
          <Field label="Cihaz adı" htmlFor="f-name" error={form.errors.name}>{text('name', { placeholder: 'Dell Latitude 5440' })}</Field>
          <Field label="Envanter numarası" htmlFor="f-inventoryNumber" error={form.errors.inventoryNumber}>{text('inventoryNumber', { placeholder: 'INV-0042' })}</Field>
          <Field label="Tür" htmlFor="f-type">
            <select {...form.field('type')} className="form-field">{Object.entries(assetTypeLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>
          </Field>
          <Field label="Durum" htmlFor="f-status">
            <select {...form.field('status')} className="form-field">{Object.entries(assetStatusLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>
          </Field>
          <Field label="Üretici" htmlFor="f-manufacturer" optional>{text('manufacturer')}</Field>
          <Field label="Model" htmlFor="f-model" optional>{text('model')}</Field>
          <Field label="Seri numarası" htmlFor="f-serialNumber" optional>{text('serialNumber')}</Field>
          <Field label="Konum" htmlFor="f-location" optional>{text('location', { placeholder: 'Merkez ofis / 2. kat' })}</Field>
          <Field label="Satın alma tarihi" htmlFor="f-purchaseDate" optional>
            <input {...form.field('purchaseDate')} type="date" className="form-field" />
          </Field>
          <Field label="Garanti bitişi" htmlFor="f-warrantyEndDate" optional error={form.errors.warrantyEndDate}>
            <input {...form.field('warrantyEndDate')} type="date" className="form-field" />
          </Field>
        </div>
        <Field label="Mağaza" htmlFor="f-storeId" optional hint="Mağaza sahibi yalnızca kendi mağazasının cihazlarını görür.">
          <select {...form.field('storeId')} className="form-field">
            <option value="">Merkez depo / mağazaya bağlı değil</option>
            {stores.data?.map((s) => <option key={s.id} value={s.id}>{s.name} ({s.code})</option>)}
          </select>
        </Field>
        <Field label="Zimmetli kullanıcı" htmlFor="f-assignedUserId" optional>
          <select {...form.field('assignedUserId')} className="form-field">
            <option value="">Kimseye zimmetli değil</option>
            {users.data?.map((u) => <option key={u.id} value={u.id}>{u.fullName} · {u.email}</option>)}
          </select>
        </Field>
        <Field label="Notlar" htmlFor="f-notes" optional>
          <textarea {...form.field('notes')} className="form-field min-h-24" maxLength={2000} />
        </Field>

        <div className="sticky bottom-0 -mx-4 flex justify-end gap-2 border-t border-gray-200 bg-gray-100 px-4 py-4 md:mx-0 md:px-0 dark:border-gray-700/60 dark:bg-gray-900">
          <Link to={asset ? `/assets/${asset.id}` : '/assets'} className="btn-secondary">Vazgeç</Link>
          <button type="submit" className="btn-violet" aria-busy={save.isPending}>
            {save.isPending && <Spinner />} {asset ? 'Değişiklikleri kaydet' : 'Cihazı kaydet'}
          </button>
        </div>
      </form>
      <UnsavedChangesGuard
        when={form.dirty && !saved}
        title="Kaydedilmemiş değişiklikler silinsin mi?"
        description="Bu formda yaptığınız değişiklikler kaydedilmedi. Sayfadan çıkarsanız girdiğiniz bilgiler kaybolur."
        confirmLabel="Değişiklikleri sil"
      />
    </div>
  )
}
