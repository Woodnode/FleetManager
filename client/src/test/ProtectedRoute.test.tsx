import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import ProtectedRoute from '../components/ProtectedRoute'
import * as AuthContext from '../contexts/AuthContext'

// Mock useAuth to control auth state in tests
const mockUseAuth = vi.spyOn(AuthContext, 'useAuth')

function renderRoute(user: AuthContext.AuthContextType['user'], loading = false) {
  mockUseAuth.mockReturnValue({
    user,
    setUser: vi.fn(),
    loading,
  })

  return render(
    <MemoryRouter initialEntries={['/protected']}>
      <Routes>
        <Route element={<ProtectedRoute />}>
          <Route path="/protected" element={<div>Contenu protégé</div>} />
        </Route>
        <Route path="/login" element={<div>Page de connexion</div>} />
      </Routes>
    </MemoryRouter>
  )
}

describe('ProtectedRoute', () => {
  beforeEach(() => { vi.clearAllMocks() })

  it('redirige vers /login quand non authentifié', () => {
    renderRoute(null)
    expect(screen.getByText('Page de connexion')).toBeInTheDocument()
    expect(screen.queryByText('Contenu protégé')).not.toBeInTheDocument()
  })

  it('affiche le contenu quand authentifié', () => {
    renderRoute({ userId: '1', role: 'Admin', storeId: null })
    expect(screen.getByText('Contenu protégé')).toBeInTheDocument()
    expect(screen.queryByText('Page de connexion')).not.toBeInTheDocument()
  })

  it('affiche un spinner pendant le chargement', () => {
    mockUseAuth.mockReturnValue({ user: null, setUser: vi.fn(), loading: true })
    render(
      <MemoryRouter initialEntries={['/protected']}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route path="/protected" element={<div>Contenu protégé</div>} />
          </Route>
          <Route path="/login" element={<div>Page de connexion</div>} />
        </Routes>
      </MemoryRouter>
    )
    expect(screen.queryByText('Contenu protégé')).not.toBeInTheDocument()
    expect(screen.queryByText('Page de connexion')).not.toBeInTheDocument()
  })
})
