import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, ensureSession } from "../../../api/client";
import { ErrorNotice, QueryState } from "../../../components/ui";
import { mountActivity, type ActivityDefinition } from "./activityRuntime";
import { offlineActivityHtml } from "./offlineActivity";
import { WholeClassActivity } from "./WholeClassActivity";

export function ActivityPlayer({
  definition,
  wholeClass = false,
}: {
  definition: ActivityDefinition;
  wholeClass?: boolean;
}) {
  const host = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (host.current) {
      const cleanup = mountActivity(host.current, definition, wholeClass);
      if (wholeClass)
        host.current.querySelectorAll("form").forEach((form) => {
          form.onsubmit = (event) => event.preventDefault();
        });
      return cleanup;
    }
  }, [definition, wholeClass]);
  return (
    <div
      ref={host}
      className="activity-player"
      aria-label="Interactive learning activity"
    />
  );
}
export function PublishedActivity({
  resourceId,
  assetId,
  downloadUrl,
  downloadable,
  wholeClass = false,
}: {
  resourceId: string;
  assetId: string;
  downloadUrl: string;
  downloadable: boolean;
  wholeClass?: boolean;
}) {
  const query = useQuery({
    queryKey: ["published-activity", resourceId, assetId],
    queryFn: ({ signal }) =>
      api<{
        definition: ActivityDefinition;
        checksum: string;
        assetId: string;
      }>(`/api/learning/content/${resourceId}/activities/${assetId}`, {
        signal,
      }),
  });
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  return (
    <QueryState query={query}>
      {(data) => (
        <section>
          {wholeClass ? (
            <WholeClassActivity key={assetId} definition={data.definition} />
          ) : (
            <ActivityPlayer key={assetId} definition={data.definition} />
          )}
          {!!error && <ErrorNotice error={error} />}
          {downloadable && (
            <button
              className="button secondary"
              disabled={busy}
              onClick={() =>
                void (async () => {
                  setBusy(true);
                  setError(undefined);
                  try {
                    if (!(await ensureSession()))
                      throw new Error(
                        "Sign in again before downloading this resource.",
                      );
                    const response = await fetch(downloadUrl, {
                      credentials: "same-origin",
                      cache: "no-store",
                    });
                    if (!response.ok)
                      throw new Error(
                        "Download is unavailable. Check access and try again.",
                      );
                    const bytes = await response.arrayBuffer();
                    if (bytes.byteLength > 100000)
                      throw new Error("Activity exceeds its size limit.");
                    const digest = Array.from(
                      new Uint8Array(
                        await crypto.subtle.digest("SHA-256", bytes),
                      ),
                    )
                      .map((x) => x.toString(16).padStart(2, "0"))
                      .join("");
                    if (digest.toLowerCase() !== data.checksum.toLowerCase())
                      throw new Error(
                        "Integrity check failed. Download the current version again.",
                      );
                    const html = await offlineActivityHtml(
                      data.definition,
                      `${assetId} / SHA-256 ${data.checksum}`,
                    );
                    const url = URL.createObjectURL(
                      new Blob([html], { type: "text/html;charset=utf-8" }),
                    );
                    const link = document.createElement("a");
                    link.href = url;
                    link.download = "mwabu-activity-offline.html";
                    link.click();
                    setTimeout(() => URL.revokeObjectURL(url), 10000);
                  } catch (failure) {
                    setError(failure);
                  } finally {
                    setBusy(false);
                  }
                })()
              }
            >
              {busy
                ? "Verifying download…"
                : "Download activity for offline use"}
            </button>
          )}
        </section>
      )}
    </QueryState>
  );
}
