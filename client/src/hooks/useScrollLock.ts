import { useEffect } from 'react'

let locks = 0

/**
 * Bloque le défilement de la page tant que `active` est vrai (modale, tiroir de navigation).
 * Compteur partagé : une modale ouverte depuis le tiroir ne débloque pas la page en se fermant.
 */
export function useScrollLock(active: boolean) {
  useEffect(() => {
    if (!active) return
    locks += 1
    document.body.classList.add('fm-scroll-lock')
    return () => {
      locks -= 1
      if (locks === 0) document.body.classList.remove('fm-scroll-lock')
    }
  }, [active])
}
