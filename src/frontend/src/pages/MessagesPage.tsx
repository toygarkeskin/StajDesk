import { useDeferredValue, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { ArrowLeft, Check, CheckCheck, PenSquare, Send } from 'lucide-react'
import { api, errorMessage } from '@/lib/api'
import { formatDateTime, timeAgo } from '@/lib/format'
import { socialApi, type SocialAuthor } from '@/lib/social'
import { toast } from '@/stores/ui'
import { EmptyState, ErrorState, PageHeader, SlideOver, Spinner } from '@/components/ui'
import { PersonAvatar } from '@/components/AuthImage'

/* =========================================================
   Birebir mesajlaşma. Yeni mesaj SignalR ile anında gelir (bağlantı yoksa 20 sn'de bir yenilenir).
   Mesajı yalnızca iki taraf görür; sunucu her istekte bunu denetler.
   ========================================================= */

export function MessagesPage() {
  const [params, setParams] = useSearchParams()
  const withId = Number(params.get('with')) || null
  const [composing, setComposing] = useState(false)
  const conversations = useQuery({ queryKey: ['messages', 'conversations'], queryFn: socialApi.conversations, refetchInterval: 20_000 })
  const active = conversations.data?.find((c) => c.other.id === withId)?.other
  const open = (id: number) => setParams({ with: String(id) })

  return (
    <>
      <PageHeader title="Mesajlar" description="Çalışma arkadaşlarınızla birebir yazışma. Mesajları yalnızca siz ve karşı taraf görür."
        actions={<button className="btn-violet" onClick={() => setComposing(true)}><PenSquare className="size-4" aria-hidden="true" /> Yeni mesaj</button>} />
      <div className="card grid min-h-[60dvh] overflow-hidden md:grid-cols-[18rem_1fr]">
        <aside className={clsx('border-gray-100 md:border-r dark:border-gray-700/60', withId && 'hidden md:block')} aria-label="Konuşmalar">
          {conversations.isLoading ? <div className="p-4"><Spinner /></div> : conversations.isError ? <ErrorState message={errorMessage(conversations.error)} /> : conversations.data!.length === 0 ? (
            <EmptyState title="Konuşma yok" description="Yeni mesaj ile bir çalışma arkadaşınıza yazın." />
          ) : (
            <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
              {conversations.data!.map((c) => (
                <li key={c.id}>
                  <button type="button" onClick={() => open(c.other.id)} aria-current={c.other.id === withId ? 'true' : undefined}
                    className={clsx('flex w-full items-center gap-3 px-3 py-3 text-left hover:bg-gray-50 dark:hover:bg-gray-700/20', c.other.id === withId && 'bg-violet-500/10')}>
                    <PersonAvatar id={c.other.id} name={c.other.fullName} photoVersion={c.other.hasPhoto ? c.other.photoVersion : null} size="md" />
                    <span className="min-w-0 flex-1">
                      <span className="flex items-baseline justify-between gap-2">
                        <span className={clsx('truncate text-sm', c.unread ? 'font-bold text-gray-900 dark:text-gray-50' : 'font-medium text-gray-800 dark:text-gray-100')}>{c.other.fullName}</span>
                        <span className="shrink-0 text-[11px] text-gray-400">{timeAgo(c.lastMessageAt)}</span>
                      </span>
                      <span className="flex items-center justify-between gap-2">
                        <span className="truncate text-xs text-gray-500">{c.lastIsMine && 'Siz: '}{c.lastMessage}</span>
                        {c.unread > 0 && <span className="grid min-w-5 shrink-0 place-items-center rounded-full bg-violet-500 px-1.5 text-[11px] font-semibold text-white">{c.unread}</span>}
                      </span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </aside>
        <section className={clsx('flex min-h-0 flex-col', !withId && 'hidden md:flex')} aria-label="Konuşma">
          {withId ? <Thread userId={withId} person={active} onBack={() => setParams({})} /> : (
            <div className="grid flex-1 place-items-center p-8 text-center text-sm text-gray-500">Soldan bir konuşma seçin veya yeni mesaj yazın.</div>
          )}
        </section>
      </div>
      {composing && <NewMessagePanel onPick={(p) => { setComposing(false); open(p) }} onClose={() => setComposing(false)} />}
    </>
  )
}

function Thread({ userId, person, onBack }: { userId: number; person?: SocialAuthor; onBack: () => void }) {
  const qc = useQueryClient()
  const profile = useQuery({ queryKey: ['employee', userId], queryFn: () => api.employees.get(userId), enabled: !person })
  const name = person?.fullName ?? profile.data?.fullName ?? '…'
  const thread = useQuery({ queryKey: ['messages', 'thread', userId], queryFn: () => socialApi.thread(userId), refetchInterval: 20_000 })
  const [text, setText] = useState('')
  const bottom = useRef<HTMLDivElement>(null)
  const unreadFromThem = thread.data?.some((m) => !m.isMine && !m.readAt)
  useEffect(() => {
    bottom.current?.scrollIntoView({ block: 'end' })
    if (unreadFromThem) void socialApi.markRead(userId).then(() => qc.invalidateQueries({ queryKey: ['messages', 'conversations'] }).then(() => qc.invalidateQueries({ queryKey: ['messages', 'unread'] })))
  }, [thread.data, unreadFromThem, userId, qc])
  const send = useMutation({
    mutationFn: () => socialApi.send(userId, text.trim()),
    onSuccess: () => { setText(''); qc.invalidateQueries({ queryKey: ['messages'] }) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <>
      <header className="flex items-center gap-2 border-b border-gray-100 px-3 py-2 dark:border-gray-700/60">
        <button type="button" onClick={onBack} aria-label="Konuşmalara dön" className="grid size-11 place-items-center rounded-lg text-gray-500 hover:bg-gray-100 md:hidden dark:hover:bg-gray-700/40"><ArrowLeft className="size-5" aria-hidden="true" /></button>
        <PersonAvatar id={userId} name={name} photoVersion={person?.hasPhoto ? person.photoVersion : null} size="sm" />
        <div className="min-w-0"><div className="truncate font-semibold text-gray-800 dark:text-gray-100">{name}</div>{person && <div className="truncate text-xs text-gray-500">{[person.title, person.department].filter(Boolean).join(' · ')}</div>}</div>
      </header>
      <div className="min-h-0 flex-1 space-y-2 overflow-y-auto p-4" role="log" aria-live="polite" aria-label="Mesajlar">
        {thread.isLoading ? <Spinner /> : thread.isError ? <ErrorState message={errorMessage(thread.error)} /> : thread.data!.length === 0 ? (
          <p className="text-center text-sm text-gray-500">Henüz mesaj yok. İlk mesajı siz yazın.</p>
        ) : thread.data!.map((m) => (
          <div key={m.id} className={clsx('flex', m.isMine ? 'justify-end' : 'justify-start')}>
            <div className={clsx('max-w-[80%] rounded-2xl px-3 py-2 text-sm', m.isMine ? 'rounded-br-sm bg-violet-500 text-white' : 'rounded-bl-sm bg-gray-100 text-gray-800 dark:bg-gray-700/60 dark:text-gray-100')}>
              <p className="whitespace-pre-wrap break-words">{m.body}</p>
              <div className={clsx('mt-0.5 flex items-center justify-end gap-1 text-[10px]', m.isMine ? 'text-violet-100' : 'text-gray-400')} title={formatDateTime(m.createdAt)}>
                {timeAgo(m.createdAt)}
                {m.isMine && (m.readAt ? <CheckCheck className="size-3" aria-label="Okundu" /> : <Check className="size-3" aria-label="Gönderildi" />)}
              </div>
            </div>
          </div>
        ))}
        <div ref={bottom} />
      </div>
      <form className="flex gap-2 border-t border-gray-100 p-3 dark:border-gray-700/60" onSubmit={(e) => { e.preventDefault(); if (text.trim()) send.mutate() }}>
        <label htmlFor="msg-body" className="sr-only">Mesaj yaz</label>
        <textarea id="msg-body" rows={1} className="form-field max-h-32 min-h-11 resize-y" maxLength={2000} placeholder="Mesaj yazın…" value={text} onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); if (text.trim()) send.mutate() } }} />
        <button className="btn-violet" disabled={send.isPending || !text.trim()} aria-label="Gönder">{send.isPending ? <Spinner /> : <Send className="size-4" aria-hidden="true" />}</button>
      </form>
    </>
  )
}

function NewMessagePanel({ onPick, onClose }: { onPick: (userId: number) => void; onClose: () => void }) {
  const [search, setSearch] = useState('')
  const term = useDeferredValue(search.trim())
  const people = useQuery({ queryKey: ['employees', 'pick', term], queryFn: () => api.employees.list({ search: term, pageSize: 10 }), enabled: term.length >= 2 })
  return (
    <SlideOver open onClose={onClose} title="Yeni mesaj" description="Yazmak istediğiniz çalışma arkadaşınızı arayın."
      footer={<div className="flex justify-end"><button className="btn-secondary" onClick={onClose}>Vazgeç</button></div>}>
      <label htmlFor="msg-person" className="form-label">Kişi</label>
      <input id="msg-person" className="form-field" autoFocus placeholder="Ad, e-posta veya unvan" value={search} onChange={(e) => setSearch(e.target.value)} />
      <ul className="mt-2 divide-y divide-gray-100 dark:divide-gray-700/60">
        {people.data?.items.map((p) => (
          <li key={p.id}><button type="button" className="flex min-h-11 w-full items-center gap-3 px-1 py-2 text-left hover:bg-gray-50 dark:hover:bg-gray-700/30" onClick={() => onPick(p.id)}>
            <PersonAvatar id={p.id} name={p.fullName} photoVersion={p.hasPhoto ? p.photoVersion : null} size="sm" />
            <span><span className="block text-sm font-medium">{p.fullName}</span><span className="block text-xs text-gray-500">{[p.title, p.department?.name].filter(Boolean).join(' · ')}</span></span>
          </button></li>
        ))}
        {people.data && people.data.items.length === 0 && <li className="py-3 text-sm text-gray-500">Eşleşen çalışan yok.</li>}
      </ul>
    </SlideOver>
  )
}
