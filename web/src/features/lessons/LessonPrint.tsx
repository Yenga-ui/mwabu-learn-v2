import type { LessonDocument } from "./types";
export function LessonPrint({
  document,
  organisation,
  teacher,
  revision,
  status,
  dirty,
}: {
  document: LessonDocument;
  organisation: string;
  teacher: string;
  revision: number;
  status: string;
  dirty: boolean;
}) {
  return (
    <article className="lesson-print">
      <p className="eyebrow">MWABU LEARN · LESSON PLAN</p>
      <h1>{document.setup.title}</h1>
      <p>
        {organisation} · {teacher} · {status} · revision {revision}
        {dirty && " · UNSAVED PREVIEW"}
      </p>
      <dl>
        <dt>Grade / learners</dt>
        <dd>{document.curriculum?.grade ?? document.setup.targetLearners}</dd>
        <dt>Subject / topic</dt>
        <dd>
          {document.curriculum?.subject ?? document.setup.subject} /{" "}
          {document.curriculum?.topic ?? document.setup.title}
        </dd>
        <dt>Duration / date / language</dt>
        <dd>
          {document.setup.durationMinutes} minutes ·{" "}
          {document.setup.lessonDate ?? "Not scheduled"} ·{" "}
          {document.setup.language}
        </dd>
      </dl>
      {document.sections
        .filter((section) => section.body.length > 0)
        .map((section) => (
          <section key={section.code}>
            <h2>
              {section.title}
              {section.minutes > 0 && ` (${section.minutes} minutes)`}
            </h2>
            <p>{section.body}</p>
            {section.sourceIds.length > 0 && (
              <small>
                References:{" "}
                {document.references
                  .filter((source) => section.sourceIds.includes(source.id))
                  .map((source) => source.title)
                  .join("; ")}
              </small>
            )}
          </section>
        ))}
      {document.references.length > 0 && (
        <section>
          <h2>References and rights</h2>
          {document.references.map((source) => (
            <p key={source.id}>
              {source.title} · {source.publisher ?? "Publisher unverified"}
              <br />
              {source.url}
              <br />
              {source.licence ?? "Unknown licence: link/reference only"}
              <br />
              {source.evidenceDescription}
            </p>
          ))}
        </section>
      )}
    </article>
  );
}
