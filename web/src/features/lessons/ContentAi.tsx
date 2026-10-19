import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../api/client";
import type { Content, Page } from "../../api/types";
import { ErrorNotice, QueryState } from "../../components/ui";
import { ActivityPlayer } from "../content/activities/ActivityPlayer";
import type { ActivityDefinition } from "../content/activities/activityRuntime";
type Job = { id: string; state: string; errorCode?: string };
type Proposal = {
  activity?: ActivityDefinition;
  storyboard?: {
    title: string;
    steps: {
      caption: string;
      visualDescription: string;
      durationSeconds: number;
    }[];
  };
  provider: string;
  model: string;
  revision: number;
};
export default function ContentAi({
  org,
  plan,
  save,
}: {
  org: string;
  plan: string;
  save: () => Promise<number>;
}) {
  const base = `/api/organisations/${org}/lessons/${plan}/content-jobs`;
  const [kind, setKind] = useState("quiz");
  const [instructions, setInstructions] = useState("");
  const [consent, setConsent] = useState(false);
  const [selected, setSelected] = useState("");
  const [target, setTarget] = useState("");
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const client = useQueryClient();
  const jobs = useQuery({
    queryKey: ["content-jobs", org, plan],
    queryFn: ({ signal }) => api<Job[]>(base, { signal }),
    refetchInterval: (q) =>
      q.state.data?.some((x) => ["queued", "processing"].includes(x.state))
        ? 1500
        : false,
  });
  const proposal = useQuery({
    queryKey: ["content-proposal", org, plan, selected],
    queryFn: ({ signal }) => api<Proposal>(base + `/${selected}`, { signal }),
    enabled: !!selected,
  });
  const drafts = useQuery({
    queryKey: ["content-drafts-for-ai"],
    queryFn: ({ signal }) =>
      api<Page<Content>>("/api/content?status=Draft&pageSize=100", { signal }),
    enabled: !!proposal.data?.activity,
  });
  const run = (action: () => Promise<unknown>) => {
    setBusy(true);
    setError(undefined);
    void action()
      .catch(setError)
      .finally(() => setBusy(false));
  };
  return (
    <details className="card">
      <summary>Generate interactive content from this lesson</summary>
      <p>
        Requires a selected curriculum outcome, organisation AI approval and
        global Content Studio access. Review every proposal. Saving adds a draft
        activity; the independent approval workflow still applies.
      </p>
      {!!error && <ErrorNotice error={error} />}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          run(async () => {
            const revision = await save();
            await api(base, {
              method: "POST",
              body: {
                expectedRevision: revision,
                kind,
                instructions,
                confirmNoPersonalData: consent,
              },
            });
            await client.invalidateQueries({
              queryKey: ["content-jobs", org, plan],
            });
          });
        }}
      >
        <fieldset disabled={busy}>
          <legend>Content proposal request</legend>
          <label>
            Generate format
            <select value={kind} onChange={(e) => setKind(e.target.value)}>
              {[
                "quiz",
                "matching",
                "sequencing",
                "fill-blank",
                "number-line",
                "fraction",
                "place-value-exercise",
                "storyboard",
                "visual-explanation",
                "worked-example",
                "discussion-prompts",
                "activity-suggestions",
                "learner-explanation",
              ].map((x) => (
                <option key={x} value={x}>
                  {x.replaceAll("-", " ")}
                </option>
              ))}
            </select>
          </label>
          <label>
            Content instructions
            <textarea
              maxLength={2000}
              value={instructions}
              onChange={(e) => setInstructions(e.target.value)}
            />
          </label>
          <label>
            <input
              type="checkbox"
              checked={consent}
              onChange={(e) => setConsent(e.target.checked)}
              required
            />
            I confirm these instructions contain no personal data.
          </label>
          <button className="button" disabled={!consent}>
            Queue content proposal
          </button>
        </fieldset>
      </form>
      <QueryState query={jobs}>
        {(rows) => (
          <ul>
            {rows.map((job) => (
              <li key={job.id}>
                {job.state}
                {job.errorCode && ` · ${job.errorCode}`}{" "}
                {job.state === "completed" && (
                  <button
                    className="button secondary"
                    onClick={() => {
                      setSelected(job.id);
                      setSaved(false);
                    }}
                  >
                    Review content proposal
                  </button>
                )}
                {["queued", "processing"].includes(job.state) && (
                  <button
                    className="button secondary"
                    disabled={busy}
                    onClick={() =>
                      run(async () => {
                        await api(base + `/${job.id}/cancel`, {
                          method: "POST",
                        });
                        await client.invalidateQueries({
                          queryKey: ["content-jobs", org, plan],
                        });
                      })
                    }
                  >
                    Cancel content generation
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </QueryState>
      {selected && (
        <QueryState query={proposal}>
          {(value) => (
            <section>
              <h3>Review generated content</h3>
              <p>
                Provider: {value.provider} · Model: {value.model} · Saved lesson
                revision {value.revision}
              </p>
              {value.activity && (
                <>
                  <ActivityPlayer definition={value.activity} />
                  <QueryState query={drafts}>
                    {(rows) => (
                      <label>
                        Save into a draft resource
                        <select
                          value={target}
                          onChange={(e) => {
                            setTarget(e.target.value);
                            setSaved(false);
                          }}
                        >
                          <option value="">Choose an existing draft…</option>
                          {rows.items.map((x) => (
                            <option value={x.id} key={x.id}>
                              {x.title}
                            </option>
                          ))}
                        </select>
                      </label>
                    )}
                  </QueryState>
                  <button
                    className="button"
                    disabled={!target || busy || saved}
                    onClick={() =>
                      run(async () => {
                        await api(base + `/${selected}/draft/${target}`, {
                          method: "POST",
                        });
                        setSaved(true);
                        await client.invalidateQueries({
                          queryKey: ["studio-assets", target],
                        });
                      })
                    }
                  >
                    {saved
                      ? "Draft activity saved for review"
                      : "Accept into draft resource"}
                  </button>
                  {saved && (
                    <Link to={`/app/content/${target}`}>
                      Open resource review workflow
                    </Link>
                  )}
                </>
              )}
              {value.storyboard && (
                <>
                  <h4>{value.storyboard.title}</h4>
                  <ol>
                    {value.storyboard.steps.map((step, i) => (
                      <li key={i}>
                        <strong>{step.caption}</strong>
                        <p>{step.visualDescription}</p>
                        <p>{step.durationSeconds} seconds</p>
                      </li>
                    ))}
                  </ol>
                  <p>
                    This is a draft text teaching outline for review. It
                    contains no executable animation code and is not published
                    automatically.
                  </p>
                </>
              )}
            </section>
          )}
        </QueryState>
      )}
    </details>
  );
}
