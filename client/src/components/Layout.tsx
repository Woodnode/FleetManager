import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { LayoutDashboard, Car, Wrench, Building2, Archive, LogOut } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import LogoIcon from './ui/LogoIcon'
import { isManagerOrAdminRole } from '../utils/auth'

const nav = [
  { to: '/dashboard',     label: 'Dashboard',     icon: LayoutDashboard },
  { to: '/vehicles',      label: 'Véhicules',      icon: Car },
  { to: '/interventions', label: 'Interventions',  icon: Wrench },
  { to: '/stores',        label: 'Enseignes',      icon: Building2 },
  // Archives : réservées aux rôles qui peuvent supprimer un véhicule (l'API applique la même règle)
  { to: '/archives',      label: 'Archives',       icon: Archive, managerOnly: true },
]

export default function Layout() {
  const navigate = useNavigate()
  const { user, logout } = useAuth()

  const handleLogout = async () => {
    try { await logout() } catch { /* ignore */ }
    navigate('/login')
  }

  const role = user?.role ?? ''
  const displayName = user?.firstName && user?.lastName
    ? `${user.firstName} ${user.lastName}`
    : role
  const initials = user?.firstName && user?.lastName
    ? `${user.firstName[0]}${user.lastName[0]}`.toUpperCase()
    : role.slice(0, 2).toUpperCase()

  return (
    <div className="flex h-screen overflow-hidden" style={{ background: 'var(--surface-page)' }}>
      {/* ── Sidebar ── */}
      <aside className="fm-sidebar w-60 flex flex-col shrink-0">

        {/* Logo */}
        <div className="px-5 pt-6 pb-5" style={{ borderBottom: '1px solid var(--sidebar-border)' }}>
          <div className="flex items-center gap-3">
            <LogoIcon size={32} />
            <div className="min-w-0">
              <p className="text-sm font-semibold text-white tracking-tight leading-tight">AutoNexus</p>
              <p className="text-[11px] text-slate-400 mt-0.5">Gestion du parc auto</p>
            </div>
          </div>
        </div>

        {/* Navigation */}
        <nav aria-label="Navigation principale" className="flex-1 px-3 py-4 space-y-0.5">
          {nav.filter(item => !item.managerOnly || isManagerOrAdminRole(user?.role)).map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) => `fm-nav-link${isActive ? ' active' : ''}`}
            >
              <Icon size={15} />
              {label}
            </NavLink>
          ))}
        </nav>

        {/* User section */}
        <div className="px-3 py-4" style={{ borderTop: '1px solid var(--sidebar-border)' }}>
          <div
            className="flex items-center gap-3 px-3 py-2.5 rounded-xl mb-1"
            style={{ background: 'rgba(255, 255, 255, 0.03)' }}
          >
            <div
              className="w-7 h-7 rounded-full flex items-center justify-center shrink-0 text-[11px] font-bold text-white"
              style={{ background: 'linear-gradient(135deg, #4c6ef5, #7c3aed)' }}
            >
              {initials}
            </div>
            <div className="min-w-0">
              <p className="text-xs font-medium text-slate-300 truncate">{displayName}</p>
              <p className="text-[11px] text-slate-400">{user?.storeId ? 'En enseigne' : 'Admin global'}</p>
            </div>
          </div>

          <button
            onClick={handleLogout}
            className="flex items-center gap-2.5 w-full px-3 py-2 text-sm text-slate-400 hover:text-slate-100 hover:bg-white/5 rounded-lg transition-all duration-150"
          >
            <LogOut size={14} />
            Déconnexion
          </button>
        </div>
      </aside>

      {/* ── Main content ── */}
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  )
}
