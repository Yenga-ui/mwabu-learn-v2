import { useState } from "react";
import { useSession } from "./Session";
import { api, sessionChanged } from "../api/client";
import { Action, Badge, Form, PageTitle } from "../components/ui";
export default function Account() {
  const { user, memberships } = useSession();
  const [changed, setChanged] = useState(false);
  return (
    <>
      <PageTitle
        eyebrow="YOUR ACCOUNT"
        title={`${user?.firstName} ${user?.lastName}`}
      >
        Your profile and sign-in security.
      </PageTitle>
      <div className="two-column">
        <section className="card">
          <h2>Profile</h2>
          <dl>
            <dt>Email</dt>
            <dd>{user?.email}</dd>
            <dt>Phone</dt>
            <dd>{user?.phoneNumber ?? "Not provided"}</dd>
            <dt>Account</dt>
            <dd>
              <Badge>Active</Badge>
            </dd>
          </dl>
          <h3>Your organisations</h3>
          {memberships.map((x) => (
            <p key={x.membership.id}>
              {x.organisationName}
              <small className="muted">{x.roleCodes.join(" · ")}</small>
            </p>
          ))}
        </section>
        <section className="card">
          <h2>Change password</h2>
          {changed ? (
            <p role="status">Password changed. Please sign in again.</p>
          ) : (
            <Form
              fields={[
                {
                  name: "currentPassword",
                  label: "Current password",
                  type: "password",
                  required: true,
                  maxLength: 128,
                },
                {
                  name: "newPassword",
                  label: "New password",
                  type: "password",
                  required: true,
                  minLength: 12,
                  maxLength: 128,
                  hint: "At least 12 characters, including uppercase, lowercase, a number and a symbol.",
                },
              ]}
              submit={async (values) => {
                await api("/api/auth/change-password", {
                  method: "POST",
                  body: values,
                });
                setChanged(true);
                await api("/api/browser/session/logout", {
                  method: "POST",
                  retryAuth: false,
                });
                sessionChanged();
              }}
            />
          )}
          <hr />
          <h3>Shared a device?</h3>
          <p>
            Sign out of every session to revoke access on other devices. This
            includes this browser.
          </p>
          <Action
            confirm="Sign out of all your sessions on every device?"
            run={async () => {
              await api("/api/browser/session/logout-all", { method: "POST" });
              sessionChanged();
            }}
          >
            Sign out everywhere
          </Action>
        </section>
      </div>
    </>
  );
}
