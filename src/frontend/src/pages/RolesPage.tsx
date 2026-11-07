import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Lock, Save } from 'lucide-react'
import { api, errorDetails, errorMessage } from '@/lib/api'
import type { Permission } from '@/lib/types'
import { toast } from '@/stores/ui'
import { ErrorState, PageHeader, Spinner, TableSkeleton } from '@/components/ui'
import { UnsavedChangesGuard } from '@/components/UnsavedChangesGuard'

/**
 * Rol × İzin matrisi. Kod rol adına değil izne baktığı için buradaki değişiklik
 * hiçbir kod değişikliği gerektirmeden tüm ekranlara ve API'ye anında yansır.
 */
export function RolesPage() {
  const qc = useQueryClient()
  const data = useQuery({ queryKey: ['roles'], queryFn: api.roles.list })
  const [draft, setDraft] = useState<Record<number, Set<Permission>>>({})

  const groups = useMemo(() => {
    const map = new Map<string, { key: Permission; description: string }[]>()
    for (const p of data.data?.permissions ?? []) map.set(p.group, [...(map.get(p.group) ?? []), p])
    return [...map.entries()]
  }, [data.data])

  const current = (roleId: number, original: Permission[]) => draft[roleId] ?? new Set(original)
  const dirtyIds = Object.keys(draft).map(Number)

  const save = useMutation({
    mutationFn: async () => {
      for (const id of dirtyIds) await api.roles.setPermissions(id, [...draft[id]!])
    },
    onSuccess: () => {
      setDraft({})
      qc.invalidateQueries({ queryKey: ['roles'] })
      toast('Rol izinleri kaydedildi; değişiklik kullanıcılara hemen yansır.')
    },
    onError: (err) => toast(errorMessage(err), 'error'),
  })

  const toggle = (roleId: number, original: Permission[], key: Permission) => {
    const next = new Set(current(roleId, original))
    if (next.has(key)) next.delete(key)
    else next.add(key)
    setDraft({ ...draft, [roleId]: next })
  }

  return (
    <>
      <PageHeader
        title="Roller ve İzinler"
        description="Her rolün hangi işlemleri yapabileceğini belirleyin. Sistem Yöneticisi her zaman tüm izinlere sahiptir."
        actions={dirtyIds.length > 0 && (
          <>
            <button className="btn-secondary" onClick={() => setDraft({})}>Değişiklikleri geri al</button>
            <button className="btn-violet" onClick={() => save.mutate()} aria-busy={save.isPending}>{save.isPending ? <Spinner /> : <Save className="size-4" aria-hidden="true" />} İzinleri kaydet</button>
          </>
        )}
      />
      <section className="card overflow-hidden">
        {data.isLoading ? <TableSkeleton cols={5} /> : data.isError ? (
          <ErrorState message={errorMessage(data.error)} details={errorDetails(data.error)} onRetry={() => data.refetch()} />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="table-head">
                <tr>
                  <th scope="col" className="px-4 py-3">İzin</th>
                  {data.data!.roles.map((r) => (
                    <th key={r.id} scope="col" className="px-4 py-3 text-center">
                      <div>{r.displayName}</div>
                      <div className="font-normal normal-case">{r.userCount} kullanıcı</div>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {groups.map(([group, perms]) => (
                  <GroupRows key={group} group={group} perms={perms} roles={data.data!.roles} current={current} toggle={toggle} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <UnsavedChangesGuard when={dirtyIds.length > 0} title="Kaydedilmemiş izin değişiklikleri silinsin mi?" description="Yaptığınız izin değişiklikleri kaydedilmedi. Sayfadan çıkarsanız kaybolur." confirmLabel="Değişiklikleri sil" />
    </>
  )
}

function GroupRows({ group, perms, roles, current, toggle }: {
  group: string
  perms: { key: Permission; description: string }[]
  roles: { id: number; name: string; displayName: string; permissions: Permission[] }[]
  current: (roleId: number, original: Permission[]) => Set<Permission>
  toggle: (roleId: number, original: Permission[], key: Permission) => void
}) {
  return (
    <>
      <tr className="bg-gray-50/60 dark:bg-gray-900/30">
        <th colSpan={roles.length + 1} scope="rowgroup" className="px-4 py-2 text-left text-xs font-semibold tracking-wide text-gray-500 uppercase">{group}</th>
      </tr>
      {perms.map((p) => (
        <tr key={p.key} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
          <td className="px-4 py-2">
            <div className="text-gray-800 dark:text-gray-100">{p.description}</div>
            <code className="text-xs text-gray-400">{p.key}</code>
          </td>
          {roles.map((r) => {
            const locked = r.name === 'system_admin'
            const checked = locked || current(r.id, r.permissions).has(p.key)
            return (
              <td key={r.id} className="px-4 py-2 text-center">
                <label className="inline-flex size-11 cursor-pointer items-center justify-center">
                  <span className="sr-only">{r.displayName}: {p.description}</span>
                  {locked ? <Lock className="size-4 text-gray-400" aria-label="Değiştirilemez" /> : (
                    <input type="checkbox" className="size-4 accent-violet-500" checked={checked} onChange={() => toggle(r.id, r.permissions, p.key)} />
                  )}
                </label>
              </td>
            )
          })}
        </tr>
      ))}
    </>
  )
}
