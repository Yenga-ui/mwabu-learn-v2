import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { api, queryString } from "../../api/client";
import type { User, Page, MyMembership } from "../../api/types";
import { useSession } from "../../auth/Session";
import {
  Action,
  Badge,
  Empty,
  Form,
  Modal,
  PageTitle,
  Pagination,
  QueryState,
  SearchBar,
  type Field,
} from "../../components/ui";
export const userFields: Field[] = [
  {
    name: "email",
    label: "Email address",
    type: "email",
    required: true,
    maxLength: 256,
  },
  { name: "firstName", label: "First name", required: true, maxLength: 100 },
  { name: "lastName", label: "Last name", required: true, maxLength: 100 },
  { name: "phoneNumber", label: "Phone (optional)", maxLength: 30 },
  {
    name: "initialPassword",
    label: "Initial password",
    type: "password",
    required: true,
    minLength: 12,
    maxLength: 128,
    hint: "At least 12 characters: uppercase, lowercase, a digit and a symbol. Share securely outside this application; it is never returned.",
  },
];
export default function Users() {
  const session = useSession();
  const { id } = useParams();
  const navigate = useNavigate();
  const client = useQueryClient();
  const [text, setText] = useState("");
  const [page, setPage] = useState(1);
  const [active, setActive] = useState("");
  const [creating, setCreating] = useState(false);
  const allowed = session.can("users.read", "platform");
  const manage = session.can("users.manage", "platform");
  const list = useQuery({
    queryKey: ["users", text, page, active],
    queryFn: ({ signal }) =>
      api<Page<User>>(
        "/api/users?" +
          queryString({ text, page, isActive: active, pageSize: 20 }),
        { signal },
      ),
    enabled: allowed && !id,
  });
  const detail = useQuery({
    queryKey: ["user", id],
    queryFn: () => api<User>("/api/users/" + id),
    enabled: allowed && !!id,
  });
  const memberships = useQuery({
    queryKey: ["user-memberships", id],
    queryFn: () => api<MyMembership[]>(`/api/users/${id}/memberships`),
    enabled: allowed && !!id,
  });
  if (!allowed)
    return (
      <Empty title="People administration requires platform access">
        Use your organisation workspace to manage its members.
      </Empty>
    );
  return (
    <>
      <PageTitle
        eyebrow="PEOPLE & ACCESS"
        title={
          detail.data
            ? detail.data.firstName + " " + detail.data.lastName
            : "Your learning community."
        }
        actions={
          manage &&
          !id && (
            <button className="button" onClick={() => setCreating(true)}>
              <Plus size={16} />
              Create user
            </button>
          )
        }
      >
        Provision accounts and manage their active state safely.
      </PageTitle>
      {id ? (
        <>
          <QueryState query={detail}>
            {(user) => (
              <section className="card">
                <h2>Account details</h2>
                <dl>
                  <dt>Email</dt>
                  <dd>{user.email}</dd>
                  <dt>Phone</dt>
                  <dd>{user.phoneNumber ?? "Not provided"}</dd>
                  <dt>Last sign in</dt>
                  <dd>
                    {user.lastLoginAt
                      ? new Date(user.lastLoginAt).toLocaleString()
                      : "Not yet signed in"}
                  </dd>
                  <dt>Status</dt>
                  <dd>
                    <Badge>{user.isActive ? "Active" : "Inactive"}</Badge>
                  </dd>
                </dl>
                {manage && (
                  <Action
                    confirm={
                      user.isActive
                        ? "Deactivate this account? All access tokens will be invalidated."
                        : "Reactivate this account? Existing organisational roles will still apply."
                    }
                    run={async () => {
                      await api(`/api/users/${id}/active`, {
                        method: "PATCH",
                        body: { isActive: !user.isActive },
                      });
                      await client.invalidateQueries({
                        queryKey: ["user", id],
                      });
                    }}
                  >
                    {user.isActive ? "Deactivate user" : "Reactivate user"}
                  </Action>
                )}
              </section>
            )}
          </QueryState>
          <section className="card">
            <h2>Organisation memberships</h2>
            <QueryState query={memberships}>
              {(items) =>
                items.length ? (
                  items.map((x) => (
                    <div className="assignment" key={x.membership.id}>
                      <div>
                        <Link
                          to={
                            "/app/organisations/" + x.membership.organisationId
                          }
                        >
                          {x.organisationName}
                        </Link>
                        <small>
                          {x.roleCodes.join(" · ") || "No roles assigned"} ·{" "}
                          {x.membership.isActive ? "Active" : "Inactive"}
                        </small>
                      </div>
                    </div>
                  ))
                ) : (
                  <Empty title="No memberships yet">
                    Open an organisation and add this user to its members.
                  </Empty>
                )
              }
            </QueryState>
            <Link className="button secondary" to="/app/organisations">
              Manage organisation memberships
            </Link>
          </section>
        </>
      ) : (
        <>
          <section className="card catalogue-tools">
            <SearchBar
              value={text}
              onSearch={(value) => {
                setText(value);
                setPage(1);
              }}
              placeholder="Search name or email…"
            />
            <label>
              Account status
              <select
                value={active}
                onChange={(e) => {
                  setActive(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">All accounts</option>
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </select>
            </label>
          </section>
          <QueryState query={list}>
            {(data) => (
              <>
                {data.items.length ? (
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Name</th>
                          <th>Email</th>
                          <th>Status</th>
                        </tr>
                      </thead>
                      <tbody>
                        {data.items.map((x) => (
                          <tr key={x.id}>
                            <td>
                              <Link to={"/app/users/" + x.id}>
                                {x.firstName} {x.lastName}
                              </Link>
                            </td>
                            <td>{x.email}</td>
                            <td>
                              <Badge>
                                {x.isActive ? "Active" : "Inactive"}
                              </Badge>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <Empty title="No matching accounts" />
                )}
                <Pagination
                  page={page}
                  hasMore={page * data.pageSize < (data.totalCount ?? 0)}
                  onChange={setPage}
                />
              </>
            )}
          </QueryState>
        </>
      )}
      {creating && (
        <Modal title="Create user account" onClose={() => setCreating(false)}>
          <Form
            fields={userFields}
            submit={async (values) => {
              const user = await api<User>("/api/users", {
                method: "POST",
                body: values,
              });
              setCreating(false);
              await client.invalidateQueries({ queryKey: ["users"] });
              navigate("/app/users/" + user.id);
            }}
            onCancel={() => setCreating(false)}
          />
        </Modal>
      )}
    </>
  );
}
