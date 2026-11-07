import axios, { AxiosError } from 'axios'
import { useAuth } from '@/stores/auth'
import type {
  ApiResponse,
  AssetDetail,
  AssetListItem,
  AuthResponse,
  Category,
  DashboardStats,
  NotificationItem,
  PagedResult,
  TicketComment,
  TicketDetail,
  TicketListItem,
  User,
  UserListItem,
  UserSummary,
} from './types'

/** Tüm HTTP istekleri bu tek axios örneğinden geçer. */
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Her isteğe JWT eklenir.
http.interceptors.request.use((config) => {
  const token = useAuth.getState().token
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// 401 gelirse oturum kapatılır ve giriş sayfasına yönlendirilir.
http.interceptors.response.use(
  (r) => r,
  (error: AxiosError) => {
    const isAuthCall = error.config?.url?.startsWith('/auth/login') || error.config?.url?.startsWith('/auth/register')
    if (error.response?.status === 401 && !isAuthCall && useAuth.getState().token) {
      useAuth.getState().logout()
      if (!location.pathname.startsWith('/login')) location.assign('/login?expired=1')
    }
    return Promise.reject(error)
  },
)

const unwrap = <T>(p: Promise<{ data: ApiResponse<T> }>) => p.then((r) => r.data.data)

/** API hata yanıtından kullanıcıya gösterilecek mesajı çıkarır. */
export function errorMessage(err: unknown, fallback = "Sunucu isteği tamamlayamadı. Birkaç saniye sonra tekrar deneyin.") {
  if (err instanceof AxiosError) {
    if (!err.response) return "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin."
    const body = err.response.data as Partial<ApiResponse<unknown>> | undefined
    if (body?.message && err.response.status < 500) return body.message
  }
  return fallback
}

/** Destek ekibine iletilebilecek teknik ayrıntı (birincil arayüzde gösterilmez, yalnızca kopyalanır). */
export function errorDetails(err: unknown) {
  if (err instanceof AxiosError) {
    return [`${err.config?.method?.toUpperCase()} ${err.config?.url}`, `HTTP ${err.response?.status ?? "ağ hatası"}`, err.message, new Date().toISOString()].join('\n')
  }
  return String(err)
}

export function fieldErrors(err: unknown): Record<string, string> {
  if (err instanceof AxiosError) {
    const errors = (err.response?.data as Partial<ApiResponse<unknown>> | undefined)?.errors
    if (errors) return Object.fromEntries(Object.entries(errors).map(([k, v]) => [k, v[0] ?? '']))
  }
  return {}
}

type Query = Record<string, string | number | boolean | undefined | null>
const clean = (q: Query) => Object.fromEntries(Object.entries(q).filter(([, v]) => v !== undefined && v !== null && v !== ''))

export const api = {
  auth: {
    login: (email: string, password: string) => unwrap<AuthResponse>(http.post('/auth/login', { email, password })),
    register: (body: { fullName: string; email: string; password: string; department?: string }) =>
      unwrap<AuthResponse>(http.post('/auth/register', body)),
    me: () => unwrap<User>(http.get('/auth/me')),
  },
  tickets: {
    list: (q: Query) => unwrap<PagedResult<TicketListItem>>(http.get('/tickets', { params: clean(q) })),
    get: (id: number) => unwrap<TicketDetail>(http.get(`/tickets/${id}`)),
    create: (body: object) => unwrap<TicketDetail>(http.post('/tickets', body)),
    update: (id: number, body: object) => unwrap<TicketDetail>(http.put(`/tickets/${id}`, body)),
    setStatus: (id: number, status: string) => unwrap<TicketDetail>(http.patch(`/tickets/${id}/status`, { status })),
    setPriority: (id: number, priority: string) => unwrap<TicketDetail>(http.patch(`/tickets/${id}/priority`, { priority })),
    assign: (id: number, assigneeId: number | null) => unwrap<TicketDetail>(http.patch(`/tickets/${id}/assign`, { assigneeId })),
    comment: (id: number, body: string, isInternal: boolean) =>
      unwrap<TicketComment>(http.post(`/tickets/${id}/comments`, { body, isInternal })),
    remove: (id: number) => http.delete(`/tickets/${id}`),
  },
  categories: {
    list: (includeInactive = false) => unwrap<Category[]>(http.get('/categories', { params: { includeInactive } })),
    create: (body: object) => unwrap<Category>(http.post('/categories', body)),
    update: (id: number, body: object) => unwrap<Category>(http.put(`/categories/${id}`, body)),
    remove: (id: number) => http.delete(`/categories/${id}`),
  },
  assets: {
    list: (q: Query) => unwrap<PagedResult<AssetListItem>>(http.get('/assets', { params: clean(q) })),
    mine: () => unwrap<AssetListItem[]>(http.get('/assets/mine')),
    get: (id: number) => unwrap<AssetDetail>(http.get(`/assets/${id}`)),
    create: (body: object) => unwrap<AssetDetail>(http.post('/assets', body)),
    update: (id: number, body: object) => unwrap<AssetDetail>(http.put(`/assets/${id}`, body)),
    remove: (id: number) => http.delete(`/assets/${id}`),
  },
  users: {
    list: (q: Query) => unwrap<PagedResult<UserListItem>>(http.get('/users', { params: clean(q) })),
    agents: () => unwrap<UserSummary[]>(http.get('/users/agents')),
    lookup: (search: string) => unwrap<UserSummary[]>(http.get('/users/lookup', { params: { search } })),
    create: (body: object) => unwrap<User>(http.post('/users', body)),
    update: (id: number, body: object) => unwrap<User>(http.put(`/users/${id}`, body)),
  },
  stats: {
    dashboard: () => unwrap<DashboardStats>(http.get('/stats')),
  },
  notifications: {
    list: (q: Query) => unwrap<PagedResult<NotificationItem>>(http.get('/notifications', { params: clean(q) })),
    unreadCount: () => unwrap<number>(http.get('/notifications/unread-count')),
    markRead: (id: number) => http.patch(`/notifications/${id}/read`),
    markAllRead: () => unwrap<number>(http.post('/notifications/read-all')),
  },
}
