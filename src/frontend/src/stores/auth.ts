import { create } from 'zustand'
import type { Me, Permission } from '@/lib/types'

/**
 * Oturum durumu.
 * Güvenlik notu: erişim token'ı yalnızca BELLEKTE tutulur (localStorage'a yazılmaz → XSS ile çalınamaz).
 * Sayfa yenilenince oturum, httpOnly cookie'deki refresh token ile sessizce yenilenir (bkz. lib/oauth.ts).
 */
interface AuthState {
  accessToken: string | null
  expiresAt: number | null
  me: Me | null
  /** Açılışta oturum geri yükleniyor mu? (yüklenene kadar korumalı sayfalar bekler) */
  restoring: boolean
  setToken: (token: string, expiresIn: number) => void
  setMe: (me: Me) => void
  setRestoring: (v: boolean) => void
  clear: () => void
}

export const useAuth = create<AuthState>()((set) => ({
  accessToken: null,
  expiresAt: null,
  me: null,
  restoring: true,
  setToken: (accessToken, expiresIn) => set({ accessToken, expiresAt: Date.now() + expiresIn * 1000 }),
  setMe: (me) => set({ me }),
  setRestoring: (restoring) => set({ restoring }),
  clear: () => set({ accessToken: null, expiresAt: null, me: null }),
}))

/** İzin kontrolü: menü, buton ve sayfa görünürlüğü bunu kullanır (asıl kontrol sunucudadır). */
export function useCan() {
  const perms = useAuth((s) => s.me?.permissions)
  const set = new Set(perms ?? [])
  const can = (...anyOf: Permission[]) => anyOf.some((p) => set.has(p))
  return {
    can,
    isStaff: can('tickets.manage'),
    isAdmin: can('users.manage'),
  }
}

/** Geriye uyumluluk: eski bileşenlerin kullandığı kısa yol. */
export const useRole = () => {
  const { can, isStaff, isAdmin } = useCan()
  return { can, isStaff, isAdmin }
}
