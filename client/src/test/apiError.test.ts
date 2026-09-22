import { describe, it, expect } from 'vitest'
import { AxiosError, AxiosHeaders } from 'axios'
import { getApiErrorMessage } from '../utils/apiError'

function axiosErrorWith(status: number, data: unknown) {
  const config = { headers: new AxiosHeaders() }
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', config, null, {
    status, statusText: '', headers: {}, config, data,
  })
}

describe('getApiErrorMessage', () => {
  it('retourne le champ detail des ProblemDetails', () => {
    const error = axiosErrorWith(409, { title: 'Conflict.', status: 409, detail: 'Véhicule en intervention.' })

    expect(getApiErrorMessage(error, 'Erreur')).toBe('Véhicule en intervention.')
  })

  it('retombe sur le message générique sans detail exploitable', () => {
    expect(getApiErrorMessage(axiosErrorWith(500, undefined), 'Erreur')).toBe('Erreur')
    expect(getApiErrorMessage(axiosErrorWith(400, { detail: '   ' }), 'Erreur')).toBe('Erreur')
    expect(getApiErrorMessage(new Error('réseau'), 'Erreur')).toBe('Erreur')
  })
})
