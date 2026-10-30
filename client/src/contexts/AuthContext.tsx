import { createContext, useCallback, useContext, useEffect, useState, useMemo, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { me, logout as apiLogout } from '../api/auth'
import type { UserRole } from '../types'

interface AuthUser {
  userId: string
  role: UserRole
  storeId: string | null
  firstName: string | null
  lastName: string | null
}

export interface AuthContextType {
  user: AuthUser | null
  setUser: (u: AuthUser | null) => void
  logout: () => Promise<void>
  loading: boolean
}

const AuthContext = createContext<AuthContextType | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)
  const queryClient = useQueryClient()

  useEffect(() => {
    let mounted = true
    me()
      .then(data => {
        if (!mounted) return
        if (data)
          setUser({
            userId: data.userId,
            role: data.role as UserRole,
            storeId: data.storeId,
            firstName: data.firstName,
            lastName: data.lastName,
          })
        else setUser(null)
      })
      .finally(() => { if (mounted) setLoading(false) })
    return () => { mounted = false }
  }, [])

  // Session terminée par le serveur (refresh refusé) : les données en cache appartiennent à l'ancien compte.
  useEffect(() => {
    const handler = () => {
      queryClient.clear()
      setUser(null)
    }
    window.addEventListener('auth:unauthorized', handler)
    return () => window.removeEventListener('auth:unauthorized', handler)
  }, [queryClient])

  // La session locale est fermée et le cache vidé même si l'appel échoue : sinon l'onglet garde
  // l'utilisateur, la requête suivante relancerait la session par le cookie de rafraîchissement,
  // et le compte suivant sur ce navigateur verrait les données de celui-ci.
  const logout = useCallback(async () => {
    try {
      await apiLogout()
    } finally {
      queryClient.clear()
      setUser(null)
    }
  }, [queryClient])

  const value = useMemo(() => ({ user, setUser, logout, loading }), [user, logout, loading])

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextType {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
