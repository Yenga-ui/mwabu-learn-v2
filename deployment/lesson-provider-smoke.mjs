// Explicit operator action: real providers, ordinary TLS verification, no sensitive logging.
import assert from "node:assert/strict";

const origin = process.env.MWABU_LESSON_SMOKE_ORIGIN;
const organisation = process.env.MWABU_LESSON_SMOKE_ORGANISATION;
const token = process.env.MWABU_LESSON_SMOKE_TOKEN;
if (!origin || !organisation || !token) {
  console.error(
    "Live verification blocked: supply an approved HTTPS API origin, organisation GUID and short-lived authorized token through the documented environment variables.",
  );
  process.exit(78);
}
const url = new URL(origin);
assert(
  url.protocol === "https:" &&
    !url.username &&
    !url.password &&
    !url.search &&
    !url.hash &&
    url.pathname === "/",
  "Use a plain HTTPS API origin.",
);
assert(
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    organisation,
  ),
  "Organisation must be a GUID.",
);
const base = `/api/organisations/${organisation}/lessons`;
async function request(path, method = "GET", body) {
  const response = await fetch(new URL(path, url), {
    method,
    redirect: "error",
    signal: AbortSignal.timeout(185000),
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body ? { "Content-Type": "application/json" } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok)
    throw new Error(
      `Live verification HTTP ${response.status}; inspect safe API diagnostics without logging credentials or response bodies.`,
    );
  return response.status === 204 ? undefined : response.json();
}
let draft;
try {
  const configuration = await request(base + "/configuration");
  assert(
    configuration.aiConfigured &&
      configuration.searchConfigured &&
      configuration.organisationAiApproved &&
      configuration.organisationSearchApproved,
    "Live verification blocked: global providers and organisation processing approvals must be configured.",
  );
  const setup = {
    mode: "FreeForm",
    title: "LIVE PROVIDER ACCEPTANCE — equal shares",
    subject: "Mathematics",
    targetLearners: "Age 9–10",
    durationMinutes: 40,
    language: "en",
    classSize: 30,
    objectives: "Identify and explain one half using equal shares.",
    availableResources: "Paper and pencils",
    teachingContext: "Non-personal provider acceptance fixture",
    supportRequirements: "Use verbal and visual explanations",
    instructions:
      "Use familiar, low-resource teaching examples; do not invent evidence or references.",
    reuseLicence: "Internal",
  };
  const sections = [
    "objectives",
    "starter",
    "development",
    "formative-assessment",
    "conclusion",
  ].map((code) => ({
    code,
    title: code.replaceAll("-", " "),
    body: "",
    minutes: 0,
    sourceIds: [],
  }));
  draft = await request(base, "POST", {
    expectedRevision: 0,
    setup,
    sections,
    reason: "Explicit live-provider acceptance",
  });
  const path = base + "/" + draft.lesson.id;
  const found = await request(path + "/discover/web", "POST", {
    text: "teaching fractions equal shares primary school",
    language: "en",
    multipleQueries: false,
  });
  assert(
    Array.isArray(found) &&
      found.length > 0 &&
      found.every((x) => /^https?:\/\//.test(x.url)),
    "Real search must return traceable public URLs.",
  );
  const result = await request(path + "/generations", "POST", {
    expectedRevision: draft.lesson.revision,
    operation: "generate",
    instructions:
      "Generate a practical 40-minute lesson with verifiable objectives and assessment.",
    confirmNoPersonalData: true,
  });
  assert.equal(
    result.provider,
    "OpenAI",
    "The live smoke must use the real OpenAI adapter, never a controlled test provider.",
  );
  assert.equal(result.state, "completed");
  assert(
    result.inputTokens > 0 &&
      result.outputTokens > 0 &&
      result.result.sections.length === draft.document.sections.length,
  );
  assert.equal(
    (await request(path)).lesson.revision,
    draft.lesson.revision,
    "A provider proposal must not silently edit the draft.",
  );
  console.log(
    "Live search and OpenAI proposal verified: public search URLs, strict proposal, actual usage and unchanged draft. No response content logged.",
  );
} catch (failure) {
  console.error(
    failure instanceof assert.AssertionError ||
      failure.message?.startsWith("Live verification")
      ? failure.message
      : "Live verification failed; review provider/network configuration using safe server diagnostics.",
  );
  process.exitCode = 1;
} finally {
  if (draft) {
    try {
      await request(base + "/" + draft.lesson.id + "/status", "PATCH", {
        expectedRevision: draft.lesson.revision,
        status: "Archived",
        note: "Live-provider acceptance completed; retained for authorized history.",
      });
    } catch {
      console.error(
        "Review required: acceptance draft could not be archived automatically.",
      );
      process.exitCode = 1;
    }
  }
}
