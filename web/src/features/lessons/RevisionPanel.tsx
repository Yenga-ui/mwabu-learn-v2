import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "../../api/client";
import type { Page } from "../../api/types";
import { ErrorNotice, Pagination, QueryState } from "../../components/ui";
import type { Detail, LessonDocument } from "./types";

export function DocumentComparison({
  before,
  after,
}: {
  before: LessonDocument;
  after: LessonDocument;
}) {
  const changed = after.sections.filter((section) => {
    const prior = before.sections.find((x) => x.code === section.code);
    return (
      !prior ||
      prior.body !== section.body ||
      prior.minutes !== section.minutes ||
      prior.title !== section.title ||
      JSON.stringify(prior.sourceIds) !== JSON.stringify(section.sourceIds)
    );
  });
  return (
    <div className="lesson-comparison">
      {JSON.stringify(before.setup) !== JSON.stringify(after.setup) && (
        <details>
          <summary>Setup changes</summary>
          <div className="lesson-compare-grid">
            <pre>{JSON.stringify(before.setup, null, 2)}</pre>
            <pre>{JSON.stringify(after.setup, null, 2)}</pre>
          </div>
        </details>
      )}
      {changed.length === 0 ? (
        <p>No section changes.</p>
      ) : (
        changed.map((section) => (
          <details key={section.code} open>
            <summary>{section.title}</summary>
            <div className="lesson-compare-grid">
              <div>
                <h4>Before</h4>
                <p>
                  {before.sections.find((x) => x.code === section.code)
                    ?.minutes ?? 0}{" "}
                  minutes
                </p>
                <pre>
                  {before.sections.find((x) => x.code === section.code)?.body ||
                    "Empty section"}
                </pre>
              </div>
              <div>
                <h4>Proposed / later version</h4>
                <p>{section.minutes} minutes</p>
                <pre>{section.body}</pre>
              </div>
            </div>
          </details>
        ))
      )}
      {before.sections
        .filter((x) => !after.sections.some((s) => s.code === x.code))
        .map((x) => (
          <p key={x.code}>Removed section: {x.title}</p>
        ))}
    </div>
  );
}
export default function RevisionPanel({
  path,
  current,
  editable,
  restore,
}: {
  path: string;
  current: LessonDocument;
  editable: boolean;
  restore: (value: LessonDocument) => void;
}) {
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState<number>();
  const history = useQuery({
    queryKey: ["lesson-revisions", path, page],
    queryFn: ({ signal }) =>
      api<Page<{ number: number; reason: string; createdAt: string }>>(
        path + `/revisions?page=${page}&pageSize=20`,
        { signal },
      ),
  });
  const selected = useQuery({
    queryKey: ["lesson-revision", path, revision],
    queryFn: ({ signal }) =>
      api<Detail>(path + `?revision=${revision}`, { signal }),
    enabled: !!revision,
  });
  return (
    <section className="panel">
      <h2>Revision history and comparison</h2>
      <p>
        Each successful save and lifecycle transition creates an immutable
        snapshot. Restoring creates a new draft revision.
      </p>
      <QueryState query={history}>
        {(data) => (
          <>
            <label>
              Compare saved revision
              <select
                value={revision ?? ""}
                onChange={(e) =>
                  setRevision(Number(e.target.value) || undefined)
                }
              >
                <option value="">Choose a revision</option>
                {data.items.map((item) => (
                  <option key={item.number} value={item.number}>
                    Revision {item.number} · {item.reason} ·{" "}
                    {new Date(item.createdAt).toLocaleString()}
                  </option>
                ))}
              </select>
            </label>
            <Pagination
              page={page}
              hasMore={data.hasMore ?? false}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>
      {revision && (
        <>
          {selected.error && <ErrorNotice error={selected.error} />}
          {selected.data && (
            <>
              <DocumentComparison
                before={selected.data.document}
                after={current}
              />
              {editable && (
                <button
                  className="button secondary"
                  onClick={() => {
                    if (
                      window.confirm(
                        "Restore this earlier content as a new draft edit? Current unsaved edits will be replaced.",
                      )
                    )
                      restore(selected.data.document);
                  }}
                >
                  Restore selected revision into draft
                </button>
              )}
            </>
          )}
        </>
      )}
    </section>
  );
}
