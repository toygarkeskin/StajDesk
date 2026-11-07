import { create } from 'zustand'

type Theme = 'light' | 'dark'

const readTheme = (): Theme => (document.documentElement.classList.contains('dark') ? 'dark' : 'light')

interface Toast {
  id: number
  type: 'success' | 'error'
  message: string
}

interface UiState {
  theme: Theme
  toggleTheme: () => void
  sidebarOpen: boolean
  setSidebarOpen: (open: boolean) => void
  toasts: Toast[]
  toast: (message: string, type?: Toast['type']) => void
  dismiss: (id: number) => void
}

let nextId = 1

export const useUi = create<UiState>((set, get) => ({
  theme: readTheme(),
  toggleTheme: () => {
    const theme: Theme = get().theme === 'dark' ? 'light' : 'dark'
    document.documentElement.classList.toggle('dark', theme === 'dark')
    try {
      localStorage.setItem('stajdesk-theme', theme)
    } catch {
      /* depolama kapalı olabilir */
    }
    set({ theme })
  },
  sidebarOpen: false,
  setSidebarOpen: (sidebarOpen) => set({ sidebarOpen }),
  toasts: [],
  toast: (message, type = 'success') => {
    const id = nextId++
    set((s) => ({ toasts: [...s.toasts, { id, type, message }] }))
    setTimeout(() => get().dismiss(id), 4000)
  },
  dismiss: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}))

export const toast = (message: string, type?: Toast['type']) => useUi.getState().toast(message, type)
