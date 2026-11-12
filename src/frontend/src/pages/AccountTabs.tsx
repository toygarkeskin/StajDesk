import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, ShieldAlert, ShieldCheck, Smartphone } from 'lucide-react'
import { api, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import type { EmployeeDetail } from '@/lib/types'
import { toast } from '@/stores/ui'
import { BlockSkeleton, Field, Spinner } from '@/components/ui'

const passwordRule = (v: string) =>
  v.length < 10 ? 'En az 10 karakterlik bir şifre belirleyin.'
    : !/[A-Z]/.test(v) || !/[a-z]/.test(v) || !/\d/.test(v) ? 'Şifreye en az bir büyük harf, bir küçük harf ve bir rakam ekleyin.'
    : undefined

/** Hesap ve güvenlik (yalnızca kişinin kendisi): giriş bilgileri, şifre değiştirme, SMS ile iki adımlı doğrulama. */
export function AccountTab({ p }: { p: EmployeeDetail }) {
  return (
    <div className="grid gap-6 xl:grid-cols-2">
      <section className="card p-5" aria-labelledby="acc-login">
        <h2 id="acc-login" className="mb-3 text-sm font-semibold">Giriş bilgileri</h2>
        <dl className="space-y-2 text-sm">
          <div className="flex justify-between gap-2"><dt className="text-gray-500">Kullanıcı adı</dt><dd className="font-medium text-gray-800 dark:text-gray-100">{p.username ?? 'Tanımlı değil (İK atayabilir)'}</dd></div>
          <div className="flex justify-between gap-2"><dt className="text-gray-500">E-posta</dt><dd className="truncate font-medium text-gray-800 dark:text-gray-100">{p.email}</dd></div>
          <div className="flex justify-between gap-2"><dt className="text-gray-500">E-posta doğrulaması</dt><dd className={p.emailVerified ? 'font-medium text-emerald-600' : 'font-medium text-amber-600'}>{p.emailVerified ? 'Doğrulandı' : 'Bekliyor'}</dd></div>
        </dl>
        <ChangePassword />
      </section>
      <TwoFactor />
    </div>
  )
}

function ChangePassword() {
  const form = useForm({ current: '', next: '', confirm: '' }, {
    current: (v) => (!v ? 'Mevcut şifrenizi girin.' : undefined),
    next: passwordRule,
    confirm: (v, all) => (v !== all.next ? 'Şifreler eşleşmiyor.' : undefined),
  })
  const save = useMutation({
    mutationFn: () => api.account.changePassword(form.values.current, form.values.next),
    onSuccess: () => {
      toast('Şifreniz değiştirildi.')
      form.reset({ current: '', next: '', confirm: '' })
    },
    onError: (err) => {
      form.setServerErrors(fieldErrors(err))
      toast(errorMessage(err), 'error')
    },
  })
  return (
    <form className="mt-5 space-y-3 border-t border-gray-100 pt-5 dark:border-gray-700/60" noValidate onSubmit={(e) => { e.preventDefault(); if (!save.isPending && form.validateAll()) save.mutate() }}>
      <h3 className="flex items-center gap-2 text-sm font-semibold"><KeyRound className="size-4 text-gray-400" aria-hidden="true" /> Şifre değiştir</h3>
      <Field label="Mevcut şifre" htmlFor="f-current" error={form.errors.current}><input {...form.field('current')} type="password" autoComplete="current-password" className="form-field" /></Field>
      <Field label="Yeni şifre" htmlFor="f-next" error={form.errors.next} hint="En az 10 karakter; büyük harf, küçük harf ve rakam."><input {...form.field('next')} type="password" autoComplete="new-password" className="form-field" /></Field>
      <Field label="Yeni şifre (tekrar)" htmlFor="f-confirm" error={form.errors.confirm}><input {...form.field('confirm')} type="password" autoComplete="new-password" className="form-field" /></Field>
      <div className="flex justify-end"><button type="submit" className="btn-violet" aria-busy={save.isPending}>{save.isPending && <Spinner />} Şifreyi değiştir</button></div>
    </form>
  )
}

/**
 * SMS ile iki adımlı doğrulama. SMS sağlayıcısı yapılandırılmadıysa bunu açıkça söyler;
 * hiçbir zaman "SMS gönderildi" gibi davranmaz.
 */
function TwoFactor() {
  const qc = useQueryClient()
  const security = useQuery({ queryKey: ['me', 'security'], queryFn: api.account.security })
  const [challenge, setChallenge] = useState<{ challengeId: string; maskedPhone: string } | null>(null)
  const [code, setCode] = useState('')
  const [password, setPassword] = useState('')
  const refresh = () => qc.invalidateQueries({ queryKey: ['me', 'security'] })
  const onError = (err: unknown) => toast(errorMessage(err), 'error')
  const start = useMutation({ mutationFn: api.account.start2fa, onSuccess: setChallenge, onError })
  const confirm = useMutation({
    mutationFn: () => api.account.confirm2fa(challenge!.challengeId, code),
    onSuccess: () => { toast('İki adımlı doğrulama açıldı.'); setChallenge(null); setCode(''); refresh() },
    onError,
  })
  const disable = useMutation({
    mutationFn: () => api.account.disable2fa(password),
    onSuccess: () => { toast('İki adımlı doğrulama kapatıldı.'); setPassword(''); refresh() },
    onError,
  })

  if (security.isLoading) return <BlockSkeleton className="h-48" />
  const s = security.data
  if (!s) return null

  return (
    <section className="card p-5" aria-labelledby="acc-2fa">
      <h2 id="acc-2fa" className="mb-1 flex items-center gap-2 text-sm font-semibold">
        {s.twoFactorEnabled ? <ShieldCheck className="size-4 text-emerald-500" aria-hidden="true" /> : <ShieldAlert className="size-4 text-gray-400" aria-hidden="true" />}
        İki adımlı doğrulama (SMS)
      </h2>
      <p className="mb-4 text-sm text-gray-500">Açıkken girişte şifrenize ek olarak telefonunuza gelen tek kullanımlık kod istenir.</p>

      {!s.smsAvailable ? (
        <div role="status" className="rounded-lg bg-amber-500/10 px-3 py-2 text-sm text-amber-800 dark:text-amber-300">
          SMS servisi henüz yapılandırılmadı; bu nedenle iki adımlı doğrulama şu anda açılamıyor. Sistem yöneticisi bir SMS sağlayıcısı bağladığında bu bölüm etkinleşir.
        </div>
      ) : s.twoFactorEnabled ? (
        <form className="space-y-3" noValidate onSubmit={(e: FormEvent) => { e.preventDefault(); if (password && !disable.isPending) disable.mutate() }}>
          <p className="text-sm text-emerald-700 dark:text-emerald-300">Açık · kodlar {s.maskedPhone} numarasına gönderiliyor.</p>
          <Field label="Kapatmak için şifreniz" htmlFor="f-2fa-pass"><input id="f-2fa-pass" type="password" autoComplete="current-password" className="form-field" value={password} onChange={(e) => setPassword(e.target.value)} /></Field>
          <div className="flex justify-end"><button type="submit" className="btn-secondary" aria-busy={disable.isPending} disabled={!password}>İki adımlı doğrulamayı kapat</button></div>
        </form>
      ) : !s.hasPhone ? (
        <p className="text-sm text-gray-600 dark:text-gray-300">Önce "Kişisel bilgiler" sekmesinden cep telefonu numaranızı ekleyin.</p>
      ) : challenge ? (
        <form className="space-y-3" noValidate onSubmit={(e: FormEvent) => { e.preventDefault(); if (/^\d{6}$/.test(code) && !confirm.isPending) confirm.mutate() }}>
          <p className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-300"><Smartphone className="size-4" aria-hidden="true" /> {challenge.maskedPhone} numarasına kod gönderildi.</p>
          <Field label="Doğrulama kodu" htmlFor="f-2fa-code"><input id="f-2fa-code" inputMode="numeric" autoComplete="one-time-code" className="form-field tracking-[0.3em]" value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} /></Field>
          <div className="flex justify-end gap-2">
            <button type="button" className="btn-secondary" onClick={() => setChallenge(null)}>Vazgeç</button>
            <button type="submit" className="btn-violet" aria-busy={confirm.isPending} disabled={code.length !== 6}>Doğrula ve aç</button>
          </div>
        </form>
      ) : (
        <button className="btn-violet" onClick={() => start.mutate()} aria-busy={start.isPending}>{start.isPending && <Spinner />} {s.maskedPhone} numarasına kod gönder</button>
      )}
    </section>
  )
}

