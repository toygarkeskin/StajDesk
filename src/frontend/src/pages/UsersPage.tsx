import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { roleLabels } from '@/lib/labels'
import { formatDate, timeAgo } from '@/lib/format'
import type { UserListItem, UserRole } from '@/lib/types'
import { useAuth } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { Avatar, Badge, EmptyState, ErrorState, Field, IconButton, PageHeader, Pagination, SlideOver, Spinner, TableSkeleton } from '@/components/ui'
import { DataFrame, FilterSelect, ListToolbar, Th, type FilterChip } from '@/components/list'

/* List / Index kalıbı — kullanıcılar. Ekleme/düzenleme yan panelde. */

const PAGE_SIZE = 10
const roleBadge: Record<UserRole, string> = {
  User: 'bg-gray-400/15 text-gray-600 dark:text-gray-400',
  Agent: 'bg-sky-500/15 text-sky-700 dark:text-sky-400',
  Admin: 'bg-violet-500/15 text-violet-700 dark:text-violet-400',
}

export function UsersPage() {
  const [params, setParams] = useSearchParams()
  const get = (k: string) => params.get(k) ?? ''
  const f = { role: get('role'), isActive: get('isActive'), search: get('search'), page: Number(params.get('page') ?? 1) }
  const update = (patch: Record<string, string | number | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) v === null || v === '' ? next.delete(k) : next.set(k, String(v))
    if (!('page' in patch)) next.delete('page')
    setParams(next)
  }
  const [editing, setEditing] = useState<UserListItem | 'new' | null>(null)
  const users = useQuery({ queryKey: ['users', f], queryFn: () => api.users.list({ ...f, pageSize: PAGE_SIZE }), placeholderData: keepPreviousData })

  const chips: FilterChip[] = [
    f.search && { key: 'search', label: `Arama: "${f.search}"` },
    f.role && { key: 'role', label: `Rol: ${roleLabels[f.role as UserRole]}` },
    f.isActive && { key: 'isActive', label: f.isActive === 'true' ? 'Yalnızca aktif' : 'Yalnızca pasif' },
  ].filter(Boolean) as FilterChip[]
  const cols = 7

  return (
    <>
      <PageHeader
        title="Kullanıcılar"
        description="Hesaplar, roller ve erişim durumu; ada göre sıralı."
        actions={<button className="btn-primary" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Kullanıcı ekle</button>}
      />
      <DataFrame
        toolbar={
          <ListToolbar
            searchLabel="Kullanıcı ara"
            searchPlaceholder="Ad, e-posta veya departman"
            search={f.search}
            onSearch={(v) => update({ search: v })}
            filterCount={chips.filter((c) => c.key !== 'search').length}
            chips={chips}
            onRemoveChip={(k) => update({ [k]: null })}
            onClearAll={() => setParams({})}
            filters={
              <>
                <FilterSelect id="flt-role" label="Rol" allLabel="Tüm roller" value={f.role} onChange={(v) => update({ role: v })} options={Object.entries(roleLabels).map(([value, label]) => ({ value, label }))} />
                <FilterSelect id="flt-active" label="Hesap durumu" allLabel="Tümü" value={f.isActive} onChange={(v) => update({ isActive: v })} options={[{ value: 'true', label: 'Aktif' }, { value: 'false', label: 'Pasif' }]} />
              </>
            }
          />
        }
        footer={<Pagination page={users.data?.page ?? f.page} totalPages={users.data?.totalPages ?? 0} totalCount={users.data?.totalCount ?? 0} pageSize={PAGE_SIZE} onChange={(p) => update({ page: p })} />}
      >
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="table-head"><tr><Th>Kullanıcı</Th><Th>Rol</Th><Th>Departman</Th><Th>Durum</Th><Th className="text-right">Açık talep</Th><Th>Son giriş</Th><Th><span className="sr-only">İşlemler</span></Th></tr></thead>
            <tbody>
              {users.isLoading ? (
                <tr><td colSpan={cols}><TableSkeleton cols={cols - 1} /></td></tr>
              ) : users.isError ? (
                <tr><td colSpan={cols}><ErrorState message={errorMessage(users.error)} details={errorDetails(users.error)} onRetry={() => users.refetch()} /></td></tr>
              ) : users.data!.items.length === 0 ? (
                <tr><td colSpan={cols}>
                  {chips.length ? (
                    <EmptyState title="Bu filtrelere uyan kullanıcı yok" description="Arama veya filtreleri değiştirerek tekrar bakın." action={<button className="btn-secondary" onClick={() => setParams({})}>Filtreleri temizle</button>} />
                  ) : (
                    <EmptyState title="Henüz kullanıcı yok" description="Destek personeli veya çalışan hesabı ekleyin." action={<button className="btn-primary" onClick={() => setEditing('new')}>Kullanıcı ekle</button>} />
                  )}
                </td></tr>
              ) : (
                users.data!.items.map((u) => (
                  <tr key={u.id} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-4">
                        <Avatar name={u.fullName} />
                        <div className="min-w-0">
                          <div className="font-medium text-gray-900 dark:text-gray-100">{u.fullName}</div>
                          <div className="text-xs text-gray-500">{u.email}</div>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3"><Badge className={roleBadge[u.role]}>{roleLabels[u.role]}</Badge></td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{u.department ?? '—'}</td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{u.isActive ? 'Aktif' : <span className="text-red-700 dark:text-red-400">Pasif</span>}</td>
                    <td className="px-4 py-3 text-right text-gray-900 tabular-nums dark:text-gray-100">{u.openTicketCount}</td>
                    <td className="px-4 py-3 whitespace-nowrap text-gray-600 dark:text-gray-400" title={`Kayıt: ${formatDate(u.createdAt)}`}>{u.lastLoginAt ? timeAgo(u.lastLoginAt) : 'Hiç giriş yapmadı'}</td>
                    <td className="px-2 py-1 text-right">
                      <IconButton label={`${u.fullName} hesabını düzenle`} onClick={() => setEditing(u)}><Pencil className="size-4" /></IconButton>
                    </td>
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
  const meId = useAuth((s) => s.user?.id)
  const isSelf = user?.id === meId
  const form = useForm(
    { fullName: user?.fullName ?? '', email: user?.email ?? '', password: '', role: (user?.role ?? 'User') as string, department: user?.department ?? '', isActive: user?.isActive ?? true },
    {
      fullName: (v) => (!v.trim() ? 'Ad ve soyadı girin.' : undefined),
      email: (v) => (user ? undefined : !/^\S+@\S+\.\S+$/.test(v) ? 'ad@sirket.com biçiminde bir e-posta girin.' : undefined),
      password: (v) => (user ? undefined : v.length < 8 ? 'En az 8 karakterlik geçici bir şifre belirleyin.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: () => {
      const v = form.values
      return user
        ? api.users.update(user.id, { fullName: v.fullName, role: v.role, department: v.department || null, phone: null, isActive: v.isActive })
        : api.users.create({ fullName: v.fullName, email: v.email, password: v.password, role: v.role, department: v.department || null, phone: null })
    },
    onSuccess: (u) => {
      qc.invalidateQueries({ queryKey: ['users'] })
      qc.invalidateQueries({ queryKey: ['agents'] })
      toast(user ? `${u.fullName} güncellendi.` : `${u.fullName} hesabı oluşturuldu.`)
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
      title={user ? `${user.fullName} hesabını düzenle` : 'Kullanıcı ekle'}
      description={user ? user.email : 'Kullanıcı ilk girişte bu geçici şifreyi kullanır.'}
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} {user ? 'Değişiklikleri kaydet' : 'Kullanıcıyı oluştur'}</button></>}
    >
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-6">
        <Field label="Ad soyad" htmlFor="f-fullName" error={form.errors.fullName}><input {...form.field('fullName')} className="form-field" autoComplete="off" /></Field>
        {!user && (
          <>
            <Field label="E-posta" htmlFor="f-email" error={form.errors.email}><input {...form.field('email')} type="email" className="form-field" placeholder="ad@sirket.com" autoComplete="off" /></Field>
            <Field label="Geçici şifre" htmlFor="f-password" error={form.errors.password}><input {...form.field('password')} type="password" className="form-field" autoComplete="new-password" /></Field>
          </>
        )}
        <Field label="Rol" htmlFor="f-role" hint={isSelf ? 'Kendi rolünüzü değiştiremezsiniz.' : undefined}>
          <select {...form.field('role')} className="form-field" disabled={isSelf}>
            {Object.entries(roleLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </Field>
        <Field label="Departman" htmlFor="f-department" optional><input {...form.field('department')} className="form-field" /></Field>
        {user && (
          <label className="flex min-h-11 items-center gap-2 text-sm text-gray-800 dark:text-gray-200">
            <input type="checkbox" className="size-4 accent-violet-600" disabled={isSelf} checked={form.values.isActive} onChange={(e) => form.setValue('isActive', e.target.checked)} />
            Hesap aktif — pasif hesaplar giriş yapamaz
          </label>
        )}
      </form>
    </SlideOver>
  )
}
