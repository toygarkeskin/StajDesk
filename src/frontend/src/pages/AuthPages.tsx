import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router'
import { CheckCircle2, Clock3, KeyRound, MailCheck, ShieldCheck, Smartphone } from 'lucide-react'
import { api, errorCode, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { authorize, challengeOf, completeOtpLogin, createVerifier, loginWithPassword, verifyOtp, type AuthorizeResult, type TokenResponse } from '@/lib/oauth'
import { useAuth } from '@/stores/auth'
import { useSessionBootstrap } from '@/components/layout/AppLayout'
import { Field, Spinner } from '@/components/ui'
import { Logo } from '@/components/layout/Logo'

/* Form (Auth) kalıbı: sol tarafta form, sağda tanıtım paneli. */

function AuthCard({ title, description, children, footer }: { title: string; description: string; children: ReactNode; footer: ReactNode }) {
  return (
    <main className="bg-white dark:bg-gray-900">
      <div className="relative md:flex">
        <div className="md:w-1/2">
          <div className="flex min-h-dvh flex-col after:flex-1">
            <div className="flex-1">
              <div className="flex h-16 items-center px-4 md:px-8"><Logo /></div>
            </div>
            <div className="mx-auto w-full max-w-sm px-4 py-8">
              <h1 className="mb-1 text-3xl font-bold">{title}</h1>
              <p className="mb-6 text-sm text-gray-500">{description}</p>
              {children}
              <div className="mt-6 border-t border-gray-100 pt-5 text-sm dark:border-gray-700/60">{footer}</div>
            </div>
          </div>
        </div>
        <div className="absolute top-0 right-0 bottom-0 hidden md:block md:w-1/2">
          <div className="flex h-full flex-col justify-center bg-linear-to-br from-violet-600 via-violet-500 to-indigo-500 p-12 text-white">
            <h2 className="mb-4 max-w-md text-3xl font-bold text-white">Tüm destek talepleriniz tek bir yerde.</h2>
            <p className="mb-10 max-w-md text-violet-100">StajDesk ile sorunlarınızı bildirin, durumunu anlık takip edin; destek ekibi talepleri önceliğe göre yönetsin.</p>
            <ul className="space-y-4 text-sm">
              <li className="flex items-center gap-3"><CheckCircle2 className="size-5 text-violet-200" aria-hidden="true" /> Talep açma, yorum ve durum takibi</li>
              <li className="flex items-center gap-3"><Clock3 className="size-5 text-violet-200" aria-hidden="true" /> Anlık bildirimler ve e-posta</li>
              <li className="flex items-center gap-3"><ShieldCheck className="size-5 text-violet-200" aria-hidden="true" /> OAuth 2.0 ile güvenli, rol ve izin tabanlı erişim</li>
            </ul>
          </div>
        </div>
      </div>
    </main>
  )
}

const Alert = ({ message, tone = 'error' }: { message: string; tone?: 'error' | 'success' }) => (
  <div role={tone === 'error' ? 'alert' : 'status'} className={tone === 'error' ? 'mb-4 rounded-lg bg-rose-500/10 px-3 py-2 text-sm text-rose-600 dark:text-rose-400' : 'mb-4 rounded-lg bg-emerald-500/10 px-3 py-2 text-sm text-emerald-700 dark:text-emerald-400'}>
    {message}
  </div>
)

const emailRule = (v: string) => (!v.trim() ? 'E-posta adresinizi girin.' : !/^\S+@\S+\.\S+$/.test(v) ? 'ad@sirket.com biçiminde bir e-posta adresi girin.' : undefined)
const passwordRule = (v: string) =>
  v.length < 10 ? 'En az 10 karakterlik bir şifre belirleyin.'
    : !/[A-Z]/.test(v) || !/[a-z]/.test(v) || !/\d/.test(v) ? 'Şifreye en az bir büyük harf, bir küçük harf ve bir rakam ekleyin.'
    : undefined
const link = 'font-medium text-violet-500 hover:text-violet-600'

const loginRule = (v: string) => (!v.trim() ? 'Kullanıcı adınızı veya e-posta adresinizi girin.' : undefined)

/** SMS ile gelen 6 haneli kod (iki adımlı giriş). */
function OtpStep({ maskedPhone, onSubmit, onCancel }: { maskedPhone: string; onSubmit: (code: string) => Promise<void>; onCancel: () => void }) {
  const [code, setCode] = useState('')
  const [alert, setAlert] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (pending) return
    if (!/^\d{6}$/.test(code)) {
      setAlert('Telefonunuza gelen 6 haneli kodu girin.')
      return
    }
    setPending(true)
    setAlert(null)
    try {
      await onSubmit(code)
    } catch (err) {
      setAlert(errorMessage(err))
      setPending(false)
    }
  }
  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      <div className="flex items-center gap-2 rounded-lg bg-violet-500/10 px-3 py-2 text-sm text-violet-700 dark:text-violet-300">
        <Smartphone className="size-4 shrink-0" aria-hidden="true" /> {maskedPhone} numaralı telefona doğrulama kodu gönderildi.
      </div>
      {alert && <Alert message={alert} />}
      <Field label="Doğrulama kodu" htmlFor="f-otp" hint="Kod 5 dakika geçerlidir.">
        <input id="f-otp" value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} inputMode="numeric" autoComplete="one-time-code"
          className="form-field text-center text-lg tracking-[0.4em]" autoFocus />
      </Field>
      <button type="submit" className="btn-violet w-full" aria-busy={pending}>{pending && <Spinner />} Doğrula ve giriş yap</button>
      <button type="button" className="btn-secondary w-full" onClick={onCancel}>Farklı hesapla giriş yap</button>
    </form>
  )
}

