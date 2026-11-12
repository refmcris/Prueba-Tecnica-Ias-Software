import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, createRequest, listRequests } from "./requestsApi";
import { jsonResponse, requestFixture, validInput } from "../test/fixtures";

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

describe("Contrato HTTP de solicitudes", () => {
  it("consulta sin filtro y conserva la señal de cancelación", async () => {
    const controller = new AbortController();
    const request = requestFixture();
    fetchMock.mockResolvedValue(jsonResponse([request]));

    await expect(listRequests("All", controller.signal)).resolves.toEqual([
      request,
    ]);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/requests",
      expect.objectContaining({ signal: controller.signal }),
    );
  });

  it("envía el estado original de la API como filtro", async () => {
    fetchMock.mockResolvedValue(jsonResponse([]));

    await listRequests("Completed");

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/requests?status=Completed",
      expect.anything(),
    );
  });

  it.each([201, 200])(
    "distingue creación y duplicado con la respuesta %i",
    async (status) => {
      const request = requestFixture();
      fetchMock.mockResolvedValue(jsonResponse(request, status));

      await expect(createRequest(validInput)).resolves.toEqual({
        request,
        created: status === 201,
      });
      expect(fetchMock).toHaveBeenCalledWith(
        "/api/requests",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify(validInput),
          headers: expect.objectContaining({
            "Content-Type": "application/json",
          }),
        }),
      );
    },
  );

  it("recupera errores por campo y descarta formatos inesperados", async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          errors: {
            customerId: ["El cliente es obligatorio."],
            description: 123,
            desconocido: ["No corresponde al formulario."],
          },
        },
        400,
      ),
    );

    await expect(createRequest(validInput)).rejects.toMatchObject({
      message: "Revisa los datos de la solicitud.",
      fieldErrors: { customerId: "El cliente es obligatorio." },
    });
  });

  it("presenta el conflicto en español sin revelar detalles internos", async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ detail: "Detalle interno que no debe mostrarse" }, 409),
    );

    await expect(createRequest(validInput)).rejects.toThrow(
      "Usa otra referencia para una operación nueva.",
    );
  });

  it("maneja errores sin un cuerpo JSON", async () => {
    fetchMock.mockResolvedValue(new Response("No disponible", { status: 503 }));

    await expect(listRequests("All")).rejects.toThrow(
      "No fue posible completar la operación.",
    );
  });

  it("un fallo de conexión conserva una instrucción segura para reintentar", async () => {
    fetchMock.mockRejectedValue(new TypeError("Fallo de conexión"));

    await expect(createRequest(validInput)).rejects.toThrow(
      "Puedes volver a enviarlo con la misma referencia.",
    );
  });

  it("propaga la cancelación para que no aparezca como un error visible", async () => {
    const controller = new AbortController();
    controller.abort();
    const error = new DOMException("Cancelada", "AbortError");
    fetchMock.mockRejectedValue(error);

    await expect(listRequests("All", controller.signal)).rejects.toBe(error);
  });

  it.each([{}, [{}], null])(
    "rechaza un listado que no cumple el contrato: %j",
    async (value) => {
      fetchMock.mockResolvedValue(jsonResponse(value));

      await expect(listRequests("All")).rejects.toBeInstanceOf(ApiError);
    },
  );

  it("rechaza una confirmación incompleta", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ id: "incompleto" }, 201));

    await expect(createRequest(validInput)).rejects.toThrow(
      "No fue posible confirmar el registro.",
    );
  });
});
