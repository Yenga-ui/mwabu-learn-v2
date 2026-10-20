import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, queryString } from "../../api/client";
import type { Curriculum, Page, Structure } from "../../api/types";
import { nodeNames, nodeTypes } from "./Curriculum";
import { ErrorNotice, Loading, Pagination } from "../../components/ui";
export default function NodePicker({
  onSelect,
  onReset,
  startWithGrade = false,
}: {
  onSelect: (type: string, id: string, name: string, context: string[]) => void;
  onReset?: () => void;
  startWithGrade?: boolean;
}) {
  const [trail, setTrail] = useState<Pick<Structure, "id" | "name">[]>([]);
  const [gradeFirst, setGradeFirst] = useState(startWithGrade);
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
  const grades = useQuery({
    queryKey: ["grade-first-picker", page, text],
    queryFn: ({ signal }) =>
      api<
        Page<{
          id: string;
          name: string;
          curriculumId: string;
          curriculumName: string;
          versionId: string;
          versionName: string;
        }>
      >(
        "/api/curriculum/grades/search?" +
          queryString({ page, pageSize: 25, text }),
        { signal },
      ),
    enabled: gradeFirst,
  });
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
    enabled: level < 7 && !gradeFirst,
  });
  return (
    <div className="node-picker">
      <button
        type="button"
        className="button secondary"
        onClick={() => {
          setGradeFirst((x) => !x);
          setPage(1);
          setSearch("");
          setText("");
          setTrail([]);
          onReset?.();
        }}
      >
        {gradeFirst ? "Choose curriculum first" : "Start with your grade"}
      </button>
      {gradeFirst && (
        <>
          <label>
            Search grades
            <input
              type="search"
              maxLength={200}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <label>
            Grade and curriculum
            <select
              value=""
              disabled={
                grades.isFetching || search.trim() !== text || !!grades.error
              }
              onChange={(e) => {
                const row = grades.data?.items.find(
                  (x) => x.id === e.target.value,
                );
                if (row) {
                  setTrail([
                    { id: row.curriculumId, name: row.curriculumName },
                    { id: row.versionId, name: row.versionName },
                    { id: row.id, name: row.name },
                  ]);
                  setGradeFirst(false);
                  setPage(1);
                  setSearch("");
                  setText("");
                  onReset?.();
                  onSelect("Grade", row.id, row.name, [
                    row.curriculumName,
                    row.versionName,
                    row.name,
                  ]);
                }
              }}
            >
              <option value="">Choose a grade and curriculum…</option>
              {grades.data?.items.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name} · {x.curriculumName} · {x.versionName}
                </option>
              ))}
            </select>
          </label>
          {grades.isFetching && <Loading />}
          {grades.error && (
            <ErrorNotice
              error={grades.error}
              retry={() => void grades.refetch()}
            />
          )}
          <Pagination
            page={page}
            hasMore={grades.data?.hasMore ?? false}
            onChange={setPage}
          />
          {grades.data?.items.length === 0 && (
            <p>No active grades match your search.</p>
          )}
        </>
      )}
      {!gradeFirst && (
        <>
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
                Search{" "}
                {level < 0 ? "curriculum" : nodeNames[level].toLowerCase()}
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
        </>
      )}
      <small>
        Choose by name. Changing a parent clears the choices below it.
      </small>
    </div>
  );
}
