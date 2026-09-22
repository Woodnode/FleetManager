import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil, Trash2, RefreshCw, Search, LayoutList, LayoutGrid, Car } from 'lucide-react'
import toast from 'react-hot-toast'
import { vehiclesApi } from '../api/vehicles'
import { storesApi } from '../api/stores'
import Badge from '../components/ui/Badge'
import Modal from '../components/ui/Modal'
import { SkeletonTable } from '../components/ui/Skeleton'
import PageHeader from '../components/ui/PageHeader'
import Pagination from '../components/ui/Pagination'
import { useAuth } from '../contexts/AuthContext'
import { getApiErrorMessage } from '../utils/apiError'
import { createVehicleSchema, updateVehicleSchema, type CreateVehicleFormValues, type UpdateVehicleFormValues } from '../schemas/vehicle'
import type { Vehicle, VehicleStatus, CreateVehicleRequest, UpdateVehicleRequest, Store } from '../types'

// ── Constants ──────────────────────────────────────────────────────────────────

const STATUS_OPTIONS: { value: VehicleStatus; label: string }[] = [
  { value: 'Available',      label: 'Disponible' },
  { value: 'InIntervention', label: 'En intervention' },
  { value: 'Sold',           label: 'Vendu' },
  { value: 'OutOfService',   label: 'Hors service' },
]

const STATUS_PILLS = [
  { value: '',               label: 'Tous' },
  { value: 'Available',      label: 'Disponibles' },
  { value: 'InIntervention', label: 'En intervention' },
  { value: 'Sold',           label: 'Vendus' },
  { value: 'OutOfService',   label: 'Hors service' },
]

const STATUS_DOT: Record<VehicleStatus, string> = {
  Available:      '#10b981',
  InIntervention: '#4c6ef5',
  Sold:           '#94a3b8',
  OutOfService:   '#ef4444',
}

const inputCls = 'fm-input'

// ── Small helpers ──────────────────────────────────────────────────────────────

function FieldError({ msg }: { msg?: string }) {
  if (!msg) return null
  return <p className="text-xs text-red-500 mt-1">{msg}</p>
}

function label(text: string, htmlFor: string, required = false) {
  return (
    <label htmlFor={htmlFor} className="block text-sm font-medium text-slate-700 mb-1.5">
      {text}{required && <span className="text-red-400 ml-0.5">*</span>}
    </label>
  )
}

// ── Empty state ────────────────────────────────────────────────────────────────

function EmptyState({ search, statusFilter, onReset }: {
  search: string
  statusFilter: string
  onReset: () => void
}) {
  const filtered = !!(search || statusFilter)
  return (
    <div className="flex flex-col items-center justify-center py-20 gap-2">
      <div
        className="w-14 h-14 rounded-2xl flex items-center justify-center mb-2"
        style={{ background: 'rgba(100,116,139,0.06)', border: '1.5px dashed #e2e8f0' }}
      >
        <Car size={24} className="text-slate-300" />
      </div>
      <p className="text-sm font-semibold text-slate-500">
        {filtered ? 'Aucun résultat trouvé' : 'Aucun véhicule enregistré'}
      </p>
      <p className="text-xs text-slate-400">
        {filtered
          ? 'Essayez de modifier vos filtres'
          : 'Ajoutez votre premier véhicule avec le bouton ci-dessus'}
      </p>
      {filtered && (
        <button
          onClick={onReset}
          className="mt-1 text-xs font-medium transition-opacity hover:opacity-75"
          style={{ color: 'var(--brand-500)' }}
        >
          Réinitialiser les filtres
        </button>
      )}
    </div>
  )
}

// ── Vehicle card (grid view) ───────────────────────────────────────────────────

