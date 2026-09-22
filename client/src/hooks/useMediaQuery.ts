import { useSyncExternalStore } from 'react'

/** Suit une media query CSS. Sans matchMedia (tests jsdom, SSR), renvoie `fallback`. */
export function useMediaQuery(query: string, fallback = false): boolean {
  return useSyncExternalStore(
    onChange => {
      if (typeof window.matchMedia !== 'function') return () => {}
      const mql = window.matchMedia(query)
      mql.addEventListener('change', onChange)
      return () => mql.removeEventListener('change', onChange)
    },
    () => (typeof window.matchMedia === 'function' ? window.matchMedia(query).matches : fallback),
    () => fallback,
  )
}

/** Sous 768px (Tailwind `md`), les tableaux laissent place à des listes de cartes. */
export const useIsMobile = () => useMediaQuery('(max-width: 767px)')
