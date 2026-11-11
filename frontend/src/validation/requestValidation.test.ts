import { describe, expect, it } from "vitest";
import { validInput } from "../test/fixtures";
import { normalizeRequest, validateRequest } from "./requestValidation";

describe("Validación del formulario", () => {
  it("recorta espacios exteriores sin cambiar el contenido interior", () => {
    const normalized = normalizeRequest({
      externalReference: " SOL-001 ",
      customerId: " CLIENTE-001 ",
      serviceCode: " INSTALACION ",
      description: "  Instalación en la oficina.  ",
    });

    expect(normalized).toEqual(validInput);
    expect(validateRequest(normalized)).toEqual({});
  });

  it("identifica todos los campos vacíos", () => {
    const errors = validateRequest(
      normalizeRequest({
        externalReference: " ",
        customerId: "",
        serviceCode: "\t",
        description: "\n",
      }),
    );

    expect(Object.keys(errors)).toEqual([
      "externalReference",
      "customerId",
      "serviceCode",
      "description",
    ]);
  });

  it.each([
    ["externalReference", 128],
    ["customerId", 100],
    ["serviceCode", 80],
    ["description", 2000],
  ] as const)(
    "valida el límite de %s sin truncar la entrada",
    (field, limit) => {
      expect(
        validateRequest({ ...validInput, [field]: "a".repeat(limit) }),
      ).toEqual({});
      const input = { ...validInput, [field]: "a".repeat(limit + 1) };
      expect(validateRequest(input)).toEqual({
        [field]: `Máximo ${limit} caracteres.`,
      });
      expect(input[field]).toHaveLength(limit + 1);
    },
  );
});
