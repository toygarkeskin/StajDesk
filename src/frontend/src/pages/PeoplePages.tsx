import { useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Briefcase, Building2, Camera, Cake, Mail, Pencil, Phone, Plus, Send, ShieldCheck, UserRound, Users } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { employmentLabels, seniority } from '@/lib/labels'
import { formatDate, formatDateTime } from '@/lib/format'
import type { EmployeeDetail, EmploymentType } from '@/lib/types'
import { useAuth, useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { BlockSkeleton, EmptyState, ErrorState, Field, IconButton, PageHeader, Pagination, SlideOver, Spinner } from '@/components/ui'
import { PersonAvatar, useEmployeeImage } from '@/components/AuthImage'
import { AccountTab, PreferencesTab } from './AccountTabs'

const PAGE_SIZE = 24

/* =========================================================
   Çalışan rehberi (List kalıbı: kart ızgarası + arama + departman filtresi)
   ========================================================= */

export function EmployeesPage() {
  const { can } = useCan()
  const isHr = can('employees.manage')
  const [params, setParams] = useSearchParams()
  const search = params.get('search') ?? ''
  const departmentId = params.get('departmentId') ?? ''
  const inactive = params.get('inactive') === '1'
  const page = Number(params.get('page') ?? 1)
  const [creating, setCreating] = useState(false)
  const set = (k: string, v: string | null) => {
    const next = new URLSearchParams(params)
    if (v) next.set(k, v); else next.delete(k)
    if (k !== 'page') next.delete('page')
    setParams(next)
  }

  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const list = useQuery({
    queryKey: ['employees', search, departmentId, inactive, page],
    queryFn: () => api.employees.list({ search, departmentId, includeInactive: inactive || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <PageHeader
        title="Çalışanlar"
        description="Şirket rehberi: unvan, departman ve iletişim bilgileri."
        actions={isHr && <button className="btn-violet" onClick={() => setCreating(true)}><Plus className="size-4" aria-hidden="true" /> Personel ekle</button>}
      />
      <div className="mb-4 flex flex-col gap-2 sm:flex-row">
        <label htmlFor="emp-search" className="sr-only">Ad, unvan, departman veya personel no</label>
        <input id="emp-search" type="search" className="form-field sm:max-w-sm" placeholder="Ad, unvan, departman veya personel no" defaultValue={search}
          onKeyDown={(e) => e.key === 'Enter' && set('search', (e.target as HTMLInputElement).value.trim() || null)}
          onBlur={(e) => e.target.value.trim() !== search && set('search', e.target.value.trim() || null)} />
        <label htmlFor="emp-dept" className="sr-only">Departman</label>
        <select id="emp-dept" className="form-field sm:max-w-xs" value={departmentId} onChange={(e) => set('departmentId', e.target.value || null)}>
          <option value="">Tüm departmanlar</option>
          {departments.data?.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
        </select>
        {isHr && (
          <label className="flex min-h-11 items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
            <input type="checkbox" className="size-4 accent-violet-500" checked={inactive} onChange={(e) => set('inactive', e.target.checked ? '1' : null)} /> Ayrılanları da göster
          </label>
        )}
      </div>

      {list.isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">{Array.from({ length: 8 }).map((_, i) => <BlockSkeleton key={i} className="h-28" />)}</div>
      ) : list.isError ? (
        <div className="card"><ErrorState message={errorMessage(list.error)} details={errorDetails(list.error)} onRetry={() => list.refetch()} /></div>
      ) : list.data!.items.length === 0 ? (
        <div className="card"><EmptyState title="Çalışan bulunamadı" description="Aramayı veya departman filtresini değiştirerek tekrar deneyin." /></div>
      ) : (
        <>
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">
            {list.data!.items.map((e) => (
              <li key={e.id}>
                <Link to={`/employees/${e.id}`} className={clsx('card flex h-full items-start gap-3 p-4 transition hover:ring-2 hover:ring-violet-500/30', !e.isActive && 'opacity-60')}>
                  <PersonAvatar id={e.id} name={e.fullName} photoVersion={e.photoVersion} size="lg" />
                  <div className="min-w-0 text-sm">
                    <p className="truncate font-semibold text-gray-800 dark:text-gray-100">{e.fullName}{!e.isActive && <span className="ml-1 text-xs font-normal text-gray-500">(ayrıldı)</span>}</p>
                    <p className="truncate text-gray-600 dark:text-gray-300">{e.title ?? 'Unvan girilmedi'}</p>
                    <p className="truncate text-xs text-gray-500">{[e.department?.name, e.store?.name].filter(Boolean).join(' · ') || 'Departman girilmedi'}</p>
                    <p className="mt-1 truncate text-xs text-gray-500">{e.email}</p>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
          <div className="card mt-4"><Pagination page={list.data!.page} totalPages={list.data!.totalPages} totalCount={list.data!.totalCount} pageSize={PAGE_SIZE} onChange={(p) => set('page', String(p))} /></div>
        </>
      )}
      {creating && <CreateEmployeePanel onClose={() => setCreating(false)} />}
    </>
  )
}

function useOrgLookups() {
  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const positions = useQuery({ queryKey: ['positions'], queryFn: api.org.positions })
  const stores = useQuery({ queryKey: ['stores'], queryFn: api.stores.list })
  // Yönetici seçimi için tüm aktif çalışanlar (rehber 100'lük sayfalarla gelir; şema hepsini tek seferde verir).
  const chart = useQuery({ queryKey: ['org-chart'], queryFn: api.org.chart })
  return { departments, positions, stores, people: chart.data?.people ?? [] }
}

/** İK: yeni personel kaydı. Şifre girilmez; çalışana "ilk şifreni belirle" e-postası gider. */
function CreateEmployeePanel({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { departments, positions, stores, people } = useOrgLookups()
  const form = useForm(
    { fullName: '', email: '', username: '', phone: '', employeeNumber: '', departmentId: '', positionId: '', managerId: '', storeId: '', hireDate: new Date().toISOString().slice(0, 10), employmentType: 'FullTime' },
    {
      fullName: (v) => (!v.trim() ? 'Ad soyad girin.' : undefined),
      email: (v) => (!/^\S+@\S+\.\S+$/.test(v) ? 'ad@sirket.com biçiminde bir iş e-postası girin.' : undefined),
      username: (v) => (v && !/^[a-zA-Z0-9._-]{3,50}$/.test(v) ? '3–50 karakter: harf, rakam, nokta, tire, alt çizgi.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: () => {
      const v = form.values
      const n = (x: string) => (x ? Number(x) : null)
      return api.employees.create({ ...v, username: v.username || null, phone: v.phone || null, employeeNumber: v.employeeNumber || null,
        departmentId: n(v.departmentId), positionId: n(v.positionId), managerId: n(v.managerId), storeId: n(v.storeId), hireDate: v.hireDate || null })
    },
    onSuccess: (r) => {
      toast('Personel kaydı oluşturuldu; ilk şifre bağlantısı e-postayla gönderildi.')
      qc.invalidateQueries({ queryKey: ['employees'] })
      onClose()
      navigate(`/employees/${r.id}`)
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()
  return (
    <SlideOver open onClose={onClose} title="Personel ekle" description="Çalışan, iş e-postasına gelen bağlantıyla kendi şifresini belirler. Sistem rolü varsayılan olarak Kullanıcı'dır."
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} Kaydet ve davet gönder</button></>}>
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-4">
        <Field label="Ad soyad" htmlFor="f-fullName" error={form.errors.fullName} required><input {...form.field('fullName')} className="form-field" maxLength={100} /></Field>
        <Field label="İş e-postası" htmlFor="f-email" error={form.errors.email} required><input {...form.field('email')} type="email" className="form-field" /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Kullanıcı adı" htmlFor="f-username" error={form.errors.username} optional><input {...form.field('username')} className="form-field" autoCapitalize="none" /></Field>
          <Field label="Personel no" htmlFor="f-employeeNumber" optional><input {...form.field('employeeNumber')} className="form-field" maxLength={20} /></Field>
          <Field label="Departman" htmlFor="f-departmentId" optional>
            <select {...form.field('departmentId')} className="form-field"><option value="">Seçin</option>{departments.data?.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>
          </Field>
          <Field label="Unvan" htmlFor="f-positionId" optional>
            <select {...form.field('positionId')} className="form-field"><option value="">Seçin</option>{positions.data?.map((p) => <option key={p.id} value={p.id}>{p.title}</option>)}</select>
          </Field>
          <Field label="Yönetici" htmlFor="f-managerId" optional>
            <select {...form.field('managerId')} className="form-field"><option value="">Yok</option>{people.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}</select>
          </Field>
          <Field label="Mağaza / şube" htmlFor="f-storeId" optional>
            <select {...form.field('storeId')} className="form-field"><option value="">Merkez</option>{stores.data?.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
          </Field>
          <Field label="İşe giriş tarihi" htmlFor="f-hireDate" optional><input type="date" {...form.field('hireDate')} className="form-field" /></Field>
          <Field label="Çalışma türü" htmlFor="f-employmentType">
            <select {...form.field('employmentType')} className="form-field">{Object.entries(employmentLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>
          </Field>
          <Field label="İş telefonu" htmlFor="f-phone" optional><input {...form.field('phone')} type="tel" className="form-field" /></Field>
        </div>
      </form>
    </SlideOver>
  )
}

/* =========================================================
   Profil (Detail kalıbı: kapak + kimlik + sekmeler)
   ========================================================= */

type Tab = 'overview' | 'personal' | 'work' | 'account' | 'preferences'

export function MyProfilePage() {
  const me = useAuth((s) => s.me)
  return me ? <ProfileView id={me.id} /> : null
}

export function ProfilePage() {
  return <ProfileView id={Number(useParams().id)} />
}

function ProfileView({ id }: { id: number }) {
  const [params, setParams] = useSearchParams()
  const profile = useQuery({ queryKey: ['employee', id], queryFn: () => api.employees.get(id), enabled: Number.isFinite(id) })
  if (profile.isLoading) return <div className="space-y-4"><BlockSkeleton className="h-56" /><BlockSkeleton className="h-64" /></div>
  if (profile.isError) return <div className="card"><ErrorState message={errorMessage(profile.error)} details={errorDetails(profile.error)} onRetry={() => profile.refetch()} /></div>
  const p = profile.data!
  const tabs: { key: Tab; label: string; show: boolean }[] = [
    { key: 'overview', label: 'Genel bakış', show: true },
    { key: 'personal', label: 'Kişisel bilgiler', show: p.access.canSeePrivate },
    { key: 'work', label: 'İş bilgileri', show: true },
    { key: 'account', label: 'Hesap ve güvenlik', show: p.access.isSelf },
    { key: 'preferences', label: 'Bildirim tercihleri', show: p.access.isSelf },
  ]
  const visible = tabs.filter((t) => t.show)
  const tab = (visible.find((t) => t.key === params.get('tab'))?.key ?? 'overview') as Tab

  return (
    <>
      <ProfileHeader p={p} />
      <div role="tablist" aria-label="Profil bölümleri" className="mb-6 flex gap-1 overflow-x-auto border-b border-gray-200 dark:border-gray-700/60">
        {visible.map((t) => (
          <button key={t.key} role="tab" aria-selected={tab === t.key} onClick={() => setParams(t.key === 'overview' ? {} : { tab: t.key })}
            className={clsx('min-h-11 shrink-0 border-b-2 px-3 text-sm font-medium transition', tab === t.key ? 'border-violet-500 text-violet-600 dark:text-violet-300' : 'border-transparent text-gray-500 hover:text-gray-800 dark:hover:text-gray-200')}>
            {t.label}
          </button>
        ))}
      </div>
      {tab === 'overview' && <OverviewTab p={p} />}
      {tab === 'personal' && <PersonalTab p={p} />}
      {tab === 'work' && <WorkTab p={p} />}
      {tab === 'account' && <AccountTab p={p} />}
      {tab === 'preferences' && <PreferencesTab />}
    </>
  )
}

function ProfileHeader({ p }: { p: EmployeeDetail }) {
  const qc = useQueryClient()
  const cover = useEmployeeImage(p.id, 'cover', p.coverVersion)
  const photoInput = useRef<HTMLInputElement>(null)
  const coverInput = useRef<HTMLInputElement>(null)
  const canEdit = p.access.canEditPersonal
  const upload = useMutation({
    mutationFn: ({ kind, file }: { kind: 'photo' | 'cover'; file: File }) => api.employees.setImage(p.id, kind, file),
    onSuccess: (d) => {
      qc.setQueryData(['employee', p.id], d)
      qc.invalidateQueries({ queryKey: ['employee-image', p.id] })
      qc.invalidateQueries({ queryKey: ['employees'] })
      if (p.access.isSelf) api.auth.me().then((me) => useAuth.getState().setMe(me))
      toast('Fotoğraf güncellendi.')
    },
    onError: (err) => toast(errorMessage(err), 'error'),
  })
  const pick = (kind: 'photo' | 'cover', files: FileList | null) => {
    const file = files?.[0]
    if (!file) return
    if (file.size > 2 * 1024 * 1024) return toast('Fotoğraf 2 MB\'tan büyük olamaz. Küçültüp tekrar deneyin.', 'error')
    upload.mutate({ kind, file })
  }
  return (
    <section className="card mb-6 overflow-hidden">
      <div className="relative h-32 bg-linear-to-r from-violet-600 to-indigo-600 sm:h-40">
        {cover && <img src={cover} alt="" className="absolute inset-0 size-full object-cover" />}
        {canEdit && (
          <>
            <input ref={coverInput} type="file" accept=".png,.jpg,.jpeg,.webp" className="sr-only" onChange={(e) => { pick('cover', e.target.files); e.target.value = '' }} />
            <button className="absolute right-3 bottom-3 inline-flex min-h-9 items-center gap-1.5 rounded-lg bg-black/40 px-3 text-xs font-medium text-white backdrop-blur hover:bg-black/55" onClick={() => coverInput.current?.click()}>
              <Camera className="size-4" aria-hidden="true" /> Kapak fotoğrafı
            </button>
          </>
        )}
      </div>
      <div className="flex flex-col gap-3 px-4 pb-4 sm:flex-row sm:items-end sm:px-6">
        <div className="relative -mt-12 shrink-0">
          <PersonAvatar id={p.id} name={p.fullName} photoVersion={p.photoVersion} size="2xl" className="ring-4 ring-white dark:ring-gray-800" />
          {canEdit && (
            <>
              <input ref={photoInput} type="file" accept=".png,.jpg,.jpeg,.webp" className="sr-only" onChange={(e) => { pick('photo', e.target.files); e.target.value = '' }} />
              <IconButton label="Profil fotoğrafını değiştir" className="absolute -right-1 -bottom-1 size-9 bg-white shadow ring-1 ring-gray-200 dark:bg-gray-700 dark:ring-gray-600" onClick={() => photoInput.current?.click()}>
                {upload.isPending ? <Spinner /> : <Camera className="size-4" />}
              </IconButton>
            </>
          )}
        </div>
        <div className="min-w-0 flex-1">
          <h1 className="truncate text-xl font-bold uppercase sm:text-2xl">{p.fullName}</h1>
          <p className="truncate text-sm text-gray-500">{[p.position?.name, p.department?.name].filter(Boolean).join(' · ') || 'Unvan ve departman girilmedi'}</p>
          {!p.isActive && <p className="mt-1 text-xs font-medium text-rose-600">Bu çalışan ayrıldı / hesabı pasif.</p>}
        </div>
      </div>
    </section>
  )
}

function Fact({ icon, label, children }: { icon: ReactNode; label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="flex items-center gap-1.5 text-xs text-gray-500"><span className="text-violet-500" aria-hidden="true">{icon}</span>{label}</dt>
      <dd className="mt-1 truncate text-sm font-semibold text-gray-800 dark:text-gray-100">{children}</dd>
    </div>
  )
}

function OverviewTab({ p }: { p: EmployeeDetail }) {
  const birthday = p.birthday ? new Date(`2000-${p.birthday}T00:00:00`).toLocaleDateString('tr-TR', { day: 'numeric', month: 'long' }) : null
  return (
    <div className="space-y-6">
      <section className="card p-5">
        <dl className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
          <Fact icon={<Briefcase className="size-3.5" />} label="Kıdem">{seniority(p.hireDate) ?? '—'}</Fact>
          <Fact icon={<Briefcase className="size-3.5" />} label="İşe başlama tarihi">{p.hireDate ? formatDate(p.hireDate) : '—'}</Fact>
          <Fact icon={<Building2 className="size-3.5" />} label="Departman">{p.department?.name ?? '—'}</Fact>
          <Fact icon={<UserRound className="size-3.5" />} label="Çalışan türü">{employmentLabels[p.employmentType]}</Fact>
          <Fact icon={<UserRound className="size-3.5" />} label="Yönetici">{p.manager ? <Link className="text-violet-600 hover:underline dark:text-violet-300" to={`/employees/${p.manager.id}`}>{p.manager.fullName}</Link> : '—'}</Fact>
          <Fact icon={<Mail className="size-3.5" />} label="E-posta"><a className="hover:text-violet-600" href={`mailto:${p.email}`}>{p.email}</a></Fact>
          <Fact icon={<Phone className="size-3.5" />} label="Telefon">{p.phone ? <a className="hover:text-violet-600" href={`tel:${p.phone.replace(/\s/g, '')}`}>{p.phone}</a> : '—'}</Fact>
          <Fact icon={<Cake className="size-3.5" />} label="Doğum günü">{birthday ?? '—'}</Fact>
        </dl>
      </section>
      <section className="card p-5" aria-labelledby="reports-title">
        <h2 id="reports-title" className="mb-3 flex items-center gap-2 text-sm font-semibold"><Users className="size-4 text-gray-400" aria-hidden="true" /> Ekibi ({p.directReports.length})</h2>
        {p.directReports.length === 0 ? <p className="text-sm text-gray-500">Bu kişiye bağlı çalışan yok.</p> : (
          <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {p.directReports.map((r) => (
              <li key={r.id}>
                <Link to={`/employees/${r.id}`} className="flex items-center gap-3 rounded-lg border border-gray-200 p-2 hover:border-violet-300 dark:border-gray-700/60">
                  <PersonAvatar id={r.id} name={r.fullName} photoVersion={r.photoVersion} />
                  <span className="min-w-0 text-sm"><span className="block truncate font-medium text-gray-800 dark:text-gray-100">{r.fullName}</span><span className="block truncate text-xs text-gray-500">{r.title ?? '—'}</span></span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}

/** Kişisel bilgiler: yalnızca kişinin kendisi ve yetkili İK görür / düzenler. */
function PersonalTab({ p }: { p: EmployeeDetail }) {
  const qc = useQueryClient()
  const v = p.private ?? {}
  const form = useForm(
    { phone: p.phone ?? '', personalEmail: v.personalEmail ?? '', address: v.address ?? '', birthDate: v.birthDate ?? '',
      emergencyContactName: v.emergencyContactName ?? '', emergencyContactPhone: v.emergencyContactPhone ?? '', emergencyContactRelation: v.emergencyContactRelation ?? '' },
    { personalEmail: (x) => (x && !/^\S+@\S+\.\S+$/.test(x) ? 'ad@ornek.com biçiminde bir e-posta girin.' : undefined) },
  )
  const save = useMutation({
    mutationFn: () => {
      const f = form.values
      const n = (x: string) => x.trim() || null
      return api.employees.updatePersonal(p.id, { phone: n(f.phone), personalEmail: n(f.personalEmail), address: n(f.address), birthDate: n(f.birthDate),
        emergencyContactName: n(f.emergencyContactName), emergencyContactPhone: n(f.emergencyContactPhone), emergencyContactRelation: n(f.emergencyContactRelation) })
    },
    onSuccess: (d) => {
      qc.setQueryData(['employee', p.id], d)
      toast('Kişisel bilgiler kaydedildi.')
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const disabled = !p.access.canEditPersonal
  return (
    <form className="card space-y-6 p-5" noValidate onSubmit={(e) => { e.preventDefault(); if (!save.isPending && form.validateAll()) save.mutate() }}>
      <p className="flex items-center gap-2 text-xs text-gray-500"><ShieldCheck className="size-4 text-emerald-500" aria-hidden="true" /> Bu bilgileri yalnızca siz ve yetkili İnsan Kaynakları personeli görebilir.</p>
      <fieldset disabled={disabled} className="grid gap-4 sm:grid-cols-2">
        <Field label="Cep telefonu" htmlFor="f-phone" optional hint="İki adımlı doğrulama kodları bu numaraya gönderilir."><input {...form.field('phone')} type="tel" autoComplete="tel" className="form-field" /></Field>
        <Field label="Kişisel e-posta" htmlFor="f-personalEmail" error={form.errors.personalEmail} optional><input {...form.field('personalEmail')} type="email" className="form-field" /></Field>
        <Field label="Doğum tarihi" htmlFor="f-birthDate" optional hint="Diğer çalışanlar yalnızca gün ve ayı görür."><input type="date" {...form.field('birthDate')} className="form-field" /></Field>
        <Field label="Adres" htmlFor="f-address" optional><input {...form.field('address')} autoComplete="street-address" className="form-field" maxLength={300} /></Field>
      </fieldset>
      <fieldset disabled={disabled}>
        <legend className="mb-3 text-sm font-semibold text-gray-800 dark:text-gray-100">Acil durumda aranacak kişi</legend>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Ad soyad" htmlFor="f-emergencyContactName" optional><input {...form.field('emergencyContactName')} className="form-field" /></Field>
          <Field label="Telefon" htmlFor="f-emergencyContactPhone" optional><input {...form.field('emergencyContactPhone')} type="tel" className="form-field" /></Field>
          <Field label="Yakınlık" htmlFor="f-emergencyContactRelation" optional><input {...form.field('emergencyContactRelation')} className="form-field" placeholder="Eşi, annesi…" /></Field>
        </div>
      </fieldset>
      {!disabled && <div className="flex justify-end"><button type="submit" className="btn-violet" aria-busy={save.isPending}>{save.isPending && <Spinner />} Bilgileri kaydet</button></div>}
    </form>
  )
}

/** İş bilgileri: herkes görür; yalnızca İK (employees.manage) düzenler. */
function WorkTab({ p }: { p: EmployeeDetail }) {
  const qc = useQueryClient()
  const [editing, setEditing] = useState(false)
  const invite = useMutation({
    mutationFn: () => api.employees.resendInvite(p.id),
    onSuccess: () => toast('Davet e-postası yeniden gönderildi.'),
    onError: (err) => toast(errorMessage(err), 'error'),
  })
  const rows: [string, ReactNode][] = [
    ['Personel no', p.employeeNumber ?? (p.access.canSeePrivate ? '—' : 'Gizli')],
    ['Departman', p.department?.name ?? '—'],
    ['Unvan', p.position?.name ?? '—'],
    ['Yönetici', p.manager?.fullName ?? '—'],
    ['Mağaza / şube', p.store?.name ?? 'Merkez'],
    ['İşe giriş', p.hireDate ? formatDate(p.hireDate) : '—'],
    ['Çalışma türü', employmentLabels[p.employmentType]],
    ['Durum', p.isActive ? 'Aktif' : 'Ayrıldı / pasif'],
  ]
  if (p.access.canEditWork || p.access.isSelf) {
    rows.push(['Çıkış tarihi', p.terminationDate ? formatDate(p.terminationDate) : '—'])
    rows.push(['Sistem rolleri', p.roles.join(', ') || '—'])
    rows.push(['Son giriş', p.lastLoginAt ? formatDateTime(p.lastLoginAt) : 'Henüz giriş yapmadı'])
  }
  return (
    <section className="card p-5">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-gray-500">Unvan bir yetki değildir: sistemde ne yapabileceğini sistem rolleri belirler.</p>
        {p.access.canEditWork && (
          <div className="flex gap-2">
            {!p.emailVerified && p.isActive && <button className="btn-secondary" onClick={() => invite.mutate()} aria-busy={invite.isPending}><Send className="size-4" aria-hidden="true" /> Daveti yeniden gönder</button>}
            <button className="btn-secondary" onClick={() => setEditing(true)}><Pencil className="size-4" aria-hidden="true" /> İş bilgilerini düzenle</button>
          </div>
        )}
      </div>
      <dl className="divide-y divide-gray-100 dark:divide-gray-700/60">
        {rows.map(([k, v]) => (
          <div key={k} className="grid grid-cols-1 gap-1 py-2.5 text-sm sm:grid-cols-[12rem_1fr]">
            <dt className="text-gray-500">{k}</dt>
            <dd className="font-medium text-gray-800 dark:text-gray-100">{v}</dd>
          </div>
        ))}
      </dl>
      {editing && <EditWorkPanel p={p} onClose={() => setEditing(false)} onSaved={(d) => qc.setQueryData(['employee', p.id], d)} />}
    </section>
  )
}

function EditWorkPanel({ p, onClose, onSaved }: { p: EmployeeDetail; onClose: () => void; onSaved: (d: EmployeeDetail) => void }) {
  const qc = useQueryClient()
  const { departments, positions, stores, people } = useOrgLookups()
  const form = useForm(
    { fullName: p.fullName, username: p.username ?? '', employeeNumber: p.employeeNumber ?? '', departmentId: String(p.department?.id ?? ''), positionId: String(p.position?.id ?? ''),
      managerId: String(p.manager?.id ?? ''), storeId: String(p.store?.id ?? ''), hireDate: p.hireDate ?? '', terminationDate: p.terminationDate ?? '',
      employmentType: p.employmentType as string, isActive: p.isActive ? 'true' : 'false' },
    {
      fullName: (v) => (!v.trim() ? 'Ad soyad girin.' : undefined),
      username: (v) => (v && !/^[a-zA-Z0-9._-]{3,50}$/.test(v) ? '3–50 karakter: harf, rakam, nokta, tire, alt çizgi.' : undefined),
      terminationDate: (v, all) => (v && all.hireDate && v < all.hireDate ? 'Çıkış tarihi işe girişten önce olamaz.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: () => {
      const v = form.values
      const n = (x: string) => (x ? Number(x) : null)
      return api.employees.updateWork(p.id, { fullName: v.fullName, username: v.username || null, employeeNumber: v.employeeNumber || null,
        departmentId: n(v.departmentId), positionId: n(v.positionId), managerId: n(v.managerId), storeId: n(v.storeId),
        hireDate: v.hireDate || null, terminationDate: v.terminationDate || null, employmentType: v.employmentType as EmploymentType, isActive: v.isActive === 'true' })
    },
    onSuccess: (d) => {
      onSaved(d)
      qc.invalidateQueries({ queryKey: ['employees'] })
      qc.invalidateQueries({ queryKey: ['org-chart'] })
      toast('İş bilgileri güncellendi.')
      onClose()
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()
  return (
    <SlideOver open onClose={onClose} title="İş bilgilerini düzenle" description="Pasif yapılan çalışanın açık oturumları hemen kapatılır."
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} Değişiklikleri kaydet</button></>}>
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-4">
        <Field label="Ad soyad" htmlFor="f-fullName" error={form.errors.fullName} required><input {...form.field('fullName')} className="form-field" /></Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Kullanıcı adı" htmlFor="f-username" error={form.errors.username} optional><input {...form.field('username')} className="form-field" autoCapitalize="none" /></Field>
          <Field label="Personel no" htmlFor="f-employeeNumber" optional><input {...form.field('employeeNumber')} className="form-field" /></Field>
          <Field label="Departman" htmlFor="f-departmentId" optional>
            <select {...form.field('departmentId')} className="form-field"><option value="">Yok</option>{departments.data?.filter((d) => d.isActive || d.id === p.department?.id).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>
          </Field>
          <Field label="Unvan" htmlFor="f-positionId" optional>
            <select {...form.field('positionId')} className="form-field"><option value="">Yok</option>{positions.data?.map((x) => <option key={x.id} value={x.id}>{x.title}</option>)}</select>
          </Field>
          <Field label="Yönetici" htmlFor="f-managerId" optional>
            <select {...form.field('managerId')} className="form-field"><option value="">Yok</option>{people.filter((x) => x.id !== p.id).map((x) => <option key={x.id} value={x.id}>{x.fullName}</option>)}</select>
          </Field>
          <Field label="Mağaza / şube" htmlFor="f-storeId" optional>
            <select {...form.field('storeId')} className="form-field"><option value="">Merkez</option>{stores.data?.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
          </Field>
          <Field label="İşe giriş" htmlFor="f-hireDate" optional><input type="date" {...form.field('hireDate')} className="form-field" /></Field>
          <Field label="Çıkış tarihi" htmlFor="f-terminationDate" error={form.errors.terminationDate} optional><input type="date" {...form.field('terminationDate')} className="form-field" /></Field>
          <Field label="Çalışma türü" htmlFor="f-employmentType">
            <select {...form.field('employmentType')} className="form-field">{Object.entries(employmentLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>
          </Field>
          <Field label="Hesap durumu" htmlFor="f-isActive">
            <select {...form.field('isActive')} className="form-field"><option value="true">Aktif</option><option value="false">Pasif (ayrıldı)</option></select>
          </Field>
        </div>
      </form>
    </SlideOver>
  )
}

