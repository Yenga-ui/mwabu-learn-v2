import type { Detail, TeachingStep } from "./types";
export function resourceKind(mime: string) {
  if (mime === "application/vnd.mwabu.activity+json")
    return "Interactive activity";
  if (mime === "application/pdf") return "PDF document";
  if (mime.startsWith("image/")) return "Image";
  if (mime.startsWith("audio/")) return "Audio";
  if (mime.startsWith("video/")) return "Video";
  return "Download-only file";
}

// Map private planning text into preparation guidance. Teachers explicitly author
// the projected explanation; suggested answers in legacy plans are never copied.
export function journeySteps(detail: Detail): TeachingStep[] {
  if (detail.document.teachingSteps?.length)
    return detail.document.teachingSteps;
  const phases: Record<string, string> = {
    starter: "Introduction",
    development: "Explanation",
    "learner-activities": "Guided practice",
    "formative-assessment": "Assessment",
    conclusion: "Recap",
  };
  return detail.document.sections
    .filter((s) => phases[s.code] && s.body.trim())
    .map((s) => ({
      id: s.code,
      sectionCode: s.code,
      title: s.title,
      phase: phases[s.code],
      minutes: s.minutes,
      teacherInstructions: s.body,
      learnerExplanation: "",
      hint: "",
      answer: "",
      teacherNotes: "",
      resources: [],
    }));
}
export function publicStep(step: TeachingStep) {
  return {
    id: step.id,
    sectionCode: step.sectionCode,
    title: step.title,
    phase: step.phase,
    minutes: step.minutes,
    learnerExplanation: step.learnerExplanation,
    hint: step.hint,
    answer: step.answer,
    resources: step.resources,
  };
}
