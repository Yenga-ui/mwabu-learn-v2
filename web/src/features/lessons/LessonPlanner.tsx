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
import { Plus, NotebookPen } from "lucide-react";
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

export default function LessonPlanner() {
  const session = useSession();
  const navigate = useNavigate();
  const [copying, setCopying] = useState<string>();
  const [reuseError, setReuseError] = useState<unknown>();
  const org = session.organisationId;
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const [page, setPage] = useState(1);
  const [text, setText] = useState(params.get("q") ?? "");
  const [status, setStatus] = useState("");
  const [settings, setSettings] = useState(false);
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
            Plan manually, discover resources, or request an AI proposal. Every
            lesson stays a draft until an authorised reviewer approves
            publication.
          </p>
        </div>
        {session.can("lessons.manage") && (
          <Link className="button" to="/app/lessons/new">
            <Plus size={18} />
            Create lesson plan
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
                    <Badge>{lesson.status}</Badge>
                    <span>
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
                    <p>
                      {lesson.mode === "CurriculumAligned"
                        ? "Managed curriculum alignment"
                        : "Free-form planning"}
                    </p>
                    <p>
                      {[
                        lesson.grade,
                        lesson.subject,
                        lesson.topic,
                        lesson.subTopic,
                      ]
                        .filter(Boolean)
                        .join(" · ")}
                    </p>
                    <p>
                      {lesson.durationMinutes
                        ? `${lesson.durationMinutes} minutes · `
                        : ""}
                      {lesson.resourceCount ?? 0} learning resources attached
                    </p>
                    <p>
                      {lesson.canEdit
                        ? "Your editable draft"
                        : "View this lesson and use an authorised copy."}
                    </p>
                    <div className="actions">
                      <Link
                        className="button secondary"
                        to={`/app/lessons/${lesson.id}`}
                      >
                        View lesson
                      </Link>
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
                            : "Use this lesson"}
                        </button>
                      )}
                    </div>
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
                    {lesson.status !== "Archived" && (
                      <Link
                        className="button secondary"
                        to={`/app/lessons/${lesson.id}/teach?revision=${lesson.revision}`}
                      >
                        Teach lesson
                      </Link>
                    )}
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
  const [setup, setSetup] = useState<Setup>(blankSetup);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const resourcePreview = useQuery({
    queryKey: ["new-lesson-resource", resource],
    queryFn: ({ signal }) =>
      api<Resource>(`/api/learning/content/${resource}`, { signal }),
    enabled: !!resource,
  });
  const persisted = useRef(false);
  const dirty = JSON.stringify(setup) !== JSON.stringify(blankSetup());
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
          sections: blankSections(),
          reason: "Teacher created draft",
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
      navigate(`/app/lessons/${detail.lesson.id}`);
      notify("Lesson draft created.");
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <PageTitle eyebrow="A NEW TEACHING IDEA" title="Create lesson plan">
        Set your context first. You can edit the structure, discover sources and
        use AI after saving this draft.
      </PageTitle>
      <form
        className="panel"
        onSubmit={create}
        data-unsaved-work={dirty && !persisted.current}
      >
        {!!error && <ErrorNotice error={error} />}
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
        <SetupForm value={setup} change={setSetup} disabled={busy} simple />
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
            (setup.mode === "CurriculumAligned" && !setup.learningOutcomeId)
          }
        >
          {busy ? "Creating draft…" : "Create draft"}
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
