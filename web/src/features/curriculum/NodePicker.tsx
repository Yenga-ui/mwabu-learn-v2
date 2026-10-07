import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import type { Curriculum, Page, Structure } from "../../api/types";
import { nodeNames, nodeTypes } from "./Curriculum";
import { ErrorNotice, Loading, Pagination } from "../../components/ui";
export default function NodePicker({
  onSelect,
}: {
  onSelect: (type: string, id: string, name: string) => void;
}) {
  const [trail, setTrail] = useState<Structure[]>([]);
  const [page, setPage] = useState(1);
  const level = trail.length - 1;
  const query = useQuery({
    queryKey: ["node-picker", trail[0]?.id, trail.at(-1)?.id, page],
    queryFn: ({ signal }) =>
      level < 0
        ? api<Page<Curriculum>>(
            `/api/curricula/search?page=${page}&pageSize=25`,
            { signal },
          )
        : api<Page<Structure>>(
            `/api/curricula/${trail[0].id}/nodes/${nodeTypes[level]}?` +
              queryString({ parentId: trail.at(-1)?.id, page, pageSize: 25 }),
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
            {level < 0 ? "Curriculum" : nodeNames[level]}
            <select
              value=""
              onChange={(e) => {
                const node = query.data?.items.find(
                  (x) => x.id === e.target.value,
                );
                if (node) {
                  setTrail((t) => [...t, node]);
                  setPage(1);
                  if (level >= 0)
                    onSelect(nodeTypes[level], node.id, node.name);
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
          <Pagination
            page={page}
            hasMore={query.data?.hasMore ?? false}
            onChange={setPage}
          />
        </>
      )}
      <small>Continue through the hierarchy, or use the selected node.</small>
    </div>
  );
}
