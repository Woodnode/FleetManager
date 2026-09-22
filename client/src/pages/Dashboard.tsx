import { useQuery } from '@tanstack/react-query'
import { Car, Clock, CheckCircle2, TrendingUp, AlertTriangle } from 'lucide-react'
import {
  PieChart, Pie, Cell, ResponsiveContainer, Tooltip, Legend,
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
} from 'recharts'
import { dashboardApi } from '../api/dashboard'
import Badge from '../components/ui/Badge'
import { SkeletonKpi } from '../components/ui/Skeleton'
import { useIsMobile } from '../hooks/useMediaQuery'

// ── Colors / labels ────────────────────────────────────────────────────────────

const VEHICLE_COLORS: Record<string, string> = {
  Available:      '#10b981',
  InIntervention: '#4c6ef5',
  Sold:           '#94a3b8',
  OutOfService:   '#ef4444',
}

const VEHICLE_LABELS: Record<string, string> = {
  Available:      'Disponible',
  InIntervention: 'En intervention',
  Sold:           'Vendu',
  OutOfService:   'Hors service',
}

// ── Sparkline micro-chart ──────────────────────────────────────────────────────

function Sparkline({ data, color }: { data: number[]; color: string }) {
  if (data.length < 2) return null
  const min = Math.min(...data)
  const max = Math.max(...data)
  const range = max - min || 1
  const W = 56, H = 22
  const pts = data
    .map((v, i) => {
      const x = ((i / (data.length - 1)) * W).toFixed(1)
      const y = (H - ((v - min) / range) * (H - 4) - 2).toFixed(1)
      return `${x},${y}`
    })
    .join(' ')
  return (
    <svg
      width={W}
      height={H}
      viewBox={`0 0 ${W} ${H}`}
      className="overflow-visible opacity-60"
      aria-hidden
    >
      <polyline
        points={pts}
        fill="none"
        stroke={color}
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

/** Build a decorative 6-point sparkline that ends at `end` with a natural trend. */
function buildSparkline(end: number): number[] {
  if (end === 0) return [0, 0, 0, 0, 0, 0]
  const b = Math.max(1, Math.round(end * 0.6))
  return [b, Math.round(b * 1.12), Math.round(b * 0.94), Math.round(end * 0.80), Math.round(end * 0.91), end]
}

// ── KPI card ───────────────────────────────────────────────────────────────────

type KpiVariant = 'slate' | 'emerald' | 'amber' | 'blue'

interface KpiProps {
  title: string
  value: number | string
  sub?: string
  icon: React.ElementType
  variant: KpiVariant
  delta?: { label: string; positive: boolean }
  sparkline?: number[]
}

const kpiConfig: Record<KpiVariant, { topClass: string; iconBg: string; iconColor: string }> = {
  slate:   { topClass: 'fm-kpi-slate',   iconBg: 'rgba(100,116,139,0.10)', iconColor: '#64748b' },
  emerald: { topClass: 'fm-kpi-emerald', iconBg: 'rgba(16,185,129,0.10)',  iconColor: '#10b981' },
  amber:   { topClass: 'fm-kpi-amber',   iconBg: 'rgba(245,158,11,0.10)',  iconColor: '#f59e0b' },
  blue:    { topClass: 'fm-kpi-blue',    iconBg: 'rgba(76,110,245,0.10)',  iconColor: '#4c6ef5' },
}

function KpiCard({ title, value, sub, icon: Icon, variant, delta, sparkline }: KpiProps) {
  const { topClass, iconBg, iconColor } = kpiConfig[variant]
  return (
    <div className={`fm-card ${topClass} p-4 sm:p-5 min-w-0`}>
      {/* Top row: icon + delta badge */}
      <div className="flex items-start justify-between mb-3">
        <div className="p-2 sm:p-2.5 rounded-xl" style={{ background: iconBg }}>
          <Icon size={18} style={{ color: iconColor }} />
        </div>
        {delta && (
          <span
            className={`hidden sm:inline-block text-[11px] font-semibold px-2 py-0.5 rounded-full tracking-wide whitespace-nowrap ${
              delta.positive
                ? 'bg-emerald-50 text-emerald-700 border border-emerald-100'
                : 'bg-red-50 text-red-600 border border-red-100'
            }`}
          >
            {delta.positive ? '↑ ' : '↓ '}{delta.label}
          </span>
        )}
      </div>

      {/* Title + value */}
      <p className="fm-th mb-1.5">{title}</p>
      <p className="text-2xl sm:text-3xl font-bold text-slate-900 tabular-nums tracking-tight">{value}</p>

      {/* Sub + sparkline */}
      <div className="flex items-end justify-between mt-2">
        {sub && <p className="text-xs text-slate-500 min-w-0">{sub}</p>}
        {sparkline && <div className="hidden sm:block shrink-0"><Sparkline data={sparkline} color={iconColor} /></div>}
      </div>
    </div>
  )
}

// ── Dashboard ──────────────────────────────────────────────────────────────────

export default function Dashboard() {
  const isMobile = useIsMobile()
  const { data: summary, isLoading, isError } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: dashboardApi.getSummary,
    staleTime: 30_000,
  })

  // ── Loading state ────────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <div className="fm-page">
        {/* Header skeleton */}
        <div className="flex items-center justify-between mb-5 sm:mb-8">
          <div>
            <div className="h-6 w-28 bg-slate-100 rounded-md animate-pulse mb-2" />
            <div className="h-4 w-48 bg-slate-100 rounded-md animate-pulse" />
          </div>
        </div>
        {/* KPI skeletons */}
        <div className="grid grid-cols-2 xl:grid-cols-4 gap-3 sm:gap-4 mb-5 sm:mb-6">
          {[0, 1, 2, 3].map(i => <SkeletonKpi key={i} />)}
        </div>
        {/* Chart placeholders */}
        <div className="grid grid-cols-1 xl:grid-cols-2 gap-5 mb-5">
          {[0, 1].map(i => (
            <div key={i} className="fm-card p-6">
              <div className="h-4 w-36 bg-slate-100 rounded animate-pulse mb-2" />
              <div className="h-3 w-24 bg-slate-100 rounded animate-pulse mb-6" />
              <div className="h-44 bg-slate-50 rounded-lg animate-pulse" />
            </div>
          ))}
        </div>
      </div>
    )
  }

  // ── Error state ──────────────────────────────────────────────────────────────
  if (isError || !summary) {
    return (
      <div className="flex flex-col items-center justify-center h-full gap-3 text-slate-400">
        <AlertTriangle size={32} className="text-amber-400" />
        <p className="text-sm font-medium">Impossible de charger le tableau de bord.</p>
        <p className="text-xs">Vérifiez votre connexion ou réessayez dans quelques instants.</p>
      </div>
    )
  }

  const { vehicles, interventions, recentInterventions } = summary

  const availPct = vehicles.total
    ? Math.round((vehicles.available / vehicles.total) * 100)
    : 0

  const vehicleChartData = Object.entries(VEHICLE_LABELS)
    .map(([key, name]) => ({
      name,
      key,
      value: key === 'Available'      ? vehicles.available
           : key === 'InIntervention' ? vehicles.inIntervention
           : key === 'Sold'           ? vehicles.sold
           :                           vehicles.outOfService,
    }))
    .filter(d => d.value > 0)

  const typeChartData = [
    { name: 'Maintenance', value: interventions.maintenance, fill: '#7c3aed' },
    { name: 'Réparation',  value: interventions.repair,      fill: '#ea580c' },
    { name: 'Inspection',  value: interventions.inspection,  fill: '#0d9488' },
  ]

  return (
    <div className="fm-page">

      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3 mb-5 sm:mb-6 lg:mb-8">
        <div>
          <h1 className="text-xl font-bold text-slate-900 tracking-tight">Dashboard</h1>
          <p className="text-sm text-slate-500 mt-0.5">Vue d'ensemble du parc auto</p>
        </div>
        <div
          className="flex items-center gap-2 px-3 py-1.5 rounded-full"
          style={{ background: 'rgba(16,185,129,0.08)', border: '1px solid rgba(16,185,129,0.2)' }}
        >
          <div className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
          <span className="text-xs font-medium text-emerald-700">Données en direct</span>
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 xl:grid-cols-4 gap-3 sm:gap-4 mb-5 sm:mb-6">
        <KpiCard
          title="Total véhicules"
          value={vehicles.total}
          sub={`${vehicles.inIntervention} en intervention`}
          icon={Car}
          variant="slate"
          delta={
            vehicles.sold > 0
              ? { label: `${vehicles.sold} vendu${vehicles.sold > 1 ? 's' : ''}`, positive: false }
              : undefined
          }
          sparkline={buildSparkline(vehicles.total)}
        />
        <KpiCard
          title="Disponibles"
          value={vehicles.available}
          sub="Prêts à l'affectation"
          icon={CheckCircle2}
          variant="emerald"
          delta={{ label: `${availPct}% du parc`, positive: availPct >= 60 }}
          sparkline={buildSparkline(vehicles.available)}
        />
        <KpiCard
          title="Interventions prévues"
          value={interventions.planned}
          sub={`${interventions.inProgress} en cours actuellement`}
          icon={Clock}
          variant="amber"
          delta={
            interventions.inProgress > 0
              ? {
                  label: `${interventions.inProgress} active${interventions.inProgress > 1 ? 's' : ''}`,
                  positive: true,
                }
              : undefined
          }
          sparkline={buildSparkline(interventions.total)}
        />
        <KpiCard
          title="Taux de dispo."
          value={`${availPct}%`}
          sub="Véhicules disponibles"
          icon={TrendingUp}
          variant="blue"
          delta={{
            label:    availPct >= 70 ? 'Bon niveau' : availPct >= 50 ? 'Moyen' : 'Faible',
            positive: availPct >= 70,
          }}
          sparkline={buildSparkline(availPct).map(v => Math.min(100, v))}
        />
      </div>

      {/* Charts */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-5 mb-5">
        <div className="fm-card p-4 sm:p-6 min-w-0">
          <h3 className="text-sm font-semibold text-slate-900 tracking-tight">Répartition du parc</h3>
          <p className="text-xs text-slate-400 mt-0.5 mb-5">Statuts des {vehicles.total} véhicules</p>
          {vehicleChartData.length === 0 ? (
            <div className="flex items-center justify-center h-48 text-sm text-slate-400">Aucune donnée</div>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <PieChart>
                <Pie
                  data={vehicleChartData}
                  cx="50%" cy="50%"
                  innerRadius={isMobile ? 52 : 62} outerRadius={isMobile ? 78 : 92}
                  paddingAngle={3}
                  dataKey="value"
                >
                  {vehicleChartData.map(entry => (
                    <Cell key={entry.key} fill={VEHICLE_COLORS[entry.key]} />
                  ))}
                </Pie>
                <Tooltip formatter={(v, name) => [v, name]} />
                <Legend iconType="circle" iconSize={7} wrapperStyle={{ fontSize: '12px' }} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </div>

        <div className="fm-card p-4 sm:p-6 min-w-0">
          <h3 className="text-sm font-semibold text-slate-900 tracking-tight">Interventions par type</h3>
          <p className="text-xs text-slate-400 mt-0.5 mb-5">Total : {interventions.total} interventions</p>
          {typeChartData.every(d => d.value === 0) ? (
            <div className="flex items-center justify-center h-48 text-sm text-slate-400">Aucune donnée</div>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={typeChartData} barSize={isMobile ? 24 : 36} margin={{ left: -16, right: 4 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" vertical={false} />
                <XAxis dataKey="name" tick={{ fontSize: isMobile ? 11 : 12, fill: '#64748b' }} axisLine={false} tickLine={false} interval={0} />
                <YAxis tick={{ fontSize: 12, fill: '#94a3b8' }} axisLine={false} tickLine={false} allowDecimals={false} />
                <Tooltip
                  cursor={{ fill: '#f8fafc', radius: 6 }}
                  contentStyle={{ borderRadius: 8, border: '1px solid #e4e9f2', boxShadow: '0 4px 12px rgba(0,0,0,0.08)' }}
                />
                <Bar dataKey="value" name="Interventions" radius={[6, 6, 0, 0]}>
                  {typeChartData.map((entry, i) => (
                    <Cell key={i} fill={entry.fill} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>

      {/* Recent interventions */}
      <div className="fm-card overflow-hidden">
        <div className="px-4 sm:px-6 py-4" style={{ borderBottom: '1px solid var(--border-light)' }}>
          <h3 className="text-sm font-semibold text-slate-900 tracking-tight">Interventions récentes</h3>
          <p className="text-xs text-slate-400 mt-0.5">Les 6 dernières activités</p>
        </div>
        {isMobile ? (
          <ul className="divide-y divide-slate-100">
            {recentInterventions.length === 0 ? (
              <li className="px-4 py-10 text-center text-sm text-slate-400">Aucune intervention enregistrée</li>
            ) : recentInterventions.map(i => (
              <li key={i.id} className="px-4 py-3.5">
                <div className="flex items-start justify-between gap-3">
                  <p className="text-sm font-medium text-slate-900 min-w-0 truncate">{i.vehicleBrand} {i.vehicleModel}</p>
                  <span className="text-xs text-slate-400 tabular-nums shrink-0">{new Date(i.plannedStartDate).toLocaleDateString('fr-FR')}</span>
                </div>
                <div className="flex flex-wrap items-center gap-1.5 mt-2">
                  <Badge value={i.type} label={i.typeLabel} />
                  <Badge value={i.status} label={i.statusLabel} />
                </div>
                <p className="text-xs text-slate-500 mt-2 truncate">{i.technicianFullName ?? 'Non assigné'} · {i.storeName}</p>
              </li>
            ))}
          </ul>
        ) : (
        <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr style={{ background: '#fafbfd', borderBottom: '1px solid var(--border-light)' }}>
              <th className="px-6 py-3 text-left fm-th">Véhicule</th>
              <th className="px-6 py-3 text-left fm-th">Type</th>
              <th className="hidden xl:table-cell px-6 py-3 text-left fm-th">Technicien</th>
              <th className="hidden xl:table-cell px-6 py-3 text-left fm-th">Enseigne</th>
              <th className="px-6 py-3 text-left fm-th">Statut</th>
              <th className="px-6 py-3 text-left fm-th">Date prévue</th>
            </tr>
          </thead>
          <tbody>
            {recentInterventions.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-6 py-10 text-center text-sm text-slate-400">
                  Aucune intervention enregistrée
                </td>
              </tr>
            ) : recentInterventions.map(i => (
              <tr key={i.id} className="transition-colors hover:bg-slate-50/80"
                style={{ borderBottom: '1px solid var(--border-light)' }}>
                <td className="px-6 py-3.5">
                  <p className="text-sm font-medium text-slate-900">{i.vehicleBrand} {i.vehicleModel}</p>
                  <p className="text-xs text-slate-400 font-mono mt-0.5">{i.vehicleVin}</p>
                  <p className="xl:hidden text-xs text-slate-500 mt-0.5">{i.technicianFullName ?? 'Non assigné'} · {i.storeName}</p>
                </td>
                <td className="px-6 py-3.5"><Badge value={i.type} label={i.typeLabel} /></td>
                <td className="hidden xl:table-cell px-6 py-3.5 text-sm text-slate-500">{i.technicianFullName ?? 'Non assigné'}</td>
                <td className="hidden xl:table-cell px-6 py-3.5 text-sm text-slate-500">{i.storeName}</td>
                <td className="px-6 py-3.5"><Badge value={i.status} label={i.statusLabel} /></td>
                <td className="px-6 py-3.5 text-sm text-slate-500 whitespace-nowrap">
                  {new Date(i.plannedStartDate).toLocaleDateString('fr-FR')}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        </div>
        )}
      </div>
    </div>
  )
}
