import { test, expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { randomUUID } from "node:crypto";

async function login(page: Page, role: string) {
  const fixture = JSON.parse(readFileSync(".local/e2e/fixture.json", "utf8"));
  const account =
    role === "PlatformAdmin" ? fixture.admin : fixture.roles[role];
  await page.goto("/login");
  await page.getByLabel("Email address").fill(account.email);
  await page.getByLabel(/^Password/).fill(account.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.locator("#main")).toBeVisible();
}

// A tiny stored ZIP fixture. No additional archive library or executable fixture download.
function zip(files: Record<string, string>) {
  const locals: Buffer[] = [],
    central: Buffer[] = [];
  let offset = 0;
  for (const [name, text] of Object.entries(files)) {
    const filename = Buffer.from(name),
      data = Buffer.from(text);
    let crc = 0xffffffff;
    for (const b of data) {
      crc ^= b;
      for (let j = 0; j < 8; j++)
        crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0);
    }
    crc = (crc ^ 0xffffffff) >>> 0;
    const header = Buffer.alloc(30);
    header.writeUInt32LE(0x04034b50);
    header.writeUInt16LE(20, 4);
    header.writeUInt32LE(crc, 14);
    header.writeUInt32LE(data.length, 18);
    header.writeUInt32LE(data.length, 22);
    header.writeUInt16LE(filename.length, 26);
    locals.push(header, filename, data);
    const directory = Buffer.alloc(46);
    directory.writeUInt32LE(0x02014b50);
    directory.writeUInt16LE(20, 4);
    directory.writeUInt16LE(20, 6);
    directory.writeUInt32LE(crc, 16);
    directory.writeUInt32LE(data.length, 20);
    directory.writeUInt32LE(data.length, 24);
    directory.writeUInt16LE(filename.length, 28);
    directory.writeUInt32LE(offset, 42);
    central.push(directory, filename);
    offset += header.length + filename.length + data.length;
  }
  const table = Buffer.concat(central),
    end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50);
  end.writeUInt16LE(Object.keys(files).length, 8);
  end.writeUInt16LE(Object.keys(files).length, 10);
  end.writeUInt32LE(table.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...locals, table, end]);
}
const synthetic = () =>
  zip({
    "imsmanifest.xml": `<manifest xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:a="http://www.adlnet.org/xsd/adlcp_rootv1p2"><metadata><schemaversion>1.2</schemaversion></metadata><organizations><organization><title>Sandbox fractions</title></organization></organizations><resources><resource a:scormtype="sco" href="index.html"><file href="index.html"/><file href="lesson.json"/></resource></resources></manifest>`,
    "lesson.json": `{"question":"Equal shares"}`,
    "index.html": `<!doctype html><html><head><title>Sandbox fractions</title></head><body><h1>Sandbox fractions</h1><button id="answer">One of two equal parts</button><p id="feedback"></p><p id="checks"></p><script>
    API.LMSInitialize('');
    const checks=[];
    try {parent.document.body.textContent;checks.push('parent-accessible')}catch{checks.push('parent-blocked')}
    try {localStorage.setItem('answer','private');checks.push('storage-accessible')}catch{checks.push('storage-blocked')}
    Promise.allSettled([fetch('/api/browser/session'),fetch('https://metrics.articulate.com/v1/import'),fetch('./lesson.json').then(r=>r.json()).then(()=>checks.push('package-fetch-ok'))]).then(()=>document.getElementById('checks').textContent=checks.join(' '));
    document.getElementById('answer').onclick=()=>{API.LMSSetValue('cmi.core.score.raw','100');document.getElementById('feedback').textContent='Correct: '+API.LMSGetValue('cmi.core.student_name');};
  </script></body></html>`,
  });

