import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { useAuth } from '@/stores/auth'
import { refresh } from './oauth'
import type {
  Announcement,
  ApiResponse,
  AssetDetail,
  AssetListItem,
  AuditLog,
  Category,
  DashboardStats,
  Home,
  Me,
  NotificationItem,
  OnCallShift,
  PagedResult,
  PermissionInfo,
  RoleInfo,
  SentEmail,
  Store,
  TicketAttachment,
  TicketDetail,
  TicketListItem,
  UserListItem,
  UserSummary,
} from './types'

/** Tüm API istekleri bu tek axios örneğinden geçer. */
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Her isteğe bellekteki erişim token'ı eklenir.
http.interceptors.request.use((config) => {
  const token = useAuth.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

/** Aynı anda gelen 401'ler tek bir yenileme isteği paylaşır. */
let refreshing: Promise<string | null> | null = null
export function refreshAccessToken() {
  refreshing ??= refresh()
    .then((t) => {
      useAuth.getState().setToken(t.access_token, t.expires_in)
      return t.access_token
    })
    .catch(() => null)
    .finally(() => setTimeout(() => (refreshing = null), 0))
  return refreshing
}

// 401 gelirse önce sessizce token yenilenir ve istek tekrarlanır; yenileme de başarısızsa oturum kapanır.
http.interceptors.response.use(
  (r) => r,
  async (error: AxiosError) => {
    const config = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined
    if (error.response?.status === 401 && config && !config._retried && useAuth.getState().accessToken) {
      config._retried = true
      const token = await refreshAccessToken()
      if (token) {
        config.headers.Authorization = `Bearer ${token}`
        return http(config)
      }
      useAuth.getState().clear()
      if (!location.pathname.startsWith('/login')) location.assign('/login?expired=1')
    }
    return Promise.reject(error)
  },
)

const unwrap = <T>(p: Promise<{ data: ApiResponse<T> }>) => p.then((r) => r.data.data)

/** API (veya OAuth) hata yanıtından kullanıcıya gösterilecek mesajı çıkarır. */
export function errorMessage(err: unknown, fallback = 'Sunucu isteği tamamlayamadı. Birkaç saniye sonra tekrar deneyin.') {
  if (err instanceof AxiosError) {
    if (!err.response) return 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.'
    const body = err.response.data as (Partial<ApiResponse<unknown>> & { error_description?: string }) | undefined
    if (body?.error_description) return body.error_description
    if (body?.message && err.response.status < 500) return body.message
  }
  return fallback
}

/** Destek ekibine iletilebilecek teknik ayrıntı (birincil arayüzde gösterilmez, yalnızca kopyalanır). */
export function errorDetails(err: unknown) {
  if (err instanceof AxiosError) {
    return [`${err.config?.method?.toUpperCase()} ${err.config?.url}`, `HTTP ${err.response?.status ?? 'ağ hatası'}`, err.message, new Date().toISOString()].join('\n')
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
    me: () => unwrap<Me>(http.get('/auth/me')),
    register: (body: { fullName: string; email: string; password: string; department?: string }) => unwrap<object>(http.post('/auth/register', body)),
    forgotPassword: (email: string) => http.post<ApiResponse<object>>('/auth/forgot-password', { email }).then((r) => r.data.message),
    resetPassword: (token: string, password: string) => http.post<ApiResponse<object>>('/auth/reset-password', { token, password }).then((r) => r.data.message),
  },
  tickets: {
    list: (q: Query) => unwrap<PagedResult<TicketListItem>>(http.get('/tickets', { params: clean(q) })),
    get: (id: number) => unwrap<TicketDetail>(http.get(`/tickets/${id}`)),
    create: (body: object) => unwrap<TicketDetail>(http.post('/tickets', body)),
    update: (id: number, body: object) => unwrap<TicketDetail>(http.put(`/tickets/${id}`, body)),
    setStatus: (id: number, status: string) => unwrap<TicketDetail>(http.patch(`/tickets/${id}/status`, { status })),
    setPriority: (id: number, priority: string) => unwrap<TicketDetail>(http.patch(`/tickets/${id}/priority`, { priority })),
    assign: (id: number, assigneeId: number | null) => unwrap<TicketDetail>(http.patch(`/tickets/${id}/assign`, { assigneeId })),
    comment: (id: number, body: string, isInternal: boolean) => unwrap<{ id: number }>(http.post(`/tickets/${id}/comments`, { body, isInternal })),
    remove: (id: number) => http.delete(`/tickets/${id}`),
    upload: (id: number, file: File, onProgress?: (percent: number) => void) => {
      const form = new FormData()
      form.append('file', file)
      return unwrap<TicketAttachment>(http.post(`/tickets/${id}/attachments`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: (e) => onProgress?.(e.total ? Math.round((e.loaded / e.total) * 100) : 0),
      }))
    },
    download: (id: number, attachmentId: number) => http.get<Blob>(`/tickets/${id}/attachments/${attachmentId}`, { responseType: 'blob' }).then((r) => r.data),
    removeAttachment: (id: number, attachmentId: number) => http.delete(`/tickets/${id}/attachments/${attachmentId}`),
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
    create: (body: object) => unwrap<{ id: number }>(http.post('/users', body)),
    update: (id: number, body: object) => unwrap<{ id: number }>(http.put(`/users/${id}`, body)),
  },
  stores: {
    list: () => unwrap<Store[]>(http.get('/stores')),
    create: (body: object) => unwrap<Store>(http.post('/stores', body)),
    update: (id: number, body: object) => unwrap<Store>(http.put(`/stores/${id}`, body)),
  },
  roles: {
    list: () => unwrap<{ roles: RoleInfo[]; permissions: PermissionInfo[] }>(http.get('/roles')),
    setPermissions: (id: number, permissions: string[]) => unwrap<object>(http.put(`/roles/${id}/permissions`, { permissions })),
  },
  system: {
    audit: (q: Query) => unwrap<PagedResult<AuditLog>>(http.get('/system/audit', { params: clean(q) })),
    emails: (q: Query) => unwrap<PagedResult<SentEmail>>(http.get('/system/emails', { params: clean(q) })),
  },
  stats: {
    dashboard: () => unwrap<DashboardStats>(http.get('/stats')),
  },
  home: () => unwrap<Home>(http.get('/home')),
  announcements: {
    list: (all = false) => unwrap<Announcement[]>(http.get('/announcements', { params: { all } })),
    create: (body: object) => unwrap<Announcement>(http.post('/announcements', body)),
    update: (id: number, body: object) => unwrap<Announcement>(http.put(`/announcements/${id}`, body)),
    remove: (id: number) => http.delete(`/announcements/${id}`),
  },
  onCall: {
    list: () => unwrap<OnCallShift[]>(http.get('/oncall')),
    create: (body: object) => unwrap<OnCallShift>(http.post('/oncall', body)),
    remove: (id: number) => http.delete(`/oncall/${id}`),
  },
  notifications: {
    list: (q: Query) => unwrap<PagedResult<NotificationItem>>(http.get('/notifications', { params: clean(q) })),
    unreadCount: () => unwrap<number>(http.get('/notifications/unread-count')),
    markRead: (id: number) => http.patch(`/notifications/${id}/read`),
    markAllRead: () => unwrap<number>(http.post('/notifications/read-all')),
  },
}
