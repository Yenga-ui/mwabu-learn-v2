import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { readFileSync, mkdirSync } from "node:fs";
import {
  login,
  zip,
  synthetic,
  createPackage,
  approve,
} from "./support/packages";

const source = (title: string) =>
  `<!doctype html><html><head><title>Source</title></head><body><h1>${title}</h1><p>Subject: Mathematics</p><p>Grade: 4</p><p>Topic: Fractions</p><h2>Specific competences</h2><p>4.3.1.1 Apply the concept of fractions in different contexts</p><h2>Teaching and learning materials/Resources</h2><p>Paper circles</p><p>Mwabu Learn content: Sandbox fractions</p><h2>Introduction</h2><p>Fold paper into equal parts.</p><h2>Questions</h2><p>Are the parts equal?</p><h2>Lesson development</h2><p>Compare one half and two quarters.</p><h2>Conclusion</h2><p>Equivalent fractions show the same share.</p></body></html>`;
async function preview(page: Page, buffer: Buffer, name: string) {
  await page.goto("/app/content-imports");
  await page
    .getByLabel("Authorised source ZIP")
    .setInputFiles({ name, mimeType: "application/zip", buffer });
  await page
    .getByRole("button", { name: "Preview source archive", exact: true })
    .click();
  await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
}
async function apply(page: Page) {
  const panel = page.getByRole("region", { name: "Import reconciliation" });
  await panel
    .getByLabel(
      "Curriculum, language, accessibility, rights and privacy review evidence",
      { exact: true },
    )
    .fill(
      "Automated acceptance of this explicitly isolated fixture: authorised source attribution, language, accessibility fallback, rights and anonymous privacy reviewed.",
    );
  await panel
    .getByRole("checkbox", { name: /I reviewed these selected source records/ })
    .check();
  await panel
    .getByRole("checkbox", { name: /Publish eligible source plans/ })
    .check();
  await panel
    .getByLabel("Environment confirmation", { exact: true })
    .fill(await panel.locator("strong").innerText());
  await panel
    .getByRole("button", { name: /^Import \d+ reviewed records$/ })
    .click();
  await expect(
    panel.getByRole("link", { name: "Inspect resource", exact: true }).first(),
  ).toBeVisible();
}
test("reviewed source import connects teacher discovery, native adaptation, PDF, SCORM teaching and learner playback", async ({
  page,
  browser,
}) => {
  test.setTimeout(150000);
  const suffix = randomUUID().slice(0, 8);
  const title = "Sprint 4 source fractions " + suffix;
  const bytes = zip({ "fractions.html": source(title) });
  await login(page, "PlatformAdmin");
  await preview(page, bytes, "source-plans.zip");
  await page
    .getByRole("button", { name: "Select validated records", exact: true })
    .click();
  await apply(page);
  const inspect = page.getByRole("link", {
    name: "Inspect resource",
    exact: true,
  });
  const original = (await inspect.getAttribute("href"))!;
  const id = original.split("/").at(-1)!;
  await preview(page, bytes, "source-plans.zip");
  await expect(
    page.getByRole("link", { name: "Inspect resource", exact: true }),
  ).toHaveAttribute("href", original);
  const author = await browser.newContext({ ignoreHTTPSErrors: true });
  const reviewer = await browser.newContext({ ignoreHTTPSErrors: true });
  const teacher = await browser.newContext({ ignoreHTTPSErrors: true });
  const learner = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    const packageTitle = "Sprint 4 sandbox fractions " + suffix;
    const packageId = await createPackage(
      await author.newPage(),
      synthetic(),
      packageTitle,
    );
    await approve(await reviewer.newPage(), packageId);
    await page.goto(original);
    const links = page.getByRole("region", {
      name: "Review source resource associations",
    });
    await links
      .getByRole("combobox", { name: "Source resource reference", exact: true })
      .selectOption({ label: "Sandbox fractions" });
    await links
      .getByLabel("Search published learner resources", { exact: true })
      .fill(packageTitle);
    await links
      .getByRole("button", { name: "Find matching resources", exact: true })
      .click();
    await links
      .getByRole("combobox", {
        name: "Matching published resource",
        exact: true,
      })
      .selectOption(packageId);
    const file = links.getByRole("combobox", {
      name: "Exact teaching file",
      exact: true,
    });
    await expect(
      file.getByRole("option").filter({ hasText: "lesson.zip" }),
    ).toBeAttached();
    await file.selectOption(
      (await file
        .getByRole("option")
        .filter({ hasText: "lesson.zip" })
        .getAttribute("value"))!,
    );
    await links
      .getByLabel("Evidence for this source match", { exact: true })
      .fill(
        "Exact source reference Sandbox fractions matches this independently reviewed SCORM fixture. Pin this published checksum for teacher-led practice.",
      );
    await links
      .getByRole("button", { name: "Link reviewed resource", exact: true })
      .click();
    await page
      .getByRole("dialog")
      .getByRole("button", { name: "Confirm", exact: true })
      .click();
    await expect(
      links.locator(".action-row").getByText(packageTitle, { exact: true }),
    ).toBeVisible();
    const t = await teacher.newPage();
    await login(t, "Teacher");
    await t.goto("/app");
    await t
      .getByRole("navigation", { name: "Teaching tasks" })
      .getByRole("link", { name: /Find a lesson/ })
      .click();
    await t
      .getByRole("combobox", { name: "Curriculum collection", exact: true })
      .selectOption("2023");
    await t
      .getByRole("combobox", { name: "Grade", exact: true })
      .selectOption("4");
    await t
      .getByRole("combobox", { name: "Subject", exact: true })
      .selectOption("Mathematics");
    await t
      .getByRole("combobox", { name: "Topic", exact: true })
      .selectOption("Fractions");
    await expect(
      t.getByRole("link", { name: title, exact: true }),
    ).toBeVisible();
    mkdirSync("../artifacts/sprint4/screenshots", { recursive: true });
    await t.screenshot({
      path: "../artifacts/sprint4/screenshots/teacher-library.png",
      fullPage: true,
    });
    await t.getByRole("link", { name: title, exact: true }).click();
    await expect(
      t
        .frameLocator('iframe[title="Original Mwabu lesson plan"]')
        .getByText("Fold paper into equal parts.", { exact: true }),
    ).toBeVisible();
    await t
      .getByRole("button", { name: "Play " + packageTitle, exact: true })
      .click();
    await t
      .getByRole("button", { name: "Start interactive lesson", exact: true })
      .click();
    await t
      .frameLocator("iframe.scorm-frame")
      .getByRole("button", { name: "One of two equal parts", exact: true })
      .click();
    await expect(
      t.frameLocator("iframe.scorm-frame").locator("#feedback"),
    ).toContainText("Correct");
    await t
      .getByRole("button", { name: "Return to lesson plan", exact: true })
      .click();
    await t
      .getByRole("button", { name: "Use this lesson", exact: true })
      .click();
    await expect(t).toHaveURL(/\/app\/lessons\/[a-f0-9-]+$/);
    const personal = t.url();
    await t
      .getByLabel("Lesson plan title", { exact: true })
      .fill("My adapted fractions " + suffix);
    const adaptationSaved = t.waitForResponse(
      (response) =>
        response.request().method() === "PUT" &&
        /\/lessons\/[0-9a-f-]+$/.test(new URL(response.url()).pathname),
    );
    await t.getByRole("button", { name: "Save now", exact: true }).click();
    const adapted = await (await adaptationSaved).json();
    const sourceSearch = `/api/organisations/${adapted.lesson.organisationId}/lessons?text=${encodeURIComponent("My adapted fractions " + suffix)}`;
    const matching = await t.request.get(
      sourceSearch +
        "&sourceYear=2023&sourceGrade=4&sourceSubject=Mathematics&sourceTopic=Fractions",
    );
    expect(matching.ok()).toBeTruthy();
    expect(
      (await matching.json()).items.map((item: { id: string }) => item.id),
    ).toContain(adapted.lesson.id);
    for (const mismatch of [
      "sourceYear=2013",
      "sourceGrade=3",
      "sourceTerm=Term+1",
      "sourceYear=2023&status=Published",
    ]) {
      const response = await t.request.get(sourceSearch + "&" + mismatch);
      expect(response.ok()).toBeTruthy();
      expect((await response.json()).items).toHaveLength(0);
    }
    await expect(
      t.getByRole("status").filter({ hasText: /^Saved · revision/ }),
    ).toBeVisible();
    await t.reload();
    await expect(
      t.getByLabel("Lesson plan title", { exact: true }),
    ).toHaveValue("My adapted fractions " + suffix);
    await expect(
      t.getByRole("region", { name: "Original Mwabu source" }),
    ).toContainText("2023 curriculum");
    const download = t.waitForEvent("download");
    await t
      .getByRole("button", { name: "Export saved PDF", exact: true })
      .click();
    await (await download).saveAs("../artifacts/sprint4/source-adaptation.pdf");
    await t
      .getByRole("button", { name: "Prepare Lesson Journey", exact: true })
      .click();
    await t.getByRole("link", { name: "Teach Lesson", exact: true }).click();
    await expect(
      t.getByText("Fold paper into equal parts.", { exact: true }),
    ).toHaveCount(0);
    await t.getByRole("button", { name: "Next step", exact: true }).click();
    await t
      .getByRole("combobox", { name: "Teaching resource", exact: true })
      .selectOption({ label: packageTitle });
    await t
      .getByRole("button", { name: "Start interactive lesson", exact: true })
      .click();
    await t
      .frameLocator("iframe.scorm-frame")
      .getByRole("button", { name: "One of two equal parts", exact: true })
      .click();
    await expect(
      t.frameLocator("iframe.scorm-frame").locator("#feedback"),
    ).toContainText("Correct");
    await t.screenshot({
      path: "../artifacts/sprint4/screenshots/teacher-scorm-classroom.png",
      fullPage: true,
    });
    const teachingUrl = t.url();
    await t
      .getByRole("link", { name: "Exit to preparation", exact: true })
      .click();
    await t.getByRole("link", { name: "Workspace", exact: true }).click();
    await t.reload();
    await t
      .getByRole("region", { name: "Continue teaching", exact: true })
      .getByRole("link", { name: /Continue teaching:/ })
      .click();
    expect(new URL(t.url()).searchParams.get("revision")).toBe(
      new URL(teachingUrl).searchParams.get("revision"),
    );
    await expect(
      t.getByRole("heading", { name: "Lesson development", exact: true }),
    ).toBeVisible();
    await expect(
      t.getByRole("combobox", { name: "Teaching resource", exact: true }),
    ).not.toHaveValue("");
    await expect(
      t.getByRole("button", { name: "Start interactive lesson", exact: true }),
    ).toBeVisible();
    await expect(
      t.getByText(/Interactive courses reopen from the beginning/),
    ).toBeVisible();
    await t.getByRole("link", { name: "Finish lesson", exact: true }).click();
    await expect(t).toHaveURL(/\/app\/lessons/);
    const l = await learner.newPage();
    await login(l, "Learner");
    await l.goto(
      "/app/library?year=2023&grade=4&subject=Mathematics&topic=Fractions",
    );
    await l.getByRole("link", { name: packageTitle, exact: true }).click();
    await l
      .getByRole("button", { name: "Start interactive lesson", exact: true })
      .click();
    await l
      .frameLocator("iframe.scorm-frame")
      .getByRole("button", { name: "One of two equal parts", exact: true })
      .click();
    await expect(
      l.frameLocator("iframe.scorm-frame").locator("#feedback"),
    ).toContainText("Anonymous practice");
    await l.screenshot({
      path: "../artifacts/sprint4/screenshots/learner-feedback.png",
      fullPage: true,
    });
    await l
      .getByRole("link", { name: "← Curriculum library", exact: true })
      .click();
    await expect(l).toHaveURL(/year=2023/);
    await expect(
      l.getByRole("link", {
        name: "My adapted fractions " + suffix,
        exact: true,
      }),
    ).toHaveCount(0);
    await t.goto(personal);
    await expect(
      t.getByLabel("Lesson plan title", { exact: true }),
    ).toHaveValue("My adapted fractions " + suffix);
    await page.goto(`/app/content/${id}`);
    await expect(
      page.getByRole("heading", { name: title, exact: true }),
    ).toBeVisible();
  } finally {
    await Promise.all([
      author.close(),
      reviewer.close(),
      teacher.close(),
      learner.close(),
    ]);
  }
});

