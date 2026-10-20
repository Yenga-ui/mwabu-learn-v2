import type { Detail, TeachingStep } from "./types";
export const supportedTeachingMedia = [
  "application/vnd.mwabu.activity+json",
  "application/pdf",
  "application/zip",
  "application/x-zip-compressed",
  "image/png",
  "image/jpeg",
  "image/gif",
  "image/webp",
  "audio/mpeg",
  "audio/ogg",
  "audio/wav",
  "video/mp4",
  "video/webm",
];
export function resourceKind(mime: string) {
  if (mime === "application/zip" || mime === "application/x-zip-compressed")
    return "Reviewed SCORM lesson (online playback)";
  if (mime === "application/vnd.mwabu.activity+json")
    return "Interactive activity";
  if (mime === "application/pdf") return "PDF document";
  if (["image/png", "image/jpeg", "image/gif", "image/webp"].includes(mime))
    return "Image";
  if (["audio/mpeg", "audio/ogg", "audio/wav"].includes(mime)) return "Audio";
  if (["video/mp4", "video/webm"].includes(mime)) return "Video";
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
