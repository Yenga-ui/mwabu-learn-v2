import { useRef, useState } from "react";
import { api } from "../../api/client";
import type { Asset } from "../../api/types";
import { ErrorNotice } from "../../components/ui";

export function isScormCandidate(asset: Asset) {
  return (
    asset.assetType === "Package" &&
    ["application/zip", "application/x-zip-compressed"].includes(asset.mimeType)
  );
}

export default function ScormPlayer({ asset }: { asset: Asset }) {
  const [launch, setLaunch] = useState<{ launchUrl: string; title: string }>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const container = useRef<HTMLDivElement>(null);
  async function start() {
    setBusy(true);
    setError(undefined);
    setLaunch(undefined);
    try {
      const path =
        /^\/api\/learning\/content\/([a-f0-9-]{36})\/assets\/([a-f0-9-]{36})\/view$/i.exec(
          asset.viewUrl,
        );
      if (!path)
        throw new Error("This package has no supported playback address.");
      const result = await api<{ launchUrl: string; title: string }>(
        `/api/content/${path[1]}/scorm/${path[2]}/launch`,
        { method: "POST" },
      );
      if (
        !/^\/api\/scorm\/runtime\/[a-zA-Z0-9_-]{43}\//.test(result.launchUrl) ||
        result.launchUrl.includes("\\")
      )
        throw new Error("Invalid playback address.");
      setLaunch(result);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="scorm-player" ref={container}>
      <h2>Interactive lesson</h2>
      <p>
        Anonymous practice. Answers stay in this page and are cleared when it
        closes. No learner progress is saved or sent.
      </p>
      <p className="muted">
        Some lessons unlock Next after narration finishes. Use the lesson’s play
        or replay control if needed.
      </p>
      <div className="action-row">
        <button className="button" disabled={busy} onClick={() => void start()}>
          {busy
            ? "Opening lesson…"
            : launch
              ? "Restart practice"
              : "Start interactive lesson"}
        </button>
        {launch && (
          <>
            <button
              className="button secondary"
              onClick={() => {
                setLaunch(undefined);
                setError(undefined);
              }}
            >
              Close practice
            </button>
            <button
              className="button secondary"
              onClick={() =>
                void container.current?.requestFullscreen().catch(setError)
              }
            >
              Full screen
            </button>
          </>
        )}
      </div>
      {error != null && (
        <ErrorNotice error={error} retry={() => void start()} />
      )}
      {launch && (
        <>
          <p className="muted">
            If the session expires after 20 minutes, restart practice.
            Restarting clears answers.
          </p>
          <iframe
            key={launch.launchUrl}
            src={launch.launchUrl}
            title={launch.title}
            sandbox="allow-scripts"
            allow="autoplay"
            referrerPolicy="no-referrer"
            className="scorm-frame"
          />
        </>
      )}
    </div>
  );
}
