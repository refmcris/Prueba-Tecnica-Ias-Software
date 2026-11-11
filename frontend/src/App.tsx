import { useState } from "react";
import { FeedbackMessage } from "./components/FeedbackMessage";
import { RequestFilters } from "./components/RequestFilters";
import { RequestForm } from "./components/RequestForm";
import { RequestTable } from "./components/RequestTable";
import { useRequests } from "./hooks/useRequests";
import type { RequestFilter } from "./types/requests";
import { formatTime } from "./utils/dates";

export default function App() {
  const [filter, setFilter] = useState<RequestFilter>("All");
  const list = useRequests(filter);

  function handleSaved() {
    setFilter("All");
    list.refresh();
  }

  const updateLabel = list.error
    ? "Actualización pendiente"
    : list.refreshing
      ? "Actualizando solicitudes…"
      : list.updatedAt
        ? `Última actualización: ${formatTime(list.updatedAt)}`
        : "Esperando actualización";

  return (
    <main className="page-shell">
      <h1>Solicitudes de servicio</h1>
      <div className="workspace-grid">
        <RequestForm onSaved={handleSaved} />
        <section className="panel list-panel" aria-labelledby="list-title">
          <div className="list-heading">
            <h2 id="list-title">Solicitudes registradas</h2>
            <span className="request-count">
              {list.requests.length}{" "}
              {list.requests.length === 1 ? "solicitud" : "solicitudes"}
            </span>
          </div>
          <RequestFilters
            filter={filter}
            refreshing={list.refreshing}
            onFilterChange={setFilter}
            onRefresh={list.refresh}
          />
          {list.error && (
            <div className="list-feedback">
              <FeedbackMessage
                feedback={{ tone: "error", message: list.error }}
              />
            </div>
          )}
          <RequestTable
            requests={list.requests}
            loading={list.loading}
            hasError={Boolean(list.error)}
            filter={filter}
          />
          <div className="list-footer">{updateLabel}</div>
        </section>
      </div>
    </main>
  );
}
