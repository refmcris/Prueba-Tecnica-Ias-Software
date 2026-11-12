import { REQUEST_FIELDS, REQUEST_STATUSES } from "../constants/requests";
import type {
  CreateRequestInput,
  CreateRequestResult,
  FieldErrors,
  RequestFilter,
  ServiceRequest,
} from "../types/requests";

const endpoint = "/api/requests";

export class ApiError extends Error {
  constructor(
    message: string,
    readonly fieldErrors: FieldErrors = {},
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? error.message : fallback;
}

async function send(
  path: string,
  options: RequestInit,
  networkMessage: string,
): Promise<Response> {
  try {
    return await fetch(path, {
      ...options,
      headers: { Accept: "application/json", ...options.headers },
    });
  } catch (error) {
    if (options.signal?.aborted) {
      throw error;
    }

    throw new ApiError(networkMessage);
  }
}

async function responseError(response: Response): Promise<ApiError> {
  const problem: unknown = await response.json().catch(() => null);
  const fieldErrors: FieldErrors = {};

  if (problem !== null && typeof problem === "object" && "errors" in problem) {
    const errors = problem.errors;

    if (errors !== null && typeof errors === "object") {
      for (const field of REQUEST_FIELDS) {
        if (field in errors) {
          const messages: unknown = (errors as Record<string, unknown>)[field];

          if (Array.isArray(messages) && typeof messages[0] === "string") {
            fieldErrors[field] = messages[0];
          }
        }
      }
    }
  }

  if (response.status === 400) {
    return new ApiError("Revisa los datos de la solicitud.", fieldErrors);
  }

  if (response.status === 409) {
    return new ApiError(
      "Ya existe una solicitud con ese cliente y referencia, pero con datos diferentes. Usa otra referencia para una operación nueva.",
    );
  }

  return new ApiError(
    "No fue posible completar la operación. Intenta nuevamente.",
  );
}

async function readJson<T>(response: Response): Promise<T> {
  try {
    return (await response.json()) as T;
  } catch {
    throw new ApiError(
      "No fue posible confirmar la respuesta. Intenta nuevamente.",
    );
  }
}

function isRequestResponse(value: unknown): value is ServiceRequest {
  if (!value || typeof value !== "object") {
    return false;
  }

  const request = value as Record<string, unknown>;
  const stringFields = [
    "id",
    "externalReference",
    "customerId",
    "serviceCode",
    "description",
    "createdAt",
    "updatedAt",
  ];

  return (
    stringFields.every((field) => typeof request[field] === "string") &&
    REQUEST_STATUSES.some((status) => status === request.status) &&
    Number.isInteger(request.attemptCount) &&
    Number(request.attemptCount) >= 0 &&
    (request.lastError === null || typeof request.lastError === "string") &&
    (request.providerReceiptId === null ||
      typeof request.providerReceiptId === "string")
  );
}
export async function listRequests(
  filter: RequestFilter,
  signal?: AbortSignal,
): Promise<ServiceRequest[]> {
  const query =
    filter === "All" ? "" : `?${new URLSearchParams({ status: filter })}`;
  const response = await send(
    endpoint + query,
    { signal },
    "No fue posible actualizar las solicitudes. Intenta nuevamente.",
  );

  if (!response.ok) {
    throw await responseError(response);
  }

  const requests = await readJson<unknown>(response);

  if (!Array.isArray(requests) || !requests.every(isRequestResponse)) {
    throw new ApiError(
      "No fue posible actualizar las solicitudes. Intenta nuevamente.",
    );
  }

  return requests;
}

export async function createRequest(
  input: CreateRequestInput,
  signal?: AbortSignal,
): Promise<CreateRequestResult> {
  const response = await send(
    endpoint,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
      signal,
    },
    "No fue posible confirmar el registro. Puedes volver a enviarlo con la misma referencia.",
  );

  if (!response.ok) {
    throw await responseError(response);
  }

  const request = await readJson<unknown>(response);

  if (!isRequestResponse(request)) {
    throw new ApiError(
      "No fue posible confirmar el registro. Puedes volver a enviarlo con la misma referencia.",
    );
  }

  return { request, created: response.status === 201 };
}
