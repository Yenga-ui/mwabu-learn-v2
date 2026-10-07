import { useQuery } from "@tanstack/react-query";
import { api } from "../../api/client";
import type { Report } from "../../api/types";
import { useSession } from "../../auth/Session";
import { Empty, PageTitle, QueryState } from "../../components/ui";
export default function Reports() {
  const session = useSession();
  const org = session.organisationId;
  const platform = session.can("reports.read", "platform");
  const allowed = platform || (!!org && session.can("reports.read"));
  const query = useQuery({
    queryKey: ["report", platform, org],
    queryFn: () =>
      api<Report>(
        platform
          ? "/api/reports/platform"
          : `/api/reports/organisations/${org}`,
      ),
    enabled: allowed,
  });
  if (!allowed)
    return <Empty title="Reporting is not available in this context" />;
  return (
    <>
      <PageTitle
        eyebrow="REAL DATA. USEFUL PERSPECTIVE."
        title="Operational reporting"
      >
        {platform
          ? "Platform-wide indicators."
          : "Organisation indicators. Catalogue totals refer to the shared published catalogue."}
      </PageTitle>
      <QueryState query={query}>
        {(data) => (
          <>
            <p className="muted">
              Generated {new Date(data.generatedAt).toLocaleString()}. Counts
              are operational snapshots, not learner achievement or teaching
              progress.
            </p>
            <div className="stat-grid">
              {[
                ["People", data.users],
                ["Active memberships", data.activeMemberships],
                ["Organisations", data.organisations],
                ["Schools", data.schools],
                ["Projects", data.projects],
                ["Active curricula", data.curricula],
                ["Catalogue resources", data.content],
                ["Mapped resources", data.mappedContent],
                ["Active devices", data.activeDevices],
                ["Revoked devices", data.revokedDevices],
                ["Saved sync checkpoints", data.syncCheckpoints],
              ].map(([label, value]) => (
                <div className="stat card" key={label}>
                  <span>{label}</span>
                  <strong>{value}</strong>
                </div>
              ))}
            </div>
            <div className="two-column">
              <section className="card">
                <h2>Publication states</h2>
                <table>
                  <thead>
                    <tr>
                      <th>Status</th>
                      <th>Resources</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.publicationStates.map((x) => (
                      <tr key={x.code}>
                        <td>{x.code}</td>
                        <td>{x.count}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
              <section className="card">
                <h2>Resource types</h2>
                {data.contentTypes.length ? (
                  <table>
                    <thead>
                      <tr>
                        <th>Type</th>
                        <th>Resources</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.contentTypes.map((x) => (
                        <tr key={x.code}>
                          <td>{x.code}</td>
                          <td>{x.count}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : (
                  <p>No catalogue resources yet.</p>
                )}
              </section>
            </div>
          </>
        )}
      </QueryState>
    </>
  );
}
