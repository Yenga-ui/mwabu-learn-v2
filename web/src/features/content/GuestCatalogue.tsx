import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "../../api/client";
import { Brand } from "../../components/Brand";
import { QueryState } from "../../components/ui";
import { ActivityPlayer } from "./activities/ActivityPlayer";
import type { ActivityDefinition } from "./activities/activityRuntime";

type Sample = { id: string; title: string; summary?: string; canUse: boolean };
type Asset = { id: string; fileName: string; mimeType: string };
export default function GuestCatalogue() {
  const [params, setParams] = useSearchParams();
  const audience = params.get("audience") === "learner" ? "learner" : "teacher";
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string>();
  const list = useQuery({
    queryKey: ["guest-samples", audience, page],
    queryFn: ({ signal }) =>
      api<{ items: Sample[]; totalCount: number }>(
        `/api/guest/content?audience=${audience}&page=${page}`,
        { signal },
      ),
  });
  const detail = useQuery({
    queryKey: ["guest-sample", audience, selected],
    queryFn: ({ signal }) =>
      api<Sample & { assets: Asset[] }>(
        `/api/guest/content/${selected}?audience=${audience}`,
        { signal },
      ),
    enabled: !!selected,
  });
  return (
    <main className="guest-catalogue">
      <header className="actions">
        <Brand />
        <Link className="button secondary" to="/login">
          Sign in
        </Link>
        <Link className="button" to="/register">
          Create an adult account
        </Link>
      </header>
      <h1>Explore Mwabu Learn</h1>
      <p>
        Learner accounts are registered by a parent, teacher or Mwabu. Guest
        practice does not create an account.
      </p>
      <p>
        Try selected resources as a guest. Practice answers stay on this page
        and are not saved as learner results.
      </p>
      <div className="actions" aria-label="Guest experience">
        {["teacher", "learner"].map((role) => (
          <button
            className="button secondary"
            key={role}
            aria-pressed={audience === role}
            onClick={() => {
              setParams({ audience: role });
              setSelected(undefined);
              setPage(1);
            }}
          >
            Guest {role}
          </button>
        ))}
      </div>
      {selected ? (
        <>
          <button
            className="button secondary"
            onClick={() => setSelected(undefined)}
          >
            Back to samples
          </button>
          <QueryState query={detail}>
            {(item) => (
              <section className="panel">
                <h2>{item.title}</h2>
                <p>{item.summary}</p>
                {!item.canUse && (
                  <p>
                    Preview only. A licensed account is needed to use this
                    resource.
                  </p>
                )}
                {item.assets.map((asset) => (
                  <GuestAsset
                    key={asset.id}
                    asset={asset}
                    resource={item.id}
                    audience={audience}
                  />
                ))}
                {item.canUse && !item.assets.length && (
                  <p>
                    No supported guest playback is available for this resource
                    yet.
                  </p>
                )}
              </section>
            )}
          </QueryState>
        </>
      ) : (
        <QueryState query={list}>
          {(data) => (
            <>
              <div className="lesson-cards">
                {data.items.map((item) => (
                  <article className="panel lesson-card" key={item.id}>
                    <h2>{item.title}</h2>
                    <p>{item.summary}</p>
                    <button
                      className="button"
                      onClick={() => setSelected(item.id)}
                    >
                      {item.canUse ? "Open sample" : "Preview resource"}
                    </button>
                  </article>
                ))}
              </div>
              {!data.items.length && (
                <p>No guest samples have been enabled for this audience yet.</p>
              )}
              <div className="actions">
                <button disabled={page === 1} onClick={() => setPage(page - 1)}>
                  Previous
                </button>
                <span>Page {page}</span>
                <button
                  disabled={page * 24 >= data.totalCount}
                  onClick={() => setPage(page + 1)}
                >
                  Next
                </button>
              </div>
            </>
          )}
        </QueryState>
      )}
    </main>
  );
}
function GuestAsset({
  asset,
  resource,
  audience,
}: {
  asset: Asset;
  resource: string;
  audience: string;
}) {
  const url = `/api/guest/content/${resource}/assets/${asset.id}?audience=${audience}`;
  const activity = asset.mimeType === "application/vnd.mwabu.activity+json";
  const query = useQuery({
    queryKey: ["guest-activity", resource, asset.id, audience],
    queryFn: ({ signal }) =>
      api<{ definition: ActivityDefinition }>(url, { signal }),
    enabled: activity,
  });
  if (activity)
    return (
      <QueryState query={query}>
        {(data) => <ActivityPlayer definition={data.definition} />}
      </QueryState>
    );
  if (asset.mimeType.startsWith("image/"))
    return <img className="guest-media" src={url} alt={asset.fileName} />;
  if (asset.mimeType.startsWith("audio/"))
    return <audio controls src={url} aria-label={asset.fileName} />;
  if (asset.mimeType.startsWith("video/"))
    return (
      <video
        className="guest-media"
        controls
        src={url}
        aria-label={asset.fileName}
      />
    );
  return (
    <p>
      <a href={url} target="_blank" rel="noopener noreferrer">
        Open {asset.fileName}
      </a>
    </p>
  );
}
