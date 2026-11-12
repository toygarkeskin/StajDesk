import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Building2, ChevronDown, ChevronRight, Pencil, Plus, Trash2 } from 'lucide-react'
import { api, errorDetails, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import type { Department, OrgPerson, Position } from '@/lib/types'
import { toast } from '@/stores/ui'
import { BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'
import { PersonAvatar } from '@/components/AuthImage'

/* =========================================================
   Organizasyon şeması: kişiler yönetici ilişkisine göre ağaç olarak.
   Dikey ağaç kullanıldı: telefonda yatay taşma olmaz, derin hiyerarşi girintiyle okunur.
   ========================================================= */

function PersonNode({ person, childrenOf, depth }: { person: OrgPerson; childrenOf: Map<number, OrgPerson[]>; depth: number }) {
  const kids = childrenOf.get(person.id) ?? []
  const [open, setOpen] = useState(depth < 2)
  return (
    <li>
      <div className="flex items-center gap-1">
        {kids.length > 0 ? (
          <IconButton label={open ? `${person.fullName} ekibini gizle` : `${person.fullName} ekibini göster (${kids.length})`} onClick={() => setOpen(!open)} className="size-8">
            {open ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
          </IconButton>
        ) : <span className="w-8 shrink-0" aria-hidden="true" />}
        <Link to={`/employees/${person.id}`} className="flex min-h-11 min-w-0 flex-1 items-center gap-3 rounded-lg px-2 py-1 hover:bg-gray-50 dark:hover:bg-gray-700/30">
          <PersonAvatar id={person.id} name={person.fullName} photoVersion={person.photoVersion} />
          <span className="min-w-0 text-sm">
            <span className="block truncate font-medium text-gray-800 dark:text-gray-100">{person.fullName}</span>
            <span className="block truncate text-xs text-gray-500">{person.title ?? 'Unvan girilmedi'}{kids.length > 0 && ` · ${kids.length} kişilik ekip`}</span>
          </span>
        </Link>
      </div>
      {open && kids.length > 0 && (
        <ul className="ml-4 border-l border-gray-200 pl-3 dark:border-gray-700/60">
          {kids.map((k) => <PersonNode key={k.id} person={k} childrenOf={childrenOf} depth={depth + 1} />)}
        </ul>
      )}
    </li>
  )
}

export function OrgChartPage() {
  const chart = useQuery({ queryKey: ['org-chart'], queryFn: api.org.chart })
  const [params, setParams] = useSearchParams()
  const departmentId = Number(params.get('departmentId') ?? 0) || null

  const { roots, childrenOf } = useMemo(() => {
    const people = (chart.data?.people ?? []).filter((p) => !departmentId || p.departmentId === departmentId)
    const ids = new Set(people.map((p) => p.id))
    const map = new Map<number, OrgPerson[]>()
    const top: OrgPerson[] = []
    for (const p of people) {
      // Yöneticisi bu görünümde yoksa (başka departmanda) kişi kök olarak gösterilir.
      if (p.managerId && ids.has(p.managerId)) map.set(p.managerId, [...(map.get(p.managerId) ?? []), p])
      else top.push(p)
    }
    return { roots: top, childrenOf: map }
  }, [chart.data, departmentId])

  if (chart.isLoading) return <BlockSkeleton className="h-96" />
  if (chart.isError) return <div className="card"><ErrorState message={errorMessage(chart.error)} details={errorDetails(chart.error)} onRetry={() => chart.refetch()} /></div>
  const departments = chart.data!.departments

  return (
    <>
      <PageHeader title="Organizasyon şeması" description="Kimin kime bağlı olduğu ve departmanlar. Bir kişiye tıklayarak profilini açın." />
      <div className="grid gap-6 xl:grid-cols-[22rem_1fr]">
        <section className="card p-4" aria-labelledby="org-depts">
          <h2 id="org-depts" className="mb-2 flex items-center gap-2 text-sm font-semibold"><Building2 className="size-4 text-gray-400" aria-hidden="true" /> Departmanlar</h2>
          <ul className="space-y-0.5">
            <li>
              <button className={clsx('flex min-h-11 w-full items-center justify-between rounded-lg px-3 text-left text-sm', !departmentId ? 'bg-violet-500/10 font-medium text-violet-700 dark:text-violet-300' : 'hover:bg-gray-50 dark:hover:bg-gray-700/30')}
                onClick={() => setParams({})}>Tüm şirket <span className="text-xs text-gray-500">{chart.data!.people.length}</span></button>
            </li>
            <DepartmentTree items={departments} parentId={null} selected={departmentId} onSelect={(id) => setParams({ departmentId: String(id) })} depth={0} />
          </ul>
        </section>
        <section className="card min-w-0 p-4" aria-label="Kişiler">
          {roots.length === 0 ? <EmptyState title="Bu departmanda kimse yok" description="Çalışanlar İK tarafından departmana atandığında burada görünür." /> : (
            <ul className="space-y-1">{roots.map((r) => <PersonNode key={r.id} person={r} childrenOf={childrenOf} depth={0} />)}</ul>
          )}
        </section>
      </div>
    </>
  )
}

function DepartmentTree({ items, parentId, selected, onSelect, depth }: { items: Department[]; parentId: number | null; selected: number | null; onSelect: (id: number) => void; depth: number }) {
  const level = items.filter((d) => (d.parentId ?? null) === parentId)
  if (level.length === 0) return null
  return (
    <>
      {level.map((d) => (
        <li key={d.id}>
          <button style={{ paddingLeft: `${0.75 + depth * 1}rem` }}
            className={clsx('flex min-h-11 w-full items-center justify-between gap-2 rounded-lg pr-3 text-left text-sm', selected === d.id ? 'bg-violet-500/10 font-medium text-violet-700 dark:text-violet-300' : 'hover:bg-gray-50 dark:hover:bg-gray-700/30')}
            onClick={() => onSelect(d.id)}>
            <span className="min-w-0"><span className="block truncate">{d.name}</span>{d.manager && <span className="block truncate text-xs text-gray-500">Yönetici: {d.manager.fullName}</span>}</span>
            <span className="shrink-0 text-xs text-gray-500">{d.memberCount}</span>
          </button>
          <ul><DepartmentTree items={items} parentId={d.id} selected={selected} onSelect={onSelect} depth={depth + 1} /></ul>
        </li>
      ))}
    </>
  )
}

/* =========================================================
   Yönetim: departmanlar ve unvanlar (org.manage)
   ========================================================= */

export function OrgAdminPage() {
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') === 'positions' ? 'positions' : 'departments'
  return (
    <>
      <PageHeader title="Departmanlar ve unvanlar" description="Organizasyon yapısı. Unvan bir yetki değildir; yetkiler Roller ve İzinler ekranından verilir." />
      <div role="tablist" className="mb-6 flex gap-1 border-b border-gray-200 dark:border-gray-700/60">
        {(['departments', 'positions'] as const).map((t) => (
          <button key={t} role="tab" aria-selected={tab === t} onClick={() => setParams(t === 'departments' ? {} : { tab: t })}
            className={clsx('min-h-11 border-b-2 px-3 text-sm font-medium', tab === t ? 'border-violet-500 text-violet-600 dark:text-violet-300' : 'border-transparent text-gray-500 hover:text-gray-800 dark:hover:text-gray-200')}>
            {t === 'departments' ? 'Departmanlar' : 'Unvanlar'}
          </button>
        ))}
      </div>
      {tab === 'departments' ? <DepartmentsAdmin /> : <PositionsAdmin />}
    </>
  )
}

function DepartmentsAdmin() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const [editing, setEditing] = useState<Department | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<Department | null>(null)
  const remove = useMutation({
    mutationFn: (d: Department) => api.org.removeDepartment(d.id),
    onSuccess: (_, d) => { toast(`${d.name} silindi.`); setToDelete(null); qc.invalidateQueries({ queryKey: ['departments'] }); qc.invalidateQueries({ queryKey: ['org-chart'] }) },
    onError: (err) => { toast(errorMessage(err), 'error'); setToDelete(null) },
  })
  return (
    <section className="card overflow-hidden">
      <div className="flex justify-end border-b border-gray-100 p-3 dark:border-gray-700/60"><button className="btn-violet" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Departman ekle</button></div>
      {list.isLoading ? <div className="p-4"><BlockSkeleton className="h-40" /></div> : list.isError ? <ErrorState message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="table-head"><tr><th className="px-4 py-3">Kod</th><th className="px-4 py-3">Departman</th><th className="px-4 py-3">Üst departman</th><th className="px-4 py-3">Yönetici</th><th className="px-4 py-3 text-right">Çalışan</th><th className="px-4 py-3"><span className="sr-only">İşlemler</span></th></tr></thead>
            <tbody>
              {list.data!.map((d) => (
                <tr key={d.id} className={clsx('border-b border-gray-100 last:border-0 dark:border-gray-700/60', !d.isActive && 'opacity-60')}>
                  <td className="px-4 py-2 font-mono text-xs">{d.code}</td>
                  <td className="px-4 py-2 font-medium text-gray-800 dark:text-gray-100">{d.name}{!d.isActive && <span className="ml-1 text-xs font-normal text-gray-500">(pasif)</span>}</td>
                  <td className="px-4 py-2 text-gray-600 dark:text-gray-300">{d.parentName ?? '—'}</td>
                  <td className="px-4 py-2 text-gray-600 dark:text-gray-300">{d.manager?.fullName ?? '—'}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{d.memberCount}</td>
                  <td className="px-2 py-1 text-right whitespace-nowrap">
                    <IconButton label={`${d.name} departmanını düzenle`} onClick={() => setEditing(d)}><Pencil className="size-4" /></IconButton>
                    <IconButton label={`${d.name} departmanını sil`} tone="danger" onClick={() => setToDelete(d)}><Trash2 className="size-4" /></IconButton>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {editing && <DepartmentPanel item={editing === 'new' ? null : editing} all={list.data ?? []} onClose={() => setEditing(null)} />}
      <ConfirmDialog open={toDelete != null} title={`${toDelete?.name} silinsin mi?`}
        body="Alt departmanı veya çalışanı olan departman silinemez; bu durumda pasifleştirin." confirmLabel="Departmanı sil"
        pending={remove.isPending} onConfirm={() => toDelete && remove.mutate(toDelete)} onCancel={() => setToDelete(null)} />
    </section>
  )
}

function DepartmentPanel({ item, all, onClose }: { item: Department | null; all: Department[]; onClose: () => void }) {
  const qc = useQueryClient()
  const chart = useQuery({ queryKey: ['org-chart'], queryFn: api.org.chart })
  const form = useForm(
    { code: item?.code ?? '', name: item?.name ?? '', description: item?.description ?? '', parentId: String(item?.parentId ?? ''), managerId: String(item?.manager?.id ?? ''), isActive: item?.isActive === false ? 'false' : 'true' },
    {
      code: (v) => (!/^[A-Za-z0-9-]{2,20}$/.test(v) ? '2–20 karakter: harf, rakam ve tire.' : undefined),
      name: (v) => (!v.trim() ? 'Departman adını girin.' : undefined),
    },
  )
  const save = useMutation({
    mutationFn: () => {
      const v = form.values
      return api.org.saveDepartment(item?.id ?? null, { code: v.code, name: v.name, description: v.description || null, parentId: v.parentId ? Number(v.parentId) : null, managerId: v.managerId ? Number(v.managerId) : null, isActive: v.isActive === 'true' })
    },
    onSuccess: () => { toast(item ? 'Departman güncellendi.' : 'Departman eklendi.'); qc.invalidateQueries({ queryKey: ['departments'] }); qc.invalidateQueries({ queryKey: ['org-chart'] }); onClose() },
    onError: (err) => { form.setServerErrors(fieldErrors(err)); toast(errorMessage(err), 'error') },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()
  return (
    <SlideOver open onClose={onClose} title={item ? `${item.name} — düzenle` : 'Yeni departman'} description="Departman yöneticisi, onay zincirlerinde &quot;departman yöneticisi&quot; adımını üstlenir."
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-[8rem_1fr]">
          <Field label="Kod" htmlFor="f-code" error={form.errors.code} required><input {...form.field('code')} className="form-field uppercase" maxLength={20} /></Field>
          <Field label="Ad" htmlFor="f-name" error={form.errors.name} required><input {...form.field('name')} className="form-field" maxLength={120} /></Field>
        </div>
        <Field label="Açıklama" htmlFor="f-description" optional><input {...form.field('description')} className="form-field" maxLength={500} /></Field>
        <Field label="Üst departman" htmlFor="f-parentId" optional>
          <select {...form.field('parentId')} className="form-field"><option value="">Yok (en üst)</option>{all.filter((d) => d.id !== item?.id).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}</select>
        </Field>
        <Field label="Yönetici" htmlFor="f-managerId" optional>
          <select {...form.field('managerId')} className="form-field"><option value="">Seçilmedi</option>{chart.data?.people.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}</select>
        </Field>
        {item && (
          <Field label="Durum" htmlFor="f-isActive"><select {...form.field('isActive')} className="form-field"><option value="true">Aktif</option><option value="false">Pasif</option></select></Field>
        )}
      </form>
    </SlideOver>
  )
}

function PositionsAdmin() {
  const qc = useQueryClient()
  const list = useQuery({ queryKey: ['positions'], queryFn: api.org.positions })
  const [editing, setEditing] = useState<Position | 'new' | null>(null)
  const [toDelete, setToDelete] = useState<Position | null>(null)
  const remove = useMutation({
    mutationFn: (p: Position) => api.org.removePosition(p.id),
    onSuccess: (_, p) => { toast(`"${p.title}" unvanı silindi.`); setToDelete(null); qc.invalidateQueries({ queryKey: ['positions'] }) },
    onError: (err) => { toast(errorMessage(err), 'error'); setToDelete(null) },
  })
  return (
    <section className="card overflow-hidden">
      <div className="flex items-center justify-between gap-2 border-b border-gray-100 p-3 dark:border-gray-700/60">
        <p className="text-xs text-gray-500">Seviye 1 en üst kademedir (ör. CEO = 1, Stajyer = 10).</p>
        <button className="btn-violet" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Unvan ekle</button>
      </div>
      {list.isLoading ? <div className="p-4"><BlockSkeleton className="h-40" /></div> : list.isError ? <ErrorState message={errorMessage(list.error)} onRetry={() => list.refetch()} /> : (
        <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {list.data!.map((p) => (
            <li key={p.id} className="flex items-center gap-3 px-4 py-1.5">
              <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-violet-500/10 text-xs font-semibold text-violet-700 dark:text-violet-300" title="Seviye">{p.level}</span>
              <span className="min-w-0 flex-1 text-sm"><span className="font-medium text-gray-800 dark:text-gray-100">{p.title}</span><span className="ml-2 text-xs text-gray-500">{p.memberCount} kişi</span></span>
              <IconButton label={`${p.title} unvanını düzenle`} onClick={() => setEditing(p)}><Pencil className="size-4" /></IconButton>
              <IconButton label={`${p.title} unvanını sil`} tone="danger" onClick={() => setToDelete(p)}><Trash2 className="size-4" /></IconButton>
            </li>
          ))}
        </ul>
      )}
      {editing && <PositionPanel item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog open={toDelete != null} title={`"${toDelete?.title}" unvanı silinsin mi?`} body="Bu unvanı taşıyan çalışan varsa silinemez."
        confirmLabel="Unvanı sil" pending={remove.isPending} onConfirm={() => toDelete && remove.mutate(toDelete)} onCancel={() => setToDelete(null)} />
    </section>
  )
}

function PositionPanel({ item, onClose }: { item: Position | null; onClose: () => void }) {
  const qc = useQueryClient()
  const form = useForm({ title: item?.title ?? '', level: String(item?.level ?? 8), description: item?.description ?? '' }, {
    title: (v) => (!v.trim() ? 'Unvanı girin.' : undefined),
    level: (v) => (!/^\d+$/.test(v) || Number(v) < 1 || Number(v) > 20 ? '1 ile 20 arasında bir seviye girin.' : undefined),
  })
  const save = useMutation({
    mutationFn: () => api.org.savePosition(item?.id ?? null, { title: form.values.title, level: Number(form.values.level), description: form.values.description || null }),
    onSuccess: () => { toast(item ? 'Unvan güncellendi.' : 'Unvan eklendi.'); qc.invalidateQueries({ queryKey: ['positions'] }); onClose() },
    onError: (err) => { form.setServerErrors(fieldErrors(err)); toast(errorMessage(err), 'error') },
  })
  const submit = () => !save.isPending && form.validateAll() && save.mutate()
  return (
    <SlideOver open onClose={onClose} title={item ? 'Unvanı düzenle' : 'Yeni unvan'}
      footer={<><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" onClick={submit} aria-busy={save.isPending}>{save.isPending && <Spinner />} Kaydet</button></>}>
      <form onSubmit={(e) => { e.preventDefault(); submit() }} noValidate className="space-y-4">
        <Field label="Unvan" htmlFor="f-title" error={form.errors.title} required><input {...form.field('title')} className="form-field" maxLength={100} /></Field>
        <Field label="Seviye" htmlFor="f-level" error={form.errors.level} hint="1 = en üst kademe"><input {...form.field('level')} type="number" min={1} max={20} className="form-field" /></Field>
        <Field label="Açıklama" htmlFor="f-description" optional><input {...form.field('description')} className="form-field" maxLength={300} /></Field>
      </form>
    </SlideOver>
  )
}
