import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'

/** Bir bayrağı ancak `delay` ms sonra true yapar — 100 ms'den kısa yüklemelerde gösterge titremesin. */
export function useDelayedFlag(flag: boolean, delay = 100) {
  const [shown, setShown] = useState(false)
  useEffect(() => {
    if (!flag) {
      setShown(false)
      return
    }
    const t = setTimeout(() => setShown(true), delay)
    return () => clearTimeout(t)
  }, [flag, delay])
  return shown
}

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/**
 * Açılır yüzeyler (dialog, slide-over, menü) için:
 * - odağı içeride tutar (Tab / Shift+Tab döngüsü)
 * - Escape ile kapatır
 * - kapanınca odağı açan öğeye geri verir
 */
export function useFocusTrap<T extends HTMLElement>(open: boolean, onClose: () => void, initialFocus?: RefObject<HTMLElement | null>) {
  const ref = useRef<T>(null)
  const closeRef = useRef(onClose)
  closeRef.current = onClose

  useEffect(() => {
    if (!open) return
    const previous = document.activeElement as HTMLElement | null
    const node = ref.current
    // Önce ilk form alanına, yoksa ilk odaklanabilir öğeye odaklan.
    const first = () => node?.querySelector<HTMLElement>('input:not([type=hidden]):not([disabled]), textarea, select') ?? node?.querySelectorAll<HTMLElement>(FOCUSABLE)[0]
    ;(initialFocus?.current ?? first())?.focus()

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.stopPropagation()
        closeRef.current()
        return
      }
      if (e.key !== 'Tab' || !node) return
      const items = [...node.querySelectorAll<HTMLElement>(FOCUSABLE)]
      if (items.length === 0) return
      const firstEl = items[0]
      const lastEl = items[items.length - 1]
      if (e.shiftKey && document.activeElement === firstEl) {
        e.preventDefault()
        lastEl.focus()
      } else if (!e.shiftKey && document.activeElement === lastEl) {
        e.preventDefault()
        firstEl.focus()
      }
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      if (previous && document.contains(previous)) previous.focus()
    }
  }, [open, initialFocus])

  return ref
}

/** Dışarı tıklayınca veya Escape'e basınca kapanan açılır menü; kapanınca odak tetikleyiciye döner. */
export function usePopover() {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const close = useCallback((restoreFocus = true) => {
    setOpen(false)
    if (restoreFocus) triggerRef.current?.focus()
  }, [])

  useEffect(() => {
    if (!open) return
    const onClick = (e: MouseEvent) => rootRef.current && !rootRef.current.contains(e.target as Node) && setOpen(false)
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && close()
    document.addEventListener('mousedown', onClick)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onClick)
      document.removeEventListener('keydown', onKey)
    }
  }, [open, close])

  return { open, setOpen, close, rootRef, triggerRef }
}

/** "/" tuşu arama kutusuna odaklanır (yazı alanındayken tetiklenmez). */
export function useSlashFocus(ref: RefObject<HTMLInputElement | null>) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement
      if (e.key !== '/' || t.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(t.tagName)) return
      e.preventDefault()
      ref.current?.focus()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [ref])
}

type Rules<T> = { [K in keyof T]?: (value: T[K], all: T) => string | undefined }

/**
 * Basit form durumu:
 * - dokunulmamış alan yazarken asla hata göstermez
 * - ilk blur'da doğrular
 * - hatalı alana yazmaya başlayınca hata hemen temizlenir
 * - gönderimde tüm alanları doğrular ve ilk hatalı alana odaklanır
 */
export function useForm<T extends Record<string, unknown>>(initial: T, rules: Rules<T>) {
  const [values, setValues] = useState(initial)
  const [errors, setErrors] = useState<Partial<Record<keyof T, string>>>({})
  const [dirty, setDirty] = useState(false)

  const validateField = (name: keyof T, all: T) => rules[name]?.(all[name], all)

  const field = <K extends keyof T>(name: K) => ({
    id: `f-${String(name)}`,
    name: String(name),
    value: values[name] as never,
    'aria-invalid': errors[name] ? true : undefined,
    'aria-describedby': errors[name] ? `f-${String(name)}-error` : undefined,
    onChange: (e: { target: { value: string } }) => setValue(name, e.target.value as T[K]),
    onBlur: () => {
      const msg = validateField(name, values)
      setErrors((prev) => ({ ...prev, [name]: msg }))
    },
  })

  const setValue = <K extends keyof T>(name: K, value: T[K]) => {
    setValues((prev) => ({ ...prev, [name]: value }))
    setDirty(true)
    if (errors[name]) setErrors((prev) => ({ ...prev, [name]: undefined }))
  }

  const validateAll = () => {
    const next: Partial<Record<keyof T, string>> = {}
    for (const key of Object.keys(rules) as (keyof T)[]) {
      const msg = validateField(key, values)
      if (msg) next[key] = msg
    }
    setErrors(next)
    const firstKey = Object.keys(next)[0]
    if (firstKey) {
      const el = document.getElementById(`f-${firstKey}`)
      el?.scrollIntoView({ block: 'center' })
      el?.focus()
    }
    return Object.keys(next).length === 0
  }

  const setServerErrors = (server: Record<string, string>) => setErrors((prev) => ({ ...prev, ...server }))
  const reset = (v: T) => {
    setValues(v)
    setErrors({})
    setDirty(false)
  }

  return { values, errors, dirty, field, setValue, validateAll, setServerErrors, reset, setDirty }
}
