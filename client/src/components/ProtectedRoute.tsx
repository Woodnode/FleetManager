import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import Spinner from './ui/Spinner'

export default function ProtectedRoute() {
  const { user, loading } = useAuth()

  if (loading) {
    return (
      <div className="flex h-screen items-center justify-center" style={{ background: 'var(--surface-page)' }}>
        <Spinner className="w-7 h-7" />
      </div>
    )
  }

  return user ? <Outlet /> : <Navigate to="/login" replace />
}