test("a new plan retains selected library context without inventing managed alignment", async ({
  page,
}) => {
  const suffix = randomUUID().slice(0, 8);
  await login(page, "PlatformAdmin");
  await preview(
    page,
    zip({
      "context.html": source("Context source " + suffix).replace(
        "<p>Topic: Fractions</p>",
        "<p>Term: Term 1</p><p>Topic: Fractions</p>",
      ),
    }),
    "context-" + suffix + ".zip",
  );
  await page
    .getByRole("button", { name: "Select validated records", exact: true })
    .click();
  await apply(page);
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await login(page, "Teacher");
  await page.goto(
    "/app/library?year=2023&grade=4&subject=Mathematics&topic=Fractions&term=Term+1&text=no-match-" +
      suffix,
  );
  await page
    .getByRole("link", {
      name: "Create a lesson from the selected context",
      exact: true,
    })
    .click();
  await expect(
    page.getByRole("region", { name: "Selected library context" }),
  ).toContainText(
    "2023 Zambian curriculum · Grade 4 · Mathematics · Fractions · Term 1",
  );
  await expect(
    page.getByRole("button", { name: "Free-form", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByLabel("Subject / learning area")).toHaveValue(
    "Mathematics",
  );
  await expect(page.getByLabel("Target age / grade")).toHaveValue("Grade 4");
  await expect(page.getByLabel("Lesson title", { exact: true })).toHaveValue(
    "Fractions",
  );
  const draftTitle = "Context retained " + suffix;
  await page.getByLabel("Lesson title", { exact: true }).fill(draftTitle);
  const saved = page.waitForResponse(
    (response) =>
      response.request().method() === "POST" &&
      /\/api\/organisations\/[^/]+\/lessons$/.test(
        new URL(response.url()).pathname,
      ),
  );
  await page.getByRole("button", { name: "Create draft", exact: true }).click();
  const detail = await (await saved).json();
  const searchPath = `/api/organisations/${detail.lesson.organisationId}/lessons`;
  const filtered = await page.request.get(
    searchPath + "?page=1&pageSize=1&text=" + encodeURIComponent(draftTitle),
  );
  expect(filtered.ok()).toBeTruthy();
  const filteredPage = await filtered.json();
  expect(filteredPage.pageSize).toBe(1);
  expect(filteredPage.items.map((item: { id: string }) => item.id)).toEqual([
    detail.lesson.id,
  ]);
  const nextPage = await page.request.get(
    searchPath + "?page=2&pageSize=1&text=" + encodeURIComponent(draftTitle),
  );
  expect(nextPage.ok()).toBeTruthy();
  expect((await nextPage.json()).items).toHaveLength(0);
  expect(
    (await page.request.get(searchPath + "?page=0&pageSize=1")).status(),
  ).toBe(400);
  const grades = await page.request.get(
    "/api/curriculum/grades/search?page=1&pageSize=1",
  );
  expect(grades.ok()).toBeTruthy();
  expect((await grades.json()).pageSize).toBe(1);
  expect(detail.document.curriculum).toBeNull();
  expect(detail.document.importedFrom ?? null).toBeNull();
  expect(detail.document.setup.learningOutcomeId).toBeNull();
  expect(detail.document.setup.teachingContext).toContain("Fractions · Term 1");
  expect(detail.document.ministryForm.topic).toBe("Fractions");
  await expect(page).toHaveURL(/\/app\/lessons\/[0-9a-f-]+$/);
  await page.reload();
  await expect(
    page.getByRole("heading", { name: draftTitle, exact: true }),
  ).toBeVisible();
  await page.goto(
    "/app/lessons?status=Published&text=" + encodeURIComponent(draftTitle),
  );
  await expect(
    page.getByRole("combobox", { name: "Lesson status", exact: true }),
  ).toHaveValue("Published");
  await expect(
    page.getByPlaceholder("Search lesson plans", { exact: true }),
  ).toHaveValue(draftTitle);
  await expect(
    page.getByText("No lesson plans here yet", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("combobox", { name: "Lesson status", exact: true })
    .selectOption("Draft");
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await expect(
    page.getByRole("combobox", { name: "Lesson status", exact: true }),
  ).toHaveValue("Draft");
  await expect(
    page.getByRole("heading", { name: draftTitle, exact: true }),
  ).toBeVisible();
});

if (process.env.MWABU_SOURCE_PLAN_SAMPLE) {
  test("actual Product Team plan imports with its source diagrams and native official fields", async ({
    page,
    browser,
  }) => {
    await login(page, "PlatformAdmin");
    await preview(
      page,
      readFileSync(process.env.MWABU_SOURCE_PLAN_SAMPLE!),
      "product-team-grade4.zip",
    );
    const row = page
      .getByRole("row")
      .filter({ hasText: /5\. Equivalent fractions/ });
    await row.getByRole("checkbox").check();
    await apply(page);
    const href = (await row
      .getByRole("link", { name: "Inspect resource", exact: true })
      .getAttribute("href"))!;
    const teacher = await browser.newContext({ ignoreHTTPSErrors: true });
    try {
      const t = await teacher.newPage();
      await login(t, "Teacher");
      await t.goto(href.replace("/app/content/", "/app/resources/"));
      await expect(
        t
          .frameLocator('iframe[title="Original Mwabu lesson plan"]')
          .getByText(/Precious has to make 24 stitches/),
      ).toBeVisible();
      expect(
        await t
          .frameLocator('iframe[title="Original Mwabu lesson plan"]')
          .locator("img")
          .count(),
      ).toBeGreaterThan(0);
      await t
        .getByRole("button", { name: "Use this lesson", exact: true })
        .click();
      await expect(
        t.getByLabel("Lesson plan title", { exact: true }),
      ).toHaveValue("Equivalent fractions");
      await t.screenshot({
        path: "../artifacts/sprint4/screenshots/actual-native-source-plan.png",
        fullPage: true,
      });
    } finally {
      await teacher.close();
    }
  });
}
