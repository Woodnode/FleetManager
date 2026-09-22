import { useRealtimeStatus, type RealtimeStatus } from './realtime'

const LABELS: Record<RealtimeStatus, { text: string; dot: string; tone: string; pulse: boolean }> = {
  connected:    { text: 'En direct',        dot: 'bg-emerald-500', tone: 'text-emerald-700 bg-emerald-50 border-emerald-200', pulse: true },
  connecting:   { text: 'Connexion...',     dot: 'bg-slate-400',   tone: 'text-slate-600 bg-slate-50 border-slate-200',       pulse: false },
  reconnecting: { text: 'Reconnexion...',   dot: 'bg-amber-500',   tone: 'text-amber-700 bg-amber-50 border-amber-200',       pulse: true },
  offline:      { text: 'Hors ligne',       dot: 'bg-slate-400',   tone: 'text-slate-600 bg-slate-50 border-slate-200',       pulse: false },
}

/** Reflète l'état réel de la connexion temps réel, plutôt qu'un « en direct » décoratif. */
export default function RealtimeBadge() {
  const status = useRealtimeStatus()
  const { text, dot, tone, pulse } = LABELS[status]
  return (
    <div role="status" aria-live="polite"
      title={status === 'connected' ? 'Les modifications des autres utilisateurs s’affichent instantanément' : undefined}
      className={`inline-flex items-center gap-2 px-3 py-1.5 rounded-full border text-xs font-medium ${tone}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${dot} ${pulse ? 'animate-pulse' : ''}`} aria-hidden="true" />
      {text}
    </div>
  )
}
