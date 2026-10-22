import { Brand } from "../components/Brand";
import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import {
  BookOpen,
  ArrowRight,
  Play,
  Download,
  GraduationCap,
} from "lucide-react";
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
    <main className="auth-layout auth-modern">
      <header className="auth-header">
        <Link to="/" className="brand">
          <Brand />
        </Link>
        <Link className="auth-explore" to="/guest">
          Explore as a guest <ArrowRight size={16} aria-hidden="true" />
        </Link>
      </header>
      <section className="auth-story">
        <div>
          <span className="auth-welcome-icon">
            <GraduationCap size={25} aria-hidden="true" />
          </span>
          <p className="eyebrow">LEARN. TEACH. GROW.</p>
          <h2>
            More discovery.
            <br />
            Every day.
          </h2>
          <p>
            A welcoming place to learn, prepare lessons and bring ideas to life.
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
      </section>
      <section className="auth-panel">
        <div className="auth-form">
          <p className="eyebrow">YOUR LEARNING WORKSPACE</p>
          <h1>
            {mode === "login"
              ? "Sign in to Mwabu Learn"
              : mode === "recovery"
                ? "Recover your account"
                : "Set a new password"}
          </h1>
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
              <nav
                className="auth-guest-choices"
                aria-label="Guest experiences"
              >
                <Link to="/guest?audience=teacher">
                  Explore as a guest teacher
                </Link>
                <Link to="/guest?audience=learner">
                  Explore as a guest learner
                </Link>
              </nav>
            )}
            {mode === "login" && (
              <p>
                New to Mwabu Learn?{" "}
                <Link to="/register">Create an account</Link>
              </p>
            )}
            <p>
              Teachers and parents can request an account. A parent, teacher or
              Mwabu registers learners.
            </p>
          </div>
        </div>
      </section>
      <footer className="auth-footer">
        Mwabu Learn <span aria-hidden="true">·</span> A little discovery, every
        day.
      </footer>
    </main>
  );
}
