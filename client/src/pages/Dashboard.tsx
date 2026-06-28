import { useQuery } from '@tanstack/react-query'
import { Car, Clock, CheckCircle2, TrendingUp, AlertTriangle } from 'lucide-react'
import {
  PieChart, Pie, Cell, ResponsiveContainer, Tooltip, Legend,
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
} from 'recharts'
import { dashboardApi } from '../api/dashboard'
import Badge from '../components/ui/Badge'
import Spinner from '../components/ui/Spinner'

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

type KpiVariant = 'slate' | 'emerald' | 'amber' | 'blue'

interface KpiProps {
  title: string
  value: number | string
  sub?: string
  icon: React.ElementType
  variant: KpiVariant
}

const kpiConfig: Record<KpiVariant, { topClass: string; iconBg: string; iconColor: string }> = {
  slate:   { topClass: 'fm-kpi-slate',   iconBg: 'rgba(100,116,139,0.10)', iconColor: '#64748b' },
  emerald: { topClass: 'fm-kpi-emerald', iconBg: 'rgba(16,185,129,0.10)',  iconColor: '#10b981' },
  amber:   { topClass: 'fm-kpi-amber',   iconBg: 'rgba(245,158,11,0.10)',  iconColor: '#f59e0b' },
  blue:    { topClass: 'fm-kpi-blue',    iconBg: 'rgba(76,110,245,0.10)',  iconColor: '#4c6ef5' },
}

function KpiCard({ title, value, sub, icon: Icon, variant }: KpiProps) {
  const { topClass, iconBg, iconColor } = kpiConfig[variant]
  return (
    <div className={`fm-card ${topClass} p-5`}>
      <div className="flex items-start justify-between">
        <div>
          <p className="fm-th mb-2.5">{title}</p>
          <p className="text-3xl font-bold text-slate-900 tabular-nums tracking-tight">{value}</p>
          {sub && <p className="text-xs text-slate-400 mt-1.5">{sub}</p>}
        </div>
        <div className="p-2.5 rounded-xl" style={{ background: iconBg }}>
          <Icon size={18} style={{ color: iconColor }} />
        </div>
      </div>
    </div>
  )
}

export default function Dashboard() {
  const { data: summary, isLoading, isError } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: dashboardApi.getSummary,
    staleTime: 30_000,
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-full">
        <Spinner className="w-8 h-8" />
      </div>
    )
  }

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
    <div className="p-8 fm-page">

      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-xl font-bold text-slate-900 tracking-tight">Dashboard</h1>
          <p className="text-sm text-slate-400 mt-0.5">Vue d'ensemble du parc auto</p>
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
      <div className="grid grid-cols-2 xl:grid-cols-4 gap-4 mb-6">
        <KpiCard
          title="Total véhicules"
          value={vehicles.total}
          sub={`${vehicles.inIntervention} en intervention`}
          icon={Car}
          variant="slate"
        />
        <KpiCard
          title="Disponibles"
          value={vehicles.available}
          sub="Prêts à l'affectation"
          icon={CheckCircle2}
          variant="emerald"
        />
        <KpiCard
          title="Interventions prévues"
          value={interventions.planned}
          sub={`${interventions.inProgress} en cours actuellement`}
          icon={Clock}
          variant="amber"
        />
        <KpiCard
          title="Taux de dispo."
          value={`${availPct}%`}
          sub="Véhicules disponibles"
          icon={TrendingUp}
          variant="blue"
        />
      </div>

      {/* Charts */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-5 mb-5">
        <div className="fm-card p-6">
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
                  innerRadius={62} outerRadius={92}
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

        <div className="fm-card p-6">
          <h3 className="text-sm font-semibold text-slate-900 tracking-tight">Interventions par type</h3>
          <p className="text-xs text-slate-400 mt-0.5 mb-5">Total : {interventions.total} interventions</p>
          {typeChartData.every(d => d.value === 0) ? (
            <div className="flex items-center justify-center h-48 text-sm text-slate-400">Aucune donnée</div>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={typeChartData} barSize={36}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" vertical={false} />
                <XAxis dataKey="name" tick={{ fontSize: 12, fill: '#94a3b8' }} axisLine={false} tickLine={false} />
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
        <div className="px-6 py-4" style={{ borderBottom: '1px solid var(--border-light)' }}>
          <h3 className="text-sm font-semibold text-slate-900 tracking-tight">Interventions récentes</h3>
          <p className="text-xs text-slate-400 mt-0.5">Les 6 dernières activités</p>
        </div>
        <table className="w-full">
          <thead>
            <tr style={{ background: '#fafbfd', borderBottom: '1px solid var(--border-light)' }}>
              <th className="px-6 py-3 text-left fm-th">Véhicule</th>
              <th className="px-6 py-3 text-left fm-th">Type</th>
              <th className="px-6 py-3 text-left fm-th">Technicien</th>
              <th className="px-6 py-3 text-left fm-th">Enseigne</th>
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
                </td>
                <td className="px-6 py-3.5"><Badge value={i.type} label={i.typeLabel} /></td>
                <td className="px-6 py-3.5 text-sm text-slate-500">{i.technicianFullName ?? '—'}</td>
                <td className="px-6 py-3.5 text-sm text-slate-500">{i.storeName}</td>
                <td className="px-6 py-3.5"><Badge value={i.status} label={i.statusLabel} /></td>
                <td className="px-6 py-3.5 text-sm text-slate-500">
                  {new Date(i.plannedStartDate).toLocaleDateString('fr-FR')}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
