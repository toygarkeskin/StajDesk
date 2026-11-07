import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { User } from '@/lib/types'

interface AuthState {
  token: string | null
  expiresAt: string | null
  user: User | null
  setSession: (token: string, expiresAt: string, user: User) => void
  setUser: (user: User) => void
  logout: () => void
}

/** Oturum bilgisi tek yerde tutulur; tüm bileşenler buradan okur. */
export const useAuth = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      expiresAt: null,
      user: null,
      setSession: (token, expiresAt, user) => set({ token, expiresAt, user }),
      setUser: (user) => set({ user }),
      logout: () => set({ token: null, expiresAt: null, user: null }),
    }),
    { name: 'stajdesk-auth' },
  ),
)

export const isTokenValid = (expiresAt: string | null) => !!expiresAt && new Date(expiresAt).getTime() > Date.now()

export const useRole = () => {
  const role = useAuth((s) => s.user?.role)
  return {
    role,
    isStaff: role === 'Agent' || role === 'Admin',
    isAdmin: role === 'Admin',
  }
}
