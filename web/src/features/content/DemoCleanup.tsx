import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "../../api/client";
import { Action, QueryState } from "../../components/ui";
interface Report {
  environment: string;
  isolatedDatabaseConfirmed: boolean;
  snapshot: string;
  confirmation: string;
  notice: string;
  candidates: {
    id: string;
    kind: string;
    name: string;
    proof: string;
    relationships: number;
    safeToArchive: boolean;
  }[];
}
export default function DemoCleanup() {
  const [confirmation, setConfirmation] = useState("");
  const query = useQuery({
    queryKey: ["demo-cleanup-report"],
    queryFn: () => api<Report>("/api/content-imports/demo-cleanup"),
    retry: false,
  });
  return (
    <section className="card">
      <h2>Demonstration-data cleanup — dry run</h2>
      <QueryState query={query}>
        {(report) => (
          <>
            <p>{report.notice}</p>
            <p>
              {report.candidates.length} positively identified or seed-like
              candidates. Environment: {report.environment}.{" "}
              {report.isolatedDatabaseConfirmed
                ? "Explicit isolated demo database configuration confirmed."
                : "This database is report-only."}
            </p>
            <ul>
              {report.candidates.map((c) => (
                <li key={c.id}>
                  <strong>
                    {c.kind}: {c.name}
                  </strong>
                  <p>
                    {c.proof} · {c.relationships} relationships ·{" "}
                    {c.safeToArchive
                      ? "Eligible for archival"
                      : "Not authorised for cleanup"}
                  </p>
                </li>
              ))}
            </ul>
            {report.isolatedDatabaseConfirmed &&
              report.candidates.some((c) => c.safeToArchive) && (
                <>
                  <p>
                    Type {report.confirmation}. This archives verified seed
                    resources and deactivates the verified demo framework;
                    files, accounts and history are retained.
                  </p>
                  <label>
                    Demo archival confirmation
                    <input
                      value={confirmation}
                      onChange={(e) => setConfirmation(e.target.value)}
                    />
                  </label>
                  {confirmation === report.confirmation && (
                    <Action
                      confirm="Archive only the verified candidates in this isolated demo database?"
                      run={async () => {
                        await api("/api/content-imports/demo-cleanup", {
                          method: "POST",
                          body: {
                            ids: report.candidates
                              .filter((c) => c.safeToArchive)
                              .map((c) => c.id),
                            snapshot: report.snapshot,
                            confirmation,
                          },
                        });
                        setConfirmation("");
                        await query.refetch();
                      }}
                    >
                      Archive verified demo records
                    </Action>
                  )}
                </>
              )}
          </>
        )}
      </QueryState>
    </section>
  );
}
