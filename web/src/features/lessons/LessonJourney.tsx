import { useState } from "react";
import {
  Link,
  useParams,
  useSearchParams,
  useBeforeUnload,
  useBlocker,
  useNavigate,
} from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import { useSession } from "../../auth/Session";
import { Empty, ErrorNotice, QueryState } from "../../components/ui";
import type { Content, Page, Resource } from "../../api/types";
import {
  lessonBase,
  type Detail,
  type TeachingResource,
  type TeachingStep,
} from "./types";
import { journeySteps, resourceKind } from "./journey";
import { MediaViewer } from "../content/Resource";
import { PublishedActivity } from "../content/activities/ActivityPlayer";
import { activityMime } from "../content/activities/activityRuntime";

export default function LessonJourney() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const session = useSession();
  const org = session.organisationId;
  const revision = params.has("revision")
    ? Number(params.get("revision"))
    : undefined;
  const valid =
    revision === undefined || (Number.isInteger(revision) && revision > 0);
  const query = useQuery({
    queryKey: ["lesson-journey", org, id, revision],
    queryFn: ({ signal }) =>
      api<Detail>(lessonBase(org!) + `/${id}?` + queryString({ revision }), {
        signal,
      }),
    enabled: !!org && session.can("lessons.read") && valid,
    refetchOnWindowFocus: false,
  });
  if (!org || !session.can("lessons.read"))
    return <Empty title="Select an organisation with lesson access" />;
  if (!valid) return <Empty title="Choose a saved lesson revision" />;
  return (
    <QueryState query={query}>
      {(detail) => (
        <Preparation
          key={`${org}:${id}:${detail.viewedRevision}`}
          initial={detail}
          org={org}
        />
      )}
    </QueryState>
  );
}
function Preparation({ initial, org }: { initial: Detail; org: string }) {
  const navigate = useNavigate();
  const [detail, setDetail] = useState(initial);
  const [steps, setSteps] = useState(() => journeySteps(initial));
  const [index, setIndex] = useState(0);
  const [preview, setPreview] = useState<TeachingResource>();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const client = useQueryClient();
  const dirty =
    JSON.stringify(steps) !==
    JSON.stringify(detail.document.teachingSteps ?? []);
  useBeforeUnload((event) => {
    if (dirty && detail.canEdit) {
      event.preventDefault();
      event.returnValue = "";
    }
  });
  const blocker = useBlocker(dirty && detail.canEdit && !busy);
  const step = steps[index];
  const update = (value: Partial<TeachingStep>) =>
    setSteps((items) =>
      items.map((s, i) => (i === index ? { ...s, ...value } : s)),
    );
  async function save() {
    setBusy(true);
    setError(undefined);
    try {
      const saved = await api<Detail>(
        lessonBase(org) + `/${detail.lesson.id}`,
        {
          method: "PUT",
          body: {
            expectedRevision: detail.viewedRevision,
            setup: detail.document.setup,
            sections: detail.document.sections,
            ministryForm: detail.document.ministryForm,
            teachingSteps: steps,
            reason: "Teacher prepared classroom journey",
          },
        },
      );
      setDetail(saved);
      setSteps(saved.document.teachingSteps ?? []);
      navigate(
        `/app/lessons/${saved.lesson.id}/journey?revision=${saved.viewedRevision}`,
        { replace: true },
      );
      await client.invalidateQueries({
        queryKey: ["lesson", org, detail.lesson.id],
      });
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  const move = (from: number, to: number) => {
    const next = [...steps];
    [next[from], next[to]] = [next[to], next[from]];
    setSteps(next);
    setIndex(to);
  };
  return (
    <section className="lesson-journey">
      <header>
        <p className="eyebrow">
          PREPARE LESSON · SAVED REVISION {detail.viewedRevision}
        </p>
        <h1>{detail.lesson.title}</h1>
        <p>
          {detail.document.curriculum
            ? [
                detail.document.curriculum.grade,
                detail.document.curriculum.subject,
                detail.document.curriculum.term,
                detail.document.curriculum.topic,
              ].join(" · ")
            : [
                detail.document.setup.targetLearners,
                detail.document.setup.subject,
              ].join(" · ")}
        </p>
        <p className="preserve-lines">{detail.document.setup.objectives}</p>
      </header>
      <nav className="actions">
        <Link
          className="button secondary"
          to={`/app/lessons/${detail.lesson.id}`}
        >
          Edit lesson plan
        </Link>
        <Link className="button secondary" to="/app/lessons">
          Lesson library
        </Link>
        {(!dirty || !detail.canEdit) && steps.length > 0 && (
          <Link
            className="button"
            to={`/app/lessons/${detail.lesson.id}/teach?revision=${detail.viewedRevision}`}
          >
            Teach Lesson
          </Link>
        )}
        {detail.canEdit && (
          <button
            className="button"
            disabled={!dirty || busy}
            onClick={() => void save()}
          >
            {busy ? "Saving…" : "Save preparation"}
          </button>
        )}
      </nav>
      <aside className="panel" aria-label="Preparation readiness">
        <h2>Preparation readiness</h2>
        <p>
          {dirty
            ? "Save your preparation before teaching."
            : "Lesson plan and teaching sequence saved."}
        </p>
        <p>
          {detail.document.curriculum
            ? "Learning outcome linked."
            : "Free-form lesson: no managed curriculum alignment."}
        </p>
        <p>
          {steps.length} teaching steps ·{" "}
          {steps.reduce((n, s) => n + s.minutes, 0)} /{" "}
          {detail.document.setup.durationMinutes} minutes planned
        </p>
        {steps.some(
          (s) => !s.learnerExplanation.trim() && !s.resources.length,
        ) && (
          <p>
            Optional improvement: add learner-facing text or a resource to empty
            steps.
          </p>
        )}
        <p>
          Preview each attachment to check its exact approved version and
          accessibility. Private guidance is shown only here; use a printed
          lesson guide if you need it while mirroring.
        </p>
        <p>Offline packages can be prepared from the published lesson plan.</p>
      </aside>
      {!!error && <ErrorNotice error={error} />}
      {blocker.state === "blocked" && (
        <div role="alert" className="panel">
          <p>
            Your preparation has unsaved changes. Save it before leaving, or
            choose to discard these edits.
          </p>
          <button className="button" onClick={() => blocker.reset()}>
            Keep preparing
          </button>
          <button
            className="button secondary"
            onClick={() => blocker.proceed()}
          >
            Discard edits and leave
          </button>
        </div>
      )}
      <div className="journey-layout">
        <nav className="panel" aria-label="Teaching-step timeline">
          <ol>
            {steps.map((s, i) => (
              <li key={s.id}>
                <button
                  className="button secondary"
                  aria-current={i === index ? "step" : undefined}
                  onClick={() => {
                    setIndex(i);
                    setPreview(undefined);
                  }}
                >
                  {i + 1}. {s.title} · {s.minutes} min
                </button>
              </li>
            ))}
          </ol>
          {detail.canEdit && steps.length < 40 && (
            <button
              className="button secondary"
              onClick={() => {
                setSteps([
                  ...steps,
                  {
                    id: crypto.randomUUID(),
                    sectionCode: "development",
                    title: "New teaching step",
                    phase: "Interactive activity",
                    minutes: 5,
                    teacherInstructions: "",
                    learnerExplanation: "",
                    hint: "",
                    answer: "",
                    teacherNotes: "",
                    resources: [],
                  },
                ]);
                setIndex(steps.length);
              }}
            >
              Add teaching step
            </button>
          )}
        </nav>
        {step && (
          <section className="panel" aria-label="Prepare current step">
            <h2>{step.title}</h2>
            <fieldset disabled={!detail.canEdit || busy}>
              <label>
                Step title
                <input
                  value={step.title}
                  maxLength={150}
                  onChange={(e) => update({ title: e.target.value })}
                />
              </label>
              <label>
                Teaching phase
                <input
                  value={step.phase}
                  maxLength={80}
                  list="teaching-phases"
                  onChange={(e) => update({ phase: e.target.value })}
                />
              </label>
              <datalist id="teaching-phases">
                {[
                  "Introduction",
                  "Explanation",
                  "Demonstration",
                  "Guided practice",
                  "Interactive activity",
                  "Assessment",
                  "Recap",
                ].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </datalist>
              <label>
                Plan section
                <select
                  value={step.sectionCode}
                  onChange={(e) => update({ sectionCode: e.target.value })}
                >
                  {detail.document.sections.map((s) => (
                    <option key={s.code} value={s.code}>
                      {s.title}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Minutes
                <input
                  type="number"
                  min={0}
                  max={480}
                  value={step.minutes}
                  onChange={(e) => update({ minutes: Number(e.target.value) })}
                />
              </label>
              {(
                [
                  ["teacherInstructions", "Teacher instructions (private)"],
                  ["learnerExplanation", "Learner-facing explanation"],
                  ["hint", "Hint (hidden until revealed)"],
                  ["answer", "Answer (hidden until revealed)"],
                  ["teacherNotes", "Teacher notes (private)"],
                ] as const
              ).map(([key, label]) => (
                <label key={key}>
                  {label}
                  <textarea
                    value={step[key]}
                    maxLength={
                      key === "hint"
                        ? 1000
                        : key === "answer" || key === "teacherNotes"
                          ? 2000
                          : 4000
                    }
                    onChange={(e) => update({ [key]: e.target.value })}
                  />
                </label>
              ))}
              <div className="actions">
                <button
                  className="button secondary"
                  disabled={index === 0}
                  onClick={() => move(index, index - 1)}
                >
                  Move step earlier
                </button>
                <button
                  className="button secondary"
                  disabled={index === steps.length - 1}
                  onClick={() => move(index, index + 1)}
                >
                  Move step later
                </button>
                <button
                  className="button secondary"
                  onClick={() => {
                    setSteps(steps.filter((_, i) => i !== index));
                    setIndex(Math.max(0, index - 1));
                  }}
                >
                  Remove step
                </button>
              </div>
            </fieldset>
            <h3>Linked approved resources</h3>
            <ol>
              {step.resources.map((r, i) => (
                <li key={r.assetId}>
                  <button
                    className="button secondary"
                    onClick={() => setPreview(r)}
                  >
                    Preview {r.title}
                  </button>
                  {detail.canEdit && (
                    <>
                      <button
                        className="button secondary"
                        disabled={i === 0}
                        onClick={() => {
                          const next = [...step.resources];
                          [next[i - 1], next[i]] = [next[i], next[i - 1]];
                          update({ resources: next });
                        }}
                      >
                        Move resource earlier
                      </button>
                      <button
                        className="button secondary"
                        onClick={() =>
                          update({
                            resources: step.resources.filter(
                              (x) => x.assetId !== r.assetId,
                            ),
                          })
                        }
                      >
                        Remove resource
                      </button>
                    </>
                  )}
                </li>
              ))}
            </ol>
            {detail.canEdit && step.resources.length < 10 && (
              <ResourcePicker
                onPreview={setPreview}
                onAttach={(r) => {
                  if (!step.resources.some((x) => x.assetId === r.assetId))
                    update({ resources: [...step.resources, r] });
                  setPreview(r);
                }}
              />
            )}
            {preview && (
              <PinnedResource key={preview.assetId} reference={preview} />
            )}
          </section>
        )}
      </div>
      <button className="button secondary" onClick={() => window.print()}>
        Print private teacher guide
      </button>
      <article
        className="journey-teacher-guide"
        aria-label="Private printed teacher guide"
      >
        <h1>{detail.lesson.title}</h1>
        <p>
          Private teacher guide ·{" "}
          {dirty
            ? "Unsaved preparation preview"
            : `Saved revision ${detail.viewedRevision}`}{" "}
          · Keep this guide off the projected screen.
        </p>
        {steps.map((s, i) => (
          <section key={s.id}>
            <h2>
              {i + 1}. {s.title}
            </h2>
            <p>
              {s.phase} · {s.minutes} minutes
            </p>
            <h3>Teacher instructions</h3>
            <p>{s.teacherInstructions}</p>
            <h3>Learners see</h3>
            <p>{s.learnerExplanation}</p>
            {s.hint && <p>Hint: {s.hint}</p>}
            {s.answer && <p>Answer: {s.answer}</p>}
            {s.teacherNotes && <p>Private notes: {s.teacherNotes}</p>}
            <ul>
              {s.resources.map((r) => (
                <li key={r.assetId}>{r.title}</li>
              ))}
            </ul>
          </section>
        ))}
      </article>
    </section>
  );
}
function ResourcePicker({
  onAttach,
  onPreview,
}: {
  onAttach: (r: TeachingResource) => void;
  onPreview: (r: TeachingResource) => void;
}) {
  const [text, setText] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState("");
  const query = useQuery({
    queryKey: ["journey-resource-search", search, page],
    queryFn: ({ signal }) =>
      api<Page<Content>>(
        "/api/learning/content?" +
          queryString({ text: search, page, pageSize: 10 }),
        { signal },
      ),
  });
  const resource = useQuery({
    queryKey: ["journey-resource", selected],
    queryFn: ({ signal }) =>
      api<Resource>(`/api/learning/content/${selected}`, { signal }),
    enabled: !!selected,
  });
  return (
    <section aria-label="Find approved teaching resources">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setSearch(text);
          setPage(1);
          setSelected("");
        }}
      >
        <label>
          Search approved resources
          <input value={text} onChange={(e) => setText(e.target.value)} />
        </label>
        <button className="button secondary">Search resources</button>
      </form>
      <QueryState query={query}>
        {(data) => (
          <>
            <ul>
              {data.items.map((c) => (
                <li key={c.id}>
                  <button
                    className="button secondary"
                    onClick={() => setSelected(c.id)}
                  >
                    {c.title} · {c.contentType}
                  </button>
                </li>
              ))}
            </ul>
            <button
              className="button secondary"
              disabled={page === 1}
              onClick={() => setPage(page - 1)}
            >
              Previous resources
            </button>
            <button
              className="button secondary"
              disabled={!data.hasMore}
              onClick={() => setPage(page + 1)}
            >
              More resources
            </button>
          </>
        )}
      </QueryState>
      {selected && (
        <QueryState query={resource}>
          {(r) => (
            <ul>
              {r.assets.map((a) => (
                <li key={a.id}>
                  <span>
                    {a.fileName} · {resourceKind(a.mimeType)}
                  </span>
                  <button
                    className="button secondary"
                    disabled={resourceKind(a.mimeType) === "Download-only file"}
                    onClick={() =>
                      onPreview({
                        contentItemId: r.content.id,
                        assetId: a.id,
                        checksum: a.checksum,
                        title: r.content.title,
                      })
                    }
                  >
                    Preview {a.fileName}
                  </button>
                  <button
                    className="button secondary"
                    onClick={() =>
                      onAttach({
                        contentItemId: r.content.id,
                        assetId: a.id,
                        checksum: a.checksum,
                        title: r.content.title,
                      })
                    }
                  >
                    Attach {a.fileName}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </QueryState>
      )}
    </section>
  );
}
export function PinnedResource({
  reference,
  wholeClass = false,
}: {
  reference: TeachingResource;
  wholeClass?: boolean;
}) {
  const query = useQuery({
    queryKey: [
      "pinned-teaching-resource",
      reference.contentItemId,
      reference.assetId,
      reference.checksum,
    ],
    queryFn: ({ signal }) =>
      api<Resource>(`/api/learning/content/${reference.contentItemId}`, {
        signal,
      }),
    staleTime: 0,
  });
  return (
    <QueryState query={query}>
      {(resource) => {
        const asset = resource.assets.find(
          (a) =>
            a.id === reference.assetId &&
            a.checksum.toLowerCase() === reference.checksum.toLowerCase(),
        );
        return asset ? (
          asset.mimeType === activityMime ? (
            <PublishedActivity
              resourceId={reference.contentItemId}
              assetId={asset.id}
              downloadUrl={asset.downloadUrl}
              downloadable={false}
              wholeClass={wholeClass}
            />
          ) : (
            <MediaViewer asset={asset} />
          )
        ) : (
          <p role="alert">
            This approved resource version is unavailable. Return to preparation
            to select a version; it has not been replaced automatically.
          </p>
        );
      }}
    </QueryState>
  );
}
