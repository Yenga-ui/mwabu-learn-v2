import { describe, expect, it } from "vitest";
import { journeySteps, publicStep } from "./journey";
import { blankSetup, type Detail, type TeachingStep } from "./types";

describe("classroom journey contracts", () => {
  it("maps legacy planning guidance without projecting suggested answers or private text", () => {
    const detail = {
      document: {
        setup: blankSetup(),
        sections: [
          {
            code: "starter",
            title: "Introduction",
            body: "Private planning guidance",
            minutes: 5,
            sourceIds: [],
          },
          {
            code: "questions",
            title: "Answers",
            body: "Secret answer",
            minutes: 0,
            sourceIds: [],
          },
        ],
        references: [],
      },
    } as unknown as Detail;
    const steps = journeySteps(detail);
    expect(steps).toHaveLength(1);
    expect(steps[0].teacherInstructions).toBe("Private planning guidance");
    expect(steps[0].learnerExplanation).toBe("");
    const projected = JSON.stringify(publicStep(steps[0]));
    expect(projected).not.toContain("Private planning guidance");
    expect(projected).not.toContain("Secret answer");
  });
  it("whitelists projected fields and preserves pinned dependencies", () => {
    const step = {
      id: "one",
      sectionCode: "starter",
      title: "Equal parts",
      phase: "Discussion",
      minutes: 5,
      teacherInstructions: "PRIVATE INSTRUCTIONS",
      teacherNotes: "PRIVATE NOTES",
      learnerExplanation: "Compare the parts",
      hint: "Compare sizes",
      answer: "Equal",
      resources: [
        {
          contentItemId: "resource",
          assetId: "asset",
          checksum: "abc",
          title: "Fractions",
        },
      ],
    } satisfies TeachingStep;
    const projected = publicStep(step);
    expect(projected.resources).toEqual(step.resources);
    expect(JSON.stringify(projected)).not.toContain("PRIVATE");
    expect(projected.learnerExplanation).toBe("Compare the parts");
  });
});
