import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { Shield, BarChart3, Wrench, Car } from 'lucide-react'
import { login } from '../api/auth'
import LogoIcon from '../components/ui/LogoIcon'
import { useAuth } from '../contexts/AuthContext'
import { loginSchema, type LoginFormValues } from '../schemas/login'
import type { UserRole } from '../types'

const features = [
  { icon: BarChart3, text: 'Tableau de bord en temps réel avec métriques clés' },
  { icon: Car,       text: 'Gestion complète du cycle de vie des véhicules' },
  { icon: Wrench,    text: 'Suivi des interventions et maintenance planifiée' },
  { icon: Shield,    text: 'Accès sécurisé avec gestion des rôles' },
]

export default function Login() {
  const navigate = useNavigate()
  const { setUser } = useAuth()
  const [serverError, setServerError] = useState('')

  const { register, handleSubmit, formState: { errors, isSubmitting }, setValue } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
  })

  const onSubmit = async (data: LoginFormValues) => {
    setServerError('')
    try {
      const res = await login(data)
      setUser({ userId: res.userId, role: res.role as UserRole, storeId: res.storeId, firstName: res.firstName, lastName: res.lastName })
      navigate('/dashboard')
    } catch {
      setServerError('Email ou mot de passe incorrect.')
    }
  }

  return (
    <div className="min-h-screen bg-slate-950 flex">

      {/* ── Left panel ── */}
      <div className="hidden lg:flex w-1/2 flex-col justify-between p-12 relative overflow-hidden"
        style={{ background: 'linear-gradient(135deg, #0f1629 0%, #0c1020 60%, #0a0d18 100%)' }}>

        {/* Dot grid overlay */}
        <div className="absolute inset-0 fm-dot-grid" />

        {/* Glow accent top-right */}
        <div
          className="absolute -top-32 -right-32 w-80 h-80 rounded-full opacity-20 pointer-events-none"
          style={{ background: 'radial-gradient(circle, #4c6ef5 0%, transparent 70%)' }}
        />
        <div
          className="absolute bottom-0 -left-24 w-72 h-72 rounded-full opacity-10 pointer-events-none"
          style={{ background: 'radial-gradient(circle, #6366f1 0%, transparent 70%)' }}
        />

        {/* Logo */}
        <div className="relative flex items-center gap-3">
          <LogoIcon size={36} />
          <span className="text-white font-semibold text-lg tracking-tight">AutoNexus</span>
        </div>

        {/* Center content */}
        <div className="relative space-y-8">
          <div>
            <h2 className="text-3xl font-bold text-white leading-snug tracking-tight">
              Gérez vos véhicules<br />
              <span style={{ color: 'var(--brand-400)' }}>en toute simplicité</span>
            </h2>
            <p className="text-slate-400 text-sm mt-3 leading-relaxed">
              Une plateforme unifiée pour gérer véhicules, interventions et enseignes.
            </p>
          </div>

          {/* Feature list */}
          <ul className="space-y-3.5">
            {features.map(({ icon: Icon, text }) => (
              <li key={text} className="flex items-start gap-3">
                <div
                  className="w-7 h-7 rounded-lg flex items-center justify-center shrink-0 mt-0.5"
                  style={{ background: 'rgba(76,110,245,0.15)', border: '1px solid rgba(76,110,245,0.25)' }}
                >
                  <Icon size={13} style={{ color: 'var(--brand-400)' }} />
                </div>
                <span className="text-sm text-slate-400 leading-relaxed">{text}</span>
              </li>
            ))}
          </ul>
        </div>

        {/* Bottom: testimonial */}
        <div className="relative">
          <div
            className="rounded-xl p-4"
            style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.07)' }}
          >
            <p className="text-slate-400 text-sm leading-relaxed italic">
              "Une solution complète qui nous a permis de réduire nos délais de maintenance de 40 %."
            </p>
            <div className="flex items-center gap-2.5 mt-3">
              <div
                className="w-7 h-7 rounded-full flex items-center justify-center text-[11px] font-bold text-white"
                style={{ background: 'linear-gradient(135deg,#4c6ef5,#7c3aed)' }}
              >
                A
              </div>
              <div>
                <p className="text-xs font-medium text-slate-300">Administrateur</p>
                <p className="text-[11px] text-slate-600">AutoGroup Multi-Enseignes</p>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* ── Right panel (form) ── */}
      <div className="flex-1 flex items-center justify-center p-8">
        <div className="w-full max-w-sm">

          {/* Mobile logo */}
          <div className="flex items-center gap-2.5 lg:hidden mb-8">
            <LogoIcon size={30} />
            <span className="text-white font-semibold">AutoNexus</span>
          </div>

          {/* Heading */}
          <div className="mb-8">
            <h1 className="text-2xl font-bold text-white tracking-tight">Connexion</h1>
            <p className="text-slate-500 text-sm mt-1">Accédez à votre espace de gestion</p>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <label htmlFor="email" className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">
                Email
              </label>
              <input
                {...register('email')}
                id="email"
                type="email"
                className="fm-input-dark"
                placeholder="admin@fleetmanager.fr"
                autoComplete="email"
              />
              {errors.email && (
                <p className="text-red-400 text-xs mt-1">{errors.email.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="password" className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">
                Mot de passe
              </label>
              <input
                {...register('password')}
                id="password"
                type="password"
                className="fm-input-dark"
                placeholder="••••••••"
                autoComplete="current-password"
              />
              {errors.password && (
                <p className="text-red-400 text-xs mt-1">{errors.password.message}</p>
              )}
            </div>

            {serverError && (
              <div
                role="alert"
                className="flex items-center gap-2.5 px-4 py-3 rounded-lg"
                style={{ background: 'rgba(239,68,68,0.1)', border: '1px solid rgba(239,68,68,0.2)' }}
              >
                <div className="w-1.5 h-1.5 rounded-full bg-red-500 shrink-0" />
                <p className="text-red-400 text-sm">{serverError}</p>
              </div>
            )}

            <button
              type="submit"
              disabled={isSubmitting}
              className="fm-btn-primary w-full justify-center mt-2"
            >
              {isSubmitting ? 'Connexion en cours...' : 'Se connecter'}
            </button>
          </form>

          {/* Demo accounts */}
          <div className="mt-8 pt-6" style={{ borderTop: '1px solid rgba(255,255,255,0.06)' }}>
            <p className="text-[11px] font-semibold text-slate-600 uppercase tracking-widest mb-3">
              Comptes de démo
            </p>
            <div className="space-y-2">
              {[
                { role: 'Admin',      email: 'admin@fleetmanager.fr' },
                { role: 'Manager',    email: 'directeur.paris@fleetmanager.fr' },
                { role: 'Technicien', email: 'tech1.paris@fleetmanager.fr' },
              ].map(({ role, email: demoEmail }) => (
                <button
                  key={role}
                  type="button"
                  onClick={() => { setValue('email', demoEmail); setValue('password', 'Fleet@2024') }}
                  className="w-full flex items-center justify-between px-3.5 py-2.5 rounded-lg text-xs transition-colors text-left bg-white/[0.03] hover:bg-white/[0.06] border border-white/[0.06]"
                >
                  <span
                    className="font-medium px-1.5 py-0.5 rounded text-[10px] uppercase tracking-wide"
                    style={{ background: 'rgba(76,110,245,0.15)', color: 'var(--brand-400)' }}
                  >
                    {role}
                  </span>
                  <span className="text-slate-500 font-mono text-[11px]">{demoEmail}</span>
                </button>
              ))}
            </div>
            <p className="text-[11px] text-slate-600 mt-3">
              Mot de passe : <span className="text-slate-400 font-mono">Fleet@2024</span>
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
