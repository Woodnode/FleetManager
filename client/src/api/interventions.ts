import apiClient from './client'
import type { Intervention, PagedResponse, CreateInterventionRequest } from '../types'

export const interventionsApi = {
  getAll: (page = 1, pageSize = 20, status?: string, type?: string) =>
    apiClient.get<PagedResponse<Intervention>>('/interventions', {
      params: {
        page,
        pageSize,
        ...(status ? { status } : {}),
        ...(type ? { type } : {}),
      },
    }).then(r => r.data),
  getById: (id: string) => apiClient.get<Intervention>(`/interventions/${id}`).then(r => r.data),
  getByVehicle: (vehicleId: string) => apiClient.get<Intervention[]>(`/interventions/vehicle/${vehicleId}`).then(r => r.data),
  create: (data: CreateInterventionRequest) => apiClient.post<Intervention>('/interventions', data).then(r => r.data),
  changeStatus: (id: string, status: string, comment?: string) =>
    apiClient.patch(`/interventions/${id}/status`, { newStatus: status, comment }).then(r => r.data),
}
