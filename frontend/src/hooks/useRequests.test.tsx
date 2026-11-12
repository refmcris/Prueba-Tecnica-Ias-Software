import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { deferred, jsonResponse, requestFixture } from "../test/fixtures";
import type { RequestFilter } from "../types/requests";
import { useRequests } from "./useRequests";

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

async function advance(milliseconds: number) {
  await act(async () => vi.advanceTimersByTimeAsync(milliseconds));
}

describe("Actualización del listado", () => {
  it("refresca los estados cada dos segundos", async () => {
    vi.useFakeTimers();
    fetchMock
      .mockResolvedValueOnce(jsonResponse([requestFixture()]))
      .mockResolvedValueOnce(
        jsonResponse([
          requestFixture({ status: "Processing", attemptCount: 1 }),
        ]),
      )
      .mockResolvedValue(
        jsonResponse([
          requestFixture({ status: "Completed", attemptCount: 1 }),
        ]),
      );
    const { result } = renderHook(() => useRequests("All"));
    await advance(0);
    expect(result.current.requests[0].status).toBe("Pending");

    await advance(1999);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    await advance(1);
    expect(result.current.requests[0].status).toBe("Processing");
    await advance(2000);
    expect(result.current.requests[0].status).toBe("Completed");
  });

  it("espera la consulta en curso antes de programar la siguiente", async () => {
    vi.useFakeTimers();
    const pending = deferred<Response>();
    fetchMock
      .mockReturnValueOnce(pending.promise)
      .mockResolvedValue(jsonResponse([]));
    renderHook(() => useRequests("All"));

    await advance(10000);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    await act(async () => pending.resolve(jsonResponse([])));
    await advance(2000);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("ignora una respuesta anterior cuando cambia el filtro", async () => {
    const pending = deferred<Response>();
    const completed = requestFixture({
      externalReference: "SOL-C",
      status: "Completed",
    });
    fetchMock
      .mockReturnValueOnce(pending.promise)
      .mockResolvedValueOnce(jsonResponse([completed]));
    const { result, rerender } = renderHook(
      ({ filter }: { filter: RequestFilter }) => useRequests(filter),
      {
        initialProps: { filter: "All" as RequestFilter },
      },
    );
    const oldSignal = fetchMock.mock.calls[0][1]?.signal;

    rerender({ filter: "Completed" });
    await waitFor(() => expect(result.current.requests).toEqual([completed]));
    await act(async () => pending.resolve(jsonResponse([requestFixture()])));

    expect(oldSignal?.aborted).toBe(true);
    expect(result.current.requests).toEqual([completed]);
    expect(fetchMock.mock.calls[1][0]).toBe("/api/requests?status=Completed");
  });

  it("conserva los últimos datos ante un error y se recupera en la próxima consulta", async () => {
    vi.useFakeTimers();
    const original = requestFixture();
    const completed = requestFixture({ status: "Completed" });
    fetchMock
      .mockResolvedValueOnce(jsonResponse([original]))
      .mockRejectedValueOnce(new TypeError("Fallo de conexión"))
      .mockResolvedValue(jsonResponse([completed]));
    const { result } = renderHook(() => useRequests("All"));
    await advance(0);

    await advance(2000);
    expect(result.current.requests).toEqual([original]);
    expect(result.current.error).toContain("No fue posible actualizar");
    await advance(2000);
    expect(result.current.error).toBeNull();
    expect(result.current.requests).toEqual([completed]);
  });

  it("cancela consultas y temporizadores al desmontarse", async () => {
    vi.useFakeTimers();
    fetchMock.mockResolvedValue(jsonResponse([]));
    const view = renderHook(() => useRequests("All"));
    await advance(0);
    const signal = fetchMock.mock.calls[0][1]?.signal;

    view.unmount();
    await advance(10000);

    expect(signal?.aborted).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
