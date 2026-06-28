import { createContext, useCallback, useContext, useEffect, useState, useMemo, type ReactNode } from 'react'
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

  const logout = useCallback(async () => {
    await apiLogout()
    setUser(null)
  }, [])

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
