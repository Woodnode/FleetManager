import apiClient from './client'
import type { Technician } from '../types'

export const usersApi = {
  getTechniciansByStore: (storeId: string) =>
    apiClient.get<Technician[]>(`/users/technicians/${storeId}`).then(r => r.data),
}
