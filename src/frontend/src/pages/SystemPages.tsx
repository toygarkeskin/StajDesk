import { useSearchParams } from 'react-router'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ExternalLink } from 'lucide-react'
import { api, errorDetails, errorMessage } from '@/lib/api'
import { formatDateTime } from '@/lib/format'
import { Badge, EmptyState, ErrorState, PageHeader, Pagination, TableSkeleton } from '@/components/ui'
import { DataFrame, ListToolbar, Th } from '@/components/list'

const PAGE_SIZE = 20

function usePagedParams() {
  const [params, setParams] = useSearchParams()
  const search = params.get('search') ?? ''
  const page = Number(params.get('page') ?? 1)
  const update = (patch: Record<string, string | number | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) v === null || v === '' ? next.delete(k) : next.set(k, String(v))
    if (!('page' in patch)) next.delete('page')
    setParams(next)
  }
  return { search, page, update, clear: () => setParams({}) }
}

/** Denetim kayıtları: giriş denemeleri, rol değişiklikleri, silmeler… (kim, ne zaman, hangi IP'den). */
export function AuditPage() {
  const { search, page, update, clear } = usePagedParams()
  const logs = useQuery({ queryKey: ['audit', search, page], queryFn: () => api.system.audit({ search, page, pageSize: PAGE_SIZE }), placeholderData: keepPreviousData })
  const tone = (a: string) => (a.includes('failed') || a.includes('reuse') || a.includes('deleted') ? 'bg-rose-500/15 text-rose-700 dark:text-rose-400' : a.startsWith('auth.') ? 'bg-sky-500/15 text-sky-700 dark:text-sky-400' : 'bg-gray-400/15 text-gray-600 dark:text-gray-400')

  return (
    <>
      <PageHeader title="Denetim Kayıtları" description="Güvenlik açısından önemli işlemlerin kaydı; en yenisi en üstte." />
      <DataFrame
        toolbar={<ListToolbar searchLabel="İşlem ara" searchPlaceholder="auth.login_failed, role.permissions_changed…" search={search} onSearch={(v) => update({ search: v })}
          chips={search ? [{ key: 'search', label: `Arama: "${search}"` }] : []} onRemoveChip={() => update({ search: null })} onClearAll={clear} />}
        footer={<Pagination page={logs.data?.page ?? page} totalPages={logs.data?.totalPages ?? 0} totalCount={logs.data?.totalCount ?? 0} pageSize={PAGE_SIZE} onChange={(p) => update({ page: p })} />}
      >
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="table-head"><tr><Th>Zaman</Th><Th>İşlem</Th><Th>Kayıt</Th><Th>Kişi</Th><Th>IP</Th><Th>Ayrıntı</Th></tr></thead>
            <tbody>
              {logs.isLoading ? <tr><td colSpan={6}><TableSkeleton cols={6} /></td></tr>
                : logs.isError ? <tr><td colSpan={6}><ErrorState message={errorMessage(logs.error)} details={errorDetails(logs.error)} onRetry={() => logs.refetch()} /></td></tr>
                : logs.data!.items.length === 0 ? <tr><td colSpan={6}><EmptyState title="Kayıt bulunamadı" description="Aramayı değiştirerek tekrar deneyin." /></td></tr>
                : logs.data!.items.map((l) => (
                  <tr key={l.id} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                    <td className="px-4 py-3 whitespace-nowrap text-gray-500">{formatDateTime(l.createdAt)}</td>
                    <td className="px-4 py-3"><Badge className={tone(l.action)}>{l.action}</Badge></td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{l.entity}{l.entityId && ` #${l.entityId.slice(0, 12)}`}</td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{l.actorName ?? '—'}</td>
                    <td className="px-4 py-3 text-gray-500">{l.ip ?? '—'}</td>
                    <td className="max-w-xs truncate px-4 py-3 font-mono text-xs text-gray-500" title={l.data ?? undefined}>{l.data ?? ''}</td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </DataFrame>
    </>
  )
}

/** Demo e-posta kutusu: Worker'ın gönderdiği tüm e-postalar. İçerikleri Mailpit'te görülür. */
export function EmailsPage() {
  const { search, page, update, clear } = usePagedParams()
  const mails = useQuery({ queryKey: ['emails', search, page], queryFn: () => api.system.emails({ search, page, pageSize: PAGE_SIZE }), placeholderData: keepPreviousData, refetchInterval: 10_000 })

  return (
    <>
      <PageHeader
        title="Gönderilen E-postalar"
        description="Demo ortamında e-postalar gerçekten SMTP ile gönderilir ama dışarı çıkmaz; içeriklerini Mailpit'te açabilirsiniz."
        actions={<a href="http://localhost:8025" target="_blank" rel="noreferrer" className="btn-primary"><ExternalLink className="size-4" aria-hidden="true" /> Mailpit posta kutusunu aç</a>}
      />
      <DataFrame
        toolbar={<ListToolbar searchLabel="E-posta ara" searchPlaceholder="Alıcı veya konu" search={search} onSearch={(v) => update({ search: v })}
          chips={search ? [{ key: 'search', label: `Arama: "${search}"` }] : []} onRemoveChip={() => update({ search: null })} onClearAll={clear} />}
        footer={<Pagination page={mails.data?.page ?? page} totalPages={mails.data?.totalPages ?? 0} totalCount={mails.data?.totalCount ?? 0} pageSize={PAGE_SIZE} onChange={(p) => update({ page: p })} />}
      >
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="table-head"><tr><Th>Zaman</Th><Th>Alıcı</Th><Th>Konu</Th><Th>Şablon</Th><Th>Durum</Th></tr></thead>
            <tbody>
              {mails.isLoading ? <tr><td colSpan={5}><TableSkeleton cols={5} /></td></tr>
                : mails.isError ? <tr><td colSpan={5}><ErrorState message={errorMessage(mails.error)} details={errorDetails(mails.error)} onRetry={() => mails.refetch()} /></td></tr>
                : mails.data!.items.length === 0 ? <tr><td colSpan={5}><EmptyState title="Henüz e-posta gönderilmedi" description="Talep açıldığında, durumu değiştiğinde veya şifre sıfırlama istendiğinde e-postalar burada listelenir." /></td></tr>
                : mails.data!.items.map((m) => (
                  <tr key={m.id} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                    <td className="px-4 py-3 whitespace-nowrap text-gray-500">{formatDateTime(m.createdAt)}</td>
                    <td className="px-4 py-3 text-gray-800 dark:text-gray-100">{m.toAddress}</td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{m.subject}</td>
                    <td className="px-4 py-3"><code className="text-xs text-gray-500">{m.template}</code></td>
                    <td className="px-4 py-3">
                      {m.status === 'sent'
                        ? <Badge className="bg-emerald-500/15 text-emerald-700 dark:text-emerald-400">Gönderildi</Badge>
                        : <Badge className="bg-rose-500/15 text-rose-700 dark:text-rose-400">Gönderilemedi</Badge>}
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </DataFrame>
    </>
  )
}
