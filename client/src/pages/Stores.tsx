import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Building2, Pencil, Trash2 } from 'lucide-react'
import toast from 'react-hot-toast'
import { storesApi } from '../api/stores'
import Modal from '../components/ui/Modal'
import Spinner from '../components/ui/Spinner'
import PageHeader from '../components/ui/PageHeader'
import { useAuth } from '../contexts/AuthContext'
import { isAdminRole } from '../utils/auth'
import { createStoreSchema, type CreateStoreFormValues } from '../schemas/store'
import type { Store } from '../types'

const FORM_DEFAULTS: CreateStoreFormValues = { name: '', address: '', postalCode: '', city: '' }

// ── Sub-components ─────────────────────────────────────────────────────────────

function StoreForm({
  defaultValues = FORM_DEFAULTS,
  onSubmit,
  onCancel,
  pending,
  submitLabel,
}: {
  defaultValues?: CreateStoreFormValues
  onSubmit: (data: CreateStoreFormValues) => void
  onCancel: () => void
  pending: boolean
  submitLabel: string
}) {
  const { register, handleSubmit, formState: { errors } } = useForm<CreateStoreFormValues>({
    resolver: zodResolver(createStoreSchema),
    defaultValues,
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate>
      <div className="space-y-4">
        <div>
          <label htmlFor="store-name"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
            Nom <span className="text-red-400">*</span>
          </label>
          <input id="store-name" {...register('name')}
            className="fm-input" placeholder="AutoGroup Paris Nord" />
          {errors.name && (
            <p className="mt-1 text-xs text-red-500" role="alert">{errors.name.message}</p>
          )}
        </div>

        <div>
          <label htmlFor="store-address"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
            Adresse
          </label>
          <input id="store-address" {...register('address')}
            className="fm-input" placeholder="12 avenue de la République" />
          {errors.address && (
            <p className="mt-1 text-xs text-red-500" role="alert">{errors.address.message}</p>
          )}
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="store-postal"
              className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Code postal
            </label>
            <input id="store-postal" {...register('postalCode')}
              className="fm-input" placeholder="75010" maxLength={10} />
            {errors.postalCode && (
              <p className="mt-1 text-xs text-red-500" role="alert">{errors.postalCode.message}</p>
            )}
          </div>
          <div>
            <label htmlFor="store-city"
              className="block text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
              Ville <span className="text-red-400">*</span>
            </label>
            <input id="store-city" {...register('city')}
              className="fm-input" placeholder="Paris" />
            {errors.city && (
              <p className="mt-1 text-xs text-red-500" role="alert">{errors.city.message}</p>
            )}
          </div>
        </div>
      </div>

      <div className="flex justify-end gap-3 mt-6 pt-4"
        style={{ borderTop: '1px solid var(--border-light)' }}>
        <button type="button" onClick={onCancel}
          className="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg transition-colors font-medium">
          Annuler
        </button>
        <button type="submit" disabled={pending} className="fm-btn-primary">
          {pending ? '...' : submitLabel}
        </button>
      </div>
    </form>
  )
}

// ── Main component ─────────────────────────────────────────────────────────────

export default function Stores() {
  const qc = useQueryClient()
  const { user } = useAuth()
  const isAdmin = isAdminRole(user?.role)

  const [addOpen, setAddOpen]       = useState(false)
  const [editStore, setEditStore]   = useState<Store | null>(null)
  const [deleteStore, setDeleteStore] = useState<Store | null>(null)

  const { data: stores = [], isLoading } = useQuery({
    queryKey: ['stores'], queryFn: storesApi.getAll,
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['stores'] })

  const createM = useMutation({
    mutationFn: storesApi.create,
    onSuccess: () => { invalidate(); setAddOpen(false); toast.success('Enseigne ajoutée') },
    onError:   () => toast.error("Erreur lors de l'ajout"),
  })

  const updateM = useMutation({
    mutationFn: ({ id, data }: { id: string; data: CreateStoreFormValues }) =>
      storesApi.update(id, data),
    onSuccess: () => { invalidate(); setEditStore(null); toast.success('Enseigne modifiée') },
    onError:   () => toast.error('Erreur lors de la modification'),
  })

  const deleteM = useMutation({
    mutationFn: (id: string) => storesApi.remove(id),
    onSuccess: () => { invalidate(); setDeleteStore(null); toast.success('Enseigne supprimée') },
    onError:   () => toast.error('Impossible de supprimer : l\'enseigne contient des véhicules'),
  })

  return (
    <div className="p-8 fm-page">
      <PageHeader
        title="Enseignes"
        subtitle={`${stores.length} enseigne${stores.length !== 1 ? 's' : ''}`}
        action={isAdmin ? (
          <button onClick={() => setAddOpen(true)} className="fm-btn-primary">
            <Plus size={15} />Ajouter une enseigne
          </button>
        ) : undefined}
      />

      {isLoading ? (
        <div className="flex justify-center py-20"><Spinner className="w-7 h-7" /></div>
      ) : stores.length === 0 ? (
        <div
          className="flex flex-col items-center justify-center py-16 rounded-xl text-slate-400"
          style={{ background: 'white', border: '1.5px dashed var(--border-base)' }}
        >
          <div className="w-12 h-12 rounded-xl flex items-center justify-center mb-3"
            style={{ background: 'var(--surface-page)' }}>
            <Building2 size={22} className="text-slate-300" />
          </div>
          <p className="text-sm font-medium text-slate-400">Aucune enseigne enregistrée</p>
          {isAdmin && (
            <button onClick={() => setAddOpen(true)}
              className="mt-3 text-sm font-medium transition-opacity hover:opacity-75"
              style={{ color: 'var(--brand-500)' }}>
              Ajouter la première enseigne
            </button>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {stores.map(s => (
            <div key={s.id} className="fm-card p-5 group">
              <div className="flex items-start gap-3">
                <div
                  className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0"
                  style={{ background: 'rgba(76,110,245,0.08)', border: '1px solid rgba(76,110,245,0.15)' }}
                >
                  <Building2 size={15} style={{ color: 'var(--brand-500)' }} />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold text-slate-900 truncate tracking-tight">{s.name}</p>
                  <p className="text-xs text-slate-400 mt-0.5 truncate">{s.address || '—'}</p>
                  {(s.postalCode || s.city) && (
                    <p className="text-xs text-slate-400 truncate">{s.postalCode} {s.city}</p>
                  )}
                </div>

                {/* Admin actions */}
                {isAdmin && (
                  <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
                    <button
                      onClick={() => setEditStore(s)}
                      aria-label="Modifier l'enseigne"
                      className="p-1.5 rounded-md text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
                    >
                      <Pencil size={13} />
                    </button>
                    <button
                      onClick={() => setDeleteStore(s)}
                      aria-label="Supprimer l'enseigne"
                      className="p-1.5 rounded-md text-slate-400 hover:text-red-600 hover:bg-red-50 transition-colors"
                    >
                      <Trash2 size={13} />
                    </button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Add Modal */}
      <Modal open={addOpen} onClose={() => setAddOpen(false)} title="Ajouter une enseigne" size="sm">
        <StoreForm
          key={addOpen ? 'open' : 'closed'}
          onSubmit={data => createM.mutate(data)}
          onCancel={() => setAddOpen(false)}
          pending={createM.isPending}
          submitLabel="Ajouter"
        />
      </Modal>

      {/* Edit Modal */}
      {editStore && (
        <Modal open onClose={() => setEditStore(null)} title={`Modifier — ${editStore.name}`} size="sm">
          <StoreForm
            key={editStore.id}
            defaultValues={{
              name:       editStore.name,
              address:    editStore.address,
              postalCode: editStore.postalCode,
              city:       editStore.city,
            }}
            onSubmit={data => updateM.mutate({ id: editStore.id, data })}
            onCancel={() => setEditStore(null)}
            pending={updateM.isPending}
            submitLabel="Enregistrer"
          />
        </Modal>
      )}

      {/* Delete Confirm Modal */}
      <Modal open={!!deleteStore} onClose={() => setDeleteStore(null)} title="Supprimer l'enseigne" size="sm">
        <p className="text-sm text-slate-600">
          Vous êtes sur le point de supprimer{' '}
          <span className="font-semibold text-slate-900">{deleteStore?.name}</span>.
        </p>
        <p className="text-xs text-slate-400 mt-1">
          La suppression est impossible si l'enseigne contient encore des véhicules.
        </p>
        <p className="text-xs text-red-500 mt-2">Cette action est irréversible.</p>
        <div className="flex justify-end gap-3 mt-6 pt-4"
          style={{ borderTop: '1px solid var(--border-light)' }}>
          <button onClick={() => setDeleteStore(null)}
            className="px-4 py-2 text-sm text-slate-600 hover:bg-slate-100 rounded-lg transition-colors font-medium">
            Annuler
          </button>
          <button
            onClick={() => deleteStore && deleteM.mutate(deleteStore.id)}
            disabled={deleteM.isPending}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm bg-red-600 hover:bg-red-700 text-white font-medium rounded-lg transition-colors disabled:opacity-50"
          >
            {deleteM.isPending ? 'Suppression...' : 'Supprimer'}
          </button>
        </div>
      </Modal>
    </div>
  )
}
