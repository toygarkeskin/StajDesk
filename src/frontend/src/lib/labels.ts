import type { AssetStatus, AssetType, TicketPriority, TicketStatus, TicketType } from './types'

export const statusLabels: Record<TicketStatus, string> = {
  New: 'Yeni',
  Open: 'Açık',
  InProgress: 'İşlemde',
  Pending: 'Beklemede',
  Resolved: 'Çözüldü',
  Closed: 'Kapandı',
  Rejected: 'Reddedildi',
}

export const priorityLabels: Record<TicketPriority, string> = {
  Low: 'Düşük',
  Medium: 'Orta',
  High: 'Yüksek',
  Critical: 'Kritik',
}

export const typeLabels: Record<TicketType, string> = {
  Incident: 'Arıza / Olay',
  Request: 'İstek',
}

/** Rol sistem adı → Türkçe görünen ad. */
export const roleLabels: Record<string, string> = {
  system_admin: 'Sistem Yöneticisi',
  agent: 'Destek Personeli',
  store_owner: 'Mağaza Sahibi',
  user: 'Kullanıcı',
  hr: 'İK Personeli',
  it_admin: 'BT Hesap Yöneticisi',
}

export const assetTypeLabels: Record<AssetType, string> = {
  Computer: 'Masaüstü Bilgisayar',
  Laptop: 'Dizüstü Bilgisayar',
  Monitor: 'Monitör',
  Printer: 'Yazıcı',
  Phone: 'Telefon',
  NetworkDevice: 'Ağ Cihazı',
  Software: 'Yazılım / Lisans',
  Other: 'Diğer',
  PointOfSale: 'Kasa / POS',
}

export const assetStatusLabels: Record<AssetStatus, string> = {
  InUse: 'Kullanımda',
  InStock: 'Depoda',
  InRepair: 'Tamirde',
  Retired: 'Hurdaya Ayrıldı',
}

// Tailwind sınıfları derleme anında taranır; bu yüzden tam sınıf adları burada açıkça yazılı olmalı.
export const statusClasses: Record<TicketStatus, string> = {
  New: "bg-indigo-500/15 text-indigo-700 dark:text-indigo-300",
  Open: "bg-sky-500/15 text-sky-700 dark:text-sky-400",
  InProgress: "bg-amber-500/15 text-amber-700 dark:text-amber-400",
  Pending: "bg-violet-500/15 text-violet-700 dark:text-violet-400",
  Resolved: "bg-emerald-500/15 text-emerald-700 dark:text-emerald-400",
  Closed: "bg-gray-400/20 text-gray-600 dark:text-gray-400",
  Rejected: "bg-rose-500/15 text-rose-700 dark:text-rose-400",
}

export const priorityClasses: Record<TicketPriority, string> = {
  Low: "bg-gray-400/15 text-gray-600 dark:text-gray-400",
  Medium: "bg-sky-500/15 text-sky-700 dark:text-sky-400",
  High: "bg-orange-500/15 text-orange-700 dark:text-orange-400",
  Critical: "bg-rose-500/15 text-rose-700 dark:text-rose-400",
}

export const priorityDot: Record<TicketPriority, string> = {
  Low: "bg-gray-400",
  Medium: "bg-sky-500",
  High: "bg-orange-500",
  Critical: "bg-red-600",
}

export const assetStatusClasses: Record<AssetStatus, string> = {
  InUse: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400',
  InStock: 'bg-sky-500/15 text-sky-700 dark:text-sky-400',
  InRepair: 'bg-amber-500/15 text-amber-700 dark:text-amber-400',
  Retired: 'bg-gray-400/20 text-gray-600 dark:text-gray-400',
}

export const categoryColors = ['violet', 'sky', 'emerald', 'amber', 'rose', 'indigo', 'teal', 'orange', 'slate', 'pink'] as const

export const colorClasses: Record<string, { badge: string; dot: string; hex: string }> = {
  violet: { badge: 'bg-violet-500/15 text-violet-700 dark:text-violet-400', dot: 'bg-violet-500', hex: '#8b5cf6' },
  sky: { badge: 'bg-sky-500/15 text-sky-700 dark:text-sky-400', dot: 'bg-sky-500', hex: '#0ea5e9' },
  emerald: { badge: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400', dot: 'bg-emerald-500', hex: '#10b981' },
  amber: { badge: 'bg-amber-500/15 text-amber-700 dark:text-amber-400', dot: 'bg-amber-500', hex: '#f59e0b' },
  rose: { badge: 'bg-rose-500/15 text-rose-700 dark:text-rose-400', dot: 'bg-rose-500', hex: '#f43f5e' },
  indigo: { badge: 'bg-indigo-500/15 text-indigo-700 dark:text-indigo-400', dot: 'bg-indigo-500', hex: '#6366f1' },
  teal: { badge: 'bg-teal-500/15 text-teal-700 dark:text-teal-400', dot: 'bg-teal-500', hex: '#14b8a6' },
  orange: { badge: 'bg-orange-500/15 text-orange-700 dark:text-orange-400', dot: 'bg-orange-500', hex: '#f97316' },
  slate: { badge: 'bg-slate-500/15 text-slate-700 dark:text-slate-400', dot: 'bg-slate-500', hex: '#64748b' },
  pink: { badge: 'bg-pink-500/15 text-pink-700 dark:text-pink-400', dot: 'bg-pink-500', hex: '#ec4899' },
}

export const colorOf = (c?: string | null) => colorClasses[c ?? 'violet'] ?? colorClasses.violet

export const employmentLabels: Record<import('./types').EmploymentType, string> = {
  FullTime: 'Tam zamanlı',
  PartTime: 'Yarı zamanlı',
  Contract: 'Sözleşmeli',
  Intern: 'Stajyer',
}

/** "2 yıl 3 ay" — işe başlama tarihinden bugüne kıdem. */
export function seniority(hireDate?: string | null) {
  if (!hireDate) return null
  const start = new Date(`${hireDate}T00:00:00`)
  const now = new Date()
  let months = (now.getFullYear() - start.getFullYear()) * 12 + now.getMonth() - start.getMonth()
  if (now.getDate() < start.getDate()) months--
  if (months < 0) return 'Henüz başlamadı'
  const y = Math.floor(months / 12)
  const m = months % 12
  return [y > 0 && `${y} yıl`, m > 0 && `${m} ay`].filter(Boolean).join(' ') || '1 aydan az'
}
