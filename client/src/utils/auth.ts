import type { UserRole } from '../types'

export function isManagerOrAdminRole(role: UserRole | undefined): boolean {
  return role === 'Admin' || role === 'StoreManager'
}

export function isAdminRole(role: UserRole | undefined): boolean {
  return role === 'Admin'
}
