import { describe, it, expect } from 'vitest'
import { AxiosError, AxiosHeaders } from 'axios'
import { getApiErrorMessage, getArchivedVinConflict } from '../utils/apiError'

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

describe('getArchivedVinConflict', () => {
  it('extrait le véhicule archivé des extensions ProblemDetails', () => {
    const error = axiosErrorWith(409, {
      detail: 'VIN archivé', archivedVehicleId: 'v1', brand: 'Citroën', model: 'C5 X', year: 2022,
      storeName: 'Paris Centre', deletedAt: '2026-09-01T10:00:00',
    })

    expect(getArchivedVinConflict(error)).toEqual({
      archivedVehicleId: 'v1', brand: 'Citroën', model: 'C5 X', year: 2022,
      storeName: 'Paris Centre', deletedAt: '2026-09-01T10:00:00', message: 'VIN archivé',
    })
  })

  it("ignore les conflits sans véhicule restaurable et les autres statuts", () => {
    expect(getArchivedVinConflict(axiosErrorWith(409, { detail: 'Autre enseigne' }))).toBeNull()
    expect(getArchivedVinConflict(axiosErrorWith(400, { archivedVehicleId: 'v1' }))).toBeNull()
    expect(getArchivedVinConflict(new Error('réseau'))).toBeNull()
  })
})
