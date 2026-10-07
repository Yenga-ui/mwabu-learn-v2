import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { BookOpen, ArrowRight } from "lucide-react";
import { api, sessionChanged } from "../api/client";
import { Form, type Field } from "../components/ui";
import { safeReturn } from "./permissions";

export default function AuthPages({
  mode = "login",
}: {
  mode?: "login" | "recovery" | "reset";
}) {
  const navigate = useNavigate();
  const [search] = useSearchParams();
  const [message, setMessage] = useState("");
  const fields: Field[] = [
    {
      name: "email",
      label: "Email address",
      type: "email",
      required: true,
      maxLength: 256,
    },
  ];
  if (mode === "login")
    fields.push({
      name: "password",
      label: "Password",
      type: "password",
      required: true,
      maxLength: 128,
    });
  if (mode === "reset")
    fields.push(
      {
        name: "token",
        label: "Recovery code",
        required: true,
        maxLength: 4096,
        hint: "Enter the code from your recovery instructions.",
      },
      {
        name: "newPassword",
        label: "New password",
        type: "password",
        required: true,
        minLength: 12,
        maxLength: 128,
        hint: "Use at least 12 characters with uppercase, lowercase, a number and a symbol.",
      },
    );
  return (
    <main className="auth-layout">
      <section className="auth-story">
        <Link to="/" className="brand">
          <span className="brand-mark">m</span>
          <span>
            mwabu<span className="brand-sub">LEARN</span>
          </span>
        </Link>
        <div>
          <p className="eyebrow">FOR EVERY LEARNING DAY</p>
          <h1>
            Bring a little
            <br />
            <em>discovery</em>
            <br />
            to your classroom.
          </h1>
          <p>
            Thoughtful resources. Connected curricula.
            <br />
            More time for the moments that matter.
          </p>
          <div className="auth-illustration" aria-hidden="true">
            <BookOpen size={120} strokeWidth={1} />
            <span className="orbit one" />
            <span className="orbit two" />
            <span className="orbit three" />
          </div>
        </div>
        <small>Made for teachers. Built around learners.</small>
      </section>
      <section className="auth-panel">
        <div className="auth-form">
          <p className="eyebrow">WELCOME TO MWABU LEARN</p>
          <h2>
            {mode === "login"
              ? "Good to see you."
              : mode === "recovery"
                ? "Recover your account"
                : "Set a new password"}
          </h2>
          <p>
            {mode === "login"
              ? "Sign in to your learning workspace."
              : "Your organisation manages access to Mwabu Learn."}
          </p>
          {message ? (
            <div role="status" className="alert success">
              {message}
            </div>
          ) : (
            <Form
              fields={fields}
              label={
                mode === "login"
                  ? "Sign in"
                  : mode === "recovery"
                    ? "Request recovery instructions"
                    : "Reset password"
              }
              submit={async (values) => {
                if (mode === "login") {
                  await api("/api/browser/session/login", {
                    method: "POST",
                    body: values,
                    retryAuth: false,
                  });
                  sessionChanged();
                  navigate(safeReturn(search.get("return")), { replace: true });
                } else {
                  await api(
                    "/api/auth/" +
                      (mode === "recovery"
                        ? "forgot-password"
                        : "reset-password"),
                    { method: "POST", body: values, retryAuth: false },
                  );
                  setMessage(
                    mode === "recovery"
                      ? "If your account is eligible, recovery instructions will be delivered."
                      : "Your password has changed. Sign in with your new password.",
                  );
                }
              }}
            />
          )}
          <div className="auth-links">
            {mode === "login" ? (
              <Link to="/recovery">Forgot your password?</Link>
            ) : (
              <Link to="/login">
                Return to sign in <ArrowRight size={15} />
              </Link>
            )}
          </div>
          <div className="auth-help">
            <strong>Need an account?</strong>
            <p>
              Ask your school or organisation administrator to arrange access.
            </p>
          </div>
        </div>
      </section>
    </main>
  );
}
