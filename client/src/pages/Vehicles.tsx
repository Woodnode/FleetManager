import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Pencil, Trash2, RefreshCw, Search } from 'lucide-react'
import toast from 'react-hot-toast'
import { vehiclesApi } from '../api/vehicles'
import { storesApi } from '../api/stores'
import Badge from '../components/ui/Badge'
import Modal from '../components/ui/Modal'
import Spinner from '../components/ui/Spinner'
import PageHeader from '../components/ui/PageHeader'
import Pagination from '../components/ui/Pagination'
import { useAuth } from '../contexts/AuthContext'
import { createVehicleSchema, updateVehicleSchema, type CreateVehicleFormValues, type UpdateVehicleFormValues } from '../schemas/vehicle'
import type { Vehicle, VehicleStatus, CreateVehicleRequest, UpdateVehicleRequest, Store } from '../types'

const STATUS_OPTIONS: { value: VehicleStatus; label: string }[] = [
  { value: 'Available',      label: 'Disponible' },
  { value: 'InIntervention', label: 'En intervention' },
  { value: 'Sold',           label: 'Vendu' },
  { value: 'OutOfService',   label: 'Hors service' },
]

const inputCls = 'fm-input'

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

interface CreateVehicleFormProps { stores: Store[]; onSubmit: (d: CreateVehicleRequest) => void; pending: boolean; onCancel: () => void }

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

interface EditVehicleFormProps { vehicle: Vehicle; stores: Store[]; onSubmit: (d: UpdateVehicleRequest) => void; pending: boolean; onCancel: () => void }

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

export default function Vehicles() {
  const qc = useQueryClient()
  const { user } = useAuth()
  const canDelete = user?.role === 'Admin' || user?.role === 'StoreManager'

  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
  const [addOpen, setAddOpen] = useState(false)
  const [editVehicle, setEditVehicle] = useState<Vehicle | null>(null)
  const [statusVehicle, setStatusVehicle] = useState<Vehicle | null>(null)
  const [deleteVehicle, setDeleteVehicle] = useState<Vehicle | null>(null)
  const [newStatus, setNewStatus] = useState<VehicleStatus>('Available')

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
    onError:   () => toast.error('Erreur lors de l\'ajout'),
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
    onSuccess: () => { invalidate(); setDeleteVehicle(null); toast.success('Véhicule supprimé') },
    onError:   () => toast.error('Erreur lors de la suppression'),
  })

  const handlePageChange = (newPage: number) => setPage(newPage)

  const handleSearchChange = (value: string) => {
    setSearch(value)
    setPage(1)
  }

  const handleStatusChange = (value: string) => {
    setStatusFilter(value)
    setPage(1)
  }

  return (
    <div className="p-8 fm-page">
      <PageHeader
        title="Véhicules"
        subtitle={`${vehiclesPage?.totalCount ?? 0} véhicule${(vehiclesPage?.totalCount ?? 0) !== 1 ? 's' : ''} dans le parc`}
        action={
          <button onClick={() => setAddOpen(true)}
            className="fm-btn-primary">
            <Plus size={15} />Ajouter un véhicule
          </button>
        }
      />

      {/* Filters */}
      <div className="flex gap-3 mb-5">
        <div className="relative flex-1 max-w-sm">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
          <input type="text" placeholder="VIN, marque, modèle..." value={search}
            onChange={e => handleSearchChange(e.target.value)}
            className="fm-input pl-9" />
        </div>
        <select value={statusFilter} onChange={e => handleStatusChange(e.target.value)}
          className="fm-input" style={{ width: 'auto' }}>
          <option value="">Tous les statuts</option>
          {STATUS_OPTIONS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
      </div>

      {/* Table */}
      <div className="fm-card overflow-hidden">
        {isLoading ? (
          <div className="flex justify-center py-20"><Spinner className="w-7 h-7" /></div>
        ) : (
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
                <tr><td colSpan={7} className="px-5 py-14 text-center text-sm text-slate-400">
                  {search || statusFilter ? 'Aucun véhicule correspond aux filtres' : 'Aucun véhicule enregistré'}
                </td></tr>
              ) : vehicles.map(v => (
                <tr key={v.id} className="transition-colors hover:bg-slate-50/80 group"
                  style={{ borderBottom: '1px solid var(--border-light)' }}>
                  <td className="px-5 py-3.5 font-mono text-xs text-slate-500 tracking-wide">{v.vin}</td>
                  <td className="px-5 py-3.5">
                    <span className="text-sm font-medium text-slate-900">{v.brand} {v.model}</span>
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
        )}
        {!isLoading && vehiclesPage && vehiclesPage.totalPages > 1 && (
          <Pagination
            page={vehiclesPage.page}
            totalPages={vehiclesPage.totalPages}
            totalCount={vehiclesPage.totalCount}
            pageSize={vehiclesPage.pageSize}
            onPageChange={handlePageChange}
          />
        )}
      </div>

      {/* Add Modal */}
      <Modal open={addOpen} onClose={() => setAddOpen(false)} title="Ajouter un véhicule" size="md">
        <CreateVehicleForm
          key={addOpen ? 'open' : 'closed'}
          stores={stores}
          onSubmit={d => createM.mutate(d)}
          pending={createM.isPending}
          onCancel={() => setAddOpen(false)}
        />
      </Modal>

      {/* Edit Modal */}
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

      {/* Status Modal */}
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

      {/* Delete Modal */}
      <Modal open={!!deleteVehicle} onClose={() => setDeleteVehicle(null)} title="Supprimer le véhicule" size="sm">
        <p className="text-sm text-slate-600">
          Vous êtes sur le point de supprimer{' '}
          <span className="font-semibold text-slate-900">{deleteVehicle?.brand} {deleteVehicle?.model}</span>{' '}
          <span className="font-mono text-xs text-slate-400">({deleteVehicle?.vin})</span>.
        </p>
        <p className="text-xs text-red-500 mt-2">Cette action est irréversible.</p>
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

function ActionBtn({ onClick, icon, title, color }: {
  onClick: () => void; icon: React.ReactNode; title: string; color: 'blue' | 'slate' | 'red'
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
