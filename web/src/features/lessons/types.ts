export type Mode = "CurriculumAligned" | "FreeForm";
export type LessonStatus = "Draft" | "InReview" | "Published" | "Archived";
export interface Setup {
  reuseLicence: string;
  mode: Mode;
  learningOutcomeId: string | null;
  title: string;
  subject: string;
  targetLearners: string;
  durationMinutes: number;
  lessonDate: string | null;
  language: string;
  classSize: number;
  objectives: string;
  teachingContext: string;
  availableResources: string;
  supportRequirements: string;
  instructions: string;
}
export interface Section {
  code: string;
  title: string;
  body: string;
  minutes: number;
  sourceIds: string[];
}
export interface CurriculumContext {
  curriculum: string;
  version: string;
  grade: string;
  subject: string;
  term: string;
  topic: string;
  competency: string;
  outcome: string;
  provenanceNotice: string;
}
export interface Citation {
  id: string;
  title: string;
  kind: string;
  url: string | null;
  publisher: string | null;
  licence: string | null;
  retrievalStatus: string;
  evidenceDescription: string;
  contentItemId: string | null;
}
export interface LessonDocument {
  schemaVersion?: number;
  ministryForm?: MinistryLessonForm | null;
  setup: Setup;
  curriculum: CurriculumContext | null;
  sections: Section[];
  references: Citation[];
}
export interface MinistryLessonForm {
  syllabusProvenance?: string;
  referenceCompetenceCode?: string;
  school: string;
  teacherName: string;
  className: string;
  lessonTime: string;
  lessonGoal: string;
  rationale: string;
  expectedStandard: string;
  generalCompetences: string[];
  learningEnvironments: string[];
  environmentNote: string;
  evaluation: string;
  needsReview: string[];
  progression: {
    code: string;
    teacherActivities: string;
    learnerActivities: string;
    assessmentCriteria: string;
    minutes: number;
  }[];
}
export interface Lesson {
  id: string;
  organisationId: string;
  ownerUserId: string;
  title: string;
  mode: Mode;
  status: LessonStatus;
  revision: number;
  isOrganisationTemplate: boolean;
  createdAt: string;
  updatedAt: string | null;
}
export interface Detail {
  authorName: string;
  lesson: Lesson;
  document: LessonDocument;
  canEdit: boolean;
  canReview: boolean;
  viewedRevision: number;
}
export interface GenerationJob {
  id: string;
  state: string;
  generationId: string | null;
  cancelRequested: boolean;
  errorCode: string | null;
  createdAt: string;
  completedAt: string | null;
}
export interface Source extends Citation {
  publicationDate: string | null;
  accessedAt: string | null;
  credibility: string;
  isSelected: boolean;
  isExcluded: boolean;
  priority: number;
  reusePermitted: boolean;
  permissionEvidence: string | null;
  evidencePreview: string;
}
export interface LocalSource {
  id: string;
  title: string;
  summary: string | null;
  language: string;
  type: string;
  score: number;
  reasons: string[];
}
export interface WebSource {
  title: string;
  url: string;
  description: string;
  publisher: string | null;
  publicationDate: string | null;
  score: number;
}
export interface Generation {
  id: string;
  state: string;
  baseRevision: number;
  operation: string;
  sectionCode: string | null;
  provider: string;
  model: string;
  estimatedCostUsd: number;
  usageEstimated: boolean;
  errorCode: string | null;
  createdAt: string;
  result: { explanation: string; sections: Section[] } | null;
}
export interface Configuration {
  aiConfigured: boolean;
  searchConfigured: boolean;
  organisationAiApproved: boolean;
  organisationSearchApproved: boolean;
  operations: string[];
  sectionCodes: string[];
  maxDocumentBytes: number;
}
export interface Settings {
  externalAiApproved: boolean;
  webSearchApproved: boolean;
  dailyRequestLimit: number;
  monthlyTokenLimit: number;
  monthlyCostLimitUsd: number;
  sourceRetentionDays: number;
}
export interface Usage {
  requestsToday: number;
  chargedTokensThisMonth: number;
  estimatedCostUsdThisMonth: number;
  dailyRequestLimit: number;
  monthlyTokenLimit: number;
  monthlyCostLimitUsd: number;
  uncertainRequests: number;
}
export const sectionDefinitions = [
  ["title", "Lesson title"],
  ["curriculum", "Curriculum and subject"],
  ["learners", "Grade and target learners"],
  ["topic", "Topic and subtopic"],
  ["schedule", "Duration and lesson date"],
  ["competencies", "Managed competencies"],
  ["learning-outcomes", "Managed learning outcomes"],
  ["objectives", "SMART lesson objectives"],
  ["prerequisites", "Prerequisite knowledge"],
  ["materials", "Teaching and learning materials"],
  ["starter", "Introduction / lesson starter"],
  ["development", "Step-by-step lesson development"],
  ["teacher-activities", "Teacher activities"],
  ["learner-activities", "Learner activities"],
  ["timing", "Activity timings"],
  ["methodologies", "Teaching methodologies"],
  ["groups", "Individual and group activities"],
  ["differentiation", "Differentiation and inclusion"],
  ["disability-support", "Support for learners with disabilities"],
  ["low-resource", "Low-resource alternatives"],
  ["formative-assessment", "Formative assessment"],
  ["summative-assessment", "Summative assessment (where appropriate)"],
  ["questions", "Assessment questions and suggested answers"],
  ["conclusion", "Lesson conclusion"],
  ["extension", "Homework / extension"],
  ["references", "References and resource links"],
  ["reflection", "Teacher reflection"],
  ["follow-up", "Follow-up recommendations"],
] as const;
export const authoritative = [
  "curriculum",
  "competencies",
  "learning-outcomes",
];
export const blankSections = (): Section[] =>
  sectionDefinitions.map(([code, title]) => ({
    code,
    title,
    body: "",
    minutes: 0,
    sourceIds: [],
  }));
export const blankSetup = (): Setup => ({
  reuseLicence: "Internal",
  mode: "CurriculumAligned",
  learningOutcomeId: null,
  title: "",
  subject: "",
  targetLearners: "",
  durationMinutes: 40,
  lessonDate: null,
  language: "en",
  classSize: 30,
  objectives: "",
  teachingContext: "",
  availableResources: "",
  supportRequirements: "",
  instructions: "",
});
export const lessonBase = (organisation: string) =>
  `/api/organisations/${organisation}/lessons`;
export function proposalDocument(
  document: LessonDocument,
  generation: Generation,
): LessonDocument {
  if (!generation.result) return document;
  return {
    ...document,
    sections: generation.sectionCode
      ? document.sections.map(
          (section) =>
            generation.result!.sections.find((x) => x.code === section.code) ??
            section,
        )
      : generation.result.sections,
  };
}
