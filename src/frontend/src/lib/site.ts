import { http } from './api'
import type { AnnouncementLevel, ApiResponse, EmploymentType, PagedResult } from './types'

/* ---------- Herkese açık kurumsal site (giriş gerektirmez) ---------- */

export type AdPage = 'Landing' | 'Portal'
export type AdPlacement = 'Hero' | 'Sidebar' | 'Inline'
export type LiveStatus = 'live' | 'stale' | 'not_configured' | 'unavailable'

export interface LinkItem { label: string; url: string }
export interface SiteInfo { settings: Record<string, string>; links: LinkItem[] }
export interface Ad { id: number; title: string; description?: string | null; imageUrl: string; hasLink: boolean; placement: AdPlacement }
export interface NewsCard { id: number; slug: string; title: string; summary: string; imageUrl?: string | null; publishedAt: string }
export interface NewsDetail extends NewsCard { body: string }
export interface JobCard { id: number; title: string; department?: string | null; location: string; employmentType: EmploymentType; publishedAt: string; closesOn?: string | null }
export interface JobDetail extends JobCard { description: string; requirements?: string | null; applyEmail?: string | null }
export interface PublicDepartment { id: number; name: string; description?: string | null; memberCount: number; units: string[] }
export interface PublicAnnouncement { id: number; title: string; body: string; level: AnnouncementLevel; publishAt: string }
export interface CompanyStats { employees: number; stores: number; departments: number; cities: number }
export interface Landing {
  site: SiteInfo
  ads: Ad[]
  news: NewsCard[]
  announcements: PublicAnnouncement[]
  departments: PublicDepartment[]
  jobs: JobCard[]
  stats: CompanyStats
}

export interface LiveValue<T> { status: LiveStatus; data: T | null; source: string; fetchedAt?: string | null; note?: string | null }
export interface DailyForecast { date: string; weatherCode: number; description: string; max: number; min: number }
export interface Weather { city: string; temperature: number; weatherCode: number; description: string; humidity: number; windKmh: number; daily: DailyForecast[] }
export interface CurrencyRate { code: string; name: string; buying: number; selling: number }
export interface Rates { bulletinDate: string; currencies: CurrencyRate[] }
export interface Gold { gramBuying: number; gramSelling: number }
export interface City { name: string; latitude: number; longitude: number }

/* ---------- İçerik yönetimi (portal içinden) ---------- */

export interface AdAdmin {
  id: number; title: string; description?: string | null; imageUrl: string; imageKey: string; linkUrl?: string | null
  page: AdPage; placement: AdPlacement; sortOrder: number; startsAt: string; endsAt?: string | null; isActive: boolean
  status: 'inactive' | 'scheduled' | 'expired' | 'live'; impressions: number; clicks: number; clickRate: number
}
export interface AdDailyStat { day: string; impressions: number; clicks: number }
export interface NewsAdmin { id: number; slug: string; title: string; summary: string; body: string; imageKey?: string | null; imageUrl?: string | null; isPublished: boolean; publishedAt: string; authorName: string }
export interface JobAdmin {
  id: number; title: string; departmentId?: number | null; department?: string | null; location: string; employmentType: EmploymentType
  description: string; requirements?: string | null; isPublished: boolean; publishedAt: string; closesOn?: string | null
}

const unwrap = <T,>(p: Promise<{ data: ApiResponse<T> }>) => p.then((r) => r.data.data)

export const siteApi = {
  landing: () => unwrap<Landing>(http.get('/public/landing')),
  info: () => unwrap<SiteInfo>(http.get('/public/site')),
  news: (page = 1, pageSize = 9) => unwrap<PagedResult<NewsCard>>(http.get('/public/news', { params: { page, pageSize } })),
  newsDetail: (slug: string) => unwrap<NewsDetail>(http.get(`/public/news/${encodeURIComponent(slug)}`)),
  jobs: () => unwrap<JobCard[]>(http.get('/public/jobs')),
  job: (id: number) => unwrap<JobDetail>(http.get(`/public/jobs/${id}`)),
  departments: () => unwrap<PublicDepartment[]>(http.get('/public/departments')),
  announcements: () => unwrap<PublicAnnouncement[]>(http.get('/public/announcements')),
  ads: (page: AdPage) => unwrap<Ad[]>(http.get('/public/ads', { params: { page } })),
  impressions: (ids: number[]) => http.post('/public/ads/impressions', { ids }),
  cities: () => unwrap<City[]>(http.get('/public/cities')),
  weather: (city?: string) => unwrap<LiveValue<Weather>>(http.get('/public/weather', { params: { city } })),
  rates: () => unwrap<LiveValue<Rates>>(http.get('/public/rates')),
  gold: () => unwrap<LiveValue<Gold>>(http.get('/public/gold')),
}

/** Reklam tıklaması sunucuda sayılır ve kayıtlı adrese yönlendirilir (adres istemciye hiç verilmez). */
export const adClickUrl = (id: number) => `/api/public/ads/${id}/click`

export const contentApi = {
  ads: () => unwrap<AdAdmin[]>(http.get('/content/ads')),
  adStats: (id: number) => unwrap<AdDailyStat[]>(http.get(`/content/ads/${id}/stats`)),
  saveAd: (id: number | null, body: object) => unwrap<AdAdmin>(id ? http.put(`/content/ads/${id}`, body) : http.post('/content/ads', body)),
  removeAd: (id: number) => http.delete(`/content/ads/${id}`),
  news: () => unwrap<NewsAdmin[]>(http.get('/content/news')),
  saveNews: (id: number | null, body: object) => unwrap<NewsAdmin>(id ? http.put(`/content/news/${id}`, body) : http.post('/content/news', body)),
  removeNews: (id: number) => http.delete(`/content/news/${id}`),
  jobs: () => unwrap<JobAdmin[]>(http.get('/content/jobs')),
  saveJob: (id: number | null, body: object) => unwrap<JobAdmin>(id ? http.put(`/content/jobs/${id}`, body) : http.post('/content/jobs', body)),
  removeJob: (id: number) => http.delete(`/content/jobs/${id}`),
  saveSettings: (body: { settings: Record<string, string>; links: LinkItem[] }) => unwrap<SiteInfo>(http.put('/content/settings', body)),
  upload: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return unwrap<{ key: string; url: string }>(http.post('/content/media', form, { headers: { 'Content-Type': 'multipart/form-data' } }))
  },
}

/** Ayar anahtarları (backend SiteKeys ile aynı). */
export const siteKeys = {
  companyName: 'company.name',
  tagline: 'company.tagline',
  aboutTitle: 'about.title',
  aboutBody: 'about.body',
  mission: 'about.mission',
  vision: 'about.vision',
  address: 'contact.address',
  phone: 'contact.phone',
  email: 'contact.email',
  careerEmail: 'career.email',
  weatherCity: 'weather.city',
} as const
