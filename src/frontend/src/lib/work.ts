import { http } from './api'
import type { ApiResponse, PagedResult } from './types'

/* ---------- İş akışı (onay) ---------- */

export type ApproverType = 'Manager' | 'SecondLevelManager' | 'DepartmentManager' | 'User' | 'Role'
export type WorkflowStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'
export type ApprovalDecision = 'Pending' | 'Approved' | 'Rejected' | 'Skipped'

export const approverTypeLabels: Record<ApproverType, string> = {
  Manager: 'Talep sahibinin yöneticisi',
  SecondLevelManager: 'Yöneticinin yöneticisi (müdür)',
  DepartmentManager: 'Departman yöneticisi',
  User: 'Belirli bir kişi',
  Role: 'Bir role sahip herkes',
}
export const workflowStatusLabels: Record<WorkflowStatus, string> = { Pending: 'Onay bekliyor', Approved: 'Onaylandı', Rejected: 'Reddedildi', Cancelled: 'Geri çekildi' }
export const workflowStatusClasses: Record<WorkflowStatus, string> = {
  Pending: 'bg-amber-500/10 text-amber-700 dark:text-amber-300',
  Approved: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300',
  Rejected: 'bg-rose-500/10 text-rose-700 dark:text-rose-300',
  Cancelled: 'bg-gray-500/10 text-gray-600 dark:text-gray-400',
}

export interface WorkflowStep { id: number; order: number; name: string; approverType: ApproverType; roleName?: string | null; userId?: number | null; userName?: string | null; departmentId?: number | null; departmentName?: string | null }
export interface WorkflowDefinition { id: number; key: string; name: string; description?: string | null; isActive: boolean; steps: WorkflowStep[] }
export interface ApprovalStep { id: number; stepOrder: number; stepName: string; approverName?: string | null; approverRole?: string | null; decision: ApprovalDecision; decidedByName?: string | null; decidedAt?: string | null; comment?: string | null; isCurrent: boolean }
export interface WorkflowInstance {
  id: number; key: string; workflowName: string; title: string; subjectType: string; subjectId: number; link?: string | null
  requesterId: number; requesterName: string; status: WorkflowStatus; createdAt: string; completedAt?: string | null
  steps: ApprovalStep[]; canDecide: boolean; myApprovalId?: number | null
}
export interface ApprovalInboxItem {
  approvalId: number; instanceId: number; workflowName: string; title: string; summary?: string | null; link?: string | null; stepName: string
  requesterId: number; requesterName: string; requesterDepartment?: string | null; createdAt: string
}

/* ---------- Görevler ---------- */

export type WorkTaskStatus = 'Todo' | 'InProgress' | 'Done' | 'Cancelled'
export type WorkTaskPriority = 'Low' | 'Normal' | 'High'
export const taskStatusLabels: Record<WorkTaskStatus, string> = { Todo: 'Yapılacak', InProgress: 'Devam ediyor', Done: 'Tamamlandı', Cancelled: 'İptal' }
export const taskStatusClasses: Record<WorkTaskStatus, string> = {
  Todo: 'bg-sky-500/10 text-sky-700 dark:text-sky-300',
  InProgress: 'bg-amber-500/10 text-amber-700 dark:text-amber-300',
  Done: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300',
  Cancelled: 'bg-gray-500/10 text-gray-600 dark:text-gray-400',
}
export const taskPriorityLabels: Record<WorkTaskPriority, string> = { Low: 'Düşük', Normal: 'Normal', High: 'Yüksek' }
export interface WorkTask {
  id: number; title: string; description?: string | null; assignee: { id: number; fullName: string }; createdBy: { id: number; fullName: string }
  dueDate?: string | null; priority: WorkTaskPriority; status: WorkTaskStatus; createdAt: string; completedAt?: string | null
  isOverdue: boolean; canEdit: boolean; canChangeStatus: boolean
}

/* ---------- İzin ---------- */

