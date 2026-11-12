import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  deferred,
  jsonResponse,
  requestFixture,
  validInput,
} from "../test/fixtures";
import { RequestForm } from "./RequestForm";

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

async function fillForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(
    screen.getByRole("textbox", { name: "Referencia externa" }),
    " SOL-001 ",
  );
  await user.type(
    screen.getByRole("textbox", { name: "Cliente" }),
    validInput.customerId,
  );
  await user.type(
    screen.getByRole("textbox", { name: "Código de servicio" }),
    validInput.serviceCode,
  );
  await user.type(
    screen.getByRole("textbox", { name: "Descripción" }),
    validInput.description,
  );
}

function submitButton() {
  return screen.getByRole("button", { name: "Registrar solicitud" });
}

describe("Registro de solicitudes", () => {
  it("muestra los errores obligatorios y enfoca el primer campo sin enviar", async () => {
    const user = userEvent.setup();
    render(<RequestForm onSaved={vi.fn()} />);

    await user.click(submitButton());

    expect(screen.getAllByText("Este campo es obligatorio.")).toHaveLength(4);
    expect(
      screen.getByRole("textbox", { name: "Referencia externa" }),
    ).toHaveFocus();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("registra datos normalizados y confirma la creación en español", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    const request = requestFixture();
    fetchMock.mockResolvedValue(jsonResponse(request, 201));
    render(<RequestForm onSaved={onSaved} />);
    await fillForm(user);

    await user.click(submitButton());

    expect(
      await screen.findByText("Solicitud SOL-001 registrada correctamente."),
    ).toBeVisible();
    expect(onSaved).toHaveBeenCalledWith({ request, created: true });
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toEqual(
      validInput,
    );
    expect(
      screen.getByRole("textbox", { name: "Referencia externa" }),
    ).toHaveValue("SOL-001");
  });

  it("informa que el duplicado conserva el registro existente", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    const request = requestFixture({
      status: "Completed",
      attemptCount: 1,
      providerReceiptId: "recibo-001",
    });
    fetchMock.mockResolvedValue(jsonResponse(request, 200));
    render(<RequestForm onSaved={onSaved} />);
    await fillForm(user);

    await user.click(submitButton());

    expect(await screen.findByText(/ya estaba registrada/)).toBeVisible();
    expect(onSaved).toHaveBeenCalledWith({ request, created: false });
  });

  it("conserva los datos cuando hay un conflicto", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    fetchMock.mockResolvedValue(jsonResponse({}, 409));
    render(<RequestForm onSaved={onSaved} />);
    await fillForm(user);

    await user.click(submitButton());

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Usa otra referencia para una operación nueva.",
    );
    expect(screen.getByRole("textbox", { name: "Descripción" })).toHaveValue(
      validInput.description,
    );
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("muestra los errores del servidor junto al campo y devuelve el foco", async () => {
    const user = userEvent.setup();
    fetchMock.mockResolvedValue(
      jsonResponse(
        { errors: { customerId: ["El cliente no es válido."] } },
        400,
      ),
    );
    render(<RequestForm onSaved={vi.fn()} />);
    await fillForm(user);

    await user.click(submitButton());

    expect(await screen.findByText("El cliente no es válido.")).toBeVisible();
    const customer = screen.getByRole("textbox", { name: "Cliente" });
    await waitFor(() => expect(customer).toHaveFocus());
    expect(customer).toHaveAttribute("aria-invalid", "true");
    expect(customer).toHaveAccessibleDescription(/El cliente no es válido/);
  });

  it("permite reintentar con la misma referencia si se pierde la conexión", async () => {
    const user = userEvent.setup();
    fetchMock.mockRejectedValue(new TypeError("Fallo de conexión"));
    render(<RequestForm onSaved={vi.fn()} />);
    await fillForm(user);

    await user.click(submitButton());

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Puedes volver a enviarlo con la misma referencia.",
    );
    expect(submitButton()).toBeEnabled();
    expect(
      screen.getByRole("textbox", { name: "Referencia externa" }),
    ).toHaveValue("SOL-001");
  });

  it("bloquea envíos repetidos mientras espera la respuesta", async () => {
    const user = userEvent.setup();
    const pending = deferred<Response>();
    fetchMock.mockReturnValue(pending.promise);
    render(<RequestForm onSaved={vi.fn()} />);
    await fillForm(user);
    await user.click(submitButton());

    expect(screen.getByRole("button", { name: "Registrando…" })).toBeDisabled();
    fireEvent.submit(screen.getByRole("form", { name: "Registrar solicitud" }));
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await act(async () => pending.resolve(jsonResponse(requestFixture(), 201)));
    expect(await screen.findByText(/registrada correctamente/)).toBeVisible();
  });

  it("valida la longitud sin truncar lo que escribió el usuario", async () => {
    const user = userEvent.setup();
    render(<RequestForm onSaved={vi.fn()} />);
    await fillForm(user);
    const description = screen.getByRole("textbox", { name: "Descripción" });
    fireEvent.change(description, { target: { value: "a".repeat(2001) } });

    await user.click(submitButton());

    expect(screen.getByText("Máximo 2000 caracteres.")).toBeVisible();
    expect(description).toHaveValue("a".repeat(2001));
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("cancela el envío al desmontarse y evita notificaciones tardías", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    const pending = deferred<Response>();
    fetchMock.mockReturnValue(pending.promise);
    const view = render(<RequestForm onSaved={onSaved} />);
    await fillForm(user);
    await user.click(submitButton());
    const signal = fetchMock.mock.calls[0][1]?.signal;

    view.unmount();
    expect(signal?.aborted).toBe(true);
    await act(async () => pending.resolve(jsonResponse(requestFixture(), 201)));
    expect(onSaved).not.toHaveBeenCalled();
  });
});
