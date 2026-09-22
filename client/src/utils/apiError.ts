import { isAxiosError } from 'axios'

/**
 * Message à afficher pour une erreur d'API. L'API renvoie des ProblemDetails (RFC 7807) dont le
 * champ `detail` contient le message métier (ex. « Impossible de supprimer un véhicule avec une
 * intervention planifiée ou en cours. »). Sans message exploitable, on garde le texte générique.
 */
export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (isAxiosError(error)) {
    const detail = (error.response?.data as { detail?: unknown } | undefined)?.detail
    if (typeof detail === 'string' && detail.trim() !== '') return detail
  }
  return fallback
}

/** Véhicule archivé qui porte déjà le VIN saisi (réponse 409 de la création d'un véhicule). */
export interface ArchivedVinConflict {
  archivedVehicleId: string
  brand: string
  model: string
  year: number
  storeName: string | null
  deletedAt: string | null
  message: string
}

/**
 * Lit le conflit « VIN archivé » renvoyé par l'API. Les détails ne sont présents que si
 * l'utilisateur peut restaurer ce véhicule ; sinon on retombe sur le message générique.
 */
export function getArchivedVinConflict(error: unknown): ArchivedVinConflict | null {
  if (!isAxiosError(error) || error.response?.status !== 409) return null
  const data = error.response.data as Partial<ArchivedVinConflict> & { detail?: string } | undefined
  if (!data || typeof data.archivedVehicleId !== 'string') return null
  return {
    archivedVehicleId: data.archivedVehicleId,
    brand: data.brand ?? '',
    model: data.model ?? '',
    year: data.year ?? 0,
    storeName: data.storeName ?? null,
    deletedAt: data.deletedAt ?? null,
    message: data.detail ?? '',
  }
}
