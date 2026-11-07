const dateFmt = new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: 'short', year: 'numeric' })
const dateTimeFmt = new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
const rtf = new Intl.RelativeTimeFormat('tr-TR', { numeric: 'auto' })

export const formatDate = (iso?: string | null) => (iso ? dateFmt.format(new Date(iso)) : '—')
export const formatDateTime = (iso?: string | null) => (iso ? dateTimeFmt.format(new Date(iso)) : '—')

export function timeAgo(iso: string) {
  const diff = (new Date(iso).getTime() - Date.now()) / 1000
  const abs = Math.abs(diff)
  if (abs < 60) return 'az önce'
  if (abs < 3600) return rtf.format(Math.round(diff / 60), 'minute')
  if (abs < 86400) return rtf.format(Math.round(diff / 3600), 'hour')
  if (abs < 86400 * 30) return rtf.format(Math.round(diff / 86400), 'day')
  return formatDate(iso)
}

export const initials = (name: string) =>
  name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toLocaleUpperCase('tr-TR'))
    .join('')
