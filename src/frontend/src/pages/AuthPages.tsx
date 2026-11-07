import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router'
import { CheckCircle2, Clock3, KeyRound, ShieldCheck } from 'lucide-react'
import { api, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { authorize, challengeOf, createVerifier, loginWithPassword } from '@/lib/oauth'
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

/** Giriş: OAuth 2.0 Authorization Code + PKCE (kod ve token değişimi arka planda yapılır). */
export function LoginPage() {
  const restoring = useSessionBootstrap()
  const { me, setToken, setMe } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const [alert, setAlert] = useState<string | null>(params.get('expired') ? 'Oturumun süresi doldu. Devam etmek için yeniden giriş yapın.' : null)
  const [info] = useState<string | null>(params.get('registered') ? 'Hesabınız oluşturuldu. Şimdi giriş yapabilirsiniz.' : params.get('reset') ? 'Şifreniz güncellendi. Yeni şifrenizle giriş yapın.' : null)
  const [pending, setPending] = useState(false)
  const form = useForm({ email: '', password: '' }, { email: emailRule, password: (v) => (!v ? 'Şifrenizi girin.' : undefined) })

  if (restoring) return null
  if (me) return <Navigate to="/" replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (pending || !form.validateAll()) return
    setAlert(null)
    setPending(true)
    try {
      const token = await loginWithPassword(form.values.email, form.values.password)
      setToken(token.access_token, token.expires_in)
      setMe(await api.auth.me())
      const from = (location.state as { from?: string } | null)?.from
      navigate(from && from !== '/login' ? from : '/', { replace: true })
    } catch (err) {
      setAlert(errorMessage(err))
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthCard title="Tekrar hoş geldiniz" description="Devam etmek için hesabınıza giriş yapın." footer={<>Hesabınız yok mu? <Link className={link} to="/register">Hesap oluşturun</Link></>}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {info && <Alert message={info} tone="success" />}
        {alert && <Alert message={alert} />}
        <Field label="E-posta" htmlFor="f-email" error={form.errors.email}>
          <input {...form.field('email')} type="email" autoComplete="email" className="form-field" placeholder="ad@sirket.com" />
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
  const navigate = useNavigate()
  const [alert, setAlert] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const form = useForm({ fullName: '', email: '', department: '', password: '' }, {
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
      await api.auth.register({ fullName: v.fullName, email: v.email, password: v.password, department: v.department || undefined })
      navigate('/login?registered=1', { replace: true })
    } catch (err) {
      form.setServerErrors(fieldErrors(err))
      setAlert(errorMessage(err))
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthCard title="Hesap oluşturun" description="Talep açmak ve takip etmek için kayıt olun." footer={<>Zaten hesabınız var mı? <Link className={link} to="/login">Girişe dön</Link></>}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {alert && <Alert message={alert} />}
        <Field label="Ad soyad" htmlFor="f-fullName" error={form.errors.fullName}>
          <input {...form.field('fullName')} autoComplete="name" className="form-field" />
        </Field>
        <Field label="E-posta" htmlFor="f-email" error={form.errors.email}>
          <input {...form.field('email')} type="email" autoComplete="email" className="form-field" placeholder="ad@sirket.com" />
        </Field>
        <Field label="Departman" htmlFor="f-department" optional>
          <input {...form.field('department')} autoComplete="organization-title" className="form-field" placeholder="Satış" />
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

export function ResetPasswordPage() {
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
    <AuthCard title="Yeni şifre belirleyin" description="Şifreniz değişince açık tüm oturumlarınız güvenlik için kapatılır." footer={<Link className={link} to="/login">Girişe dön</Link>}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {alert && <Alert message={alert} />}
        <Field label="Yeni şifre" htmlFor="f-password" error={form.errors.password} hint="En az 10 karakter; büyük harf, küçük harf ve rakam içersin.">
          <input {...form.field('password')} type="password" autoComplete="new-password" className="form-field" />
        </Field>
        <Field label="Yeni şifre (tekrar)" htmlFor="f-confirm" error={form.errors.confirm}>
          <input {...form.field('confirm')} type="password" autoComplete="new-password" className="form-field" />
        </Field>
        <button type="submit" className="btn-violet w-full" aria-busy={pending} disabled={!token}>{pending && <Spinner />} Şifreyi güncelle</button>
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
  const form = useForm({ email: '', password: '' }, { email: emailRule, password: (v) => (!v ? 'Şifrenizi girin.' : undefined) })

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!valid || pending || !form.validateAll()) return
    setPending(true)
    setAlert(null)
    try {
      const res = await authorize({ ...form.values, clientId, redirectUri: redirect, codeChallenge: params.get('code_challenge')!, state: params.get('state') ?? undefined, scope: params.get('scope') ?? undefined })
      const target = new URL(res.redirectUri)
      target.searchParams.set('code', res.code)
      if (res.state) target.searchParams.set('state', res.state)
      window.location.assign(target.toString())
    } catch (err) {
      setAlert(errorMessage(err))
      setPending(false)
    }
  }

  return (
    <AuthCard title="Uygulamaya erişim izni" description={`"${clientId}" uygulaması StajDesk hesabınızla oturum açmak istiyor.`} footer={<Link className={link} to="/login">StajDesk'e dön</Link>}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {alert && <Alert message={alert} />}
        <div className="flex items-center gap-2 rounded-lg bg-violet-500/10 px-3 py-2 text-sm text-violet-700 dark:text-violet-300"><KeyRound className="size-4" aria-hidden="true" /> Yönlendirilecek adres: {redirect || '—'}</div>
        <Field label="E-posta" htmlFor="f-email" error={form.errors.email}>
          <input {...form.field('email')} type="email" autoComplete="email" className="form-field" />
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
