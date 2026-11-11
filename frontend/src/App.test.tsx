import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";
import { jsonResponse, requestFixture, validInput } from "./test/fixtures";
import type { ServiceRequest } from "./types/requests";

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

function serveRequests(initial: ServiceRequest[]) {
  let requests = initial;
  fetchMock.mockImplementation(async (path, options) => {
    if (options?.method === "POST") {
      const request = requestFixture();
      requests = [request, ...requests];
      return jsonResponse(request, 201);
    }
    const status = new URL(String(path), "http://localhost").searchParams.get(
      "status",
    );
    return jsonResponse(
      status
        ? requests.filter((request) => request.status === status)
        : requests,
    );
  });
}

describe("Portal de solicitudes", () => {
  it("presenta el formulario y un listado vacío comprensible", async () => {
    serveRequests([]);
    render(<App />);

    expect(await screen.findByText("No hay solicitudes")).toBeVisible();
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
      "Solicitudes de servicio",
    );
    expect(screen.getAllByRole("textbox")).toHaveLength(4);
    expect(
      screen.getByText("Crea una solicitud desde el formulario."),
    ).toBeVisible();
  });

  it("muestra estados en español y filtra mediante el contrato de la API", async () => {
    const user = userEvent.setup();
    serveRequests([
      requestFixture({ externalReference: "SOL-PENDIENTE" }),
      requestFixture({
        id: "22222222-2222-4222-8222-222222222222",
        externalReference: "SOL-COMPLETADA",
        status: "Completed",
      }),
    ]);
    render(<App />);
    await screen.findByText("SOL-PENDIENTE");
    expect(
      within(screen.getByRole("table")).getByText("Pendiente"),
    ).toBeVisible();

    await user.selectOptions(
      screen.getByRole("combobox", { name: "Filtrar por estado" }),
      "Completed",
    );

    await waitFor(() =>
      expect(screen.queryByText("SOL-PENDIENTE")).not.toBeInTheDocument(),
    );
    expect(await screen.findByText("SOL-COMPLETADA")).toBeVisible();
    expect(
      within(screen.getByRole("table")).getByText("Completada"),
    ).toBeVisible();
    expect(fetchMock.mock.calls.at(-1)?.[0]).toBe(
      "/api/requests?status=Completed",
    );
  });

  it("al registrar vuelve a todos los estados para mostrar la solicitud nueva", async () => {
    const user = userEvent.setup();
    serveRequests([
      requestFixture({
        id: "22222222-2222-4222-8222-222222222222",
        externalReference: "SOL-ANTERIOR",
        status: "Completed",
      }),
    ]);
    render(<App />);
    await screen.findByText("SOL-ANTERIOR");
    const filter = screen.getByRole("combobox", { name: "Filtrar por estado" });
    await user.selectOptions(filter, "Completed");
    await waitFor(() =>
      expect(fetchMock.mock.calls.at(-1)?.[0]).toContain("status=Completed"),
    );
    fireEvent.change(
      screen.getByRole("textbox", { name: "Referencia externa" }),
      { target: { value: validInput.externalReference } },
    );
    fireEvent.change(screen.getByRole("textbox", { name: "Cliente" }), {
      target: { value: validInput.customerId },
    });
    fireEvent.change(
      screen.getByRole("textbox", { name: "Código de servicio" }),
      { target: { value: validInput.serviceCode } },
    );
    fireEvent.change(screen.getByRole("textbox", { name: "Descripción" }), {
      target: { value: validInput.description },
    });

    await user.click(
      screen.getByRole("button", { name: "Registrar solicitud" }),
    );

    expect(
      await screen.findByText("Solicitud SOL-001 registrada correctamente."),
    ).toBeVisible();
    expect(filter).toHaveValue("All");
    const table = await screen.findByRole("table");
    await waitFor(() =>
      expect(within(table).getByText("SOL-001")).toBeVisible(),
    );
    expect(within(table).getByText("SOL-ANTERIOR")).toBeVisible();
  });

  it("permite consultar intentos y errores sin exponer controles de procesamiento", async () => {
    const user = userEvent.setup();
    serveRequests([
      requestFixture({
        status: "Failed",
        attemptCount: 3,
        lastError: "Fallo transitorio del proveedor.",
      }),
    ]);
    render(<App />);
    await screen.findByText("SOL-001");

    await user.click(screen.getByText("Ver detalle"));

    expect(screen.getByText("Fallo transitorio del proveedor.")).toBeVisible();
    expect(screen.getByText("Sin recibo todavía")).toBeVisible();
    expect(screen.getByText("Intentos")).toBeVisible();
    expect(screen.getByText("3")).toBeVisible();
    expect(
      within(screen.getByRole("table")).getByText("Fallida"),
    ).toBeVisible();
  });
});
