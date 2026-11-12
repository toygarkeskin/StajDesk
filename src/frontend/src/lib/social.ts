import { http } from './api'
import type { ApiResponse, PagedResult } from './types'

export interface SocialAuthor { id: number; fullName: string; title?: string | null; department?: string | null; hasPhoto: boolean; photoVersion?: string | null }
export interface SocialPost {
  id: number; author: SocialAuthor; body: string; hasImage: boolean; createdAt: string; updatedAt?: string | null
  likeCount: number; likedByMe: boolean; commentCount: number; canDelete: boolean; canModerate: boolean
}
export interface SocialComment { id: number; author: SocialAuthor; body: string; createdAt: string; canDelete: boolean }
export interface SocialReport { id: number; postId: number; postExcerpt: string; postAuthor: string; reporter: string; reason: string; createdAt: string; postRemoved: boolean }
export interface Conversation { id: number; other: SocialAuthor; lastMessage?: string | null; lastIsMine: boolean; lastMessageAt: string; unread: number }
export interface DirectMessage { id: number; senderId: number; body: string; createdAt: string; readAt?: string | null; isMine: boolean }
export interface FaqItem { id: number; category: string; question: string; answer: string; sortOrder: number; isPublished: boolean }

const unwrap = <T,>(p: Promise<{ data: ApiResponse<T> }>) => p.then((r) => r.data.data)

export const socialApi = {
  feed: (page: number) => unwrap<PagedResult<SocialPost>>(http.get('/social/posts', { params: { page, pageSize: 10 } })),
  create: (body: string, image?: File | null) => {
    const form = new FormData()
    form.append('body', body)
    if (image) form.append('image', image)
    return unwrap<SocialPost>(http.post('/social/posts', form, { headers: { 'Content-Type': 'multipart/form-data' } }))
  },
  remove: (id: number) => http.delete(`/social/posts/${id}`),
  like: (id: number) => unwrap<SocialPost>(http.post(`/social/posts/${id}/like`)),
  comments: (id: number) => unwrap<SocialComment[]>(http.get(`/social/posts/${id}/comments`)),
  comment: (id: number, body: string) => unwrap<SocialComment>(http.post(`/social/posts/${id}/comments`, { body })),
  removeComment: (id: number) => http.delete(`/social/comments/${id}`),
  report: (id: number, reason: string) => unwrap<object>(http.post(`/social/posts/${id}/report`, { reason })),
  moderate: (id: number, reason: string) => unwrap<object>(http.post(`/social/posts/${id}/remove`, { reason })),
  reports: () => unwrap<SocialReport[]>(http.get('/social/reports')),
  resolve: (id: number, removePost: boolean, reason?: string | null) => unwrap<object>(http.post(`/social/reports/${id}/resolve`, { removePost, reason })),
  image: (id: number) => http.get<Blob>(`/social/posts/${id}/image`, { responseType: 'blob' }).then((r) => r.data),

  conversations: () => unwrap<Conversation[]>(http.get('/messages/conversations')),
  unread: () => unwrap<number>(http.get('/messages/unread-count')),
  thread: (userId: number) => unwrap<DirectMessage[]>(http.get(`/messages/with/${userId}`)),
  send: (userId: number, body: string) => unwrap<DirectMessage>(http.post(`/messages/with/${userId}`, { body })),
  markRead: (userId: number) => http.post(`/messages/with/${userId}/read`),

  faq: (q?: string) => unwrap<FaqItem[]>(http.get('/faq', { params: q ? { q } : {} })),
  saveFaq: (id: number | null, body: Omit<FaqItem, 'id'>) => unwrap<FaqItem>(id ? http.put(`/faq/${id}`, body) : http.post('/faq', body)),
  removeFaq: (id: number) => http.delete(`/faq/${id}`),
}
