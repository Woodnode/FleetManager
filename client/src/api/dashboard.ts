import apiClient from './client'

export interface VehicleSummary {
  total:          number
  available:      number
  inIntervention: number
  sold:           number
  outOfService:   number
}

export interface InterventionSummary {
  total:       number
  planned:     number
  inProgress:  number
  completed:   number
  cancelled:   number
  maintenance: number
  repair:      number
  inspection:  number
  other:       number
}

export interface RecentIntervention {
  id:                 string
  vehicleBrand:       string
  vehicleModel:       string
  vehicleVin:         string
  storeName:          string
  technicianFullName: string | null
  type:               string
  typeLabel:          string
  status:             string
  statusLabel:        string
  plannedStartDate:   string
}

export interface DashboardSummary {
  vehicles:             VehicleSummary
  interventions:        InterventionSummary
  recentInterventions:  RecentIntervention[]
}

export const dashboardApi = {
  getSummary: () => apiClient.get<DashboardSummary>('/dashboard/summary').then(r => r.data),
}
