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
import { SkeletonTable } from '../components/ui/Skeleton'
import { useIsMobile } from '../hooks/useMediaQuery'
import PageHeader from '../components/ui/PageHeader'
import Pagination from '../components/ui/Pagination'
import { createInterventionSchema, type CreateInterventionFormValues } from '../schemas/intervention'
import type { Intervention, InterventionStatus, InterventionType, CreateInterventionRequest } from '../types'

// ── Constants ──────────────────────────────────────────────────────────────────

const FORM_DEFAULTS: CreateInterventionFormValues = {
  vehicleId: '', storeId: '', technicianId: '',
  type: 'Maintenance', plannedStartDate: '', plannedEndDate: '', comment: '',
}

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

// ── Urgency helpers ────────────────────────────────────────────────────────────

type Urgency = 'overdue' | 'soon' | 'inprogress' | 'none'

const URGENCY_BORDER: Record<Urgency, string> = {
  overdue:    '#ef4444',
  soon:       '#f59e0b',
  inprogress: '#4c6ef5',
  none:       'transparent',
}

function getUrgency(i: Intervention): Urgency {
  if (i.status === 'Completed' || i.status === 'Cancelled') return 'none'
  const now   = Date.now()
  const end   = new Date(i.plannedEndDate).getTime()
  const start = new Date(i.plannedStartDate).getTime()
  if (i.status === 'InProgress' && end < now) return 'overdue'
  if (i.status === 'InProgress')              return 'inprogress'
  if (i.status === 'Planned' && start < now)  return 'soon'
  return 'none'
}

function relativeDate(dateStr: string): { label: string; color: string } {
  const diff = Math.ceil((new Date(dateStr).getTime() - Date.now()) / 86_400_000)
  if (diff < -1) return { label: `En retard ${Math.abs(diff)}j`, color: '#ef4444' }
  if (diff === -1) return { label: 'Hier',           color: '#f59e0b' }
  if (diff === 0)  return { label: "Aujourd'hui",    color: '#f59e0b' }
  if (diff <= 3)   return { label: `Dans ${diff}j`,  color: '#f59e0b' }
  return { label: new Date(dateStr).toLocaleDateString('fr-FR'), color: '#94a3b8' }
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function TechnicianAvatar({ name }: { name?: string | null }) {
  if (!name) return <span className="text-sm text-slate-400">Non assigné</span>
  const initials = name.trim().split(/\s+/).map(p => p[0]).join('').slice(0, 2).toUpperCase()
  return (
    <div className="flex items-center gap-2">
      <div
        className="w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold shrink-0"
        style={{ background: 'rgba(124,58,237,0.10)', color: '#7c3aed' }}
      >
        {initials}
      </div>
      <span className="text-sm text-slate-500 truncate max-w-[110px]">{name}</span>
    </div>
  )
}

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
    <button onClick={onClick} aria-label={title}
      className={`fm-icon-btn ${colors[color]}`}>
      {icon}
    </button>
  )
}

function InterventionCard({ intervention: i, onStatus }: {
  intervention: Intervention
  onStatus: (i: Intervention, next: InterventionStatus) => void
}) {
  const urgency  = getUrgency(i)
  const dateInfo = relativeDate(i.plannedStartDate)
  const canStart  = i.status === 'Planned'
  const canFinish = i.status === 'InProgress'
  const canCancel = canStart || canFinish
  return (
    <article className="fm-card p-4" style={{ borderLeft: `3px solid ${URGENCY_BORDER[urgency]}` }}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-semibold text-slate-900 truncate">{i.vehicleBrand} {i.vehicleModel}</p>
          <p className="text-xs text-slate-400 font-mono mt-0.5 truncate">{i.vehicleVin}</p>
        </div>
        <span className="text-xs tabular-nums font-semibold shrink-0 mt-0.5" style={{ color: dateInfo.color }}>{dateInfo.label}</span>
      </div>
      <div className="flex flex-wrap items-center gap-1.5 mt-3">
        <Badge value={i.type} label={i.typeLabel} />
        <Badge value={i.status} label={i.statusLabel} />
      </div>
      <p className="text-xs text-slate-500 mt-2.5 truncate">{i.technicianFullName ?? 'Non assigné'} · {i.storeName}</p>
      {canCancel && (
        <div className="flex gap-2 mt-3 pt-3" style={{ borderTop: '1px solid var(--border-light)' }}>
          {canStart && (
            <button onClick={() => onStatus(i, 'InProgress')} className="fm-btn-ghost flex-1 text-blue-600 bg-blue-50/60">
              <Play size={14} />Démarrer
            </button>
          )}
          {canFinish && (
            <button onClick={() => onStatus(i, 'Completed')} className="fm-btn-ghost flex-1 text-emerald-700 bg-emerald-50/60">
              <CheckCheck size={14} />Terminer
            </button>
          )}
          <button onClick={() => onStatus(i, 'Cancelled')} className="fm-btn-ghost flex-1 text-red-600 bg-red-50/60">
            <XCircle size={14} />Annuler
          </button>
        </div>
      )}
    </article>
  )
}

