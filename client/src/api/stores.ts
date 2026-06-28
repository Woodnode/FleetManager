import apiClient from './client'
import type { Store, CreateStoreRequest } from '../types'

export interface UpdateStoreRequest {
  name:       string
  address:    string
  postalCode: string
  city:       string
}

export const storesApi = {
  getAll:  () => apiClient.get<Store[]>('/stores').then(r => r.data),
  create:  (data: CreateStoreRequest)              => apiClient.post<Store>('/stores', data).then(r => r.data),
  update:  (id: string, data: UpdateStoreRequest) => apiClient.put<Store>(`/stores/${id}`, data).then(r => r.data),
  remove:  (id: string)                           => apiClient.delete(`/stores/${id}`),
}
