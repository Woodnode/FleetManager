import { lazy, Suspense, useEffect } from 'react'
import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom'
import { Toaster } from 'react-hot-toast'
import Layout from './components/Layout'
import ProtectedRoute from './components/ProtectedRoute'
import ErrorBoundary from './components/ErrorBoundary'
import Spinner from './components/ui/Spinner'
import { AuthProvider, useAuth } from './contexts/AuthContext'

const Login         = lazy(() => import('./pages/Login'))
const Dashboard     = lazy(() => import('./pages/Dashboard'))
const Vehicles      = lazy(() => import('./pages/Vehicles'))
const Interventions = lazy(() => import('./pages/Interventions'))
const Stores        = lazy(() => import('./pages/Stores'))

function PageLoader() {
  return (
    <div className="min-h-screen flex items-center justify-center">
      <Spinner className="w-8 h-8" />
    </div>
  )
}

function UnauthorizedListener() {
  const navigate = useNavigate()
  const { setUser } = useAuth()

  useEffect(() => {
    const handler = () => {
      setUser(null)
      navigate('/login', { replace: true })
    }
    window.addEventListener('auth:unauthorized', handler)
    return () => window.removeEventListener('auth:unauthorized', handler)
  }, [navigate, setUser])

  return null
}

export default function App() {
  return (
    <ErrorBoundary>
      <AuthProvider>
        <BrowserRouter>
          <Toaster
            position="top-right"
            toastOptions={{
              duration: 3500,
              style: { fontSize: '13px', borderRadius: '10px', boxShadow: '0 4px 24px -4px rgba(0,0,0,0.15)' },
            }}
          />
          <UnauthorizedListener />
          <Suspense fallback={<PageLoader />}>
            <Routes>
              <Route path="/login" element={<Login />} />
              <Route element={<ProtectedRoute />}>
                <Route element={<Layout />}>
                  <Route path="/" element={<Navigate to="/dashboard" replace />} />
                  <Route path="/dashboard"     element={<Dashboard />} />
                  <Route path="/vehicles"      element={<Vehicles />} />
                  <Route path="/interventions" element={<Interventions />} />
                  <Route path="/stores"        element={<Stores />} />
                </Route>
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </Suspense>
        </BrowserRouter>
      </AuthProvider>
    </ErrorBoundary>
  )
}
