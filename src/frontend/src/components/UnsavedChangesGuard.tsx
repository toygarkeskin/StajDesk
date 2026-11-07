import { useEffect } from 'react'
import { useBlocker } from 'react-router'
import { ConfirmDialog } from './ui'

/** Kaydedilmemiş değişiklik varken sayfadan ayrılmadan önce uyarır (uygulama içi gezinme + sekme kapatma). */
export function UnsavedChangesGuard({ when, title, description, confirmLabel }: { when: boolean; title: string; description: string; confirmLabel: string }) {
  const blocker = useBlocker(({ currentLocation, nextLocation }) => when && currentLocation.pathname !== nextLocation.pathname)

  useEffect(() => {
    if (!when) return
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault()
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [when])

  return (
    <ConfirmDialog
      open={blocker.state === 'blocked'}
      title={title}
      body={description}
      confirmLabel={confirmLabel}
      onConfirm={() => blocker.proceed?.()}
      onCancel={() => blocker.reset?.()}
    />
  )
}
