import type { RequestFilter, ServiceRequest } from "../types/requests";
import { RequestRow } from "./RequestRow";

interface RequestTableProps {
  requests: ServiceRequest[];
  loading: boolean;
  hasError: boolean;
  filter: RequestFilter;
}

export function RequestTable({
  requests,
  loading,
  hasError,
  filter,
}: RequestTableProps) {
  if (loading) {
    return (
      <div className="list-placeholder" role="status">
        Cargando solicitudes…
      </div>
    );
  }

  if (requests.length === 0) {
    return (
      <div className="list-placeholder empty-state">
        <h3>
          {hasError ? "El listado no está disponible" : "No hay solicitudes"}
        </h3>
        <p>
          {hasError
            ? "Vuelve a intentarlo con el botón Actualizar."
            : filter === "All"
              ? "Crea una solicitud desde el formulario."
              : "No se encontraron solicitudes en este estado."}
        </p>
      </div>
    );
  }

  return (
    <div
      className="table-container"
      role="region"
      aria-label="Listado de solicitudes"
      aria-describedby="table-scroll-hint"
      tabIndex={0}
    >
      <p id="table-scroll-hint" className="table-scroll-hint">
        Desplaza la tabla para ver todos los campos.
      </p>
      <table>
        <caption className="sr-only">
          Solicitudes de servicio y su estado actual
        </caption>
        <thead>
          <tr>
            <th scope="col">Referencia</th>
            <th scope="col">Cliente</th>
            <th scope="col">Servicio</th>
            <th scope="col">Estado</th>
            <th scope="col">Seguimiento</th>
          </tr>
        </thead>
        <tbody>
          {requests.map((request) => (
            <RequestRow key={request.id} request={request} />
          ))}
        </tbody>
      </table>
    </div>
  );
}
