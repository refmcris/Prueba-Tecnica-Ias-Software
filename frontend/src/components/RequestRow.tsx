import type { ServiceRequest } from "../types/requests";
import { formatDate } from "../utils/dates";
import { StatusBadge } from "./StatusBadge";

export function RequestRow({ request }: { request: ServiceRequest }) {
  return (
    <tr>
      <td className="reference-cell">
        <strong>{request.externalReference}</strong>
        <span className="row-date">{formatDate(request.createdAt)}</span>
      </td>
      <td>{request.customerId}</td>
      <td>
        <span className="service-code">{request.serviceCode}</span>
      </td>
      <td>
        <StatusBadge status={request.status} />
      </td>
      <td>
        <details className="request-details">
          <summary>Ver detalle</summary>
          <dl>
            <dt>Descripción</dt>
            <dd>{request.description}</dd>
            <dt>Intentos</dt>
            <dd>{request.attemptCount}</dd>
            <dt>Última actualización</dt>
            <dd>{formatDate(request.updatedAt)}</dd>
            <dt>Recibo</dt>
            <dd className="receipt-value">
              {request.providerReceiptId ?? "Sin recibo todavía"}
            </dd>
            {request.lastError && (
              <>
                <dt>Último error</dt>
                <dd className="processing-error">{request.lastError}</dd>
              </>
            )}
          </dl>
        </details>
      </td>
    </tr>
  );
}
