import { useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Upload, File, Trash2 } from "lucide-react";
import { api, queryString, upload } from "../../api/client";
import type {
  Content,
  Page,
  Asset,
  Tag,
  Collection,
  Mapping,
} from "../../api/types";
import { useSession } from "../../auth/Session";
import {
  Action,
  Badge,
  Empty,
  ErrorNotice,
  Form,
  Modal,
  PageTitle,
  Pagination,
  QueryState,
  SearchBar,
  type Field,
  type FormValues,
} from "../../components/ui";
import NodePicker from "../curriculum/NodePicker";
const contentFields: Field[] = [
  { name: "title", label: "Resource title", required: true, maxLength: 200 },
  {
    name: "slug",
    label: "URL name",
    required: true,
    maxLength: 200,
    hint: "Lowercase words separated by hyphens. Must be unique.",
  },
  {
    name: "summary",
    label: "Short introduction",
    type: "textarea",
    maxLength: 1000,
  },
  {
    name: "description",
    label: "Description / teaching notes",
    type: "textarea",
    maxLength: 10000,
  },
  {
    name: "contentType",
    label: "Resource type",
    required: true,
    maxLength: 64,
    defaultValue: "lesson",
    hint: "e.g. lesson, lesson-plan, teacher-guide, audio, video",
  },
  {
    name: "languageCode",
    label: "Language code",
    required: true,
    maxLength: 35,
    defaultValue: "en",
  },
  {
    name: "sortOrder",
    label: "Display order",
    type: "number",
    required: true,
    min: 0,
    defaultValue: 0,
  },
  {
    name: "estimatedDurationMinutes",
    label: "Suggested time (minutes)",
    type: "number",
    min: 1,
  },
  {
    name: "isDownloadable",
    label: "Allow downloads",
    type: "checkbox",
    defaultValue: true,
  },
];
export default function Management() {
  const { id } = useParams();
  const navigate = useNavigate();
  const session = useSession();
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [text, setText] = useState("");
  const [status, setStatus] = useState("");
  const [editing, setEditing] = useState(false);
  const [revision, setRevision] = useState(false);
  const allowed = session.can("content.manage", "global");
  const list = useQuery({
    queryKey: ["studio", page, text, status],
    queryFn: ({ signal }) =>
      api<Page<Content>>(
        "/api/content?" + queryString({ page, text, status, pageSize: 20 }),
        { signal },
      ),
    enabled: allowed && !id,
  });
  const detail = useQuery({
    queryKey: ["studio", id],
    queryFn: ({ signal }) => api<Content>("/api/content/" + id, { signal }),
    enabled: allowed && !!id,
  });
  const refresh = () => client.invalidateQueries({ queryKey: ["studio"] });
  if (!allowed)
    return (
      <Empty title="Content studio is not available in your scope">
        A platform content-management grant is required.
      </Empty>
    );
  async function save(values: FormValues) {
    const resource = await api<Content>(
      "/api/content" + (id && !revision ? "/" + id : ""),
      { method: id && !revision ? "PUT" : "POST", body: values },
    );
    setEditing(false);
    setRevision(false);
    await refresh();
    navigate("/app/content/" + resource.id);
  }
  const content = detail.data;
  return (
    <>
      <PageTitle
        eyebrow="CONTENT STUDIO"
        title={content?.title ?? "Thoughtful resources. Ready to teach."}
        actions={
          !id && (
            <button className="button" onClick={() => setEditing(true)}>
              <Plus size={16} />
              Create resource
            </button>
          )
        }
      >
        Shape, review and publish resources for the learning catalogue.
      </PageTitle>
      {!id ? (
        <>
          <section className="card catalogue-tools">
            <SearchBar
              value={text}
              onSearch={(value) => {
                setText(value);
                setPage(1);
              }}
              placeholder="Search content metadata, tags and mappings…"
            />
            <label>
              Status
              <select
                value={status}
                onChange={(e) => {
                  setStatus(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">All statuses</option>
                {["Draft", "InReview", "Published", "Archived"].map((x) => (
                  <option key={x}>{x}</option>
                ))}
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
                          <th>Resource</th>
                          <th>Type / language</th>
                          <th>Status</th>
                          <th>Last updated</th>
                        </tr>
                      </thead>
                      <tbody>
                        {data.items.map((x) => (
                          <tr key={x.id}>
                            <td>
                              <Link to={"/app/content/" + x.id}>{x.title}</Link>
                              <small>{x.summary}</small>
                            </td>
                            <td>
                              {x.contentType}
                              <small>{x.languageCode}</small>
                            </td>
                            <td>
                              <Badge>{x.status}</Badge>
                            </td>
                            <td>
                              {x.updatedAt
                                ? new Date(x.updatedAt).toLocaleDateString()
                                : "New resource"}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <Empty title="No resources match">
                    Create a resource or adjust your search.
                  </Empty>
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
      ) : (
        <QueryState query={detail}>
          {(resource) => (
            <>
              <div className="workflow card">
                <Badge>{resource.status}</Badge>
                {["Draft", "InReview"].includes(resource.status) ? (
                  <button
                    className="button secondary"
                    onClick={() => setEditing(true)}
                  >
                    Edit metadata
                  </button>
                ) : (
                  <button
                    className="button secondary"
                    onClick={() => {
                      setRevision(true);
                      setEditing(true);
                    }}
                  >
                    Create revised resource
                  </button>
                )}
                {resource.status === "Draft" && (
                  <Action
                    run={async () => {
                      await api(`/api/content/${id}/status`, {
                        method: "PATCH",
                        body: { status: "InReview" },
                      });
                      await refresh();
                    }}
                  >
                    Submit for review
                  </Action>
                )}
                {resource.status === "InReview" && (
                  <>
                    <Action
                      run={async () => {
                        await api(`/api/content/${id}/status`, {
                          method: "PATCH",
                          body: { status: "Draft" },
                        });
                        await refresh();
                      }}
                    >
                      Return to draft
                    </Action>
                    {session.can("content.publish", "global") && (
                      <Action
                        className="button"
                        confirm="Publish this resource? Published metadata and files become immutable."
                        run={async () => {
                          await api(`/api/content/${id}/status`, {
                            method: "PATCH",
                            body: { status: "Published" },
                          });
                          await refresh();
                        }}
                      >
                        Publish resource
                      </Action>
                    )}
                  </>
                )}
                {resource.status === "Published" && (
                  <>
                    <Link
                      className="button secondary"
                      to={"/app/resources/" + id}
                    >
                      Open published resource
                    </Link>
                    <Action
                      confirm="Archive this resource? It will be removed from the published catalogue and offline feeds."
                      run={async () => {
                        await api(`/api/content/${id}/archive`, {
                          method: "POST",
                        });
                        await refresh();
                      }}
                    >
                      Archive
                    </Action>
                  </>
                )}
              </div>
              <div className="two-column">
                <section className="card">
                  <h2>Resource details</h2>
                  <p className="preserve-lines">
                    {resource.description ??
                      resource.summary ??
                      "No description provided."}
                  </p>
                  <dl>
                    <dt>Type</dt>
                    <dd>{resource.contentType}</dd>
                    <dt>Language</dt>
                    <dd>{resource.languageCode}</dd>
                    <dt>Downloads</dt>
                    <dd>{resource.isDownloadable ? "Allowed" : "Disabled"}</dd>
                  </dl>
                  {["Published", "Archived"].includes(resource.status) && (
                    <p className="alert">
                      This resource is immutable. A revision creates a separate
                      draft; attach reviewed files to the new resource before
                      publishing.
                    </p>
                  )}
                </section>
                <Assets
                  id={resource.id}
                  editable={["Draft", "InReview"].includes(resource.status)}
                />
              </div>
              <Assignments
                id={resource.id}
                editable={["Draft", "InReview"].includes(resource.status)}
              />
            </>
          )}
        </QueryState>
      )}
      {editing && (
        <Modal
          title={
            revision
              ? "Create a revised resource"
              : id
                ? "Edit resource"
                : "Create resource"
          }
          onClose={() => {
            setEditing(false);
            setRevision(false);
          }}
        >
          <Form
            fields={contentFields}
            initial={
              content
                ? {
                    ...content,
                    slug: revision
                      ? content.slug.slice(0, 150) +
                        "-revision-" +
                        crypto.randomUUID().slice(0, 8)
                      : content.slug,
                  }
                : {}
            }
            submit={save}
            onCancel={() => {
              setEditing(false);
              setRevision(false);
            }}
          />
        </Modal>
      )}
    </>
  );
}
function Assets({ id, editable }: { id: string; editable: boolean }) {
  const client = useQueryClient();
  const [progress, setProgress] = useState<number>();
  const [error, setError] = useState<unknown>();
  const [type, setType] = useState("Document");
  const abort = useRef<AbortController | null>(null);
  const assets = useQuery({
    queryKey: ["studio-assets", id],
    queryFn: ({ signal }) =>
      api<Asset[]>(`/api/content/${id}/assets`, { signal }),
  });
  const settings = useQuery({
    queryKey: ["upload-settings"],
    queryFn: () => api<{ maxUploadBytes: number }>("/api/workspace/settings"),
  });
  return (
    <section className="card">
      <h2>Resource files</h2>
      <QueryState query={assets}>
        {(items) =>
          items.length ? (
            <ul className="file-list">
              {items.map((x) => (
                <li key={x.id}>
                  <File size={18} />
                  <a href={`/api/content/${id}/assets/${x.id}`}>{x.fileName}</a>
                  <small>{(x.fileSizeBytes / 1024 / 1024).toFixed(1)} MB</small>
                  {editable && (
                    <Action
                      className="icon-button"
                      confirm={"Remove " + x.fileName + "?"}
                      run={async () => {
                        await api(`/api/content/${id}/assets/${x.id}`, {
                          method: "DELETE",
                        });
                        await client.invalidateQueries({
                          queryKey: ["studio-assets", id],
                        });
                      }}
                    >
                      <Trash2 size={16} />
                      <span className="sr-only">Remove file</span>
                    </Action>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <p className="muted">Attach a document, audio, video or image.</p>
          )
        }
      </QueryState>
      {error !== undefined && <ErrorNotice error={error} />}{" "}
      {editable && (
        <form
          className="form"
          onSubmit={async (e) => {
            e.preventDefault();
            const form = e.currentTarget;
            const file = new FormData(form).get("file");
            if (!(file instanceof window.File) || !file.size || !settings.data)
              return;
            if (file.size > settings.data.maxUploadBytes) {
              setError(
                new Error(
                  "The selected file exceeds the configured upload limit.",
                ),
              );
              return;
            }
            abort.current = new AbortController();
            setError(undefined);
            setProgress(0);
            try {
              await upload(
                `/api/content/${id}/assets`,
                file,
                type,
                setProgress,
                abort.current.signal,
              );
              form.reset();
              await client.invalidateQueries({
                queryKey: ["studio-assets", id],
              });
            } catch (err) {
              setError(err);
            } finally {
              setProgress(undefined);
              abort.current = null;
            }
          }}
        >
          <label>
            File type
            <select value={type} onChange={(e) => setType(e.target.value)}>
              {[
                "Document",
                "Audio",
                "Video",
                "Image",
                "Animation",
                "Package",
                "Other",
              ].map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </label>
          <label>
            Choose file
            <input
              type="file"
              name="file"
              required
              disabled={progress !== undefined}
            />
            <small>
              {settings.data
                ? "Maximum " +
                  Math.floor(settings.data.maxUploadBytes / 1024 / 1024) +
                  " MB. Executable formats are download-only."
                : "Loading upload limit…"}
            </small>
          </label>
          {progress !== undefined && (
            <>
              <progress
                max={100}
                value={progress}
                aria-label="Upload progress"
              />
              <span>{progress}% uploaded</span>
              <button
                type="button"
                className="button secondary"
                onClick={() => abort.current?.abort()}
              >
                Cancel upload
              </button>
            </>
          )}
          <button
            className="button secondary"
            disabled={progress !== undefined || !settings.data}
          >
            <Upload size={16} />
            Upload file
          </button>
        </form>
      )}
    </section>
  );
}
function Assignments({ id, editable }: { id: string; editable: boolean }) {
  const client = useQueryClient();
  const [mapping, setMapping] = useState<{
    nodeType: string;
    nodeId: string;
    name: string;
  }>();
  const mappings = useQuery({
    queryKey: ["mappings", id],
    queryFn: () => api<Mapping[]>(`/api/content/${id}/curriculum-mappings`),
  });
  const tags = useQuery({
    queryKey: ["content-tags", id],
    queryFn: () => api<Tag[]>(`/api/content/${id}/tags`),
  });
  const collections = useQuery({
    queryKey: ["content-collections", id],
    queryFn: () =>
      api<{ collection: Collection }[]>(`/api/content/${id}/collections`),
  });
  async function remove(kind: string, target: string) {
    await api(`/api/content/${id}/${kind}/${target}`, { method: "DELETE" });
    await client.invalidateQueries();
  }
  return (
    <div className="two-column">
      <section className="card">
        <h2>Curriculum connections</h2>
        <QueryState query={mappings}>
          {(items) =>
            items.length ? (
              items.map((x) => (
                <div key={x.id} className="assignment">
                  <span>
                    {x.nodeType.replace(/([A-Z])/g, " $1").trim()}
                    {x.nodeName && ": " + x.nodeName}
                  </span>
                  {editable && (
                    <Action
                      confirm="Remove this curriculum connection?"
                      run={() => remove("curriculum-mappings", x.id)}
                    >
                      Remove
                    </Action>
                  )}
                </div>
              ))
            ) : (
              <p>No curriculum connections yet.</p>
            )
          }
        </QueryState>
        {editable && (
          <>
            <NodePicker
              onSelect={(nodeType, nodeId, name) =>
                setMapping({ nodeType, nodeId, name })
              }
            />
            {mapping && (
              <Action
                run={async () => {
                  await api(`/api/content/${id}/curriculum-mappings`, {
                    method: "POST",
                    body: mapping,
                  });
                  setMapping(undefined);
                  await client.invalidateQueries({
                    queryKey: ["mappings", id],
                  });
                }}
              >
                Connect {mapping.name}
              </Action>
            )}
          </>
        )}
      </section>
      <section className="card">
        <h2>Tags & collections</h2>
        <QueryState query={tags}>
          {(items) => (
            <div className="tag-list">
              {items.map((x) => (
                <span key={x.id}>
                  <Badge>{x.name}</Badge>
                  {editable && (
                    <Action
                      className="button text"
                      run={() => remove("tags", x.id)}
                    >
                      Remove
                    </Action>
                  )}
                </span>
              ))}
            </div>
          )}
        </QueryState>
        {editable && <TaxonomyAssign id={id} kind="tags" />}
        <hr />
        <QueryState query={collections}>
          {(items) => (
            <>
              {items.map((x) => (
                <div className="assignment" key={x.collection.id}>
                  {x.collection.name}
                  {editable && (
                    <Action run={() => remove("collections", x.collection.id)}>
                      Remove
                    </Action>
                  )}
                </div>
              ))}
            </>
          )}
        </QueryState>
        {editable && <TaxonomyAssign id={id} kind="collections" />}
      </section>
    </div>
  );
}
function TaxonomyAssign({
  id,
  kind,
}: {
  id: string;
  kind: "tags" | "collections";
}) {
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState("");
  const client = useQueryClient();
  const query = useQuery({
    queryKey: ["taxonomy-picker", kind, page],
    queryFn: () =>
      api<Page<Tag>>(`/api/${kind}/search?page=${page}&pageSize=25`),
  });
  return (
    <div className="form">
      <label>
        Add {kind === "tags" ? "tag" : "collection"}
        <select value={selected} onChange={(e) => setSelected(e.target.value)}>
          <option value="">Choose…</option>
          {query.data?.items.map((x) => (
            <option key={x.id} value={x.id}>
              {x.name}
            </option>
          ))}
        </select>
      </label>
      {query.error && <ErrorNotice error={query.error} />}
      <Pagination
        page={page}
        hasMore={query.data?.hasMore ?? false}
        onChange={setPage}
      />
      {selected && (
        <Action
          run={async () => {
            await api(`/api/content/${id}/${kind}`, {
              method: "POST",
              body: { [kind === "tags" ? "tagId" : "collectionId"]: selected },
            });
            setSelected("");
            await client.invalidateQueries({
              queryKey: [
                kind === "tags" ? "content-tags" : "content-collections",
                id,
              ],
            });
          }}
        >
          Add selected {kind === "tags" ? "tag" : "collection"}
        </Action>
      )}
    </div>
  );
}
