import { useEffect, useRef, useState } from "react";
import {
  Link,
  useBeforeUnload,
  useBlocker,
  useNavigate,
} from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, Printer, Save, Sparkles } from "lucide-react";
import { api, ensureSession } from "../../api/client";
import { useSession } from "../../auth/Session";
import {
  Badge,
  ErrorNotice,
  Modal,
  PageTitle,
  Pagination,
  QueryState,
  notify,
} from "../../components/ui";
import type { Page } from "../../api/types";
import type {
  Configuration,
  Detail,
  Generation,
  LessonDocument,
  LessonStatus,
  Source,
} from "./types";
import {
  authoritative,
  lessonBase,
  proposalDocument,
  sectionDefinitions,
} from "./types";
import { useLessonDraft } from "./useLessonDraft";
import SetupForm from "./LessonSetup";
import SourceExplorer from "./SourceExplorer";
import RevisionPanel, { DocumentComparison } from "./RevisionPanel";
import LessonSharing from "./LessonSharing";
import { LessonPrint } from "./LessonPrint";
import { MinistryForm } from "./MinistryForm";
import { offlineLessonHtml } from "./offlineLesson";

export default function LessonEditor({
  initial,
  organisation,
}: {
  initial: Detail;
  organisation: string;
}) {
  const session = useSession();
  const client = useQueryClient();
  const navigate = useNavigate();
  const draft = useLessonDraft(initial, organisation);
  const path = lessonBase(organisation) + "/" + initial.lesson.id;
  const [tab, setTab] = useState("editor");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const [operation, setOperation] = useState("generate");
  const [sectionCode, setSectionCode] = useState("");
  const [selectedPassage, setSelectedPassage] = useState<{
    code: string;
    text: string;
  }>();
  const [instructions, setInstructions] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [proposal, setProposal] = useState<Generation>();
  const [note, setNote] = useState("");
  const [template, setTemplate] = useState(false);
  const [generationPage, setGenerationPage] = useState(1);
  const abort = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const configuration = useQuery({
    queryKey: ["lesson-configuration", organisation],
    queryFn: ({ signal }) =>
      api<Configuration>(lessonBase(organisation) + "/configuration", {
        signal,
      }),
  });
  const sources = useQuery({
    queryKey: ["lesson-sources", path],
    queryFn: ({ signal }) => api<Source[]>(path + "/sources", { signal }),
  });
  const generationHistory = useQuery({
    queryKey: ["lesson-generations", path, generationPage],
    queryFn: ({ signal }) =>
      api<Page<Generation>>(
        path + `/generations?page=${generationPage}&pageSize=10`,
        { signal },
      ),
    enabled: tab === "ai" && initial.lesson.ownerUserId === session.user?.id,
  });
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      draft.dirty && currentLocation.pathname !== nextLocation.pathname,
  );
  useBeforeUnload((e) => {
    if (draft.dirty) {
      e.preventDefault();
      e.returnValue = "";
    }
  });
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      abort.current?.abort();
    };
  }, []);
  const editable = draft.server.canEdit && !busy;
  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(undefined);
    try {
      await action();
    } catch (failure) {
      if (mounted.current) setError(failure);
    } finally {
      if (mounted.current) setBusy(false);
    }
  }
  async function changedServer(value: Detail) {
    draft.adopt(value);
    await client.invalidateQueries({ queryKey: ["lessons", organisation] });
    await client.invalidateQueries({ queryKey: ["lesson-revisions", path] });
  }
  async function transition(status: LessonStatus) {
    if (
      !window.confirm(
        `Move this lesson to ${status}? Publication requires independent review and makes this version immutable.`,
      )
    )
      return;
    await run(async () => {
      const saved = await draft.save();
      const next = await api<Detail>(path + "/status", {
        method: "PATCH",
        body: {
          expectedRevision: saved.lesson.revision,
          status,
          note,
          organisationTemplate: template,
        },
      });
      await changedServer(next);
      notify(`Lesson moved to ${status}.`);
      if (status === "Draft" && next.lesson.ownerUserId !== session.user?.id)
        navigate("/app/lessons");
    });
  }
  async function generate() {
    await run(async () => {
      const saved = await draft.save();
      abort.current = new AbortController();
      if (!(await ensureSession(180)))
        throw new Error(
          "Sign in again before starting generation; your draft is saved.",
        );
      const generated = await api<Generation>(path + "/generations", {
        method: "POST",
        signal: abort.current.signal,
        retryAuth: false,
        body: {
          expectedRevision: saved.lesson.revision,
          operation,
          sectionCode: sectionCode || null,
          instructions,
          confirmNoPersonalData: confirmed,
          selectedText:
            selectedPassage?.code === sectionCode ? selectedPassage.text : null,
        },
      });
      if (mounted.current) setProposal(generated);
      await client.invalidateQueries({
        queryKey: ["lesson-generations", path],
      });
      await client.invalidateQueries({
        queryKey: ["lesson-usage", organisation],
      });
    });
  }
  function sourceChanged(source: Source | null, removedId?: string) {
    draft.update((document) => ({
      ...document,
      sections: document.sections.map((section) => ({
        ...section,
        sourceIds: section.sourceIds.filter(
          (id) =>
            id !== removedId &&
            (id !== source?.id || (source.isSelected && !source.isExcluded)),
        ),
      })),
    }));
  }
  const selectedSources = (sources.data ?? []).filter(
    (source) =>
      source.isSelected &&
      !source.isExcluded &&
      source.retrievalStatus !== "Revoked",
  );
  function restore(document: LessonDocument) {
    const available = new Set(selectedSources.map((x) => x.id));
    draft.update(() => ({
      ...document,
      sections: document.sections.map((x) => ({
        ...x,
        sourceIds: x.sourceIds.filter((id) => available.has(id)),
      })),
    }));
    setTab("editor");
  }
  const orgName =
    session.memberships.find(
      (x) => x.membership.organisationId === organisation,
    )?.organisationName ?? "Authorised organisation";
  async function exportPdf() {
    await run(async () => {
      await draft.save();
      if (!(await ensureSession()))
        throw new Error("Sign in again to export the saved lesson.");
      const link = document.createElement("a");
      link.href = path + "/pdf";
      link.download = "mwabu-lesson-plan.pdf";
      link.click();
    });
  }
  const totalMinutes = draft.document.sections.reduce(
    (total, section) => total + section.minutes,
    0,
  );
  return (
    <div className="lesson-workspace" data-unsaved-work={draft.dirty}>
      <PageTitle eyebrow="LESSON PLANNER" title={draft.document.setup.title}>
        {orgName} ·{" "}
        {draft.document.setup.mode === "CurriculumAligned"
          ? "Managed curriculum alignment"
          : "Free-form: no official alignment claimed"}
      </PageTitle>
      <div className="lesson-toolbar">
        <Badge>{draft.server.lesson.status}</Badge>
        <span role="status" aria-live="polite">
          {draft.saving
            ? "Saving draft…"
            : draft.dirty
              ? draft.blocked
                ? "Not saved - your edits are still here"
                : "Unsaved edits - autosave pending"
              : `Saved · revision ${draft.server.lesson.revision}`}
        </span>
        {draft.server.canEdit && (
          <button
            className="button"
            disabled={busy || draft.saving}
            onClick={() =>
              void run(async () => {
                await draft.save();
                notify("Lesson saved.");
              })
            }
          >
            <Save size={16} />
            Save now
          </button>
        )}
        <button
          className="button secondary"
          disabled={busy || draft.conflict}
          onClick={() => void exportPdf()}
        >
          <Download size={16} />
          Export saved PDF
        </button>
        <button
          className="button secondary"
          disabled={busy || draft.conflict}
          onClick={() =>
            void run(async () => {
              await draft.save();
              const saved = await api<Detail>(path);
              const url = URL.createObjectURL(
                new Blob([offlineLessonHtml(saved, orgName)], {
                  type: "text/html;charset=utf-8",
                }),
              );
              const link = document.createElement("a");
              link.href = url;
              link.download = "mwabu-lesson-offline.html";
              link.click();
              setTimeout(() => URL.revokeObjectURL(url), 10000);
              notify(
                "Offline copy downloaded. Keep it secure and verify later revisions when reconnecting.",
              );
            })
          }
        >
          Download offline copy
        </button>
        <button
          className="button secondary"
          disabled={busy || draft.conflict}
          onClick={() =>
            void run(async () => {
              await draft.save();
              setTab("print");
              requestAnimationFrame(() => window.print());
            })
          }
        >
          <Printer size={16} />
          Print saved lesson
        </button>
        {session.can("lessons.manage") && (
          <button
            className="button secondary"
            disabled={busy}
            onClick={() =>
              void run(async () => {
                await draft.save();
                const duplicate = await api<Detail>(path + "/duplicate", {
                  method: "POST",
                });
                navigate(`/app/lessons/${duplicate.lesson.id}`);
              })
            }
          >
            Duplicate lesson
          </button>
        )}
        <Link className="button secondary" to="/app/lessons">
          All lesson plans
        </Link>
      </div>
      {!!draft.error && <ErrorNotice error={draft.error} />}
      {!!error && <ErrorNotice error={error} />}
      {draft.conflict && (
        <div className="alert" role="alert">
          <p>
            Your edits are preserved. Compare with the latest server revision
            before resolving the conflict. Autosave is paused.
          </p>
          <button
            className="button secondary"
            onClick={() =>
              void run(async () => {
                const latest = await api<Detail>(path);
                setProposal(undefined);
                setTab("history");
                await client.invalidateQueries({
                  queryKey: ["lesson-revisions", path],
                });
                if (
                  window.confirm(
                    "Load the latest saved revision and discard your current local edits? Cancel keeps your edits for comparison/copying.",
                  )
                )
                  draft.adopt(latest);
              })
            }
          >
            Inspect / reload latest saved revision
          </button>
        </div>
      )}
      <nav className="lesson-tabs" aria-label="Lesson planner panels">
        {[
          ["editor", "Structured editor"],
          ["sources", "Source Explorer"],
          ["ai", "AI proposals"],
          ["history", "Revision history"],
          ["review", "Review & sharing"],
          ["print", "Print preview"],
        ].map(([value, label]) => (
          <button
            className={tab === value ? "button" : "button secondary"}
            key={value}
            aria-pressed={tab === value}
            onClick={() => setTab(value)}
          >
            {label}
          </button>
        ))}
      </nav>
      {tab === "editor" && (
        <>
          <details className="panel">
            <summary>Guided lesson setup and planning mode</summary>
            <SetupForm
              value={draft.document.setup}
              disabled={!editable}
              change={(value) =>
                draft.update((document) => ({ ...document, setup: value }))
              }
            />
          </details>
          <details className="panel">
            <summary>Ministry lesson plan form and progression</summary>
            <MinistryForm
              document={draft.document}
              organisation={organisation}
              disabled={!editable}
              update={(form) =>
                draft.update((document) => ({
                  ...document,
                  ministryForm: form,
                }))
              }
            />
          </details>
          {draft.document.curriculum && (
            <section className="panel">
              <h2>Selected curriculum context</h2>
              <p>
                {[
                  draft.document.curriculum.curriculum,
                  draft.document.curriculum.version,
                  draft.document.curriculum.grade,
                  draft.document.curriculum.subject,
                  draft.document.curriculum.term,
                  draft.document.curriculum.topic,
                ].join(" / ")}
              </p>
              <p>{draft.document.curriculum.provenanceNotice}</p>
            </section>
          )}
          <section className="panel">
            <h2>Your lesson structure</h2>
            <p>
              Configure optional sections, edit teaching content and allocate
              activity minutes. Required teaching sections must be complete
              before review.
            </p>
            <p
              className={
                totalMinutes === draft.document.setup.durationMinutes
                  ? ""
                  : "alert"
              }
              role="status"
            >
              Activity time: {totalMinutes} /{" "}
              {draft.document.setup.durationMinutes} minutes
            </p>
            <details>
              <summary>Configure lesson sections</summary>
              <div className="lesson-section-options">
                {sectionDefinitions.map(([code, title]) => (
                  <label className="checkbox" key={code}>
                    <input
                      type="checkbox"
                      disabled={!editable || authoritative.includes(code)}
                      checked={draft.document.sections.some(
                        (x) => x.code === code,
                      )}
                      onChange={(e) => {
                        const checked = e.target.checked;
                        draft.update((document) => ({
                          ...document,
                          sections: checked
                            ? [
                                ...document.sections,
                                {
                                  code,
                                  title,
                                  body: "",
                                  minutes: 0,
                                  sourceIds: [],
                                },
                              ]
                            : document.sections.filter((x) => x.code !== code),
                        }));
                      }}
                    />
                    Include {title}
                  </label>
                ))}
              </div>
            </details>
            {draft.document.sections.map((section) => (
              <details
                className="lesson-section"
                key={section.code}
                open={
                  section.code === "objectives" ||
                  section.code === "development"
                }
              >
                <summary>
                  {section.title}{" "}
                  {section.minutes > 0 && `· ${section.minutes} min`}
                </summary>
                {authoritative.includes(section.code) ? (
                  <>
                    <pre>{section.body}</pre>
                    <small>
                      Bound to managed records; AI cannot rewrite this field.
                    </small>
                  </>
                ) : (
                  <>
                    <label>
                      {section.title}
                      <textarea
                        rows={7}
                        maxLength={10000}
                        disabled={!editable}
                        value={section.body}
                        onSelect={(e) => {
                          const input = e.currentTarget;
                          const text = input.value.slice(
                            input.selectionStart,
                            input.selectionEnd,
                          );
                          setSelectedPassage(
                            text.length > 0 && text.length <= 2000
                              ? { code: section.code, text }
                              : undefined,
                          );
                        }}
                        onChange={(e) => {
                          const body = e.target.value;
                          draft.update((document) => ({
                            ...document,
                            sections: document.sections.map((x) =>
                              x.code === section.code ? { ...x, body } : x,
                            ),
                          }));
                        }}
                      />
                    </label>
                    <label>
                      Minutes for {section.title}
                      <input
                        type="number"
                        min={0}
                        max={480}
                        disabled={!editable}
                        value={section.minutes}
                        onChange={(e) => {
                          const minutes = Number(e.target.value);
                          draft.update((document) => ({
                            ...document,
                            sections: document.sections.map((x) =>
                              x.code === section.code ? { ...x, minutes } : x,
                            ),
                          }));
                        }}
                      />
                    </label>
                    {editable && session.can("lessons.ai") && (
                      <button
                        className="button secondary"
                        onClick={() => {
                          setSectionCode(section.code);
                          setOperation(
                            selectedPassage?.code === section.code
                              ? "rewrite"
                              : "regenerate-section",
                          );
                          setTab("ai");
                        }}
                      >
                        <Sparkles size={16} />
                        AI action for {section.title}
                      </button>
                    )}
                    <details>
                      <summary>Section references / citations</summary>
                      {selectedSources.length === 0 ? (
                        <p>Select permitted sources in Source Explorer.</p>
                      ) : (
                        selectedSources.map((source) => (
                          <label className="checkbox" key={source.id}>
                            <input
                              type="checkbox"
                              disabled={!editable}
                              checked={section.sourceIds.includes(source.id)}
                              onChange={(e) => {
                                const checked = e.target.checked;
                                draft.update((document) => ({
                                  ...document,
                                  sections: document.sections.map((x) =>
                                    x.code === section.code
                                      ? {
                                          ...x,
                                          sourceIds: checked
                                            ? [...x.sourceIds, source.id]
                                            : x.sourceIds.filter(
                                                (id) => id !== source.id,
                                              ),
                                        }
                                      : x,
                                  ),
                                }));
                              }}
                            />
                            {source.title} · {source.retrievalStatus}
                          </label>
                        ))
                      )}
                    </details>
                  </>
                )}
              </details>
            ))}
          </section>
        </>
      )}
      {tab === "sources" && (
        <SourceExplorer
          path={path}
          title={draft.document.setup.title}
          language={draft.document.setup.language}
          editable={editable}
          configuration={configuration.data}
          prepare={draft.save}
          changed={sourceChanged}
        />
      )}
      {tab === "ai" && (
        <section className="panel lesson-ai">
          <h2>AI proposals - you remain the editor</h2>
          <p>
            Generation never publishes a plan. Review pedagogy, safety, factual
            claims and local-language wording. Only authorised retained evidence
            is sent, without account or learner identifiers.
          </p>
          {configuration.error && <ErrorNotice error={configuration.error} />}
          {(!configuration.data?.aiConfigured ||
            !configuration.data.organisationAiApproved) && (
            <div className="alert" role="status">
              AI assistance is unavailable for this organisation. Your manual
              editor remains available.
            </div>
          )}
          <fieldset disabled={!editable || !session.can("lessons.ai")}>
            <legend>Request a proposal</legend>
            <div className="lesson-grid">
              <label>
                AI action
                <select
                  value={operation}
                  onChange={(e) => {
                    setOperation(e.target.value);
                    if (e.target.value === "generate") setSectionCode("");
                  }}
                >
                  {(configuration.data?.operations ?? ["generate"]).map(
                    (value) => (
                      <option key={value} value={value}>
                        {value.replaceAll("-", " ")}
                      </option>
                    ),
                  )}
                </select>
              </label>
              <label>
                Target section
                <select
                  value={sectionCode}
                  disabled={operation === "generate"}
                  onChange={(e) => {
                    setSectionCode(e.target.value);
                    setSelectedPassage(undefined);
                  }}
                >
                  <option value="">Whole lesson</option>
                  {draft.document.sections
                    .filter((x) => !authoritative.includes(x.code))
                    .map((x) => (
                      <option key={x.code} value={x.code}>
                        {x.title}
                      </option>
                    ))}
                </select>
              </label>
            </div>
            <label>
              Instructions for this proposal
              <textarea
                rows={4}
                maxLength={2000}
                value={instructions}
                onChange={(e) => setInstructions(e.target.value)}
                placeholder="Explain the desired change, classroom constraints or selected passage to rewrite."
              />
            </label>
            {selectedPassage?.code === sectionCode && (
              <div>
                <p>
                  Selected passage only; surrounding text and timing must remain
                  unchanged.
                </p>
                <pre>{selectedPassage.text}</pre>
                <button
                  className="button secondary"
                  onClick={() => setSelectedPassage(undefined)}
                >
                  Use the whole section instead
                </button>
              </div>
            )}
            <label className="checkbox">
              <input
                type="checkbox"
                checked={confirmed}
                onChange={(e) => setConfirmed(e.target.checked)}
              />
              I reviewed this lesson and selected sources: they contain no
              identifiable learner or personal information, and are authorised
              for approved AI processing
            </label>
            <button
              className="button"
              disabled={
                !confirmed ||
                !configuration.data?.aiConfigured ||
                !configuration.data.organisationAiApproved ||
                draft.conflict ||
                (operation === "regenerate-section" && !sectionCode)
              }
              onClick={() => void generate()}
            >
              <Sparkles size={16} />
              Request AI proposal
            </button>
          </fieldset>
          {busy && (
            <div role="status" aria-live="polite">
              <progress />
              Request in progress. Your saved draft remains unchanged.{" "}
              <button
                className="button secondary"
                onClick={() => abort.current?.abort()}
              >
                Cancel generation
              </button>
            </div>
          )}
          {proposal && (
            <section className="lesson-proposal">
              <h3>Proposed changes</h3>
              <p>
                {proposal.result?.explanation ??
                  "This proposal is unavailable or expired."}
              </p>
              <p>
                Estimated processing charge USD{" "}
                {proposal.estimatedCostUsd.toFixed(6)}{" "}
                {proposal.usageEstimated && "(conservative/uncertain)"}
              </p>
              {proposal.result && (
                <>
                  <DocumentComparison
                    before={draft.document}
                    after={proposalDocument(draft.document, proposal)}
                  />
                  <button
                    className="button"
                    disabled={
                      !editable ||
                      draft.dirty ||
                      proposal.state !== "completed" ||
                      proposal.baseRevision !== draft.server.lesson.revision
                    }
                    onClick={() =>
                      void run(async () => {
                        const result = await api<Detail>(
                          path + `/generations/${proposal.id}/apply`,
                          {
                            method: "POST",
                            body: {
                              expectedRevision: draft.server.lesson.revision,
                            },
                          },
                        );
                        await changedServer(result);
                        setProposal(undefined);
                        notify("Reviewed AI proposal applied to your draft.");
                      })
                    }
                  >
                    Apply reviewed proposal to draft
                  </button>{" "}
                  <button
                    className="button secondary"
                    onClick={() => setProposal(undefined)}
                  >
                    Discard proposal view
                  </button>
                  {proposal.baseRevision !== draft.server.lesson.revision && (
                    <p>
                      The draft changed since generation. Compare/copy useful
                      wording manually or regenerate; automatic application is
                      blocked.
                    </p>
                  )}
                </>
              )}
            </section>
          )}
          {initial.lesson.ownerUserId === session.user?.id && (
            <>
              <h3>Generation history</h3>
              <QueryState query={generationHistory}>
                {(data) => (
                  <>
                    <ul>
                      {data.items.map((item) => (
                        <li key={item.id}>
                          <button
                            className="button secondary"
                            onClick={() =>
                              void run(async () =>
                                setProposal(
                                  await api<Generation>(
                                    path + `/generations/${item.id}`,
                                  ),
                                ),
                              )
                            }
                          >
                            {item.operation} · {item.state} · base revision{" "}
                            {item.baseRevision}
                          </button>{" "}
                          <small>
                            {new Date(item.createdAt).toLocaleString()} · USD{" "}
                            {item.estimatedCostUsd.toFixed(6)}
                          </small>
                        </li>
                      ))}
                    </ul>
                    <Pagination
                      page={generationPage}
                      hasMore={data.hasMore ?? false}
                      onChange={setGenerationPage}
                    />
                  </>
                )}
              </QueryState>
            </>
          )}
        </section>
      )}
      {tab === "history" && (
        <RevisionPanel
          path={path}
          current={draft.document}
          editable={editable}
          restore={restore}
        />
      )}
      {tab === "review" && (
        <section className="panel">
          <h2>Review, publication and sharing</h2>
          <p>
            Private drafts are visible to their owner and explicit authorised
            shares. Reviewers see submitted plans. Organisation templates become
            reusable only after publication.
          </p>
          <label>
            Review / lifecycle note
            <textarea
              maxLength={2000}
              value={note}
              disabled={busy}
              onChange={(e) => setNote(e.target.value)}
            />
          </label>
          <div className="lesson-actions">
            {draft.server.canEdit && (
              <button
                className="button"
                disabled={busy || draft.conflict}
                onClick={() => void transition("InReview")}
              >
                Submit lesson for review
              </button>
            )}
            {draft.server.lesson.status === "InReview" &&
              draft.server.canReview && (
                <>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={template}
                      onChange={(e) => setTemplate(e.target.checked)}
                    />
                    Publish as reusable organisation template
                  </label>
                  <button
                    className="button secondary"
                    disabled={busy}
                    onClick={() => void transition("Draft")}
                  >
                    Return lesson to draft
                  </button>
                  {session.can("lessons.publish") && (
                    <button
                      className="button"
                      disabled={busy}
                      onClick={() => void transition("Published")}
                    >
                      Approve and publish lesson
                    </button>
                  )}
                </>
              )}
            {draft.server.lesson.status !== "Archived" &&
              (initial.lesson.ownerUserId === session.user?.id ||
                session.can("lessons.review")) && (
                <button
                  className="button secondary"
                  disabled={busy}
                  onClick={() => void transition("Archived")}
                >
                  Archive lesson
                </button>
              )}
          </div>
          {initial.lesson.ownerUserId === session.user?.id &&
            draft.server.lesson.status !== "Archived" && (
              <LessonSharing path={path} prepare={draft.save} />
            )}
          {draft.server.lesson.status !== "Draft" && (
            <p>
              This version cannot be edited. Duplicate it to create a new
              private draft.
            </p>
          )}
        </section>
      )}
      {tab === "print" && (
        <LessonPrint
          document={draft.document}
          status={draft.server.lesson.status}
          revision={draft.server.lesson.revision}
          organisation={orgName}
          teacher={draft.server.authorName || "Authorised teaching colleague"}
          dirty={draft.dirty}
        />
      )}
      {blocker.state === "blocked" && (
        <Modal title="Unsaved lesson changes" onClose={() => blocker.reset()}>
          <p>
            Your edits have not been saved. Keep working, save them, or
            explicitly discard them.
          </p>
          <button
            className="button"
            onClick={() =>
              void run(async () => {
                await draft.save();
                blocker.proceed();
              })
            }
          >
            Save and continue
          </button>
          <button className="button secondary" onClick={() => blocker.reset()}>
            Keep working
          </button>
          <button
            className="button secondary"
            onClick={() => blocker.proceed()}
          >
            Discard and leave
          </button>
        </Modal>
      )}
    </div>
  );
}
