// ── Skeleton loading components ────────────────────────────────────────────────

export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse bg-slate-100 rounded-md ${className}`} />
}

// ── Table skeleton ─────────────────────────────────────────────────────────────

const COL_WIDTHS = ['w-28', 'w-36', 'w-20', 'w-16', 'w-20', 'w-12', 'w-16']

export function SkeletonTable({ rows = 5, cols = 5 }: { rows?: number; cols?: number }) {
  return (
    <div className="fm-card overflow-hidden">
      {/* header */}
      <div
        className="flex gap-8 px-5 py-3.5"
        style={{ background: '#fafbfd', borderBottom: '1px solid #f1f5f9' }}
      >
        {Array.from({ length: cols }).map((_, j) => (
          <Skeleton key={j} className={`h-2.5 ${COL_WIDTHS[j % COL_WIDTHS.length]}`} />
        ))}
      </div>

      {/* rows */}
      {Array.from({ length: rows }).map((_, i) => (
        <div
          key={i}
          className="flex gap-8 items-center px-5 py-4"
          style={{ borderBottom: i < rows - 1 ? '1px solid #f1f5f9' : 'none' }}
        >
          {Array.from({ length: cols }).map((_, j) => (
            <Skeleton
              key={j}
              className={`h-4 ${j === cols - 1 ? 'w-16 ml-auto' : COL_WIDTHS[(j + 1) % COL_WIDTHS.length]}`}
            />
          ))}
        </div>
      ))}
    </div>
  )
}

// ── KPI card skeleton ──────────────────────────────────────────────────────────

export function SkeletonKpi() {
  return (
    <div className="fm-card p-5">
      <div className="flex items-start justify-between mb-3">
        <Skeleton className="h-9 w-9 rounded-xl" />
        <Skeleton className="h-5 w-20 rounded-full" />
      </div>
      <Skeleton className="h-2.5 w-20 mb-2.5" />
      <Skeleton className="h-8 w-14 mb-1.5" />
      <div className="flex items-end justify-between">
        <Skeleton className="h-2.5 w-32" />
        <Skeleton className="h-5 w-14" />
      </div>
    </div>
  )
}

// ── Generic card skeleton ──────────────────────────────────────────────────────

export function SkeletonCard() {
  return (
    <div className="fm-card p-5 space-y-3">
      <div className="flex items-start gap-3">
        <Skeleton className="h-9 w-9 rounded-lg shrink-0" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-3 w-1/2" />
        </div>
      </div>
      <Skeleton className="h-3 w-full" />
      <Skeleton className="h-3 w-4/5" />
      <div className="pt-1">
        <div className="flex justify-between mb-1">
          <Skeleton className="h-2.5 w-20" />
          <Skeleton className="h-2.5 w-12" />
        </div>
        <Skeleton className="h-1.5 w-full rounded-full" />
      </div>
    </div>
  )
}
