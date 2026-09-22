import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, act, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

// Faux client SignalR : on capture les gestionnaires pour simuler le serveur.
const handlers: Record<string, (payload: unknown) => void> = {}
const connection = {
  state: 'Disconnected',
  start: vi.fn(),
  stop: vi.fn(() => Promise.resolve()),
  on: vi.fn((event: string, cb: (payload: unknown) => void) => { handlers[event] = cb }),
  onreconnecting: vi.fn(),
  onreconnected: vi.fn(),
  onclose: vi.fn(),
}
const withUrl = vi.fn()

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl(url: string, options: unknown) { withUrl(url, options); return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() { return connection }
  },
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  LogLevel: { None: 6 },
}))
vi.mock('../api/auth', () => ({ refresh: vi.fn(() => Promise.resolve({})) }))

const { RealtimeProvider } = await import('../realtime/RealtimeContext')
const { applyChange, hubUrl } = await import('../realtime/realtime')
const { default: RealtimeBadge } = await import('../realtime/RealtimeBadge')

function renderProvider(client = new QueryClient()) {
  render(
    <QueryClientProvider client={client}>
      <RealtimeProvider><RealtimeBadge /></RealtimeProvider>
    </QueryClientProvider>,
  )
  return client
}

describe('temps réel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.keys(handlers).forEach(k => delete handlers[k])
  })

  it('ouvre le hub sous /api/v1 avec les cookies de session', async () => {
    connection.start.mockResolvedValue(undefined)

    renderProvider()

    await waitFor(() => expect(connection.start).toHaveBeenCalled())
    expect(withUrl).toHaveBeenCalledWith(hubUrl(), { withCredentials: true })
    expect(hubUrl()).toMatch(/\/api\/v1\/hubs\/fleet$/)
  })

  it('affiche « En direct » seulement une fois connecté', async () => {
    let resolveStart!: () => void
    connection.start.mockReturnValue(new Promise<void>(r => { resolveStart = r }))

    renderProvider()
    expect(screen.getByRole('status')).toHaveTextContent('Connexion...')

    await act(async () => resolveStart())
    expect(screen.getByRole('status')).toHaveTextContent('En direct')
  })

  it('affiche « Hors ligne » si la connexion échoue', async () => {
    connection.start.mockRejectedValue(new Error('négociation refusée'))

    renderProvider()

    expect(await screen.findByText('Hors ligne')).toBeInTheDocument()
  })

  it('recharge les données concernées à chaque signal du serveur', async () => {
    connection.start.mockResolvedValue(undefined)
    const client = new QueryClient()
    const invalidate = vi.spyOn(client, 'invalidateQueries')

    renderProvider(client)
    await waitFor(() => expect(handlers.changed).toBeDefined())
    act(() => handlers.changed!({ entities: ['interventions'] }))

    const keys = invalidate.mock.calls.map(([filters]) => (filters as { queryKey: string[] }).queryKey[0])
    expect(keys).toEqual(expect.arrayContaining(['interventions', 'dashboard-summary']))
    expect(keys).not.toContain('stores')
  })

  it('ignore un type de données inconnu', () => {
    const client = new QueryClient()
    const invalidate = vi.spyOn(client, 'invalidateQueries')

    applyChange(client, ['inconnu'])

    expect(invalidate).not.toHaveBeenCalled()
  })
})
