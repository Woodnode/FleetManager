import { useEffect, useState } from 'react'
import { Outlet, NavLink, useLocation, useNavigate } from 'react-router-dom'
import { LayoutDashboard, Car, Wrench, Building2, Archive, LogOut, Menu, X } from 'lucide-react'
import { useAuth } from '../contexts/AuthContext'
import LogoIcon from './ui/LogoIcon'
import { isManagerOrAdminRole } from '../utils/auth'
import { useMediaQuery } from '../hooks/useMediaQuery'
import { useScrollLock } from '../hooks/useScrollLock'
import { RealtimeProvider } from '../realtime/RealtimeContext'

const nav = [
  { to: '/dashboard',     label: 'Dashboard',     icon: LayoutDashboard },
  { to: '/vehicles',      label: 'Véhicules',      icon: Car },
  { to: '/interventions', label: 'Interventions',  icon: Wrench },
  { to: '/stores',        label: 'Enseignes',      icon: Building2 },
  // Archives : réservées aux rôles qui peuvent supprimer un véhicule (l'API applique la même règle)
  { to: '/archives',      label: 'Archives',       icon: Archive, managerOnly: true },
]

function Brand() {
  return (
    <div className="flex items-center gap-3 min-w-0">
      <LogoIcon size={32} />
      <div className="min-w-0">
        <p className="text-sm font-semibold text-white tracking-tight leading-tight">AutoNexus</p>
        <p className="text-[11px] text-slate-400 mt-0.5">Gestion du parc auto</p>
      </div>
    </div>
  )
}

export default function Layout() {
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const { user, logout } = useAuth()
  // Le tiroir retient la page où il a été ouvert : naviguer ailleurs le referme, sans effet.
  const [openedAt, setOpenedAt] = useState<string | null>(null)
  const drawerOpen = openedAt === pathname
  const setDrawerOpen = (open: boolean) => setOpenedAt(open ? pathname : null)
  // Sous 1024px, la barre latérale devient un tiroir : à 768px elle laissait moins de 530px au contenu.
  const isDesktop = useMediaQuery('(min-width: 1024px)', true)
  const drawerActive = drawerOpen && !isDesktop
  useScrollLock(drawerActive)

  useEffect(() => {
    if (!drawerActive) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpenedAt(null) }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [drawerActive])

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
    <div className="min-h-dvh lg:flex" style={{ background: 'var(--surface-page)' }}>
      {/* ── Barre supérieure (mobile et tablette) ── */}
      <header
        className="fm-sidebar lg:hidden sticky top-0 z-30 flex items-center justify-between gap-3 pl-4 pr-2 h-14 pt-[env(safe-area-inset-top)] box-content"
        style={{ borderBottom: '1px solid var(--sidebar-border)' }}
      >
        <Brand />
        <button
          onClick={() => setDrawerOpen(true)}
          aria-label="Ouvrir le menu"
          aria-expanded={drawerActive}
          aria-controls="fm-sidebar"
          className="fm-icon-btn w-11 h-11 text-slate-300 hover:text-white hover:bg-white/10"
        >
          <Menu size={20} />
        </button>
      </header>

      {/* Voile derrière le tiroir */}
      {drawerActive && (
        <div className="lg:hidden fixed inset-0 z-40 bg-black/50 backdrop-blur-[2px]" onClick={() => setDrawerOpen(false)} />
      )}

      {/* ── Sidebar (tiroir sous lg) ── */}
      <aside
        id="fm-sidebar"
        aria-label="Menu"
        className={`fm-sidebar w-72 max-w-[85vw] lg:w-60 flex flex-col shrink-0
          fixed inset-y-0 left-0 z-50 transition-transform duration-300 ease-[cubic-bezier(0.16,1,0.3,1)]
          lg:sticky lg:top-0 lg:h-dvh lg:translate-x-0 lg:z-auto
          ${drawerActive ? 'translate-x-0 shadow-2xl' : '-translate-x-full'}`}
        {...(!isDesktop && !drawerOpen ? { inert: true } : {})}
      >
        {/* Logo */}
        <div className="flex items-center justify-between gap-2 pl-5 pr-3 pt-[calc(1.5rem+env(safe-area-inset-top))] lg:pt-6 pb-5"
          style={{ borderBottom: '1px solid var(--sidebar-border)' }}>
          <Brand />
          <button
            onClick={() => setDrawerOpen(false)}
            aria-label="Fermer le menu"
            className="lg:hidden fm-icon-btn w-11 h-11 text-slate-400 hover:text-white hover:bg-white/10"
          >
            <X size={18} />
          </button>
        </div>

        {/* Navigation */}
        <nav aria-label="Navigation principale" className="flex-1 overflow-y-auto px-3 py-4 space-y-0.5">
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
        <div className="px-3 pt-4 pb-[calc(1rem+env(safe-area-inset-bottom))]" style={{ borderTop: '1px solid var(--sidebar-border)' }}>
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
            className="flex items-center gap-2.5 w-full px-3 py-2 pointer-coarse:py-3 text-sm text-slate-400 hover:text-slate-100 hover:bg-white/5 rounded-lg transition-all duration-150"
          >
            <LogOut size={14} />
            Déconnexion
          </button>
        </div>
      </aside>

      {/* ── Main content ── */}
      <main className="flex-1 min-w-0">
        {/* Temps réel ouvert seulement dans la zone connectée, fermé à la déconnexion (démontage). */}
        <RealtimeProvider>
          <Outlet />
        </RealtimeProvider>
      </main>
    </div>
  )
}
