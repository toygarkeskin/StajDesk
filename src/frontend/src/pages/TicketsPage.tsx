import { Link, useSearchParams } from 'react-router'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { MessageSquare, Plus } from 'lucide-react'
import { api, errorDetails, errorMessage } from '@/lib/api'
import { priorityLabels, statusLabels, typeLabels } from '@/lib/labels'
import { formatDate, timeAgo } from '@/lib/format'
import { useAuth, useRole } from '@/stores/auth'
import { CategoryText, EmptyState, ErrorState, PageHeader, Pagination, PriorityText, StatusBadge, TableSkeleton } from '@/components/ui'
import { ClickableRow, DataFrame, FilterCheckbox, FilterSelect, ListToolbar, Th, type FilterChip } from '@/components/list'

/* List / Index kalıbı. Filtre durumu adres çubuğunda tutulur: paylaşılabilir ve geri tuşuyla korunur. */

const PAGE_SIZE = 10
const sortLabels: Record<string, string> = { oldest: 'En eski', priority: 'Önceliğe göre', updated: 'Son güncellenen' }

export function TicketsPage() {
  const [params, setParams] = useSearchParams()
  const { isStaff, can } = useRole()
  const userId = useAuth((s) => s.me?.id)
  const mine = params.get('mine') === '1'
  const get = (k: string) => params.get(k) ?? ''
  const f = {
    status: get('status'), priority: get('priority'), categoryId: get('categoryId'), departmentId: get('departmentId'), type: get('type'), storeId: get('storeId'),
    sort: get('sort'), activeOnly: get('activeOnly'), unassigned: get('unassigned'), search: get('search'),
    page: Number(params.get('page') ?? 1),
  }

  const update = (patch: Record<string, string | number | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) v === null || v === '' ? next.delete(k) : next.set(k, String(v))
    if (!('page' in patch)) next.delete('page')
    setParams(next)
  }
  const clearAll = () => setParams(mine ? { mine: '1' } : {})

  const categories = useQuery({ queryKey: ['categories'], queryFn: () => api.categories.list() })
  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const tickets = useQuery({
    queryKey: ['tickets', f, mine],
    queryFn: () => api.tickets.list({ ...f, assigneeId: mine ? userId : undefined, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  })

  const categoryName = categories.data?.find((c) => String(c.id) === f.categoryId)?.name
  const chips: FilterChip[] = [
    f.search && { key: 'search', label: `Arama: "${f.search}"` },
    f.status && { key: 'status', label: `Durum: ${statusLabels[f.status as keyof typeof statusLabels]}` },
    f.priority && { key: 'priority', label: `Öncelik: ${priorityLabels[f.priority as keyof typeof priorityLabels]}` },
    f.categoryId && { key: 'categoryId', label: `Kategori: ${categoryName ?? f.categoryId}` },
    f.departmentId && { key: 'departmentId', label: `Departman: ${departments.data?.find((d) => String(d.id) === f.departmentId)?.name ?? f.departmentId}` },
    f.type && { key: 'type', label: `Tür: ${typeLabels[f.type as keyof typeof typeLabels]}` },
    f.storeId && { key: 'storeId', label: `Mağaza #${f.storeId}` },
    f.activeOnly && { key: 'activeOnly', label: 'Kapananlar gizli' },
    f.unassigned && { key: 'unassigned', label: 'Yalnızca atanmamış' },
      ].filter(Boolean) as FilterChip[]
  const filtered = chips.length > 0

  const title = mine ? 'Bana atanan talepler' : can('tickets.read.all') ? 'Tüm talepler' : can('tickets.read.store') ? 'Mağaza talepleri' : 'Taleplerim'
  const description = mine ? 'Üzerinizde olan ve sonuçlandırmanız beklenen talepler.' : isStaff ? 'Şirket genelindeki destek talepleri; en yeni talep en üstte.' : 'Açtığınız destek talepleri ve güncel durumları.'
  const colCount = isStaff ? 8 : 7

  return (
    <>
      <PageHeader title={title} description={description} actions={<Link to="/tickets/new" className="btn-primary"><Plus className="size-4" aria-hidden="true" /> Talep oluştur</Link>} />

      <DataFrame
        toolbar={
          <ListToolbar
            searchLabel="Talep ara"
            searchPlaceholder="Başlık, açıklama veya #numara"
            search={f.search}
            onSearch={(v) => update({ search: v })}
            filterCount={chips.filter((c) => c.key !== 'search').length}
            chips={chips}
            onRemoveChip={(k) => update({ [k]: null })}
            onClearAll={clearAll}
            filters={
              <>
                <FilterSelect id="flt-status" label="Durum" allLabel="Tüm durumlar" value={f.status} onChange={(v) => update({ status: v })}
                  options={Object.entries(statusLabels).map(([value, label]) => ({ value, label }))} />
                <FilterSelect id="flt-priority" label="Öncelik" allLabel="Tüm öncelikler" value={f.priority} onChange={(v) => update({ priority: v })}
                  options={Object.entries(priorityLabels).map(([value, label]) => ({ value, label }))} />
                <FilterSelect id="flt-category" label="Kategori" allLabel="Tüm kategoriler" value={f.categoryId} onChange={(v) => update({ categoryId: v })}
                  options={(categories.data ?? []).map((c) => ({ value: String(c.id), label: c.name }))} />
                <FilterSelect id="flt-department" label="Departman" allLabel="Tüm departmanlar" value={f.departmentId} onChange={(v) => update({ departmentId: v })}
                  options={(departments.data ?? []).filter((d) => d.isActive).map((d) => ({ value: String(d.id), label: d.name }))} />
                <div>
                  <label htmlFor="flt-sort" className="sr-only">Sıralama</label>
                  <select id="flt-sort" className="form-field" value={f.sort} onChange={(e) => update({ sort: e.target.value })}>
                    <option value="">En yeni</option>
                    {Object.entries(sortLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                  </select>
                </div>
                <div className="col-span-full flex flex-wrap items-center gap-2">
                  <div className="w-full md:w-48">
                    <FilterSelect id="flt-type" label="Tür" allLabel="Tüm türler" value={f.type} onChange={(v) => update({ type: v })}
                      options={Object.entries(typeLabels).map(([value, label]) => ({ value, label }))} />
                  </div>
                  <FilterCheckbox label="Kapananları gizle" checked={!!f.activeOnly} onChange={(c) => update({ activeOnly: c ? 'true' : null })} />
                  {isStaff && <FilterCheckbox label="Yalnızca atanmamış" checked={!!f.unassigned} onChange={(c) => update({ unassigned: c ? 'true' : null })} />}
                </div>
              </>
            }
          />
        }
        footer={
          <Pagination
            page={tickets.data?.page ?? f.page}
            totalPages={tickets.data?.totalPages ?? 0}
            totalCount={tickets.data?.totalCount ?? 0}
            pageSize={PAGE_SIZE}
            onChange={(p) => update({ page: p })}
          />
        }
      >
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="table-head">
              <tr>
                <Th>Talep</Th><Th>Mağaza</Th><Th>Kategori</Th><Th>Öncelik</Th><Th>Durum</Th>
                {isStaff && <Th>Atanan</Th>}
                <Th className="text-right">Oluşturma</Th><Th />
              </tr>
            </thead>
            <tbody className={tickets.isFetching && !tickets.isLoading ? 'opacity-60' : undefined}>
              {tickets.isLoading ? (
                <tr><td colSpan={colCount}><TableSkeleton cols={colCount - 1} /></td></tr>
              ) : tickets.isError ? (
                <tr><td colSpan={colCount}><ErrorState message={errorMessage(tickets.error)} details={errorDetails(tickets.error)} onRetry={() => tickets.refetch()} /></td></tr>
              ) : tickets.data!.items.length === 0 ? (
                <tr><td colSpan={colCount}>
                  {filtered ? (
                    <EmptyState title="Bu filtrelere uyan talep yok" description="Arama veya filtreleri değiştirerek daha fazla talep görebilirsiniz."
                      action={<button className="btn-secondary" onClick={clearAll}>Filtreleri temizle</button>} />
                  ) : (
                    <EmptyState title="Henüz talep yok" description="Bir sorun yaşadığınızda talep oluşturun; destek ekibi ilgilensin ve durumunu buradan izleyin."
                      action={<Link to="/tickets/new" className="btn-primary">Talep oluştur</Link>} />
                  )}
                </td></tr>
              ) : (
                tickets.data!.items.map((t) => (
                  <ClickableRow key={t.id} to={`/tickets/${t.id}`}>
                    <td className="max-w-md min-w-56 px-4 py-3">
                      <Link to={`/tickets/${t.id}`} className="font-medium text-gray-800 hover:text-violet-600 dark:text-gray-100">{t.title}</Link>
                      <div className="mt-1 flex flex-wrap items-center gap-x-2 text-xs text-gray-500">
                        <span>#{t.id}</span>
                        {isStaff && <span>· {t.requester.fullName}</span>}
                        <span>· {typeLabels[t.type]}</span>
                        {t.commentCount > 0 && <span className="inline-flex items-center gap-1">· <MessageSquare className="size-3" aria-hidden="true" /> {t.commentCount} yorum</span>}
                      </div>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-gray-700 dark:text-gray-300">{t.store?.name ?? <span className="text-gray-400">Merkez</span>}</td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300"><CategoryText name={t.category.name} color={t.category.color} /></td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300"><PriorityText priority={t.priority} /></td>
                    <td className="px-4 py-3"><StatusBadge status={t.status} /></td>
                    {isStaff && <td className="px-4 py-3 whitespace-nowrap text-gray-700 dark:text-gray-300">{t.assignee?.fullName ?? <span className="text-gray-400">Atanmadı</span>}</td>}
                    <td className="px-4 py-3 text-right whitespace-nowrap text-gray-600 dark:text-gray-400"><time dateTime={t.createdAt} title={formatDate(t.createdAt)}>{timeAgo(t.createdAt)}</time></td>
                  </ClickableRow>
                ))
              )}
            </tbody>
          </table>
        </div>
      </DataFrame>
    </>
  )
}
