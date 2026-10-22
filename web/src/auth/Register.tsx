import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api/client";
import { Brand } from "../components/Brand";
import { Form, QueryState, type Field } from "../components/ui";

type Policy = {
  id: string;
  version: string;
  title: string;
  text: string;
  sourceUrl: string;
  allowLearnerRegistration: boolean;
};
export function useRegistrationPolicy() {
  return useQuery({
    queryKey: ["registration-policy"],
    queryFn: ({ signal }) =>
      api<{ enabled: boolean; policy: Policy | null }>(
        "/api/registration/policy",
        { signal },
      ),
    staleTime: 0,
  });
}
export default function Register() {
  const policy = useRegistrationPolicy();
  const [sent, setSent] = useState(false);
  return (
    <main className="guest-catalogue">
      <Brand />
      <h1>Create your Mwabu Learn account</h1>
      <p>
        Registration does not activate a licence. Mwabu will verify your paid
        entitlement and arrange access.
      </p>
      {sent ? (
        <section className="panel" role="status">
          <h2>Your request has been saved</h2>
          <p>
            Contact Mwabu to arrange your licence. You can explore guest samples
            while waiting.
          </p>
        </section>
      ) : (
        <QueryState query={policy}>
          {(data) =>
            data.enabled && data.policy ? (
              <section className="panel">
                <h2>{data.policy.title}</h2>
                <p>Agreement version {data.policy.version}</p>
                <div className="lesson-readable-text">{data.policy.text}</div>
                {data.policy.sourceUrl && (
                  <p>
                    <a
                      href={data.policy.sourceUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      View agreement source
                    </a>
                  </p>
                )}
                <Form
                  fields={[
                    {
                      name: "firstName",
                      label: "First name",
                      required: true,
                      maxLength: 100,
                    },
                    {
                      name: "lastName",
                      label: "Last name",
                      required: true,
                      maxLength: 100,
                    },
                    {
                      name: "email",
                      label: "Email address",
                      type: "email",
                      required: true,
                      maxLength: 256,
                    },
                    {
                      name: "password",
                      label: "Choose a password",
                      type: "password",
                      required: true,
                      minLength: 12,
                      maxLength: 128,
                      hint: "Use at least 12 characters with uppercase, lowercase, a number and a symbol.",
                    },
                    {
                      name: "role",
                      label: "Account type",
                      type: "select",
                      required: true,
                      defaultValue: "Teacher",
                      options: ["Teacher", "Parent"].map((value) => ({
                        value,
                        label: value,
                      })),
                    },
                    {
                      name: "acceptPolicy",
                      label: `I have read and accept ${data.policy.title} (version ${data.policy.version})`,
                      type: "checkbox",
                      required: true,
                    },
                  ]}
                  label="Create account request"
                  submit={async (values) => {
                    await api("/api/registration", {
                      method: "POST",
                      body: { ...values, policyId: data.policy!.id },
                      retryAuth: false,
                    });
                    setSent(true);
                  }}
                />
              </section>
            ) : (
              <p role="status">
                Self-registration is not available yet. You can explore guest
                samples or contact Mwabu for an account.
              </p>
            )
          }
        </QueryState>
      )}
      <p>
        <Link to="/guest">Explore guest samples</Link> ·{" "}
        <Link to="/login">Back to sign in</Link>
      </p>
    </main>
  );
}

export function RegistrationSettings() {
  const policy = useRegistrationPolicy();
  const client = useQueryClient();
  const fields: Field[] = [
    {
      name: "version",
      label: "New agreement version",
      required: true,
      maxLength: 80,
    },
    { name: "title", label: "Agreement title", required: true, maxLength: 200 },
    {
      name: "text",
      label: "Approved agreement text",
      type: "textarea",
      required: true,
      minLength: 100,
      maxLength: 30000,
    },
    {
      name: "sourceUrl",
      label: "Approved source URL (HTTPS, optional for a printed policy)",
      maxLength: 1000,
    },
    {
      name: "allowLearnerRegistration",
      label:
        "Allow authorised adults to register learners under this agreement (never learner self-registration)",
      type: "checkbox",
    },
    {
      name: "confirmApproved",
      label:
        "This wording and registration policy are approved for Mwabu Learn v2",
      type: "checkbox",
      required: true,
    },
  ];
  return (
    <details className="panel">
      <summary>Public registration and agreements</summary>
      <p>
        Publishing a version enables registration. Review privacy, learner
        consent and the applicable service terms before publishing. Old versions
        and recorded acceptances are retained.
      </p>
      <QueryState query={policy}>
        {(data) => (
          <p>
            {data.enabled
              ? `Current version: ${data.policy?.version}`
              : "Registration disabled: no approved agreement published."}
          </p>
        )}
      </QueryState>
      <Form
        fields={fields}
        label="Publish approved agreement"
        submit={async (values) => {
          await api("/api/registration/policy", {
            method: "POST",
            body: values,
          });
          await client.invalidateQueries({ queryKey: ["registration-policy"] });
        }}
      />
    </details>
  );
}
