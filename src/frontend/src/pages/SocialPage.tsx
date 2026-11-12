import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { Flag, Heart, ImagePlus, MessageCircle, MessageSquare, ShieldAlert, Trash2, X } from 'lucide-react'
import { errorMessage } from '@/lib/api'
import { timeAgo } from '@/lib/format'
import { socialApi, type SocialPost } from '@/lib/social'
import { useAuth, useCan } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { BlockSkeleton, ConfirmDialog, EmptyState, ErrorState, Field, IconButton, PageHeader, SlideOver, Spinner } from '@/components/ui'
import { PersonAvatar } from '@/components/AuthImage'

/* =========================================================
   Sosyal akış: yalnızca kurumsal ve uygun paylaşımlar. Uygunsuz içerik şikayet edilir; moderatör kaldırır.
   Görseller oturum bilgisiyle indirilir (blob), doğrudan bağlantıyla açılmaz.
   ========================================================= */

export function SocialPage() {
  const { can } = useCan()
  const [params, setParams] = useSearchParams()
  const moderation = can('social.moderate') && params.get('tab') === 'moderation'
  return (
    <>
      <PageHeader title="Sosyal akış" description="Ekiplerden kurumsal paylaşımlar: başarılar, etkinlik fotoğrafları, duyurular. Kişisel ve uygunsuz içerik paylaşmayın." />
      {can('social.moderate') && (
        <div role="tablist" className="mb-4 flex gap-1 border-b border-gray-200 dark:border-gray-700/60">
          {[['feed', 'Akış'], ['moderation', 'Moderasyon']].map(([id, label]) => (
            <button key={id} role="tab" aria-selected={(id === 'moderation') === moderation} onClick={() => setParams(id === 'feed' ? {} : { tab: id })}
              className={clsx('min-h-11 border-b-2 px-3 text-sm font-medium', (id === 'moderation') === moderation ? 'border-violet-500 text-violet-600 dark:text-violet-300' : 'border-transparent text-gray-500 hover:text-gray-800 dark:hover:text-gray-200')}>
              {label}
            </button>
          ))}
        </div>
      )}
      {moderation ? <ModerationQueue /> : <Feed focusPost={Number(params.get('post')) || null} />}
    </>
  )
}

function Feed({ focusPost }: { focusPost: number | null }) {
  const { can } = useCan()
  const feed = useInfiniteQuery({
    queryKey: ['social', 'feed'],
    queryFn: ({ pageParam }) => socialApi.feed(pageParam),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  })
  const posts = feed.data?.pages.flatMap((p) => p.items) ?? []
  useEffect(() => { if (focusPost && posts.length) document.getElementById(`post-${focusPost}`)?.scrollIntoView({ block: 'start' }) }, [focusPost, posts.length])
  return (
    <div className="mx-auto max-w-2xl space-y-4">
      {can('social.post') && <Composer />}
      {feed.isLoading ? <BlockSkeleton className="h-48" /> : feed.isError ? <div className="card"><ErrorState message={errorMessage(feed.error)} onRetry={() => feed.refetch()} /></div>
        : posts.length === 0 ? <div className="card"><EmptyState title="Henüz paylaşım yok" description="İlk paylaşımı siz yapın: ekibinizin bir başarısını veya etkinlikten bir kareyi paylaşın." /></div>
        : posts.map((p) => <PostCard key={p.id} post={p} openComments={p.id === focusPost} />)}
      {feed.hasNextPage && (
        <button type="button" className="btn-secondary w-full" disabled={feed.isFetchingNextPage} onClick={() => feed.fetchNextPage()}>
          {feed.isFetchingNextPage && <Spinner />}Daha eski paylaşımları göster
        </button>
      )}
    </div>
  )
}

