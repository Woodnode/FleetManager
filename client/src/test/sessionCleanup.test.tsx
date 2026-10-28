import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from 'axios'
import { AuthProvider, useAuth, type AuthContextType } from '../contexts/AuthContext'
import * as authApi from '../api/auth'
import apiClient from '../api/client'
import Interventions from '../pages/Interventions'
import * as AuthContext from '../contexts/AuthContext'
import { interventionsApi } from '../api/interventions'
import { vehiclesApi } from '../api/vehicles'
import { storesApi } from '../api/stores'

const EMPTY_PAGE = {
  items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, hasNextPage: false, hasPreviousPage: false,
}

// ── Fin de session : utilisateur et cache vidés ─────────────────────────────

function renderAuth() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  queryClient.setQueryData(['vehicles', 1], { items: ['donnée de l’ancien compte'] })
  let auth: AuthContextType | null = null
  function Probe() {
    auth = useAuth()
    return <div>{auth.user ? `connecté:${auth.user.userId}` : 'déconnecté'}</div>
  }
  render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider><Probe /></AuthProvider>
    </QueryClientProvider>,
  )
  return { queryClient, getAuth: () => auth! }
}

describe('Fin de session', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(authApi, 'me').mockResolvedValue({
      userId: 'u1', firstName: 'Lucas', lastName: 'Moreau', role: 'StoreManager', storeId: 's1',
    })
  })

  it("déconnecte et vide le cache même si l'appel API échoue", async () => {
    vi.spyOn(authApi, 'logout').mockRejectedValue(new Error('réseau indisponible'))
    const { queryClient, getAuth } = renderAuth()
    await screen.findByText('connecté:u1')

    await act(async () => {
      await getAuth().logout().catch(() => { /* l'erreur remonte, la session est fermée quand même */ })
    })

    expect(screen.getByText('déconnecté')).toBeInTheDocument()
    expect(queryClient.getQueryData(['vehicles', 1])).toBeUndefined()
  })

  it('vide le cache quand le serveur met fin à la session (auth:unauthorized)', async () => {
    const { queryClient } = renderAuth()
    await screen.findByText('connecté:u1')

    act(() => { window.dispatchEvent(new CustomEvent('auth:unauthorized')) })

    expect(screen.getByText('déconnecté')).toBeInTheDocument()
    expect(queryClient.getQueryData(['vehicles', 1])).toBeUndefined()
  })
})

// ── Requêtes en attente d'un refresh qui échoue ─────────────────────────────

describe('Rafraîchissement de session refusé', () => {
  const originalAdapter = apiClient.defaults.adapter

  afterEach(() => { apiClient.defaults.adapter = originalAdapter })

  it('rejette les requêtes mises en file au lieu de les laisser suspendues', async () => {
    // Toutes les réponses sont 401 ; le refresh répond après un court délai pour que la 2e requête attende.
    const adapter: AxiosAdapter = async (config: InternalAxiosRequestConfig) => {
      if (config.url?.includes('/auth/refresh'))
        await new Promise(resolve => setTimeout(resolve, 20))
      throw new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401, statusText: 'Unauthorized', headers: {}, config, data: {},
      })
    }
    apiClient.defaults.adapter = adapter
    const unauthorized = vi.fn()
    window.addEventListener('auth:unauthorized', unauthorized)

    const first  = apiClient.get('/vehicles')
    const second = apiClient.get('/stores')

    await expect(first).rejects.toBeInstanceOf(AxiosError)
    // Avant le correctif, cette promesse ne se réglait jamais (le test expirait).
    await expect(second).rejects.toBeInstanceOf(AxiosError)
    expect(unauthorized).toHaveBeenCalledTimes(1)
    window.removeEventListener('auth:unauthorized', unauthorized)
  })
})

// ── Création d'intervention réservée aux gérants ────────────────────────────

function renderInterventions(role: 'Admin' | 'StoreManager' | 'Technician') {
  vi.spyOn(AuthContext, 'useAuth').mockReturnValue({
    user: { userId: 'u1', role, firstName: 'Test', lastName: 'User', storeId: 's1' },
    setUser: vi.fn(),
    logout: vi.fn(),
    loading: false,
  })
  vi.spyOn(interventionsApi, 'getAll').mockResolvedValue(EMPTY_PAGE as never)
  vi.spyOn(vehiclesApi, 'getAll').mockResolvedValue(EMPTY_PAGE as never)
  vi.spyOn(storesApi, 'getAll').mockResolvedValue([] as never)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={client}><Interventions /></QueryClientProvider>)
}

describe('Bouton « Nouvelle intervention »', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('est masqué pour un technicien', async () => {
    renderInterventions('Technician')

    await waitFor(() => expect(interventionsApi.getAll).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: /nouvelle intervention/i })).not.toBeInTheDocument()
  })

  it.each(['Admin', 'StoreManager'] as const)('est affiché pour le rôle %s', async (role) => {
    renderInterventions(role)

    expect(await screen.findByRole('button', { name: /nouvelle intervention/i })).toBeInTheDocument()
  })
})
