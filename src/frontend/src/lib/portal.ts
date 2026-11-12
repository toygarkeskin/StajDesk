import { http } from './api'
import type { ApiResponse } from './types'

/* ---------- Panel kutucukları (widget) ---------- */

export type WidgetSize = 'Small' | 'Medium' | 'Large' | 'Full'
export interface DashboardWidget { key: string; title: string; description: string; category: string; size: WidgetSize }
export interface DashboardLayout { widgets: DashboardWidget[]; hidden: DashboardWidget[]; customized: boolean }
export interface WidgetAdmin {
  key: string; title: string; description: string; category: string; requiredAnyPermission: string[]
  isEnabled: boolean; sortOrder: number; size: WidgetSize; roles: string[]; defaultSize: WidgetSize; defaultOrder: number
}

/** 12 sütunlu ızgarada kutucuk genişliği; telefonda hepsi tam genişlik. */
export const sizeClass: Record<WidgetSize, string> = {
  Small: 'col-span-12 md:col-span-6 xl:col-span-4',
  Medium: 'col-span-12 xl:col-span-6',
  Large: 'col-span-12 xl:col-span-8',
  Full: 'col-span-12',
}
export const sizeLabels: Record<WidgetSize, string> = { Small: 'Küçük (1/3)', Medium: 'Orta (1/2)', Large: 'Geniş (2/3)', Full: 'Tam satır' }

/* ---------- Portal akışları ---------- */

export interface PersonRef { id: number; fullName: string; title?: string | null; department?: string | null; hasPhoto: boolean; photoVersion?: string | null }
export interface Birthday { person: PersonRef; month: number; day: number; isToday: boolean }
export interface NewHire { person: PersonRef; hireDate: string }
export interface CompanyEvent { id: number; title: string; description?: string | null; location?: string | null; startsAt: string; endsAt?: string | null; isPublished: boolean }
export type RecognitionType = 'Achievement' | 'EmployeeOfWeek'
export interface Recognition { id: number; type: RecognitionType; title: string; description?: string | null; date: string; person: PersonRef }

export interface SearchRef { id: number; title: string; subtitle?: string | null }
export interface SearchResult {
  people: (PersonRef & { title?: string | null })[]
  departments: SearchRef[]
  announcements: SearchRef[]
  tickets: SearchRef[]
}

const unwrap = <T,>(p: Promise<{ data: ApiResponse<T> }>) => p.then((r) => r.data.data)

export const portalApi = {
  layout: () => unwrap<DashboardLayout>(http.get('/dashboard/layout')),
  savePreferences: (order: string[], hidden: string[]) => unwrap<DashboardLayout>(http.put('/dashboard/preferences', { order, hidden })),
  resetPreferences: () => unwrap<DashboardLayout>(http.delete('/dashboard/preferences')),
  widgets: () => unwrap<{ widgets: WidgetAdmin[]; roles: { name: string; displayName: string }[] }>(http.get('/admin/widgets')),
  saveWidget: (key: string, body: { isEnabled: boolean; sortOrder: number; size: WidgetSize; roles: string[] }) =>
    unwrap<WidgetAdmin>(http.put(`/admin/widgets/${encodeURIComponent(key)}`, body)),

  birthdays: () => unwrap<Birthday[]>(http.get('/portal/birthdays')),
  newHires: () => unwrap<NewHire[]>(http.get('/portal/new-hires')),
  events: () => unwrap<CompanyEvent[]>(http.get('/portal/events')),
  recognitions: () => unwrap<Recognition[]>(http.get('/portal/recognitions')),
  search: (q: string) => unwrap<SearchResult>(http.get('/search', { params: { q } })),

  // Yönetim
  allEvents: () => unwrap<CompanyEvent[]>(http.get('/content/portal/events')),
  saveEvent: (id: number | null, body: object) => unwrap<CompanyEvent>(id ? http.put(`/content/portal/events/${id}`, body) : http.post('/content/portal/events', body)),
  removeEvent: (id: number) => http.delete(`/content/portal/events/${id}`),
  allRecognitions: () => unwrap<Recognition[]>(http.get('/content/portal/recognitions')),
  saveRecognition: (id: number | null, body: object) => unwrap<Recognition>(id ? http.put(`/content/portal/recognitions/${id}`, body) : http.post('/content/portal/recognitions', body)),
  removeRecognition: (id: number) => http.delete(`/content/portal/recognitions/${id}`),
}
