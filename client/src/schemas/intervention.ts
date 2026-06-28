import { z } from 'zod'

export const createInterventionSchema = z.object({
  vehicleId:        z.string().min(1, 'Veuillez sélectionner un véhicule'),
  storeId:          z.string().min(1, 'Veuillez sélectionner une enseigne'),
  technicianId:     z.string().min(1, 'Veuillez sélectionner un technicien'),
  type:             z.enum(['Maintenance', 'Repair', 'Inspection', 'Other'] as const),
  plannedStartDate: z.string().min(1, 'Veuillez renseigner la date de début'),
  plannedEndDate:   z.string().min(1, 'Veuillez renseigner la date de fin'),
  comment:          z.string().optional(),
}).refine(
  d => !d.plannedStartDate || !d.plannedEndDate || d.plannedEndDate > d.plannedStartDate,
  { message: 'La date de fin doit être après la date de début', path: ['plannedEndDate'] }
)

export type CreateInterventionFormValues = z.infer<typeof createInterventionSchema>
