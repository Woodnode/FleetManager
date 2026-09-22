import { describe, it, expect } from 'vitest'
import { buildTargetUrl, isWebSocketUpgrade } from '../../functions/_proxy'

const ORIGIN = 'https://fleet.141-148-52-17.sslip.io'

describe('relais /api vers le serveur', () => {
  it('conserve le chemin et les paramètres', () => {
    expect(buildTargetUrl('https://fleet-julien.pages.dev/api/v1/vehicles?page=2&search=clio', ORIGIN))
      .toBe(`${ORIGIN}/api/v1/vehicles?page=2&search=clio`)
  })

  it('transmet aussi le hub temps réel', () => {
    expect(buildTargetUrl('https://fleet-julien.pages.dev/api/v1/hubs/fleet', ORIGIN))
      .toBe(`${ORIGIN}/api/v1/hubs/fleet`)
  })

  it('refuse une origine non chiffrée (les cookies de session y transiteraient en clair)', () => {
    expect(() => buildTargetUrl('https://fleet-julien.pages.dev/api/v1/vehicles', 'http://141.148.52.17'))
      .toThrow(/https/)
  })

  it('ne se laisse pas détourner vers un autre domaine par le chemin demandé', () => {
    const target = buildTargetUrl('https://fleet-julien.pages.dev/api/v1/../../etc', ORIGIN)

    expect(new URL(target).host).toBe('fleet.141-148-52-17.sslip.io')
  })

  it('détecte une requête WebSocket', () => {
    expect(isWebSocketUpgrade(new Headers({ Upgrade: 'WebSocket' }))).toBe(true)
    expect(isWebSocketUpgrade(new Headers())).toBe(false)
  })
})
