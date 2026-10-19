import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import { useSession } from "../../auth/Session";
import type { Page } from "../../api/types";
import { ErrorNotice, Pagination, QueryState } from "../../components/ui";
export default function AttachToLesson({ resourceId }: { resourceId: string }) {
  const session = useSession();
  const org = session.organisationId;
  const [open, setOpen] = useState(false),
    [input, setInput] = useState(""),
    [text, setText] = useState(""),
    [page, setPage] = useState(1),
    [selected, setSelected] = useState(""),
    [busy, setBusy] = useState(false),
    [saved, setSaved] = useState(false),
    [error, setError] = useState<unknown>();
  const base = `/api/organisations/${org}/lessons`;
  const drafts = useQuery({
    queryKey: ["attach-resource-drafts", org, page, text],
    queryFn: ({ signal }) =>
      api<Page<{ id: string; title: string; ownerUserId: string }>>(
        base + "?" + queryString({ status: "Draft", page, pageSize: 25, text }),
        { signal },
      ),
    enabled: open && !!org,
  });
  return (
    <details onToggle={(e) => setOpen(e.currentTarget.open)}>
      <summary>Attach to an existing lesson</summary>
      <p>
        Attach this published resource to one of your drafts, then review and
        select it in Source Explorer. Other lesson text stays intact.
      </p>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setText(input.trim());
          setPage(1);
          setSelected("");
          setSaved(false);
        }}
      >
        <label>
          Find your draft
          <input
            type="search"
            maxLength={200}
            value={input}
            onChange={(e) => setInput(e.target.value)}
          />
        </label>
        <button className="button secondary">Search draft lessons</button>
      </form>
      <QueryState query={drafts}>
        {(data) => (
          <>
            <label>
              Draft lesson
              <select
                value={selected}
                onChange={(e) => {
                  setSelected(e.target.value);
                  setSaved(false);
                }}
              >
                <option value="">Choose a draft…</option>
                {data.items
                  .filter((x) => x.ownerUserId === session.user?.id)
                  .map((x) => (
                    <option key={x.id} value={x.id}>
                      {x.title}
                    </option>
                  ))}
              </select>
            </label>
            <Pagination
              page={page}
              hasMore={!!data.hasMore}
              onChange={(next) => {
                setPage(next);
                setSelected("");
                setSaved(false);
              }}
            />
          </>
        )}
      </QueryState>
      <button
        className="button secondary"
        disabled={!selected || busy || saved}
        onClick={() => {
          setBusy(true);
          setError(undefined);
          void api(base + `/${selected}/sources`, {
            method: "POST",
            body: { contentItemId: resourceId, url: null, title: null },
          })
            .then(() => setSaved(true))
            .catch(setError)
            .finally(() => setBusy(false));
        }}
      >
        {saved ? "Resource attached for review" : "Attach resource to draft"}
      </button>
      {selected && (
        <Link to={`/app/lessons/${selected}`}>
          Open lesson to review resources
        </Link>
      )}
      {!!error && <ErrorNotice error={error} />}
    </details>
  );
}
