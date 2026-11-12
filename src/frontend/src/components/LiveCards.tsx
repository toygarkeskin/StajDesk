import { useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { Cloud, CloudDrizzle, CloudFog, CloudLightning, CloudRain, CloudSnow, CloudSun, Coins, Droplets, Sun, TrendingUp, Wind } from 'lucide-react'
import { siteApi, type LiveValue } from '@/lib/site'
import { formatDateTime } from '@/lib/format'

/* =========================================================
   Canlı veri kartları: hava durumu, döviz, gram altın.
   Veri yalnızca sunucudan gelir (anahtar yok, CORS yok). Her kart KAYNAĞI ve SON GÜNCELLEME zamanını gösterir;
   kaynak çökmüşse "son başarılı veri", hiç yoksa açık bir "ulaşılamıyor" mesajı görünür — sahte sayı gösterilmez.
   ========================================================= */

const money = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 4, maximumFractionDigits: 4 })
const money2 = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const dayFmt = new Intl.DateTimeFormat('tr-TR', { weekday: 'short' })

function weatherIcon(code: number, className = 'size-5') {
  const p = { className, 'aria-hidden': true as const }
  if (code === 0 || code === 1) return <Sun {...p} />
  if (code === 2) return <CloudSun {...p} />
  if (code === 3) return <Cloud {...p} />
  if (code === 45 || code === 48) return <CloudFog {...p} />
  if (code >= 51 && code <= 57) return <CloudDrizzle {...p} />
  if ((code >= 61 && code <= 67) || (code >= 80 && code <= 82)) return <CloudRain {...p} />
  if ((code >= 71 && code <= 77) || code === 85 || code === 86) return <CloudSnow {...p} />
  if (code >= 95) return <CloudLightning {...p} />
  return <Cloud {...p} />
}

/** Kaynak + güncelleme satırı; eski veri veya hata açıkça yazılır. */
function SourceLine<T>({ v }: { v: LiveValue<T> }) {
  return (
    <p className={clsx('mt-3 text-xs', v.status === 'live' ? 'text-gray-500' : 'text-amber-700 dark:text-amber-400')}>
      {v.status === 'stale' && 'Kaynağa şu anda ulaşılamıyor · son başarılı veri · '}
      Kaynak: {v.source}{v.fetchedAt && ` · ${formatDateTime(v.fetchedAt)}`}
    </p>
  )
}

function Shell({ title, icon, children, tone, className }: { title: string; icon: ReactNode; children: ReactNode; tone: 'light' | 'auto'; className?: string }) {
  return (
    <section className={clsx('flex min-w-0 flex-col rounded-xl p-4', tone === 'light' ? 'border border-slate-200 bg-white shadow-xs' : 'card', className)} aria-label={title}>
      <h2 className={clsx('mb-3 flex items-center gap-2 text-[13px] font-semibold', tone === 'light' ? 'text-slate-800' : 'text-gray-800 dark:text-gray-100')}>
        <span className="text-gray-400" aria-hidden="true">{icon}</span>{title}
      </h2>
      {children}
    </section>
  )
}

const Muted = ({ children }: { children: ReactNode }) => <p className="text-sm text-gray-500">{children}</p>
const Skeleton = () => <div className="h-20 animate-pulse rounded-lg bg-gray-100 dark:bg-gray-700/40" aria-busy="true" />

