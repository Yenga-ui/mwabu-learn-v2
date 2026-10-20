import { useQueries } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../../api/client";
import { useSession } from "../../auth/Session";
import { lessonBase, type Detail } from "./types";

const key = (org: string, user: string) => `mwabu:lesson-visits:${org}:${user}`;
export function recentLessonIds(org: string, user: string): string[] {
  try {
    const value: unknown = JSON.parse(
      sessionStorage.getItem(key(org, user)) ?? "[]",
    );
    return Array.isArray(value)
      ? value.filter((id): id is string => typeof id === "string").slice(0, 6)
      : [];
  } catch {
    return [];
  }
}
export function rememberLesson(org: string, user: string, id: string) {
  try {
    sessionStorage.setItem(
      key(org, user),
      JSON.stringify(
        [id, ...recentLessonIds(org, user).filter((x) => x !== id)].slice(0, 6),
      ),
    );
  } catch {
    /* Navigation still works when session storage is unavailable. */
  }
}
export default function RecentLessons() {
  const session = useSession();
  const org = session.organisationId!;
  const ids = recentLessonIds(org, session.user!.id);
  // Store IDs only; reauthorise every result before displaying any cached title.
  const queries = useQueries({
    queries: ids.map((id) => ({
      queryKey: ["recent-lesson", org, session.user!.id, id],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        api<Detail>(`${lessonBase(org)}/${id}`, { signal }),
      staleTime: 0,
      retry: false,
    })),
  });
  const lessons = queries
    .filter((q) => q.isSuccess && !q.isFetching)
    .map((q) => q.data!);
  return (
    <section className="card">
      <h2>Recently viewed lessons</h2>
      {lessons.length ? (
        <ul className="recent-lesson-list">
          {lessons.map((item) => (
            <li key={item.lesson.id}>
              <Link to={`/app/lessons/${item.lesson.id}`}>
                {item.lesson.title}
              </Link>
            </li>
          ))}
        </ul>
      ) : (
        <p>
          {queries.some((q) => q.isFetching)
            ? "Checking your recent lessons…"
            : "Lessons you open in this session will appear here."}
        </p>
      )}
    </section>
  );
}
