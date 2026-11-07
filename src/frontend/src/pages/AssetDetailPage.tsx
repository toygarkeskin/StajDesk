import { useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Pencil, Trash2 } from 'lucide-react'
import { api, errorDetails, errorMessage } from '@/lib/api'
import { assetTypeLabels } from '@/lib/labels'
import { formatDate, timeAgo } from '@/lib/format'
import { useRole } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { AssetStatusBadge, BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, IconButton, PageHeader, PriorityText, StatusBadge } from '@/components/ui'
import { ClickableRow, DataFrame, Th } from '@/components/list'

/* Detail / Record kalıbı — cihaz. Sekmeler: Ayrıntılar | Talepler */

const Row = ({ label, children }: { label: string; children: ReactNode }) => (
  <div className="grid grid-cols-[10rem_1fr] gap-4 py-3 text-sm">
    <dt className="text-gray-600 dark:text-gray-400">{label}</dt>
    <dd className="text-gray-900 dark:text-gray-100">{children || <span className="text-gray-500">Girilmemiş</span>}</dd>
  </div>
)

export function AssetDetailPage() {
  const id = Number(useParams().id)
  const { isStaff, isAdmin } = useRole()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [tab, setTab] = useState<'details' | 'tickets'>('details')
  const [deleteOpen, setDeleteOpen] = useState(false)
  const asset = useQuery({ queryKey: ['asset', id], queryFn: () => api.assets.get(id) })
  const remove = useMutation({
    mutationFn: () => api.assets.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['assets'] })
      toast('Cihaz envanterden silindi.')
      navigate('/assets')
    },
    onError: (err) => {
      setDeleteOpen(false)
      toast(errorMessage(err), 'error')
    },
  })

  if (asset.isLoading) return <div className="space-y-6"><BlockSkeleton className="h-24" /><BlockSkeleton className="h-80" /></div>
  if (asset.isError) return <div className="card"><ErrorState message={errorMessage(asset.error)} details={errorDetails(asset.error)} onRetry={() => asset.refetch()} /></div>
  const a = asset.data!

  return (
    <>
      <PageHeader
        breadcrumb={isStaff ? [{ label: 'Envanter', to: '/assets' }, { label: a.inventoryNumber }] : undefined}
        title={a.name}
        description={
          <span className="flex flex-wrap items-center gap-x-4 gap-y-1">
            <AssetStatusBadge status={a.status} />
            <span>{assetTypeLabels[a.type]}</span>
            <span>{a.inventoryNumber}</span>
            {a.assignedUser && <span>Zimmet: {a.assignedUser.fullName}</span>}
          </span>
        }
        actions={
          <>
            {isStaff && <Link to={`/assets/${a.id}/edit`} className="btn-secondary"><Pencil className="size-4" aria-hidden="true" /> Cihazı düzenle</Link>}
            {isAdmin && <IconButton label="Cihazı sil" tone="danger" onClick={() => setDeleteOpen(true)}><Trash2 className="size-5" /></IconButton>}
          </>
        }
      />

      <div role="tablist" aria-label="Cihaz bölümleri" className="mb-6 flex gap-2 border-b border-gray-200 dark:border-gray-700/60">
        {([['details', 'Ayrıntılar'], ['tickets', `Talepler (${a.tickets.length})`]] as const).map(([key, label]) => (
          <button key={key} role="tab" aria-selected={tab === key} onClick={() => setTab(key)}
            className={clsx('-mb-px min-h-11 border-b-2 px-4 text-sm font-medium whitespace-nowrap', tab === key ? 'border-violet-600 text-violet-700 dark:text-violet-300' : 'border-transparent text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100')}>
            {label}
          </button>
        ))}
      </div>

      {tab === 'details' ? (
        <section role="tabpanel" className="card max-w-3xl px-6 py-2">
          <dl className="divide-y divide-gray-100 dark:divide-gray-700/60">
            <Row label="Üretici / model">{[a.manufacturer, a.model].filter(Boolean).join(' ')}</Row>
            <Row label="Seri numarası">{a.serialNumber}</Row>
            <Row label="Konum">{a.location}</Row>
            <Row label="Zimmetli kullanıcı">{a.assignedUser?.fullName}</Row>
            <Row label="Satın alma">{a.purchaseDate && formatDate(a.purchaseDate)}</Row>
            <Row label="Garanti bitişi">{a.warrantyEndDate && formatDate(a.warrantyEndDate)}</Row>
            <Row label="Notlar">{a.notes && <span className="block max-w-prose whitespace-pre-wrap">{a.notes}</span>}</Row>
          </dl>
        </section>
      ) : (
        <section role="tabpanel">
          <DataFrame>
            {a.tickets.length === 0 ? (
              <EmptyState title="Bu cihaz için talep yok" description="Bu cihazla ilgili bir sorun bildirildiğinde talepler burada listelenir." action={<Link to="/tickets/new" className="btn-primary">Talep oluştur</Link>} />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="table-head"><tr><Th>Talep</Th><Th>Öncelik</Th><Th>Durum</Th><Th className="text-right">Oluşturma</Th><Th /></tr></thead>
                  <tbody>
                    {a.tickets.map((t) => (
                      <ClickableRow key={t.id} to={`/tickets/${t.id}`}>
                        <td className="px-4 py-3"><Link to={`/tickets/${t.id}`} className="font-medium text-gray-800 hover:text-violet-600 dark:text-gray-100">{t.title}</Link><div className="mt-1 text-xs text-gray-500">#{t.id}</div></td>
                        <td className="px-4 py-3 text-gray-700 dark:text-gray-300"><PriorityText priority={t.priority} /></td>
                        <td className="px-4 py-3"><StatusBadge status={t.status} /></td>
                        <td className="px-4 py-3 text-right whitespace-nowrap text-gray-600 dark:text-gray-400">{timeAgo(t.createdAt)}</td>
                      </ClickableRow>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </DataFrame>
        </section>
      )}

      <ConfirmDialog
        open={deleteOpen}
        title={`"${a.name}" cihazı silinsin mi?`}
        body={`${a.inventoryNumber} numaralı kayıt envanterden kalıcı olarak silinir. Bağlı talebi olan cihazlar silinemez; bunun yerine durumunu "Hurdaya ayrıldı" yapabilirsiniz.`}
        confirmLabel="Cihazı sil"
        pending={remove.isPending}
        onConfirm={() => remove.mutate()}
        onCancel={() => setDeleteOpen(false)}
      />
    </>
  )
}
