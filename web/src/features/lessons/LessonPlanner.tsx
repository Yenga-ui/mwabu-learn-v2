import { useRef, useState } from "react";
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
import type { Page } from "../../api/types";
import type { Detail, Lesson, Setup } from "./types";
import { blankSections, blankSetup, lessonBase } from "./types";
import SetupForm from "./LessonSetup";
import LessonEditor from "./LessonEditor";
import PlannerSettings from "./PlannerSettings";
import NodePicker from "../curriculum/NodePicker";

export default function LessonPlanner() {
  const session = useSession();
  const org = session.organisationId;
  const { id } = useParams();
  const [params] = useSearchParams();
  const [page, setPage] = useState(1);
  const [text, setText] = useState("");
  const [status, setStatus] = useState("");
  const [settings, setSettings] = useState(false);
  const [curriculumFilter, setCurriculumFilter] = useState<{
    nodeType: string;
    nodeId: string;
    name: string;
  }>();
  const allowed = !!org && session.can("lessons.read");
  const list = useQuery({
    queryKey: ["lessons", org, page, text, status, curriculumFilter],
    queryFn: ({ signal }) =>
      api<Page<Lesson>>(
        lessonBase(org!) +
          "?" +
          queryString({
            page,
            text,
            status,
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
      <PageTitle eyebrow="TEACHER WORKSPACE" title="Lesson Planner">
        Thoughtful lessons, grounded in your curriculum and teaching resources.
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
      <details className="panel">
        <summary>
          Find lessons by grade, subject, term or topic
          {curriculumFilter ? ` · ${curriculumFilter.name}` : ""}
        </summary>
        <NodePicker
          onSelect={(type, selectedId, name) => {
            if (type !== "curricula") {
              setCurriculumFilter({ nodeType: type, nodeId: selectedId, name });
              setPage(1);
            }
          }}
          onReset={() => {
            setCurriculumFilter(undefined);
            setPage(1);
          }}
        />
        {curriculumFilter && (
          <button
            className="button secondary"
            onClick={() => {
              setCurriculumFilter(undefined);
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
        <SetupForm value={setup} change={setSetup} disabled={busy} />
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
