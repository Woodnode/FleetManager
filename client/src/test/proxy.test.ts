import { describe, it, expect } from 'vitest'
import { buildTargetUrl, forwardClientIp, isWebSocketUpgrade } from '../../functions/_proxy'

const ORIGIN = 'https://fleet.203-0-113-10.sslip.io'

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
    expect(() => buildTargetUrl('https://fleet-julien.pages.dev/api/v1/vehicles', 'http://203.0.113.10'))
      .toThrow(/https/)
  })

  it('ne se laisse pas détourner vers un autre domaine par le chemin demandé', () => {
    const target = buildTargetUrl('https://fleet-julien.pages.dev/api/v1/../../etc', ORIGIN)

    expect(new URL(target).host).toBe('fleet.203-0-113-10.sslip.io')
  })

  it('détecte une requête WebSocket', () => {
    expect(isWebSocketUpgrade(new Headers({ Upgrade: 'WebSocket' }))).toBe(true)
    expect(isWebSocketUpgrade(new Headers())).toBe(false)
  })

  it("transmet l'IP du visiteur en écrasant celle qu'il aurait envoyée lui-même", () => {
    const headers = new Headers({ 'X-Real-Client-IP': '1.2.3.4' })
    forwardClientIp(headers, new Headers({ 'CF-Connecting-IP': '198.51.100.7' }))
    expect(headers.get('X-Real-Client-IP')).toBe('198.51.100.7')

    forwardClientIp(headers, new Headers())
    expect(headers.has('X-Real-Client-IP')).toBe(false)
  })
})
