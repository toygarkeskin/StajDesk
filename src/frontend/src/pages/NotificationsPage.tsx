import { useNavigate, useSearchParams } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Bell } from 'lucide-react'
import { api, errorDetails, errorMessage } from '@/lib/api'
import { formatDateTime, timeAgo } from '@/lib/format'
import { toast } from '@/stores/ui'
import { EmptyState, ErrorState, PageHeader, Pagination, TableSkeleton } from '@/components/ui'
import { DataFrame } from '@/components/list'

/* Inbox (A) kalıbı: zaman sıralı akış, tablo değil. Okunmamış = kalın yazı + soldaki vurgu çizgisi (yalnız renk değil). */

const PAGE_SIZE = 15

export function NotificationsPage() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? 1)
  const unreadOnly = params.get('view') === 'unread'
  const list = useQuery({
    queryKey: ['notifications', 'page', page, unreadOnly],
    queryFn: () => api.notifications.list({ page, pageSize: PAGE_SIZE, unreadOnly: unreadOnly || undefined }),
    placeholderData: keepPreviousData,
    refetchInterval: 15_000,
  })
  const markAll = useMutation({
    mutationFn: api.notifications.markAllRead,
    onSuccess: (n) => {
      qc.invalidateQueries({ queryKey: ['notifications'] })
      toast(n > 0 ? `${n} bildirim okundu olarak işaretlendi.` : 'Okunmamış bildirim yoktu.')
    },
  })

  return (
    <>
      <PageHeader
        title="Bildirimler"
        description="Talepleriniz hakkındaki gelişmeler; en yenisi en üstte. Liste 15 saniyede bir yenilenir."
        actions={<button className="btn-secondary" onClick={() => markAll.mutate()}>Tümünü okundu yap</button>}
      />

      <div role="tablist" aria-label="Bildirim görünümü" className="mb-6 flex gap-2 border-b border-gray-200 dark:border-gray-700/60">
        {([['all', 'Tümü'], ['unread', 'Okunmamış']] as const).map(([key, label]) => {
          const selected = (key === 'unread') === unreadOnly
          return (
            <button key={key} role="tab" aria-selected={selected} onClick={() => setParams(key === 'unread' ? { view: 'unread' } : {})}
              className={clsx('-mb-px min-h-11 border-b-2 px-4 text-sm font-medium', selected ? 'border-violet-500 text-violet-700 dark:text-violet-300' : 'border-transparent text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100')}>
              {label}
            </button>
          )
        })}
      </div>

      <DataFrame footer={<Pagination page={list.data?.page ?? page} totalPages={list.data?.totalPages ?? 0} totalCount={list.data?.totalCount ?? 0} pageSize={PAGE_SIZE} onChange={(p) => setParams({ ...(unreadOnly ? { view: 'unread' } : {}), page: String(p) })} />}>
        {list.isLoading ? (
          <TableSkeleton cols={2} />
        ) : list.isError ? (
          <ErrorState message={errorMessage(list.error)} details={errorDetails(list.error)} onRetry={() => list.refetch()} />
        ) : list.data!.items.length === 0 ? (
          unreadOnly
            ? <EmptyState title="Okunmamış bildirim yok" description="Her şeye göz attınız. Talepleriniz güncellendiğinde yeni bildirimler burada görünür." action={<button className="btn-secondary" onClick={() => setParams({})}>Tüm bildirimleri göster</button>} />
            : <EmptyState title="Henüz bildirim yok" description="Talep açtığınızda, talebiniz atandığında veya durumu değiştiğinde burada haber verilir." />
        ) : (
          <ul>
            {list.data!.items.map((n) => (
              <li key={n.id} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                <button
                  className={clsx('flex w-full items-start gap-4 border-l-2 px-4 py-4 text-left hover:bg-gray-50 dark:hover:bg-gray-700/10', n.isRead ? 'border-transparent' : 'border-violet-500')}
                  onClick={async () => {
                    if (!n.isRead) await api.notifications.markRead(n.id)
                    qc.invalidateQueries({ queryKey: ['notifications'] })
                    if (n.ticketId) navigate(`/tickets/${n.ticketId}`)
                  }}
                >
                  <span className={clsx('mt-1 flex size-9 shrink-0 items-center justify-center rounded-full', n.isRead ? 'bg-gray-100 text-gray-400 dark:bg-gray-700' : 'bg-violet-500/15 text-violet-500')} aria-hidden="true"><Bell className="size-4" /></span>
                  <span className="min-w-0 flex-1">
                    <span className={clsx('block text-sm', n.isRead ? 'text-gray-700 dark:text-gray-300' : 'font-semibold text-gray-900 dark:text-gray-100')}>
                      {!n.isRead && <span className="sr-only">Okunmadı: </span>}{n.title}
                    </span>
                    <span className="mt-1 block text-sm text-gray-600 dark:text-gray-400">{n.message}</span>
                  </span>
                  <time className="shrink-0 text-xs text-gray-500" dateTime={n.createdAt} title={formatDateTime(n.createdAt)}>{timeAgo(n.createdAt)}</time>
                </button>
              </li>
            ))}
          </ul>
        )}
      </DataFrame>
    </>
  )
}