export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'
export interface LeaveType { id: number; code: string; name: string; description?: string | null; tracksBalance: boolean; defaultDaysPerYear: number; isPaid: boolean; isActive: boolean; sortOrder: number }
export interface LeaveBalance { leaveTypeId: number; leaveType: string; year: number; entitled: number; isCustom: boolean; used: number; pending: number; remaining: number }
export interface LeaveRequest {
  id: number; userId: number; userName: string; department?: string | null; leaveTypeId: number; leaveType: string
  startDate: string; endDate: string; days: number; reason?: string | null; status: LeaveStatus; createdAt: string; workflowInstanceId?: number | null
}

const unwrap = <T,>(p: Promise<{ data: ApiResponse<T> }>) => p.then((r) => r.data.data)
type Q = Record<string, string | number | boolean | undefined | null>
const clean = (q: Q) => Object.fromEntries(Object.entries(q).filter(([, v]) => v !== undefined && v !== null && v !== ''))

export const workApi = {
  tasks: (q: Q) => unwrap<PagedResult<WorkTask>>(http.get('/tasks', { params: clean(q) })),
  taskSummary: () => unwrap<{ open: number; overdue: number; dueToday: number }>(http.get('/tasks/summary')),
  task: (id: number) => unwrap<WorkTask>(http.get(`/tasks/${id}`)),
  saveTask: (id: number | null, body: { title: string; description?: string | null; assigneeId?: number | null; dueDate?: string | null; priority: WorkTaskPriority }) =>
    unwrap<WorkTask>(id ? http.put(`/tasks/${id}`, body) : http.post('/tasks', body)),
  setTaskStatus: (id: number, status: WorkTaskStatus) => unwrap<WorkTask>(http.patch(`/tasks/${id}/status`, { status })),
  removeTask: (id: number) => http.delete(`/tasks/${id}`),

  inbox: (page = 1) => unwrap<PagedResult<ApprovalInboxItem>>(http.get('/approvals', { params: { page, pageSize: 20 } })),
  inboxCount: () => unwrap<number>(http.get('/approvals/count')),
  instance: (id: number) => unwrap<WorkflowInstance>(http.get(`/approvals/instances/${id}`)),
  approve: (approvalId: number, comment?: string | null) => unwrap<WorkflowInstance>(http.post(`/approvals/${approvalId}/approve`, { comment })),
  reject: (approvalId: number, comment: string) => unwrap<WorkflowInstance>(http.post(`/approvals/${approvalId}/reject`, { comment })),
  workflows: () => unwrap<WorkflowDefinition[]>(http.get('/workflows')),
  saveWorkflow: (id: number, body: object) => unwrap<WorkflowDefinition>(http.put(`/workflows/${id}`, body)),
  workflowRoles: () => unwrap<{ name: string; displayName: string }[]>(http.get('/workflows/roles')),

  leaveTypes: (includeInactive = false) => unwrap<LeaveType[]>(http.get('/leave/types', { params: { includeInactive } })),
  saveLeaveType: (id: number | null, body: object) => unwrap<LeaveType>(id ? http.put(`/leave/types/${id}`, body) : http.post('/leave/types', body)),
  balances: (userId?: number, year?: number) => unwrap<LeaveBalance[]>(http.get('/leave/balances', { params: clean({ userId, year }) })),
  setEntitlement: (body: { userId: number; leaveTypeId: number; year: number; days: number; note?: string | null }) => unwrap<object>(http.put('/leave/entitlements', body)),
  countDays: (start: string, end: string) => unwrap<{ days: number; excludedHolidays: string[] }>(http.get('/leave/days', { params: { start, end } })),
  leaveRequests: (q: Q) => unwrap<PagedResult<LeaveRequest>>(http.get('/leave/requests', { params: clean(q) })),
  leaveRequest: (id: number) => unwrap<LeaveRequest>(http.get(`/leave/requests/${id}`)),
  leaveWorkflow: (id: number) => unwrap<WorkflowInstance | null>(http.get(`/leave/requests/${id}/workflow`)),
  createLeave: (body: { leaveTypeId: number; startDate: string; endDate: string; reason?: string | null }) => unwrap<LeaveRequest>(http.post('/leave/requests', body)),
  cancelLeave: (id: number) => unwrap<object>(http.post(`/leave/requests/${id}/cancel`)),
}
