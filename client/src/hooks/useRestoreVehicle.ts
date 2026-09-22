import { useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { vehiclesApi } from '../api/vehicles'
import { getApiErrorMessage } from '../utils/apiError'
import type { Vehicle } from '../types'

/**
 * Restaure un véhicule archivé. Il réapparaît dans le parc, les compteurs du tableau de bord
 * et, avec lui, son historique d'interventions : toutes ces vues sont rafraîchies.
 */
export function useRestoreVehicle(onRestored?: (vehicle: Vehicle) => void) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => vehiclesApi.restore(id),
    onSuccess: vehicle => {
      for (const key of ['vehicles', 'archived-vehicles', 'dashboard-summary', 'interventions'])
        qc.invalidateQueries({ queryKey: [key] })
      toast.success(`${vehicle.brand} ${vehicle.model} restauré dans le parc`)
      onRestored?.(vehicle)
    },
    onError: err => toast.error(getApiErrorMessage(err, 'Erreur lors de la restauration')),
  })
}
