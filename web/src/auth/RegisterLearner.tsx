import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";
import { useSession } from "./Session";
import { useRegistrationPolicy } from "./Register";
import { Form, PageTitle, QueryState } from "../components/ui";
import type { Organisation } from "../api/types";

export default function RegisterLearner() {
  const session = useSession();
  const policy = useRegistrationPolicy();
  const [sent, setSent] = useState(false);
  const administrator = !!session.access?.platformAuthority;
  const organisations = useQuery({
    queryKey: ["learner-registration-organisations"],
    queryFn: () => api<Organisation[]>("/api/organisations"),
    enabled: administrator,
  });
  const eligible = session.memberships.filter(
    (x) =>
      x.membership.isActive &&
      x.organisationIsActive &&
      x.roleCodes.some(
        (role) => role === "Teacher" || role === "ParentGuardian",
      ),
  );
  const choices = administrator
    ? (organisations.data ?? [])
        .filter((x) => x.isActive)
        .map((x) => ({ value: x.id, label: x.name }))
    : eligible.map((x) => ({
        value: x.membership.organisationId,
        label: x.organisationName,
      }));
  return (
    <>
      <PageTitle title="Register a learner">
        A parent, teacher or Mwabu staff member registers the learner. Mwabu
        must review the request, licence and relationships before access is
        enabled.
      </PageTitle>
      {sent ? (
        <p role="status">
          Learner request saved. Mwabu will review it before assigning learning
          access. No guardian relationship has been verified automatically.
        </p>
      ) : (
        <QueryState query={policy}>
          {(data) =>
            data.enabled && data.policy?.allowLearnerRegistration ? (
              <section className="panel">
                <h2>{data.policy.title}</h2>
                <p>Version {data.policy.version}</p>
                <div className="lesson-readable-text">{data.policy.text}</div>
                <Form
                  fields={[
                    {
                      name: "organisationId",
                      label: "Learner's school or organisation",
                      type: "select",
                      required: true,
                      options: choices,
                    },
                    {
                      name: "firstName",
                      label: "Learner first name",
                      required: true,
                      maxLength: 100,
                    },
                    {
                      name: "lastName",
                      label: "Learner last name",
                      required: true,
                      maxLength: 100,
                    },
                    {
                      name: "email",
                      label: "Learner account email",
                      type: "email",
                      required: true,
                      maxLength: 256,
                      hint: "Use an authorised, unique account address. Do not reuse another person's account.",
                    },
                    {
                      name: "password",
                      label: "Initial learner password",
                      type: "password",
                      required: true,
                      minLength: 12,
                      maxLength: 128,
                      hint: "Use 12 or more characters with uppercase, lowercase, a number and a symbol. Share securely with the authorised adult or learner.",
                    },
                    {
                      name: "confirmAuthority",
                      label:
                        "I am authorised to register this learner and have obtained any required parent or guardian consent",
                      type: "checkbox",
                      required: true,
                    },
                    {
                      name: "acceptPolicy",
                      label: `I accept ${data.policy.title}, version ${data.policy.version}, for this registration`,
                      type: "checkbox",
                      required: true,
                    },
                  ]}
                  label="Submit learner registration request"
                  submit={async (values) => {
                    await api("/api/registration/learners", {
                      method: "POST",
                      body: { ...values, policyId: data.policy!.id },
                    });
                    setSent(true);
                  }}
                />
              </section>
            ) : (
              <p>
                Adult-managed learner registration is waiting for the applicable
                approved agreement. Contact Mwabu for assistance.
              </p>
            )
          }
        </QueryState>
      )}
    </>
  );
}
