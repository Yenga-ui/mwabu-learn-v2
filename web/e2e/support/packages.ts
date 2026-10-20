import { expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { randomUUID } from "node:crypto";

export async function login(page: Page, role: string) {
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
export function zip(files: Record<string, string>) {
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
export const synthetic = () =>
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

export async function createPackage(page: Page, buffer: Buffer, title: string) {
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

export async function approve(page: Page, id: string) {
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
