import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import Archives from '../pages/Archives'
import * as AuthContext from '../contexts/AuthContext'
import { vehiclesApi } from '../api/vehicles'
import type { ArchivedVehicle, PagedResponse, UserRole } from '../types'

const archived: ArchivedVehicle = {
  id: 'v1', vin: '1HGBH41JXMN109186', brand: 'Toyota', model: 'Corolla', year: 2019, mileage: 84000,
  storeId: 's1', storeName: 'Paris Centre', deletedAt: '2026-09-01T10:00:00', interventionCount: 2,
}

const page: PagedResponse<ArchivedVehicle> = {
  items: [archived], totalCount: 1, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false, hasPreviousPage: false,
}

function renderAs(role: UserRole) {
  // Recréé à chaque rendu : restoreAllMocks (beforeEach) réinitialise aussi ce mock.
  vi.spyOn(AuthContext, 'useAuth').mockReturnValue({
    user: { userId: 'u1', role, firstName: 'Test', lastName: 'User', storeId: 's1' },
    setUser: vi.fn(),
    logout: vi.fn(),
    loading: false,
  })
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={client}><Archives /></QueryClientProvider>)
}

describe('Archives', () => {
  beforeEach(() => vi.restoreAllMocks())

  it("refuse l'accès aux techniciens sans appeler l'API", () => {
    const getArchived = vi.spyOn(vehiclesApi, 'getArchived')

    renderAs('Technician')

    expect(screen.getByText(/réservé aux administrateurs et aux gérants/i)).toBeInTheDocument()
    expect(getArchived).not.toHaveBeenCalled()
  })

  it('liste les véhicules archivés pour un gérant', async () => {
    vi.spyOn(vehiclesApi, 'getArchived').mockResolvedValue(page)

    renderAs('StoreManager')

    expect(await screen.findByText('Toyota Corolla')).toBeInTheDocument()
    expect(screen.getByText('1 véhicule archivé. Historique conservé pour les rapports.')).toBeInTheDocument()
  })

  it("ouvre l'historique complet du véhicule", async () => {
    vi.spyOn(vehiclesApi, 'getArchived').mockResolvedValue(page)
    const getHistory = vi.spyOn(vehiclesApi, 'getArchivedHistory').mockResolvedValue({
      vehicle: archived,
      interventions: [{
        id: 'i1', vehicleId: 'v1', vehicleBrand: 'Toyota', vehicleModel: 'Corolla', vehicleVin: archived.vin,
        storeId: 's1', storeName: 'Paris Centre', technicianId: 't1', technicianFullName: 'Jean Dupont',
        type: 'Repair', typeLabel: 'Réparation', status: 'Completed', statusLabel: 'Terminée',
        plannedStartDate: '2026-03-02T09:00:00', plannedEndDate: '2026-03-02T17:00:00', comment: 'Embrayage remplacé',
      }],
    } as never)

    renderAs('Admin')
    fireEvent.click(await screen.findByRole('button', { name: /historique de Toyota Corolla/i }))

    await waitFor(() => expect(getHistory).toHaveBeenCalledWith('v1'))
    expect(await screen.findByText('Embrayage remplacé')).toBeInTheDocument()
    expect(screen.getByText('Technicien : Jean Dupont')).toBeInTheDocument()
  })

  it("affiche un état vide explicite quand aucun véhicule n'est archivé", async () => {
    vi.spyOn(vehiclesApi, 'getArchived').mockResolvedValue({ ...page, items: [], totalCount: 0 })

    renderAs('Admin')

    expect(await screen.findByText('Aucun véhicule archivé')).toBeInTheDocument()
  })

  it('restaure un véhicule après confirmation', async () => {
    vi.spyOn(vehiclesApi, 'getArchived').mockResolvedValue(page)
    const restore = vi.spyOn(vehiclesApi, 'restore').mockResolvedValue({ ...archived, status: 'Available' } as never)

    renderAs('StoreManager')
    fireEvent.click(await screen.findByRole('button', { name: /restaurer Toyota Corolla/i }))

    expect(screen.getByText(/ses 2 interventions d’historique/)).toBeInTheDocument()
    expect(restore).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Restaurer' }))

    await waitFor(() => expect(restore).toHaveBeenCalledWith('v1'))
  })
})