/** Giriş: OAuth 2.0 Authorization Code + PKCE (kod ve token değişimi arka planda yapılır). */
export function LoginPage() {
  const restoring = useSessionBootstrap()
  const { me, setToken, setMe } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const [alert, setAlert] = useState<string | null>(params.get('expired') ? 'Oturumun süresi doldu. Devam etmek için yeniden giriş yapın.' : null)
  const [info, setInfo] = useState<string | null>(
    params.get('verified') ? 'E-posta adresiniz doğrulandı. Şimdi giriş yapabilirsiniz.'
      : params.get('reset') ? 'Şifreniz kaydedildi. Yeni şifrenizle giriş yapın.' : null)
  const [pending, setPending] = useState(false)
  const [unverified, setUnverified] = useState(false)
  const [otp, setOtp] = useState<{ challengeId: string; maskedPhone: string; verifier: string } | null>(null)
  const form = useForm({ email: '', password: '' }, { email: loginRule, password: (v) => (!v ? 'Şifrenizi girin.' : undefined) })

  if (restoring) return null
  if (me) return <Navigate to="/" replace />

  const finish = async (token: TokenResponse) => {
    setToken(token.access_token, token.expires_in)
    setMe(await api.auth.me())
    const from = (location.state as { from?: string } | null)?.from
    navigate(from && from !== '/login' ? from : '/', { replace: true })
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (pending || !form.validateAll()) return
    setAlert(null)
    setInfo(null)
    setUnverified(false)
    setPending(true)
    try {
      const res = await loginWithPassword(form.values.email, form.values.password)
      if (res.kind === 'otp') setOtp(res)
      else await finish(res.token)
    } catch (err) {
      setUnverified(errorCode(err) === 'email_not_verified')
      setAlert(errorMessage(err))
    } finally {
      setPending(false)
    }
  }

  const resend = async () => {
    const email = form.values.email.trim()
    if (!email.includes('@')) {
      setAlert('Yeni bağlantı için kullanıcı adı yerine e-posta adresinizi yazıp tekrar deneyin.')
      return
    }
    try {
      setInfo((await api.auth.resendVerification(email)) ?? 'Bağlantı gönderildi.')
      setAlert(null)
      setUnverified(false)
    } catch (err) {
      setAlert(errorMessage(err))
    }
  }

  if (otp) {
    return (
      <AuthCard title="İki adımlı doğrulama" description="Hesabınızı korumak için telefonunuza gelen kodu girin." footer={<Link className={link} to="/forgot-password">Telefonuma erişemiyorum</Link>}>
        <OtpStep maskedPhone={otp.maskedPhone} onCancel={() => setOtp(null)} onSubmit={async (code) => finish(await completeOtpLogin(otp.challengeId, code, otp.verifier))} />
      </AuthCard>
    )
  }

  return (
    <AuthCard title="Tekrar hoş geldiniz" description="Devam etmek için hesabınıza giriş yapın." footer={<>Hesabınız yok mu? <Link className={link} to="/register">Hesap oluşturun</Link></>}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {info && <Alert message={info} tone="success" />}
        {alert && <Alert message={alert} />}
        {unverified && <button type="button" className="btn-secondary w-full" onClick={resend}><MailCheck className="size-4" aria-hidden="true" /> Doğrulama bağlantısını yeniden gönder</button>}
        <Field label="Kullanıcı adı veya e-posta" htmlFor="f-email" error={form.errors.email}>
          <input {...form.field('email')} type="text" autoComplete="username" autoCapitalize="none" spellCheck={false} className="form-field" placeholder="ad.soyad veya ad@sirket.com" />
        </Field>
        <Field label="Şifre" htmlFor="f-password" error={form.errors.password}>
          <input {...form.field('password')} type="password" autoComplete="current-password" className="form-field" />
        </Field>
        <div className="flex justify-end text-sm"><Link className={link} to="/forgot-password">Şifremi unuttum</Link></div>
        <button type="submit" className="btn-violet w-full" aria-busy={pending}>
          {pending && <Spinner />} {pending ? 'Giriş yapılıyor…' : 'Giriş yap'}
        </button>
      </form>
    </AuthCard>
  )
}