// ── Types ──────────────────────────────────────────────────────────────────────

interface StatusAction {
  intervention: Intervention
  nextStatus: InterventionStatus
}

// ── Component ──────────────────────────────────────────────────────────────────

export default function Interventions() {
  const qc = useQueryClient()

  const [statusFilter, setStatusFilter] = useState('')
  const [typeFilter,   setTypeFilter]   = useState('')
  const [page, setPage]                 = useState(1)
  const [addOpen, setAddOpen]           = useState(false)
  const [statusAction, setStatusAction] = useState<StatusAction | null>(null)
  const [comment, setComment]           = useState('')
  const isMobile = useIsMobile()

  // ── Form ─────────────────────────────────────────────────────────────────────
  const {
    register, handleSubmit, watch, setValue, reset,
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

  const handleStatusFilterChange = (v: string) => { setStatusFilter(v); setPage(1) }
  const handleTypeFilterChange   = (v: string) => { setTypeFilter(v);   setPage(1) }

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
    if (watchedStoreId) qc.removeQueries({ queryKey: ['technicians', watchedStoreId] })
    setValue('storeId', storeId, { shouldValidate: true })
    setValue('technicianId', '')
  }

  const onSubmit = (data: CreateInterventionFormValues) => createM.mutate(data)

  const openAdd  = () => { reset(FORM_DEFAULTS); setAddOpen(true) }
  const closeAdd = () => { reset(FORM_DEFAULTS); setAddOpen(false) }

  const handleStatusChange = () => {
    if (!statusAction) return
    if (statusAction.nextStatus === 'Cancelled' && !comment.trim()) {
      toast.error('Une raison est requise pour annuler')
      return
    }
    statusM.mutate({
      id:      statusAction.intervention.id,
      status:  statusAction.nextStatus,
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
    <div className="fm-page">
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
      <div className="grid grid-cols-2 sm:flex gap-3 mb-4">
        <select value={statusFilter} onChange={e => handleStatusFilterChange(e.target.value)}
          aria-label="Filtrer par statut" className="fm-input sm:w-auto">
          <option value="">Tous les statuts</option>
          {STATUS_OPTIONS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
        <select value={typeFilter} onChange={e => handleTypeFilterChange(e.target.value)}
          aria-label="Filtrer par type" className="fm-input sm:w-auto">
          <option value="">Tous les types</option>
          {TYPE_OPTIONS.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
        </select>
      </div>

      {/* Urgency legend */}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 mb-3">
        {([
          { color: '#4c6ef5', label: 'En cours' },
          { color: '#f59e0b', label: 'À démarrer / bientôt' },
          { color: '#ef4444', label: 'En retard' },
        ] as const).map(({ color, label }) => (
          <div key={label} className="flex items-center gap-1.5">
            <div className="w-2.5 h-2.5 rounded-sm" style={{ background: color, opacity: 0.8 }} />
            <span className="text-xs text-slate-500">{label}</span>
          </div>
        ))}
      </div>

      {/* Table (md et plus) / cartes (mobile) */}
      {isLoading ? (
        <SkeletonTable rows={6} cols={7} />
      ) : isMobile ? (
        <div className="space-y-3">
          {interventions.length === 0 ? (
            <div className="fm-card px-5 py-14 text-center text-sm text-slate-400">
              {statusFilter || typeFilter ? 'Aucune intervention ne correspond aux filtres' : 'Aucune intervention enregistrée'}
            </div>
          ) : interventions.map(i => (
            <InterventionCard key={i.id} intervention={i} onStatus={openStatus} />
          ))}
          {interventionsPage && interventionsPage.totalPages > 1 && (
            <div className="fm-card overflow-hidden">
              <Pagination
                page={interventionsPage.page}
                totalPages={interventionsPage.totalPages}
                totalCount={interventionsPage.totalCount}
                pageSize={interventionsPage.pageSize}
                onPageChange={p => setPage(p)}
              />
            </div>
          )}
        </div>
      ) : (
        <div className="fm-card overflow-hidden">
          <div className="overflow-x-auto">
          <table className="w-full">
            <caption className="sr-only">Liste des interventions</caption>
            <thead>
              <tr style={{ background: '#fafbfd', borderBottom: '1px solid var(--border-light)' }}>
                <th scope="col" className="py-3.5 text-left fm-th" style={{ paddingLeft: 14, paddingRight: 20 }}>Véhicule</th>
                <th scope="col" className="px-5 py-3.5 text-left fm-th">Type</th>
                <th scope="col" className="hidden xl:table-cell px-5 py-3.5 text-left fm-th">Technicien</th>
                <th scope="col" className="hidden xl:table-cell px-5 py-3.5 text-left fm-th">Enseigne</th>
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
                      ? 'Aucune intervention ne correspond aux filtres'
                      : 'Aucune intervention enregistrée'}
                  </td>
                </tr>
              ) : interventions.map(i => {
                const urgency  = getUrgency(i)
                const dateInfo = relativeDate(i.plannedStartDate)

                const showProgress = i.status === 'InProgress'
                const startMs = new Date(i.plannedStartDate).getTime()
                const endMs   = new Date(i.plannedEndDate).getTime()
                const progress = showProgress
                  ? Math.min(100, Math.max(5, ((Date.now() - startMs) / (endMs - startMs)) * 100))
                  : 0

                return (
                  <tr
                    key={i.id}
                    className="transition-colors hover:bg-slate-50/80 group"
                    style={{
                      borderBottom: '1px solid var(--border-light)',
                      borderLeft:   `3px solid ${URGENCY_BORDER[urgency]}`,
                    }}
                  >
                    {/* Véhicule */}
                    <td className="py-3.5" style={{ paddingLeft: 14, paddingRight: 20 }}>
                      <p className="text-sm font-medium text-slate-900">{i.vehicleBrand} {i.vehicleModel}</p>
                      <p className="text-xs text-slate-400 font-mono mt-0.5">{i.vehicleVin}</p>
                      <p className="xl:hidden text-xs text-slate-500 mt-0.5">{i.technicianFullName ?? 'Non assigné'} · {i.storeName}</p>
                    </td>

                    {/* Type */}
                    <td className="px-5 py-3.5">
                      <Badge value={i.type} label={i.typeLabel} />
                    </td>

                    {/* Technicien */}
                    <td className="hidden xl:table-cell px-5 py-3.5">
                      <TechnicianAvatar name={i.technicianFullName} />
                    </td>

                    {/* Enseigne */}
                    <td className="hidden xl:table-cell px-5 py-3.5 text-sm text-slate-500">{i.storeName}</td>

                    {/* Statut + progress bar */}
                    <td className="px-5 py-3.5">
                      <Badge value={i.status} label={i.statusLabel} />
                      {showProgress && (
                        <div className="mt-1.5 w-16 h-1 bg-slate-100 rounded-full overflow-hidden">
                          <div
                            className="h-full rounded-full"
                            style={{
                              width:      `${progress}%`,
                              background: urgency === 'overdue' ? '#ef4444' : '#4c6ef5',
                            }}
                          />
                        </div>
                      )}
                    </td>

                    {/* Date début (relative) */}
                    <td className="px-5 py-3.5 text-sm tabular-nums font-medium whitespace-nowrap" style={{ color: dateInfo.color }}>
                      {dateInfo.label}
                    </td>

                    {/* Actions */}
                    <td className="px-5 py-3.5">
                      <div className="flex items-center justify-end gap-1 fm-reveal">
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
                )
              })}
            </tbody>
          </table>
          </div>

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
      )}

      {/* ── Modale : Créer une intervention ── */}
      <Modal open={addOpen} onClose={closeAdd} title="Nouvelle intervention" size="md">
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">

          <div>
            <label htmlFor="int-vehicleId" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Véhicule <span className="text-red-400">*</span>
            </label>
            <select id="int-vehicleId" {...register('vehicleId')} className="fm-input">
              <option value="">Sélectionner un véhicule</option>
              {vehicles.map(v => (
                <option key={v.id} value={v.id}>
                  {v.brand} {v.model} ({v.vin})
                  {v.status !== 'Available' ? ` (${v.statusLabel})` : ''}
                </option>
              ))}
            </select>
            {errors.vehicleId && <p className="text-red-400 text-xs mt-1">{errors.vehicleId.message}</p>}
          </div>

          <div>
            <label htmlFor="int-type" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Type <span className="text-red-400">*</span>
            </label>
            <select id="int-type" {...register('type')} className="fm-input">
              {TYPE_OPTIONS.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
          </div>

          <div>
            <label htmlFor="int-storeId" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Enseigne <span className="text-red-400">*</span>
            </label>
            <select id="int-storeId" value={watchedStoreId}
              onChange={e => handleStoreChange(e.target.value)} className="fm-input">
              <option value="">Sélectionner une enseigne</option>
              {stores.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
            {errors.storeId && <p className="text-red-400 text-xs mt-1">{errors.storeId.message}</p>}
          </div>

          <div>
            <label htmlFor="int-technicianId" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Technicien <span className="text-red-400">*</span>
            </label>
            <select id="int-technicianId" {...register('technicianId')}
              disabled={!watchedStoreId || loadingTechs} className="fm-input">
              <option value="">
                {!watchedStoreId
                  ? "Sélectionnez d'abord une enseigne"
                  : loadingTechs
                    ? 'Chargement...'
                    : technicians.length === 0
                      ? 'Aucun technicien dans cette enseigne'
                      : 'Sélectionner un technicien'}
              </option>
              {technicians.map(t => <option key={t.id} value={t.id}>{t.fullName}</option>)}
            </select>
            {errors.technicianId && <p className="text-red-400 text-xs mt-1">{errors.technicianId.message}</p>}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label htmlFor="int-startDate" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
                Date début <span className="text-red-400">*</span>
              </label>
              <input id="int-startDate" type="date" {...register('plannedStartDate')} className="fm-input" />
              {errors.plannedStartDate && <p className="text-red-400 text-xs mt-1">{errors.plannedStartDate.message}</p>}
            </div>
            <div>
              <label htmlFor="int-endDate" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
                Date fin <span className="text-red-400">*</span>
              </label>
              <input id="int-endDate" type="date" {...register('plannedEndDate')} className="fm-input" />
              {errors.plannedEndDate && <p className="text-red-400 text-xs mt-1">{errors.plannedEndDate.message}</p>}
            </div>
          </div>

          <div>
            <label htmlFor="int-comment" className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Commentaire
            </label>
            <textarea id="int-comment" {...register('comment')} rows={3}
              placeholder="Description optionnelle..." className="fm-input resize-none" />
          </div>

          <div className="fm-modal-actions pt-4" style={{ borderTop: '1px solid var(--border-light)' }}>
            <button type="button" onClick={closeAdd}
              className="fm-btn-ghost">
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
                <textarea id="status-comment" value={comment} onChange={e => setComment(e.target.value)}
                  rows={3}
                  placeholder={statusAction.nextStatus === 'Cancelled' ? "Raison de l'annulation..." : 'Optionnel...'}
                  className="fm-input resize-none" />
              </div>
            )}
          </div>
        )}

        <div className="fm-modal-actions mt-6 pt-4"
          style={{ borderTop: '1px solid var(--border-light)' }}>
          <button onClick={() => setStatusAction(null)}
            className="fm-btn-ghost">
            Annuler
          </button>
          <button onClick={handleStatusChange} disabled={statusM.isPending}
            className={`fm-btn-danger ${
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
