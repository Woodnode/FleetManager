const dotColors: Record<string, string> = {
  Available:      '#10b981',
  InIntervention: '#3b82f6',
  InProgress:     '#3b82f6',
  Planned:        '#f59e0b',
  Completed:      '#10b981',
  Sold:           '#94a3b8',
  Cancelled:      '#94a3b8',
  OutOfService:   '#ef4444',
  Maintenance:    '#7c3aed',
  Repair:         '#ea580c',
  Inspection:     '#0d9488',
}

const variants: Record<string, string> = {
  Available:      'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200/80',
  InIntervention: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200/80',
  Sold:           'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
  OutOfService:   'bg-red-50 text-red-600 ring-1 ring-red-200/80',
  Planned:        'bg-amber-50 text-amber-700 ring-1 ring-amber-200/80',
  InProgress:     'bg-blue-50 text-blue-700 ring-1 ring-blue-200/80',
  Completed:      'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200/80',
  Cancelled:      'bg-slate-100 text-slate-400 ring-1 ring-slate-200',
  Maintenance:    'bg-violet-50 text-violet-700 ring-1 ring-violet-200/80',
  Repair:         'bg-orange-50 text-orange-700 ring-1 ring-orange-200/80',
  Inspection:     'bg-teal-50 text-teal-700 ring-1 ring-teal-200/80',
}

interface BadgeProps {
  value: string
  label?: string
}

export default function Badge({ value, label }: BadgeProps) {
  const cls = variants[value] ?? 'bg-slate-100 text-slate-500 ring-1 ring-slate-200'
  const dot = dotColors[value]
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-xs font-medium ${cls}`}>
      {dot && <span className="fm-dot" style={{ background: dot }} />}
      {label ?? value}
    </span>
  )
}