function VehicleCard({ vehicle: v, canDelete, onEdit, onStatus, onDelete }: {
  vehicle: Vehicle
  canDelete: boolean
  onEdit: () => void
  onStatus: () => void
  onDelete: () => void
}) {
  return (
    <div className="fm-card p-5 group">
      <div className="flex items-start justify-between mb-3">
        <div className="min-w-0 flex-1">
          <p className="font-semibold text-slate-900 text-sm truncate">{v.brand} {v.model}</p>
          <p className="text-[11px] font-mono text-slate-400 mt-0.5 tracking-wide">{v.vin}</p>
        </div>
        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0 ml-2">
          <ActionBtn onClick={onStatus} icon={<RefreshCw size={12} />} title="Changer le statut" color="blue" />
          <ActionBtn onClick={onEdit} icon={<Pencil size={12} />} title="Modifier" color="slate" />
          {canDelete && <ActionBtn onClick={onDelete} icon={<Trash2 size={12} />} title="Supprimer" color="red" />}
        </div>
      </div>

      <div className="flex items-center gap-2 mb-3">
        <div className="w-2 h-2 rounded-full shrink-0" style={{ background: STATUS_DOT[v.status] }} />
        <Badge value={v.status} label={v.statusLabel} />
      </div>

      <div
        className="flex items-center justify-between text-xs text-slate-400 pt-3"
        style={{ borderTop: '1px solid var(--border-light)' }}
      >
        <span className="font-medium">{v.year}</span>
        <span className="tabular-nums">{v.mileage.toLocaleString('fr-FR')} km</span>
        <span className="truncate max-w-[96px] ml-2">{v.storeName}</span>
      </div>
    </div>
  )
}

// ── Create / Edit forms ────────────────────────────────────────────────────────

interface CreateVehicleFormProps {
  stores: Store[]
  onSubmit: (d: CreateVehicleRequest) => void
  pending: boolean
  onCancel: () => void
}