export function WeatherCard({ tone = 'auto', className, allowCityChange = true }: { tone?: 'light' | 'auto'; className?: string; allowCityChange?: boolean }) {
  const [city, setCity] = useState<string | undefined>(() => {
    try { return localStorage.getItem('stajdesk-weather-city') ?? undefined } catch { return undefined }
  })
  const q = useQuery({ queryKey: ['live', 'weather', city ?? ''], queryFn: () => siteApi.weather(city), staleTime: 5 * 60_000, refetchInterval: 15 * 60_000 })
  const cities = useQuery({ queryKey: ['live', 'cities'], queryFn: siteApi.cities, staleTime: Infinity, enabled: allowCityChange })
  const v = q.data
  const w = v?.data
  const change = (name: string) => {
    setCity(name)
    try { localStorage.setItem('stajdesk-weather-city', name) } catch { /* depolama kapalı olabilir */ }
  }

  return (
    <Shell title="Hava durumu" icon={<CloudSun className="size-4" />} tone={tone} className={className}>
      {q.isLoading ? <Skeleton /> : q.isError || !v ? <Muted>Hava durumu alınamadı.</Muted> : !w ? <Muted>{v.note ?? 'Kaynağa şu anda ulaşılamıyor.'}</Muted> : (
        <>
          <div className="flex items-start justify-between gap-3">
            <div>
              {allowCityChange && cities.data ? (
                <label className="block">
                  <span className="sr-only">Şehir</span>
                  <select value={w.city} onChange={(e) => change(e.target.value)}
                    className="-ml-1 min-h-9 rounded-md border-0 bg-transparent px-1 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-200 dark:hover:bg-gray-700/50">
                    {cities.data.map((c) => <option key={c.name} value={c.name}>{c.name}</option>)}
                  </select>
                </label>
              ) : <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{w.city}</div>}
              <div className="mt-1 flex items-center gap-2">
                <span className="text-3xl font-bold text-gray-900 tabular-nums dark:text-gray-50">{Math.round(w.temperature)}°</span>
                <span className="text-amber-500">{weatherIcon(w.weatherCode, 'size-7')}</span>
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-300">{w.description}</div>
            </div>
            <dl className="space-y-1 text-right text-xs text-gray-500">
              <div className="flex items-center justify-end gap-1"><Droplets className="size-3.5" aria-hidden="true" /><dt className="sr-only">Nem</dt><dd>%{w.humidity}</dd></div>
              <div className="flex items-center justify-end gap-1"><Wind className="size-3.5" aria-hidden="true" /><dt className="sr-only">Rüzgâr</dt><dd>{Math.round(w.windKmh)} km/s</dd></div>
            </dl>
          </div>
          <ul className="mt-3 grid grid-cols-4 gap-1 border-t border-gray-100 pt-3 text-center text-xs dark:border-gray-700/60" aria-label="Günlük tahmin">
            {w.daily.slice(0, 4).map((d) => (
              <li key={d.date} title={d.description}>
                <div className="text-gray-500">{dayFmt.format(new Date(d.date + 'T12:00:00'))}</div>
                <div className="my-1 flex justify-center text-gray-400">{weatherIcon(d.weatherCode, 'size-4')}</div>
                <div className="tabular-nums text-gray-700 dark:text-gray-200">{Math.round(d.max)}° <span className="text-gray-400">{Math.round(d.min)}°</span></div>
              </li>
            ))}
          </ul>
          <SourceLine v={v} />
        </>
      )}
    </Shell>
  )
}

export function RatesCard({ tone = 'auto', className }: { tone?: 'light' | 'auto'; className?: string }) {
  const q = useQuery({ queryKey: ['live', 'rates'], queryFn: siteApi.rates, staleTime: 10 * 60_000, refetchInterval: 30 * 60_000 })
  const v = q.data
  return (
    <Shell title="Döviz kurları" icon={<TrendingUp className="size-4" />} tone={tone} className={className}>
      {q.isLoading ? <Skeleton /> : q.isError || !v ? <Muted>Kurlar alınamadı.</Muted> : !v.data ? <Muted>{v.note ?? 'Kaynağa şu anda ulaşılamıyor.'}</Muted> : (
        <>
          <table className="w-full text-sm">
            <thead><tr className="text-xs text-gray-500"><th className="pb-1 text-left font-medium">Kur</th><th className="pb-1 text-right font-medium">Alış</th><th className="pb-1 text-right font-medium">Satış</th></tr></thead>
            <tbody>
              {v.data.currencies.map((c) => (
                <tr key={c.code} className="border-t border-gray-100 dark:border-gray-700/60">
                  <th scope="row" className="py-1.5 text-left font-semibold text-gray-800 dark:text-gray-100">{c.code}<span className="sr-only"> {c.name}</span>/TRY</th>
                  <td className="py-1.5 text-right tabular-nums">{money.format(c.buying)}</td>
                  <td className="py-1.5 text-right font-medium text-gray-900 tabular-nums dark:text-gray-50">{money.format(c.selling)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="mt-2 text-xs text-gray-500">TCMB gösterge niteliğindeki döviz kuru · bülten {v.data.bulletinDate}</p>
          <SourceLine v={v} />
        </>
      )}
    </Shell>
  )
}

export function GoldCard({ tone = 'auto', className }: { tone?: 'light' | 'auto'; className?: string }) {
  const q = useQuery({ queryKey: ['live', 'gold'], queryFn: siteApi.gold, staleTime: 10 * 60_000 })
  const v = q.data
  return (
    <Shell title="Gram altın" icon={<Coins className="size-4" />} tone={tone} className={className}>
      {q.isLoading ? <Skeleton /> : q.isError || !v ? <Muted>Altın fiyatı alınamadı.</Muted> : v.status === 'not_configured' ? (
        <div className="rounded-lg bg-gray-50 p-3 text-sm text-gray-600 dark:bg-gray-900/40 dark:text-gray-400">
          <div className="font-medium text-gray-700 dark:text-gray-300">Veri kaynağı yapılandırılmadı</div>
          <p className="mt-1 text-xs">{v.note}</p>
        </div>
      ) : !v.data ? <Muted>{v.note ?? 'Kaynağa şu anda ulaşılamıyor.'}</Muted> : (
        <>
          <div className="flex justify-between text-sm"><span className="text-gray-500">Alış</span><span className="tabular-nums">{money2.format(v.data.gramBuying)} ₺</span></div>
          <div className="flex justify-between text-sm"><span className="text-gray-500">Satış</span><span className="font-semibold tabular-nums">{money2.format(v.data.gramSelling)} ₺</span></div>
          <SourceLine v={v} />
        </>
      )}
    </Shell>
  )
}
