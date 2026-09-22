import { createContext, useContext } from 'react'
import type { QueryClient } from '@tanstack/react-query'
import apiClient from '../api/client'

export type RealtimeStatus = 'connecting' | 'connected' | 'reconnecting' | 'offline'
type RealtimeEntity = 'vehicles' | 'interventions' | 'stores'

/**
 * Données à recharger pour chaque signal du serveur. Le serveur n'envoie que « ceci a changé » :
 * les données repassent par l'API, qui applique ses contrôles d'accès.
 */
export const INVALIDATIONS: Record<RealtimeEntity, string[]> = {
  vehicles:      ['vehicles', 'vehicles-all', 'vehicles-select', 'archived-vehicles', 'archived-vehicle-history', 'dashboard-summary'],
  interventions: ['interventions', 'dashboard-summary', 'archived-vehicle-history'],
  stores:        ['stores', 'vehicles', 'vehicles-all', 'interventions', 'dashboard-summary'],
}

export const ALL_KEYS = [...new Set(Object.values(INVALIDATIONS).flat())]
export const RETRY_DELAY_MS = 10_000

export function applyChange(qc: QueryClient, entities: readonly string[]) {
  const keys = new Set(entities.flatMap(e => INVALIDATIONS[e as RealtimeEntity] ?? []))
  keys.forEach(key => qc.invalidateQueries({ queryKey: [key] }))
}

export function hubUrl() {
  return `${apiClient.defaults.baseURL}/hubs/fleet`
}

export const RealtimeContext = createContext<RealtimeStatus>('offline')

export const useRealtimeStatus = () => useContext(RealtimeContext)
