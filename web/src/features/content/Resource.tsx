import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Download, RotateCcw } from "lucide-react";
import { api, ensureSession, queryString } from "../../api/client";
import type { Resource as ResourceType, Asset } from "../../api/types";
import { useSession } from "../../auth/Session";
import {
  Badge,
  Empty,
  ErrorNotice,
  PageTitle,
  QueryState,
} from "../../components/ui";
export function MediaViewer({ asset }: { asset: Asset }) {
  const [failed, setFailed] = useState(false);
  const [version, setVersion] = useState(0);
  const src = asset.viewUrl + "?v=" + version;
  async function retry() {
    if (await ensureSession()) {
      setFailed(false);
      setVersion((x) => x + 1);
    }
  }
  if (failed)
    return (
      <ErrorNotice
        error={
          new Error("Unable to open this file. Your session may have expired.")
        }
        retry={() => void retry()}
      />
    );
  if (asset.mimeType.startsWith("audio/"))
    return (
      <div className="audio-viewer">
        <p className="eyebrow">LISTEN & LEARN</p>
        <h3>{asset.fileName}</h3>
        <audio
          key={src}
          controls
          preload="none"
          src={src}
          onError={() => setFailed(true)}
          aria-label={asset.fileName}
        />
        <button
          className="button secondary"
          onClick={(e) => {
            const audio = e.currentTarget.parentElement?.querySelector("audio");
            if (audio) {
              audio.pause();
              audio.currentTime = 0;
            }
          }}
        >
          <RotateCcw size={16} />
          Restart audio
        </button>
        <small>Audio starts only when you press play.</small>
      </div>
    );
  if (asset.mimeType.startsWith("video/"))
    return (
      <video
        key={src}
        controls
        preload="metadata"
        src={src}
        onError={() => setFailed(true)}
        aria-label={asset.fileName}
      />
    );
  if (
    ["image/png", "image/jpeg", "image/gif", "image/webp"].includes(
      asset.mimeType,
    )
  )
    return (
      <img
        className="resource-image"
        loading="lazy"
        src={src}
        alt={asset.fileName}
        onError={() => setFailed(true)}
      />
    );
  if (asset.mimeType === "application/pdf")
    return (
      <iframe
        className="document-viewer"
        src={src}
        title={asset.fileName}
        sandbox="allow-same-origin"
      />
    );
  return (
    <Empty title="Download this resource to open it">
      This file type requires a compatible application.
    </Empty>
  );
}
export default function Resource() {
  const { id } = useParams();
  const session = useSession();
  const [selected, setSelected] = useState<string>();
  const query = useQuery({
    queryKey: ["resource", id],
    queryFn: ({ signal }) =>
      api<ResourceType>("/api/learning/content/" + id, { signal }),
    enabled: session.can("content.read", "catalogue"),
  });
  const resourceId = query.data?.content.id;
  const organisationId = session.organisationId;
  const recordVisit = session.can("content.read");
  useEffect(() => {
    if (resourceId && organisationId && recordVisit)
      void api(`/api/organisations/${organisationId}/workspace/recent/${id}`, {
        method: "POST",
      }).catch(() => {
        /* Viewing succeeds independently of recording personal recents. */
      });
  }, [id, resourceId, organisationId, recordVisit]);
  if (!session.can("content.read", "catalogue"))
    return <Empty title="You do not have access to learning resources" />;
  return (
    <QueryState query={query}>
      {(resource) => {
        const asset =
          resource.assets.find((x) => x.id === selected) ??
          resource.assets.find((x) => x.isPrimary) ??
          resource.assets[0];
        return (
          <>
            <Link className="back-link" to="/app/resources">
              ← Learning resources
            </Link>
            <PageTitle
              eyebrow={resource.content.contentType.replaceAll("-", " ")}
              title={resource.content.title}
              actions={
                asset &&
                resource.content.isDownloadable && (
                  <a className="button secondary" href={asset.downloadUrl}>
                    <Download size={16} />
                    Download
                  </a>
                )
              }
            >
              {resource.content.summary}
            </PageTitle>
            <div className="resource-layout">
              <section className="card media-card">
                {asset ? (
                  <MediaViewer key={asset.id} asset={asset} />
                ) : (
                  <Empty title="No files attached">
                    This resource currently contains text only.
                  </Empty>
                )}
                {resource.content.description && (
                  <div className="resource-description">
                    <h2>About this resource</h2>
                    <p className="preserve-lines">
                      {resource.content.description}
                    </p>
                  </div>
                )}
              </section>
              <aside className="card resource-context">
                <h2>Resource guide</h2>
                <dl>
                  <dt>Language</dt>
                  <dd>{resource.content.languageCode}</dd>
                  {resource.content.estimatedDurationMinutes && (
                    <>
                      <dt>Suggested time</dt>
                      <dd>
                        {resource.content.estimatedDurationMinutes} minutes
                      </dd>
                    </>
                  )}
                </dl>
                {resource.assets.length > 0 && (
                  <>
                    <h3>Files</h3>
                    {resource.assets.map((x) => (
                      <button
                        key={x.id}
                        className={
                          "file-choice " +
                          (asset?.id === x.id ? "selected" : "")
                        }
                        onClick={() => setSelected(x.id)}
                      >
                        {x.fileName}
                        <small>
                          {(x.fileSizeBytes / 1024 / 1024).toFixed(1)} MB ·{" "}
                          {x.assetType}
                        </small>
                      </button>
                    ))}
                  </>
                )}
                <h3>Tags</h3>
                <div className="tag-list">
                  {resource.tags.map((x) => (
                    <Link key={x.id} to={"/app/resources?tagId=" + x.id}>
                      <Badge>{x.name}</Badge>
                    </Link>
                  ))}
                </div>
                <h3>Collections</h3>
                {resource.collections.map((x) => (
                  <p key={x.id}>
                    <Link to={"/app/resources?collectionId=" + x.collection.id}>
                      {x.collection.name}
                    </Link>
                  </p>
                ))}
                {resource.mappings.length > 0 && (
                  <>
                    <h3>Curriculum connections</h3>
                    {resource.mappings.map((x) => (
                      <p key={x.id}>
                        <Link
                          to={
                            "/app/resources?" +
                            queryString({
                              [x.nodeType[0].toLowerCase() +
                              x.nodeType.slice(1) +
                              "Id"]: x.nodeId,
                            })
                          }
                        >
                          {x.nodeType.replace(/([A-Z])/g, " $1").trim()}
                          {x.nodeName && ": " + x.nodeName}
                        </Link>
                      </p>
                    ))}
                  </>
                )}
              </aside>
            </div>
          </>
        );
      }}
    </QueryState>
  );
}
