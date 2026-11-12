import { useEffect, useRef, useState, type ReactNode } from 'react'
import clsx from 'clsx'
import { ChevronLeft, ChevronRight, ExternalLink, Pause, Play } from 'lucide-react'
import { adClickUrl, siteApi, type Ad } from '@/lib/site'

/* =========================================================
   Reklam alanları. Gösterim: reklam ekranda gerçekten görünür olduğunda, sayfa oturumu başına bir kez sayılır
   (toplu gönderilir). Tıklama: sunucudaki /click adresine gider; sunucu sayar ve kayıtlı adrese yönlendirir.
   ========================================================= */

const seen = new Set<number>()
let queue: number[] = []
let timer: ReturnType<typeof setTimeout> | undefined

function trackImpression(id: number) {
  if (seen.has(id)) return
  seen.add(id)
  queue.push(id)
  clearTimeout(timer)
  timer = setTimeout(() => {
    const ids = queue
    queue = []
    void siteApi.impressions(ids).catch(() => { /* sayaç kritik değil */ })
  }, 1200)
}

/** Öğe en az yarısı görünür olduğunda gösterimi sayar. */
function useImpression(id: number | undefined) {
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const el = ref.current
    if (!el || id === undefined) return
    if (!('IntersectionObserver' in window)) { trackImpression(id); return }
    const io = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) { trackImpression(id); io.disconnect() }
    }, { threshold: 0.5 })
    io.observe(el)
    return () => io.disconnect()
  }, [id])
  return ref
}

function AdLink({ ad, children, className }: { ad: Ad; children: ReactNode; className?: string }) {
  return ad.hasLink
    ? <a href={adClickUrl(ad.id)} target="_blank" rel="noopener noreferrer sponsored" className={className}>{children}</a>
    : <div className={className}>{children}</div>
}

/** Görsel yüklenemezse koyu düz zemin kalır; başlık yine okunur. */
function AdImage({ src, eager }: { src: string; eager?: boolean }) {
  const [failed, setFailed] = useState(false)
  return failed ? null : <img src={src} alt="" loading={eager ? 'eager' : 'lazy'} onError={() => setFailed(true)} className="absolute inset-0 size-full object-cover" />
}

export function HeroCarousel({ ads, fallback }: { ads: Ad[]; fallback: ReactNode }) {
  const [index, setIndex] = useState(0)
  const [paused, setPaused] = useState(false)
  const reduced = typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
  const count = ads.length
  const current = ads[index % Math.max(count, 1)]
  const ref = useImpression(current?.id)

  useEffect(() => {
    if (count < 2 || paused || reduced) return
    const t = setInterval(() => setIndex((i) => (i + 1) % count), 7000)
    return () => clearInterval(t)
  }, [count, paused, reduced])

  if (!current) return <>{fallback}</>
  const go = (d: number) => setIndex((i) => (i + d + count) % count)

  return (
    <section aria-roledescription="carousel" aria-label="Kampanyalar ve duyurular" className="relative overflow-hidden rounded-2xl bg-slate-800"
      onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)} onFocus={() => setPaused(true)} onBlur={() => setPaused(false)}>
      <div ref={ref} key={current.id} aria-roledescription="slide" aria-label={`${(index % count) + 1} / ${count}`}>
        <AdLink ad={current} className="relative block aspect-[16/9] w-full sm:aspect-[3/1]">
          <AdImage src={current.imageUrl} eager />
          <div className="absolute inset-0 bg-linear-to-r from-slate-950/75 via-slate-950/35 to-transparent" aria-hidden="true" />
          <div className="relative flex h-full max-w-xl flex-col justify-start p-5 pb-16 sm:justify-center sm:p-10">
            <span className="mb-2 w-fit rounded bg-white/15 px-2 py-0.5 text-[11px] font-medium tracking-wide text-white/90 uppercase">Reklam</span>
            <h2 className="text-xl font-bold text-white sm:text-3xl">{current.title}</h2>
            {current.description && <p className="mt-2 line-clamp-2 text-sm text-white/85 sm:text-base">{current.description}</p>}
            {current.hasLink && <span className="mt-4 inline-flex w-fit items-center gap-1 rounded-lg bg-white px-3 py-2 text-sm font-medium text-slate-900">İncele <ExternalLink className="size-4" aria-hidden="true" /></span>}
          </div>
        </AdLink>
      </div>
      {count > 1 && (
        <div className="absolute right-3 bottom-3 flex items-center gap-1">
          <button type="button" onClick={() => go(-1)} aria-label="Önceki" className="grid size-11 place-items-center rounded-full bg-slate-950/50 text-white hover:bg-slate-950/70"><ChevronLeft className="size-5" aria-hidden="true" /></button>
          <button type="button" onClick={() => setPaused((p) => !p)} aria-label={paused ? 'Otomatik geçişi başlat' : 'Otomatik geçişi durdur'} className="grid size-11 place-items-center rounded-full bg-slate-950/50 text-white hover:bg-slate-950/70">
            {paused ? <Play className="size-4" aria-hidden="true" /> : <Pause className="size-4" aria-hidden="true" />}
          </button>
          <button type="button" onClick={() => go(1)} aria-label="Sonraki" className="grid size-11 place-items-center rounded-full bg-slate-950/50 text-white hover:bg-slate-950/70"><ChevronRight className="size-5" aria-hidden="true" /></button>
        </div>
      )}
      {count > 1 && (
        <div className="absolute bottom-5 left-5 hidden gap-1.5 sm:flex" aria-hidden="true">
          {ads.map((a, i) => <span key={a.id} className={clsx('h-1.5 rounded-full transition-all', i === index % count ? 'w-6 bg-white' : 'w-1.5 bg-white/50')} />)}
        </div>
      )}
    </section>
  )
}

/** Yan sütun veya satır içi tek reklam kartı. */
export function AdCard({ ad, tone = 'light' }: { ad: Ad; tone?: 'light' | 'auto' }) {
  const ref = useImpression(ad.id)
  return (
    <div ref={ref}>
      <AdLink ad={ad} className={clsx('group block overflow-hidden rounded-xl', tone === 'light' ? 'border border-slate-200 bg-white shadow-xs' : 'card')}>
        <div className="relative aspect-[3/1] bg-slate-700">
          <AdImage src={ad.imageUrl} />
          <span className="absolute top-2 left-2 rounded bg-slate-950/60 px-1.5 py-0.5 text-[10px] font-medium tracking-wide text-white uppercase">Reklam</span>
        </div>
        <div className="p-3">
          <div className={clsx('text-sm font-semibold', tone === 'light' ? 'text-slate-800 group-hover:text-indigo-700' : 'text-gray-800 dark:text-gray-100')}>{ad.title}</div>
          {ad.description && <p className="mt-0.5 line-clamp-2 text-xs text-gray-500">{ad.description}</p>}
        </div>
      </AdLink>
    </div>
  )
}
