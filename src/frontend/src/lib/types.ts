// Backend DTO'larının TypeScript karşılıkları (enum'lar API'den metin olarak gelir).

export type TicketStatus = 'Open' | 'InProgress' | 'Pending' | 'Resolved' | 'Closed'
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type TicketType = 'Incident' | 'Request'
export type AssetType = 'Computer' | 'Laptop' | 'Monitor' | 'Printer' | 'Phone' | 'NetworkDevice' | 'Software' | 'Other' | 'PointOfSale'
export type AssetStatus = 'InUse' | 'InStock' | 'InRepair' | 'Retired'
export type HistoryAction = 'Created' | 'StatusChanged' | 'Assigned' | 'PriorityChanged' | 'Updated' | 'AttachmentAdded' | 'AttachmentRemoved'

/** Sunucudaki izin anahtarları (StajDesk.Domain.Security.Permissions). */
export type Permission =
  | 'tickets.create' | 'tickets.read.own' | 'tickets.read.store' | 'tickets.read.all' | 'tickets.update.own'
  | 'tickets.manage' | 'tickets.delete' | 'comments.create' | 'comments.internal'
  | 'categories.read' | 'categories.manage' | 'stores.read' | 'stores.manage'
  | 'assets.read.own' | 'assets.read.store' | 'assets.read.all' | 'assets.manage'
  | 'users.read' | 'users.manage' | 'roles.manage' | 'reports.read.store' | 'reports.read.all'
  | 'audit.read' | 'mail.read' | 'announcements.manage' | 'oncall.manage'

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

export interface StoreRef {
  id: number
  code: string
  name: string
}

export interface UserSummary {
  id: number
  fullName: string
  email: string
}

/** Oturumdaki kullanıcı: menüler ve ekranlar bu izin listesine göre çizilir. */
export interface Me {
  id: number
  fullName: string
  email: string
  department?: string | null
  store?: StoreRef | null
  roles: string[]
  roleNames: string[]
  permissions: Permission[]
  createdAt: string
}

export interface UserListItem extends UserSummary {
  department?: string | null
  phone?: string | null
  isActive: boolean
  store?: StoreRef | null
  roles: string[]
  createdAt: string
  lastLoginAt?: string | null
  openTicketCount: number
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
  store?: StoreRef | null
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
  author: { id: number; fullName: string; isStaff: boolean }
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

export interface TicketDetail extends Omit<TicketListItem, 'commentCount'> {
  description: string
  asset?: { id: number; name: string; inventoryNumber: string; type: AssetType } | null
  resolvedAt?: string | null
  closedAt?: string | null
  comments: TicketComment[]
  history: TicketHistory[]
  allowedStatuses: TicketStatus[]
  attachments: TicketAttachment[]
  permissions: { canEdit: boolean; canManage: boolean; canDelete: boolean; canComment: boolean; canInternal: boolean; canAttach: boolean }
}

export interface TicketAttachment {
  id: number
  fileName: string
  contentType: string
  size: number
  uploadedBy: UserSummary
  createdAt: string
  canDelete: boolean
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
  store?: StoreRef | null
  assignedUser?: UserSummary | null
  warrantyEndDate?: string | null
  openTicketCount: number
}

export interface AssetDetail extends Omit<AssetListItem, 'openTicketCount'> {
  purchaseDate?: string | null
  notes?: string | null
  createdAt: string
  updatedAt?: string | null
  canManage: boolean
  tickets: { id: number; title: string; status: TicketStatus; priority: TicketPriority; createdAt: string }[]
}

export interface Store extends StoreRef {
  city?: string | null
  address?: string | null
  phone?: string | null
  isActive: boolean
  userCount: number
  openTicketCount: number
  assetCount: number
  owners: string[]
}

export interface RoleInfo {
  id: number
  name: string
  displayName: string
  description?: string | null
  isSystem: boolean
  userCount: number
  permissions: Permission[]
}

export interface PermissionInfo {
  key: Permission
  group: string
  description: string
}

export interface AuditLog {
  id: number
  action: string
  entity: string
  entityId?: string | null
  data?: string | null
  ip?: string | null
  actorName?: string | null
  createdAt: string
}

export interface SentEmail {
  id: number
  toAddress: string
  subject: string
  template: string
  status: 'sent' | 'failed'
  error?: string | null
  createdAt: string
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
  scope: 'all' | 'store' | 'user'
  scopeLabel: string
  total: number
  open: number
  inProgress: number
  pending: number
  resolved: number
  closed: number
  unassigned: number
  criticalOpen: number
  avgResolutionHours?: number | null
  byStatus: CountItem[]
  byPriority: CountItem[]
  byCategory: CountItem[]
  byStore: CountItem[]
  last14Days: { date: string; created: number; resolved: number }[]
  topAgents: CountItem[]
  recent: TicketListItem[]
  assetCount: number
  userCount: number
  storeCount: number
  generatedAt: string
}

// ---------- Ana sayfa paneli ----------

export type AnnouncementLevel = 'Info' | 'Warning' | 'Critical'

export interface Announcement {
  id: number
  title: string
  body: string
  level: AnnouncementLevel
  isPinned: boolean
  publishAt: string
  expiresAt?: string | null
  storeId?: number | null
  storeName?: string | null
  authorName: string
}

export interface OnCallShift {
  id: number
  userId: number
  fullName: string
  email: string
  phone?: string | null
  department?: string | null
  /** yyyy-MM-dd */
  startsOn: string
  endsOn: string
  note?: string | null
  isCurrent: boolean
}

export interface Activity {
  ticketId: number
  ticketTitle: string
  action: HistoryAction
  oldValue?: string | null
  newValue?: string | null
  actorName?: string | null
  createdAt: string
}

export interface Home {
  welcome: { userId: number; fullName: string; department?: string | null; storeName?: string | null; roles: string[]; memberSince: string }
  counters: { myOpenTickets: number; awaitingMyApproval: number; assignedToMe: number; myAssets: number; unreadNotifications: number }
  announcements: Announcement[]
  onCall: OnCallShift[]
  myTasks: TicketListItem[]
  awaitingApproval: TicketListItem[]
  activity: Activity[]
}
