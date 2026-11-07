import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router'
import { api, errorMessage, fieldErrors } from '@/lib/api'
import { useForm } from '@/lib/hooks'
import { isTokenValid, useAuth } from '@/stores/auth'
import { CheckCircle2, Clock3, ShieldCheck } from 'lucide-react'
import { Field, Spinner } from '@/components/ui'
import { Logo } from '@/components/layout/Logo'

/* Form (Auth) kalıbı: ortalanmış kart · logo · başlık · alanlar · tek birincil eylem · çıkış bağlantısı. Uygulama menüsü yok. */

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
        {/* Sağ panel: tanıtım */}
        <div className="absolute top-0 right-0 bottom-0 hidden md:block md:w-1/2">
          <div className="flex h-full flex-col justify-center bg-linear-to-br from-violet-600 via-violet-500 to-indigo-500 p-12 text-white">
            <h2 className="mb-4 max-w-md text-3xl font-bold text-white">Tüm destek talepleriniz tek bir yerde.</h2>
            <p className="mb-10 max-w-md text-violet-100">StajDesk ile sorunlarınızı bildirin, durumunu anlık takip edin; destek ekibi talepleri önceliğe göre yönetsin.</p>
            <ul className="space-y-4 text-sm">
              <li className="flex items-center gap-3"><CheckCircle2 className="size-5 text-violet-200" aria-hidden="true" /> Talep açma, yorum ve durum takibi</li>
              <li className="flex items-center gap-3"><Clock3 className="size-5 text-violet-200" aria-hidden="true" /> Gerçek zamanlıya yakın bildirimler</li>
              <li className="flex items-center gap-3"><ShieldCheck className="size-5 text-violet-200" aria-hidden="true" /> Rol bazlı güvenli erişim</li>
            </ul>
          </div>
        </div>
      </div>
    </main>
  )
}

const FormAlert = ({ message }: { message: string }) => (
  <div role="alert" className="mb-4 rounded-lg bg-rose-500/10 px-3 py-2 text-sm text-rose-600 dark:text-rose-400">{message}</div>
)

const emailRule = (v: string) =>
  !v.trim() ? 'E-posta adresinizi girin.' : !/^\S+@\S+\.\S+$/.test(v) ? 'ad@sirket.com biçiminde bir e-posta adresi girin.' : undefined

export function LoginPage() {
  const { token, expiresAt, setSession } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const [alert, setAlert] = useState<string | null>(params.get('expired') ? 'Oturumun süresi doldu. Devam etmek için yeniden giriş yapın.' : null)
  const [pending, setPending] = useState(false)
  const form = useForm({ email: '', password: '' }, {
    email: emailRule,
    password: (v) => (!v ? 'Şifrenizi girin.' : undefined),
  })

  if (token && isTokenValid(expiresAt)) return <Navigate to="/" replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (pending || !form.validateAll()) return
    setAlert(null)
    setPending(true)
    try {
      const res = await api.auth.login(form.values.email, form.values.password)
      setSession(res.token, res.expiresAt, res.user)
      const from = (location.state as { from?: string } | null)?.from
      navigate(from && from !== '/login' ? from : '/', { replace: true })
    } catch (err) {
      setAlert(errorMessage(err))
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthCard
      title="Tekrar hoş geldiniz"
      description="Devam etmek için hesabınıza giriş yapın."
      footer={<>Hesabınız yok mu? <Link className="font-medium text-violet-500 hover:text-violet-600" to="/register">Hesap oluşturun</Link></>}
    >
      <form onSubmit={submit} noValidate className="space-y-4">
        {alert && <FormAlert message={alert} />}
        <Field label="E-posta" htmlFor="f-email" error={form.errors.email}>
          <input {...form.field('email')} type="email" autoComplete="email" className="form-field" placeholder="ad@sirket.com" />
        </Field>
        <Field label="Şifre" htmlFor="f-password" error={form.errors.password}>
          <input {...form.field('password')} type="password" autoComplete="current-password" className="form-field" />
        </Field>
        <button type="submit" className="btn-violet w-full" aria-busy={pending}>
          {pending && <Spinner />} {pending ? 'Giriş yapılıyor…' : 'Giriş yap'}
        </button>
      </form>
    </AuthCard>
  )
}

export function RegisterPage() {
  const setSession = useAuth((s) => s.setSession)
  const navigate = useNavigate()
  const [alert, setAlert] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const form = useForm({ fullName: '', email: '', department: '', password: '' }, {
    fullName: (v) => (!v.trim() ? 'Adınızı ve soyadınızı girin.' : undefined),
    email: emailRule,
    password: (v) =>
      v.length < 8 ? 'En az 8 karakterlik bir şifre belirleyin.'
        : !/[A-Z]/.test(v) || !/[a-z]/.test(v) || !/\d/.test(v) ? 'Şifreye en az bir büyük harf, bir küçük harf ve bir rakam ekleyin.'
        : undefined,
  })

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (pending || !form.validateAll()) return
    setAlert(null)
    setPending(true)
    try {
      const v = form.values
      const res = await api.auth.register({ fullName: v.fullName, email: v.email, password: v.password, department: v.department || undefined })
      setSession(res.token, res.expiresAt, res.user)
      navigate('/', { replace: true })
    } catch (err) {
      form.setServerErrors(fieldErrors(err))
      setAlert(errorMessage(err))
    } finally {
      setPending(false)
    }
  }

  return (
    <AuthCard
      title="Hesap oluşturun"
      description="Talep açmak ve takip etmek için kayıt olun."
      footer={<>Zaten hesabınız var mı? <Link className="font-medium text-violet-500 hover:text-violet-600" to="/login">Girişe dön</Link></>}
    >
      <form onSubmit={submit} noValidate className="space-y-4">
        {alert && <FormAlert message={alert} />}
        <Field label="Ad soyad" htmlFor="f-fullName" error={form.errors.fullName}>
          <input {...form.field('fullName')} autoComplete="name" className="form-field" />
        </Field>
        <Field label="E-posta" htmlFor="f-email" error={form.errors.email}>
          <input {...form.field('email')} type="email" autoComplete="email" className="form-field" placeholder="ad@sirket.com" />
        </Field>
        <Field label="Departman" htmlFor="f-department" optional>
          <input {...form.field('department')} autoComplete="organization-title" className="form-field" placeholder="Muhasebe" />
        </Field>
        <Field label="Şifre" htmlFor="f-password" error={form.errors.password} hint="En az 8 karakter; büyük harf, küçük harf ve rakam içersin.">
          <input {...form.field('password')} type="password" autoComplete="new-password" className="form-field" />
        </Field>
        <button type="submit" className="btn-violet w-full" aria-busy={pending}>
          {pending && <Spinner />} {pending ? 'Hesap oluşturuluyor…' : 'Hesap oluştur'}
        </button>
      </form>
    </AuthCard>
  )
}
