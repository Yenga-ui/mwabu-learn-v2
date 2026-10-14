import type { LessonDocument } from "./types";
import { ministryForm, stageLabels } from "./MinistryForm";
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
  const form = ministryForm(document);
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
      <h2>Lesson information</h2>
      <p>
        School: {form.school || organisation} · Name of teacher:{" "}
        {form.teacherName || teacher} · Class:{" "}
        {form.className || "Not supplied"} · Time:{" "}
        {form.lessonTime || "Not scheduled"} · Total no. of learners:{" "}
        {document.setup.classSize}
      </p>
      <h2>Lesson details</h2>
      <p>
        General competences:{" "}
        {form.generalCompetences.join(", ") || "Not selected"}
      </p>
      <p>
        Learning environment:{" "}
        {form.learningEnvironments.join(", ") || "Not selected"}{" "}
        {form.environmentNote}
      </p>
      <p>Lesson goal: {form.lessonGoal || "Not supplied"}</p>
      <p>Rationale: {form.rationale || "Not supplied"}</p>
      <p>
        Expected standard:{" "}
        {form.expectedStandard || "Verify with curriculum administrator"}
      </p>
      {form.syllabusProvenance && <p>{form.syllabusProvenance}</p>}
      <h2>Lesson progression</h2>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Stage / minutes</th>
              <th>Teacher's role</th>
              <th>Learners' role</th>
              <th>Assessment criteria</th>
            </tr>
          </thead>
          <tbody>
            {stageLabels
              .filter(
                ([code], i) =>
                  i < 5 ||
                  form.progression.some(
                    (x) =>
                      x.code === code &&
                      (x.teacherActivities ||
                        x.learnerActivities ||
                        x.assessmentCriteria),
                  ),
              )
              .map(([code, label]) => {
                const stage = form.progression.find((x) => x.code === code);
                return (
                  <tr key={code}>
                    <th>
                      {label} ({stage?.minutes ?? 0})
                    </th>
                    <td>{stage?.teacherActivities}</td>
                    <td>{stage?.learnerActivities}</td>
                    <td>{stage?.assessmentCriteria}</td>
                  </tr>
                );
              })}
          </tbody>
        </table>
      </div>
      <h2>Lesson evaluation</h2>
      <p>
        {form.evaluation ||
          "Reflect after teaching: what went well, what could improve, and what learners enjoyed or found difficult."}
      </p>
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
