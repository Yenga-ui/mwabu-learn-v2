import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { BookOpen, ChevronDown, GraduationCap } from "lucide-react";
import { api, queryString } from "../../api/client";
import type { Page, Structure } from "../../api/types";
import { Pagination, QueryState, SearchBar } from "../../components/ui";
import TeachingIcon from "../../components/TeachingIcon";

interface Grade extends Structure {
  curriculumId: string;
  curriculumName: string;
  versionId: string;
  versionName: string;
}
function useNodes(
  root: string | undefined,
  parent: string | undefined,
  type: string,
  page = 1,
  text = "",
) {
  return useQuery({
    queryKey: ["teacher-curriculum", root, parent, type, page, text],
    queryFn: ({ signal }) =>
      api<Page<Structure>>(
        `/api/curricula/${root}/nodes/${type}?` +
          queryString({
            parentId: parent,
            activeOnly: true,
            page,
            pageSize: 30,
            text,
          }),
        { signal },
      ),
    enabled: !!root && !!parent,
  });
}
export default function TeacherCurriculum() {
  const [params, setParams] = useSearchParams();
  const [gradePage, setGradePage] = useState(1);
  const [subjectPage, setSubjectPage] = useState(1);
  const [termPage, setTermPage] = useState(1);
  const [page, setPage] = useState(1);
  const grades = useQuery({
    queryKey: ["teacher-curriculum-grades", gradePage],
    queryFn: ({ signal }) =>
      api<Page<Grade>>(
        "/api/curriculum/grades/search?" +
          queryString({ page: gradePage, pageSize: 30 }),
        { signal },
      ),
  });
  const grade = grades.data?.items.find((x) => x.id === params.get("gradeId"));
  const subjects = useNodes(
    grade?.curriculumId,
    grade?.id,
    "Subject",
    subjectPage,
  );
  const subject = subjects.data?.items.find(
    (x) => x.id === params.get("subjectId"),
  );
  const terms = useNodes(grade?.curriculumId, subject?.id, "Term", termPage);
  const term = terms.data?.items.find((x) => x.id === params.get("termId"));
  const topics = useNodes(
    grade?.curriculumId,
    term?.id,
    "Topic",
    page,
    params.get("text") ?? "",
  );
  const choose = (key: string, id: string) => {
    const next = new URLSearchParams(params);
    const order = ["gradeId", "subjectId", "termId"];
    order.slice(order.indexOf(key) + 1).forEach((k) => next.delete(k));
    next.delete("text");
    next.set(key, id);
    setParams(next);
    setPage(1);
    if (key === "gradeId") {
      setSubjectPage(1);
      setTermPage(1);
    } else if (key === "subjectId") setTermPage(1);
  };
  const versions = [
    ...new Set(grades.data?.items.map((x) => x.versionId) ?? []),
  ];
  const version = params.get("version") ?? "";
  return (
    <div className="curriculum-workspace">
      <aside className="curriculum-filters" aria-label="Curriculum selection">
        <div className="filter-heading">
          <GraduationCap size={20} />
          <h1>Curriculum</h1>
        </div>
        <h2>Grade</h2>
        <QueryState query={grades}>
          {(data) => (
            <>
              <div className="grade-buttons">
                {data.items
                  .filter((x) => !version || x.versionId === version)
                  .map((x) => (
                    <button
                      key={x.id}
                      aria-pressed={grade?.id === x.id}
                      onClick={() => choose("gradeId", x.id)}
                      title={x.curriculumName + " · " + x.versionName}
                    >
                      {x.name}
                      {data.items.some(
                        (other) => other.id !== x.id && other.name === x.name,
                      ) && (
                        <small>
                          {x.versionName.match(/\b(?:19|20)\d{2}\b/)?.[0] ??
                            x.versionName}
                        </small>
                      )}
                    </button>
                  ))}
              </div>
              {!data.items.length && (
                <p>No accessible grades are available yet.</p>
              )}
              {(gradePage > 1 || data.hasMore) && (
                <Pagination
                  page={gradePage}
                  hasMore={!!data.hasMore}
                  onChange={(p) => {
                    setGradePage(p);
                    setParams({});
                  }}
                />
              )}
            </>
          )}
        </QueryState>
        <h2>Subject</h2>
        {!grade ? (
          <p>Choose your grade to see subjects.</p>
        ) : (
          <QueryState query={subjects}>
            {(data) => (
              <>
                <div className="subject-buttons">
                  {data.items.map((x) => (
                    <button
                      key={x.id}
                      aria-pressed={subject?.id === x.id}
                      onClick={() => choose("subjectId", x.id)}
                    >
                      <TeachingIcon subject={x.name} />
                      {x.name}
                    </button>
                  ))}
                </div>
                {!data.items.length && (
                  <p>No subjects are available for this grade.</p>
                )}
                {(subjectPage > 1 || data.hasMore) && (
                  <Pagination
                    page={subjectPage}
                    hasMore={!!data.hasMore}
                    onChange={(p) => {
                      setSubjectPage(p);
                      const n = new URLSearchParams(params);
                      n.delete("subjectId");
                      n.delete("termId");
                      setParams(n);
                    }}
                  />
                )}
              </>
            )}
          </QueryState>
        )}
        <label>
          Curriculum version
          <select
            value={version}
            onChange={(e) => {
              setParams(e.target.value ? { version: e.target.value } : {});
              setPage(1);
            }}
          >
            <option value="">All available versions</option>
            {versions.map((id) => (
              <option key={id} value={id}>
                {
                  grades.data?.items.find((x) => x.versionId === id)
                    ?.versionName
                }
              </option>
            ))}
          </select>
        </label>
        {subject && (
          <QueryState query={terms}>
            {(data) => (
              <>
                <label>
                  Term / teaching sequence
                  <select
                    value={term?.id ?? ""}
                    disabled={!data.items.length}
                    onChange={(e) => choose("termId", e.target.value)}
                  >
                    <option value="">Choose a verified term</option>
                    {data.items.map((x) => (
                      <option key={x.id} value={x.id}>
                        {x.name}
                      </option>
                    ))}
                  </select>
                </label>
                {!data.items.length && (
                  <p>
                    No term mapping is available. Browse published topics
                    instead; terms are not guessed.
                  </p>
                )}
                {(termPage > 1 || data.hasMore) && (
                  <Pagination
                    page={termPage}
                    hasMore={!!data.hasMore}
                    onChange={(p) => {
                      setTermPage(p);
                      choose("termId", "");
                    }}
                  />
                )}
              </>
            )}
          </QueryState>
        )}
        <Link className="filter-library-link" to="/app/library">
          <BookOpen size={17} /> Browse lesson plans and resources
        </Link>
      </aside>
      <section className="curriculum-content">
        <header>
          <p className="eyebrow">YOUR TEACHING CONTEXT</p>
          <h2>
            {grade
              ? [grade.name, subject?.name].filter(Boolean).join(" · ")
              : "Start with your grade"}
          </h2>
          <p>
            {grade
              ? `${grade.curriculumName} · ${grade.versionName}`
              : "Choose a grade and subject. Find topics by name, then explore their learning outcomes."}
          </p>
        </header>
        {!term &&
        subject &&
        grade &&
        terms.isSuccess &&
        !terms.data.items.length ? (
          <PublishedTopics grade={grade} subject={subject.name} />
        ) : !term ? (
          <div className="curriculum-empty">
            <GraduationCap size={32} />
            <h3>
              {subject
                ? "Choose a term to explore topics"
                : "A clear path to your next lesson"}
            </h3>
            <p>Grade → Subject → Verified term → Topic → Learning outcome</p>
            <Link className="button secondary" to="/app/library">
              Find available teaching materials
            </Link>
          </div>
        ) : (
          <>
            <SearchBar
              value={params.get("text") ?? ""}
              placeholder="Search topics by name"
              onSearch={(text) => {
                const n = new URLSearchParams(params);
                n.set("text", text);
                setParams(n);
                setPage(1);
              }}
            />
            <QueryState query={topics}>
              {(data) => (
                <>
                  {!data.items.length && (
                    <p>No topics match. Try another search or term.</p>
                  )}
                  {data.items.map((topic) => (
                    <Topic
                      key={topic.id}
                      root={grade!.curriculumId}
                      topic={topic}
                    />
                  ))}
                  <Pagination
                    page={page}
                    hasMore={!!data.hasMore}
                    onChange={setPage}
                  />
                </>
              )}
            </QueryState>
          </>
        )}
      </section>
    </div>
  );
}
function PublishedTopics({
  grade,
  subject,
}: {
  grade: Grade;
  subject: string;
}) {
  const query = useQuery({
    queryKey: ["teacher-published-contexts"],
    queryFn: ({ signal }) =>
      api<{
        choices: {
          year: number;
          grade: number;
          subject: string;
          topic: string;
          term: string | null;
          subTopic?: string | null;
        }[];
      }>("/api/learning/library?pageSize=1", { signal }),
  });
  const year = grade.versionName.match(/\b(?:19|20)\d{2}\b/)?.[0];
  const number = grade.name.match(/\d+/)?.[0];
  return (
    <QueryState query={query}>
      {(data) => {
        const choices = data.choices.filter(
          (x) =>
            String(x.grade) === number &&
            x.subject === subject &&
            (!year || String(x.year) === year),
        );
        const topics = [...new Set(choices.map((x) => x.topic))];
        return (
          <>
            <p className="notice">
              Published plan topics are available below. Managed term and
              outcome mappings are not recorded for this subject yet.
            </p>
            {!topics.length && (
              <p>
                No published topics match this context yet.{" "}
                <Link to="/app/library">Browse all available materials</Link>.
              </p>
            )}
            {topics.map((topic) => (
              <section className="curriculum-topic" key={topic}>
                <h3>{topic}</h3>
                {choices
                  .filter((x) => x.topic === topic)
                  .map((x, index) => (
                    <div className="curriculum-outcome" key={index}>
                      <p>
                        {x.year} source collection
                        {x.term ? ` · ${x.term}` : " · Term not recorded"}
                        {x.subTopic ? ` · ${x.subTopic}` : ""}
                      </p>
                      <Link
                        to={
                          "/app/library?" +
                          new URLSearchParams({
                            year: String(x.year),
                            grade: String(x.grade),
                            subject: x.subject,
                            topic: x.topic,
                            ...(x.subTopic ? { subTopic: x.subTopic } : {}),
                            ...(x.term ? { term: x.term } : {}),
                          })
                        }
                      >
                        View lesson plans and resources →
                      </Link>
                    </div>
                  ))}
              </section>
            ))}
          </>
        );
      }}
    </QueryState>
  );
}
function Topic({ root, topic }: { root: string; topic: Structure }) {
  const [open, setOpen] = useState(false);
  return (
    <section className="curriculum-topic">
      <h3>
        <button
          className="topic-heading"
          aria-label={topic.name}
          aria-expanded={open}
          onClick={() => setOpen(!open)}
        >
          {topic.name}
          <ChevronDown size={18} aria-hidden="true" />
        </button>
      </h3>
      {topic.description && <p>{topic.description}</p>}
      {open && (
        <OutcomeGroups root={root} parent={topic.id} type="Competency" />
      )}
    </section>
  );
}
function OutcomeGroups({
  root,
  parent,
  type,
}: {
  root: string;
  parent: string;
  type: "Competency" | "LearningOutcome";
}) {
  const [page, setPage] = useState(1);
  const query = useNodes(root, parent, type, page);
  return (
    <QueryState query={query}>
      {(data) => (
        <>
          {!data.items.length && (
            <p>
              No {type === "Competency" ? "competencies" : "learning outcomes"}{" "}
              are recorded here yet.
            </p>
          )}
          {data.items.map((node) => (
            <div key={node.id} className="curriculum-outcome">
              <h4>{node.name}</h4>
              {node.description && <p>{node.description}</p>}
              {type === "Competency" ? (
                <OutcomeGroups
                  root={root}
                  parent={node.id}
                  type="LearningOutcome"
                />
              ) : (
                <Link
                  to={`/app/lessons?nodeType=LearningOutcome&nodeId=${node.id}&context=${encodeURIComponent(node.name)}`}
                >
                  Find linked lesson plans →
                </Link>
              )}
              {node.code && (
                <details>
                  <summary>Curriculum reference</summary>
                  <small>{node.code}</small>
                </details>
              )}
            </div>
          ))}
          {(page > 1 || data.hasMore) && (
            <Pagination
              page={page}
              hasMore={!!data.hasMore}
              onChange={setPage}
            />
          )}
        </>
      )}
    </QueryState>
  );
}