/** E-posta bildirim tercihleri. Uygulama içi bildirimler her zaman oluşur. */
export function PreferencesTab() {
  const qc = useQueryClient()
  const settings = useQuery({ queryKey: ['me', 'settings'], queryFn: api.account.settings })
  const save = useMutation({
    mutationFn: api.account.saveSettings,
    onSuccess: (d) => { qc.setQueryData(['me', 'settings'], d); toast('Tercihleriniz kaydedildi.') },
    onError: (err) => toast(errorMessage(err), 'error'),
  })
  if (settings.isLoading || !settings.data) return <BlockSkeleton className="h-32" />
  const s = settings.data
  return (
    <section className="card p-5" aria-labelledby="pref-title">
      <h2 id="pref-title" className="mb-1 text-sm font-semibold">E-posta bildirimleri</h2>
      <p className="mb-4 text-sm text-gray-500">Uygulama içi bildirimler (zil simgesi) her zaman gelir; burada yalnızca e-postaları seçersiniz.</p>
      <label className="flex min-h-11 items-start gap-3 text-sm">
        <input type="checkbox" className="mt-1 size-4 accent-violet-500" checked={s.emailTicketUpdates} disabled={save.isPending}
          onChange={(e) => save.mutate({ ...s, emailTicketUpdates: e.target.checked })} />
        <span><span className="font-medium text-gray-800 dark:text-gray-100">Talep e-postaları</span><br />
          <span className="text-gray-500">Talebim alındığında, durumu değiştiğinde, yanıt yazıldığında veya bana talep atandığında e-posta gönder.</span></span>
      </label>
      <p className="mt-3 text-xs text-gray-500">Şifre sıfırlama ve hesap güvenliği e-postaları bu ayardan etkilenmez.</p>
      <h2 className="mt-6 mb-1 text-sm font-semibold">Gizlilik</h2>
      <label className="flex min-h-11 items-start gap-3 text-sm">
        <input type="checkbox" className="mt-1 size-4 accent-violet-500" checked={s.showBirthday} disabled={save.isPending}
          onChange={(e) => save.mutate({ ...s, showBirthday: e.target.checked })} />
        <span><span className="font-medium text-gray-800 dark:text-gray-100">Doğum günüm portalda görünsün</span><br />
          <span className="text-gray-500">Açarsanız doğum gününüz (yalnızca gün ve ay, yaşınız değil) çalışma arkadaşlarınızın panelindeki "Doğum günleri" kutucuğunda görünür. Kurumsal sitede hiçbir zaman gösterilmez.</span></span>
      </label>
    </section>
  )
}
