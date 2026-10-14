import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, upload } from "../../api/client";
import { Badge, ErrorNotice, QueryState, notify } from "../../components/ui";
import type { Configuration, LocalSource, Source, WebSource } from "./types";

export default function SourceExplorer({
  path,
  title,
  language,
  editable,
  configuration,
  prepare,
  changed,
}: {
  path: string;
  title: string;
  language: string;
  editable: boolean;
  configuration?: Configuration;
  prepare: () => Promise<unknown>;
  changed: (source: Source | null, removedId?: string) => void;
}) {
  const client = useQueryClient();
  const [query, setQuery] = useState(title);
  const [searchLanguage, setSearchLanguage] = useState(language);
  const [resourceType, setResourceType] = useState("");
  const [local, setLocal] = useState<LocalSource[]>([]);
  const [web, setWeb] = useState<WebSource[]>([]);
  const [tab, setTab] = useState("mwabu");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const [url, setUrl] = useState("");
  const [rights, setRights] = useState(false);
  const [file, setFile] = useState<File>();
  const [progress, setProgress] = useState(0);
  const abort = useRef<AbortController | null>(null);
  const sources = useQuery({
    queryKey: ["lesson-sources", path],
    queryFn: ({ signal }) => api<Source[]>(path + "/sources", { signal }),
  });
  useEffect(() => () => abort.current?.abort(), []);
  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(undefined);
    try {
      await prepare();
      await action();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }
  async function refresh() {
    await client.invalidateQueries({ queryKey: ["lesson-sources", path] });
  }
  async function attach(body: {
    contentItemId?: string;
    url?: string;
    title?: string;
  }) {
    const source = await api<Source>(path + "/sources", {
      method: "POST",
      body,
    });
    const selected = await api<Source>(path + `/sources/${source.id}`, {
      method: "PATCH",
      body: { isSelected: true, isExcluded: false, priority: 50 },
    });
    changed(selected);
    await refresh();
    notify("Reference attached and selected.");
  }
  return (
    <section
      className="panel source-explorer"
      aria-label="Teacher Source Explorer"
    >
      <h2>Teacher Source Explorer</h2>
      <p>
        Select resources deliberately. A search result is not a read source;
        licence-unknown external material is link-only. Source text cannot
        change official curriculum records.
      </p>
      <div role="group" aria-label="Source library">
        <button
          className={tab === "mwabu" ? "button" : "button secondary"}
          onClick={() => setTab("mwabu")}
        >
          Mwabu library
        </button>
        <button
          className={tab === "web" ? "button" : "button secondary"}
          onClick={() => setTab("web")}
        >
          Public internet
        </button>
        <button
          className={tab === "documents" ? "button" : "button secondary"}
          onClick={() => setTab("documents")}
        >
          My reference documents
        </button>
      </div>
      {!!error && <ErrorNotice error={error} />}
      {tab !== "documents" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void run(async () => {
              const body = {
                text: query,
                language: searchLanguage,
                resourceType: resourceType || null,
                multipleQueries: tab === "web",
              };
              if (tab === "mwabu")
                setLocal(
                  await api<LocalSource[]>(path + "/discover/local", {
                    method: "POST",
                    body,
                  }),
                );
              else
                setWeb(
                  await api<WebSource[]>(path + "/discover/web", {
                    method: "POST",
                    body,
                  }),
                );
            });
          }}
        >
          <div className="lesson-grid">
            <label>
              Search educational sources
              <input
                required={tab === "web"}
                maxLength={200}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
            </label>
            <label>
              Discovery language
              <input
                maxLength={35}
                value={searchLanguage}
                onChange={(e) => setSearchLanguage(e.target.value)}
              />
            </label>
            <label>
              Resource type
              <input
                maxLength={64}
                placeholder="lesson-plan, worksheet, pdf…"
                value={resourceType}
                onChange={(e) => setResourceType(e.target.value)}
              />
            </label>
          </div>
          <button
            className="button"
            disabled={
              busy ||
              (tab === "web" &&
                (!configuration?.searchConfigured ||
                  !configuration.organisationSearchApproved))
            }
          >
            {busy
              ? "Searching…"
              : tab === "web"
                ? "Search public educational sources"
                : "Search Mwabu resources"}
          </button>
          {tab === "web" &&
            (!configuration?.searchConfigured ||
              !configuration.organisationSearchApproved) && (
              <p role="status">
                Public search is not configured/approved. You can still use the
                Mwabu library and teacher-owned references.
              </p>
            )}
        </form>
      )}
      {tab === "mwabu" && (
        <div className="source-results">
          {local.map((item) => (
            <article key={item.id}>
              <Badge>Mwabu · Published</Badge>
              <h3>{item.title}</h3>
              <p>{item.summary}</p>
              <small>
                {item.language} · {item.type} · relevance score {item.score}
              </small>
              <ul>
                {item.reasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ul>
              <Link
                className="button secondary"
                to={`/app/resources/${item.id}`}
              >
                Preview resource
              </Link>{" "}
              {editable && (
                <button
                  className="button"
                  disabled={busy}
                  onClick={() =>
                    void run(() => attach({ contentItemId: item.id }))
                  }
                >
                  Attach resource
                </button>
              )}
            </article>
          ))}
        </div>
      )}
      {tab === "web" && (
        <>
          <p>
            Broad discovery is not restricted to a fixed website list. Preferred
            domains affect ranking; security and publisher access restrictions
            still apply.
          </p>
          <div className="source-results">
            {web.map((item) => (
              <article key={item.url}>
                <Badge>External · Discovered only</Badge>
                <h3>
                  <a href={item.url} target="_blank" rel="noopener noreferrer">
                    {item.title}
                  </a>
                </h3>
                <p>{item.description}</p>
                <small>
                  {item.publisher ?? "Publisher unverified"} · contents not read
                  · licence unknown
                </small>
                {editable && (
                  <button
                    className="button"
                    disabled={busy}
                    onClick={() =>
                      void run(() =>
                        attach({ url: item.url, title: item.title }),
                      )
                    }
                  >
                    Select external reference
                  </button>
                )}
              </article>
            ))}
          </div>
          {editable && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void run(async () => {
                  await attach({ url });
                  setUrl("");
                });
              }}
            >
              <label>
                Attach a public source URL
                <input
                  type="url"
                  required
                  maxLength={2000}
                  value={url}
                  onChange={(e) => setUrl(e.target.value)}
                />
              </label>
              <button className="button secondary" disabled={busy}>
                Attach source link
              </button>
            </form>
          )}
        </>
      )}
      {tab === "documents" && (
        <fieldset disabled={!editable || busy}>
          <legend>Teacher-supplied references</legend>
          <p>
            PDF, DOCX, UTF-8 text or Markdown, maximum 2 MiB. Only a bounded
            text excerpt is retained; the original upload is deleted after
            extraction. Remove personal/learner information first.
          </p>
          <label>
            Choose reference document
            <input
              type="file"
              accept=".pdf,.docx,.txt,.md"
              onChange={(e) => setFile(e.target.files?.[0])}
            />
          </label>
          <label className="checkbox">
            <input
              type="checkbox"
              checked={rights}
              onChange={(e) => setRights(e.target.checked)}
            />
            I have permission to use this material for this lesson
          </label>
          <button
            className="button"
            disabled={!rights || !file || file.size > 2097152}
            onClick={() =>
              void run(async () => {
                abort.current = new AbortController();
                await upload(
                  path + "/documents",
                  file!,
                  "Document",
                  setProgress,
                  abort.current.signal,
                  { rightsConfirmed: "true" },
                );
                await refresh();
                notify(
                  "Reference document extracted; select it below to use it.",
                );
              })
            }
          >
            Upload reference
          </button>
        </fieldset>
      )}
      {busy && tab === "documents" && (
        <div role="status">
          <progress value={progress} max={100} />
          Upload/extraction in progress{" "}
          <button
            className="button secondary"
            onClick={() => abort.current?.abort()}
          >
            Cancel reference upload
          </button>
        </div>
      )}
      <h3>Attached references and evidence</h3>
      <QueryState query={sources}>
        {(items) => (
          <>
            {items.length === 0 && (
              <p>
                No sources attached. Manual planning and AI without external
                references are available.
              </p>
            )}
            {items.map((source) => (
              <article className="lesson-source" key={source.id}>
                <header>
                  <h4>{source.title}</h4>
                  <Badge>
                    {source.kind} · {source.retrievalStatus}
                  </Badge>
                </header>
                <p>{source.evidenceDescription}</p>
                <small>{source.credibility}</small>
                <p>
                  Licence: {source.licence ?? "Unknown - reference/link only"}.{" "}
                  {source.reusePermitted
                    ? "Authorised evidence may ground a proposal; retain attribution."
                    : "Do not reproduce, redistribute or bundle this material."}
                </p>
                {source.permissionEvidence && (
                  <p>{source.permissionEvidence}</p>
                )}
                {source.url &&
                  (source.url.startsWith("/app/") ? (
                    <Link to={source.url}>Open original Mwabu resource</Link>
                  ) : (
                    <a
                      href={source.url}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      Open original source
                    </a>
                  ))}
                {source.evidencePreview && (
                  <details>
                    <summary>
                      Preview retained plain-text evidence (bounded excerpt)
                    </summary>
                    <pre>{source.evidencePreview}</pre>
                  </details>
                )}
                <div className="lesson-grid">
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={source.isSelected}
                      disabled={
                        !editable ||
                        busy ||
                        source.retrievalStatus === "Revoked"
                      }
                      onChange={(e) => {
                        const checked = e.target.checked;
                        void run(async () => {
                          const updated = await api<Source>(
                            path + `/sources/${source.id}`,
                            {
                              method: "PATCH",
                              body: {
                                isSelected: checked,
                                isExcluded: false,
                                priority: source.priority,
                              },
                            },
                          );
                          changed(updated);
                          await refresh();
                        });
                      }}
                    />
                    Use {source.title} as a reference
                  </label>
                  <label>
                    Source priority
                    <select
                      disabled={!editable || busy}
                      value={source.priority}
                      onChange={(e) => {
                        const priority = Number(e.target.value);
                        void run(async () => {
                          const updated = await api<Source>(
                            path + `/sources/${source.id}`,
                            {
                              method: "PATCH",
                              body: {
                                isSelected: source.isSelected,
                                isExcluded: source.isExcluded,
                                priority,
                              },
                            },
                          );
                          changed(updated);
                          await refresh();
                        });
                      }}
                    >
                      <option value={0}>Normal</option>
                      <option value={50}>Preferred</option>
                      <option value={100}>Highest priority</option>
                    </select>
                  </label>
                </div>
                {editable && (
                  <div className="lesson-actions">
                    {source.kind !== "TeacherDocument" && (
                      <button
                        className="button secondary"
                        disabled={
                          busy ||
                          (source.kind === "External" &&
                            !configuration?.organisationSearchApproved)
                        }
                        onClick={() =>
                          void run(async () => {
                            const updated = await api<Source>(
                              path + `/sources/${source.id}/retrieve`,
                              { method: "POST" },
                            );
                            changed(updated);
                            await refresh();
                          })
                        }
                      >
                        Retrieve permitted evidence
                      </button>
                    )}
                    <button
                      className="button secondary"
                      disabled={busy}
                      onClick={() =>
                        void run(async () => {
                          const updated = await api<Source>(
                            path + `/sources/${source.id}`,
                            {
                              method: "PATCH",
                              body: {
                                isSelected: false,
                                isExcluded: true,
                                priority: source.priority,
                              },
                            },
                          );
                          changed(updated);
                          await refresh();
                        })
                      }
                    >
                      Exclude source
                    </button>
                    <button
                      className="button secondary"
                      disabled={busy}
                      onClick={() => {
                        if (
                          window.confirm(
                            "Remove this source's retained evidence and selection? Historical citation metadata remains for revision integrity.",
                          )
                        )
                          void run(async () => {
                            await api(path + `/sources/${source.id}`, {
                              method: "DELETE",
                            });
                            changed(null, source.id);
                            await refresh();
                          });
                      }}
                    >
                      Remove evidence
                    </button>
                  </div>
                )}
              </article>
            ))}
          </>
        )}
      </QueryState>
    </section>
  );
}
