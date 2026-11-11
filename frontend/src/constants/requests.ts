import type {
  CreateRequestInput,
  RequestField,
  RequestStatus,
} from "../types/requests";

export const REQUEST_FIELDS: RequestField[] = [
  "externalReference",
  "customerId",
  "serviceCode",
  "description",
];

// Límites técnicos alineados con la API; el enunciado no fija máximos.
export const FIELD_LIMITS: Record<RequestField, number> = {
  externalReference: 128,
  customerId: 100,
  serviceCode: 80,
  description: 2000,
};

export const EMPTY_REQUEST: CreateRequestInput = {
  externalReference: "",
  customerId: "",
  serviceCode: "",
  description: "",
};

export const STATUS_LABELS: Record<RequestStatus, string> = {
  Pending: "Pendiente",
  Processing: "En proceso",
  Completed: "Completada",
  Failed: "Fallida",
};

export const REQUEST_STATUSES = Object.keys(STATUS_LABELS) as RequestStatus[];
