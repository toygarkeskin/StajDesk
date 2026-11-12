import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import clsx from 'clsx'
import { assetTypeLabels, colorOf, priorityLabels, typeLabels } from '@/lib/labels'
import { useAuth } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { Paperclip, X } from 'lucide-react'
import { ATTACHMENT_ACCEPT, ATTACHMENT_MAX_MB, formatSize, precheck } from '@/components/Attachments'
import { Field, IconButton, PageHeader, Spinner } from '@/components/ui'
import { UnsavedChangesGuard } from '@/components/UnsavedChangesGuard'

/* Form (Page) kalıbı: dar sütun · etiketler üstte · yapışkan Vazgeç / Kaydet · taslak otomatik kaydı · ayrılırken uyarı */

const empty = { title: '', description: '', categoryId: '', priority: 'Medium', type: 'Incident', assetId: '' }
type Draft = typeof empty

const safeGet = (k: string) => { try { return localStorage.getItem(k) } catch { return null } }
const safeSet = (k: string, v: string) => { try { localStorage.setItem(k, v) } catch { /* depolama kapalı */ } }
const safeDel = (k: string) => { try { localStorage.removeItem(k) } catch { /* depolama kapalı */ } }

export function NewTicketPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const userId = useAuth((s) => s.me?.id)
  const draftKey = `stajdesk-draft-ticket-${userId}`
  const lastCategoryKey = `stajdesk-last-category-${userId}`

  // Taslak varsa geri yükle; yoksa son kullanılan kategoriyi varsayılan yap.
  const initial = useRef<{ values: Draft; restored: boolean }>(null)
  if (!initial.current) {
    const saved = safeGet(draftKey)
    initial.current = saved
      ? { values: { ...empty, ...JSON.parse(saved) }, restored: true }
      : { values: { ...empty, categoryId: safeGet(lastCategoryKey) ?? '' }, restored: false }
  }
  const [restored, setRestored] = useState(initial.current.restored)
  const [submitted, setSubmitted] = useState(false)

  const form = useForm(initial.current.values, {
    title: (v) => (v.trim().length < 5 ? 'En az 5 karakterlik kısa bir başlık yazın.' : undefined),
    description: (v) => (v.trim().length < 10 ? 'Sorunu en az 10 karakterle anlatın: ne oldu, ne zamandan beri?' : undefined),
    categoryId: (v) => (!v ? 'Talebin ilgili olduğu kategoriyi seçin.' : undefined),
  })

  // Her değişiklikte taslağı yerel olarak kaydet (yenileme veya yanlışlıkla ayrılmada veri kaybolmaz).
  useEffect(() => {
    if (form.dirty) safeSet(draftKey, JSON.stringify(form.values))
  }, [form.values, form.dirty, draftKey])

  const categories = useQuery({ queryKey: ['categories'], queryFn: () => api.categories.list() })
  const assets = useQuery({ queryKey: ['assets', 'mine'], queryFn: api.assets.mine })

  // Kategori listesi gelince geçersiz bir varsayılanı temizle; tek kategori varsa onu seç.
  useEffect(() => {
    const list = categories.data
    if (!list) return
    if (form.values.categoryId && !list.some((c) => String(c.id) === form.values.categoryId)) form.setValue('categoryId', '')
    if (!form.values.categoryId && list.length === 1) form.setValue('categoryId', String(list[0].id))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [categories.data])

  const [files, setFiles] = useState<File[]>([])
  const fileInput = useRef<HTMLInputElement>(null)
  const addFiles = (list: FileList | null) => {
    const picked = Array.from(list ?? [])
    const problem = picked.map(precheck).find(Boolean)
    if (problem) return toast(problem, 'error')
    setFiles((prev) => [...prev, ...picked].slice(0, 10))
  }

  const create = useMutation({
    mutationFn: async () => {
      const v = form.values
      const t = await api.tickets.create({ title: v.title, description: v.description, categoryId: Number(v.categoryId), priority: v.priority, type: v.type, assetId: v.assetId ? Number(v.assetId) : null })
      // Talep oluştuktan sonra ekler yüklenir; biri başarısız olsa bile talep kaybolmaz.
      let failed = 0
      for (const f of files) {
        try { await api.tickets.upload(t.id, f) } catch { failed++ }
      }
      if (failed > 0) toast(`${failed} dosya eklenemedi. Talep detayından tekrar ekleyebilirsiniz.`, 'error')
      return t
    },
    onSuccess: (t) => {
      safeDel(draftKey)
      safeSet(lastCategoryKey, form.values.categoryId)
      setSubmitted(true)
      qc.invalidateQueries({ queryKey: ['tickets'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
      toast(`Talebiniz oluşturuldu (#${t.id}). Destek ekibine iletildi.`)
      // Kaydedildikten sonra uyarı çıkmasın diye bir sonraki çizimde yönlendir.
      setTimeout(() => navigate(`/tickets/${t.id}`), 0)
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (!create.isPending && form.validateAll()) create.mutate()
  }

  const discardDraft = () => {
    safeDel(draftKey)
    form.reset({ ...empty, categoryId: safeGet(lastCategoryKey) ?? '' })
    setRestored(false)
  }

  return (
    <>
      <PageHeader title="Yeni Talep" description="Sorununuzu ya da isteğinizi olabildiğince açık biçimde anlatınız." />

      {restored && (
        <div role="status" className="mb-6 flex flex-col gap-2 rounded-xl bg-violet-500/10 px-4 py-2 text-sm text-violet-700 md:flex-row md:items-center md:justify-between dark:text-violet-300">
          <span>Yarım kalan taslağınız geri yüklendi.</span>
          <button type="button" className="min-h-11 font-medium underline" onClick={discardDraft}>Taslağı sil</button>
        </div>
      )}

      <form onSubmit={submit} noValidate className="grid grid-cols-12 gap-6">
        <div className="card col-span-12 space-y-5 p-6 md:col-span-8">
          <Field label="Başlık" htmlFor="f-title" error={form.errors.title} required hint="Kısa ve açıklayıcı olsun, ör. “Yazıcı kâğıt sıkıştırıyor”">
            <input {...form.field('title')} className="form-field" maxLength={200} />
          </Field>
          <Field label="Açıklama" htmlFor="f-description" error={form.errors.description} required hint="Ne oldu, ne zamandan beri oluyor, hangi hata mesajını görüyorsunuz?">
            <textarea {...form.field('description')} className="form-field min-h-48" maxLength={5000} />
          </Field>
          <fieldset>
            <legend className="form-label">Kategori <span className="text-rose-500">*</span></legend>
            <div id="f-categoryId" tabIndex={-1} className="grid grid-cols-2 gap-2 outline-none md:grid-cols-3" aria-describedby={form.errors.categoryId ? 'f-categoryId-error' : undefined}>
              {categories.data?.map((c) => {
                const selected = form.values.categoryId === String(c.id)
                return (
                  <label
                    key={c.id}
                    className={clsx(
                      'flex min-h-11 cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-left text-sm transition has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-violet-500',
                      selected
                        ? 'border-violet-500 bg-violet-500/5 text-gray-800 ring-2 ring-violet-500/20 dark:text-gray-100'
                        : form.errors.categoryId ? 'border-rose-300 dark:border-rose-500/50' : 'border-gray-200 hover:border-gray-300 dark:border-gray-700/60 dark:hover:border-gray-600',
                    )}
                  >
                    <input type="radio" name="categoryId" value={c.id} checked={selected} onChange={() => form.setValue('categoryId', String(c.id))} className="sr-only" />
                    <span className={clsx('size-2.5 shrink-0 rounded-full', colorOf(c.color).dot)} aria-hidden="true" />
                    <span className="truncate">{c.name}</span>
                  </label>
                )
              })}
            </div>
            {form.errors.categoryId && <p id="f-categoryId-error" className="mt-1 text-xs text-rose-500">{form.errors.categoryId}</p>}
          </fieldset>
        </div>

        <div className="col-span-12 space-y-6 md:col-span-4">
          <div className="card space-y-5 p-6">
            <Field label="Tür" htmlFor="f-type">
              <select {...form.field('type')} className="form-field">
                {Object.entries(typeLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </Field>
            <Field label="Öncelik" htmlFor="f-priority" hint="Destek ekibi önceliği yeniden değerlendirebilir.">
              <select {...form.field('priority')} className="form-field">
                {Object.entries(priorityLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            </Field>
            <Field label="İlgili cihaz" htmlFor="f-assetId" optional hint={assets.data?.length ? 'Size zimmetli cihazlardan birini seçebilirsiniz.' : 'Size zimmetli cihaz bulunmuyor.'}>
              <select {...form.field('assetId')} className="form-field" disabled={!assets.data?.length}>
                <option value="">Seçilmedi</option>
                {assets.data?.map((a) => <option key={a.id} value={a.id}>{a.name} · {assetTypeLabels[a.type]} ({a.inventoryNumber})</option>)}
              </select>
            </Field>
          </div>
          <div className="card p-6">
            <div className="form-label flex items-center gap-2"><Paperclip className="size-4" aria-hidden="true" /> Ekler <span className="font-normal text-gray-400">(isteğe bağlı)</span></div>
            {files.length > 0 && (
              <ul className="mb-3 space-y-1">
                {files.map((f, i) => (
                  <li key={`${f.name}-${i}`} className="flex items-center gap-2 rounded-lg border border-gray-200 py-0.5 pl-3 text-sm dark:border-gray-700/60">
                    <span className="min-w-0 flex-1 truncate" title={f.name}>{f.name}</span>
                    <span className="shrink-0 text-xs text-gray-500">{formatSize(f.size)}</span>
                    <IconButton label={`${f.name} dosyasını listeden çıkar`} onClick={() => setFiles(files.filter((_, j) => j !== i))}><X className="size-4" /></IconButton>
                  </li>
                ))}
              </ul>
            )}
            <input ref={fileInput} id="f-files" type="file" multiple accept={ATTACHMENT_ACCEPT} className="sr-only" onChange={(e) => { addFiles(e.target.files); e.target.value = '' }} />
            <button type="button" className="btn-secondary w-full" onClick={() => fileInput.current?.click()}>Ekran görüntüsü veya dosya seç</button>
            <p className="mt-2 text-xs text-gray-500">Dosya başına en fazla {ATTACHMENT_MAX_MB} MB · resim, PDF, metin, Word, Excel</p>
          </div>
          <div className="flex gap-2">
            <Link to="/tickets" className="btn-secondary flex-1">Vazgeç</Link>
            <button type="submit" className="btn-violet flex-1" aria-busy={create.isPending}>
              {create.isPending && <Spinner />} {create.isPending ? 'Gönderiliyor…' : 'Talebi gönder'}
            </button>
          </div>
        </div>
      </form>

      <UnsavedChangesGuard
        when={form.dirty && !submitted}
        title="Talep gönderilmeden sayfadan çıkılsın mı?"
        description="Yazdıklarınız bu cihazda taslak olarak saklanır; Yeni talep sayfasına döndüğünüzde kaldığınız yerden devam edebilirsiniz."
        confirmLabel="Sayfadan çık"
      />
    </>
  )
}
