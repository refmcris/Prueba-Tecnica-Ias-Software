import { FIELD_LIMITS, REQUEST_FIELDS } from "../constants/requests";
import type { CreateRequestInput, FieldErrors } from "../types/requests";

export function normalizeRequest(
  input: CreateRequestInput,
): CreateRequestInput {
  return {
    externalReference: input.externalReference.trim(),
    customerId: input.customerId.trim(),
    serviceCode: input.serviceCode.trim(),
    description: input.description.trim(),
  };
}

export function validateRequest(input: CreateRequestInput): FieldErrors {
  const errors: FieldErrors = {};

  for (const field of REQUEST_FIELDS) {
    if (input[field].length === 0) {
      errors[field] = "Este campo es obligatorio.";
    } else if (input[field].length > FIELD_LIMITS[field]) {
      errors[field] = `Máximo ${FIELD_LIMITS[field]} caracteres.`;
    }
  }

  return errors;
}
