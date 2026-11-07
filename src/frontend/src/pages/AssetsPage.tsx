import { Link, useSearchParams } from 'react-router'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { api, errorDetails, errorMessage } from '@/lib/api'
import { assetStatusLabels, assetTypeLabels } from '@/lib/labels'
import { formatDate } from '@/lib/format'
import { AssetStatusBadge, EmptyState, ErrorState, PageHeader, Pagination, TableSkeleton } from '@/components/ui'
import { ClickableRow, DataFrame, FilterSelect, ListToolbar, Th, type FilterChip } from '@/components/list'

/* List / Index kalıbı — envanter. Ekleme ve düzenleme çok alanlı olduğu için ayrı form sayfasında. */

const PAGE_SIZE = 10

export function AssetsPage() {
  const [params, setParams] = useSearchParams()
  const get = (k: string) => params.get(k) ?? ''
  const f = { type: get('type'), status: get('status'), search: get('search'), page: Number(params.get('page') ?? 1) }
  const update = (patch: Record<string, string | number | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) v === null || v === '' ? next.delete(k) : next.set(k, String(v))
    if (!('page' in patch)) next.delete('page')
    setParams(next)
  }

  const assets = useQuery({ queryKey: ['assets', f], queryFn: () => api.assets.list({ ...f, pageSize: PAGE_SIZE }), placeholderData: keepPreviousData })

  const chips: FilterChip[] = [
    f.search && { key: 'search', label: `Arama: "${f.search}"` },
    f.type && { key: 'type', label: `Tür: ${assetTypeLabels[f.type as keyof typeof assetTypeLabels]}` },
    f.status && { key: 'status', label: `Durum: ${assetStatusLabels[f.status as keyof typeof assetStatusLabels]}` },
  ].filter(Boolean) as FilterChip[]
  const cols = 7

  return (
    <>
      <PageHeader
        title="Envanter"
        description="Şirket cihazları, zimmet bilgileri ve garanti durumu; envanter numarasına göre sıralı."
        actions={<Link to="/assets/new" className="btn-primary"><Plus className="size-4" aria-hidden="true" /> Cihaz ekle</Link>}
      />
      <DataFrame
        toolbar={
          <ListToolbar
            searchLabel="Cihaz ara"
            searchPlaceholder="Ad, envanter no, seri no veya konum"
            search={f.search}
            onSearch={(v) => update({ search: v })}
            filterCount={chips.filter((c) => c.key !== 'search').length}
            chips={chips}
            onRemoveChip={(k) => update({ [k]: null })}
            onClearAll={() => setParams({})}
            filters={
              <>
                <FilterSelect id="flt-type" label="Tür" allLabel="Tüm türler" value={f.type} onChange={(v) => update({ type: v })}
                  options={Object.entries(assetTypeLabels).map(([value, label]) => ({ value, label }))} />
                <FilterSelect id="flt-status" label="Durum" allLabel="Tüm durumlar" value={f.status} onChange={(v) => update({ status: v })}
                  options={Object.entries(assetStatusLabels).map(([value, label]) => ({ value, label }))} />
              </>
            }
          />
        }
        footer={<Pagination page={assets.data?.page ?? f.page} totalPages={assets.data?.totalPages ?? 0} totalCount={assets.data?.totalCount ?? 0} pageSize={PAGE_SIZE} onChange={(p) => update({ page: p })} />}
      >
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="table-head">
              <tr><Th>Cihaz</Th><Th>Tür</Th><Th>Durum</Th><Th>Zimmetli</Th><Th>Konum</Th><Th>Garanti bitişi</Th><Th className="text-right">Açık talep</Th><Th /></tr>
            </thead>
            <tbody>
              {assets.isLoading ? (
                <tr><td colSpan={cols + 1}><TableSkeleton cols={cols} /></td></tr>
              ) : assets.isError ? (
                <tr><td colSpan={cols + 1}><ErrorState message={errorMessage(assets.error)} details={errorDetails(assets.error)} onRetry={() => assets.refetch()} /></td></tr>
              ) : assets.data!.items.length === 0 ? (
                <tr><td colSpan={cols + 1}>
                  {chips.length ? (
                    <EmptyState title="Bu filtrelere uyan cihaz yok" description="Arama veya filtreleri değiştirerek daha fazla kayıt görebilirsiniz." action={<button className="btn-secondary" onClick={() => setParams({})}>Filtreleri temizle</button>} />
                  ) : (
                    <EmptyState title="Envanter boş" description="Cihazları kaydedin; çalışanlar talep açarken kendi cihazlarını seçebilsin." action={<Link to="/assets/new" className="btn-primary">Cihaz ekle</Link>} />
                  )}
                </td></tr>
              ) : (
                assets.data!.items.map((a) => {
                  const expired = a.warrantyEndDate && new Date(a.warrantyEndDate) < new Date()
                  return (
                    <ClickableRow key={a.id} to={`/assets/${a.id}`}>
                      <td className="px-4 py-3">
                        <Link to={`/assets/${a.id}`} className="font-medium text-gray-800 hover:text-violet-600 dark:text-gray-100">{a.name}</Link>
                        <div className="mt-1 text-xs text-gray-500">{a.inventoryNumber}{a.serialNumber && ` · SN ${a.serialNumber}`}</div>
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap text-gray-700 dark:text-gray-300">{assetTypeLabels[a.type]}</td>
                      <td className="px-4 py-3"><AssetStatusBadge status={a.status} /></td>
                      <td className="px-4 py-3 whitespace-nowrap text-gray-700 dark:text-gray-300">{a.assignedUser?.fullName ?? <span className="text-gray-400">Yok</span>}</td>
                      <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{a.location ?? '—'}</td>
                      <td className="px-4 py-3 whitespace-nowrap text-gray-700 dark:text-gray-300">
                        {formatDate(a.warrantyEndDate)} {expired && <span className="text-xs text-red-700 dark:text-red-400">(doldu)</span>}
                      </td>
                      <td className="px-4 py-3 text-right text-gray-900 tabular-nums dark:text-gray-100">{a.openTicketCount}</td>
                    </ClickableRow>
                  )
                })
              )}
            </tbody>
          </table>
        </div>
      </DataFrame>
    </>
  )
}
