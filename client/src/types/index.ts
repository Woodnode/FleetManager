export interface PagedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export type VehicleStatus = 'Available' | 'InIntervention' | 'Sold' | 'OutOfService'
export type InterventionStatus = 'Planned' | 'InProgress' | 'Completed' | 'Cancelled'
export type InterventionType = 'Maintenance' | 'Repair' | 'Inspection' | 'Other'
export type UserRole = 'Admin' | 'StoreManager' | 'Technician'

export interface Vehicle {
  id: string
  vin: string
  brand: string
  model: string
  year: number
  mileage: number
  status: VehicleStatus
  statusLabel: string
  storeId: string
  storeName: string
}

export interface Intervention {
  id: string
  vehicleId: string
  vehicleBrand: string
  vehicleModel: string
  vehicleVin: string
  storeId: string
  storeName: string
  technicianId?: string
  technicianFullName?: string
  type: InterventionType
  typeLabel: string
  status: InterventionStatus
  statusLabel: string
  plannedStartDate: string
  plannedEndDate: string
  actualEndDate?: string
  comment?: string
}

export interface Store {
  id: string
  name: string
  address: string
  postalCode: string
  city: string
}

export interface User {
  id: string
  email: string
  role: UserRole
  firstName: string
  lastName: string
  storeId?: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  userId: string
  firstName: string
  lastName: string
  email: string
  role: UserRole
  storeId: string | null
}

export interface CreateVehicleRequest {
  vin: string
  brand: string
  model: string
  year: number
  mileage: number
  storeId: string
}

export interface UpdateVehicleRequest {
  brand: string
  model: string
  year: number
  mileage: number
  storeId: string
}

export interface Technician {
  id: string
  fullName: string
  email: string
  storeId: string | null
}

export interface CreateInterventionRequest {
  vehicleId: string
  storeId: string
  technicianId: string
  type: InterventionType
  plannedStartDate: string
  plannedEndDate: string
  comment?: string
}

export interface CreateStoreRequest {
  name: string
  address: string
  postalCode: string
  city: string
}
