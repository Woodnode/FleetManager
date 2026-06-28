import apiClient from './client'
import type { Vehicle, PagedResponse, CreateVehicleRequest, UpdateVehicleRequest } from '../types'

export const vehiclesApi = {
  getAll: (page = 1, pageSize = 20, search?: string, status?: string) =>
    apiClient.get<PagedResponse<Vehicle>>('/vehicles', {
      params: {
        page,
        pageSize,
        ...(search ? { search } : {}),
        ...(status ? { status } : {}),
      },
    }).then(r => r.data),
  getById: (id: string) => apiClient.get<Vehicle>(`/vehicles/${id}`).then(r => r.data),
  getByStore: (storeId: string) => apiClient.get<Vehicle[]>(`/vehicles/store/${storeId}`).then(r => r.data),
  create: (data: CreateVehicleRequest) => apiClient.post<Vehicle>('/vehicles', data).then(r => r.data),
  update: (id: string, data: UpdateVehicleRequest) => apiClient.put<Vehicle>(`/vehicles/${id}`, data).then(r => r.data),
  changeStatus: (id: string, status: string) => apiClient.patch(`/vehicles/${id}/status`, { newStatus: status }).then(r => r.data),
  delete: (id: string) => apiClient.delete(`/vehicles/${id}`),
}
