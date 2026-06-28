import { ChevronLeft, ChevronRight } from 'lucide-react'

interface PaginationProps {
  page: number
  totalPages: number
  totalCount: number
  pageSize: number
  onPageChange: (page: number) => void
}

export default function Pagination({ page, totalPages, totalCount, pageSize, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null

  const start = (page - 1) * pageSize + 1
  const end   = Math.min(page * pageSize, totalCount)

  return (
    <div className="flex items-center justify-between px-5 py-3 text-xs text-slate-400"
      style={{ borderTop: '1px solid var(--border-light)' }}>
      <span>{start}–{end} sur {totalCount}</span>

      <div className="flex items-center gap-1">
        <button
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
          className="p-1.5 rounded-md disabled:opacity-30 hover:bg-slate-100 transition-colors"
          aria-label="Page précédente"
        >
          <ChevronLeft size={14} aria-hidden="true" />
        </button>

        <span className="px-2 font-medium text-slate-600" aria-live="polite">
          {page} / {totalPages}
        </span>

        <button
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
          className="p-1.5 rounded-md disabled:opacity-30 hover:bg-slate-100 transition-colors"
          aria-label="Page suivante"
        >
          <ChevronRight size={14} aria-hidden="true" />
        </button>
      </div>
    </div>
  )
}
