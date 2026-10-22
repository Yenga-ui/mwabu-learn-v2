import { useEffect, useRef, useState } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
  useBeforeUnload,
  useBlocker,
} from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  Plus,
  NotebookPen,
  Clock,
  Paperclip,
  Play,
  BookOpen,
  Sparkles,
  FilePenLine,
} from "lucide-react";
import TeachingIcon from "../../components/TeachingIcon";
import { api, queryString } from "../../api/client";
import { useSession } from "../../auth/Session";
import {
  Badge,
  Empty,
  ErrorNotice,
  PageTitle,
  Pagination,
  QueryState,
  SearchBar,
  notify,
} from "../../components/ui";
import type { Page, Resource } from "../../api/types";
import type { Detail, Lesson, Setup } from "./types";
import { blankSections, blankSetup, lessonBase } from "./types";
import SetupForm from "./LessonSetup";
import LessonEditor from "./LessonEditor";
import PlannerSettings from "./PlannerSettings";
import NodePicker from "../curriculum/NodePicker";
import { rememberLesson } from "./RecentLessons";
import { supportedTeachingMedia } from "./journey";
import TeachingContextPicker from "./TeachingContextPicker";

export default function LessonPlanner() {
  const session = useSession();
  const navigate = useNavigate();
  const [copying, setCopying] = useState<string>();
  const [reuseError, setReuseError] = useState<unknown>();
  const org = session.organisationId;
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const [page, setPage] = useState(1);
  const [text, setText] = useState(params.get("q") ?? params.get("text") ?? "");
  const [status, setStatus] = useState(() => {
    const requested = params.get("status") ?? "";
    return ["Draft", "InReview", "Published", "Archived"].includes(requested)
      ? requested
      : "";
  });
  const [settings, setSettings] = useState(false);
  useEffect(() => {
    setText(params.get("q") ?? params.get("text") ?? "");
    const requested = params.get("status") ?? "";
    setStatus(
      ["Draft", "InReview", "Published", "Archived"].includes(requested)
        ? requested
        : "",
    );
    setPage(1);
  }, [params]);
  const [curriculumFilter, setCurriculumFilter] = useState<
    | {
        nodeType: string;
        nodeId: string;
        name: string;
      }
    | undefined
  >(
    params.get("nodeId") && params.get("nodeType")
      ? {
          nodeId: params.get("nodeId")!,
          nodeType: params.get("nodeType")!,
          name: params.get("context") ?? "Selected learning context",
        }
      : undefined,
  );
  const allowed = !!org && session.can("lessons.read");
  const list = useQuery({
    queryKey: [
      "lessons",
      org,
      page,
      text,
      status,
      curriculumFilter,
      params.get("mine"),
    ],
    queryFn: ({ signal }) =>
      api<Page<Lesson>>(
        lessonBase(org!) +
          "?" +
          queryString({
            page,
            text,
            status,
            mine: params.get("mine") === "true" ? true : undefined,
            pageSize: 20,
            nodeType: curriculumFilter?.nodeType,
            nodeId: curriculumFilter?.nodeId,
          }),
        { signal },
      ),
    enabled: allowed && !id,
  });
  const detail = useQuery({
    queryKey: ["lesson", org, id],
    queryFn: ({ signal }) =>
      api<Detail>(lessonBase(org!) + "/" + id, { signal }),
    enabled: allowed && !!id && id !== "new",
  });
  useEffect(() => {
    if (detail.data && org && session.user)
      rememberLesson(org, session.user.id, detail.data.lesson.id);
  }, [detail.data, org, session.user]);
  if (!allowed)
    return <Empty title="Select an organisation with lesson-planning access" />;
  if (id === "new")
    return (
      <NewLesson
        key={org + params.toString()}
        organisation={org!}
        resource={params.get("resourceId")}
      />
    );
  if (id)
    return (
      <QueryState query={detail}>
        {(data) => (
          <LessonEditor
            key={`${org}:${id}`}
            initial={data}
            organisation={org!}
          />
        )}
      </QueryState>
    );
  return (
    <>
      <PageTitle
        eyebrow="TEACHER WORKSPACE"
        title={
          params.get("mine") === "true" ? "My lesson plans" : "Find a lesson"
        }
      >
        Search by lesson title, grade, subject or topic. View a plan or use it
        to create your own.
      </PageTitle>
      <section className="lesson-welcome">
        <NotebookPen aria-hidden="true" />
        <div>
          <h2>Your teaching judgement comes first.</h2>
          <p>
            Choose a ready-made lesson, write your own, or answer a few
            questions for AI. Every lesson stays a draft until an authorised
            reviewer approves publication.
          </p>
        </div>
        {session.can("lessons.manage") && (
          <Link className="button" to="/app/lessons/new?planning=topics">
            <Plus size={18} />
            Create lesson plan
          </Link>
        )}
        {session.can("lessons.manage") && session.can("lessons.ai") && (
          <Link
            className="button secondary"
            to="/app/lessons/new?planning=topics&assist=ai"
          >
            Create with AI
          </Link>
        )}
      </section>
      <div className="catalogue-tools">
        <SearchBar
          value={text}
          onSearch={(value) => {
            setText(value);
            const next = new URLSearchParams(params);
            next.set("q", value);
            setParams(next, { replace: true });
            setPage(1);
          }}
          placeholder="Search lesson plans"
        />
        <label>
          Lesson status
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              const next = new URLSearchParams(params);
              if (e.target.value) next.set("status", e.target.value);
              else next.delete("status");
              setParams(next, { replace: true });
              setPage(1);
            }}
          >
            <option value="">All authorised plans</option>
            {["Draft", "InReview", "Published", "Archived"].map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>
        <button
          className="button secondary"
          onClick={() => setSettings(!settings)}
        >
          {settings ? "Close" : "Usage and organisation settings"}
        </button>
      </div>
      {settings && <PlannerSettings organisation={org!} />}
      {!!reuseError && <ErrorNotice error={reuseError} />}
      <details className="panel">
        <summary>
          Find lessons by grade, subject, term or topic
          {curriculumFilter ? ` · ${curriculumFilter.name}` : ""}
        </summary>
        <NodePicker
          startWithGrade
          onSelect={(type, selectedId, name) => {
            if (type !== "curricula") {
              setCurriculumFilter({ nodeType: type, nodeId: selectedId, name });
              const next = new URLSearchParams(params);
              next.set("nodeType", type);
              next.set("nodeId", selectedId);
              next.set("context", name);
              setParams(next, { replace: true });
              setPage(1);
            }
          }}
          onReset={() => {
            setCurriculumFilter(undefined);
            const next = new URLSearchParams(params);
            ["nodeId", "nodeType", "context"].forEach((key) =>
              next.delete(key),
            );
            setParams(next, { replace: true });
            setPage(1);
          }}
        />
        {curriculumFilter && (
          <button
            className="button secondary"
            onClick={() => {
              setCurriculumFilter(undefined);
              const next = new URLSearchParams(params);
              ["nodeId", "nodeType", "context"].forEach((key) =>
                next.delete(key),
              );
              setParams(next, { replace: true });
              setPage(1);
            }}
          >
            Clear lesson curriculum filter
          </button>
        )}
      </details>
      <QueryState query={list}>
        {(data) => (
          <>
            {data.items.length === 0 ? (
              <Empty title="No lesson plans here yet">
                Create your first draft, or change the search/status filter.
              </Empty>
            ) : (
              <div className="lesson-cards">
                {data.items.map((lesson) => (
                  <article className="panel lesson-card" key={lesson.id}>
                    <header className="lesson-card-heading">
                      <span className="lesson-card-icon">
                        <TeachingIcon subject={lesson.subject ?? undefined} />
                      </span>
                      <Badge>{lesson.status}</Badge>
                    </header>
                    <span className="lesson-card-owner">
                      {lesson.isOrganisationTemplate
                        ? "Approved organisation template"
                        : lesson.ownerUserId === session.user?.id
                          ? "Your lesson"
                          : "Authorised shared/review lesson"}
                    </span>
                    <h2>
                      <Link to={`/app/lessons/${lesson.id}`}>
                        {lesson.title}
                      </Link>
                    </h2>
                    <p className="lesson-card-context">
                      {[
                        lesson.grade,
                        lesson.subject,
                        lesson.topic,
                        lesson.subTopic,
                      ]
                        .filter(Boolean)
                        .join(" · ")}
                    </p>
                    <div className="lesson-card-facts">
                      {lesson.durationMinutes ? (
                        <span>
                          <Clock size={15} aria-hidden="true" />
                          {lesson.durationMinutes} minutes
                        </span>
                      ) : (
                        ""
                      )}
                      <span>
                        <Paperclip size={15} aria-hidden="true" />
                        {lesson.resourceCount ?? 0}{" "}
                        {(lesson.resourceCount ?? 0) === 1
                          ? "resource"
                          : "resources"}
                      </span>
                    </div>
                    <div className="actions lesson-card-primary">
                      <Link className="button" to={`/app/lessons/${lesson.id}`}>
                        <BookOpen size={16} aria-hidden="true" /> View lesson
                      </Link>
                      {lesson.status !== "Archived" && (
                        <Link
                          className="button secondary"
                          to={`/app/lessons/${lesson.id}/teach?revision=${lesson.revision}`}
                        >
                          <Play size={16} aria-hidden="true" /> Teach lesson
                        </Link>
                      )}
                    </div>
                    <div className="actions lesson-card-secondary">
                      <Link
                        className="button secondary"
                        to={`/app/lessons/${lesson.id}?view=resources`}
                      >
                        View resources
                      </Link>
                      {lesson.canReuse && (
                        <button
                          className="button"
                          disabled={!!copying}
                          onClick={() => {
                            setCopying(lesson.id);
                            setReuseError(undefined);
                            void api<Detail>(
                              lessonBase(org!) + `/${lesson.id}/duplicate`,
                              { method: "POST" },
                            )
                              .then((copy) =>
                                navigate(`/app/lessons/${copy.lesson.id}`),
                              )
                              .catch(setReuseError)
                              .finally(() => setCopying(undefined));
                          }}
                        >
                          {copying === lesson.id
                            ? "Creating your draft…"
                            : "Adapt plan"}
                        </button>
                      )}
                    </div>
                    <details className="lesson-card-more">
                      <summary>Resources and more options</summary>
                      <p className="muted">
                        {lesson.mode === "CurriculumAligned"
                          ? "Managed curriculum alignment"
                          : "Teacher-created plan: no official alignment claimed"}
                      </p>
                      <small>
                        Revision {lesson.revision} ·{" "}
                        {new Date(
                          lesson.updatedAt ?? lesson.createdAt,
                        ).toLocaleDateString()}
                      </small>
                      <Link
                        className="button"
                        to={`/app/lessons/${lesson.id}/journey?revision=${lesson.revision}`}
                      >
                        Open Lesson Journey
                      </Link>
                    </details>
                  </article>
                ))}
              </div>
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
  );
}
function NewLesson({
  organisation,
  resource,
}: {
  organisation: string;
  resource: string | null;
}) {
  const navigate = useNavigate();
  const [draftParams] = useSearchParams();
  const [libraryContext, setLibraryContext] = useState(() => {
    const field = (key: string) => {
      const value = draftParams.get("library" + key) ?? "";
      return value.length <= (key === "subTopic" ? 500 : 200) &&
        !Array.from(value).some(
          (character) =>
            character.charCodeAt(0) < 32 || character.charCodeAt(0) === 127,
        )
        ? value
        : "";
    };
    const year = field("year");
    const grade = field("grade");
    const values = {
      year: year === "2013" || year === "2023" ? year : "",
      grade: /^(?:[1-9]|1[0-2])$/.test(grade) ? grade : "",
      subject: field("subject"),
      topic: field("topic"),
      term: field("term"),
      subTopic: field("subTopic"),
    };
    return Object.values(values).some(Boolean) ? values : null;
  });
  const [topicPlanning] = useState(
    () => draftParams.get("planning") === "topics" || !!libraryContext,
  );
  const aiPlanning = draftParams.get("assist") === "ai";
  const [customContext, setCustomContext] = useState(false);
  const contextLabel = libraryContext
    ? [
        libraryContext.year && `${libraryContext.year} Zambian curriculum`,
        libraryContext.grade && `Grade ${libraryContext.grade}`,
        libraryContext.subject,
        libraryContext.topic,
        libraryContext.term,
        libraryContext.subTopic,
      ]
        .filter(Boolean)
        .join(" · ")
    : "";
  const [setup, setSetup] = useState<Setup>(() =>
    libraryContext
      ? {
          ...blankSetup(),
          mode: "FreeForm",
          title: libraryContext.topic,
          subject: libraryContext.subject,
          targetLearners: libraryContext.grade
            ? `Grade ${libraryContext.grade}`
            : "",
          teachingContext: `Selected library context: ${contextLabel}. Select a managed learning outcome to establish official curriculum alignment.`,
        }
      : {
          ...blankSetup(),
          ...(topicPlanning ? { mode: "FreeForm" as const } : {}),
        },
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const resourcePreview = useQuery({
    queryKey: ["new-lesson-resource", resource],
    queryFn: ({ signal }) =>
      api<Resource>(`/api/learning/content/${resource}`, { signal }),
    enabled: !!resource,
  });
  const persisted = useRef(false);
  const initialSetup = useRef(setup);
  const dirty = JSON.stringify(setup) !== JSON.stringify(initialSetup.current);
  const blocker = useBlocker(() => dirty && !persisted.current);
  useBeforeUnload((event) => {
    if (dirty && !persisted.current) {
      event.preventDefault();
      event.returnValue = "";
    }
  });
  async function create(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      const detail = await api<Detail>(lessonBase(organisation), {
        method: "POST",
        body: {
          expectedRevision: 0,
          setup,
          sections: blankSections().map((section) =>
            section.code === "topic" && libraryContext?.topic
              ? { ...section, body: libraryContext.topic }
              : section,
          ),
          reason: "Teacher created draft",
          ...(setup.mode === "FreeForm" && libraryContext?.subTopic
            ? {
                ministryForm: {
                  topic: libraryContext.topic,
                  subTopic: libraryContext.subTopic,
                  progression: [
                    "introduction",
                    "lesson-development",
                    "exercise",
                    "homework",
                    "conclusion",
                    "reinforcement",
                    "extension",
                  ].map((code) => ({
                    code,
                    teacherActivities: "",
                    learnerActivities: "",
                    assessmentCriteria: "",
                    minutes: 0,
                  })),
                  needsReview: [
                    "introduction",
                    "lesson-development",
                    "exercise",
                    "homework",
                    "conclusion",
                    "reinforcement",
                    "extension",
                    "expected-standard",
                  ],
                },
              }
            : {}),
          teachingSteps: resourcePreview.data?.assets.some((a) =>
            supportedTeachingMedia.includes(a.mimeType),
          )
            ? [
                {
                  id: "resource-introduction",
                  sectionCode: "development",
                  title: "Explore the learning resource",
                  phase: "Demonstration",
                  minutes: 0,
                  teacherInstructions:
                    "Review this resource and complete your teaching sequence.",
                  learnerExplanation: "",
                  hint: "",
                  answer: "",
                  teacherNotes: "",
                  resources: resourcePreview.data.assets
                    .filter((a) => supportedTeachingMedia.includes(a.mimeType))
                    .slice(0, 10)
                    .map((a) => ({
                      contentItemId: resource!,
                      assetId: a.id,
                      checksum: a.checksum,
                      title: resourcePreview.data!.content.title,
                    })),
                },
              ]
            : [],
        },
      });
      persisted.current = true;
      if (resource) {
        try {
          const source = await api<{ id: string }>(
            lessonBase(organisation) + `/${detail.lesson.id}/sources`,
            { method: "POST", body: { contentItemId: resource } },
          );
          await api(
            lessonBase(organisation) +
              `/${detail.lesson.id}/sources/${source.id}`,
            {
              method: "PATCH",
              body: { isSelected: true, isExcluded: false, priority: 50 },
            },
          );
          const attached = await api<Detail>(
            lessonBase(organisation) + `/${detail.lesson.id}`,
          );
          await api<Detail>(lessonBase(organisation) + `/${detail.lesson.id}`, {
            method: "PUT",
            body: {
              expectedRevision: attached.lesson.revision,
              setup: attached.document.setup,
              sections: attached.document.sections,
              ministryForm: attached.document.ministryForm,
              teachingSteps: attached.document.teachingSteps,
              reason: "Selected published learning resource",
            },
          });
        } catch {
          notify(
            "Draft created. The chosen resource could not be attached; check Source Explorer.",
          );
        }
      }
      navigate(
        `/app/lessons/${detail.lesson.id}${aiPlanning ? "?view=ai" : ""}`,
      );
      notify("Lesson draft created.");
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <PageTitle
        eyebrow="A NEW TEACHING IDEA"
        title={aiPlanning ? "Create a lesson with AI" : "Create lesson plan"}
      >
        Choose your grade, subject and topic below. We will suggest a title.
        Save your plan, then write your teaching steps or ask AI to help.
      </PageTitle>
      <nav
        className="creation-methods"
        aria-label="Choose how to create your lesson"
      >
        <Link
          to="/app/lessons/new?planning=topics&assist=ai"
          aria-current={aiPlanning ? "page" : undefined}
        >
          <Sparkles size={18} />
          Create with AI
        </Link>
        <Link to="/app/library">
          <BookOpen size={18} />
          Adapt existing plan
        </Link>
        <Link
          to="/app/lessons/new?planning=topics"
          aria-current={!aiPlanning ? "page" : undefined}
        >
          <FilePenLine size={18} />
          Start blank
        </Link>
      </nav>
      <form
        className="panel"
        onSubmit={create}
        data-unsaved-work={dirty && !persisted.current}
      >
        {!!error && <ErrorNotice error={error} />}
        {topicPlanning && setup.mode === "FreeForm" && !customContext && (
          <TeachingContextPicker
            disabled={busy}
            value={
              libraryContext ?? {
                year: "",
                grade: "",
                subject: "",
                topic: "",
                term: "",
              }
            }
            change={(context) => {
              const label = [
                context.year && `${context.year} Zambian curriculum`,
                context.grade && `Grade ${context.grade}`,
                context.subject,
                context.term,
                context.topic,
                context.subTopic,
              ]
                .filter(Boolean)
                .join(" · ");
              setSetup((previous) => ({
                ...previous,
                title:
                  !previous.title.trim() ||
                  previous.title ===
                    (libraryContext?.subTopic || libraryContext?.topic)
                    ? context.subTopic || context.topic
                    : previous.title,
                subject: context.subject,
                targetLearners: context.grade ? `Grade ${context.grade}` : "",
                teachingContext: `Selected library context: ${label}. Select a managed learning outcome to establish official curriculum alignment.`,
              }));
              setLibraryContext({
                ...context,
                subTopic: context.subTopic ?? "",
              });
            }}
          />
        )}
        {libraryContext && (
          <section className="card" aria-label="Selected library context">
            <h2>Your selected teaching context</h2>
            <p>{contextLabel}</p>
            <p>
              Your choices are retained. Choose a managed learning outcome if
              you need official curriculum alignment.
            </p>
          </section>
        )}
        {resource && (
          <section className="card" aria-label="Selected learning resource">
            {resourcePreview.isPending ? (
              <p>Checking the selected resource…</p>
            ) : resourcePreview.error ? (
              <ErrorNotice error={resourcePreview.error} />
            ) : (
              <>
                <h2>{resourcePreview.data.content.title}</h2>
                <p>{resourcePreview.data.content.summary}</p>
                <Link
                  to={`/app/resources/${resource}`}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  Preview learning resource
                </Link>
                <p>
                  Choose the correct curriculum outcome below. A resource is a
                  starting point; complete your own lesson plan.
                </p>
              </>
            )}
          </section>
        )}
        {topicPlanning && setup.mode === "FreeForm" && (
          <p>
            <button
              type="button"
              className="button secondary"
              disabled={busy}
              onClick={() => {
                setCustomContext(!customContext);
                if (!customContext) {
                  setLibraryContext(null);
                  setSetup((previous) => ({
                    ...previous,
                    teachingContext: previous.teachingContext.startsWith(
                      "Selected library context:",
                    )
                      ? ""
                      : previous.teachingContext,
                  }));
                }
              }}
            >
              {customContext
                ? "Choose Mwabu topics"
                : "Use my own subject and grade"}
            </button>
          </p>
        )}
        <SetupForm
          value={setup}
          change={(next) =>
            setSetup(
              next.mode === "FreeForm" &&
                setup.mode !== "FreeForm" &&
                topicPlanning &&
                libraryContext
                ? {
                    ...next,
                    subject: libraryContext.subject,
                    targetLearners: libraryContext.grade
                      ? `Grade ${libraryContext.grade}`
                      : "",
                  }
                : next,
            )
          }
          disabled={busy}
          simple
          hideFreeFormContext={topicPlanning && !customContext}
        />
        {aiPlanning && (
          <>
            <label>
              Optional teacher instruction
              <textarea
                rows={2}
                maxLength={4000}
                value={setup.instructions}
                onChange={(e) =>
                  setSetup((previous) => ({
                    ...previous,
                    instructions: e.target.value,
                  }))
                }
                placeholder="For example: use bottle tops and simple classroom examples."
              />
            </label>
            <p>
              Next, review your AI settings and request a proposal. Nothing is
              generated or published automatically.
            </p>
          </>
        )}
        {resource && (
          <p>
            The selected published Mwabu resource will be attached to your new
            draft.
          </p>
        )}
        <button
          className="button"
          disabled={
            busy ||
            (!!resource && !resourcePreview.data) ||
            (topicPlanning &&
              setup.mode === "FreeForm" &&
              !customContext &&
              (!libraryContext?.grade ||
                !libraryContext.subject ||
                !libraryContext.topic)) ||
            (setup.mode === "CurriculumAligned" && !setup.learningOutcomeId)
          }
        >
          {busy
            ? "Creating draft…"
            : aiPlanning
              ? "Save and continue to AI"
              : "Create draft"}
        </button>{" "}
        <Link className="button secondary" to="/app/lessons">
          Cancel
        </Link>
        {blocker.state === "blocked" && (
          <div role="alert" className="notice">
            <p>
              Your lesson setup has not been saved. Leave this page and discard
              it?
            </p>
            <button
              type="button"
              className="button secondary"
              onClick={() => blocker.reset()}
            >
              Keep editing
            </button>{" "}
            <button
              type="button"
              className="button"
              onClick={() => blocker.proceed()}
            >
              Discard setup and leave
            </button>
          </div>
        )}
      </form>
    </>
  );
}
