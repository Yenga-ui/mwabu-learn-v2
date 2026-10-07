import { useState } from "react";
import { Link, Navigate, useNavigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Building2 } from "lucide-react";
import { api, queryString } from "../../api/client";
import type {
  Access,
  Organisation,
  Page,
  Member,
  Role,
  User,
  CurriculumAssignment,
  GuardianLink,
} from "../../api/types";
import { useSession } from "../../auth/Session";
import { can } from "../../auth/permissions";
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
import { userFields } from "../users/Users";
import NodePicker from "../curriculum/NodePicker";
const organisationFields: Field[] = [
  { name: "name", label: "Organisation name", required: true, maxLength: 200 },
  { name: "code", label: "Organisation code", required: true, maxLength: 50 },
  {
    name: "organisationType",
    label: "Organisation type",
    type: "select",
    required: true,
    options: [
      "Platform",
      "Ministry",
      "Province",
      "District",
      "School",
      "Partner",
      "Project",
      "Other",
    ].map((value) => ({ value, label: value })),
  },
];
export default function Organisations() {
  const { id } = useParams();
  const session = useSession();
  const client = useQueryClient();
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [text, setText] = useState("");
  const [edit, setEdit] = useState(false);
  const platform = session.can("organisations.read", "platform");
  const list = useQuery({
    queryKey: ["organisations", page, text],
    queryFn: () =>
      api<Page<Organisation>>(
        "/api/organisations/search?" +
          queryString({ page, pageSize: 25, text }),
      ),
    enabled: platform,
  });
  const access = useQuery({
    queryKey: ["org-access", id],
    queryFn: () => api<Access>("/api/workspace/access?organisationId=" + id),
    enabled: !!id,
  });
  const allowed = can(access.data, "organisations.read");
  const manage = can(access.data, "organisations.manage");
  const detail = useQuery({
    queryKey: ["organisation", id],
    queryFn: () => api<Organisation>("/api/organisations/" + id),
    enabled: !!id && allowed,
  });
  if (!id && !platform)
    return session.organisationId ? (
      <Navigate replace to={"/app/organisations/" + session.organisationId} />
    ) : (
      <Empty title="No organisation selected" />
    );
  if (id && access.isPending)
    return <QueryState query={access}>{() => null}</QueryState>;
  if (id && !allowed)
    return <Empty title="You do not have access to this organisation" />;
  const fields = [...organisationFields];
  if (session.can("organisations.manage", "platform"))
    fields.push({
      name: "parentOrganisationId",
      label: "Parent organisation (optional)",
      type: "select",
      options: (list.data?.items ?? [])
        .filter((x) => x.id !== id)
        .map((x) => ({ value: x.id, label: x.name })),
    });
  return (
    <>
      <PageTitle
        eyebrow="ORGANISATIONS"
        title={detail.data?.name ?? "Connected around learning."}
        actions={
          id
            ? manage && (
                <button
                  className="button secondary"
                  onClick={() => setEdit(true)}
                >
                  Edit organisation
                </button>
              )
            : session.can("organisations.manage", "platform") && (
                <button className="button" onClick={() => setEdit(true)}>
                  <Plus size={16} />
                  Create organisation
                </button>
              )
        }
      >
        Organisational boundaries keep people and permissions in the right
        place.
      </PageTitle>
      {!id ? (
        <>
          <SearchBar
            value={text}
            onSearch={(value) => {
              setText(value);
              setPage(1);
            }}
            placeholder="Search organisations…"
          />
          <QueryState query={list}>
            {(data) => (
              <>
                {data.items.length ? (
                  <div className="node-grid">
                    {data.items.map((x) => (
                      <Link
                        className="card"
                        key={x.id}
                        to={"/app/organisations/" + x.id}
                      >
                        <Building2 size={25} />
                        <h2>{x.name}</h2>
                        <p>
                          {x.code} · {x.organisationType}
                        </p>
                        <Badge>{x.isActive ? "Active" : "Inactive"}</Badge>
                        {x.parentOrganisationId && (
                          <small>Part of an organisation hierarchy</small>
                        )}
                      </Link>
                    ))}
                  </div>
                ) : (
                  <Empty title="No matching organisations" />
                )}
                <Pagination
                  page={page}
                  hasMore={data.hasMore ?? false}
                  onChange={setPage}
                />
              </>
            )}
          </QueryState>
        </>
      ) : (
        <QueryState query={detail}>
          {(org) => (
            <>
              <section className="card">
                <div className="section-heading">
                  <h2>{org.organisationType} profile</h2>
                  <Badge>{org.isActive ? "Active" : "Inactive"}</Badge>
                </div>
                <dl>
                  <dt>Code</dt>
                  <dd>{org.code}</dd>
                  <dt>Parent organisation</dt>
                  <dd>
                    {org.parentOrganisationId ? (
                      <Link
                        to={"/app/organisations/" + org.parentOrganisationId}
                      >
                        View parent organisation
                      </Link>
                    ) : (
                      "No parent organisation"
                    )}
                  </dd>
                </dl>
                {session.can("organisations.manage", "platform") && (
                  <Action
                    confirm={
                      (org.isActive ? "Deactivate " : "Reactivate ") +
                      org.name +
                      "? Organisation-scoped access is affected immediately."
                    }
                    run={async () => {
                      await api(`/api/organisations/${id}/active`, {
                        method: "PATCH",
                        body: { isActive: !org.isActive },
                      });
                      await client.invalidateQueries({
                        queryKey: ["organisation", id],
                      });
                      await client.invalidateQueries({
                        queryKey: ["org-access", id],
                      });
                    }}
                  >
                    {org.isActive
                      ? "Deactivate organisation"
                      : "Reactivate organisation"}
                  </Action>
                )}
              </section>
              {org.isActive && (
                <>
                  <Members organisationId={org.id} access={access.data!} />
                  <SchoolConnections
                    organisationId={org.id}
                    access={access.data!}
                  />
                  {can(access.data, "projects.read") && (
                    <Link
                      className="button secondary"
                      to="/app/projects"
                      onClick={() => session.setOrganisation(org.id)}
                    >
                      Open organisation projects
                    </Link>
                  )}
                </>
              )}
            </>
          )}
        </QueryState>
      )}
      {edit && (
        <Modal
          title={id ? "Edit organisation" : "Create organisation"}
          onClose={() => setEdit(false)}
        >
          {session.can("organisations.manage", "platform") && (
            <>
              <SearchBar
                value={text}
                onSearch={(value) => {
                  setText(value);
                  setPage(1);
                }}
                placeholder="Find a parent organisation…"
              />
              <Pagination
                page={page}
                hasMore={list.data?.hasMore ?? false}
                onChange={setPage}
              />
            </>
          )}
          <Form
            fields={fields}
            initial={detail.data ? { ...detail.data } : {}}
            submit={async (values) => {
              const org = await api<Organisation>(
                "/api/organisations" + (id ? "/" + id : ""),
                {
                  method: id ? "PUT" : "POST",
                  body: {
                    ...values,
                    parentOrganisationId:
                      "parentOrganisationId" in values
                        ? values.parentOrganisationId || null
                        : detail.data?.parentOrganisationId || null,
                  },
                },
              );
              setEdit(false);
              await client.invalidateQueries({ queryKey: ["organisations"] });
              await client.invalidateQueries({ queryKey: ["organisation"] });
              navigate("/app/organisations/" + org.id);
            }}
            onCancel={() => setEdit(false)}
          />
        </Modal>
      )}
    </>
  );
}
function Members({
  organisationId: id,
  access,
}: {
  organisationId: string;
  access: Access;
}) {
  const session = useSession();
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [text, setText] = useState("");
  const [role, setRole] = useState("");
  const [adding, setAdding] = useState(false);
  const [provision, setProvision] = useState(false);
  const [selected, setSelected] = useState<Member>();
  const [userSearch, setUserSearch] = useState("");
  const [userPage, setUserPage] = useState(1);
  const [userId, setUserId] = useState("");
  const manage = can(access, "memberships.manage");
  const read = can(access, "users.read");
  const members = useQuery({
    queryKey: ["members", id, page, text, role],
    queryFn: () =>
      api<Page<Member>>(
        `/api/organisations/${id}/workspace/members?` +
          queryString({ page, pageSize: 20, text, role }),
      ),
    enabled: read,
  });
  const roles = useQuery({
    queryKey: ["roles"],
    queryFn: () => api<Role[]>("/api/roles"),
    enabled: manage,
  });
  const users = useQuery({
    queryKey: ["member-user-picker", userPage, userSearch],
    queryFn: () =>
      api<Page<User>>(
        "/api/users?" +
          queryString({
            page: userPage,
            text: userSearch,
            pageSize: 20,
            isActive: true,
          }),
      ),
    enabled: adding && session.can("users.read", "platform"),
  });
  const refresh = async () => {
    await client.invalidateQueries({ queryKey: ["members", id] });
    await client.invalidateQueries({ queryKey: ["access"] });
  };
  if (!read) return null;
  return (
    <section className="card">
      <div className="section-heading">
        <h2>People in this organisation</h2>
        <div className="actions">
          {manage && session.can("users.read", "platform") && (
            <button
              className="button secondary"
              onClick={() => setAdding(true)}
            >
              Add existing user
            </button>
          )}
          {manage && can(access, "users.manage") && (
            <button className="button" onClick={() => setProvision(true)}>
              <Plus size={16} />
              Create member account
            </button>
          )}
        </div>
      </div>
      <div className="filter-row">
        <SearchBar
          value={text}
          onSearch={(value) => {
            setText(value);
            setPage(1);
          }}
          placeholder="Search members…"
        />
        <label>
          Role
          <select
            value={role}
            onChange={(e) => {
              setRole(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All members</option>
            {[
              "OrganisationAdmin",
              "ProjectManager",
              "HeadTeacher",
              "Teacher",
              "Learner",
              "ParentGuardian",
              "ContentManager",
              "DataAnalyst",
            ].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </select>
        </label>
      </div>
      <QueryState query={members}>
        {(data) => (
          <>
            {data.items.length ? (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Roles</th>
                      <th>Status</th>
                      {manage && <th>Actions</th>}
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((x) => (
                      <tr key={x.id}>
                        <td>
                          {x.firstName} {x.lastName}
                          <small>{x.email}</small>
                        </td>
                        <td>{x.roleCodes.join(", ") || "No roles"}</td>
                        <td>
                          <Badge>{x.isActive ? "Active" : "Inactive"}</Badge>
                        </td>
                        {manage && (
                          <td>
                            <div className="actions">
                              <button
                                className="button text"
                                onClick={() => setSelected(x)}
                              >
                                Manage roles
                              </button>
                              <Action
                                confirm={
                                  (x.isActive ? "Deactivate " : "Reactivate ") +
                                  "this membership?"
                                }
                                run={async () => {
                                  await api(
                                    `/api/organisations/${id}/members/${x.id}/active`,
                                    {
                                      method: "PATCH",
                                      body: { isActive: !x.isActive },
                                    },
                                  );
                                  await refresh();
                                }}
                              >
                                {x.isActive ? "Deactivate" : "Reactivate"}
                              </Action>
                            </div>
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <Empty title="No matching members" />
            )}
            <Pagination
              page={page}
              hasMore={data.hasMore ?? false}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>
      {provision && (
        <Modal
          title="Create an organisation member"
          onClose={() => setProvision(false)}
        >
          <Form
            fields={userFields}
            submit={async (values) => {
              await api(`/api/organisations/${id}/users`, {
                method: "POST",
                body: values,
              });
              setProvision(false);
              await refresh();
            }}
            onCancel={() => setProvision(false)}
          />
        </Modal>
      )}
      {adding && (
        <Modal title="Add existing user" onClose={() => setAdding(false)}>
          <SearchBar
            value={userSearch}
            onSearch={(value) => {
              setUserSearch(value);
              setUserPage(1);
            }}
            placeholder="Search name or email…"
          />
          <QueryState query={users}>
            {(data) => (
              <>
                <label>
                  User
                  <select
                    value={userId}
                    onChange={(e) => setUserId(e.target.value)}
                  >
                    <option value="">Choose user…</option>
                    {data.items.map((x) => (
                      <option key={x.id} value={x.id}>
                        {x.firstName} {x.lastName} — {x.email}
                      </option>
                    ))}
                  </select>
                </label>
                <Pagination
                  page={userPage}
                  hasMore={userPage * data.pageSize < (data.totalCount ?? 0)}
                  onChange={setUserPage}
                />
                {userId && (
                  <Action
                    run={async () => {
                      await api(`/api/organisations/${id}/members`, {
                        method: "POST",
                        body: { userId },
                      });
                      setAdding(false);
                      await refresh();
                    }}
                  >
                    Add membership
                  </Action>
                )}
              </>
            )}
          </QueryState>
        </Modal>
      )}
      {selected && (
        <Modal
          title={"Roles for " + selected.firstName}
          onClose={() => setSelected(undefined)}
        >
          <p>
            Roles apply to this organisation only. You can delegate permissions
            you hold.
          </p>
          <QueryState query={roles}>
            {(items) => (
              <div className="role-options">
                {items
                  .filter(
                    (r) =>
                      access.platformAuthority ||
                      (!r.grantsPlatformAuthority &&
                        r.permissions.every((permission) =>
                          can(access, permission),
                        )),
                  )
                  .map((r) => (
                    <div className="assignment" key={r.id}>
                      <div>
                        <strong>{r.name}</strong>
                        <small>{r.permissions.join(" · ")}</small>
                      </div>
                      <Action
                        confirm={
                          selected.roleCodes.includes(r.code)
                            ? "Remove this role assignment?"
                            : undefined
                        }
                        run={async () => {
                          const assigned = selected.roleCodes.includes(r.code);
                          await api(
                            `/api/organisations/${id}/members/${selected.id}/roles` +
                              (assigned ? "/" + r.id : ""),
                            {
                              method: assigned ? "DELETE" : "POST",
                              body: assigned ? undefined : { roleId: r.id },
                            },
                          );
                          setSelected((current) =>
                            current
                              ? {
                                  ...current,
                                  roleCodes: assigned
                                    ? current.roleCodes.filter(
                                        (x) => x !== r.code,
                                      )
                                    : [
                                        ...new Set([
                                          ...current.roleCodes,
                                          r.code,
                                        ]),
                                      ],
                                }
                              : undefined,
                          );
                          await refresh();
                        }}
                      >
                        {selected.roleCodes.includes(r.code)
                          ? "Remove"
                          : "Assign"}
                      </Action>
                    </div>
                  ))}
              </div>
            )}
          </QueryState>
        </Modal>
      )}
    </section>
  );
}
function SchoolConnections({
  organisationId: id,
  access,
}: {
  organisationId: string;
  access: Access;
}) {
  const client = useQueryClient();
  const [version, setVersion] = useState<{ id: string; name: string }>();
  const [linking, setLinking] = useState(false);
  const [linkPage, setLinkPage] = useState(1);
  const [memberPage, setMemberPage] = useState(1);
  const [memberText, setMemberText] = useState("");
  const curricula = useQuery({
    queryKey: ["org-curricula", id],
    queryFn: () =>
      api<CurriculumAssignment[]>(
        `/api/organisations/${id}/workspace/curricula`,
      ),
  });
  const links = useQuery({
    queryKey: ["guardian-links", id, linkPage],
    queryFn: () =>
      api<Page<GuardianLink>>(
        `/api/organisations/${id}/workspace/guardian-links?page=${linkPage}&pageSize=25`,
      ),
    enabled: can(access, "memberships.manage"),
  });
  const members = useQuery({
    queryKey: ["guardian-members-picker", id, memberPage, memberText],
    queryFn: () =>
      api<Page<Member>>(
        `/api/organisations/${id}/workspace/members?` +
          queryString({ pageSize: 100, page: memberPage, text: memberText }),
      ),
    enabled: linking,
  });
  return (
    <div className="two-column">
      <section className="card">
        <h2>Assigned curriculum</h2>
        <QueryState query={curricula}>
          {(items) =>
            items.length ? (
              items.map((x) => (
                <div className="assignment" key={x.id}>
                  <Link to={"/app/curriculum?curriculumId=" + x.curriculumId}>
                    {x.name}
                  </Link>
                  <Badge>{x.isActive ? "Active" : "Inactive"}</Badge>
                  {can(access, "organisations.manage") && (
                    <Action
                      run={async () => {
                        await api(
                          `/api/organisations/${id}/workspace/curricula/${x.id}/active`,
                          { method: "PATCH", body: { isActive: !x.isActive } },
                        );
                        await client.invalidateQueries({
                          queryKey: ["org-curricula", id],
                        });
                      }}
                    >
                      {x.isActive ? "Deactivate" : "Reactivate"}
                    </Action>
                  )}
                </div>
              ))
            ) : (
              <p>No curriculum assigned yet.</p>
            )
          }
        </QueryState>
        {can(access, "organisations.manage") && (
          <>
            <NodePicker
              onSelect={(type, node, name) => {
                if (type === "CurriculumVersion")
                  setVersion({ id: node, name });
              }}
            />
            {version && (
              <Action
                run={async () => {
                  await api(`/api/organisations/${id}/workspace/curricula`, {
                    method: "POST",
                    body: { targetId: version.id },
                  });
                  setVersion(undefined);
                  await client.invalidateQueries({
                    queryKey: ["org-curricula", id],
                  });
                }}
              >
                Assign {version.name}
              </Action>
            )}
          </>
        )}
      </section>
      {can(access, "memberships.manage") && (
        <section className="card">
          <div className="section-heading">
            <h2>Guardian relationships</h2>
            <button
              className="button secondary"
              onClick={() => setLinking(true)}
            >
              Link guardian & learner
            </button>
          </div>
          <QueryState query={links}>
            {(data) =>
              data.items.length ? (
                data.items.map((x) => (
                  <div className="assignment" key={x.id}>
                    <span>
                      {x.guardianName ?? "Guardian"} →{" "}
                      {x.learnerName ?? "Learner"}
                      <small>{x.isActive ? "Active" : "Inactive"}</small>
                    </span>
                    <Action
                      confirm="Change this guardian relationship's active state?"
                      run={async () => {
                        await api(
                          `/api/organisations/${id}/workspace/guardian-links/${x.id}/active`,
                          { method: "PATCH", body: { isActive: !x.isActive } },
                        );
                        await client.invalidateQueries({
                          queryKey: ["guardian-links", id],
                        });
                      }}
                    >
                      {x.isActive ? "Deactivate" : "Reactivate"}
                    </Action>
                  </div>
                ))
              ) : (
                <p>No guardian relationships yet.</p>
              )
            }
          </QueryState>
          <Pagination
            page={linkPage}
            hasMore={links.data?.hasMore ?? false}
            onChange={setLinkPage}
          />
          {linking && (
            <Modal
              title="Link guardian and learner"
              onClose={() => setLinking(false)}
            >
              <SearchBar
                value={memberText}
                onSearch={(value) => {
                  setMemberText(value);
                  setMemberPage(1);
                }}
                placeholder="Find guardian or learner by name…"
              />
              <Pagination
                page={memberPage}
                hasMore={members.data?.hasMore ?? false}
                onChange={setMemberPage}
              />
              <QueryState query={members}>
                {(data) => (
                  <Form
                    fields={[
                      {
                        name: "guardianMembershipId",
                        label: "Guardian",
                        type: "select",
                        required: true,
                        options: data.items
                          .filter(
                            (x) =>
                              x.roleCodes.includes("ParentGuardian") &&
                              x.isActive,
                          )
                          .map((x) => ({
                            value: x.id,
                            label: x.firstName + " " + x.lastName,
                          })),
                      },
                      {
                        name: "learnerMembershipId",
                        label: "Learner",
                        type: "select",
                        required: true,
                        options: data.items
                          .filter(
                            (x) =>
                              x.roleCodes.includes("Learner") && x.isActive,
                          )
                          .map((x) => ({
                            value: x.id,
                            label: x.firstName + " " + x.lastName,
                          })),
                      },
                    ]}
                    submit={async (values) => {
                      await api(
                        `/api/organisations/${id}/workspace/guardian-links`,
                        { method: "POST", body: values },
                      );
                      setLinking(false);
                      await client.invalidateQueries({
                        queryKey: ["guardian-links", id],
                      });
                    }}
                  />
                )}
              </QueryState>
            </Modal>
          )}
        </section>
      )}
    </div>
  );
}
