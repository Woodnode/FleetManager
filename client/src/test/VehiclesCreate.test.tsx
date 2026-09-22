import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AxiosError, AxiosHeaders } from 'axios'
import Vehicles from '../pages/Vehicles'
import * as AuthContext from '../contexts/AuthContext'
import { vehiclesApi } from '../api/vehicles'
import { storesApi } from '../api/stores'

const VIN = '1HGBH41JXMN109186'
const STORE_ID = '3f2b8c1e-4d5a-4e6f-9a7b-1c2d3e4f5a6b'

function conflict(data: unknown) {
  const config = { headers: new AxiosHeaders() }
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', config, null, {
    status: 409, statusText: '', headers: {}, config, data,
  })
}

function renderPage() {
  vi.spyOn(AuthContext, 'useAuth').mockReturnValue({
    user: { userId: 'u1', role: 'Admin', firstName: 'Test', lastName: 'User', storeId: null },
    setUser: vi.fn(),
    logout: vi.fn(),
    loading: false,
  })
  vi.spyOn(vehiclesApi, 'getAll').mockResolvedValue({
    items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, hasNextPage: false, hasPreviousPage: false,
  })
  vi.spyOn(storesApi, 'getAll').mockResolvedValue([{ id: STORE_ID, name: 'Paris Centre' }] as never)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(<QueryClientProvider client={client}><Vehicles /></QueryClientProvider>)
}

async function submitVin() {
  fireEvent.click(await screen.findByRole('button', { name: /ajouter un véhicule/i }))
  fireEvent.change(screen.getByLabelText(/VIN/), { target: { value: VIN } })
  fireEvent.change(screen.getByLabelText(/Marque/), { target: { value: 'Citroën' } })
  fireEvent.change(screen.getByLabelText(/Modèle/), { target: { value: 'C5 X' } })
  await screen.findByRole('option', { name: 'Paris Centre' })
  fireEvent.change(screen.getByLabelText(/Enseigne/), { target: { value: STORE_ID } })
  fireEvent.click(screen.getByRole('button', { name: 'Ajouter' }))
}

describe('Création de véhicule avec un VIN archivé', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('propose de restaurer le véhicule archivé', async () => {
    renderPage()
    vi.spyOn(vehiclesApi, 'create').mockRejectedValue(conflict({
      detail: 'VIN archivé', archivedVehicleId: 'v1', brand: 'Citroën', model: 'C5 X', year: 2022,
      storeName: 'Paris Centre', deletedAt: '2026-09-01T10:00:00',
    }))
    const restore = vi.spyOn(vehiclesApi, 'restore').mockResolvedValue({ id: 'v1', brand: 'Citroën', model: 'C5 X' } as never)

    await submitVin()

    expect(await screen.findByText('Ce VIN appartient à un véhicule archivé')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Restaurer ce véhicule' }))
    await waitFor(() => expect(restore).toHaveBeenCalledWith('v1'))
  })

  it("n'affiche pas de proposition quand le véhicule n'est pas restaurable", async () => {
    renderPage()
    vi.spyOn(vehiclesApi, 'create').mockRejectedValue(conflict({ detail: 'Ce VIN appartient à une autre enseigne.' }))

    await submitVin()

    await waitFor(() => expect(vehiclesApi.create).toHaveBeenCalled())
    expect(screen.queryByText('Ce VIN appartient à un véhicule archivé')).not.toBeInTheDocument()
  })
})
