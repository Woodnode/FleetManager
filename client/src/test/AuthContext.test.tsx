import { describe, it, expect, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AuthProvider, useAuth } from '../contexts/AuthContext'
import * as authApi from '../api/auth'

vi.spyOn(authApi, 'me').mockResolvedValue({
  userId: 'u1', firstName: 'Thomas', lastName: 'Dupont', role: 'StoreManager', storeId: 's1',
})

function Probe() {
  const { user, logout } = useAuth()
  return (
    <div>
      <span>{user ? `connecté : ${user.firstName}` : 'déconnecté'}</span>
      <button onClick={() => { logout().catch(() => { /* erreur réseau attendue */ }) }}>Déconnexion</button>
    </div>
  )
}

describe('AuthContext', () => {
  // Un échec de l'appel (jeton expiré, réseau coupé) laissait l'utilisateur dans l'onglet : la
  // requête suivante relançait la session par le cookie de rafraîchissement.
  it('ferme la session locale même si la déconnexion côté serveur échoue', async () => {
    vi.spyOn(authApi, 'logout').mockRejectedValue(new Error('401'))
    render(<AuthProvider><Probe /></AuthProvider>)
    await screen.findByText('connecté : Thomas')

    await userEvent.click(screen.getByRole('button', { name: 'Déconnexion' }))

    await waitFor(() => expect(screen.getByText('déconnecté')).toBeInTheDocument())
  })
})