export function RegisterPage() {
  const [alert, setAlert] = useState<string | null>(null)
  const [sentTo, setSentTo] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const form = useForm({ fullName: '', email: '', password: '' }, {
    fullName: (v) => (!v.trim() ? 'Adınızı ve soyadınızı girin.' : undefined),
    email: emailRule,
    password: passwordRule,
  })

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (pending || !form.validateAll()) return
    setAlert(null)
    setPending(true)
    try {
      const v = form.values
      await api.auth.register({ fullName: v.fullName, email: v.email, password: v.password })
      setSentTo(v.email)
    } catch (err) {
      form.setServerErrors(fieldErrors(err))
      setAlert(errorMessage(err))
    } finally {
      setPending(false)
    }
  }

  if (sentTo) {
    return (
      <AuthCard title="E-postanızı kontrol edin" description="Hesabınız oluşturuldu. Giriş yapmadan önce e-posta adresinizi doğrulayın." footer={<Link className={link} to="/login">Girişe dön</Link>}>
        <div className="space-y-4">
          <Alert tone="success" message={`${sentTo} adresine bir doğrulama bağlantısı gönderdik. Bağlantı 24 saat geçerlidir.`} />
          <p className="text-sm text-gray-500">Demo ortamında e-postalar <a className={link} href="http://localhost:8025" target="_blank" rel="noreferrer">Mailpit posta kutusuna</a> düşer.</p>
        </div>
      </AuthCard>
    )
  }

  return (
    <AuthCard title="Hesap oluşturun" description="Talep açmak ve takip etmek için kayıt olun. Departman ve unvan bilgileriniz İnsan Kaynakları tarafından girilir." footer={<>Zaten hesabınız var mı? <Link className={link} to="/login">Girişe dön</Link></>}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {alert && <Alert message={alert} />}
        <Field label="Ad soyad" htmlFor="f-fullName" error={form.errors.fullName}>
          <input {...form.field('fullName')} autoComplete="name" className="form-field" />
        </Field>
        <Field label="E-posta" htmlFor="f-email" error={form.errors.email}>
          <input {...form.field('email')} type="email" autoComplete="email" className="form-field" placeholder="ad@sirket.com" />
        </Field>
        <Field label="Şifre" htmlFor="f-password" error={form.errors.password} hint="En az 10 karakter; büyük harf, küçük harf ve rakam içersin.">
          <input {...form.field('password')} type="password" autoComplete="new-password" className="form-field" />
        </Field>
        <button type="submit" className="btn-violet w-full" aria-busy={pending}>
          {pending && <Spinner />} {pending ? 'Hesap oluşturuluyor…' : 'Hesap oluştur'}
        </button>
      </form>
    </AuthCard>
  )
}

