import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { api } from '@/lib/api'
import { initials } from '@/lib/format'

/**
 * Personel fotoğrafları herkese açık değildir: tarayıcı <img src> ile token gönderemediği için fotoğraf
 * yetkili istekle (Authorization başlığı) indirilip geçici bir blob adresiyle gösterilir.
 * Sürüm anahtarı değişmedikçe önbellekten gelir.
 */
export function useEmployeeImage(userId: number | undefined, kind: 'photo' | 'cover', version: string | null | undefined) {
  const query = useQuery({
    queryKey: ['employee-image', userId, kind, version],
    queryFn: () => api.employees.image(userId!, kind, version!),
    enabled: !!userId && !!version,
    staleTime: Infinity,
    gcTime: 30 * 60_000,
    retry: false,
  })
  const [url, setUrl] = useState<string | null>(null)
  useEffect(() => {
    if (!query.data) {
      setUrl(null)
      return
    }
    const objectUrl = URL.createObjectURL(query.data)
    setUrl(objectUrl)
    return () => URL.revokeObjectURL(objectUrl)
  }, [query.data])
  return url
}

const colors = ['bg-violet-500', 'bg-sky-500', 'bg-emerald-500', 'bg-amber-500', 'bg-rose-500', 'bg-indigo-500']
const sizes = { sm: 'size-6 text-xs', md: 'size-8 text-xs', lg: 'size-10 text-sm', xl: 'size-16 text-xl', '2xl': 'size-24 text-3xl' } as const

/** Fotoğraf varsa fotoğraf, yoksa (veya yüklenemezse) baş harfler. */
export function PersonAvatar({ id, name, photoVersion, size = 'md', className }: {
  id: number
  name: string
  photoVersion?: string | null
  size?: keyof typeof sizes
  className?: string
}) {
  const url = useEmployeeImage(id, 'photo', photoVersion)
  const color = colors[[...name].reduce((a, c) => a + c.charCodeAt(0), 0) % colors.length]
  return url ? (
    <img src={url} alt="" className={clsx('shrink-0 rounded-full object-cover', sizes[size], className)} />
  ) : (
    <span aria-hidden="true" className={clsx('inline-flex shrink-0 items-center justify-center rounded-full font-semibold text-white', color, sizes[size], className)}>
      {initials(name)}
    </span>
  )
}
