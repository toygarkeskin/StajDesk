import type { ReactNode } from 'react'
import {
  Bell, Building2, CalendarClock, FolderKanban, Globe, LayoutDashboard, LayoutGrid, Mail, Megaphone, MonitorSmartphone, Network, PlusCircle, ScrollText,
  ShieldCheck, Store as StoreIcon, Tags, Ticket, UserCheck, Users, UsersRound,
} from 'lucide-react'
import { useCan } from '@/stores/auth'

/* =========================================================
   Portal menü kaydı: kenar çubuğu ve genel arama ("Sayfalar" grubu) aynı listeyi kullanır.
   Menü rol adına değil İZİNLERE göre oluşur. Henüz yapılmamış uygulama menüye eklenmez (boş sayfa yok).
   ========================================================= */

export interface NavItem {
  to: string
  label: string
  icon: ReactNode
  /** Genel aramada eşleşmesi için ek kelimeler */
  keywords?: string
  isActive: (pathname: string, search: URLSearchParams) => boolean
}
export interface NavGroup { id: string; title: string; items: NavItem[] }

export function useNavGroups(): NavGroup[] {
  const { can } = useCan()
  const i = 'size-[18px]'
  const isTicketPage = (p: string) => p === '/tickets' || /^\/tickets\/\d+$/.test(p)
  const ticketsLabel = can('tickets.read.all') ? 'Tüm talepler' : can('tickets.read.store') ? 'Mağaza talepleri' : 'Taleplerim'
  const keep = (items: (NavItem | false)[]) => items.filter(Boolean) as NavItem[]

  const groups: NavGroup[] = [
    {
      id: 'general', title: 'Genel', items: keep([
        { to: '/portal', label: 'Ana sayfa', keywords: 'özet panel dashboard widget', icon: <LayoutDashboard className={i} />, isActive: (p) => p === '/portal' },
        { to: '/announcements', label: 'Duyurular', keywords: 'haber bilgilendirme', icon: <Megaphone className={i} />, isActive: (p) => p === '/announcements' },
        { to: '/notifications', label: 'Bildirimler', icon: <Bell className={i} />, isActive: (p) => p === '/notifications' },
        { to: '/oncall', label: 'Nöbetçi personel', keywords: 'nöbet çizelgesi bt', icon: <CalendarClock className={i} />, isActive: (p) => p === '/oncall' },
      ]),
    },
    {
      id: 'tickets', title: 'Destek Talep Sistemi', items: keep([
        { to: '/tickets', label: ticketsLabel, keywords: 'talep destek arıza', icon: <Ticket className={i} />, isActive: (p, q) => isTicketPage(p) && q.get('mine') !== '1' },
        can('tickets.create') && { to: '/tickets/new', label: 'Yeni talep', keywords: 'talep aç oluştur', icon: <PlusCircle className={i} />, isActive: (p) => p === '/tickets/new' },
        can('tickets.manage') && { to: '/tickets?mine=1', label: 'Bana atananlar', keywords: 'görevlerim', icon: <FolderKanban className={i} />, isActive: (p, q) => p === '/tickets' && q.get('mine') === '1' },
        can('categories.manage') && { to: '/admin/categories', label: 'Kategoriler', icon: <Tags className={i} />, isActive: (p) => p === '/admin/categories' },
      ]),
    },
    {
      id: 'people', title: 'Çalışanlar', items: keep([
        { to: '/employees', label: 'Çalışan rehberi', keywords: 'kişi personel arama', icon: <UsersRound className={i} />, isActive: (p) => p.startsWith('/employees') },
        { to: '/org', label: 'Organizasyon şeması', keywords: 'hiyerarşi yönetici', icon: <Network className={i} />, isActive: (p) => p === '/org' },
        can('accounts.request', 'users.manage') && { to: '/account-requests', label: 'Hesap talepleri', keywords: 'işe giriş çıkış hesap aç kapat', icon: <UserCheck className={i} />, isActive: (p) => p === '/account-requests' },
        can('org.manage') && { to: '/admin/org', label: 'Departmanlar ve unvanlar', keywords: 'csv içe aktar pozisyon', icon: <Building2 className={i} />, isActive: (p) => p === '/admin/org' },
      ]),
    },
    {
      id: 'operations', title: 'Mağaza ve operasyon', items: keep([
        can('stores.read', 'stores.manage') && { to: '/stores', label: can('stores.manage') ? 'Mağazalar' : 'Mağazam', icon: <StoreIcon className={i} />, isActive: (p) => p === '/stores' },
        can('assets.read.all', 'assets.read.store') && { to: '/assets', label: 'Envanter', keywords: 'cihaz zimmet donanım', icon: <MonitorSmartphone className={i} />, isActive: (p) => p.startsWith('/assets') },
        can('users.read', 'users.manage') && !can('users.manage') && { to: '/admin/users', label: 'Mağaza çalışanları', icon: <Users className={i} />, isActive: (p) => p === '/admin/users' },
      ]),
    },
    {
      id: 'admin', title: 'Yönetim', items: keep([
        can('users.manage') && { to: '/admin/users', label: 'Kullanıcı hesapları', keywords: 'kilit aktif pasif davet', icon: <Users className={i} />, isActive: (p) => p === '/admin/users' },
        can('roles.manage') && { to: '/admin/roles', label: 'Roller ve izinler', keywords: 'yetki rbac', icon: <ShieldCheck className={i} />, isActive: (p) => p === '/admin/roles' },
        can('content.manage', 'ads.manage', 'employees.manage') && { to: '/admin/content', label: 'Kurumsal site ve içerik', keywords: 'reklam banner haber ilan etkinlik takdir', icon: <Globe className={i} />, isActive: (p) => p === '/admin/content' },
        can('widgets.manage') && { to: '/admin/widgets', label: 'Kutucuk yönetimi', keywords: 'widget panel', icon: <LayoutGrid className={i} />, isActive: (p) => p === '/admin/widgets' },
        can('mail.read') && { to: '/admin/emails', label: 'Gönderilen e-postalar', icon: <Mail className={i} />, isActive: (p) => p === '/admin/emails' },
        can('audit.read') && { to: '/admin/audit', label: 'Denetim kayıtları', keywords: 'audit log', icon: <ScrollText className={i} />, isActive: (p) => p === '/admin/audit' },
      ]),
    },
  ]
  return groups.filter((g) => g.items.length > 0)
}