/** Şifremi unuttum: e-posta (demo ortamında Mailpit'e) sıfırlama bağlantısı gönderir. */
export function ForgotPasswordPage() {
  const [done, setDone] = useState<string | null>(null)
  const [alert, setAlert] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const form = useForm({ email: '' }, { email: emailRule })

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (pending || !form.validateAll()) return
    setPending(true)
    setAlert(null)
    try {
      setDone((await api.auth.forgotPassword(form.values.email)) ?? 'Bağlantı gönderildi.')
    } catch (err) {
      setAlert(errorMessage(err))
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthCard title="Şifrenizi mi unuttunuz?" description="E-posta adresinizi girin; size bir sıfırlama bağlantısı gönderelim." footer={<Link className={link} to="/login">Girişe dön</Link>}>
      {done ? (
        <div className="space-y-4">
          <Alert message={done} tone="success" />
          <p className="text-sm text-gray-500">Demo ortamında e-postalar <a className={link} href="http://localhost:8025" target="_blank" rel="noreferrer">Mailpit posta kutusuna</a> düşer.</p>
        </div>
      ) : (
        <form onSubmit={submit} noValidate className="space-y-4">
          {alert && <Alert message={alert} />}
          <Field label="E-posta" htmlFor="f-email" error={form.errors.email}>
            <input {...form.field('email')} type="email" autoComplete="email" className="form-field" placeholder="ad@sirket.com" />
          </Field>
          <button type="submit" className="btn-violet w-full" aria-busy={pending}>{pending && <Spinner />} Sıfırlama bağlantısı gönder</button>
        </form>
      )}
    </AuthCard>
  )
}

/** E-postadaki doğrulama bağlantısı: açılınca otomatik doğrular ve girişe yönlendirir. */
export function VerifyEmailPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const token = params.get('token') ?? ''
  const [error, setError] = useState<string | null>(token ? null : 'Bağlantı eksik veya hatalı. E-postadaki bağlantıyı yeniden açın.')
  const started = useRef(false)
  useEffect(() => {
    if (!token || started.current) return
    started.current = true
    api.auth.verifyEmail(token)
      .then(() => navigate('/login?verified=1', { replace: true }))
      .catch((err) => setError(errorMessage(err)))
  }, [token, navigate])
  return (
    <AuthCard title="E-posta doğrulama" description="Adresiniz doğrulanıyor…" footer={<Link className={link} to="/login">Girişe dön</Link>}>
      {error ? <Alert message={error} /> : <p className="flex items-center gap-2 text-sm text-gray-500"><Spinner /> Lütfen bekleyin…</p>}
    </AuthCard>
  )
}

/** Davet e-postasındaki bağlantı: İK'nın oluşturduğu hesap için ilk şifre belirlenir. */
export function SetPasswordPage() {
  return <ResetPasswordPage invitation />
}

export function ResetPasswordPage({ invitation = false }: { invitation?: boolean }) {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const token = params.get('token') ?? ''
  const [alert, setAlert] = useState<string | null>(token ? null : 'Bağlantı eksik veya hatalı. E-postadaki bağlantıyı yeniden açın.')
  const [pending, setPending] = useState(false)
  const form = useForm({ password: '', confirm: '' }, {
    password: passwordRule,
    confirm: (v, all) => (v !== all.password ? 'Şifreler eşleşmiyor.' : undefined),
  })

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (pending || !form.validateAll()) return
    setPending(true)
    setAlert(null)
    try {
      await api.auth.resetPassword(token, form.values.password)
      navigate('/login?reset=1', { replace: true })
    } catch (err) {
      form.setServerErrors(fieldErrors(err))
      setAlert(errorMessage(err))
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthCard
      title={invitation ? 'StajDesk\'e hoş geldiniz' : 'Yeni şifre belirleyin'}
      description={invitation ? 'Hesabınız İnsan Kaynakları tarafından oluşturuldu. Giriş yapabilmek için şifrenizi belirleyin.' : 'Şifreniz değişince açık tüm oturumlarınız güvenlik için kapatılır.'}
      footer={<Link className={link} to="/login">Girişe dön</Link>}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {alert && <Alert message={alert} />}
        <Field label="Yeni şifre" htmlFor="f-password" error={form.errors.password} hint="En az 10 karakter; büyük harf, küçük harf ve rakam içersin.">
          <input {...form.field('password')} type="password" autoComplete="new-password" className="form-field" />
        </Field>
        <Field label="Yeni şifre (tekrar)" htmlFor="f-confirm" error={form.errors.confirm}>
          <input {...form.field('confirm')} type="password" autoComplete="new-password" className="form-field" />
        </Field>
        <button type="submit" className="btn-violet w-full" aria-busy={pending} disabled={!token}>{pending && <Spinner />} {invitation ? 'Şifremi belirle' : 'Şifreyi güncelle'}</button>
      </form>
    </AuthCard>
  )
}

