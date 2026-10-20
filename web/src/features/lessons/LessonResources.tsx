import { useQueries } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../../api/client";
import type { Resource } from "../../api/types";
import type { LessonDocument } from "./types";
export default function LessonResources({
  document,
}: {
  document: LessonDocument;
}) {
  const ids = [
    ...new Set([
      ...document.references
        .map((r) => r.contentItemId)
        .filter((id): id is string => !!id),
      ...(document.teachingSteps ?? []).flatMap((step) =>
        step.resources.map((r) => r.contentItemId),
      ),
    ]),
  ].slice(0, 30);
  const resources = useQueries({
    queries: ids.map((id) => ({
      queryKey: ["lesson-approved-resource", id],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        api<Resource>(`/api/learning/content/${id}`, { signal }),
      retry: false,
    })),
  });
  return (
    <section className="card" aria-label="Lesson learning resources">
      <div className="section-heading">
        <h2>Learning resources for this lesson</h2>
        <Link
          className="button secondary"
          to={
            document.curriculum?.topicId
              ? `/app/resources?topicId=${document.curriculum.topicId}`
              : "/app/resources"
          }
          target="_blank"
          rel="noopener noreferrer"
        >
          Add learning resource
        </Link>
      </div>
      <p>
        Preview resources in a separate tab, then return to your lesson. Choose
        the exact teaching step in Lesson Journey.
      </p>
      {!ids.length && (
        <p>
          No learning resources attached yet. Find a published resource or add
          one in Lesson Journey.
        </p>
      )}
      <ul className="recent-lesson-list">
        {resources.map((q, i) => (
          <li key={ids[i]}>
            {q.isPending ? (
              "Checking resource…"
            ) : q.data ? (
              <>
                <Link
                  to={`/app/resources/${ids[i]}`}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  Preview {q.data.content.title}
                </Link>
                <span>
                  {q.data.content.contentType.replaceAll("-", " ")} ·{" "}
                  {q.data.assets.length} files
                </span>
              </>
            ) : (
              <span>
                This resource is currently unavailable. Choose a published
                replacement.
              </span>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
