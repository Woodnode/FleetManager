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
