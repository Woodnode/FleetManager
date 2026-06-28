import { z } from 'zod'

export const createStoreSchema = z.object({
  name:       z.string().min(1, 'Le nom est requis').max(100, 'Le nom est trop long'),
  address:    z.string().max(200, "L'adresse est trop longue"),
  postalCode: z.string().max(10, 'Le code postal est trop long'),
  city:       z.string().min(1, 'La ville est requise').max(100, 'La ville est trop longue'),
})

export type CreateStoreFormValues = z.infer<typeof createStoreSchema>
