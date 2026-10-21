import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { BookOpen, FilePlus, Play, Search } from "lucide-react";
import { api } from "../../api/client";
import type { Page } from "../../api/types";
import { useSession } from "../../auth/Session";
import { PageTitle, QueryState } from "../../components/ui";
import { lessonBase, type Lesson } from "../lessons/types";
import RecentLessons from "../lessons/RecentLessons";
import ContinueTeaching from "../lessons/ContinueTeaching";

export default function TeacherHome() {
  const session = useSession();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const grades = useQuery({
    queryKey: ["teacher-home-library-grades"],
    queryFn: ({ signal }) =>
      api<{ choices: { grade: number }[] }>(
        "/api/learning/library?pageSize=1",
        { signal },
      ),
    enabled: session.can("content.read", "catalogue"),
  });
  const drafts = useQuery({
    queryKey: ["teacher-home-drafts", session.organisationId],
    queryFn: ({ signal }) =>
      api<Page<Lesson>>(
        lessonBase(session.organisationId!) +
          "?mine=true&pageSize=4&status=Draft",
        { signal },
      ),
    enabled: !!session.organisationId && session.can("lessons.read"),
  });
  return (
    <>
      <PageTitle
        eyebrow={`WELCOME, ${session.user?.firstName.toUpperCase()}`}
        title="What would you like to teach?"
      >
        Find a lesson, make it your own and bring it to your classroom.
      </PageTitle>
      <section
        className="card teaching-choices"
        aria-label="Start with your grade"
      >
        <h2>Choose your grade</h2>
        <p>
          Next, choose a subject and topic to see lesson plans and activities.
        </p>
        <QueryState query={grades}>
          {(data) => (
            <div className="teaching-choice-grid">
              {[...new Set(data.choices.map((choice) => choice.grade))]
                .sort((a, b) => a - b)
                .map((grade) => (
                  <Link
                    className="teaching-choice"
                    key={grade}
                    to={`/app/library?grade=${grade}`}
                  >
                    Grade {grade}
                  </Link>
                ))}
              {!data.choices.length && (
                <p>
                  No published curriculum resources are available yet. You can
                  still create your own plan below.
                </p>
              )}
            </div>
          )}
        </QueryState>
      </section>
      <details>
        <summary>Search for a lesson</summary>
        <form
          className="card teacher-home-search"
          onSubmit={(e) => {
            e.preventDefault();
            navigate("/app/library?text=" + encodeURIComponent(search.trim()));
          }}
        >
          <label>
            Search lessons
            <input
              type="search"
              value={search}
              maxLength={200}
              placeholder="Try Grade 4 mathematics or fractions"
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <button className="button">
            <Search aria-hidden="true" size={20} /> Find a lesson
          </button>
        </form>
      </details>
      <nav className="teacher-tasks" aria-label="Teaching tasks">
        <Link className="card" to="/app/library">
          <BookOpen aria-hidden="true" />
          <strong>Find a lesson</strong>
          <span>Choose a grade, subject and topic.</span>
        </Link>
        <Link className="card" to="/app/lessons?mine=true">
          <BookOpen aria-hidden="true" />
          <strong>My lesson plans</strong>
          <span>Reopen and adapt your saved plans.</span>
        </Link>
        {session.can("lessons.manage") && (
          <Link className="card" to="/app/lessons/new?planning=topics">
            <FilePlus aria-hidden="true" />
            <strong>Create a lesson</strong>
            <span>Start with the official lesson plan.</span>
          </Link>
        )}
        {session.can("lessons.manage") && (
          <Link
            className="card"
            to="/app/lessons/new?planning=topics&assist=ai"
          >
            <FilePlus aria-hidden="true" />
            <strong>Create with AI</strong>
            <span>Choose a topic, then review an AI proposal.</span>
          </Link>
        )}
        <Link className="card" to="/app/resources">
          <Search aria-hidden="true" />
          <strong>Learning resources</strong>
          <span>Find videos, documents and activities.</span>
        </Link>
        <Link className="card" to="/app/lessons?teach=true">
          <Play aria-hidden="true" />
          <strong>Teach a lesson</strong>
          <span>Open your saved Lesson Journey.</span>
        </Link>
      </nav>
      <ContinueTeaching />
      <section className="card">
        <div className="section-heading">
          <h2>Your recent drafts</h2>
          <Link to="/app/lessons?mine=true">My lesson plans</Link>
        </div>
        <QueryState query={drafts}>
          {(page) =>
            page.items.length ? (
              <ul className="recent-lesson-list">
                {page.items.map((lesson) => (
                  <li key={lesson.id}>
                    <Link to={`/app/lessons/${lesson.id}`}>{lesson.title}</Link>
                    <span>
                      {lesson.grade} {lesson.subject}
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              <p>
                Your saved drafts will appear here. Start by finding a lesson to
                use or creating your own.
              </p>
            )
          }
        </QueryState>
      </section>
      {session.organisationId && session.user && <RecentLessons />}
    </>
  );
}
