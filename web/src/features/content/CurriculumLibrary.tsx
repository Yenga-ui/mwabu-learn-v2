import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { api } from "../../api/client";
import type { Content, Page } from "../../api/types";
import type { Lesson } from "../lessons/types";
import { PageTitle, QueryState, SearchBar, Badge } from "../../components/ui";
import { useSession } from "../../auth/Session";
import TeachingIcon from "../../components/TeachingIcon";
import { GraduationCap } from "lucide-react";

interface Choice {
  year: number;
  grade: number;
  subject: string;
  topic: string;
  term: string | null;
  subTopic?: string | null;
}
interface Entry {
  content: Content;
  year: number | null;
  grade: number | null;
  subject: string | null;
  topic: string | null;
  term: string | null;
  isTemplate: boolean;
  associatedResource: boolean;
  subTopic?: string | null;
}
interface Result {
  resources: {
    items: Entry[];
    totalCount: number;
    page: number;
    pageSize: number;
  };
  choices: Choice[];
}
export default function CurriculumLibrary() {
  const [params, setParams] = useSearchParams();
  const session = useSession();
  const query = useQuery({
    queryKey: ["curriculum-library", params.toString()],
    queryFn: ({ signal }) =>
      api<Result>("/api/learning/library?" + params, { signal }),
    enabled: session.can("content.read", "catalogue"),
    placeholderData: (previous) => previous,
  });
  const year = params.get("year") ?? "";
  const grade = params.get("grade") ?? "";
  const subject = params.get("subject") ?? "";
  const topic = params.get("topic") ?? "";
  const update = (key: string, value: string) => {
    const next = new URLSearchParams(params);
    next.delete("page");
    const order = ["year", "grade", "subject", "term", "topic", "subTopic"];
    const index = order.indexOf(key);
    if (index >= 0) order.slice(index + 1).forEach((k) => next.delete(k));
    if (value) next.set(key, value);
    else next.delete(key);
    setParams(next);
  };
  const distinct = (values: string[]) =>
    [...new Set(values)].sort((a, b) =>
      a.localeCompare(b, undefined, { numeric: true }),
    );
  const choices = query.data?.choices ?? [];
  const subjectChoices = choices.filter(
    (x) =>
      (!year || String(x.year) === year) &&
      (!grade || String(x.grade) === grade),
  );
  const scoped = subjectChoices.filter(
    (x) => !subject || x.subject === subject,
  );
  const returnPath = "/app/library" + (params.size ? "?" + params : "");
  const draftContext = new URLSearchParams();
  const schoolContext = new URLSearchParams();
  for (const key of ["year", "grade", "subject", "topic", "term", "subTopic"]) {
    const value = params.get(key);
    if (value) {
      draftContext.set("library" + key, value);
      if (key !== "subTopic")
        schoolContext.set(
          "source" + key[0].toUpperCase() + key.slice(1),
          value,
        );
    }
  }
  const createPath =
    "/app/lessons/new" + (draftContext.size ? "?" + draftContext : "");
  return (
    <>
      <PageTitle
        eyebrow="MWABU CURRICULUM LIBRARY"
        title="What would you like to teach?"
      >
        Find a real Mwabu plan and its learning resources in one place.
      </PageTitle>
      <nav className="action-row" aria-label="Lesson starting points">
        {session.can("lessons.read") && (
          <Link className="button secondary" to="/app/lessons?mine=true">
            My lesson plans
          </Link>
        )}
        {session.can("lessons.manage") && (
          <>
            <Link
              className="button secondary"
              to="/app/lessons/new?planning=topics"
            >
              Write a lesson plan
            </Link>
            <Link
              className="button secondary"
              to={
                createPath +
                (draftContext.size ? "&" : "?") +
                "planning=topics&assist=ai"
              }
            >
              Create with AI
            </Link>
          </>
        )}
      </nav>
      <section
        className="card teaching-choices"
        aria-label="Browse by grade, subject and topic"
      >
        <h2>1. Choose your grade</h2>
        <div className="teaching-choice-grid">
          {distinct(
            choices
              .filter((x) => !year || String(x.year) === year)
              .map((x) => String(x.grade)),
          ).map((value) => (
            <button
              key={value}
              className="teaching-choice"
              aria-pressed={grade === value}
              onClick={() => update("grade", value)}
            >
              <GraduationCap size={24} aria-hidden="true" /> Grade {value}
            </button>
          ))}
        </div>
        {grade && (
          <>
            <h2>2. Choose your subject</h2>
            <div className="teaching-choice-grid">
              {distinct(subjectChoices.map((x) => x.subject)).map((value) => (
                <button
                  key={value}
                  className="teaching-choice"
                  aria-pressed={subject === value}
                  onClick={() => update("subject", value)}
                >
                  <TeachingIcon subject={value} /> {value}
                </button>
              ))}
            </div>
          </>
        )}
        {grade && subject && (
          <>
            <h2>3. Choose your topic</h2>
            <div className="teaching-choice-grid">
              {distinct(
                scoped
                  .filter(
                    (x) => !params.get("term") || x.term === params.get("term"),
                  )
                  .map((x) => x.topic),
              ).map((value) => (
                <button
                  key={value}
                  className="teaching-choice"
                  aria-pressed={topic === value}
                  onClick={() => update("topic", value)}
                >
                  <TeachingIcon subject={subject} /> {value}
                </button>
              ))}
            </div>
          </>
        )}
        {topic && session.can("lessons.manage") && (
          <p>
            <Link className="button" to={createPath}>
              Write a plan for {topic}
            </Link>
          </p>
        )}
        {topic &&
          distinct(
            scoped
              .filter(
                (x) =>
                  x.topic === topic &&
                  (!params.get("term") || x.term === params.get("term")),
              )
              .flatMap((x) => (x.subTopic ? [x.subTopic] : [])),
          ).length > 0 && (
            <>
              <h2>4. Choose a subtopic (optional)</h2>
              <div className="teaching-choice-grid">
                <button
                  className="teaching-choice"
                  aria-pressed={!params.get("subTopic")}
                  onClick={() => update("subTopic", "")}
                >
                  All subtopics
                </button>
                {distinct(
                  scoped
                    .filter(
                      (x) =>
                        x.topic === topic &&
                        (!params.get("term") || x.term === params.get("term")),
                    )
                    .flatMap((x) => (x.subTopic ? [x.subTopic] : [])),
                ).map((value) => (
                  <button
                    className="teaching-choice"
                    key={value}
                    aria-pressed={params.get("subTopic") === value}
                    onClick={() => update("subTopic", value)}
                  >
                    {value}
                  </button>
                ))}
              </div>
            </>
          )}
        <p className="muted">
          Choose a plan below to adapt it, or write your own. No typing needed
          to browse.
        </p>
      </section>
      <details
        open={params.has("text") || undefined}
        className="card catalogue-tools"
      >
        <summary>Search and filters</summary>
        <section
          className="card catalogue-tools"
          aria-label="Choose learning context"
        >
          <SearchBar
            value={params.get("text") ?? ""}
            onSearch={(text) => update("text", text)}
            placeholder="Search lesson titles, topics and keywords…"
          />
          <div className="filter-row">
            <label>
              Curriculum collection
              <select
                value={year}
                onChange={(e) => update("year", e.target.value)}
              >
                <option value="">All collections — labelled separately</option>
                {distinct(choices.map((x) => String(x.year))).map((x) => (
                  <option key={x} value={x}>
                    {x} Zambian curriculum
                  </option>
                ))}
              </select>
            </label>
            <label>
              Grade
              <select
                value={grade}
                onChange={(e) => update("grade", e.target.value)}
              >
                <option value="">Choose a grade…</option>
                {distinct(
                  choices
                    .filter((x) => !year || String(x.year) === year)
                    .map((x) => String(x.grade)),
                ).map((x) => (
                  <option key={x} value={x}>
                    Grade {x}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Subject
              <select
                value={subject}
                onChange={(e) => update("subject", e.target.value)}
              >
                <option value="">All subjects</option>
                {distinct(subjectChoices.map((x) => x.subject)).map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </label>
            <label>
              Topic
              <select
                value={topic}
                onChange={(e) => update("topic", e.target.value)}
              >
                <option value="">All topics</option>
                {distinct(
                  scoped
                    .filter(
                      (x) =>
                        !params.get("term") || x.term === params.get("term"),
                    )
                    .map((x) => x.topic),
                ).map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </select>
            </label>
            <label>
              Term
              <select
                value={params.get("term") ?? ""}
                onChange={(e) => update("term", e.target.value)}
              >
                <option value="">All / unspecified in source</option>
                {distinct(scoped.flatMap((x) => (x.term ? [x.term] : []))).map(
                  (x) => (
                    <option key={x}>{x}</option>
                  ),
                )}
              </select>
            </label>
            <label>
              Show
              <select
                value={params.get("type") ?? ""}
                onChange={(e) => update("type", e.target.value)}
              >
                <option value="">Plans and learning resources</option>
                <option value="lesson-plan">Mwabu lesson plans</option>
                <option value="interactive-activity">
                  Interactive lessons
                </option>
                <option value="video">Videos</option>
                <option value="audio">Audio</option>
              </select>
            </label>
          </div>
          <p className="muted">
            2013 and 2023 source collections stay distinct. Linked older learner
            resources are shown as teaching associations; their original
            curriculum attribution is retained. Missing terms are not guessed.
          </p>
        </section>
      </details>
      <QueryState query={query}>
        {(result) => (
          <>
            <p role="status">
              {result.resources.totalCount} available resources
            </p>
            {!result.resources.items.length && (
              <section className="card">
                <h2>No reviewed resources match yet</h2>
                <p>
                  Try another grade or topic. Unreviewed source files stay out
                  of this library.
                </p>
                {session.can("lessons.manage") && (
                  <Link to={createPath}>
                    Create a lesson from the selected context
                  </Link>
                )}
              </section>
            )}
            <div className="content-grid">
              {result.resources.items.map((entry) => (
                <article className="content-card" key={entry.content.id}>
                  <div className="content-card-body">
                    <Badge>
                      {entry.isTemplate
                        ? "Mwabu lesson plan"
                        : entry.content.contentType.replaceAll("-", " ")}
                    </Badge>
                    <h2>
                      <Link
                        to={`/app/resources/${entry.content.id}?return=${encodeURIComponent(returnPath)}`}
                      >
                        {entry.content.title}
                      </Link>
                    </h2>
                    {entry.year && (
                      <p>
                        {entry.year} curriculum · Grade {entry.grade} ·{" "}
                        {entry.subject} · {entry.topic}
                      </p>
                    )}
                    {entry.associatedResource && (
                      <p>
                        Reviewed resource associated with this teaching context.
                      </p>
                    )}
                    {entry.subTopic && <p>Subtopic: {entry.subTopic}</p>}
                    {!entry.isTemplate && <p>{entry.content.summary}</p>}
                    <Link
                      className="button"
                      to={`/app/resources/${entry.content.id}?return=${encodeURIComponent(returnPath)}`}
                    >
                      {entry.isTemplate
                        ? "Open lesson plan"
                        : "Open learning resource"}
                    </Link>
                  </div>
                </article>
              ))}
            </div>
            <nav className="pagination" aria-label="Library pages">
              <button
                className="button secondary"
                disabled={result.resources.page <= 1}
                onClick={() => {
                  const n = new URLSearchParams(params);
                  n.set("page", String(result.resources.page - 1));
                  setParams(n);
                }}
              >
                Previous
              </button>
              <span>Page {result.resources.page}</span>
              <button
                className="button secondary"
                disabled={
                  result.resources.page * 24 >= result.resources.totalCount
                }
                onClick={() => {
                  const n = new URLSearchParams(params);
                  n.set("page", String(result.resources.page + 1));
                  setParams(n);
                }}
              >
                Next
              </button>
            </nav>
          </>
        )}
      </QueryState>
      {session.can("lessons.read") && session.organisationId && (
        <SchoolPlans
          key={JSON.stringify([
            session.organisationId,
            schoolContext.toString(),
            params.get("text") ?? "",
          ])}
          organisation={session.organisationId}
          text={params.get("text") ?? ""}
          contextQuery={schoolContext.toString()}
        />
      )}
    </>
  );
}

function SchoolPlans({
  organisation,
  text,
  contextQuery,
}: {
  organisation: string;
  text: string;
  contextQuery: string;
}) {
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ["school-library-plans", organisation, text, contextQuery, page],
    staleTime: 0,
    queryFn: ({ signal }) =>
      api<Page<Lesson>>(
        `/api/organisations/${organisation}/lessons?status=Published&pageSize=12&page=${page}&text=${encodeURIComponent(text)}${contextQuery ? "&" + contextQuery : ""}`,
        { signal },
      ),
  });
  return (
    <section className="card" aria-label="Authorised school lesson plans">
      <h2>Approved school lesson plans</h2>
      <p>
        These plans belong to your organisation and keep their own curriculum
        attribution. Your private drafts stay in My lesson plans.
      </p>
      {!!contextQuery && (
        <p>
          <Link
            to={`/app/lessons?status=Published&text=${encodeURIComponent(text)}`}
          >
            Find other school plans using their managed curriculum filters
          </Link>
        </p>
      )}
      <QueryState query={query}>
        {(result) => (
          <>
            {result.items?.length === 0 && (
              <p>No approved school plans match this search yet.</p>
            )}
            <div className="resource-grid">
              {result.items?.map((lesson) => (
                <article className="resource-card" key={lesson.id}>
                  <div>
                    <h3>
                      <Link to={`/app/lessons/${lesson.id}`}>
                        {lesson.title}
                      </Link>
                    </h3>
                    {lesson.importedFrom && (
                      <p>
                        Mwabu {lesson.importedFrom.curriculumYear} source ·
                        Grade {lesson.importedFrom.grade} ·{" "}
                        {lesson.importedFrom.subject} ·{" "}
                        {lesson.importedFrom.topic}
                        {lesson.importedFrom.term
                          ? " · " + lesson.importedFrom.term
                          : ""}
                      </p>
                    )}
                    <p>
                      Teaching target:{" "}
                      {[lesson.grade, lesson.subject, lesson.topic]
                        .filter(Boolean)
                        .join(" · ")}
                    </p>
                    <p>
                      {lesson.resourceCount ?? 0} learning resources attached
                    </p>
                    <Link
                      className="button secondary"
                      to={`/app/lessons/${lesson.id}`}
                    >
                      View lesson
                    </Link>
                  </div>
                </article>
              ))}
            </div>
            {(page > 1 || result.hasMore) && (
              <nav className="pagination" aria-label="School lesson pages">
                <button
                  className="button secondary"
                  disabled={page <= 1}
                  onClick={() => setPage(page - 1)}
                >
                  Previous school plans
                </button>
                <span>Page {page}</span>
                <button
                  className="button secondary"
                  disabled={!result.hasMore}
                  onClick={() => setPage(page + 1)}
                >
                  More school plans
                </button>
              </nav>
            )}
          </>
        )}
      </QueryState>
    </section>
  );
}