function CreateVehicleForm({ stores, onSubmit, pending, onCancel }: CreateVehicleFormProps) {
  const { register, handleSubmit, formState: { errors } } = useForm<CreateVehicleFormValues>({
    resolver: zodResolver(createVehicleSchema),
    defaultValues: { vin: '', brand: '', model: '', year: new Date().getFullYear(), mileage: 0, storeId: '' },
  })
  const submit = (data: CreateVehicleFormValues) =>
    onSubmit({ ...data, vin: data.vin.toUpperCase() } as CreateVehicleRequest)

  return (
    <form onSubmit={handleSubmit(submit)} className="space-y-4">
      <div>
        {label('VIN', 'cv-vin', true)}
        <input id="cv-vin" {...register('vin')} maxLength={17}
          className={`${inputCls} font-mono uppercase tracking-widest`}
          placeholder="Ex: VF1RFD00X67891234" />
        <FieldError msg={errors.vin?.message} />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          {label('Marque', 'cv-brand', true)}
          <input id="cv-brand" {...register('brand')} className={inputCls} placeholder="Renault" />
          <FieldError msg={errors.brand?.message} />
        </div>
        <div>
          {label('Modèle', 'cv-model', true)}
          <input id="cv-model" {...register('model')} className={inputCls} placeholder="Clio" />
          <FieldError msg={errors.model?.message} />
        </div>
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          {label('Année', 'cv-year')}
          <input id="cv-year" type="number" {...register('year', { valueAsNumber: true })} min={1990} max={2030} className={inputCls} />
          <FieldError msg={errors.year?.message} />
        </div>
        <div>
          {label('Kilométrage', 'cv-mileage')}
          <div className="relative">
            <input id="cv-mileage" type="number" {...register('mileage', { valueAsNumber: true })} min={0} className={inputCls} />
            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-slate-400">km</span>
          </div>
          <FieldError msg={errors.mileage?.message} />
        </div>
      </div>
      <div>
        {label('Enseigne', 'cv-storeId', true)}
        <select id="cv-storeId" {...register('storeId')} className={inputCls}>
          <option value="">Sélectionner une enseigne</option>
          {stores.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
        </select>
        <FieldError msg={errors.storeId?.message} />
      </div>
      <div className="flex justify-end gap-3 mt-6 pt-4" style={{ borderTop: '1px solid var(--border-light)' }}>
        <button type="button" onClick={onCancel}
          className="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg transition-colors font-medium">
          Annuler
        </button>
        <button type="submit" disabled={pending} className="fm-btn-primary">
          {pending ? 'Enregistrement...' : 'Ajouter'}
        </button>
      </div>
    </form>
  )
}

interface EditVehicleFormProps {
  vehicle: Vehicle
  stores: Store[]
  onSubmit: (d: UpdateVehicleRequest) => void
  pending: boolean
  onCancel: () => void
}

function EditVehicleForm({ vehicle, stores, onSubmit, pending, onCancel }: EditVehicleFormProps) {
  const { register, handleSubmit, formState: { errors } } = useForm<UpdateVehicleFormValues>({
    resolver: zodResolver(updateVehicleSchema),
    defaultValues: { brand: vehicle.brand, model: vehicle.model, year: vehicle.year, mileage: vehicle.mileage, storeId: vehicle.storeId },
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <div>
          {label('Marque', 'ev-brand', true)}
          <input id="ev-brand" {...register('brand')} className={inputCls} placeholder="Renault" />
          <FieldError msg={errors.brand?.message} />
        </div>
        <div>
          {label('Modèle', 'ev-model', true)}
          <input id="ev-model" {...register('model')} className={inputCls} placeholder="Clio" />
          <FieldError msg={errors.model?.message} />
        </div>
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          {label('Année', 'ev-year')}
          <input id="ev-year" type="number" {...register('year', { valueAsNumber: true })} min={1990} max={2030} className={inputCls} />
        </div>
        <div>
          {label('Kilométrage', 'ev-mileage')}
          <div className="relative">
            <input id="ev-mileage" type="number" {...register('mileage', { valueAsNumber: true })} min={0} className={inputCls} />
            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-slate-400">km</span>
          </div>
        </div>
      </div>
      <div>
        {label('Enseigne', 'ev-storeId')}
        <select id="ev-storeId" {...register('storeId')} className={inputCls}>
          {stores.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
        </select>
      </div>
      <div className="flex justify-end gap-3 mt-6 pt-4" style={{ borderTop: '1px solid var(--border-light)' }}>
        <button type="button" onClick={onCancel}
          className="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg transition-colors font-medium">
          Annuler
        </button>
        <button type="submit" disabled={pending} className="fm-btn-primary">
          {pending ? 'Enregistrement...' : 'Enregistrer'}
        </button>
      </div>
    </form>
  )
}

// ── Main component ─────────────────────────────────────────────────────────────

export default function Vehicles() {
  const qc = useQueryClient()
  const { user } = useAuth()
  const canDelete = user?.role === 'Admin' || user?.role === 'StoreManager'

  const [search, setSearch]             = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage]                 = useState(1)
  const [viewMode, setViewMode]         = useState<'table' | 'grid'>('table')
  const [addOpen, setAddOpen]           = useState(false)
  const [editVehicle, setEditVehicle]   = useState<Vehicle | null>(null)
  const [statusVehicle, setStatusVehicle] = useState<Vehicle | null>(null)
  const [deleteVehicle, setDeleteVehicle] = useState<Vehicle | null>(null)
  const [newStatus, setNewStatus]       = useState<VehicleStatus>('Available')

  const { data: vehiclesPage, isLoading } = useQuery({
    queryKey: ['vehicles', page, search, statusFilter],
    queryFn: () => vehiclesApi.getAll(page, 20, search || undefined, statusFilter || undefined),
    staleTime: 30_000,
  })
  const vehicles = vehiclesPage?.items ?? []

  const { data: stores = [] } = useQuery({ queryKey: ['stores'], queryFn: storesApi.getAll, staleTime: 60_000 })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['vehicles'] })

  const createM = useMutation({
    mutationFn: (d: CreateVehicleRequest) => vehiclesApi.create(d),
    onSuccess: () => { invalidate(); setAddOpen(false); toast.success('Véhicule ajouté') },
    onError:   () => toast.error("Erreur lors de l'ajout"),
  })

  const updateM = useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateVehicleRequest }) => vehiclesApi.update(id, data),
    onSuccess: () => { invalidate(); setEditVehicle(null); toast.success('Véhicule modifié') },
    onError:   () => toast.error('Erreur lors de la modification'),
  })

  const statusM = useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) => vehiclesApi.changeStatus(id, status),
    onSuccess: () => { invalidate(); setStatusVehicle(null); toast.success('Statut mis à jour') },
    onError:   () => toast.error('Changement de statut refusé'),
  })

  const deleteM = useMutation({
    mutationFn: (id: string) => vehiclesApi.delete(id),
    onSuccess: () => {
      invalidate()
      qc.invalidateQueries({ queryKey: ['archived-vehicles'] })
      setDeleteVehicle(null)
      toast.success('Véhicule supprimé et placé dans les archives')
    },
    // L'API explique le refus (ex. véhicule en intervention) : on affiche son message.
    onError:   (err) => toast.error(getApiErrorMessage(err, 'Erreur lors de la suppression')),
  })

  const handleSearchChange = (value: string) => { setSearch(value); setPage(1) }
  const handleStatusChange = (value: string) => { setStatusFilter(value); setPage(1) }
  const resetFilters = () => { handleSearchChange(''); handleStatusChange('') }

  return (
    <div className="p-8 fm-page">
      <PageHeader
        title="Véhicules"
        subtitle={`${vehiclesPage?.totalCount ?? 0} véhicule${(vehiclesPage?.totalCount ?? 0) !== 1 ? 's' : ''} dans le parc`}
        action={
          <button onClick={() => setAddOpen(true)} className="fm-btn-primary">
            <Plus size={15} />Ajouter un véhicule
          </button>
        }
      />

      {/* Toolbar: pills + search + view toggle */}
      <div className="flex items-center gap-2 flex-wrap mb-5">
        {/* Status pills */}
        {STATUS_PILLS.map(pill => (
          <button
            key={pill.value}
            onClick={() => handleStatusChange(pill.value)}
            className={`px-3.5 py-1.5 rounded-full text-xs font-semibold whitespace-nowrap transition-all ${
              statusFilter === pill.value
                ? 'bg-slate-900 text-white shadow-sm'
                : 'bg-white text-slate-500 border border-slate-200 hover:border-slate-400 hover:text-slate-700'
            }`}
          >
            {pill.label}
          </button>
        ))}

        <div className="flex-1 min-w-[8px]" />

        {/* Search */}
        <div className="relative">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
          <input
            type="text"
            placeholder="VIN, marque, modèle..."
            value={search}
            onChange={e => handleSearchChange(e.target.value)}
            className="fm-input pl-9"
            style={{ width: 210 }}
          />
        </div>

        {/* View toggle */}
        <div className="flex gap-0.5 p-1 bg-slate-100 rounded-lg">
          <button
            onClick={() => setViewMode('table')}
            className={`p-1.5 rounded-md transition-all ${viewMode === 'table' ? 'bg-white shadow-sm text-slate-800' : 'text-slate-400 hover:text-slate-600'}`}
            aria-label="Vue tableau"
          >
            <LayoutList size={14} />
          </button>
          <button
            onClick={() => setViewMode('grid')}
            className={`p-1.5 rounded-md transition-all ${viewMode === 'grid' ? 'bg-white shadow-sm text-slate-800' : 'text-slate-400 hover:text-slate-600'}`}
            aria-label="Vue grille"
          >
            <LayoutGrid size={14} />
          </button>
        </div>
      </div>

      {/* ── Table view ── */}
      {viewMode === 'table' && (
        isLoading ? (
          <SkeletonTable rows={6} cols={7} />
        ) : (
          <div className="fm-card overflow-hidden">
            <table className="w-full">
              <caption className="sr-only">Liste des véhicules du parc</caption>
              <thead>
                <tr style={{ background: '#fafbfd', borderBottom: '1px solid var(--border-light)' }}>
                  <th scope="col" className="px-5 py-3.5 text-left fm-th">VIN</th>
                  <th scope="col" className="px-5 py-3.5 text-left fm-th">Marque / Modèle</th>
                  <th scope="col" className="px-5 py-3.5 text-left fm-th">Année</th>
                  <th scope="col" className="px-5 py-3.5 text-left fm-th">Kilométrage</th>
                  <th scope="col" className="px-5 py-3.5 text-left fm-th">Statut</th>
                  <th scope="col" className="px-5 py-3.5 text-left fm-th">Enseigne</th>
                  <th scope="col" className="px-5 py-3.5 text-right fm-th">Actions</th>
                </tr>
              </thead>
              <tbody>
                {vehicles.length === 0 ? (
                  <tr>
                    <td colSpan={7}>
                      <EmptyState search={search} statusFilter={statusFilter} onReset={resetFilters} />
                    </td>
                  </tr>
                ) : vehicles.map(v => (
                  <tr key={v.id} className="transition-colors hover:bg-slate-50/80 group"
                    style={{ borderBottom: '1px solid var(--border-light)' }}>
                    <td className="px-5 py-3.5 font-mono text-xs text-slate-500 tracking-wide">{v.vin}</td>
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <div className="w-1.5 h-1.5 rounded-full shrink-0" style={{ background: STATUS_DOT[v.status] }} />
                        <span className="text-sm font-medium text-slate-900">{v.brand} {v.model}</span>
                      </div>
                    </td>
                    <td className="px-5 py-3.5 text-sm text-slate-500">{v.year}</td>
                    <td className="px-5 py-3.5 text-sm text-slate-500 tabular-nums">
                      {v.mileage.toLocaleString('fr-FR')} km
                    </td>
                    <td className="px-5 py-3.5"><Badge value={v.status} label={v.statusLabel} /></td>
                    <td className="px-5 py-3.5 text-sm text-slate-500">{v.storeName}</td>
                    <td className="px-5 py-3.5">
                      <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                        <ActionBtn onClick={() => { setNewStatus(v.status); setStatusVehicle(v) }} icon={<RefreshCw size={13} />} title="Changer le statut" color="blue" />
                        <ActionBtn onClick={() => setEditVehicle(v)} icon={<Pencil size={13} />} title="Modifier" color="slate" />
                        {canDelete && <ActionBtn onClick={() => setDeleteVehicle(v)} icon={<Trash2 size={13} />} title="Supprimer" color="red" />}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!isLoading && vehiclesPage && vehiclesPage.totalPages > 1 && (
              <Pagination
                page={vehiclesPage.page}
                totalPages={vehiclesPage.totalPages}
                totalCount={vehiclesPage.totalCount}
                pageSize={vehiclesPage.pageSize}
                onPageChange={p => setPage(p)}
              />
            )}
          </div>
        )
      )}

      {/* ── Grid view ── */}
      {viewMode === 'grid' && (
        isLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="fm-card p-5 space-y-3 animate-pulse">
                <div className="h-4 w-3/4 bg-slate-100 rounded" />
                <div className="h-3 w-1/2 bg-slate-100 rounded" />
                <div className="h-5 w-20 bg-slate-100 rounded-full" />
                <div className="pt-2 flex justify-between">
                  <div className="h-3 w-10 bg-slate-100 rounded" />
                  <div className="h-3 w-20 bg-slate-100 rounded" />
                  <div className="h-3 w-16 bg-slate-100 rounded" />
                </div>
              </div>
            ))}
          </div>
        ) : vehicles.length === 0 ? (
          <div className="fm-card">
            <EmptyState search={search} statusFilter={statusFilter} onReset={resetFilters} />
          </div>
        ) : (
          <>
            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
              {vehicles.map(v => (
                <VehicleCard
                  key={v.id}
                  vehicle={v}
                  canDelete={canDelete}
                  onEdit={() => setEditVehicle(v)}
                  onStatus={() => { setNewStatus(v.status); setStatusVehicle(v) }}
                  onDelete={() => setDeleteVehicle(v)}
                />
              ))}
            </div>
            {vehiclesPage && vehiclesPage.totalPages > 1 && (
              <div className="mt-4">
                <Pagination
                  page={vehiclesPage.page}
                  totalPages={vehiclesPage.totalPages}
                  totalCount={vehiclesPage.totalCount}
                  pageSize={vehiclesPage.pageSize}
                  onPageChange={p => setPage(p)}
                />
              </div>
            )}
          </>
        )
      )}

      {/* ── Modals ── */}

      <Modal open={addOpen} onClose={() => setAddOpen(false)} title="Ajouter un véhicule" size="md">
        <CreateVehicleForm
          key={addOpen ? 'open' : 'closed'}
          stores={stores}
          onSubmit={d => createM.mutate(d)}
          pending={createM.isPending}
          onCancel={() => setAddOpen(false)}
        />
      </Modal>

      {editVehicle && (
        <Modal open onClose={() => setEditVehicle(null)} title={`Modifier — ${editVehicle.brand} ${editVehicle.model}`} size="md">
          <EditVehicleForm
            key={editVehicle.id}
            vehicle={editVehicle}
            stores={stores}
            onSubmit={d => updateM.mutate({ id: editVehicle.id, data: d })}
            pending={updateM.isPending}
            onCancel={() => setEditVehicle(null)}
          />
        </Modal>
      )}

      <Modal open={!!statusVehicle} onClose={() => setStatusVehicle(null)} title="Changer le statut" size="sm">
        <div className="space-y-4">
          <p className="text-sm text-slate-500">
            Véhicule : <span className="font-medium text-slate-900">{statusVehicle?.brand} {statusVehicle?.model}</span>
            <span className="font-mono text-xs text-slate-400 ml-2">{statusVehicle?.vin}</span>
          </p>
          <div>
            <label htmlFor="vs-newStatus" className="block text-sm font-medium text-slate-700 mb-1.5">Nouveau statut</label>
            <select id="vs-newStatus" value={newStatus} onChange={e => setNewStatus(e.target.value as VehicleStatus)}
              disabled={statusVehicle?.status === 'Sold'}
              className={inputCls}>
              {STATUS_OPTIONS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
          </div>
          {statusVehicle?.status === 'Sold' && (
            <p className="text-xs text-red-500 bg-red-50 border border-red-100 rounded-lg px-3 py-2">
              Un véhicule vendu ne peut plus changer de statut.
            </p>
          )}
        </div>
        <div className="flex justify-end gap-3 mt-6 pt-4" style={{ borderTop: '1px solid var(--border-light)' }}>
          <button onClick={() => setStatusVehicle(null)}
            className="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg transition-colors font-medium">
            Annuler
          </button>
          <button onClick={() => statusVehicle && statusM.mutate({ id: statusVehicle.id, status: newStatus })}
            disabled={statusM.isPending} className="fm-btn-primary">
            {statusM.isPending ? 'Enregistrement...' : 'Confirmer'}
          </button>
        </div>
      </Modal>

      <Modal open={!!deleteVehicle} onClose={() => setDeleteVehicle(null)} title="Supprimer le véhicule" size="sm">
        <p className="text-sm text-slate-600">
          Vous êtes sur le point de supprimer{' '}
          <span className="font-semibold text-slate-900">{deleteVehicle?.brand} {deleteVehicle?.model}</span>{' '}
          <span className="font-mono text-xs text-slate-400">({deleteVehicle?.vin})</span>.
        </p>
        <p className="text-xs text-slate-500 mt-2">
          Le véhicule sera retiré du parc et placé dans les archives, avec son historique d’interventions.
        </p>
        <div className="flex justify-end gap-3 mt-6 pt-4" style={{ borderTop: '1px solid var(--border-light)' }}>
          <button onClick={() => setDeleteVehicle(null)}
            className="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg transition-colors font-medium">
            Annuler
          </button>
          <button onClick={() => deleteVehicle && deleteM.mutate(deleteVehicle.id)} disabled={deleteM.isPending}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm bg-red-600 hover:bg-red-700 text-white font-medium rounded-lg transition-colors disabled:opacity-50">
            {deleteM.isPending ? 'Suppression...' : 'Supprimer'}
          </button>
        </div>
      </Modal>
    </div>
  )
}

// ── ActionBtn ──────────────────────────────────────────────────────────────────

function ActionBtn({ onClick, icon, title, color }: {
  onClick: () => void
  icon: React.ReactNode
  title: string
  color: 'blue' | 'slate' | 'red'
}) {
  const colors = {
    blue:  'text-slate-400 hover:text-blue-600 hover:bg-blue-50',
    slate: 'text-slate-400 hover:text-slate-700 hover:bg-slate-100',
    red:   'text-slate-400 hover:text-red-600 hover:bg-red-50',
  }
  return (
    <button onClick={onClick} aria-label={title}
      className={`p-1.5 rounded-md transition-colors ${colors[color]}`}>
      {icon}
    </button>
  )
}
