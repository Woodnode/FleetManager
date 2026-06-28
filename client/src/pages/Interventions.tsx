import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Play, CheckCheck, XCircle } from 'lucide-react'
import toast from 'react-hot-toast'
import { interventionsApi } from '../api/interventions'
import { vehiclesApi } from '../api/vehicles'
import { storesApi } from '../api/stores'
import { usersApi } from '../api/users'
import Badge from '../components/ui/Badge'
import Modal from '../components/ui/Modal'
import Spinner from '../components/ui/Spinner'
import PageHeader from '../components/ui/PageHeader'
import Pagination from '../components/ui/Pagination'
import { createInterventionSchema, type CreateInterventionFormValues } from '../schemas/intervention'
import type { Intervention, InterventionStatus, InterventionType, CreateInterventionRequest } from '../types'

const FORM_DEFAULTS: CreateInterventionFormValues = {
  vehicleId: '', storeId: '', technicianId: '',
  type: 'Maintenance', plannedStartDate: '', plannedEndDate: '', comment: '',
}

// ── Constants ─────────────────────────────────────────────────────────────────

const TYPE_OPTIONS: { value: InterventionType; label: string }[] = [
  { value: 'Maintenance', label: 'Maintenance' },
  { value: 'Repair',      label: 'Réparation' },
  { value: 'Inspection',  label: 'Inspection' },
  { value: 'Other',       label: 'Autre' },
]

const STATUS_OPTIONS: { value: InterventionStatus; label: string }[] = [
  { value: 'Planned',    label: 'Planifiée' },
  { value: 'InProgress', label: 'En cours' },
  { value: 'Completed',  label: 'Terminée' },
  { value: 'Cancelled',  label: 'Annulée' },
]

