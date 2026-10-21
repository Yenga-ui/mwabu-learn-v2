import NodePicker from "../curriculum/NodePicker";
import type { Setup } from "./types";
export default function LessonSetup({
  value,
  change,
  disabled = false,
  simple = false,
  hideFreeFormContext = false,
}: {
  value: Setup;
  change: (value: Setup) => void;
  disabled?: boolean;
  simple?: boolean;
  hideFreeFormContext?: boolean;
}) {
  const set = <K extends keyof Setup>(key: K, item: Setup[K]) =>
    change({ ...value, [key]: item });
  return (
    <fieldset className="lesson-setup" disabled={disabled}>
      <legend>Lesson setup</legend>
      <div className="lesson-modes" role="group" aria-label="Planning mode">
        <button
          type="button"
          className={
            value.mode === "CurriculumAligned" ? "button" : "button secondary"
          }
          aria-pressed={value.mode === "CurriculumAligned"}
          onClick={() => set("mode", "CurriculumAligned")}
        >
          Curriculum-aligned
        </button>
        <button
          type="button"
          className={value.mode === "FreeForm" ? "button" : "button secondary"}
          aria-pressed={value.mode === "FreeForm"}
          onClick={() => set("mode", "FreeForm")}
        >
          Free-form
        </button>
      </div>
      <p>
        {value.mode === "CurriculumAligned"
          ? "Choose your curriculum, then follow the grade, subject and topic to a learning outcome. Search by name at any step."
          : "Create a flexible lesson. No official curriculum alignment is claimed. Your other setup fields are retained when switching modes."}
      </p>
      {value.mode === "CurriculumAligned" ? (
        <>
          <NodePicker
            startWithGrade={simple}
            onReset={() => set("learningOutcomeId", null)}
            onSelect={(type, id, name, context) =>
              change({
                ...value,
                learningOutcomeId: type === "LearningOutcome" ? id : null,
                ...(type === "LearningOutcome"
                  ? {
                      title: value.title.trim() ? value.title : name,
                      subject: context[3],
                      targetLearners: context[2],
                      objectives: value.objectives.trim()
                        ? value.objectives
                        : name,
                    }
                  : {}),
              })
            }
          />
          <p role="status">
            {value.learningOutcomeId
              ? "Learning outcome selected. The saved lesson shows its authoritative full context."
              : "Choose a learning outcome to finish alignment. Your curriculum choices stay linked automatically."}
          </p>
        </>
      ) : hideFreeFormContext ? (
        <p role="status">
          {value.targetLearners && value.subject
            ? `${value.targetLearners} · ${value.subject}. Change your choices above.`
            : "Choose your grade, subject and topic above to continue."}
        </p>
      ) : (
        <div className="lesson-grid">
          <label>
            Subject / learning area
            <input
              required
              maxLength={200}
              value={value.subject}
              onChange={(e) => set("subject", e.target.value)}
            />
          </label>
          <label>
            Target age / grade
            <input
              required
              maxLength={200}
              value={value.targetLearners}
              onChange={(e) => set("targetLearners", e.target.value)}
            />
          </label>
        </div>
      )}
      <label>
        Lesson title
        <input
          required
          maxLength={200}
          value={value.title}
          onChange={(e) => set("title", e.target.value)}
        />
      </label>
      {simple && (
        <small>
          A title is suggested when you choose a learning outcome. You can
          change it.
        </small>
      )}
      <div className="lesson-grid">
        <label>
          Lesson duration (minutes)
          <input
            type="number"
            required
            min={5}
            max={480}
            value={value.durationMinutes}
            onChange={(e) => set("durationMinutes", Number(e.target.value))}
          />
        </label>
        <label>
          Lesson date
          <input
            type="date"
            value={value.lessonDate ?? ""}
            onChange={(e) => set("lessonDate", e.target.value || null)}
          />
        </label>
        <label>
          Language of instruction
          <select
            required
            value={value.language}
            onChange={(e) => set("language", e.target.value)}
          >
            {[
              ["en", "English"],
              ["bem", "Bemba"],
              ["nya", "Chinyanja"],
              ["toi", "Tonga"],
              ["loz", "Lozi"],
              ["lun", "Lunda"],
              ["lue", "Luvale"],
              ["kqn", "Kaonde"],
            ].map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
            {!["en", "bem", "nya", "toi", "loz", "lun", "lue", "kqn"].includes(
              value.language,
            ) && <option value={value.language}>Saved language</option>}
          </select>
        </label>
        <label>
          Class size
          <input
            type="number"
            min={1}
            max={1000}
            value={value.classSize}
            onChange={(e) => set("classSize", Number(e.target.value))}
          />
        </label>
      </div>
      <details open={!simple}>
        <summary>Optional teaching context and reuse settings</summary>
        {[
          ["objectives", "Initial learning objectives", 4000],
          ["teachingContext", "Teaching context", 4000],
          ["availableResources", "Available teaching resources", 2000],
          [
            "supportRequirements",
            "Learner ability and support requirements (no individual names)",
            2000,
          ],
          ["instructions", "Additional teaching instructions", 4000],
        ].map(([key, label, limit]) => (
          <label key={key}>
            {label}
            <textarea
              rows={3}
              maxLength={Number(limit)}
              value={value[key as keyof Setup] as string}
              onChange={(e) => set(key as keyof Setup, e.target.value)}
            />
          </label>
        ))}
        <label>
          Teacher-created lesson reuse licence
          <select
            value={value.reuseLicence}
            onChange={(e) => set("reuseLicence", e.target.value)}
          >
            {["Internal", "CC0-1.0", "CC-BY-4.0", "CC-BY-SA-4.0"].map(
              (licence) => (
                <option key={licence}>{licence}</option>
              ),
            )}
          </select>
        </label>
        <small>
          Only license your own teaching contribution. Third-party
          curricula/resources retain their rights; selected share-alike evidence
          requires a compatible lesson licence.
        </small>
        <small>
          Use anonymous class-level needs. Do not enter identifiable learner
          information in material intended for AI assistance.
        </small>
      </details>
    </fieldset>
  );
}
