import { useRef, useState } from 'react'
import { Link } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { CheckCircle2, Download, FileUp, XCircle } from 'lucide-react'
import { api, errorMessage } from '@/lib/api'
import { employmentLabels } from '@/lib/labels'
import { formatDate } from '@/lib/format'
import type { AccountImportResult } from '@/lib/types'
import { toast } from '@/stores/ui'
import { SlideOver, Spinner } from './ui'

/**
 * CSV ile toplu çalışan ekleme. Kapalı portal kuralı: dosya HESAP AÇMAZ; her geçerli satır için BT/Admin onayına
 * düşen bir hesap talebi oluşur. Önce önizleme (sunucu doğrular, hiçbir şey kaydetmez), sonra onayla gönderilir.
 */
export function CsvImportPanel({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<AccountImportResult | null>(null)
  const [done, setDone] = useState<AccountImportResult | null>(null)

  const reset = () => {
    setFile(null)
    setPreview(null)
    setDone(null)
    if (input.current) input.current.value = ''
  }
  const close = () => {
    reset()
    onClose()
  }

  const check = useMutation({
    mutationFn: (f: File) => api.accountRequests.importCsv(f, true),
    onSuccess: setPreview,
    onError: (err) => { setPreview(null); toast(errorMessage(err), 'error') },
  })
  const send = useMutation({
    mutationFn: (f: File) => api.accountRequests.importCsv(f, false),
    onSuccess: (r) => {
      setDone(r)
      setPreview(null)
      toast(`${r.createdCount} hesap talebi oluşturuldu ve onaya gönderildi.`)
      for (const key of ['account-requests', 'departments', 'positions', 'pending-requests']) qc.invalidateQueries({ queryKey: [key] })
    },
    onError: (err) => toast(errorMessage(err), 'error'),
  })

  const downloadTemplate = async () => {
    try {
      const blob = await api.accountRequests.importTemplate()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = 'calisan-hesap-sablonu.csv'
      a.click()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (err) {
      toast(errorMessage(err), 'error')
    }
  }

  const pick = (f: File | null) => {
    setFile(f)
    setPreview(null)
    setDone(null)
    if (f) check.mutate(f)
  }

  const result = done ?? preview
  const invalid = result ? result.rows.filter((r) => !r.valid).length : 0
  // Binlerce satırda sayfa ağırlaşmasın: hatalı satırların hepsi, geçerlilerin ilk 200'ü gösterilir.
  const shownValid = 200
  const rows = result ? [...result.rows.filter((r) => !r.valid), ...result.rows.filter((r) => r.valid).slice(0, shownValid)] : []
  const hidden = result ? result.rows.length - rows.length : 0

  return (
    <SlideOver
      wide
      open={open}
      onClose={close}
      title="CSV ile çalışan ekle"
      description="Her satır, Bilgi Teknolojileri onayına giden bir hesap talebi olur. Hesaplar onaydan sonra açılır ve çalışana ilk şifre bağlantısı gönderilir."
      footer={
        done ? (
          <div className="flex flex-wrap justify-end gap-2">
            <button className="btn-secondary" onClick={reset}>Başka dosya yükle</button>
            <Link className="btn-violet" to="/account-requests" onClick={close}>Hesap taleplerini aç</Link>
          </div>
        ) : (
          <div className="flex flex-wrap justify-end gap-2">
            <button className="btn-secondary" onClick={close}>Vazgeç</button>
            <button className="btn-violet" disabled={!file || !preview || preview.validCount === 0 || send.isPending} onClick={() => file && send.mutate(file)}>
              {send.isPending && <Spinner />}
              {preview ? `${preview.validCount} talebi gönder` : 'Talepleri gönder'}
            </button>
          </div>
        )
      }
    >
      <div className="space-y-5">
        <ol className="space-y-1 text-sm text-gray-600 dark:text-gray-400">
          <li>1. Şablonu indirin, Excel'de doldurun ve <strong>CSV UTF-8</strong> olarak kaydedin.</li>
          <li>2. Dosyayı seçin; satırlar kaydedilmeden önce kontrol edilir.</li>
          <li>3. Hatalı satırları düzeltin ya da yalnızca geçerli satırları gönderin.</li>
        </ol>
        <p className="text-xs text-gray-500">
          Zorunlu sütunlar: Ad Soyad, E-posta, Personel Kodu, Departman, Unvan. İsteğe bağlı: Personel Türü, İşe Başlama (gg.aa.yyyy), Telefon, Not.
          Ayırıcı noktalı virgül veya virgül olabilir; en fazla 5.000 satır (boş satırlar sayılmaz).
        </p>
        <div className="flex flex-wrap gap-2">
          <button className="btn-secondary" onClick={downloadTemplate}><Download className="size-4" aria-hidden="true" /> Şablonu indir</button>
          <label className="btn-secondary">
            <FileUp className="size-4" aria-hidden="true" /> {file ? 'Başka dosya seç' : 'CSV dosyası seç'}
            <input ref={input} type="file" accept=".csv,text/csv" className="sr-only" onChange={(e) => pick(e.target.files?.[0] ?? null)} />
          </label>
        </div>
        {file && <p className="text-sm text-gray-700 dark:text-gray-300">Seçilen dosya: <span className="font-medium">{file.name}</span></p>}
        {check.isPending && <p className="flex items-center gap-2 text-sm text-gray-500"><Spinner /> Dosya kontrol ediliyor…</p>}

        {result && (
          <div className="space-y-3">
            <div className={clsx('rounded-lg p-3 text-sm', done ? 'bg-emerald-50 text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-300' : 'bg-gray-50 text-gray-700 dark:bg-gray-900/40 dark:text-gray-300')} role="status">
              {done
                ? <>{done.createdCount} hesap talebi oluşturuldu. {invalid > 0 && `${invalid} satır hatalı olduğu için atlandı.`}</>
                : <>{result.total} satırdan {result.validCount} tanesi gönderilmeye hazır{invalid > 0 && `, ${invalid} satır hatalı (gönderilmez)`}.</>}
              {result.newDepartments.length > 0 && <div className="mt-1">Yeni departman{done ? ' oluşturuldu' : ' oluşturulacak'}: {result.newDepartments.join(', ')}</div>}
              {result.newPositions.length > 0 && <div className="mt-1">Yeni unvan{done ? ' oluşturuldu' : ' oluşturulacak'}: {result.newPositions.join(', ')}</div>}
            </div>
            <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700/60">
              <table className="w-full text-sm">
                <thead className="table-head">
                  <tr><th className="px-3 py-2">Satır</th><th className="px-3 py-2">Çalışan</th><th className="px-3 py-2">Departman / unvan</th><th className="px-3 py-2">Durum</th></tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.line} className="border-t border-gray-100 align-top dark:border-gray-700/60">
                      <td className="px-3 py-2 text-gray-500 tabular-nums">{r.line}</td>
                      <td className="px-3 py-2">
                        <div className="font-medium text-gray-800 dark:text-gray-100">{r.fullName || '—'}</div>
                        <div className="text-xs text-gray-500">{r.email || '—'}{r.employeeNumber && ` · ${r.employeeNumber}`}</div>
                      </td>
                      <td className="px-3 py-2 text-xs">
                        <div>{r.department ?? '—'}</div>
                        <div className="text-gray-500">{r.position ?? '—'} · {employmentLabels[r.employmentType]}{r.hireDate && ` · ${formatDate(r.hireDate)}`}</div>
                      </td>
                      <td className="px-3 py-2">
                        {r.valid ? (
                          <span className="inline-flex items-center gap-1 text-emerald-600 dark:text-emerald-400"><CheckCircle2 className="size-4" aria-hidden="true" />{r.requestId ? 'Talep açıldı' : 'Hazır'}</span>
                        ) : (
                          <div className="text-rose-600 dark:text-rose-400">
                            <span className="inline-flex items-center gap-1 font-medium"><XCircle className="size-4" aria-hidden="true" />Hatalı</span>
                            <ul className="mt-1 list-disc pl-4 text-xs">{r.errors.map((e) => <li key={e}>{e}</li>)}</ul>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {hidden > 0 && <p className="text-xs text-gray-500">Hatalı satırların hepsi listelendi; {hidden} geçerli satır daha var (listede gösterilmiyor, gönderime dahil).</p>}
          </div>
        )}
      </div>
    </SlideOver>
  )
}