function Composer() {
  const qc = useQueryClient()
  const me = useAuth((s) => s.me)
  const [body, setBody] = useState('')
  const [image, setImage] = useState<File | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  useEffect(() => {
    if (!image) { setPreview(null); return }
    const url = URL.createObjectURL(image)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [image])
  const post = useMutation({
    mutationFn: () => socialApi.create(body.trim(), image),
    onSuccess: () => { setBody(''); setImage(null); toast('Paylaşıldı.'); qc.invalidateQueries({ queryKey: ['social'] }) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <section className="card p-4" aria-label="Yeni paylaşım">
      <div className="flex gap-3">
        {me && <PersonAvatar id={me.id} name={me.fullName} size="md" />}
        <div className="min-w-0 flex-1">
          <label htmlFor="post-body" className="sr-only">Paylaşım metni</label>
          <textarea id="post-body" className="form-field min-h-20" maxLength={2000} placeholder="Ekibinizden bir haber, başarı veya etkinlik paylaşın…" value={body} onChange={(e) => setBody(e.target.value)} />
          {preview && (
            <div className="relative mt-2 inline-block">
              <img src={preview} alt="Eklenecek görsel önizlemesi" className="max-h-48 rounded-lg" />
              <button type="button" onClick={() => setImage(null)} aria-label="Görseli kaldır" className="absolute top-1 right-1 grid size-8 place-items-center rounded-full bg-gray-900/70 text-white"><X className="size-4" aria-hidden="true" /></button>
            </div>
          )}
          <div className="mt-2 flex items-center justify-between gap-2">
            <label className="inline-flex min-h-11 cursor-pointer items-center gap-1.5 rounded-lg px-2 text-sm text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700/40">
              <ImagePlus className="size-4" aria-hidden="true" /> Görsel ekle
              <input type="file" className="sr-only" accept="image/png,image/jpeg,image/webp" onChange={(e) => { setImage(e.target.files?.[0] ?? null); e.target.value = '' }} />
            </label>
            <span className="text-xs text-gray-400 tabular-nums">{body.length}/2000</span>
            <button type="button" className="btn-violet" disabled={post.isPending || (!body.trim() && !image)} onClick={() => post.mutate()}>{post.isPending && <Spinner />}Paylaş</button>
          </div>
        </div>
      </div>
    </section>
  )
}

function PostImage({ id }: { id: number }) {
  const q = useQuery({ queryKey: ['social', 'image', id], queryFn: () => socialApi.image(id), staleTime: Infinity })
  const [url, setUrl] = useState<string | null>(null)
  useEffect(() => {
    if (!q.data) return
    const u = URL.createObjectURL(q.data)
    setUrl(u)
    return () => URL.revokeObjectURL(u)
  }, [q.data])
  if (q.isError) return <p className="mt-3 rounded-lg bg-gray-50 p-3 text-xs text-gray-500 dark:bg-gray-900/40">Görsel yüklenemedi.</p>
  return url ? <img src={url} alt="Paylaşım görseli" className="mt-3 max-h-[28rem] w-full rounded-lg object-contain" /> : <BlockSkeleton className="mt-3 h-48" />
}

function PostCard({ post, openComments = false }: { post: SocialPost; openComments?: boolean }) {
  const qc = useQueryClient()
  const [showComments, setShowComments] = useState(openComments)
  const [confirm, setConfirm] = useState(false)
  const [reporting, setReporting] = useState<'report' | 'moderate' | null>(null)
  const refresh = () => qc.invalidateQueries({ queryKey: ['social'] })
  const like = useMutation({ mutationFn: () => socialApi.like(post.id), onSuccess: refresh, onError: (e) => toast(errorMessage(e), 'error') })
  const remove = useMutation({ mutationFn: () => socialApi.remove(post.id), onSuccess: () => { toast('Paylaşım silindi.'); setConfirm(false); refresh() }, onError: (e) => toast(errorMessage(e), 'error') })
  const meId = useAuth((s) => s.me?.id)
  const mine = post.author.id === meId
  return (
    <article id={`post-${post.id}`} className="card scroll-mt-20 p-4" aria-label={`${post.author.fullName} paylaşımı`}>
      <header className="flex items-start gap-3">
        <PersonAvatar id={post.author.id} name={post.author.fullName} photoVersion={post.author.hasPhoto ? post.author.photoVersion : null} size="md" />
        <div className="min-w-0 flex-1">
          <Link to={`/employees/${post.author.id}`} className="font-semibold text-gray-800 hover:text-violet-700 dark:text-gray-100">{post.author.fullName}</Link>
          <div className="truncate text-xs text-gray-500">{[post.author.title, post.author.department].filter(Boolean).join(' · ')} · <time dateTime={post.createdAt}>{timeAgo(post.createdAt)}</time></div>
        </div>
        {mine
          ? <IconButton label="Paylaşımı sil" tone="danger" onClick={() => setConfirm(true)}><Trash2 className="size-4" /></IconButton>
          : post.canModerate
            ? <IconButton label="Paylaşımı kaldır (moderasyon)" tone="danger" onClick={() => setReporting('moderate')}><ShieldAlert className="size-4" /></IconButton>
            : <IconButton label="Uygunsuz içerik olarak bildir" onClick={() => setReporting('report')}><Flag className="size-4" /></IconButton>}
      </header>
      {post.body && <p className="mt-3 text-sm whitespace-pre-wrap text-gray-800 dark:text-gray-200">{post.body}</p>}
      {post.hasImage && <PostImage id={post.id} />}
      <div className="mt-3 flex items-center gap-1 border-t border-gray-100 pt-2 dark:border-gray-700/60">
        <button type="button" onClick={() => like.mutate()} disabled={like.isPending} aria-pressed={post.likedByMe}
          className={clsx('inline-flex min-h-11 items-center gap-1.5 rounded-lg px-3 text-sm', post.likedByMe ? 'font-medium text-rose-600 dark:text-rose-400' : 'text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700/40')}>
          <Heart className={clsx('size-4', post.likedByMe && 'fill-current')} aria-hidden="true" /> {post.likeCount > 0 ? post.likeCount : ''} Beğen
        </button>
        <button type="button" onClick={() => setShowComments((v) => !v)} aria-expanded={showComments}
          className="inline-flex min-h-11 items-center gap-1.5 rounded-lg px-3 text-sm text-gray-600 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700/40">
          <MessageCircle className="size-4" aria-hidden="true" /> {post.commentCount > 0 ? post.commentCount : ''} Yorum
        </button>
      </div>
      {showComments && <Comments postId={post.id} />}
      <ConfirmDialog open={confirm} title="Paylaşım silinsin mi?" body="Paylaşım, yorumları ve beğenileriyle birlikte kalıcı olarak silinir." confirmLabel="Paylaşımı sil"
        pending={remove.isPending} onConfirm={() => remove.mutate()} onCancel={() => setConfirm(false)} />
      {reporting && <ReasonPanel postId={post.id} mode={reporting} onClose={() => setReporting(null)} />}
    </article>
  )
}

function Comments({ postId }: { postId: number }) {
  const qc = useQueryClient()
  const { can } = useCan()
  const q = useQuery({ queryKey: ['social', 'comments', postId], queryFn: () => socialApi.comments(postId) })
  const [text, setText] = useState('')
  const add = useMutation({
    mutationFn: () => socialApi.comment(postId, text.trim()),
    onSuccess: () => { setText(''); qc.invalidateQueries({ queryKey: ['social'] }) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  const remove = useMutation({ mutationFn: (id: number) => socialApi.removeComment(id), onSuccess: () => qc.invalidateQueries({ queryKey: ['social'] }), onError: (e) => toast(errorMessage(e), 'error') })
  return (
    <div className="mt-2 space-y-3">
      {q.isLoading ? <Spinner /> : q.data?.map((c) => (
        <div key={c.id} className="flex gap-2">
          <PersonAvatar id={c.author.id} name={c.author.fullName} photoVersion={c.author.hasPhoto ? c.author.photoVersion : null} size="sm" />
          <div className="min-w-0 flex-1 rounded-lg bg-gray-50 px-3 py-2 dark:bg-gray-900/40">
            <div className="flex items-center justify-between gap-2 text-xs">
              <span className="font-semibold text-gray-800 dark:text-gray-100">{c.author.fullName}</span>
              <span className="flex items-center gap-1 text-gray-400"><time dateTime={c.createdAt}>{timeAgo(c.createdAt)}</time>
                {c.canDelete && <button type="button" className="ml-1 font-medium text-rose-600" onClick={() => remove.mutate(c.id)}>Sil</button>}</span>
            </div>
            <p className="text-sm whitespace-pre-wrap text-gray-700 dark:text-gray-300">{c.body}</p>
          </div>
        </div>
      ))}
      {can('social.post') && (
        <form className="flex gap-2" onSubmit={(e) => { e.preventDefault(); if (text.trim()) add.mutate() }}>
          <label htmlFor={`c-${postId}`} className="sr-only">Yorum yaz</label>
          <input id={`c-${postId}`} className="form-field" maxLength={1000} placeholder="Yorum yazın…" value={text} onChange={(e) => setText(e.target.value)} />
          <button className="btn-secondary" disabled={add.isPending || !text.trim()}>{add.isPending ? <Spinner /> : 'Gönder'}</button>
        </form>
      )}
    </div>
  )
}

function ReasonPanel({ postId, mode, onClose }: { postId: number; mode: 'report' | 'moderate'; onClose: () => void }) {
  const qc = useQueryClient()
  const [reason, setReason] = useState('')
  const send = useMutation({
    mutationFn: () => (mode === 'report' ? socialApi.report(postId, reason.trim()) : socialApi.moderate(postId, reason.trim())),
    onSuccess: () => { toast(mode === 'report' ? 'Şikayetiniz moderatörlere iletildi.' : 'Paylaşım kaldırıldı; sahibine bildirildi.'); qc.invalidateQueries({ queryKey: ['social'] }); onClose() },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <SlideOver open onClose={onClose} title={mode === 'report' ? 'Paylaşımı bildir' : 'Paylaşımı kaldır'}
      description={mode === 'report' ? 'Moderatörler inceler. Kimin bildirdiği paylaşım sahibine gösterilmez.' : 'Gerekçe paylaşım sahibine bildirilir ve denetim kaydına yazılır.'}
      footer={<div className="flex justify-end gap-2"><button className="btn-secondary" onClick={onClose}>Vazgeç</button>
        <button className={mode === 'report' ? 'btn-violet' : 'btn-danger'} disabled={send.isPending || reason.trim().length < 3} onClick={() => send.mutate()}>{send.isPending && <Spinner />}{mode === 'report' ? 'Bildir' : 'Kaldır'}</button></div>}>
      <Field label={mode === 'report' ? 'Neden uygunsuz?' : 'Kaldırma gerekçesi'} htmlFor="reason">
        <textarea id="reason" className="form-field min-h-28" maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Field>
    </SlideOver>
  )
}

function ModerationQueue() {
  const qc = useQueryClient()
  const q = useQuery({ queryKey: ['social', 'reports'], queryFn: socialApi.reports })
  const resolve = useMutation({
    mutationFn: ({ id, remove }: { id: number; remove: boolean }) => socialApi.resolve(id, remove, null),
    onSuccess: (_, v) => { toast(v.remove ? 'Paylaşım kaldırıldı.' : 'Şikayet yersiz bulundu.'); qc.invalidateQueries({ queryKey: ['social'] }) },
    onError: (e) => toast(errorMessage(e), 'error'),
  })
  return (
    <section className="card mx-auto max-w-3xl overflow-hidden">
      {q.isLoading ? <div className="p-4"><BlockSkeleton className="h-32" /></div> : q.isError ? <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : q.data!.length === 0 ? (
        <EmptyState title="Bekleyen şikayet yok" description="Çalışanlar uygunsuz bir paylaşımı bildirdiğinde burada listelenir ve size bildirim gelir." />
      ) : (
        <ul className="divide-y divide-gray-100 dark:divide-gray-700/60">
          {q.data!.map((r) => (
            <li key={r.id} className="space-y-2 p-4">
              <div className="text-xs text-gray-500"><strong className="text-gray-700 dark:text-gray-300">{r.reporter}</strong> bildirdi · {timeAgo(r.createdAt)} · Paylaşan: {r.postAuthor}</div>
              <blockquote className="rounded-lg bg-gray-50 p-3 text-sm text-gray-700 dark:bg-gray-900/40 dark:text-gray-300">{r.postExcerpt || '(yalnızca görsel)'}</blockquote>
              <p className="text-sm"><span className="text-gray-500">Neden: </span>{r.reason}</p>
              <div className="flex flex-wrap gap-2">
                <Link to={`/social?post=${r.postId}`} className="btn-secondary"><MessageSquare className="size-4" aria-hidden="true" /> Paylaşımı aç</Link>
                <button className="btn-secondary" disabled={resolve.isPending} onClick={() => resolve.mutate({ id: r.id, remove: false })}>Yersiz, yayında kalsın</button>
                <button className="btn-danger" disabled={resolve.isPending} onClick={() => resolve.mutate({ id: r.id, remove: true })}>Paylaşımı kaldır</button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
