import { REQUEST_STATUSES, STATUS_LABELS } from "../constants/requests";
import type { RequestFilter } from "../types/requests";

interface RequestFiltersProps {
  filter: RequestFilter;
  refreshing: boolean;
  onFilterChange: (filter: RequestFilter) => void;
  onRefresh: () => void;
}

export function RequestFilters({
  filter,
  refreshing,
  onFilterChange,
  onRefresh,
}: RequestFiltersProps) {
  return (
    <div className="list-toolbar">
      <div className="filter-control">
        <label htmlFor="status-filter">Filtrar por estado</label>
        <select
          id="status-filter"
          value={filter}
          onChange={(event) =>
            onFilterChange(event.target.value as RequestFilter)
          }
        >
          <option value="All">Todos los estados</option>
          {REQUEST_STATUSES.map((status) => (
            <option key={status} value={status}>
              {STATUS_LABELS[status]}
            </option>
          ))}
        </select>
      </div>
      <button
        type="button"
        className="button button-secondary"
        onClick={onRefresh}
        disabled={refreshing}
      >
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.7"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
          focusable="false"
        >
          <path d="M20 7v5h-5M4 17v-5h5" />
          <path d="M6 7a7 7 0 0 1 11.5-1L20 9M4 15l2.5 3A7 7 0 0 0 18 17" />
        </svg>
        Actualizar
      </button>
    </div>
  );
}
