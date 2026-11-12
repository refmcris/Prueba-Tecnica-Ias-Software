import type { RequestField } from "../types/requests";

interface FormFieldProps {
  name: RequestField;
  label: string;
  value: string;
  placeholder: string;
  limit: number;
  error?: string;
  multiline?: boolean;
  disabled: boolean;
  onChange: (name: RequestField, value: string) => void;
}

export function FormField({
  name,
  label,
  value,
  placeholder,
  limit,
  error,
  multiline,
  disabled,
  onChange,
}: FormFieldProps) {
  const id = `request-${name}`;
  const inputProps = {
    id,
    name,
    value,
    placeholder,
    required: true,
    disabled,
    "aria-invalid": Boolean(error),
    "aria-describedby": `${id}-hint${error ? ` ${id}-error` : ""}`,
    onChange: (
      event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>,
    ) => onChange(name, event.target.value),
  };

  return (
    <div
      className={`form-field${error ? " field-invalid" : ""}`}
    >
      <label htmlFor={id}>
        {label} <span aria-hidden="true">*</span>
      </label>
      {multiline ? (
        <textarea {...inputProps} rows={4} />
      ) : (
        <input {...inputProps} type="text" autoComplete="off" />
      )}
      <div className="field-meta" id={`${id}-hint`}>
        <span>Hasta {limit} caracteres</span>
        {multiline && (
          <span>
            {value.trim().length}/{limit}
          </span>
        )}
      </div>
      {error && (
        <p className="field-error" id={`${id}-error`}>
          {error}
        </p>
      )}
    </div>
  );
}
