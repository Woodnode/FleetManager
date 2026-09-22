import { createPortal } from 'react-dom'
import { useEffect, useId, useRef, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { useScrollLock } from '../../hooks/useScrollLock'

interface ModalProps {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  size?: 'sm' | 'md' | 'lg'
}

const sizes = { sm: 'sm:max-w-md', md: 'sm:max-w-xl', lg: 'sm:max-w-2xl' }

export default function Modal({ open, onClose, title, children, size = 'md' }: ModalProps) {
  const titleId = useId()
  const panelRef = useRef<HTMLDivElement>(null)
  useScrollLock(open)

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open, onClose])

  // Le focus entre dans la modale (lecteurs d'écran, clavier) et revient ensuite au bouton d'origine.
  useEffect(() => {
    if (!open) return
    const previous = document.activeElement as HTMLElement | null
    panelRef.current?.focus()
    return () => previous?.focus?.()
  }, [open])

  if (!open) return null

  // Portail vers <body> : les pages ont une animation d'entrée (.fm-page) dont le transform
  // persiste et ferait de la page le repère des éléments "fixed" : la modale serait alors
  // positionnée et recouverte par rapport à la page, et non à la fenêtre.
  // Mobile : panneau ancré en bas (bottom sheet), plus facile à atteindre au pouce.
  return createPortal(
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center sm:p-4">
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onClose} />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={`fm-modal fm-sheet relative w-full ${sizes[size]} max-h-[92dvh] sm:max-h-[90dvh] overflow-y-auto overscroll-contain focus:outline-none`}
      >
        <div className="fm-modal-bar" />
        <div className="sticky top-0 z-10 bg-white flex items-center justify-between gap-3 pl-5 pr-3 sm:pl-6 py-3 border-b border-slate-100">
          <h2 id={titleId} className="text-sm font-semibold text-slate-900 tracking-tight min-w-0 truncate">{title}</h2>
          <button
            onClick={onClose}
            aria-label="Fermer"
            className="fm-icon-btn text-slate-400 hover:text-slate-600 hover:bg-slate-100"
          >
            <X size={16} />
          </button>
        </div>
        <div className="px-5 sm:px-6 py-5 pb-[calc(1.25rem+env(safe-area-inset-bottom))] sm:pb-5">{children}</div>
      </div>
    </div>,
    document.body,
  )
}
