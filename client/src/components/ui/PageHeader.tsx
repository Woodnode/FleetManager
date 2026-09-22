import type { ReactNode } from 'react'

interface PageHeaderProps {
  title: string
  subtitle?: string
  action?: ReactNode
}

// Mobile : titre puis action pleine largeur ; à partir de sm, action alignée à droite.
export default function PageHeader({ title, subtitle, action }: PageHeaderProps) {
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between mb-5 sm:mb-6 lg:mb-8">
      <div className="min-w-0">
        <h1 className="text-xl font-bold text-slate-900 tracking-tight">{title}</h1>
        {subtitle && <p className="text-sm text-slate-500 mt-0.5">{subtitle}</p>}
      </div>
      {action && <div className="shrink-0 [&>button]:w-full sm:[&>button]:w-auto [&>button]:justify-center">{action}</div>}
    </div>
  )
}
