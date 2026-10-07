import {
  useEffect,
  useRef,
  useState,
  useId,
  type FormEvent,
  type ReactNode,
} from "react";
import { useBlocker } from "react-router-dom";
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  BookOpen,
  RefreshCw,
  X,
} from "lucide-react";
import type { UseQueryResult } from "@tanstack/react-query";
import { ApiError } from "../api/client";

export function Loading({
  label = "Loading your workspace…",
}: {
  label?: string;
}) {
  return (
    <div className="loading" role="status">
      <div className="skeleton" />
      <div className="skeleton short" />
      {label}
    </div>
  );
}
export function ErrorNotice({
  error,
  retry,
}: {
  error: unknown;
  retry?: () => void;
}) {
  const message =
    error instanceof Error ? error.message : "Unable to load this information.";
  return (
    <div className="alert error" role="alert">
      <AlertCircle size={20} />
      <div>
        <strong>{message}</strong>
        {error instanceof ApiError &&
          Object.values(error.fields)
            .flat()
            .map((x, i) => <p key={i}>{x}</p>)}
        {error instanceof ApiError && error.correlationId && (
          <small>Support reference: {error.correlationId}</small>
        )}
      </div>
      {retry && (
        <button className="button secondary" onClick={retry}>
          <RefreshCw size={16} />
          Try again
        </button>
      )}
    </div>
  );
}
export function QueryState<T>({
  query,
  children,
}: {
  query: UseQueryResult<T, Error>;
  children: (data: T) => ReactNode;
}) {
  if (query.isPending) return <Loading />;
  if (query.error)
    return (
      <ErrorNotice error={query.error} retry={() => void query.refetch()} />
    );
  return <>{children(query.data!)}</>;
}
export function Empty({
  title = "Nothing here yet",
  children,
}: {
  title?: string;
  children?: ReactNode;
}) {
  return (
    <div className="empty">
      <BookOpen size={32} />
      <h3>{title}</h3>
      <p>
        {children ??
          "Try another search or check with your organisation administrator."}
      </p>
    </div>
  );
}
export function PageTitle({
  eyebrow,
  title,
  children,
  actions,
}: {
  eyebrow?: string;
  title: string;
  children?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <header className="page-title">
      <div>
        {eyebrow && <p className="eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>
        {children && <p>{children}</p>}
      </div>
      <div className="actions">{actions}</div>
    </header>
  );
}
export function Pagination({
  page,
  hasMore,
  onChange,
}: {
  page: number;
  hasMore: boolean;
  onChange: (page: number) => void;
}) {
  return (
    <nav className="pagination" aria-label="Pagination">
      <button
        className="button secondary"
        disabled={page <= 1}
        onClick={() => onChange(page - 1)}
      >
        <ArrowLeft size={16} />
        Previous
      </button>
      <span>Page {page}</span>
      <button
        className="button secondary"
        disabled={!hasMore}
        onClick={() => onChange(page + 1)}
      >
        Next
        <ArrowRight size={16} />
      </button>
    </nav>
  );
}
export function Badge({ children }: { children: ReactNode }) {
  return <span className="badge">{children}</span>;
}
export interface Field {
  name: string;
  label: string;
  type?:
    | "text"
    | "email"
    | "password"
    | "number"
    | "textarea"
    | "checkbox"
    | "date"
    | "select";
  required?: boolean;
  maxLength?: number;
  min?: number;
  minLength?: number;
  options?: { value: string; label: string }[];
  hint?: string;
  defaultValue?: string | number | boolean;
}
export type FormValues = Record<string, string | number | boolean | null>;
export function notify(message: string) {
  window.dispatchEvent(new CustomEvent("mwabu-notice", { detail: message }));
}
export function ToastRegion() {
  const [message, setMessage] = useState("");
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const receive = (event: Event) => {
      setMessage((event as CustomEvent<string>).detail);
      if (timer) clearTimeout(timer);
      timer = setTimeout(() => setMessage(""), 5000);
    };
    window.addEventListener("mwabu-notice", receive);
    return () => {
      window.removeEventListener("mwabu-notice", receive);
      if (timer) clearTimeout(timer);
    };
  }, []);
  return (
    <div className="toast-region" role="status" aria-live="polite">
      {message && (
        <div className="alert success">
          {message}
          <button
            className="button text"
            onClick={() => setMessage("")}
            aria-label="Dismiss notification"
          >
            Dismiss
          </button>
        </div>
      )}
    </div>
  );
}
export function Form({
  fields,
  initial = {},
  submit,
  label = "Save changes",
  onCancel,
}: {
  fields: Field[];
  initial?: Record<string, unknown>;
  submit: (values: FormValues) => Promise<unknown>;
  label?: string;
  onCancel?: () => void;
}) {
  const [error, setError] = useState<unknown>();
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const blocker = useBlocker(dirty && !saving);
  useEffect(() => {
    if (!dirty) return;
    const before = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", before);
    return () => window.removeEventListener("beforeunload", before);
  }, [dirty]);
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    const data = new FormData(event.currentTarget);
    const values: FormValues = {};
    fields.forEach((field) => {
      const value = data.get(field.name);
      values[field.name] =
        field.type === "checkbox"
          ? value === "on"
          : field.type === "number"
            ? value === ""
              ? null
              : Number(value)
            : typeof value === "string"
              ? field.type === "password"
                ? value
                : value.trim()
              : null;
    });
    setSaving(true);
    setError(undefined);
    try {
      await submit(values);
      setDirty(false);
      notify("Changes saved.");
    } catch (e) {
      setError(e);
    } finally {
      setSaving(false);
    }
  }
  return (
    <>
      <form
        className="form"
        data-dirty={dirty}
        data-saving={saving}
        onSubmit={save}
        onChange={() => setDirty(true)}
      >
        {error !== undefined && <ErrorNotice error={error} />}
        <fieldset disabled={saving}>
          {fields.map((field) => {
            const value = initial[field.name] ?? field.defaultValue ?? "";
            return (
              <label
                key={field.name}
                className={field.type === "checkbox" ? "check-field" : ""}
              >
                <span>
                  {field.label}
                  {field.required && <span aria-hidden="true"> *</span>}
                </span>
                {field.type === "textarea" ? (
                  <textarea
                    name={field.name}
                    defaultValue={String(value)}
                    required={field.required}
                    maxLength={field.maxLength}
                    rows={4}
                  />
                ) : field.type === "select" ? (
                  <select
                    name={field.name}
                    defaultValue={String(value)}
                    required={field.required}
                  >
                    <option value="">Choose…</option>
                    {field.options?.map((x) => (
                      <option key={x.value} value={x.value}>
                        {x.label}
                      </option>
                    ))}
                  </select>
                ) : field.type === "checkbox" ? (
                  <input
                    type="checkbox"
                    name={field.name}
                    defaultChecked={Boolean(value)}
                  />
                ) : (
                  <input
                    name={field.name}
                    type={field.type ?? "text"}
                    defaultValue={String(value)}
                    required={field.required}
                    maxLength={field.maxLength}
                    minLength={field.minLength}
                    min={field.min}
                    autoComplete={
                      field.type === "password"
                        ? ["password", "currentPassword"].includes(field.name)
                          ? "current-password"
                          : "new-password"
                        : field.type === "email"
                          ? "email"
                          : undefined
                    }
                  />
                )}{" "}
                {field.hint && <small>{field.hint}</small>}
              </label>
            );
          })}
        </fieldset>
        <div className="actions">
          <button className="button" disabled={saving}>
            {saving ? "Saving…" : label}
          </button>
          {onCancel && (
            <button
              className="button secondary"
              type="button"
              disabled={saving}
              onClick={() => {
                if (!dirty || window.confirm("Discard your unsaved changes?"))
                  onCancel();
              }}
            >
              Cancel
            </button>
          )}
        </div>
      </form>
      {blocker.state === "blocked" && (
        <Modal title="Discard unsaved changes?" onClose={() => blocker.reset()}>
          <p>Your changes have not been saved.</p>
          <div className="actions">
            <button
              className="button secondary"
              onClick={() => blocker.reset()}
            >
              Keep editing
            </button>
            <button className="button danger" onClick={() => blocker.proceed()}>
              Discard changes
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}
export function Modal({
  title,
  onClose,
  children,
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  function close() {
    if (ref.current?.querySelector('form[data-saving="true"]')) return;
    if (
      ref.current?.querySelector('form[data-dirty="true"]') &&
      !window.confirm("Discard your unsaved changes?")
    )
      return;
    onClose();
  }
  useEffect(() => {
    const previous = document.activeElement as HTMLElement;
    ref.current?.showModal();
    return () => previous?.focus();
  }, []);
  return (
    <dialog
      ref={ref}
      onCancel={(e) => {
        e.preventDefault();
        close();
      }}
      aria-labelledby={titleId}
    >
      <header>
        <h2 id={titleId}>{title}</h2>
        <button
          className="icon-button"
          onClick={close}
          aria-label="Close dialog"
        >
          <X />
        </button>
      </header>
      {children}
    </dialog>
  );
}
export function Action({
  children,
  run,
  confirm,
  className = "button secondary",
}: {
  children: ReactNode;
  run: () => Promise<unknown>;
  confirm?: string;
  className?: string;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const [confirmation, setConfirmation] = useState(false);
  async function act() {
    setBusy(true);
    setError(undefined);
    try {
      await run();
      setConfirmation(false);
      notify("Action completed.");
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <button
        className={className}
        disabled={busy}
        onClick={() => (confirm ? setConfirmation(true) : void act())}
      >
        {busy ? "Working…" : children}
      </button>
      {error !== undefined && <ErrorNotice error={error} />}{" "}
      {confirmation && (
        <Modal
          title="Confirm action"
          onClose={() => !busy && setConfirmation(false)}
        >
          <p>{confirm}</p>
          <div className="actions">
            <button
              className="button secondary"
              disabled={busy}
              onClick={() => setConfirmation(false)}
            >
              Cancel
            </button>
            <button
              className="button danger"
              disabled={busy}
              onClick={() => void act()}
            >
              Confirm
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}
export function SearchBar({
  value,
  onSearch,
  placeholder = "Search…",
}: {
  value: string;
  onSearch: (value: string) => void;
  placeholder?: string;
}) {
  const searchId = useId();
  return (
    <form
      className="search"
      onSubmit={(e) => {
        e.preventDefault();
        onSearch(
          String(new FormData(e.currentTarget).get("search") ?? "").trim(),
        );
      }}
    >
      <label className="sr-only" htmlFor={searchId}>
        Search
      </label>
      <input
        id={searchId}
        key={value}
        name="search"
        defaultValue={value}
        placeholder={placeholder}
        maxLength={200}
      />
      <button className="button secondary">Search</button>
    </form>
  );
}
