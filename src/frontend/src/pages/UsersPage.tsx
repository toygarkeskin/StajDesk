import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Lock, LockOpen, Pencil, Plus, Send } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { roleLabels } from '@/lib/labels'
import { formatDate, timeAgo } from '@/lib/format'
import type { UserListItem } from '@/lib/types'
import { useAuth, useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { Avatar, Badge, EmptyState, ErrorState, Field, IconButton, PageHeader, Pagination, SlideOver, Spinner, TableSkeleton } from '@/components/ui'
import { DataFrame, FilterSelect, ListToolbar, Th, type FilterChip } from '@/components/list'

/* Kullanıcı hesapları: BT / yönetici hesap açar, rol verir, pasife alır ve kilitler (users.manage).
   Mağaza sahibi kendi mağazasındaki kullanıcıları yalnızca görür. Şifreyi hiçbir zaman başkası belirlemez. */

const PAGE_SIZE = 10
const roleBadge: Record<string, string> = {
  system_admin: 'bg-violet-500/15 text-violet-700 dark:text-violet-400',
  agent: 'bg-sky-500/15 text-sky-700 dark:text-sky-400',
  store_owner: 'bg-amber-500/15 text-amber-700 dark:text-amber-400',
  user: 'bg-gray-400/15 text-gray-600 dark:text-gray-400',
  hr: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400',
  it_admin: 'bg-indigo-500/15 text-indigo-700 dark:text-indigo-400',
}

export function UsersPage() {
  const { can } = useCan()
  const canManage = can('users.manage')
  const [params, setParams] = useSearchParams()
  const get = (k: string) => params.get(k) ?? ''
  const f = { role: get('role'), storeId: get('storeId'), isActive: get('isActive'), search: get('search'), page: Number(params.get('page') ?? 1) }
  const update = (patch: Record<string, string | number | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) v === null || v === '' ? next.delete(k) : next.set(k, String(v))
    if (!('page' in patch)) next.delete('page')
    setParams(next)
  }
  const [editing, setEditing] = useState<UserListItem | 'new' | null>(null)
  const users = useQuery({ queryKey: ['users', f], queryFn: () => api.users.list({ ...f, pageSize: PAGE_SIZE }), placeholderData: keepPreviousData })
  const stores = useQuery({ queryKey: ['stores'], queryFn: api.stores.list })

  const storeName = stores.data?.find((s) => String(s.id) === f.storeId)?.name
  const chips = [
    f.search && { key: 'search', label: `Arama: "${f.search}"` },
    f.role && { key: 'role', label: `Rol: ${roleLabels[f.role] ?? f.role}` },
    f.storeId && { key: 'storeId', label: `Mağaza: ${storeName ?? f.storeId}` },
    f.isActive && { key: 'isActive', label: f.isActive === 'true' ? 'Yalnızca aktif' : 'Yalnızca pasif' },
  ].filter(Boolean) as FilterChip[]
  const cols = 7

  return (
    <>
      <PageHeader
        title="Kullanıcılar"
        description={canManage ? 'Hesaplar, roller, mağaza ataması ve erişim durumu.' : 'Mağazanızdaki kullanıcılar.'}
        actions={canManage && <button className="btn-primary" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Kullanıcı ekle</button>}
      />
      <DataFrame
        toolbar={
          <ListToolbar
            searchLabel="Kullanıcı ara" searchPlaceholder="Ad, e-posta veya departman" search={f.search} onSearch={(v) => update({ search: v })}
            chips={chips} onRemoveChip={(k) => update({ [k]: null })} onClearAll={() => setParams({})}
            filters={
              <>
                <FilterSelect id="flt-role" label="Rol" allLabel="Tüm roller" value={f.role} onChange={(v) => update({ role: v })} options={Object.entries(roleLabels).map(([value, label]) => ({ value, label }))} />
                <FilterSelect id="flt-store" label="Mağaza" allLabel="Tüm mağazalar" value={f.storeId} onChange={(v) => update({ storeId: v })} options={(stores.data ?? []).map((s) => ({ value: String(s.id), label: s.name }))} />
                <FilterSelect id="flt-active" label="Hesap durumu" allLabel="Aktif ve pasif" value={f.isActive} onChange={(v) => update({ isActive: v })} options={[{ value: 'true', label: 'Aktif' }, { value: 'false', label: 'Pasif' }]} />
              </>
            }
          />
        }
        footer={<Pagination page={users.data?.page ?? f.page} totalPages={users.data?.totalPages ?? 0} totalCount={users.data?.totalCount ?? 0} pageSize={PAGE_SIZE} onChange={(p) => update({ page: p })} />}
      >
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="table-head"><tr><Th>Kullanıcı</Th><Th>Roller</Th><Th>Mağaza</Th><Th>Durum</Th><Th className="text-right">Açık talep</Th><Th>Son giriş</Th><Th><span className="sr-only">İşlemler</span></Th></tr></thead>
            <tbody>
              {users.isLoading ? (
                <tr><td colSpan={cols}><TableSkeleton cols={cols - 1} /></td></tr>
              ) : users.isError ? (
                <tr><td colSpan={cols}><ErrorState message={errorMessage(users.error)} details={errorDetails(users.error)} onRetry={() => users.refetch()} /></td></tr>
              ) : users.data!.items.length === 0 ? (
                <tr><td colSpan={cols}>
                  {chips.length
                    ? <EmptyState title="Bu filtrelere uyan kullanıcı yok" description="Arama veya filtreleri değiştirerek tekrar bakın." action={<button className="btn-secondary" onClick={() => setParams({})}>Filtreleri temizle</button>} />
                    : <EmptyState title="Henüz kullanıcı yok" description="Destek personeli, mağaza sahibi veya çalışan hesabı ekleyin." action={canManage && <button className="btn-violet" onClick={() => setEditing('new')}>Kullanıcı ekle</button>} />}
                </td></tr>
              ) : (
                users.data!.items.map((u) => (
                  <tr key={u.id} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <Avatar name={u.fullName} />
                        <div className="min-w-0">
                          <div className="font-medium text-gray-800 dark:text-gray-100">{u.fullName}</div>
                          <div className="text-xs text-gray-500">{u.email}</div>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3"><div className="flex flex-wrap gap-1">{u.roles.map((r) => <Badge key={r} className={roleBadge[r] ?? roleBadge.user!}>{roleLabels[r] ?? r}</Badge>)}</div></td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{u.store?.name ?? '—'}</td>
                    <td className="px-4 py-3">
                      {!u.isActive ? <span className="text-rose-500">Pasif</span>
                        : u.lockedAt ? <span className="inline-flex items-center gap-1 text-amber-600" title={u.lockReason ?? undefined}><Lock className="size-3.5" aria-hidden="true" /> Kilitli</span>
                        : u.passwordSet === false ? <span className="inline-flex items-center gap-1 text-sky-600"><KeyRound className="size-3.5" aria-hidden="true" /> Şifre bekleniyor</span>
                        : 'Aktif'}
                    </td>
                    <td className="px-4 py-3 text-right">{u.openTicketCount}</td>
                    <td className="px-4 py-3 whitespace-nowrap text-gray-500" title={`Kayıt: ${formatDate(u.createdAt)}`}>{u.lastLoginAt ? timeAgo(u.lastLoginAt) : 'Hiç giriş yapmadı'}</td>
                    <td className="px-2 py-1 text-right">{canManage && <IconButton label={`${u.fullName} hesabını düzenle`} onClick={() => setEditing(u)}><Pencil className="size-4" /></IconButton>}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </DataFrame>
      {editing && <UserPanel user={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
    </>
  )
}

function UserPanel({ user, onClose }: { user?: UserListItem; onClose: () => void }) {
  const qc = useQueryClient()
  const meId = useAuth((s) => s.me?.id)
  const isSelf = user?.id === meId
  const stores = useQuery({ queryKey: ['stores'], queryFn: api.stores.list })
  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const roleList = useQuery({ queryKey: ['roles'], queryFn: api.roles.list })
  const myPermissions = useAuth((s) => s.me?.permissions ?? [])
  // Bir rolü verebilmek için rolün tüm yetkilerine sahip olmak gerekir (yetki yükseltme koruması; API de aynı kuralı uygular).
  const canAssign = (name: string) => {
    const role = roleList.data?.roles.find((r) => r.name === name)
    return !role || role.permissions.every((p) => myPermissions.includes(p))
  }
  const [roles, setRoles] = useState<string[]>(user?.roles ?? ['user'])
  const [rolesError, setRolesError] = useState<string>()
  const form = useForm(
    { fullName: user?.fullName ?? '', email: user?.email ?? '', username: user?.username ?? '', storeId: user?.store ? String(user.store.id) : '', departmentId: user?.departmentId ? String(user.departmentId) : '', isActive: user?.isActive ?? true },
    {
      fullName: (v) => (!v.trim() ? 'Ad ve soyadı girin.' : undefined),
      email: (v) => (user ? undefined : !/^\S+@\S+\.\S+$/.test(v) ? 'ad@sirket.com biçiminde bir e-posta girin.' : undefined),
      username: (v) => (v && !/^[a-zA-Z0-9._-]{3,50}$/.test(v) ? '3–50 karakter: harf, rakam, nokta, tire, alt çizgi.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: () => {
      const v = form.values
      const common = { fullName: v.fullName, roles, storeId: v.storeId ? Number(v.storeId) : null, departmentId: v.departmentId ? Number(v.departmentId) : null, phone: null, username: v.username || null }
      return user ? api.users.update(user.id, { ...common, isActive: v.isActive }) : api.users.create({ ...common, email: v.email })
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] })
      qc.invalidateQueries({ queryKey: ['agents'] })
      qc.invalidateQueries({ queryKey: ['stores'] })
      toast(user ? `${form.values.fullName} güncellendi.` : `${form.values.fullName} hesabı oluşturuldu; ilk şifre bağlantısı e-postayla gönderildi.`)
      onClose()
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const submit = () => {
    if (save.isPending) return
    const rolesOk = roles.length > 0
    setRolesError(rolesOk ? undefined : 'En az bir rol seçin.')
    if (form.validateAll() && rolesOk) save.mutate()
  }
  const needsStore = roles.includes('store_owner') && !form.values.storeId

  return (
    <SlideOver open onClose={onClose} title={user ? `${user.fullName} hesabını düzenle` : 'Kullanıcı ekle'} description={user ? user.email : 'Yeni çalışanlar için önerilen yol Hesap talepleri ekranıdır. Burada açılan hesaba da şifre girilmez; çalışana kendi şifresini belirleyeceği davet e-postası gider.'}
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} {user ? 'Değişiklikleri kaydet' : 'Kullanıcıyı oluştur'}</button></>}>
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-5">
        <Field label="Ad soyad" htmlFor="f-fullName" error={form.errors.fullName}><input {...form.field('fullName')} className="form-field" autoComplete="off" /></Field>
        {!user && (
          <>
            <Field label="E-posta" htmlFor="f-email" error={form.errors.email}><input {...form.field('email')} type="email" className="form-field" placeholder="ad@sirket.com" autoComplete="off" /></Field>
          </>
        )}
        <Field label="Kullanıcı adı" htmlFor="f-username" error={form.errors.username} optional hint="E-posta yerine girişte kullanılabilir."><input {...form.field('username')} className="form-field" autoCapitalize="none" autoComplete="off" /></Field>
        <fieldset>
          <legend className="form-label">Roller</legend>
          <div className="space-y-1">
            {Object.entries(roleLabels).map(([name, label]) => (
              <label key={name} className="flex min-h-11 items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
                <input type="checkbox" className="size-4 accent-violet-500" disabled={(isSelf && name === 'system_admin') || (!canAssign(name) && !roles.includes(name))} checked={roles.includes(name)}
                  onChange={(e) => { setRoles(e.target.checked ? [...roles, name] : roles.filter((r) => r !== name)); setRolesError(undefined) }} />
                {label}{!canAssign(name) && <span className="text-xs text-gray-400">(yalnızca sistem yöneticisi verebilir)</span>}
              </label>
            ))}
          </div>
          {rolesError && <p className="mt-1 text-xs text-rose-500">{rolesError}</p>}
          {isSelf && <p className="mt-1 text-xs text-gray-400">Kendi yönetici rolünüzü kaldıramazsınız.</p>}
        </fieldset>
        <Field label="Mağaza" htmlFor="f-storeId" optional hint={needsStore ? 'Mağaza sahibi rolü için mağaza seçin; aksi hâlde mağaza verilerini göremez.' : undefined}>
          <select {...form.field('storeId')} className="form-field">
            <option value="">Merkez / mağazaya bağlı değil</option>
            {stores.data?.map((s) => <option key={s.id} value={s.id}>{s.name} ({s.code})</option>)}
          </select>
        </Field>
        <Field label="Departman" htmlFor="f-departmentId" optional hint="Unvan, yönetici ve diğer personel bilgileri Çalışanlar ekranından İK tarafından yönetilir.">
          <select {...form.field('departmentId')} className="form-field">
            <option value="">Seçilmedi</option>
            {departments.data?.filter((d) => d.isActive || String(d.id) === form.values.departmentId).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        </Field>
        {user && !isSelf && <AccountActions user={user} onDone={onClose} />}
        {user && (
          <label className="flex min-h-11 items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
            <input type="checkbox" className="size-4 accent-violet-500" disabled={isSelf} checked={form.values.isActive} onChange={(e) => form.setValue('isActive', e.target.checked)} />
            Hesap aktif — pasif hesaplar giriş yapamaz, açık oturumları kapatılır
          </label>
        )}
      </form>
    </SlideOver>
  )
}

/** Hesap işlemleri (BT / yönetici): kilitle, kilidi aç, ilk şifre davetini yeniden gönder. Hepsi denetim kaydına yazılır. */
function AccountActions({ user, onDone }: { user: UserListItem; onDone: () => void }) {
  const qc = useQueryClient()
  const [reason, setReason] = useState('')
  const [locking, setLocking] = useState(false)
  const finish = (msg: string) => { toast(msg); qc.invalidateQueries({ queryKey: ['users'] }); onDone() }
  const onError = (err: unknown) => toast(errorMessage(err), 'error')
  const lock = useMutation({ mutationFn: () => api.users.lock(user.id, reason), onSuccess: () => finish(`${user.fullName} hesabı kilitlendi; açık oturumları kapatıldı.`), onError })
  const unlock = useMutation({ mutationFn: () => api.users.unlock(user.id), onSuccess: () => finish(`${user.fullName} hesabının kilidi açıldı.`), onError })
  const invite = useMutation({ mutationFn: () => api.users.resendInvite(user.id), onSuccess: () => finish('İlk şifre bağlantısı yeniden gönderildi.'), onError })
  return (
    <section className="space-y-3 rounded-lg border border-gray-200 p-4 dark:border-gray-700/60" aria-labelledby="acc-actions">
      <h3 id="acc-actions" className="text-sm font-semibold">Hesap işlemleri</h3>
      {user.lockedAt ? (
        <div className="space-y-2">
          <p className="text-sm text-amber-700 dark:text-amber-300">Kilitli{user.lockReason && ` — ${user.lockReason}`}</p>
          <button type="button" className="btn-secondary" aria-busy={unlock.isPending} onClick={() => unlock.mutate()}><LockOpen className="size-4" aria-hidden="true" /> Kilidi aç</button>
        </div>
      ) : locking ? (
        <div className="space-y-2">
          <Field label="Kilitleme gerekçesi" htmlFor="f-lock-reason" required><input id="f-lock-reason" className="form-field" value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} placeholder="Ör. şüpheli giriş denemesi" /></Field>
          <div className="flex gap-2">
            <button type="button" className="btn-secondary" onClick={() => setLocking(false)}>Vazgeç</button>
            <button type="button" className="btn-danger" disabled={!reason.trim()} aria-busy={lock.isPending} onClick={() => lock.mutate()}><Lock className="size-4" aria-hidden="true" /> Hesabı kilitle</button>
          </div>
        </div>
      ) : (
        <div className="flex flex-wrap gap-2">
          <button type="button" className="btn-secondary" onClick={() => setLocking(true)}><Lock className="size-4" aria-hidden="true" /> Kilitle</button>
          {user.passwordSet === false && user.isActive && (
            <button type="button" className="btn-secondary" aria-busy={invite.isPending} onClick={() => invite.mutate()}><Send className="size-4" aria-hidden="true" /> Şifre davetini yeniden gönder</button>
          )}
        </div>
      )}
      <p className="text-xs text-gray-500">Kilit, şifre doğru olsa bile girişi engeller ve açık oturumları hemen kapatır. Ayrılan çalışan için hesabı pasif yapın veya İK'nın kapatma talebini onaylayın.</p>
    </section>
  )
}
