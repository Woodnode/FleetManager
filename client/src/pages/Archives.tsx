import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Archive, History, Search, ShieldAlert } from 'lucide-react'
import { vehiclesApi } from '../api/vehicles'
import Badge from '../components/ui/Badge'
import Modal from '../components/ui/Modal'
import { Skeleton, SkeletonTable } from '../components/ui/Skeleton'
import PageHeader from '../components/ui/PageHeader'
import Pagination from '../components/ui/Pagination'
import { useAuth } from '../contexts/AuthContext'
import { isManagerOrAdminRole } from '../utils/auth'
import type { ArchivedVehicle } from '../types'

// ── Helpers ────────────────────────────────────────────────────────────────────

function formatDate(value: string | null | undefined) {
  if (!value) return '-'
  return new Date(value).toLocaleDateString('fr-FR', { day: '2-digit', month: 'short', year: 'numeric' })
}

function archivedLabel(count: number) {
  const s = count > 1 ? 's' : ''
  return `${count} véhicule${s} archivé${s}`
}

// ── Empty state ────────────────────────────────────────────────────────────────

function EmptyState({ search, onReset }: { search: string; onReset: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-20 gap-2">
      <div
        className="w-14 h-14 rounded-2xl flex items-center justify-center mb-2"
        style={{ background: 'rgba(100,116,139,0.06)', border: '1.5px dashed #e2e8f0' }}
      >
        <Archive size={24} className="text-slate-300" />
      </div>
      <p className="text-sm font-semibold text-slate-500">
        {search ? 'Aucun résultat trouvé' : 'Aucun véhicule archivé'}
      </p>
      <p className="text-xs text-slate-400">
        {search
          ? 'Essayez un autre VIN, une autre marque ou un autre modèle'
          : 'Les véhicules supprimés apparaîtront ici, avec leur historique d’interventions'}
      </p>
      {search && (
        <button
          onClick={onReset}
          className="mt-1 text-xs font-medium transition-opacity hover:opacity-75"
          style={{ color: 'var(--brand-500)' }}
        >
          Effacer la recherche
        </button>
      )}
    </div>
  )
}

// ── History modal ──────────────────────────────────────────────────────────────

function HistoryModal({ vehicle, onClose }: { vehicle: ArchivedVehicle | null; onClose: () => void }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['archived-vehicle-history', vehicle?.id],
    queryFn: () => vehiclesApi.getArchivedHistory(vehicle!.id),
    enabled: !!vehicle,
    staleTime: 60_000,
  })

  return (
    <Modal
      open={!!vehicle}
      onClose={onClose}
      title={vehicle ? `Historique : ${vehicle.brand} ${vehicle.model}` : ''}
      size="lg"
    >
      {vehicle && (
        <dl className="grid grid-cols-2 sm:grid-cols-4 gap-4 pb-5 mb-5" style={{ borderBottom: '1px solid var(--border-light)' }}>
          {[
            ['VIN', <span key="vin" className="font-mono text-xs tracking-wide">{vehicle.vin}</span>],
            ['Kilométrage', `${vehicle.mileage.toLocaleString('fr-FR')} km`],
            ['Enseigne', vehicle.storeName],
            ['Supprimé le', formatDate(vehicle.deletedAt)],
          ].map(([term, value]) => (
            <div key={term as string}>
              <dt className="text-[11px] font-medium uppercase tracking-wide text-slate-400">{term}</dt>
              <dd className="text-sm text-slate-700 mt-1">{value}</dd>
            </div>
          ))}
        </dl>
      )}

      {isLoading && (
        <div className="space-y-3">
          {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-16 w-full rounded-xl" />)}
        </div>
      )}

      {isError && (
        <p className="text-sm text-red-500 py-6 text-center">Impossible de charger l’historique. Réessayez plus tard.</p>
      )}

      {data && data.interventions.length === 0 && (
        <p className="text-sm text-slate-400 py-8 text-center">Aucune intervention enregistrée pour ce véhicule.</p>
      )}

      {data && data.interventions.length > 0 && (
        <ol className="space-y-2.5 max-h-[55vh] overflow-y-auto pr-1" aria-label="Interventions du véhicule">
          {data.interventions.map(i => (
            <li key={i.id} className="rounded-xl p-4" style={{ border: '1px solid var(--border-light)' }}>
              <div className="flex items-center justify-between gap-3 flex-wrap">
                <div className="flex items-center gap-2">
                  <Badge value={i.type} label={i.typeLabel} />
                  <Badge value={i.status} label={i.statusLabel} />
                </div>
                <span className="text-xs text-slate-400 tabular-nums">
                  {formatDate(i.plannedStartDate)}
                  {formatDate(i.plannedEndDate) !== formatDate(i.plannedStartDate) && ` → ${formatDate(i.plannedEndDate)}`}
                </span>
              </div>
              <p className="text-xs text-slate-500 mt-2">
                {i.technicianFullName ? `Technicien : ${i.technicianFullName}` : 'Technicien non renseigné'}
              </p>
              {i.comment && <p className="text-sm text-slate-700 mt-1.5">{i.comment}</p>}
            </li>
          ))}
        </ol>
      )}
    </Modal>
  )
}

