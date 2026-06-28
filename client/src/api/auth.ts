import apiClient from './client'
import type { LoginRequest, LoginResponse } from '../types'

export async function login(data: LoginRequest): Promise<LoginResponse> {
  const response = await apiClient.post<LoginResponse>('/auth/login', data)
  return response.data
}

export interface MeResponse {
  userId: string
  firstName: string | null
  lastName: string | null
  role: string
  storeId: string | null
}

export async function me(): Promise<MeResponse | null> {
  try {
    const response = await apiClient.get<MeResponse>('/auth/me')
    return response.data
  } catch {
    return null
  }
}

export async function refresh(): Promise<MeResponse> {
  const response = await apiClient.post<MeResponse>('/auth/refresh')
  return response.data
}

export async function logout(): Promise<void> {
  await apiClient.post('/auth/logout')
}
