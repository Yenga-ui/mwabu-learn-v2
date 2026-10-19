import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import type { Curriculum, Page, Structure } from "../../api/types";
import { nodeNames, nodeTypes } from "./Curriculum";
import { ErrorNotice, Loading, Pagination } from "../../components/ui";
export default function NodePicker({
  onSelect,
  onReset,
}: {
  onSelect: (type: string, id: string, name: string, context: string[]) => void;
  onReset?: () => void;
}) {
  const [trail, setTrail] = useState<Structure[]>([]);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [text, setText] = useState("");
  useEffect(() => {
    const timer = setTimeout(() => {
      setText(search.trim());
      setPage(1);
    }, 250);
    return () => clearTimeout(timer);
  }, [search]);
  const level = trail.length - 1;
  const query = useQuery({
    queryKey: ["node-picker", trail[0]?.id, trail.at(-1)?.id, page, text],
    queryFn: ({ signal }) =>
      level < 0
        ? api<Page<Curriculum>>(
            "/api/curricula/search?" +
              queryString({ page, pageSize: 25, text, activeOnly: true }),
            { signal },
          )
        : api<Page<Structure>>(
            `/api/curricula/${trail[0].id}/nodes/${nodeTypes[level]}?` +
              queryString({
                parentId: trail.at(-1)?.id,
                page,
                pageSize: 25,
                text,
                activeOnly: true,
              }),
            { signal },
          ),
    enabled: level < 7,
  });
  return (
    <div className="node-picker">
      <div className="curriculum-trail">
        <button
          type="button"
          onClick={() => {
            setTrail([]);
            setSearch("");
            setText("");
            onReset?.();
            setPage(1);
          }}
        >
          Curricula
        </button>
        {trail.map((x, i) => (
          <button
            key={x.id}
            type="button"
            onClick={() => {
              setTrail((t) => t.slice(0, i + 1));
              setSearch("");
              setText("");
              onReset?.();
              setPage(1);
            }}
          >
            {x.name} ›
          </button>
        ))}
      </div>
      {level < 7 && (
        <>
          <label>
            Search {level < 0 ? "curriculum" : nodeNames[level].toLowerCase()}
            <input
              type="search"
              maxLength={200}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Type part of a name…"
            />
          </label>
          <label>
            {level < 0 ? "Curriculum" : nodeNames[level]}
            <select
              value=""
              disabled={
                query.isFetching || text !== search.trim() || !!query.error
              }
              aria-busy={query.isFetching}
              onChange={(e) => {
                const node = query.data?.items.find(
                  (x) => x.id === e.target.value,
                );
                if (node) {
                  onReset?.();
                  setTrail((t) => [...t, node]);
                  setSearch("");
                  setText("");
                  setPage(1);
                  if (level >= 0)
                    onSelect(
                      nodeTypes[level],
                      node.id,
                      node.name,
                      [...trail, node].map((x) => x.name),
                    );
                }
              }}
            >
              <option value="">Choose a learning context…</option>
              {query.data?.items.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </select>
          </label>
          {query.isPending && <Loading label="Loading curriculum…" />}
          {query.error && (
            <ErrorNotice
              error={query.error}
              retry={() => void query.refetch()}
            />
          )}
          {!query.isFetching &&
            !query.error &&
            query.data?.items.length === 0 && (
              <p role="status">
                No active choices match
                {search ? " your search" : " this selection"}.{" "}
                {search
                  ? "Try another name."
                  : "Ask your curriculum administrator to add the missing records."}
              </p>
            )}
          <Pagination
            page={page}
            hasMore={query.data?.hasMore ?? false}
            onChange={setPage}
          />
        </>
      )}
      <small>
        Choose by name. Changing a parent clears the choices below it.
      </small>
    </div>
  );
}