// ── Page ───────────────────────────────────────────────────────────────────────

export default function Archives() {
  const { user } = useAuth()
  const allowed = isManagerOrAdminRole(user?.role)

  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<ArchivedVehicle | null>(null)

  const { data: archivePage, isLoading, isError, refetch } = useQuery({
    queryKey: ['archived-vehicles', page, search],
    queryFn: () => vehiclesApi.getArchived(page, 20, search || undefined),
    enabled: allowed,
    staleTime: 30_000,
  })
  const vehicles = archivePage?.items ?? []
  const total = archivePage?.totalCount ?? 0

  if (!allowed) {
    return (
      <div className="p-8 fm-page">
        <PageHeader title="Archives" />
        <div className="fm-card flex flex-col items-center justify-center py-16 gap-2 text-center">
          <ShieldAlert size={24} className="text-slate-300" />
          <p className="text-sm font-semibold text-slate-500">Accès réservé aux administrateurs et aux gérants</p>
        </div>
      </div>
    )
  }

  return (
    <div className="p-8 fm-page">
      <PageHeader
        title="Archives"
        subtitle={`${archivedLabel(total)}. Historique conservé pour les rapports.`}
      />

      <div className="flex items-center gap-2 flex-wrap mb-5">
        <div className="flex-1 min-w-[8px]" />
        <div className="relative">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
          <input
            type="text"
            placeholder="VIN, marque, modèle..."
            aria-label="Rechercher dans les archives"
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1) }}
            className="fm-input pl-9"
            style={{ width: 210 }}
          />
        </div>
      </div>

      {isLoading ? (
        <SkeletonTable rows={6} cols={8} />
      ) : isError ? (
        <div className="fm-card flex flex-col items-center justify-center py-16 gap-3">
          <p className="text-sm text-slate-500">Impossible de charger les archives.</p>
          <button onClick={() => refetch()} className="fm-btn-primary">Réessayer</button>
        </div>
      ) : (
        <div className="fm-card overflow-hidden">
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-light)' }}>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">VIN</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Marque / Modèle</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Année</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Kilométrage</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Enseigne</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Supprimé le</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Interventions</th>
                <th scope="col" className="px-5 py-3.5 text-right fm-th"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {vehicles.length === 0 ? (
                <tr>
                  <td colSpan={8}>
                    <EmptyState search={search} onReset={() => { setSearch(''); setPage(1) }} />
                  </td>
                </tr>
              ) : vehicles.map(v => (
                <tr key={v.id} className="transition-colors hover:bg-slate-50/80"
                  style={{ borderBottom: '1px solid var(--border-light)' }}>
                  <td className="px-5 py-3.5 font-mono text-xs text-slate-500 tracking-wide">{v.vin}</td>
                  <td className="px-5 py-3.5 text-sm font-medium text-slate-900">{v.brand} {v.model}</td>
                  <td className="px-5 py-3.5 text-sm text-slate-500">{v.year}</td>
                  <td className="px-5 py-3.5 text-sm text-slate-500 tabular-nums">{v.mileage.toLocaleString('fr-FR')} km</td>
                  <td className="px-5 py-3.5 text-sm text-slate-500">{v.storeName}</td>
                  <td className="px-5 py-3.5 text-sm text-slate-500 tabular-nums">{formatDate(v.deletedAt)}</td>
                  <td className="px-5 py-3.5 text-sm text-slate-500 tabular-nums">{v.interventionCount}</td>
                  <td className="px-5 py-3.5 text-right">
                    <button
                      onClick={() => setSelected(v)}
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold text-slate-600 border border-slate-200 bg-white transition-colors hover:border-slate-400 hover:text-slate-900"
                      aria-label={`Voir l’historique de ${v.brand} ${v.model} (${v.vin})`}
                    >
                      <History size={13} />Historique
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {archivePage && archivePage.totalPages > 1 && (
            <Pagination
              page={archivePage.page}
              totalPages={archivePage.totalPages}
              totalCount={archivePage.totalCount}
              pageSize={archivePage.pageSize}
              onPageChange={p => setPage(p)}
            />
          )}
        </div>
      )}

      <HistoryModal vehicle={selected} onClose={() => setSelected(null)} />
    </div>
  )
}
