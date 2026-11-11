import { STATUS_LABELS } from "../constants/requests";
import type { RequestStatus } from "../types/requests";

export function StatusBadge({ status }: { status: RequestStatus }) {
  return (
    <span className={`status-badge status-${status.toLowerCase()}`}>
      {STATUS_LABELS[status] ?? "Estado desconocido"}
    </span>
  );
}