async function createPackage(page: Page, buffer: Buffer, title: string) {
  await login(page, "ContentManager");
  await page.goto("/app/content");
  await page
    .getByRole("button", { name: "Create resource", exact: true })
    .click();
  await page
    .getByRole("textbox", { name: "Resource title", exact: true })
    .fill(title);
  await page.getByLabel("URL name").fill("scorm-" + randomUUID());
  await page
    .getByRole("combobox", { name: "Resource type", exact: true })
    .selectOption("interactive-activity");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Save changes", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: title, exact: true }),
  ).toBeVisible();
  const id = page.url().split("/").at(-1)!;
  await page
    .getByRole("combobox", { name: "File type", exact: true })
    .selectOption("Package");
  await page
    .locator('input[type="file"]')
    .setInputFiles({ name: "lesson.zip", mimeType: "application/zip", buffer });
  await page.getByRole("button", { name: "Upload file", exact: true }).click();
  await expect(
    page.getByRole("link", { name: "lesson.zip", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Submit for review", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Publish resource", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Confirm", exact: true })
    .click();
  await expect(
    page.getByRole("link", { name: "Open published resource", exact: true }),
  ).toBeVisible();
  return id;
}

async function approve(page: Page, id: string) {
  await login(page, "PlatformAdmin");
  await page.goto(`/app/content/${id}`);
  await page
    .getByRole("button", { name: "Inspect SCORM package", exact: true })
    .click();
  await expect(
    page.getByText("Playback approval required", { exact: true }),
  ).toBeVisible();
  await page
    .getByLabel("Review evidence", { exact: true })
    .fill(
      "Reviewed test fixture for anonymous isolated playback, curriculum, accessibility, rights and privacy regression testing.",
    );
  await page
    .getByRole("checkbox", {
      name: "I reviewed this version and approve anonymous practice only.",
      exact: true,
    })
    .check();
  await page
    .getByRole("button", { name: "Approve SCORM playback", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Confirm", exact: true })
    .click();
  await expect(
    page.getByText("Approved for isolated anonymous playback", { exact: true }),
  ).toBeVisible();
}

test("reviewed SCORM answers work with opaque-origin isolation, scoped fetch and revocation", async ({
  page,
  browser,
}) => {
  const id = await createPackage(page, synthetic(), "SCORM sandbox regression");
  const reviewer = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    const reviewerPage = await reviewer.newPage();
    await approve(reviewerPage, id);
    await login(page, "Learner");
    await page.goto(`/app/resources/${id}`);
    const outbound: string[] = [];
    page.on("request", (request) => {
      if (request.url().startsWith("https://metrics.articulate.com"))
        outbound.push(request.url());
    });
    await page
      .getByRole("button", { name: "Start interactive lesson", exact: true })
      .click();
    const frame = page.frameLocator("iframe.scorm-frame");
    await expect(
      frame.getByRole("heading", { name: "Sandbox fractions", exact: true }),
    ).toBeVisible();
    await expect(frame.locator("#checks")).toHaveText(
      "parent-blocked storage-blocked package-fetch-ok",
    );
    await frame
      .getByRole("button", { name: "One of two equal parts", exact: true })
      .click();
    await expect(frame.locator("#feedback")).toHaveText(
      "Correct: Anonymous practice",
    );
    expect(outbound).toEqual([]);
    await page
      .getByRole("button", { name: "Restart practice", exact: true })
      .click();
    await expect(frame.locator("#feedback")).toHaveText("");
    await reviewerPage
      .getByRole("button", { name: "Revoke playback approval", exact: true })
      .click();
    await reviewerPage
      .getByRole("dialog")
      .getByRole("button", { name: "Confirm", exact: true })
      .click();
    await expect(
      reviewerPage.getByText("Playback approval required", { exact: true }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Restart practice", exact: true })
      .click();
    await expect(page.getByRole("alert")).toContainText(
      "independent playback approval",
    );
  } finally {
    await reviewer.close();
  }
});

test("mapping a published resource creates a draft with the source file and explicit evidence", async ({
  page,
}) => {
  const id = await createPackage(
    page,
    synthetic(),
    "Mapping source regression",
  );
  const panel = page
    .getByRole("heading", { name: "Review curriculum mapping", exact: true })
    .locator("..");
  const grade = panel.getByRole("combobox", {
    name: "Grade and curriculum",
    exact: true,
  });
  const option = grade
    .getByRole("option")
    .filter({ hasText: "Grade 4" })
    .first();
  await expect(option).toBeAttached();
  await grade.selectOption((await option.getAttribute("value")) as string);
  await panel
    .getByLabel("Evidence for this mapping", { exact: true })
    .fill(
      "Source explicitly identifies Grade 4 in the 2023 curriculum. Topic and outcome remain unverified.",
    );
  await panel
    .getByLabel("Revision URL name", { exact: true })
    .fill("mapped-" + randomUUID());
  await panel
    .getByRole("button", { name: "Create mapping revision", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Confirm", exact: true })
    .click();
  await expect(page.getByText("Draft", { exact: true }).first()).toBeVisible();
  expect(page.url()).not.toContain(id);
  await expect(
    page.getByRole("link", { name: "lesson.zip", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText(/Source evidence: Source explicitly identifies Grade 4/),
  ).toBeVisible();
  await page.goto(`/app/content/${id}`);
  await expect(
    page.getByText("Published", { exact: true }).first(),
  ).toBeVisible();
  await expect(
    page.getByText("No curriculum connections yet.", { exact: true }),
  ).toBeVisible();
});

// Proprietary source content stays outside Git and is supplied explicitly for local acceptance.
if (process.env.MWABU_SCORM_SAMPLE) {
  test("Product Team Storyline package opens, renders and advances inside the sandbox", async ({
    page,
    browser,
  }) => {
    const id = await createPackage(
      page,
      readFileSync(process.env.MWABU_SCORM_SAMPLE!),
      "Product Team SCORM local acceptance",
    );
    const reviewer = await browser.newContext({ ignoreHTTPSErrors: true });
    try {
      await approve(await reviewer.newPage(), id);
      await page.goto(`/app/resources/${id}`);
      await page
        .getByRole("button", { name: "Start interactive lesson", exact: true })
        .click();
      const frame = page.frameLocator("iframe.scorm-frame");
      await expect(frame.locator("body")).toContainText("The adventures of", {
        timeout: 30000,
      });
      await expect(
        frame.getByRole("button", { name: /next/i }).first(),
      ).toBeVisible({ timeout: 30000 });
      // This source gates Next on narration completion. Replay supplies an audio gesture.
      await frame.getByRole("button", { name: "replay", exact: true }).click();
      await expect(async () => {
        await frame.getByRole("button", { name: "next", exact: true }).click();
        expect(await frame.locator("body").ariaSnapshot()).toMatch(
          /slide: slide [2-9]/,
        );
      }).toPass({ timeout: 20000, intervals: [1000] });
      await page.screenshot({
        path: "../artifacts/scorm-storyline-acceptance.png",
        fullPage: true,
      });
    } finally {
      await reviewer.close();
    }
  });
}
