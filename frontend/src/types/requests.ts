export type RequestStatus = "Pending" | "Processing" | "Completed" | "Failed";

export type RequestFilter = RequestStatus | "All";

export interface CreateRequestInput {
  externalReference: string;
  customerId: string;
  serviceCode: string;
  description: string;
}

export type RequestField = keyof CreateRequestInput;

export type FieldErrors = Partial<Record<RequestField, string>>;

export interface ServiceRequest extends CreateRequestInput {
  id: string;
  status: RequestStatus;
  createdAt: string;
  updatedAt: string;
  attemptCount: number;
  lastError: string | null;
  providerReceiptId: string | null;
}

export interface CreateRequestResult {
  request: ServiceRequest;
  created: boolean;
}
