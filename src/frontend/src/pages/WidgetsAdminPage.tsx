import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil } from 'lucide-react'
import { errorMessage, fieldErrors } from '@/lib/api'
import { portalApi, sizeLabels, type WidgetAdmin, type WidgetSize } from '@/lib/portal'
import { toast } from '@/stores/ui'
import { Badge, BlockSkeleton, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'

/* =========================================================
   Kutucuk (widget) yönetimi: şirket genelinde açık/kapalı, sıra, boyut ve hangi rollerin göreceği.
   Kutucuğun gerektirdiği izin burada değiştirilemez (güvenlik); yalnızca daraltılabilir.
   ========================================================= */

export function WidgetsAdminPage() {
  const q = useQuery({ queryKey: ['admin', 'widgets'], queryFn: portalApi.widgets })
  const [editing, setEditing] = useState<WidgetAdmin | null>(null)
  const roleName = (n: string) => q.data?.roles.find((r) => r.name === n)?.displayName ?? n

  return (
    <>
      <PageHeader title="Kutucuk yönetimi"
        description="Portal ana sayfasındaki kutucukların şirket geneli ayarı. Kullanıcılar kendi panellerinde sırayı değiştirip gizleyebilir; burada kapatılan kutucuğu kimse göremez." />
      <section className="card overflow-hidden">
        {q.isLoading ? <div className="p-4"><BlockSkeleton className="h-64" /></div> : q.isError ? <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="table-head"><tr>
                <th className="px-4 py-3">Sıra</th><th className="px-4 py-3">Kutucuk</th><th className="px-4 py-3">Boyut</th><th className="px-4 py-3">Kimler görür</th><th className="px-4 py-3">Durum</th><th className="px-4 py-3"><span className="sr-only">İşlemler</span></th>
              </tr></thead>
              <tbody>
                {q.data!.widgets.map((w) => (
                  <tr key={w.key} className="border-b border-gray-100 last:border-0 dark:border-gray-700/60">
                    <td className="px-4 py-2 text-gray-500 tabular-nums">{w.sortOrder}</td>
                    <td className="px-4 py-2">
                      <div className="font-medium text-gray-800 dark:text-gray-100">{w.title}</div>
                      <div className="text-xs text-gray-500">{w.category} · {w.description}</div>
                    </td>
                    <td className="px-4 py-2 text-xs whitespace-nowrap">{sizeLabels[w.size]}</td>
                    <td className="px-4 py-2 text-xs">
                      {w.roles.length ? w.roles.map(roleName).join(', ') : 'Herkes'}
                      {w.requiredAnyPermission.length > 0 && <div className="text-gray-500">İzin şartı: {w.requiredAnyPermission.join(' veya ')}</div>}
                    </td>
                    <td className="px-4 py-2"><Badge className={w.isEnabled ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400' : 'bg-gray-500/15 text-gray-600 dark:text-gray-400'}>{w.isEnabled ? 'Açık' : 'Kapalı'}</Badge></td>
                    <td className="px-4 py-2 text-right"><IconButton label={`${w.title} kutucuğunu düzenle`} onClick={() => setEditing(w)}><Pencil className="size-4" /></IconButton></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      {editing && q.data && <WidgetForm widget={editing} roles={q.data.roles} onClose={() => setEditing(null)} />}
    </>
  )
}

function WidgetForm({ widget, roles, onClose }: { widget: WidgetAdmin; roles: { name: string; displayName: string }[]; onClose: () => void }) {
  const qc = useQueryClient()
  const [f, setF] = useState({ isEnabled: widget.isEnabled, sortOrder: widget.sortOrder, size: widget.size, roles: widget.roles })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useMutation({
    mutationFn: () => portalApi.saveWidget(widget.key, f),
    onSuccess: () => {
      toast('Kutucuk ayarı kaydedildi.')
      qc.invalidateQueries({ queryKey: ['admin', 'widgets'] })
      qc.invalidateQueries({ queryKey: ['dashboard', 'layout'] })
      onClose()
    },
    onError: (e) => { setErrors(fieldErrors(e)); toast(errorMessage(e), 'error') },
  })
  const toggleRole = (n: string) => setF((p) => ({ ...p, roles: p.roles.includes(n) ? p.roles.filter((x) => x !== n) : [...p.roles, n] }))
  return (
    <SlideOver open onClose={onClose} title={widget.title} description={widget.description}
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button><button className="btn-violet" disabled={save.isPending} onClick={() => save.mutate()}>{save.isPending && <Spinner />}Ayarı kaydet</button></div>}>
      <div className="space-y-5">
        <label className="flex min-h-11 cursor-pointer items-start gap-3">
          <input type="checkbox" className="mt-1 size-4 accent-violet-500" checked={f.isEnabled} onChange={(e) => setF((p) => ({ ...p, isEnabled: e.target.checked }))} />
          <span><span className="text-sm font-medium text-gray-800 dark:text-gray-200">Açık</span><span className="block text-xs text-gray-500">Kapalı kutucuk hiçbir kullanıcının panelinde görünmez.</span></span>
        </label>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Varsayılan sıra" htmlFor="w-order" hint="Küçük sayı önce. Kullanıcının kendi sırası bunu geçersiz kılar." error={errors.SortOrder ?? errors.sortOrder}>
            <input id="w-order" type="number" min={0} max={10000} className="form-field" value={f.sortOrder} onChange={(e) => setF((p) => ({ ...p, sortOrder: Number(e.target.value) }))} />
          </Field>
          <Field label="Boyut" htmlFor="w-size" hint={`Varsayılan: ${sizeLabels[widget.defaultSize]}`}>
            <select id="w-size" className="form-field" value={f.size} onChange={(e) => setF((p) => ({ ...p, size: e.target.value as WidgetSize }))}>
              {(Object.keys(sizeLabels) as WidgetSize[]).map((s) => <option key={s} value={s}>{sizeLabels[s]}</option>)}
            </select>
          </Field>
        </div>
        <fieldset>
          <legend className="form-label">Kimler görür?</legend>
          <p className="mb-2 text-xs text-gray-500">Hiçbiri seçilmezse herkes görür. Seçilirse yalnızca bu rollerden birine sahip olanlar görür.
            {widget.requiredAnyPermission.length > 0 && ` Her durumda şu izinlerden biri gerekir: ${widget.requiredAnyPermission.join(', ')}.`}</p>
          <div className="grid gap-1 sm:grid-cols-2">
            {roles.map((r) => (
              <label key={r.name} className="flex min-h-11 cursor-pointer items-center gap-2 rounded-lg px-2 text-sm hover:bg-gray-50 dark:hover:bg-gray-700/30">
                <input type="checkbox" className="size-4 accent-violet-500" checked={f.roles.includes(r.name)} onChange={() => toggleRole(r.name)} />
                {r.displayName}
              </label>
            ))}
          </div>
        </fieldset>
        <button type="button" className="text-xs font-medium text-violet-600 dark:text-violet-400"
          onClick={() => setF({ isEnabled: true, sortOrder: widget.defaultOrder, size: widget.defaultSize, roles: [] })}>Varsayılan ayarlara dön</button>
      </div>
    </SlideOver>
  )
}
