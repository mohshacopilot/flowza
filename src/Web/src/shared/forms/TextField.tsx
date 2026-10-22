import { Form, Input } from 'antd';
import { useId } from 'react';
import { Controller, type Control, type FieldValues, type Path } from 'react-hook-form';

interface TextFieldProps<TValues extends FieldValues> {
  control: Control<TValues>;
  name: Path<TValues>;
  label: string;
  extra?: string;
  required?: boolean;
  readOnly?: boolean;
  /** 'password' hides the value and adds Ant Design's show/hide button. */
  type?: 'text' | 'email' | 'tel' | 'password';
  autoComplete?: string;
}

/**
 * A labelled text input bound to React Hook Form, laid out by Ant Design (constitution 12.2: every field has a
 * label; its error and help text are linked with aria-describedby, and aria-invalid marks a wrong value).
 */
export function TextField<TValues extends FieldValues>({
  control,
  name,
  label,
  extra,
  required = false,
  readOnly = false,
  type = 'text',
  autoComplete = 'off',
}: TextFieldProps<TValues>) {
  const id = useId();
  const errorId = `${id}-error`;
  const extraId = `${id}-extra`;
  const InputControl = type === 'password' ? Input.Password : Input;
  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => {
        const message = fieldState.error?.message;
        const describedBy = [message ? errorId : '', extra ? extraId : ''].filter(Boolean).join(' ');
        return (
          <Form.Item
            label={label}
            htmlFor={id}
            required={required}
            {...(message
              ? { validateStatus: 'error' as const, help: <span id={errorId}>{message}</span> }
              : {})}
            {...(extra ? { extra: <span id={extraId}>{extra}</span> } : {})}
          >
            <InputControl
              id={id}
              ref={field.ref}
              name={field.name}
              value={typeof field.value === 'string' ? field.value : ''}
              onChange={field.onChange}
              onBlur={field.onBlur}
              type={type}
              autoComplete={autoComplete}
              readOnly={readOnly}
              aria-required={required}
              aria-invalid={message ? true : undefined}
              {...(describedBy ? { 'aria-describedby': describedBy } : {})}
            />
          </Form.Item>
        );
      }}
    />
  );
}
