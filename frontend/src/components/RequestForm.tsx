import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import {
  EMPTY_REQUEST,
  FIELD_LIMITS,
  REQUEST_FIELDS,
} from "../constants/requests";
import { ApiError, createRequest, errorMessage } from "../services/requestsApi";
import type {
  CreateRequestInput,
  CreateRequestResult,
  FieldErrors,
  RequestField,
} from "../types/requests";
import {
  normalizeRequest,
  validateRequest,
} from "../validation/requestValidation";
import { FeedbackMessage } from "./FeedbackMessage";
import type { Feedback } from "./FeedbackMessage";
import { FormField } from "./FormField";

interface RequestFormProps {
  onSaved: (result: CreateRequestResult) => void;
}

function focusInvalidField(fieldErrors: FieldErrors) {
  const field = REQUEST_FIELDS.find((name) => fieldErrors[name]);
  if (field) {
    document.getElementById(`request-${field}`)?.focus();
  }
}

export function RequestForm({ onSaved }: RequestFormProps) {
  const [values, setValues] = useState<CreateRequestInput>({
    ...EMPTY_REQUEST,
  });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const activeRequest = useRef<AbortController | null>(null);
  const shouldFocusErrors = useRef(false);

  useEffect(() => () => activeRequest.current?.abort(), []);

  useEffect(() => {
    if (!submitting && shouldFocusErrors.current) {
      shouldFocusErrors.current = false;
      focusInvalidField(errors);
    }
  }, [errors, submitting]);

  function updateField(field: RequestField, value: string) {
    setValues((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
    setFeedback(null);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (activeRequest.current) {
      return;
    }

    const input = normalizeRequest(values);
    const validationErrors = validateRequest(input);
    setErrors(validationErrors);
    setFeedback(null);

    if (Object.keys(validationErrors).length > 0) {
      focusInvalidField(validationErrors);
      return;
    }

    const controller = new AbortController();
    activeRequest.current = controller;
    setValues(input);
    setSubmitting(true);

    try {
      const result = await createRequest(input, controller.signal);
      if (controller.signal.aborted) {
        return;
      }

      setFeedback({
        tone: result.created ? "success" : "info",
        message: result.created
          ? `Solicitud ${result.request.externalReference} registrada correctamente.`
          : `La solicitud ${result.request.externalReference} ya estaba registrada. Se conserva el mismo registro.`,
      });
      onSaved(result);
    } catch (error) {
      if (!controller.signal.aborted) {
        const fieldErrors = error instanceof ApiError ? error.fieldErrors : {};
        setErrors(fieldErrors);
        shouldFocusErrors.current = true;
        setFeedback({
          tone: "error",
          message: errorMessage(
            error,
            "No fue posible confirmar el registro. Puedes volver a enviarlo con la misma referencia.",
          ),
        });
      }
    } finally {
      if (activeRequest.current === controller) {
        activeRequest.current = null;
      }
      if (!controller.signal.aborted) {
        setSubmitting(false);
      }
    }
  }

  function clear() {
    setValues({ ...EMPTY_REQUEST });
    setErrors({});
    setFeedback(null);
  }

  return (
    <section className="panel form-panel" aria-labelledby="form-title">
      <h2 id="form-title">Registrar solicitud</h2>
      <form onSubmit={submit} noValidate aria-label="Registrar solicitud">
        <p className="required-note">Todos los campos son obligatorios.</p>
        <FormField
          name="externalReference"
          label="Referencia externa"
          value={values.externalReference}
          placeholder="Ej. SOL-2026-001"
          limit={FIELD_LIMITS.externalReference}
          error={errors.externalReference}
          disabled={submitting}
          onChange={updateField}
        />
        <FormField
          name="customerId"
          label="Cliente"
          value={values.customerId}
          placeholder="Ej. CLIENTE-001"
          limit={FIELD_LIMITS.customerId}
          error={errors.customerId}
          disabled={submitting}
          onChange={updateField}
        />
        <FormField
          name="serviceCode"
          label="Código de servicio"
          value={values.serviceCode}
          placeholder="Ej. INSTALACION"
          limit={FIELD_LIMITS.serviceCode}
          error={errors.serviceCode}
          disabled={submitting}
          onChange={updateField}
        />
        <FormField
          name="description"
          label="Descripción"
          value={values.description}
          placeholder="Describe el servicio solicitado"
          limit={FIELD_LIMITS.description}
          error={errors.description}
          disabled={submitting}
          onChange={updateField}
          multiline
        />
        <FeedbackMessage feedback={feedback} />
        <div className="form-actions">
          <button
            type="submit"
            className="button button-primary"
            disabled={submitting}
          >
            {!submitting && (
              <span className="button-icon" aria-hidden="true">
                +
              </span>
            )}
            {submitting ? "Registrando…" : "Registrar solicitud"}
          </button>
          <button
            type="button"
            className="button button-quiet"
            disabled={submitting}
            onClick={clear}
          >
            Limpiar
          </button>
        </div>
      </form>
    </section>
  );
}
