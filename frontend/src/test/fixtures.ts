import type { CreateRequestInput, ServiceRequest } from "../types/requests";

export const validInput: CreateRequestInput = {
  externalReference: "SOL-001",
  customerId: "CLIENTE-001",
  serviceCode: "INSTALACION",
  description: "Instalación en la oficina.",
};

export function requestFixture(
  overrides: Partial<ServiceRequest> = {},
): ServiceRequest {
  return {
    ...validInput,
    id: "11111111-1111-4111-8111-111111111111",
    status: "Pending",
    createdAt: "2026-11-11T12:00:00Z",
    updatedAt: "2026-11-11T12:00:00Z",
    attemptCount: 0,
    lastError: null,
    providerReceiptId: null,
    ...overrides,
  };
}

export function jsonResponse(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

export function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((accept, fail) => {
    resolve = accept;
    reject = fail;
  });
  return { promise, resolve, reject };
}
