import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api, ApiError } from "../../api/client";
import { useSession } from "../../auth/Session";
import { ErrorNotice } from "../../components/ui";
import type { Content, Resource } from "../../api/types";
import type {
  MinistryLessonForm,
  SourcePlanContext,
  TeachingResource,
} from "../lessons/types";
import { MediaViewer } from "./Resource";

export interface OfficialTemplate {
  resource: Content;
  context: SourcePlanContext;
  form: MinistryLessonForm;
  resources: TeachingResource[];
  missingDependencies: string[];
}
export default function OfficialSourcePlan({ id }: { id: string }) {
  const session = useSession();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const [selected, setSelected] = useState<TeachingResource>();
  const query = useQuery({
    queryKey: ["official-source-plan", id],
    queryFn: () => api<OfficialTemplate>(`/api/learning/library/plans/${id}`),
    retry: false,
  });
  const linked = useQuery({
    queryKey: ["source-plan-media", selected?.contentItemId],
    queryFn: () =>
      api<Resource>(`/api/learning/content/${selected!.contentItemId}`),
    enabled: !!selected,
    retry: false,
  });
  if (query.error instanceof ApiError && query.error.status === 404)
    return null;
  if (query.error) return <ErrorNotice error={query.error} />;
  if (!query.data) return <p role="status">Checking Mwabu lesson plan…</p>;
  const plan = query.data;
  const asset = linked.data?.assets.find(
    (a) => a.id === selected?.assetId && a.checksum === selected?.checksum,
  );
  async function copyPlan() {
    setBusy(true);
    setError(undefined);
    try {
      const result = await api<{ lesson: { id: string } }>(
        `/api/organisations/${session.organisationId}/lessons/from-library/${id}`,
        { method: "POST" },
      );
      navigate(`/app/lessons/${result.lesson.id}`);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="card" aria-label="Mwabu source lesson plan">
      <h2>Mwabu lesson plan</h2>
      <p>
        {plan.context.curriculumYear} curriculum · Grade {plan.context.grade} ·{" "}
        {plan.context.subject} · {plan.context.topic}
      </p>
      <p>{plan.context.notice}</p>
      {session.can("lessons.manage") && session.organisationId && (
        <button
          className="button"
          disabled={busy}
          onClick={() => void copyPlan()}
        >
          {busy ? "Creating your copy…" : "Use this lesson"}
        </button>
      )}
      {error != null && <ErrorNotice error={error} />}
      <h3>Learning resources for this plan</h3>
      {plan.resources.map((r) => (
        <div className="action-row" key={r.assetId}>
          <button className="button secondary" onClick={() => setSelected(r)}>
            Play {r.title}
          </button>
          <Link
            to={`/app/resources/${r.contentItemId}?return=${encodeURIComponent("/app/resources/" + id)}`}
          >
            Open resource page
          </Link>
        </div>
      ))}
      {plan.missingDependencies.length > 0 && (
        <p role="status">
          Referenced resources awaiting verified links:{" "}
          {plan.missingDependencies.join("; ")}. Original plan remains
          available.
        </p>
      )}
      {linked.error && <ErrorNotice error={linked.error} />}
      {selected && asset && (
        <div>
          <button
            className="button secondary"
            onClick={() => setSelected(undefined)}
          >
            Return to lesson plan
          </button>
          <MediaViewer key={asset.id} asset={asset} />
        </div>
      )}
      <details open>
        <summary>Read original plan, including diagrams</summary>
        <iframe
          className="document-viewer"
          title="Original Mwabu lesson plan"
          src={`/api/learning/library/plans/${id}/source-preview`}
          sandbox=""
          referrerPolicy="no-referrer"
        />
      </details>
      <details>
        <summary>Official template fields from this source</summary>
        <dl>
          <dt>Subject</dt>
          <dd>{plan.form.subject}</dd>
          <dt>Topic</dt>
          <dd>{plan.form.topic}</dd>
          <dt>Specific competences</dt>
          <dd>{plan.form.specificCompetence}</dd>
          <dt>Teaching and learning materials/resources</dt>
          <dd className="preserve-lines">{plan.form.materials}</dd>
        </dl>
        {plan.form.progression.map((s) => (
          <section key={s.code}>
            <h3>{s.code.replaceAll("-", " ")}</h3>
            <p className="preserve-lines">
              {s.teacherActivities || "Not supplied in source"}
            </p>
          </section>
        ))}
      </details>
    </section>
  );
}
