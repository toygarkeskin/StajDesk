import { useDeferredValue, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronDown, ChevronUp, Pencil, Plus, Trash2 } from 'lucide-react'
import { api, errorMessage } from '@/lib/api'
import { approverTypeLabels, workApi, type ApproverType, type WorkflowDefinition } from '@/lib/work'
import { toast } from '@/stores/ui'
import { Badge, BlockSkeleton, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'

/* =========================================================
   Onay zincirleri (workflows.manage): talep türü başına sıralı adımlar ve onaylayıcılar.
   Değişiklik yalnızca YENİ talepleri etkiler; başlamış talepler başladığı zincirle biter.
   ========================================================= */

interface StepDraft { name: string; approverType: ApproverType; roleName: string; userId: number | null; userName: string; departmentId: string }

export function WorkflowsAdminPage() {
  const q = useQuery({ queryKey: ['workflows'], queryFn: workApi.workflows })
  const roles = useQuery({ queryKey: ['workflows', 'roles'], queryFn: workApi.workflowRoles })
  const [edit, setEdit] = useState<WorkflowDefinition | null>(null)
  const roleName = (n?: string | null) => roles.data?.find((r) => r.name === n)?.displayName ?? n ?? ''
  return (
    <>
      <PageHeader title="Onay zincirleri" description="Her talep türünün hangi sırayla, kimler tarafından onaylanacağını belirleyin. Değişiklik yalnızca yeni talepleri etkiler." />
      {q.isLoading ? <BlockSkeleton className="h-48" /> : q.isError ? <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : (
        <div className="grid gap-4 lg:grid-cols-2">
          {q.data!.map((d) => (
            <section key={d.id} className="card p-5" aria-label={d.name}>
              <div className="mb-3 flex items-start justify-between gap-2">
                <div>
                  <h2 className="text-base font-semibold">{d.name} {!d.isActive && <Badge className="bg-gray-500/15 text-gray-600 dark:text-gray-400">Pasif</Badge>}</h2>
                  {d.description && <p className="text-sm text-gray-500">{d.description}</p>}
                </div>
                <IconButton label={`${d.name} zincirini düzenle`} onClick={() => setEdit(d)}><Pencil className="size-4" /></IconButton>
              </div>
              {d.steps.length === 0 ? <p className="text-sm text-gray-500">Adım yok: talepler doğrudan onaylanır.</p> : (
                <ol className="space-y-2">
                  {d.steps.map((s) => (
                    <li key={s.id} className="flex items-start gap-3 rounded-lg bg-gray-50 p-3 text-sm dark:bg-gray-900/40">
                      <span className="grid size-6 shrink-0 place-items-center rounded-full bg-violet-500/15 text-xs font-semibold text-violet-700 dark:text-violet-300">{s.order}</span>
                      <span>
                        <span className="block font-medium text-gray-800 dark:text-gray-100">{s.name}</span>
                        <span className="block text-xs text-gray-500">
                          {approverTypeLabels[s.approverType]}
                          {s.approverType === 'Role' && `: ${roleName(s.roleName)}`}
                          {s.approverType === 'User' && `: ${s.userName ?? '—'}`}
                          {s.approverType === 'DepartmentManager' && (s.departmentName ? `: ${s.departmentName}` : ' (talep sahibinin departmanı)')}
                        </span>
                      </span>
                    </li>
                  ))}
                </ol>
              )}
              <p className="mt-3 text-xs text-gray-500">Onaylayıcısı bulunamayan veya talep sahibinin kendisine düşen adım atlanır; ardışık iki adım aynı kişiye düşerse kişi bir kez onaylar.</p>
            </section>
          ))}
        </div>
      )}
      {edit && <WorkflowForm def={edit} roles={roles.data ?? []} onClose={() => setEdit(null)} />}
    </>
  )
}

function WorkflowForm({ def, roles, onClose }: { def: WorkflowDefinition; roles: { name: string; displayName: string }[]; onClose: () => void }) {
  const qc = useQueryClient()
  const departments = useQuery({ queryKey: ['departments'], queryFn: api.org.departments })
  const [name, setName] = useState(def.name)
  const [description, setDescription] = useState(def.description ?? '')
  const [isActive, setIsActive] = useState(def.isActive)
  const [steps, setSteps] = useState<StepDraft[]>(def.steps.map((s) => ({
    name: s.name, approverType: s.approverType, roleName: s.roleName ?? '', userId: s.userId ?? null, userName: s.userName ?? '', departmentId: s.departmentId ? String(s.departmentId) : '',
  })))
  const set = (i: number, patch: Partial<StepDraft>) => setSteps((l) => l.map((s, j) => (j === i ? { ...s, ...patch } : s)))
  const move = (i: number, d: -1 | 1) => setSteps((l) => { const j = i + d; if (j < 0 || j >= l.length) return l; const n = [...l]; [n[i], n[j]] = [n[j]!, n[i]!]; return n })
  const save = useMutation({
    mutationFn: () => workApi.saveWorkflow(def.id, {
      name, description: description || null, isActive,
      steps: steps.map((s) => ({ name: s.name, approverType: s.approverType, roleName: s.approverType === 'Role' ? s.roleName : null,
        userId: s.approverType === 'User' ? s.userId : null, departmentId: s.approverType === 'DepartmentManager' && s.departmentId ? Number(s.departmentId) : null })),
    }),
    onSuccess: () => { toast('Onay zinciri kaydedildi. Yeni talepler bu zincirle ilerler.'); qc.invalidateQueries({ queryKey: ['workflows'] }); onClose() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const invalid = !name.trim() || steps.some((s) => !s.name.trim() || (s.approverType === 'Role' && !s.roleName) || (s.approverType === 'User' && !s.userId))
  return (
    <SlideOver wide open onClose={onClose} title={`${def.name} · onay zinciri`} description="Adımlar yukarıdan aşağıya sırayla işler."
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" disabled={save.isPending || invalid} onClick={() => save.mutate()}>{save.isPending && <Spinner />}Zinciri kaydet</button></div>}>
      <div className="space-y-4">
        <Field label="Ad" htmlFor="wf-name" required><input id="wf-name" className="form-field" maxLength={100} value={name} onChange={(e) => setName(e.target.value)} /></Field>
        <Field label="Açıklama" htmlFor="wf-desc" optional><input id="wf-desc" className="form-field" maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} /></Field>
        <label className="flex min-h-11 items-center gap-3 text-sm"><input type="checkbox" className="size-4 accent-violet-500" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} /> Zincir aktif (pasifse talepler onaysız kabul edilir)</label>
        <ol className="space-y-3">
          {steps.map((s, i) => (
            <li key={i} className="rounded-lg border border-gray-200 p-3 dark:border-gray-700/60">
              <div className="mb-2 flex items-center justify-between">
                <span className="text-sm font-semibold">{i + 1}. adım</span>
                <span className="flex">
                  <IconButton label={`${i + 1}. adımı yukarı taşı`} disabled={i === 0} onClick={() => move(i, -1)}><ChevronUp className="size-4" /></IconButton>
                  <IconButton label={`${i + 1}. adımı aşağı taşı`} disabled={i === steps.length - 1} onClick={() => move(i, 1)}><ChevronDown className="size-4" /></IconButton>
                  <IconButton label={`${i + 1}. adımı sil`} tone="danger" onClick={() => setSteps((l) => l.filter((_, j) => j !== i))}><Trash2 className="size-4" /></IconButton>
                </span>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <Field label="Adım adı" htmlFor={`st-name-${i}`} required><input id={`st-name-${i}`} className="form-field" maxLength={100} value={s.name} onChange={(e) => set(i, { name: e.target.value })} /></Field>
                <Field label="Onaylayıcı" htmlFor={`st-type-${i}`}>
                  <select id={`st-type-${i}`} className="form-field" value={s.approverType} onChange={(e) => set(i, { approverType: e.target.value as ApproverType })}>
                    {(Object.keys(approverTypeLabels) as ApproverType[]).map((t) => <option key={t} value={t}>{approverTypeLabels[t]}</option>)}
                  </select>
                </Field>
                {s.approverType === 'Role' && (
                  <Field label="Rol" htmlFor={`st-role-${i}`} required>
                    <select id={`st-role-${i}`} className="form-field" value={s.roleName} onChange={(e) => set(i, { roleName: e.target.value })}>
                      <option value="">Rol seçin</option>
                      {roles.map((r) => <option key={r.name} value={r.name}>{r.displayName}</option>)}
                    </select>
                  </Field>
                )}
                {s.approverType === 'DepartmentManager' && (
                  <Field label="Departman" htmlFor={`st-dept-${i}`} hint="Boşsa talep sahibinin departmanı. Seçilirse o departmana yönlendirilir.">
                    <select id={`st-dept-${i}`} className="form-field" value={s.departmentId} onChange={(e) => set(i, { departmentId: e.target.value })}>
                      <option value="">Talep sahibinin departmanı</option>
                      {departments.data?.filter((d) => d.isActive).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                    </select>
                  </Field>
                )}
                {s.approverType === 'User' && <UserPicker id={`st-user-${i}`} value={s.userId ? { id: s.userId, fullName: s.userName } : null} onChange={(p) => set(i, { userId: p?.id ?? null, userName: p?.fullName ?? '' })} />}
              </div>
            </li>
          ))}
        </ol>
        <button type="button" className="btn-secondary w-full" disabled={steps.length >= 10}
          onClick={() => setSteps((l) => [...l, { name: 'Yönetici onayı', approverType: 'Manager', roleName: '', userId: null, userName: '', departmentId: '' }])}>
          <Plus className="size-4" aria-hidden="true" /> Adım ekle
        </button>
      </div>
    </SlideOver>
  )
}

function UserPicker({ id, value, onChange }: { id: string; value: { id: number; fullName: string } | null; onChange: (p: { id: number; fullName: string } | null) => void }) {
  const [search, setSearch] = useState('')
  const term = useDeferredValue(search.trim())
  const people = useQuery({ queryKey: ['employees', 'pick', term], queryFn: () => api.employees.list({ search: term, pageSize: 8 }), enabled: term.length >= 2 && !value })
  return (
    <Field label="Kişi" htmlFor={id} required>
      {value ? (
        <div className="flex min-h-11 items-center justify-between rounded-lg border border-gray-200 px-3 dark:border-gray-700/60">
          <span className="text-sm font-medium">{value.fullName}</span>
          <button type="button" className="text-xs font-medium text-violet-600 dark:text-violet-400" onClick={() => onChange(null)}>Değiştir</button>
        </div>
      ) : (
        <div className="relative">
          <input id={id} className="form-field" placeholder="Ad veya e-posta" value={search} onChange={(e) => setSearch(e.target.value)} />
          {people.data && (
            <ul className="absolute z-10 mt-1 max-h-56 w-full overflow-y-auto rounded-lg border border-gray-200 bg-white shadow-lg dark:border-gray-700 dark:bg-gray-800">
              {people.data.items.length === 0 ? <li className="px-3 py-2 text-sm text-gray-500">Eşleşen çalışan yok.</li> : people.data.items.map((p) => (
                <li key={p.id}><button type="button" className="flex min-h-11 w-full flex-col justify-center px-3 text-left hover:bg-gray-50 dark:hover:bg-gray-700/30" onClick={() => onChange({ id: p.id, fullName: p.fullName })}>
                  <span className="text-sm font-medium">{p.fullName}</span><span className="text-xs text-gray-500">{[p.title, p.department?.name].filter(Boolean).join(' · ')}</span>
                </button></li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Field>
  )
}
