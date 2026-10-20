import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../api/client";
import type { Asset } from "../../api/types";
import { useSession } from "../../auth/Session";
import { Action, QueryState } from "../../components/ui";
import { isScormCandidate } from "./ScormPlayer";

interface Inspection {
  title: string;
  version: string;
  launchPath: string;
  files: number;
  expandedBytes: number;
  checksum: string;
  approved: boolean;
}
export default function ScormReview({ id }: { id: string }) {
  const assets = useQuery({
    queryKey: ["studio-assets", id],
    queryFn: () => api<Asset[]>(`/api/content/${id}/assets`),
  });
  return (
    <>
      {assets.data?.filter(isScormCandidate).map((asset) => (
        <Review key={asset.id} id={id} asset={asset} />
      ))}
    </>
  );
}
function Review({ id, asset }: { id: string; asset: Asset }) {
  const [inspect, setInspect] = useState(false);
  const [evidence, setEvidence] = useState("");
  const [reviewed, setReviewed] = useState(false);
  const session = useSession();
  const client = useQueryClient();
  const path = `/api/content/${id}/scorm/${asset.id}`;
  const query = useQuery({
    queryKey: ["scorm-review", id, asset.id],
    queryFn: () => api<Inspection>(path),
    enabled: inspect,
  });
  return (
    <section className="card">
      <h2>SCORM playback review</h2>
      <p>{asset.fileName}</p>
      <p>
        Publishing a ZIP does not approve its scripts for playback. A different
        authorised publisher must review this exact version. Playback blocks
        external connections, browser storage and identifiable learner tracking.
      </p>
      <button className="button secondary" onClick={() => setInspect(true)}>
        Inspect SCORM package
      </button>
      {inspect && (
        <QueryState query={query}>
          {(inspection) => (
            <>
              <p>
                {inspection.title} · SCORM {inspection.version} ·{" "}
                {inspection.files} files
              </p>
              <p className="checksum">SHA-256: {inspection.checksum}</p>
              <p role="status">
                {inspection.approved
                  ? "Approved for isolated anonymous playback"
                  : "Playback approval required"}
              </p>
              {session.can("content.publish", "global") &&
                (inspection.approved ? (
                  <Action
                    confirm="Revoke playback approval? Active sessions stop receiving files."
                    run={async () => {
                      await api(path + "/approval", { method: "DELETE" });
                      await client.invalidateQueries({
                        queryKey: ["scorm-review", id],
                      });
                    }}
                  >
                    Revoke playback approval
                  </Action>
                ) : (
                  <>
                    <label>
                      Review evidence
                      <textarea
                        maxLength={2000}
                        value={evidence}
                        onChange={(e) => setEvidence(e.target.value)}
                        placeholder="Source, curriculum accuracy, language, accessibility, rights and privacy review…"
                      />
                    </label>
                    <label>
                      <input
                        type="checkbox"
                        checked={reviewed}
                        onChange={(e) => setReviewed(e.target.checked)}
                      />
                      I reviewed this version and approve anonymous practice
                      only.
                    </label>
                    {reviewed && evidence.trim().length >= 20 && (
                      <Action
                        confirm="Approve these exact package bytes for isolated anonymous playback?"
                        run={async () => {
                          await api(path + "/approval", {
                            method: "POST",
                            body: { checksum: inspection.checksum, evidence },
                          });
                          await client.invalidateQueries({
                            queryKey: ["scorm-review", id],
                          });
                        }}
                      >
                        Approve SCORM playback
                      </Action>
                    )}
                  </>
                ))}
            </>
          )}
        </QueryState>
      )}
    </section>
  );
}