interface StatusAction {
  intervention: Intervention
  nextStatus: InterventionStatus
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function Interventions() {
  const qc = useQueryClient()

  const [statusFilter, setStatusFilter] = useState('')
  const [typeFilter,   setTypeFilter]   = useState('')
  const [page, setPage]                 = useState(1)
  const [addOpen, setAddOpen]           = useState(false)
  const [statusAction, setStatusAction] = useState<StatusAction | null>(null)
  const [comment, setComment]           = useState('')

  // ── React Hook Form ──────────────────────────────────────────────────────────
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors },
  } = useForm<CreateInterventionFormValues>({
    resolver: zodResolver(createInterventionSchema),
    defaultValues: FORM_DEFAULTS,
  })

  const watchedStoreId = watch('storeId')

  // ── Queries ──────────────────────────────────────────────────────────────────
  const { data: interventionsPage, isLoading } = useQuery({
    queryKey: ['interventions', page, statusFilter, typeFilter],
    queryFn: () => interventionsApi.getAll(page, 20, statusFilter || undefined, typeFilter || undefined),
    staleTime: 30_000,
  })
  const interventions = interventionsPage?.items ?? []

  const handleStatusFilterChange = (value: string) => { setStatusFilter(value); setPage(1) }
  const handleTypeFilterChange   = (value: string) => { setTypeFilter(value);   setPage(1) }

  const { data: vehicles = [] } = useQuery({
    queryKey: ['vehicles-select'],
    queryFn: () => vehiclesApi.getAll(1, 500).then(r => r.items),
  })

  const { data: stores = [] } = useQuery({
    queryKey: ['stores'],
    queryFn: storesApi.getAll,
  })

  const { data: technicians = [], isFetching: loadingTechs } = useQuery({
    queryKey: ['technicians', watchedStoreId],
    queryFn: () => usersApi.getTechniciansByStore(watchedStoreId!),
    enabled: !!watchedStoreId,
  })

  // ── Mutations ─────────────────────────────────────────────────────────────────
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['interventions'] })
    qc.invalidateQueries({ queryKey: ['vehicles'] })
  }

  const createM = useMutation({
    mutationFn: (d: CreateInterventionRequest) => interventionsApi.create(d),
    onSuccess: () => {
      invalidate()
      reset(FORM_DEFAULTS)
      setAddOpen(false)
      toast.success('Intervention créée')
    },
    onError: () => toast.error('Erreur lors de la création'),
  })

  const statusM = useMutation({
    mutationFn: ({ id, status, comment }: { id: string; status: string; comment?: string }) =>
      interventionsApi.changeStatus(id, status, comment),
    onSuccess: () => {
      invalidate()
      setStatusAction(null)
      setComment('')
      toast.success('Statut mis à jour')
    },
    onError: () => toast.error('Changement de statut refusé'),
  })

  // ── Handlers ─────────────────────────────────────────────────────────────────
  const handleStoreChange = (storeId: string) => {
    // Remove stale technicians cache for the previously-selected store
    if (watchedStoreId) qc.removeQueries({ queryKey: ['technicians', watchedStoreId] })
    setValue('storeId', storeId, { shouldValidate: true })
    setValue('technicianId', '')
  }

  const onSubmit = (data: CreateInterventionFormValues) => {
    createM.mutate(data)
  }

  const openAdd = () => { reset(FORM_DEFAULTS); setAddOpen(true) }
  const closeAdd = () => { reset(FORM_DEFAULTS); setAddOpen(false) }

  const handleStatusChange = () => {
    if (!statusAction) return
    if (statusAction.nextStatus === 'Cancelled' && !comment.trim())
      return toast.error('Une raison est requise pour annuler')
    statusM.mutate({
      id: statusAction.intervention.id,
      status: statusAction.nextStatus,
      comment: comment || undefined,
    })
  }

  const openStatus = (i: Intervention, next: InterventionStatus) => {
    setComment('')
    setStatusAction({ intervention: i, nextStatus: next })
  }

  const modalTitle = (next?: InterventionStatus) => {
    if (next === 'InProgress') return "Démarrer l'intervention"
    if (next === 'Completed')  return "Terminer l'intervention"
    if (next === 'Cancelled')  return "Annuler l'intervention"
    return ''
  }

  // ── Render ────────────────────────────────────────────────────────────────────
  return (
    <div className="p-8 fm-page">
      <PageHeader
        title="Interventions"
        subtitle={`${interventionsPage?.totalCount ?? 0} intervention${(interventionsPage?.totalCount ?? 0) !== 1 ? 's' : ''} enregistrée${(interventionsPage?.totalCount ?? 0) !== 1 ? 's' : ''}`}
        action={
          <button onClick={openAdd} className="fm-btn-primary">
            <Plus size={15} />Nouvelle intervention
          </button>
        }
      />

      {/* Filters */}
      <div className="flex gap-3 mb-5">
        <select value={statusFilter} onChange={e => handleStatusFilterChange(e.target.value)}
          className="fm-input" style={{ width: 'auto' }}>
          <option value="">Tous les statuts</option>
          {STATUS_OPTIONS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
        <select value={typeFilter} onChange={e => handleTypeFilterChange(e.target.value)}
          className="fm-input" style={{ width: 'auto' }}>
          <option value="">Tous les types</option>
          {TYPE_OPTIONS.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
        </select>
      </div>

      {/* Table */}
      <div className="fm-card overflow-hidden">
        {isLoading ? (
          <div className="flex justify-center py-20"><Spinner className="w-7 h-7" /></div>
        ) : (
          <table className="w-full">
            <caption className="sr-only">Liste des interventions</caption>
            <thead>
              <tr style={{ background: '#fafbfd', borderBottom: '1px solid var(--border-light)' }}>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Véhicule</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Type</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Technicien</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Enseigne</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Statut</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Date début</th>
                <th scope="col" className="px-5 py-3.5 text-right fm-th">Actions</th>
              </tr>
            </thead>
            <tbody>
              {interventions.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-5 py-14 text-center text-sm text-slate-400">
                    {statusFilter || typeFilter
                      ? 'Aucune intervention correspond aux filtres'
                      : 'Aucune intervention enregistrée'}
                  </td>
                </tr>
              ) : interventions.map(i => (
                <tr key={i.id} className="transition-colors hover:bg-slate-50/80 group"
                  style={{ borderBottom: '1px solid var(--border-light)' }}>
                  <td className="px-5 py-3.5">
                    <p className="text-sm font-medium text-slate-900">{i.vehicleBrand} {i.vehicleModel}</p>
                    <p className="text-xs text-slate-400 font-mono mt-0.5">{i.vehicleVin}</p>
                  </td>
                  <td className="px-5 py-3.5"><Badge value={i.type} label={i.typeLabel} /></td>
                  <td className="px-5 py-3.5 text-sm text-slate-500">{i.technicianFullName ?? '—'}</td>
                  <td className="px-5 py-3.5 text-sm text-slate-500">{i.storeName}</td>
                  <td className="px-5 py-3.5"><Badge value={i.status} label={i.statusLabel} /></td>
                  <td className="px-5 py-3.5 text-sm text-slate-500 tabular-nums">
                    {new Date(i.plannedStartDate).toLocaleDateString('fr-FR')}
                  </td>
                  <td className="px-5 py-3.5">
                    <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                      {i.status === 'Planned' && (
                        <ActionBtn onClick={() => openStatus(i, 'InProgress')}
                          icon={<Play size={13} />} title="Démarrer" color="blue" />
                      )}
                      {i.status === 'InProgress' && (
                        <ActionBtn onClick={() => openStatus(i, 'Completed')}
                          icon={<CheckCheck size={13} />} title="Terminer" color="green" />
                      )}
                      {(i.status === 'Planned' || i.status === 'InProgress') && (
                        <ActionBtn onClick={() => openStatus(i, 'Cancelled')}
                          icon={<XCircle size={13} />} title="Annuler" color="red" />
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {!isLoading && interventionsPage && interventionsPage.totalPages > 1 && (
          <Pagination
            page={interventionsPage.page}
            totalPages={interventionsPage.totalPages}
            totalCount={interventionsPage.totalCount}
            pageSize={interventionsPage.pageSize}
            onPageChange={p => setPage(p)}
          />
        )}
      </div>

      {/* ── Modale : Créer une intervention ── */}
      <Modal open={addOpen} onClose={closeAdd} title="Nouvelle intervention" size="md">
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">

          {/* Véhicule */}
          <div>
            <label htmlFor="int-vehicleId" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Véhicule <span className="text-red-400">*</span>
            </label>
            <select id="int-vehicleId" {...register('vehicleId')} className="fm-input">
              <option value="">Sélectionner un véhicule</option>
              {vehicles.map(v => (
                <option key={v.id} value={v.id}>
                  {v.brand} {v.model} — {v.vin}
                  {v.status !== 'Available' ? ` (${v.statusLabel})` : ''}
                </option>
              ))}
            </select>
            {errors.vehicleId && (
              <p className="text-red-400 text-xs mt-1">{errors.vehicleId.message}</p>
            )}
          </div>

          {/* Type */}
          <div>
            <label htmlFor="int-type" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Type <span className="text-red-400">*</span>
            </label>
            <select id="int-type" {...register('type')} className="fm-input">
              {TYPE_OPTIONS.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
          </div>

          {/* Enseigne */}
          <div>
            <label htmlFor="int-storeId" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Enseigne <span className="text-red-400">*</span>
            </label>
            <select
              id="int-storeId"
              value={watchedStoreId}
              onChange={e => handleStoreChange(e.target.value)}
              className="fm-input"
            >
              <option value="">Sélectionner une enseigne</option>
              {stores.map(s => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
            {errors.storeId && (
              <p className="text-red-400 text-xs mt-1">{errors.storeId.message}</p>
            )}
          </div>

          {/* Technicien — activé uniquement après sélection d'une enseigne */}
          <div>
            <label htmlFor="int-technicianId" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Technicien <span className="text-red-400">*</span>
            </label>
            <select
              id="int-technicianId"
              {...register('technicianId')}
              disabled={!watchedStoreId || loadingTechs}
              className="fm-input"
            >
              <option value="">
                {!watchedStoreId
                  ? "Sélectionnez d'abord une enseigne"
                  : loadingTechs
                    ? 'Chargement...'
                    : technicians.length === 0
                      ? 'Aucun technicien dans cette enseigne'
                      : 'Sélectionner un technicien'}
              </option>
              {technicians.map(t => (
                <option key={t.id} value={t.id}>{t.fullName}</option>
              ))}
            </select>
            {errors.technicianId && (
              <p className="text-red-400 text-xs mt-1">{errors.technicianId.message}</p>
            )}
          </div>

          {/* Dates */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="int-startDate" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
                Date début <span className="text-red-400">*</span>
              </label>
              <input id="int-startDate" type="date" {...register('plannedStartDate')} className="fm-input" />
              {errors.plannedStartDate && (
                <p className="text-red-400 text-xs mt-1">{errors.plannedStartDate.message}</p>
              )}
            </div>
            <div>
              <label htmlFor="int-endDate" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
                Date fin <span className="text-red-400">*</span>
              </label>
              <input id="int-endDate" type="date" {...register('plannedEndDate')} className="fm-input" />
              {errors.plannedEndDate && (
                <p className="text-red-400 text-xs mt-1">{errors.plannedEndDate.message}</p>
              )}
            </div>
          </div>

          {/* Commentaire */}
          <div>
            <label htmlFor="int-comment" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Commentaire
            </label>
            <textarea
              id="int-comment"
              {...register('comment')}
              rows={3}
              placeholder="Description optionnelle..."
              className="fm-input resize-none"
            />
          </div>

          <div className="flex justify-end gap-3 pt-4"
            style={{ borderTop: '1px solid var(--border-light)' }}>
            <button type="button" onClick={closeAdd}
              className="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg transition-colors font-medium">
              Annuler
            </button>
            <button type="submit" disabled={createM.isPending} className="fm-btn-primary">
              {createM.isPending ? 'Création...' : 'Créer'}
            </button>
          </div>
        </form>
      </Modal>

      {/* ── Modale : Changer le statut ── */}
      <Modal
        open={!!statusAction}
        onClose={() => setStatusAction(null)}
        title={modalTitle(statusAction?.nextStatus)}
        size="sm"
      >
        {statusAction && (
          <div className="space-y-4">
            <div className="flex items-start gap-3 p-3 rounded-lg"
              style={{ background: 'var(--surface-page)' }}>
              <div>
                <p className="text-sm font-semibold text-slate-900">
                  {statusAction.intervention.vehicleBrand} {statusAction.intervention.vehicleModel}
                </p>
                <p className="text-xs text-slate-400 font-mono mt-0.5">
                  {statusAction.intervention.vehicleVin}
                </p>
              </div>
            </div>

            {(statusAction.nextStatus === 'Completed' || statusAction.nextStatus === 'Cancelled') && (
              <div>
                <label htmlFor="status-comment" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
                  {statusAction.nextStatus === 'Cancelled'
                    ? <>Raison <span className="text-red-400">*</span></>
                    : 'Commentaire de clôture'}
                </label>
                <textarea
                  id="status-comment"
                  value={comment}
                  onChange={e => setComment(e.target.value)}
                  rows={3}
                  placeholder={statusAction.nextStatus === 'Cancelled'
                    ? "Raison de l'annulation..."
                    : 'Optionnel...'}
                  className="fm-input resize-none"
                />
              </div>
            )}
          </div>
        )}

        <div className="flex justify-end gap-3 mt-6 pt-4"
          style={{ borderTop: '1px solid var(--border-light)' }}>
          <button onClick={() => setStatusAction(null)}
            className="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg transition-colors font-medium">
            Annuler
          </button>
          <button onClick={handleStatusChange} disabled={statusM.isPending}
            className={`inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors disabled:opacity-50 text-white ${
              statusAction?.nextStatus === 'Cancelled'  ? 'bg-red-600 hover:bg-red-700' :
              statusAction?.nextStatus === 'Completed'  ? 'bg-emerald-600 hover:bg-emerald-700' :
              'bg-blue-600 hover:bg-blue-700'
            }`}>
            {statusM.isPending ? 'Mise à jour...' : 'Confirmer'}
          </button>
        </div>
      </Modal>
    </div>
  )
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function ActionBtn({ onClick, icon, title, color }: {
  onClick: () => void
  icon: React.ReactNode
  title: string
  color: 'blue' | 'green' | 'red'
}) {
  const colors = {
    blue:  'text-blue-600 hover:bg-blue-50',
    green: 'text-emerald-600 hover:bg-emerald-50',
    red:   'text-red-500 hover:bg-red-50',
  }
  return (
    <button
      onClick={onClick}
      aria-label={title}
      className={`p-1.5 rounded-md transition-colors ${colors[color]}`}
    >
      {icon}
    </button>
  )
}
