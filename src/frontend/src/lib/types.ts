// Backend DTO'larının TypeScript karşılıkları (enum'lar API'den metin olarak gelir).

export type UserRole = 'User' | 'Agent' | 'Admin'
export type TicketStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed'
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type TicketType = 'Incident' | 'Request'
export type AssetType = 'Computer' | 'Laptop' | 'Monitor' | 'Printer' | 'Phone' | 'NetworkDevice' | 'Software' | 'Other'
export type AssetStatus = 'InUse' | 'InStock' | 'InRepair' | 'Retired'
export type HistoryAction = 'Created' | 'StatusChanged' | 'Assigned' | 'PriorityChanged' | 'Updated'

export interface ApiResponse<T> {
  success: boolean
  data: T
  message?: string | null
  errors?: Record<string, string[]> | null
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface User {
  id: number
  fullName: string
  email: string
  role: UserRole
  department?: string | null
  phone?: string | null
  isActive: boolean
  createdAt: string
  lastLoginAt?: string | null
}

export interface UserSummary {
  id: number
  fullName: string
  email: string
  role: UserRole
}

export interface UserListItem extends UserSummary {
  department?: string | null
  isActive: boolean
  createdAt: string
  lastLoginAt?: string | null
  openTicketCount: number
  assetCount: number
}

export interface AuthResponse {
  token: string
  expiresAt: string
  user: User
}

export interface CategoryRef {
  id: number
  name: string
  color: string
}

export interface Category extends CategoryRef {
  description?: string | null
  isActive: boolean
  ticketCount: number
  openTicketCount: number
}

export interface TicketListItem {
  id: number
  title: string
  status: TicketStatus
  priority: TicketPriority
  type: TicketType
  category: CategoryRef
  requester: UserSummary
  assignee?: UserSummary | null
  commentCount: number
  createdAt: string
  updatedAt?: string | null
}

export interface TicketComment {
  id: number
  body: string
  isInternal: boolean
  author: UserSummary
  createdAt: string
}

export interface TicketHistory {
  id: number
  action: HistoryAction
  oldValue?: string | null
  newValue?: string | null
  actor?: UserSummary | null
  createdAt: string
}

export interface TicketDetail {
  id: number
  title: string
  description: string
  status: TicketStatus
  priority: TicketPriority
  type: TicketType
  category: CategoryRef
  requester: UserSummary
  assignee?: UserSummary | null
  asset?: { id: number; name: string; inventoryNumber: string; type: AssetType } | null
  createdAt: string
  updatedAt?: string | null
  resolvedAt?: string | null
  closedAt?: string | null
  comments: TicketComment[]
  history: TicketHistory[]
  allowedStatuses: TicketStatus[]
}

export interface AssetListItem {
  id: number
  name: string
  type: AssetType
  status: AssetStatus
  inventoryNumber: string
  serialNumber?: string | null
  manufacturer?: string | null
  model?: string | null
  location?: string | null
  assignedUser?: UserSummary | null
  warrantyEndDate?: string | null
  openTicketCount: number
}

export interface AssetDetail extends Omit<AssetListItem, 'openTicketCount'> {
  purchaseDate?: string | null
  notes?: string | null
  createdAt: string
  updatedAt?: string | null
  tickets: { id: number; title: string; status: TicketStatus; priority: TicketPriority; createdAt: string }[]
}

export interface NotificationItem {
  id: number
  title: string
  message: string
  ticketId?: number | null
  isRead: boolean
  createdAt: string
}

export interface CountItem {
  key: string
  label: string
  count: number
  color?: string | null
}

export interface DashboardStats {
  total: number
  open: number
  inProgress: number
  resolved: number
  closed: number
  unassigned: number
  criticalOpen: number
  avgResolutionHours?: number | null
  byStatus: CountItem[]
  byPriority: CountItem[]
  byCategory: CountItem[]
  last14Days: { date: string; created: number; resolved: number }[]
  topAgents: CountItem[]
  recent: TicketListItem[]
  assetCount: number
  generatedAt: string
}
