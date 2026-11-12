import { useMemo, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { CheckCircle2, Plus, UserMinus, UserPlus, XCircle } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { employmentLabels } from '@/lib/labels'
import { formatDate, formatDateTime, timeAgo } from '@/lib/format'
import type { AccountRequestItem, AccountRequestStatus, EmploymentType, Permission, RoleInfo } from '@/lib/types'
import { useAuth, useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { Badge, EmptyState, ErrorState, Field, PageHeader, Pagination, SlideOver, Spinner, TableSkeleton } from '@/components/ui'
import { BulkApproveDialog, type BulkTarget } from '@/components/BulkApproveDialog'

/*
 * Kurumsal hesap süreci (kapalı portal):
 *   İK → "Yeni çalışan hesabı talebi" (accounts.request)
 *   BT / yönetici → inceler, kullanıcı adı + rolleri belirleyip onaylar ya da gerekçeyle reddeder (users.manage)
 *   Çalışan → davet e-postasıyla kendi şifresini belirler.
 * Arayüz yalnızca kolaylık içindir; tüm kurallar API'de de uygulanır.
 */

const PAGE_SIZE = 20

const statusStyle: Record<AccountRequestStatus, { label: string; cls: string }> = {
  Pending: { label: 'Bekliyor', cls: 'bg-amber-500/10 text-amber-700 dark:text-amber-300' },
  Approved: { label: 'Onaylandı', cls: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300' },
  Rejected: { label: 'Reddedildi', cls: 'bg-rose-500/10 text-rose-700 dark:text-rose-300' },
  Cancelled: { label: 'Geri çekildi', cls: 'bg-gray-500/10 text-gray-600 dark:text-gray-400' },
}

const tabs: { key: AccountRequestStatus | ''; label: string }[] = [
  { key: 'Pending', label: 'Bekleyenler' },
  { key: 'Approved', label: 'Onaylananlar' },
  { key: 'Rejected', label: 'Reddedilenler' },
  { key: '', label: 'Tümü' },
]

/** "Ayşe Nur Kaya" → "ayse.kaya" (öneri; BT değiştirebilir). */
function suggestUsername(fullName: string) {
  const map: Record<string, string> = { ç: 'c', ğ: 'g', ı: 'i', i: 'i', ö: 'o', ş: 's', ü: 'u' }
  const parts = fullName.toLocaleLowerCase('tr-TR').split(/\s+/).filter(Boolean)
    .map((p) => [...p].map((ch) => map[ch] ?? ch).join('').replace(/[^a-z0-9]/g, ''))
    .filter(Boolean)
  if (parts.length === 0) return ''
  return parts.length === 1 ? parts[0]! : `${parts[0]}.${parts[parts.length - 1]}`
}

export function AccountRequestsPage() {
  const { can } = useCan()
  const canRequest = can('accounts.request', 'users.manage')
  const [params, setParams] = useSearchParams()
  const status = (params.get('status') ?? 'Pending') as AccountRequestStatus | ''
  const page = Number(params.get('page') ?? 1)
  const [creating, setCreating] = useState(params.get('new') === '1')
  const [selected, setSelected] = useState<AccountRequestItem | null>(null)
  const qc = useQueryClient()
  // Toplu onay yalnızca hesap yöneticisine (users.manage) ve yalnızca "Bekleyenler" sekmesinde açılır; API de aynı izni ister.
  const canBulk = can('users.manage') && status === 'Pending'
  const [checked, setChecked] = useState<Map<number, BulkTarget>>(new Map())
  const [bulkOpen, setBulkOpen] = useState(false)
  const [loadingAll, setLoadingAll] = useState(false)

  const list = useQuery({
    queryKey: ['account-requests', status, page],
    queryFn: () => api.accountRequests.list({ status: status || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  })

  const items = list.data?.items ?? []
  const pageTargets: BulkTarget[] = canBulk ? items.filter((r) => r.status === 'Pending').map((r) => ({ id: r.id, fullName: r.fullName, type: r.type })) : []
  const allOnPage = pageTargets.length > 0 && pageTargets.every((t) => checked.has(t.id))
  const someOnPage = pageTargets.some((t) => checked.has(t.id))
  const toggle = (t: BulkTarget) => setChecked((m) => { const n = new Map(m); if (n.has(t.id)) n.delete(t.id); else n.set(t.id, t); return n })
  const togglePage = () => setChecked((m) => {
    const n = new Map(m)
    if (allOnPage) pageTargets.forEach((t) => n.delete(t.id))
    else pageTargets.forEach((t) => n.set(t.id, t))
    return n
  })
  /** Tüm sayfalardaki bekleyen YENİ HESAP taleplerini seçer (hesap kapatma talepleri bilerek dahil edilmez). */
  const selectAllPendingNew = async () => {
    setLoadingAll(true)
    try {
      const refs = await api.accountRequests.pendingRefs('NewAccount')
      setChecked(new Map(refs.map((r) => [r.id, r])))
    } catch (e) {
      toast(errorMessage(e), 'error')
    } finally {
      setLoadingAll(false)
    }
  }
  const pendingTotal = list.data?.totalCount ?? 0

  return (
    <>
      <PageHeader
        title="Hesap talepleri"
        description={can('users.manage')
          ? 'İnsan Kaynakları\'nın açtığı yeni hesap ve hesap kapatma talepleri. Onayladığınızda hesap açılır ve çalışana ilk şifre bağlantısı gider.'
          : 'Yeni çalışan için portal hesabı veya ayrılan çalışan için hesap kapatma talebi açın. Talepleri Bilgi Teknolojileri onaylar.'}
        actions={canRequest && <button className="btn-violet" onClick={() => setCreating(true)}><Plus className="size-4" aria-hidden="true" /> Yeni çalışan hesabı talebi</button>}
      />
      <div role="tablist" aria-label="Talep durumu" className="mb-4 flex gap-1 overflow-x-auto border-b border-gray-200 dark:border-gray-700/60">
        {tabs.map((t) => (
          <button key={t.key || 'all'} role="tab" aria-selected={status === t.key}
            onClick={() => { setChecked(new Map()); setParams(t.key === 'Pending' ? {} : { status: t.key }) }}
            className={clsx('min-h-11 shrink-0 border-b-2 px-3 text-sm font-medium', status === t.key ? 'border-violet-500 text-violet-600 dark:text-violet-300' : 'border-transparent text-gray-500 hover:text-gray-800 dark:hover:text-gray-200')}>
            {t.label}
          </button>
        ))}
      </div>

      {canBulk && items.length > 0 && (
        <div className="card mb-3 flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2" aria-label="Toplu işlemler">
          <label className="flex min-h-11 cursor-pointer items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-200">
            <input type="checkbox" className="size-4 rounded accent-violet-500" checked={allOnPage}
              ref={(el) => { if (el) el.indeterminate = someOnPage && !allOnPage }} onChange={togglePage} />
            Tümünü Seç <span className="font-normal text-gray-500">(bu sayfa)</span>
          </label>
          {pendingTotal > pageTargets.length && (
            <button type="button" className="inline-flex min-h-11 items-center gap-1.5 text-sm font-medium text-violet-600 hover:text-violet-700 dark:text-violet-300" disabled={loadingAll} onClick={selectAllPendingNew}>
              {loadingAll && <Spinner />}Tüm sayfalardaki bekleyen yeni hesap taleplerini seç
            </button>
          )}
          <span className="ml-auto flex flex-wrap items-center gap-2">
            {checked.size > 0 && (
              <>
                <span className="text-sm text-gray-600 tabular-nums dark:text-gray-300">{checked.size} talep seçildi</span>
                <button type="button" className="btn-secondary" onClick={() => setChecked(new Map())}>Seçimi temizle</button>
                <button type="button" className="btn-violet" onClick={() => setBulkOpen(true)}><CheckCircle2 className="size-4" aria-hidden="true" /> Seçilenleri Onayla</button>
              </>
            )}
          </span>
        </div>
      )}

      <section className="card overflow-hidden">
        {list.isLoading ? <TableSkeleton cols={4} /> : list.isError ? (
          <ErrorState message={errorMessage(list.error)} details={errorDetails(list.error)} onRetry={() => list.refetch()} />
        ) : list.data!.items.length === 0 ? (
          <EmptyState title={status === 'Pending' ? 'Bekleyen talep yok' : 'Talep bulunamadı'}
            description={canRequest ? 'Yeni bir çalışan geldiğinde buradan hesap talebi açın.' : 'Yeni talepler geldiğinde burada listelenir ve size bildirim gelir.'} />
        ) : (
          <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
            {list.data!.items.map((r) => (
              <li key={r.id} className={clsx('flex items-center', checked.has(r.id) && 'bg-violet-500/5')}>
                {canBulk && r.status === 'Pending' && (
                  <label className="flex min-h-11 shrink-0 cursor-pointer items-center self-stretch pr-1 pl-4">
                    <span className="sr-only">{r.fullName} talebini seç</span>
                    <input type="checkbox" className="size-4 rounded accent-violet-500" checked={checked.has(r.id)}
                      onChange={() => toggle({ id: r.id, fullName: r.fullName, type: r.type })} />
                  </label>
                )}
                <button className="flex min-w-0 flex-1 flex-wrap items-center gap-3 px-4 py-3 text-left hover:bg-gray-50 dark:hover:bg-gray-700/20" onClick={() => setSelected(r)}>
                  <span className={clsx('flex size-9 shrink-0 items-center justify-center rounded-full', r.type === 'NewAccount' ? 'bg-violet-500/10 text-violet-600' : 'bg-rose-500/10 text-rose-600')} aria-hidden="true">
                    {r.type === 'NewAccount' ? <UserPlus className="size-4" /> : <UserMinus className="size-4" />}
                  </span>
                  <span className="min-w-0 flex-1 text-sm">
                    <span className="block truncate font-medium text-gray-800 dark:text-gray-100">
                      {r.type === 'NewAccount' ? 'Yeni hesap' : 'Hesap kapatma'} · {r.fullName}
                    </span>
                    <span className="block truncate text-xs text-gray-500">
                      {r.type === 'NewAccount'
                        ? [r.employeeNumber, r.position?.name, r.department?.name].filter(Boolean).join(' · ')
                        : `Ayrılış: ${r.terminationDate ? formatDate(r.terminationDate) : '—'}`}
                      {' · '}{r.requestedBy.fullName}, {timeAgo(r.createdAt)}
                    </span>
                  </span>
                  <Badge className={statusStyle[r.status].cls}>{statusStyle[r.status].label}</Badge>
                </button>
              </li>
            ))}
          </ul>
        )}
        {list.data && list.data.totalCount > PAGE_SIZE && (
          <Pagination page={list.data.page} totalPages={list.data.totalPages} totalCount={list.data.totalCount} pageSize={PAGE_SIZE}
            onChange={(p) => setParams({ ...(status !== 'Pending' ? { status } : {}), page: String(p) })} />
        )}
      </section>

      {creating && <NewAccountRequestPanel onClose={() => { setCreating(false); if (params.get('new')) setParams({}) }} />}
      {selected && <RequestDetailPanel request={selected} onClose={() => setSelected(null)} />}
      {bulkOpen && (
        <BulkApproveDialog
          targets={[...checked.values()]}
          onClose={() => setBulkOpen(false)}
          onProgress={(ids) => {
            // Onaylananlar seçimden ve bekleyenler listesinden anında düşer.
            setChecked((m) => { const n = new Map(m); ids.forEach((id) => n.delete(id)); return n })
            qc.invalidateQueries({ queryKey: ['account-requests'] })
          }}
          onDone={(r) => {
            qc.invalidateQueries({ queryKey: ['account-requests'] })
            qc.invalidateQueries({ queryKey: ['pending-requests'] })
            qc.invalidateQueries({ queryKey: ['users'] })
            toast(r.failed.length ? `${r.approved.length} talep başarıyla onaylandı, ${r.failed.length} talep onaylanamadı.` : `${r.approved.length} talep başarıyla onaylandı.`, r.approved.length ? 'success' : 'error')
          }}
        />
      )}
    </>
  )
}

/* ---------------- İK: yeni çalışan hesabı talebi ---------------- */

function NewAccountRequestPanel({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const positions = useQuery({ queryKey: ['positions'], queryFn: api.org.positions })
  const stores = useQuery({ queryKey: ['stores'], queryFn: api.stores.list })
  const chart = useQuery({ queryKey: ['org-chart'], queryFn: api.org.chart })
  const form = useForm(
    { fullName: '', email: '', employeeNumber: '', departmentId: '', positionId: '', employmentType: 'FullTime', hireDate: '', managerId: '', storeId: '', phone: '', note: '' },
    {
      fullName: (v) => (!v.trim() ? 'Çalışanın adını ve soyadını girin.' : undefined),
      email: (v) => (!/^\S+@\S+\.\S+$/.test(v) ? 'Kurumsal e-postayı ad@sirket.com biçiminde girin.' : undefined),
      employeeNumber: (v) => (!v.trim() ? 'Personel kodunu girin.' : undefined),
      departmentId: (v) => (!v ? 'Departman seçin.' : undefined),
      positionId: (v) => (!v ? 'Unvan seçin.' : undefined),
      hireDate: (v) => (!v ? 'İşe başlama tarihini girin.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: () => {
      const v = form.values
      const n = (x: string) => (x ? Number(x) : null)
      return api.accountRequests.createNewAccount({
        fullName: v.fullName, email: v.email, employeeNumber: v.employeeNumber, departmentId: n(v.departmentId), positionId: n(v.positionId),
        employmentType: v.employmentType as EmploymentType, hireDate: v.hireDate, managerId: n(v.managerId), storeId: n(v.storeId),
        phone: v.phone || null, note: v.note || null,
      })
    },
    onSuccess: () => {
      toast('Talep Bilgi Teknolojileri\'ne iletildi. Onaylandığında çalışana ilk şifre bağlantısı gider.')
      qc.invalidateQueries({ queryKey: ['account-requests'] })
      onClose()
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()
  return (
    <SlideOver open onClose={onClose} title="Yeni çalışan hesabı talebi"
      description="Bu bir taleptir: hesap, Bilgi Teknolojileri onayladığında açılır. Departman ve unvan personel bilgisidir; sistem yetkilerini BT belirler."
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} Talebi gönder</button></>}>
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-4">
        <Field label="Ad soyad" htmlFor="f-fullName" error={form.errors.fullName} required><input {...form.field('fullName')} className="form-field" maxLength={100} autoComplete="off" /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Personel kodu" htmlFor="f-employeeNumber" error={form.errors.employeeNumber} required><input {...form.field('employeeNumber')} className="form-field" maxLength={20} autoComplete="off" /></Field>
          <Field label="Kurumsal e-posta" htmlFor="f-email" error={form.errors.email} required><input {...form.field('email')} type="email" className="form-field" autoComplete="off" /></Field>
          <Field label="Departman" htmlFor="f-departmentId" error={form.errors.departmentId} required>
            <select {...form.field('departmentId')} className="form-field"><option value="">Seçin</option>{departments.data?.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>
          </Field>
          <Field label="Unvan" htmlFor="f-positionId" error={form.errors.positionId} required>
            <select {...form.field('positionId')} className="form-field"><option value="">Seçin</option>{positions.data?.map((p) => <option key={p.id} value={p.id}>{p.title}</option>)}</select>
          </Field>
          <Field label="Personel türü" htmlFor="f-employmentType" required>
            <select {...form.field('employmentType')} className="form-field">{Object.entries(employmentLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>
          </Field>
          <Field label="İşe başlama tarihi" htmlFor="f-hireDate" error={form.errors.hireDate} required><input type="date" {...form.field('hireDate')} className="form-field" /></Field>
          <Field label="Yöneticisi" htmlFor="f-managerId" optional>
            <select {...form.field('managerId')} className="form-field"><option value="">Seçilmedi</option>{chart.data?.people.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}</select>
          </Field>
          <Field label="Mağaza / şube" htmlFor="f-storeId" optional>
            <select {...form.field('storeId')} className="form-field"><option value="">Merkez</option>{stores.data?.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
          </Field>
          <Field label="İş telefonu" htmlFor="f-phone" optional><input {...form.field('phone')} type="tel" className="form-field" /></Field>
        </div>
        <Field label="BT'ye not" htmlFor="f-note" optional hint="Ör. hangi uygulamalara erişmesi gerektiği."><textarea {...form.field('note')} className="form-field min-h-20" maxLength={1000} /></Field>
      </form>
    </SlideOver>
  )
}

/* ---------------- Talep ayrıntısı: BT onayı / reddi, talep sahibinin geri çekmesi ---------------- */

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid grid-cols-1 gap-0.5 py-2 text-sm sm:grid-cols-[10rem_1fr]">
      <dt className="text-gray-500">{label}</dt>
      <dd className="font-medium text-gray-800 dark:text-gray-100">{children}</dd>
    </div>
  )
}

/** Bir rolü atayabilmek için rolün tüm izinlerine sahip olmak gerekir (API ile aynı kural). */
function assignable(role: RoleInfo, mine: Permission[]) {
  return role.permissions.every((p) => mine.includes(p))
}

function RequestDetailPanel({ request: r, onClose }: { request: AccountRequestItem; onClose: () => void }) {
  const qc = useQueryClient()
  const { can } = useCan()
  const me = useAuth((s) => s.me)
  const canReview = can('users.manage') && r.status === 'Pending'
  const ownRequest = r.requestedBy.id === me?.id
  const roles = useQuery({ queryKey: ['roles'], queryFn: api.roles.list, enabled: canReview && r.type === 'NewAccount' })
  const [username, setUsername] = useState(suggestUsername(r.fullName))
  const [selectedRoles, setSelectedRoles] = useState<string[]>(['user'])
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState<string>()

  const assignableRoles = useMemo(() => (roles.data?.roles ?? []).map((role) => ({ role, ok: assignable(role, me?.permissions ?? []) })), [roles.data, me])
  const done = (msg: string) => {
    toast(msg)
    qc.invalidateQueries({ queryKey: ['account-requests'] })
    qc.invalidateQueries({ queryKey: ['account-requests-pending'] })
    qc.invalidateQueries({ queryKey: ['users'] })
    qc.invalidateQueries({ queryKey: ['employees'] })
    onClose()
  }
  const onError = (err: unknown) => toast(errorMessage(err), 'error')
  const approve = useMutation({
    mutationFn: () => api.accountRequests.approve(r.id, r.type === 'NewAccount' ? { username: username || null, roles: selectedRoles } : {}),
    onSuccess: () => done(r.type === 'NewAccount' ? `${r.fullName} için hesap açıldı; ilk şifre bağlantısı e-postayla gönderildi.` : `${r.fullName} hesabı kapatıldı.`),
    onError,
  })
  const reject = useMutation({ mutationFn: () => api.accountRequests.reject(r.id, reason), onSuccess: () => done('Talep reddedildi; talep sahibine bildirildi.'), onError })
  const cancel = useMutation({ mutationFn: () => api.accountRequests.cancel(r.id), onSuccess: () => done('Talep geri çekildi.'), onError })

  const footer = r.status !== 'Pending' ? <button className="btn-secondary" onClick={onClose}>Kapat</button>
    : canReview ? (rejecting ? (
      <>
        <button className="btn-secondary" onClick={() => setRejecting(false)}>Vazgeç</button>
        <button className="btn-danger" aria-busy={reject.isPending} onClick={() => { if (!reason.trim()) { setReasonError('Gerekçe yazın; talep sahibine iletilir.'); return } reject.mutate() }}>
          {reject.isPending && <Spinner />} Talebi reddet
        </button>
      </>
    ) : (
      <>
        <button className="btn-secondary" onClick={() => setRejecting(true)}><XCircle className="size-4" aria-hidden="true" /> Reddet</button>
        <button className="btn-violet" aria-busy={approve.isPending} disabled={ownRequest && !can('roles.manage')} onClick={() => approve.mutate()}>
          {approve.isPending && <Spinner />} <CheckCircle2 className="size-4" aria-hidden="true" /> {r.type === 'NewAccount' ? 'Onayla ve hesabı aç' : 'Onayla ve hesabı kapat'}
        </button>
      </>
    )) : ownRequest ? (
      <>
        <button className="btn-secondary" onClick={onClose}>Kapat</button>
        <button className="btn-secondary text-rose-600" aria-busy={cancel.isPending} onClick={() => cancel.mutate()}>Talebi geri çek</button>
      </>
    ) : <button className="btn-secondary" onClick={onClose}>Kapat</button>

  return (
    <SlideOver open onClose={onClose} title={`${r.type === 'NewAccount' ? 'Yeni hesap' : 'Hesap kapatma'} talebi — ${r.fullName}`}
      description={`${r.requestedBy.fullName} · ${formatDateTime(r.createdAt)}`} footer={footer}>
      <div className="space-y-5">
        <div className="flex items-center gap-2"><Badge className={statusStyle[r.status].cls}>{statusStyle[r.status].label}</Badge>
          {r.reviewedBy && <span className="text-xs text-gray-500">{r.reviewedBy.fullName} · {r.reviewedAt && formatDateTime(r.reviewedAt)}</span>}</div>
        <dl className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {r.type === 'NewAccount' ? (
            <>
              <Row label="Ad soyad">{r.fullName}</Row>
              <Row label="Personel kodu">{r.employeeNumber ?? '—'}</Row>
              <Row label="Kurumsal e-posta">{r.email}</Row>
              <Row label="Departman">{r.department?.name ?? '—'}</Row>
              <Row label="Unvan">{r.position?.name ?? '—'}</Row>
              <Row label="Personel türü">{employmentLabels[r.employmentType]}</Row>
              <Row label="İşe başlama">{r.hireDate ? formatDate(r.hireDate) : '—'}</Row>
              <Row label="Yöneticisi">{r.manager?.fullName ?? '—'}</Row>
              <Row label="Mağaza / şube">{r.store?.name ?? 'Merkez'}</Row>
              {r.phone && <Row label="Telefon">{r.phone}</Row>}
            </>
          ) : (
            <>
              <Row label="Çalışan">{r.targetUser ? `${r.targetUser.fullName} (${r.targetUser.email})` : r.fullName}</Row>
              <Row label="Ayrılış tarihi">{r.terminationDate ? formatDate(r.terminationDate) : '—'}</Row>
            </>
          )}
          {r.note && <Row label="Not">{r.note}</Row>}
          {r.reviewNote && <Row label={r.status === 'Rejected' ? 'Reddetme gerekçesi' : 'BT notu'}>{r.reviewNote}</Row>}
        </dl>

        {canReview && ownRequest && !can('roles.manage') && (
          <p role="status" className="rounded-lg bg-amber-500/10 px-3 py-2 text-sm text-amber-800 dark:text-amber-300">Görevler ayrılığı: kendi açtığınız talebi başka bir BT yetkilisi onaylamalıdır.</p>
        )}

        {canReview && !rejecting && r.type === 'NewAccount' && (
          <section className="space-y-3 rounded-lg border border-gray-200 p-4 dark:border-gray-700/60" aria-labelledby="approve-title">
            <h3 id="approve-title" className="text-sm font-semibold">Açılacak hesap</h3>
            <Field label="Kullanıcı adı" htmlFor="f-username" optional hint="Çalışan e-posta veya bu adla giriş yapabilir.">
              <input id="f-username" className="form-field" value={username} onChange={(e) => setUsername(e.target.value)} autoCapitalize="none" />
            </Field>
            <fieldset>
              <legend className="form-label">Sistem rolleri</legend>
              {roles.isLoading ? <Spinner /> : (
                <div className="space-y-1">
                  {assignableRoles.map(({ role, ok }) => (
                    <label key={role.id} className={clsx('flex min-h-11 items-start gap-2 text-sm', ok ? 'text-gray-700 dark:text-gray-300' : 'text-gray-400')}>
                      <input type="checkbox" className="mt-0.5 size-4 accent-violet-500" disabled={!ok} checked={selectedRoles.includes(role.name)}
                        onChange={(e) => setSelectedRoles(e.target.checked ? [...selectedRoles, role.name] : selectedRoles.filter((x) => x !== role.name))} />
                      <span>{role.displayName}{!ok && <span className="block text-xs">Sizde olmayan yetkiler içeriyor; bu rolü bir sistem yöneticisi verebilir.</span>}</span>
                    </label>
                  ))}
                </div>
              )}
            </fieldset>
            <p className="text-xs text-gray-500">Şifre belirlenmez: çalışana 72 saat geçerli "ilk şifreni belirle" bağlantısı gider.</p>
          </section>
        )}

        {canReview && rejecting && (
          <Field label="Reddetme gerekçesi" htmlFor="f-reason" error={reasonError} required>
            <textarea id="f-reason" className="form-field min-h-24" value={reason} maxLength={1000} onChange={(e) => { setReason(e.target.value); setReasonError(undefined) }} />
          </Field>
        )}
      </div>
    </SlideOver>
  )
}
