import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { api, queryString } from "../../api/client";
import type {
  Project,
  ProjectDetails,
  Page,
  Organisation,
  Member,
  Content,
} from "../../api/types";
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
import NodePicker from "../curriculum/NodePicker";
export default function Projects() {
  const session = useSession();
  const org = session.organisationId;
  const { id } = useParams();
  const client = useQueryClient();
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [text, setText] = useState("");
  const [editing, setEditing] = useState(false);
  const [version, setVersion] = useState<string | null>(null);
  const allowed = !!org && session.can("projects.read");
  const manage = session.can("projects.manage");
  const list = useQuery({
    queryKey: ["projects", org, page, text],
    queryFn: () =>
      api<Page<Project>>(
        `/api/organisations/${org}/projects?` +
          queryString({ page, text, pageSize: 20 }),
      ),
    enabled: allowed && !id,
  });
  const detail = useQuery({
    queryKey: ["project", org, id],
    queryFn: () =>
      api<ProjectDetails>(`/api/organisations/${org}/projects/${id}`),
    enabled: allowed && !!id,
  });
  if (!allowed)
    return <Empty title="Select an organisation with project access" />;
  const fields: Field[] = [
    { name: "name", label: "Project name", required: true, maxLength: 200 },
    { name: "code", label: "Project code", required: true, maxLength: 50 },
    {
      name: "description",
      label: "Programme description",
      type: "textarea",
      maxLength: 4000,
    },
    {
      name: "status",
      label: "Status",
      required: true,
      type: "select",
      defaultValue: "Planned",
      options: ["Planned", "Active", "Completed", "Archived"].map((value) => ({
        value,
        label: value,
      })),
    },
    { name: "startsAt", label: "Start date", type: "date" },
    { name: "endsAt", label: "End date", type: "date" },
  ];
  return (
    <>
      <PageTitle
        eyebrow="PROGRAMMES & PROJECTS"
        title={detail.data?.project.name ?? "Learning, with a shared purpose."}
        actions={
          manage && (
            <button
              className="button"
              onClick={() => {
                setVersion(detail.data?.project.curriculumVersionId ?? null);
                setEditing(true);
              }}
            >
              <Plus size={16} />
              {id ? "Edit project" : "Create project"}
            </button>
          )
        }
      >
        Projects belong to the selected organisation. Site assignment needs
        authority over each site.
      </PageTitle>
      {!id ? (
        <>
          <SearchBar
            value={text}
            onSearch={(value) => {
              setText(value);
              setPage(1);
            }}
            placeholder="Search projects…"
          />
          <QueryState query={list}>
            {(data) => (
              <>
                {data.items.length ? (
                  <div className="node-grid">
                    {data.items.map((x) => (
                      <Link
                        key={x.id}
                        className="card"
                        to={"/app/projects/" + x.id}
                      >
                        <Badge>{x.status}</Badge>
                        <h2>{x.name}</h2>
                        <p>{x.description}</p>
                        <small>{x.code}</small>
                      </Link>
                    ))}
                  </div>
                ) : (
                  <Empty title="No projects in this organisation" />
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
          {(data) => (
            <>
              <section className="card">
                <Badge>{data.project.status}</Badge>
                <p className="preserve-lines">{data.project.description}</p>
                <dl>
                  <dt>Programme dates</dt>
                  <dd>
                    {data.project.startsAt?.slice(0, 10) ?? "Not scheduled"} —{" "}
                    {data.project.endsAt?.slice(0, 10) ?? "Open ended"}
                  </dd>
                </dl>
              </section>
              <div className="node-grid">
                {(["sites", "participants", "resources"] as const).map(
                  (kind) => (
                    <section className="card" key={kind}>
                      <h2>
                        {kind === "sites"
                          ? "Schools & sites"
                          : kind === "participants"
                            ? "Project participants"
                            : "Assigned learning resources"}
                      </h2>
                      {data[kind].length ? (
                        data[kind].map((x) => (
                          <div className="assignment" key={x.id}>
                            <span>
                              {kind === "resources" ? (
                                <Link to={"/app/resources/" + x.targetId}>
                                  {x.name}
                                </Link>
                              ) : (
                                x.name
                              )}
                            </span>
                            {manage && (
                              <Action
                                confirm="Remove this assignment from the project?"
                                run={async () => {
                                  await api(
                                    `/api/organisations/${org}/projects/${id}/${kind}/${x.id}`,
                                    { method: "DELETE" },
                                  );
                                  await client.invalidateQueries({
                                    queryKey: ["project", org, id],
                                  });
                                }}
                              >
                                Remove
                              </Action>
                            )}
                          </div>
                        ))
                      ) : (
                        <p className="muted">No {kind} assigned.</p>
                      )}
                      {manage && (
                        <ProjectPicker
                          org={org!}
                          project={id!}
                          kind={kind}
                          sites={data.sites}
                        />
                      )}
                    </section>
                  ),
                )}
              </div>
            </>
          )}
        </QueryState>
      )}
      {editing && (
        <Modal
          title={id ? "Edit project" : "Create project"}
          onClose={() => setEditing(false)}
        >
          <h3>Curriculum context (optional)</h3>
          <NodePicker
            onSelect={(type, node) => {
              if (type === "CurriculumVersion") setVersion(node);
            }}
          />
          {version && (
            <button className="button text" onClick={() => setVersion(null)}>
              Remove curriculum context
            </button>
          )}
          <Form
            fields={fields}
            initial={
              detail.data
                ? {
                    ...detail.data.project,
                    startsAt: detail.data.project.startsAt?.slice(0, 10),
                    endsAt: detail.data.project.endsAt?.slice(0, 10),
                  }
                : {}
            }
            submit={async (values) => {
              const project = await api<Project>(
                `/api/organisations/${org}/projects` + (id ? "/" + id : ""),
                {
                  method: id ? "PUT" : "POST",
                  body: {
                    ...values,
                    startsAt: values.startsAt
                      ? new Date(String(values.startsAt)).toISOString()
                      : null,
                    endsAt: values.endsAt
                      ? new Date(String(values.endsAt)).toISOString()
                      : null,
                    curriculumVersionId: version,
                  },
                },
              );
              setEditing(false);
              await client.invalidateQueries({ queryKey: ["projects"] });
              await client.invalidateQueries({ queryKey: ["project"] });
              navigate("/app/projects/" + project.id);
            }}
            onCancel={() => setEditing(false)}
          />
        </Modal>
      )}
    </>
  );
}
function ProjectPicker({
  org,
  project,
  kind,
  sites,
}: {
  org: string;
  project: string;
  kind: "sites" | "participants" | "resources";
  sites: { targetId: string; name: string }[];
}) {
  const session = useSession();
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState("");
  const [memberOrganisation, setMemberOrganisation] = useState(org);
  const query = useQuery({
    queryKey: ["project-picker", kind, org, memberOrganisation, text, page],
    queryFn: async () => {
      const params = queryString({ page, pageSize: 20, text });
      if (kind === "sites") {
        const data = await api<Page<Organisation>>(
          "/api/organisations/search?" + params,
        );
        return {
          ...data,
          items: data.items.map((x) => ({ id: x.id, name: x.name })),
        };
      }
      if (kind === "participants") {
        const data = await api<Page<Member>>(
          `/api/organisations/${memberOrganisation}/workspace/members?` +
            params,
        );
        return {
          ...data,
          items: data.items
            .filter((x) => x.isActive)
            .map((x) => ({ id: x.id, name: x.firstName + " " + x.lastName })),
        };
      }
      const data = await api<Page<Content>>("/api/learning/content?" + params);
      return {
        ...data,
        hasMore: page * data.pageSize < (data.totalCount ?? 0),
        items: data.items.map((x) => ({ id: x.id, name: x.title })),
      };
    },
    enabled: open,
  });
  if (kind === "sites" && !session.can("organisations.read", "platform"))
    return (
      <p className="muted">
        Ask a platform administrator to assign sites with consent from each
        organisation.
      </p>
    );
  return (
    <>
      <button className="button secondary" onClick={() => setOpen(true)}>
        Assign{" "}
        {kind === "resources"
          ? "resource"
          : kind === "participants"
            ? "participant"
            : "site"}
      </button>
      {open && (
        <Modal title={"Assign " + kind} onClose={() => setOpen(false)}>
          {kind === "participants" && (
            <label>
              Participant organisation
              <select
                value={memberOrganisation}
                onChange={(event) => {
                  setMemberOrganisation(event.target.value);
                  setPage(1);
                  setSelected("");
                }}
              >
                <option value={org}>Project organisation</option>
                {sites.map((site) => (
                  <option key={site.targetId} value={site.targetId}>
                    {site.name}
                  </option>
                ))}
              </select>
              <small>
                Reading and assigning a site's members requires permissions in
                that site.
              </small>
            </label>
          )}
          <SearchBar
            value={text}
            onSearch={(value) => {
              setText(value);
              setPage(1);
            }}
          />
          <QueryState query={query}>
            {(data) => (
              <>
                <label>
                  Select
                  <select
                    value={selected}
                    onChange={(e) => setSelected(e.target.value)}
                  >
                    <option value="">Choose…</option>
                    {data.items.map((x) => (
                      <option key={x.id} value={x.id}>
                        {x.name}
                      </option>
                    ))}
                  </select>
                </label>
                <Pagination
                  page={page}
                  hasMore={data.hasMore ?? false}
                  onChange={setPage}
                />
                {selected && (
                  <Action
                    run={async () => {
                      await api(
                        `/api/organisations/${org}/projects/${project}/${kind}`,
                        { method: "POST", body: { targetId: selected } },
                      );
                      setOpen(false);
                      setSelected("");
                      await client.invalidateQueries({
                        queryKey: ["project", org, project],
                      });
                    }}
                  >
                    Assign to project
                  </Action>
                )}
              </>
            )}
          </QueryState>
        </Modal>
      )}
    </>
  );
}