/**
 * Harici OAuth istemcileri için yetkilendirme ekranı (RFC 6749 §4.1):
 * /oauth/authorize?response_type=code&client_id=..&redirect_uri=..&code_challenge=..&code_challenge_method=S256&state=..
 * Kullanıcı giriş yapınca istemcinin redirect_uri adresine ?code=..&state=.. ile yönlendirilir.
 */
export function OAuthAuthorizePage() {
  const [params] = useSearchParams()
  const clientId = params.get('client_id') ?? ''
  const redirect = params.get('redirect_uri') ?? ''
  const valid = params.get('response_type') === 'code' && clientId && redirect && params.get('code_challenge') && params.get('code_challenge_method') === 'S256'
  const [alert, setAlert] = useState<string | null>(valid ? null : 'Yetkilendirme isteği eksik: response_type=code, client_id, redirect_uri ve S256 code_challenge gereklidir.')
  const [pending, setPending] = useState(false)
  const [otp, setOtp] = useState<{ challengeId: string; maskedPhone: string } | null>(null)
  const form = useForm({ email: '', password: '' }, { email: loginRule, password: (v) => (!v ? 'Şifrenizi girin.' : undefined) })
  const state = params.get('state') ?? undefined

  const redirectWith = (res: AuthorizeResult) => {
    const target = new URL(res.redirectUri)
    target.searchParams.set('code', res.code!)
    if (state) target.searchParams.set('state', state)
    window.location.assign(target.toString())
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!valid || pending || !form.validateAll()) return
    setPending(true)
    setAlert(null)
    try {
      const res = await authorize({ ...form.values, clientId, redirectUri: redirect, codeChallenge: params.get('code_challenge')!, state, scope: params.get('scope') ?? undefined })
      if (res.otpRequired && res.challengeId) {
        setOtp({ challengeId: res.challengeId, maskedPhone: res.maskedPhone ?? '' })
        setPending(false)
      } else redirectWith(res)
    } catch (err) {
      setAlert(errorMessage(err))
      setPending(false)
    }
  }

  if (otp) {
    return (
      <AuthCard title="İki adımlı doğrulama" description={`"${clientId}" uygulamasına izin vermeden önce telefonunuza gelen kodu girin.`} footer={<Link className={link} to="/login">StajDesk'e dön</Link>}>
        <OtpStep maskedPhone={otp.maskedPhone} onCancel={() => setOtp(null)} onSubmit={async (code) => redirectWith(await verifyOtp(otp.challengeId, code))} />
      </AuthCard>
    )
  }

  return (
    <AuthCard title="Uygulamaya erişim izni" description={`"${clientId}" uygulaması StajDesk hesabınızla oturum açmak istiyor.`} footer={<Link className={link} to="/login">StajDesk'e dön</Link>}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {alert && <Alert message={alert} />}
        <div className="flex items-center gap-2 rounded-lg bg-violet-500/10 px-3 py-2 text-sm text-violet-700 dark:text-violet-300"><KeyRound className="size-4" aria-hidden="true" /> Yönlendirilecek adres: {redirect || '—'}</div>
        <Field label="Kullanıcı adı veya e-posta" htmlFor="f-email" error={form.errors.email}>
          <input {...form.field('email')} type="text" autoComplete="username" autoCapitalize="none" className="form-field" />
        </Field>
        <Field label="Şifre" htmlFor="f-password" error={form.errors.password}>
          <input {...form.field('password')} type="password" autoComplete="current-password" className="form-field" />
        </Field>
        <button type="submit" className="btn-violet w-full" aria-busy={pending} disabled={!valid}>{pending && <Spinner />} Giriş yap ve izin ver</button>
      </form>
    </AuthCard>
  )
}

// Kullanılmayan dışa aktarım uyarısını önlemek için (PKCE yardımcıları başka sayfalarda da kullanılabilir)
export const _pkce = { createVerifier, challengeOf }
