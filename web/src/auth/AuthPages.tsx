import { Brand } from "../components/Brand";
import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { BookOpen, ArrowRight, Play, Download } from "lucide-react";
import { api, sessionChanged } from "../api/client";
import { Form, type Field } from "../components/ui";
import { safeReturn } from "./permissions";

export default function AuthPages({
  mode = "login",
}: {
  mode?: "login" | "recovery" | "reset";
}) {
  const navigate = useNavigate();
  const client = useQueryClient();
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
          <Brand />
        </Link>
        <div>
          <p className="eyebrow">MWABU LEARN</p>
          <h1>Ready for your next lesson.</h1>
          <p>
            Find curriculum resources, prepare your lessons and bring learning
            to life.
          </p>
          <div className="auth-features">
            <p>
              <BookOpen aria-hidden="true" />
              <span>
                <strong>Plan with confidence</strong>Find a lesson or create
                your own.
              </span>
            </p>
            <p>
              <Play aria-hidden="true" />
              <span>
                <strong>Bring lessons to life</strong>Open interactive teaching
                content.
              </span>
            </p>
            <p>
              <Download aria-hidden="true" />
              <span>
                <strong>Prepare for offline learning</strong>Download available
                lesson packages.
              </span>
            </p>
          </div>
        </div>
        <small>Made for teachers. Built around learners.</small>
      </section>
      <section className="auth-panel">
        <div className="auth-form">
          <p className="eyebrow">WELCOME TO MWABU LEARN</p>
          <h2>
            {mode === "login"
              ? "Sign in to Mwabu Learn"
              : mode === "recovery"
                ? "Recover your account"
                : "Set a new password"}
          </h2>
          <p>
            {mode === "login"
              ? "Sign in to your learning workspace."
              : "Get help accessing your Mwabu Learn account."}
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
                  // Logout/expiry can leave a fresh null profile cached. Fetch
                  // the new session instead of immediately redirecting it out.
                  await client.cancelQueries();
                  client.clear();
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
            {mode === "login" && (
              <p>
                <Link to="/guest?audience=teacher">
                  Explore as a guest teacher
                </Link>{" "}
                ·{" "}
                <Link to="/guest?audience=learner">
                  Explore as a guest learner
                </Link>
              </p>
            )}
            <strong>Need an account?</strong>
            {mode === "login" && (
              <p>
                <Link to="/register">Create an account</Link>
              </p>
            )}
            <p>
              Teachers and parents can request an account. An authorised adult
              or Mwabu registers learners. Your school administrator or Mwabu
              can help arrange access.
            </p>
          </div>
        </div>
      </section>
    </main>
  );
}
