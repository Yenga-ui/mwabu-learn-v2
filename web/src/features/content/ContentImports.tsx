import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "../../api/client";
import { useSession } from "../../auth/Session";
import { Empty, ErrorNotice, PageTitle, QueryState } from "../../components/ui";
import DemoCleanup from "./DemoCleanup";
interface Row {
  id: string;
  sourcePath: string;
  format: string;
  fileSize: number;
  checksum: string;
  state: string;
  issue: string | null;
  contentItemId: string | null;
  plan: {
    title: string;
    grade: number;
    subject: string;
    topic: string;
    term: string | null;
    dependencies: string[];
    notices: string[];
  } | null;
}
interface Report {
  id: string;
  sourceCollection: string;
  curriculumYear: number;
  fileName: string;
  archiveChecksum: string;
  environment: string;
  confirmation: string;
  reviewEvidence: string;
  records: Row[];
}
export default function ContentImports() {
  const session = useSession();
  const [file, setFile] = useState<File>();
  const [year, setYear] = useState("2023");
  const [report, setReport] = useState<Report>();
  const [selected, setSelected] = useState<string[]>([]);
  const [evidence, setEvidence] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [reviewed, setReviewed] = useState(false);
  const [publish, setPublish] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const allowed = session.can("content.manage", "global");
  const batches = useQuery({
    queryKey: ["content-imports"],
    queryFn: () => api<Report[]>("/api/content-imports"),
    enabled: allowed,
    retry: false,
  });
  function show(value: Report) {
    setReport(value);
    setSelected([]);
    setConfirmation("");
    setReviewed(false);
    setEvidence(value.reviewEvidence);
    setPublish(false);
  }
  async function preview() {
    if (!file) return;
    setBusy(true);
    setError(undefined);
    try {
      const body = new FormData();
      body.append("file", file);
      body.append("curriculumYear", year);
      show(
        await api<Report>("/api/content-imports/preview", {
          method: "POST",
          body,
        }),
      );
      await batches.refetch();
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  async function apply() {
    if (!report) return;
    setBusy(true);
    setError(undefined);
    try {
      show(
        await api<Report>(`/api/content-imports/${report.id}/apply`, {
          method: "POST",
          body: {
            recordIds: selected,
            confirmation,
            reviewEvidence: evidence,
            publish,
          },
        }),
      );
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  if (!allowed)
    return (
      <Empty title="Content import requires catalogue management permission" />
    );
  return (
    <>
      <PageTitle
        eyebrow="REVIEWED CONTENT INTAKE"
        title="Import authorised Mwabu source plans"
      >
        Original files are preserved. Preview first, review curriculum and
        rights, then apply selected records. Existing resources are never
        overwritten.
      </PageTitle>
      <DemoCleanup />
      <section className="card">
        <h2>Preview a downloaded source archive</h2>
        <p>
          This service must be explicitly enabled in Development or Staging.
          Production imports are disabled.
        </p>
        <label>
          Product Team source collection
          <select value={year} onChange={(e) => setYear(e.target.value)}>
            <option value="2023">
              2023 SharePoint lesson plans — Grades 1, 2 and 4
            </option>
            <option value="2013">
              2013 SharePoint lesson plans — retain original attribution
            </option>
          </select>
        </label>
        <label>
          Authorised source ZIP
          <input
            type="file"
            accept=".zip"
            onChange={(e) => setFile(e.target.files?.[0])}
          />
        </label>
        <button
          className="button"
          disabled={!file || busy}
          onClick={() => void preview()}
        >
          {busy ? "Processing…" : "Preview source archive"}
        </button>
      </section>
      {error != null && <ErrorNotice error={error} />}
      {report && (
        <section className="card" aria-label="Import reconciliation">
          <h2>{report.fileName}</h2>
          <p>
            {report.sourceCollection} · {report.environment}
          </p>
          <p className="checksum">Archive SHA-256: {report.archiveChecksum}</p>
          <p>
            {report.records.length} inventoried files ·{" "}
            {report.records.filter((r) => r.state === "Imported").length}{" "}
            imported ·{" "}
            {report.records.filter((r) => r.state === "Duplicate").length}{" "}
            duplicates ·{" "}
            {
              report.records.filter(
                (r) => r.state === "NeedsReview" || r.state === "Failed",
              ).length
            }{" "}
            held/failed ·{" "}
            {report.records.filter((r) => r.state === "Excluded").length}{" "}
            excluded
          </p>
          <button
            className="button secondary"
            disabled={busy}
            onClick={() =>
              setSelected(
                report.records
                  .filter((r) => r.state === "Ready" || r.state === "Failed")
                  .slice(0, 200)
                  .map((r) => r.id),
              )
            }
          >
            Select validated records
          </button>
          <button
            className="button secondary"
            onClick={() => {
              const blob = new Blob([JSON.stringify(report, null, 2)], {
                type: "application/json",
              });
              const url = URL.createObjectURL(blob);
              const a = document.createElement("a");
              a.href = url;
              a.download = `mwabu-import-${report.id}.json`;
              a.click();
              URL.revokeObjectURL(url);
            }}
          >
            Download reconciliation report
          </button>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Select</th>
                  <th>Source / checksum</th>
                  <th>Curriculum context</th>
                  <th>Status / issue</th>
                </tr>
              </thead>
              <tbody>
                {report.records.map((r) => (
                  <tr key={r.id}>
                    <td>
                      <input
                        type="checkbox"
                        aria-label={`Select ${r.sourcePath}`}
                        disabled={
                          busy || !["Ready", "Failed"].includes(r.state)
                        }
                        checked={selected.includes(r.id)}
                        onChange={(e) =>
                          setSelected((v) =>
                            e.target.checked
                              ? [...v, r.id]
                              : v.filter((x) => x !== r.id),
                          )
                        }
                      />
                    </td>
                    <td>
                      {r.sourcePath}
                      <small className="checksum">{r.checksum}</small>
                      <small>
                        {r.fileSize} bytes · {r.format}
                      </small>
                    </td>
                    <td>
                      {r.plan ? (
                        <>
                          {r.plan.title}
                          <br />
                          Grade {r.plan.grade} · {r.plan.subject} ·{" "}
                          {r.plan.topic}
                          <br />
                          Term: {r.plan.term ?? "not specified"}
                          {r.plan.dependencies.length > 0 && (
                            <p>
                              Dependencies: {r.plan.dependencies.join("; ")}
                            </p>
                          )}
                        </>
                      ) : (
                        "Human classification required"
                      )}
                    </td>
                    <td>
                      {r.state}
                      {r.issue && <p>{r.issue}</p>}
                      {r.contentItemId && (
                        <Link to={`/app/content/${r.contentItemId}`}>
                          Inspect resource
                        </Link>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <label>
            Curriculum, language, accessibility, rights and privacy review
            evidence
            <textarea
              maxLength={2000}
              value={evidence}
              onChange={(e) => setEvidence(e.target.value)}
            />
          </label>
          <label>
            <input
              type="checkbox"
              checked={reviewed}
              onChange={(e) => setReviewed(e.target.checked)}
            />
            I reviewed these selected source records and their collection
            attribution. No unverified term or outcome will be inferred.
          </label>
          <label>
            <input
              type="checkbox"
              checked={publish}
              onChange={(e) => setPublish(e.target.checked)}
            />
            Publish eligible source plans through the normal resource workflow.
            Independent SCORM playback review remains separate.
          </label>
          <p>
            Type <strong>{report.confirmation}</strong> to apply only to this
            environment.
          </p>
          <label>
            Environment confirmation
            <input
              value={confirmation}
              onChange={(e) => setConfirmation(e.target.value)}
              autoComplete="off"
            />
          </label>
          <button
            className="button"
            disabled={
              busy ||
              !reviewed ||
              !selected.length ||
              evidence.trim().length < 30 ||
              confirmation !== report.confirmation ||
              !session.can("content.publish", "global") ||
              !session.can("curriculum.manage", "global")
            }
            onClick={() => void apply()}
          >
            {busy
              ? "Applying selected records…"
              : `Import ${selected.length} reviewed records`}
          </button>
        </section>
      )}
      <section className="card">
        <h2>Resume a previous import</h2>
        <QueryState query={batches}>
          {(items) => (
            <ul>
              {items.map((b) => (
                <li key={b.id}>
                  <button
                    className="button secondary"
                    onClick={() => {
                      setError(undefined);
                      void api<Report>(`/api/content-imports/${b.id}`)
                        .then(show)
                        .catch(setError);
                    }}
                  >
                    {b.curriculumYear}: {b.fileName}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </QueryState>
      </section>
    </>
  );
}
