import { z } from 'zod'

const MAX_YEAR = new Date().getFullYear() + 1

export const createVehicleSchema = z.object({
  vin: z
    .string()
    .length(17, 'Le VIN doit faire exactement 17 caractères')
    .regex(/^[A-HJ-NPR-Z0-9]{17}$/i, 'Le VIN ne peut contenir que des lettres et chiffres (sauf I, O, Q)'),
  brand: z.string().min(1, 'Marque requise').max(100, 'La marque ne peut pas dépasser 100 caractères'),
  model: z.string().min(1, 'Modèle requis').max(100, 'Le modèle ne peut pas dépasser 100 caractères'),
  year: z.number().min(1990, 'Année minimum: 1990').max(MAX_YEAR, `Année maximum: ${MAX_YEAR}`),
  mileage: z.number().min(0, 'Kilométrage invalide'),
  storeId: z.string().uuid('Veuillez sélectionner une enseigne'),
})

export const updateVehicleSchema = z.object({
  brand: z.string().min(1, 'Marque requise').max(100, 'La marque ne peut pas dépasser 100 caractères'),
  model: z.string().min(1, 'Modèle requis').max(100, 'Le modèle ne peut pas dépasser 100 caractères'),
  year: z.number().min(1990, 'Année minimum: 1990').max(MAX_YEAR, `Année maximum: ${MAX_YEAR}`),
  mileage: z.number().min(0, 'Kilométrage invalide'),
  storeId: z.string().uuid('Veuillez sélectionner une enseigne'),
})

export type CreateVehicleFormValues = z.infer<typeof createVehicleSchema>
export type UpdateVehicleFormValues = z.infer<typeof updateVehicleSchema>
