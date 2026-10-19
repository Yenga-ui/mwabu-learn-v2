import { useEffect, useRef, useState } from "react";
import { api } from "../../api/client";
import { ErrorNotice } from "../../components/ui";
import {
  clearPackageStorage,
  downloadPackageAsset,
  lessonPackageHtml,
  packageStorage,
  type LessonPackage,
} from "./lessonPackage";
export default function LessonPackageDownload({
  path,
  revision,
}: {
  path: string;
  revision: number;
}) {
  const [busy, setBusy] = useState(false),
    [error, setError] = useState<unknown>(),
    [progress, setProgress] = useState(""),
    [consent, setConsent] = useState(false),
    [size, setSize] = useState(0);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    void packageStorage()
      .then(setSize)
      .catch(() => {});
    return () => controller.current?.abort();
  }, []);
  async function download() {
    setBusy(true);
    setError(undefined);
    const request = new AbortController();
    controller.current = request;
    try {
      const manifest = await api<LessonPackage>(
        path + `/package?revision=${revision}`,
        { signal: request.signal },
      );
      const files: Uint8Array[] = [];
      for (const [i, asset] of manifest.assets.entries()) {
        setProgress(`Resource ${i + 1} of ${manifest.assets.length}`);
        files.push(
          await downloadPackageAsset(asset, request.signal, (bytes) =>
            setProgress(
              `Resource ${i + 1} of ${manifest.assets.length}: ${Math.round((bytes / asset.size) * 100)}%`,
            ),
          ),
        );
      }
      const current = await api<LessonPackage>(
        path + `/package?revision=${revision}`,
        { signal: request.signal },
      );
      if (current.version !== manifest.version)
        throw new Error("The package changed. Recheck and retry.");
      const html = await lessonPackageHtml(manifest, files);
      request.signal.throwIfAborted();
      const url = URL.createObjectURL(new Blob([html], { type: "text/html" }));
      const link = document.createElement("a");
      link.href = url;
      link.download = `mwabu-lesson-${revision}-${manifest.version.slice(0, 12)}.html`;
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 30000);
      setProgress(
        "Verified package exported. Check for updates when reconnecting.",
      );
    } catch (value) {
      if (!request.signal.aborted) setError(value);
      else
        setProgress(
          "Download paused. Retry to resume completed chunks after access is rechecked.",
        );
    } finally {
      setBusy(false);
      void packageStorage()
        .then(setSize)
        .catch(() => {});
    }
  }
  return (
    <details className="card">
      <summary>Complete offline lesson package</summary>
      <p>
        Includes the approved lesson and every attached supported downloadable
        resource, up to 25 MiB. Resumable temporary media storage is limited to
        50 MiB. Exported files cannot receive revocations; recheck on
        reconnection and remove obsolete copies.
      </p>
      <label>
        <input
          type="checkbox"
          checked={consent}
          disabled={busy}
          onChange={(e) => setConsent(e.target.checked)}
        />
        I checked this teaching content contains no personal data and will store
        the offline copy securely.
      </label>
      <button
        className="button"
        disabled={busy || !consent}
        onClick={() => void download()}
      >
        Download complete lesson package
      </button>
      {busy && (
        <button
          className="button secondary"
          onClick={() => controller.current?.abort()}
        >
          Pause package download
        </button>
      )}
      <button
        className="button secondary"
        disabled={busy}
        onClick={() => {
          void clearPackageStorage()
            .then(() => setSize(0))
            .catch(setError);
        }}
      >
        Clear temporary downloaded media ({(size / 1024 / 1024).toFixed(1)} MiB)
      </button>
      <p role="status">{progress}</p>
      {!!error && <ErrorNotice error={error} />}
    </details>
  );
}
