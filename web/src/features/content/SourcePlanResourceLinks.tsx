import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "../../api/client";
import type { Content, Page, Resource } from "../../api/types";
import type { OfficialTemplate } from "./OfficialSourcePlan";
import { Action, ErrorNotice } from "../../components/ui";
import { useSession } from "../../auth/Session";

export default function SourcePlanResourceLinks({ id }: { id: string }) {
  const session = useSession();
  const client = useQueryClient();
  const [text, setText] = useState("");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState("");
  const [asset, setAsset] = useState("");
  const [dependency, setDependency] = useState("");
  const [evidence, setEvidence] = useState("");
  const source = useQuery({
    queryKey: ["official-source-plan", id],
    queryFn: () => api<OfficialTemplate>(`/api/learning/library/plans/${id}`),
    retry: false,
  });
  const found = useQuery({
    queryKey: ["source-link-search", search],
    queryFn: () =>
      api<Page<Content>>(
        `/api/learning/content?pageSize=30&text=${encodeURIComponent(search)}`,
      ),
    enabled: search.length > 0,
  });
  const resource = useQuery({
    queryKey: ["source-link-assets", selected],
    queryFn: () => api<Resource>(`/api/learning/content/${selected}`),
    enabled: !!selected,
  });
  if (source.error instanceof ApiError && source.error.status === 404)
    return null;
  if (!source.data)
    return source.error ? <ErrorNotice error={source.error} /> : null;
  return (
    <section className="card" aria-label="Review source resource associations">
      <h2>Associated learner resources</h2>
      <p>
        Choose an exact published file version and record evidence that it
        matches a named reference in this plan. A teaching association does not
        relabel a legacy resource's curriculum.
      </p>
      {source.data.resources.map((r) => (
        <div className="action-row" key={r.assetId}>
          <span>{r.title}</span>
          {session.can("content.publish", "global") && (
            <Action
              confirm="Remove this association from future source copies? Existing saved lessons retain their pinned versions."
              run={async () => {
                await api(
                  `/api/content-imports/plans/${id}/resources/${r.assetId}`,
                  { method: "DELETE" },
                );
                await client.invalidateQueries({
                  queryKey: ["official-source-plan", id],
                });
              }}
            >
              Unlink resource
            </Action>
          )}
        </div>
      ))}
      {session.can("content.publish", "global") && (
        <>
          <label>
            Source resource reference
            <select
              value={dependency}
              onChange={(e) => setDependency(e.target.value)}
            >
              <option value="">Choose an unresolved source reference…</option>
              {source.data.missingDependencies.map((d) => (
                <option key={d}>{d}</option>
              ))}
            </select>
          </label>
          <label>
            Search published learner resources
            <input value={text} onChange={(e) => setText(e.target.value)} />
          </label>
          <button
            className="button secondary"
            onClick={() => {
              setSearch(text.trim());
              setSelected("");
              setAsset("");
            }}
          >
            Find matching resources
          </button>
          <label>
            Matching published resource
            <select
              value={selected}
              onChange={(e) => {
                setSelected(e.target.value);
                setAsset("");
              }}
            >
              <option value="">Choose a resource…</option>
              {found.data?.items.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.title}
                </option>
              ))}
            </select>
          </label>
          <label>
            Exact teaching file
            <select value={asset} onChange={(e) => setAsset(e.target.value)}>
              <option value="">Choose a file…</option>
              {resource.data?.assets.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.fileName} · {a.mimeType}
                </option>
              ))}
            </select>
          </label>
          <label>
            Evidence for this source match
            <textarea
              maxLength={2000}
              value={evidence}
              onChange={(e) => setEvidence(e.target.value)}
            />
          </label>
          {dependency && asset && evidence.trim().length >= 30 && (
            <Action
              confirm="Link this exact published asset version to the named source reference?"
              run={async () => {
                await api(`/api/content-imports/plans/${id}/resources`, {
                  method: "POST",
                  body: {
                    assetId: asset,
                    sourceDependency: dependency,
                    evidence,
                  },
                });
                await client.invalidateQueries({
                  queryKey: ["official-source-plan", id],
                });
                setDependency("");
                setAsset("");
                setEvidence("");
              }}
            >
              Link reviewed resource
            </Action>
          )}
        </>
      )}
      {found.error && <ErrorNotice error={found.error} />}{" "}
      {resource.error && <ErrorNotice error={resource.error} />}
    </section>
  );
}
