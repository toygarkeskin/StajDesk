// Backend DTO'larının TypeScript karşılıkları (enum'lar API'den metin olarak gelir).

export type TicketStatus = 'New' | 'Open' | 'InProgress' | 'Pending' | 'Resolved' | 'Closed' | 'Rejected'
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
  | 'employees.read.private' | 'employees.manage' | 'org.manage' | 'accounts.request'
  | 'content.manage' | 'ads.manage' | 'widgets.manage'
  | 'leave.request' | 'leave.manage' | 'workflows.manage' | 'tasks.manage'

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
  username?: string | null
  title?: string | null
  /** Profil fotoğrafının sürüm anahtarı; yoksa baş harfler gösterilir. */
  photoVersion?: string | null
  language?: 'tr' | 'en'
  theme?: 'system' | 'light' | 'dark'
}

export interface UserListItem extends UserSummary {
  department?: string | null
  departmentId?: number | null
  username?: string | null
  /** false: davet gönderildi, kullanıcı henüz şifresini belirlemedi */
  passwordSet?: boolean
  lockedAt?: string | null
  lockReason?: string | null
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
  /** İlgili departman */
  department?: Ref | null
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
  /** Talep dışı bildirimlerde gidilecek portal adresi (ör. /approvals) */
  link?: string | null
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
  targetDepartmentId?: number | null
  targetDepartment?: string | null
  isRead: boolean
  attachmentName?: string | null
  attachmentContentType?: string | null
  attachmentSize?: number | null
  /** Yalnızca duyuru yöneticisine dolu gelir */
  readCount?: number | null
  audienceCount?: number | null
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

// ---------- Organizasyon ve personel ----------

export type EmploymentType = 'FullTime' | 'PartTime' | 'Contract' | 'Intern'
export interface Ref { id: number; name: string }

export interface EmployeeCard {
  id: number
  fullName: string
  email: string
  phone?: string | null
  title?: string | null
  department?: Ref | null
  store?: StoreRef | null
  hasPhoto: boolean
  photoVersion?: string | null
  isActive: boolean
}

export interface EmployeePrivate {
  birthDate?: string | null
  personalEmail?: string | null
  address?: string | null
  emergencyContactName?: string | null
  emergencyContactPhone?: string | null
  emergencyContactRelation?: string | null
}

export interface EmployeeDetail {
  id: number
  fullName: string
  email: string
  username?: string | null
  phone?: string | null
  isActive: boolean
  position?: Ref | null
  positionLevel?: number | null
  department?: Ref | null
  manager?: UserSummary | null
  store?: StoreRef | null
  employeeNumber?: string | null
  hireDate?: string | null
  terminationDate?: string | null
  employmentType: EmploymentType
  /** "MM-dd" */
  birthday?: string | null
  directReports: EmployeeCard[]
  hasPhoto: boolean
  hasCover: boolean
  photoVersion?: string | null
  coverVersion?: string | null
  private?: EmployeePrivate | null
  roles: string[]
  emailVerified: boolean
  lastLoginAt?: string | null
  access: { isSelf: boolean; canEditPersonal: boolean; canEditWork: boolean; canSeePrivate: boolean }
}

export interface Department {
  id: number
  code: string
  name: string
  description?: string | null
  isActive: boolean
  parentId?: number | null
  parentName?: string | null
  manager?: UserSummary | null
  memberCount: number
}

export interface Position { id: number; title: string; level: number; description?: string | null; memberCount: number }

export interface OrgPerson { id: number; fullName: string; title?: string | null; level?: number | null; managerId?: number | null; departmentId?: number | null; hasPhoto: boolean; photoVersion?: string | null }
export interface OrgChart { departments: Department[]; people: OrgPerson[] }

export interface UserSettings { language: 'tr' | 'en'; theme: 'system' | 'light' | 'dark'; emailTicketUpdates: boolean; emailAnnouncements: boolean; showBirthday: boolean }
export interface SecurityInfo { smsAvailable: boolean; smsProvider?: string | null; twoFactorEnabled: boolean; maskedPhone?: string | null; hasPhone: boolean }

// ---------- Kurumsal hesap talepleri ----------

export type AccountRequestType = 'NewAccount' | 'Deactivation'
export type AccountRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'

export interface AccountRequestItem {
  id: number
  type: AccountRequestType
  status: AccountRequestStatus
  fullName: string
  email?: string | null
  employeeNumber?: string | null
  department?: Ref | null
  position?: Ref | null
  manager?: UserSummary | null
  store?: StoreRef | null
  employmentType: EmploymentType
  hireDate?: string | null
  phone?: string | null
  targetUser?: UserSummary | null
  terminationDate?: string | null
  note?: string | null
  requestedBy: UserSummary
  createdAt: string
  reviewedBy?: UserSummary | null
  reviewedAt?: string | null
  reviewNote?: string | null
  createdUserId?: number | null
}

/** CSV ile toplu hesap talebi: satır sonucu ve özet. */
export interface AccountImportRow {
  line: number
  fullName: string
  email: string
  employeeNumber?: string | null
  department?: string | null
  position?: string | null
  employmentType: EmploymentType
  hireDate?: string | null
  phone?: string | null
  valid: boolean
  errors: string[]
  requestId?: number | null
}
export interface AccountImportResult {
  dryRun: boolean
  total: number
  validCount: number
  createdCount: number
  newDepartments: string[]
  newPositions: string[]
  rows: AccountImportRow[]
}

/** Toplu hesap talebi onayı sonucu. */
export interface BulkApproveResult {
  requested: number
  approved: number[]
  failed: { id: number; fullName?: string | null; reason: string }[]
}
