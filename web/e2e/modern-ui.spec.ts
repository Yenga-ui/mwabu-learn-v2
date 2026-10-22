import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
for (const size of [
  { width: 390, height: 844 },
  { width: 768, height: 1024 },
  { width: 1366, height: 768 },
]) {
  test(`modern teacher workspace fits ${size.width}px with labelled controls and preserved navigation`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize(size);
    const account = JSON.parse(readFileSync(".local/e2e/fixture.json", "utf8"))
      .roles.Teacher;
    await page.goto("/login");
    await page.getByLabel("Email address").fill(account.email);
    await page.getByLabel(/^Password/).fill(account.password);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(
      page.getByRole("heading", {
        name: "What would you like to teach?",
        exact: true,
      }),
    ).toBeVisible();
    const contrast = await page
      .locator(".sidebar nav a.active")
      .evaluate((element) => {
        const style = getComputedStyle(element);
        const luminance = (colour: string) => {
          const channels = colour
            .match(/\d+(?:\.\d+)?/g)!
            .slice(0, 3)
            .map(Number)
            .map((value) => {
              const channel = value / 255;
              return channel <= 0.04045
                ? channel / 12.92
                : ((channel + 0.055) / 1.055) ** 2.4;
            });
          return (
            channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722
          );
        };
        const text = luminance(style.color),
          background = luminance(style.backgroundColor);
        return (
          (Math.max(text, background) + 0.05) /
          (Math.min(text, background) + 0.05)
        );
      });
    expect(contrast).toBeGreaterThanOrEqual(4.5);
    for (const [name, url, heading] of [
      ["home", "/app", "What would you like to teach?"],
      ["curriculum", "/app/curriculum", "Curriculum"],
      ["discovery", "/app/library", "What would you like to teach?"],
      [
        "ai-create",
        "/app/lessons/new?planning=topics&assist=ai",
        "Create a lesson with AI",
      ],
      ["my-plans", "/app/lessons?mine=true", "My lesson plans"],
    ]) {
      await page.goto(url);
      await expect(
        page.getByRole("heading", { name: heading, exact: true }).first(),
      ).toBeVisible();
      await expect(page.locator("#main")).not.toContainText(
        "Loading your workspace…",
      );
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
      ).toBe(true);
      await page.screenshot({
        path: testInfo.outputPath(`${name}.png`),
        fullPage: true,
      });
    }
    if (size.width > 1000) {
      await page
        .getByRole("button", { name: "Hide navigation labels" })
        .click();
      await expect(page.locator(".app-shell")).toHaveClass(/sidebar-collapsed/);
      await page.reload();
      await expect(page.locator(".app-shell")).toHaveClass(/sidebar-collapsed/);
      await page.getByRole("link", { name: "Curriculum", exact: true }).click();
      await expect(
        page.getByRole("heading", { name: "Curriculum", exact: true }),
      ).toBeVisible();
    } else {
      await page.getByRole("button", { name: "Open navigation" }).click();
      await page.keyboard.press("Escape");
      await expect(
        page.getByRole("button", { name: "Open navigation" }),
      ).toBeFocused();
      await page.getByRole("button", { name: "Open navigation" }).click();
      await expect(
        page.getByRole("navigation", { name: "Main navigation" }),
      ).toBeVisible();
      await page.getByRole("link", { name: "Curriculum", exact: true }).click();
      await expect(
        page.getByRole("heading", { name: "Curriculum", exact: true }),
      ).toBeVisible();
      await expect(page.locator(".sidebar")).not.toHaveClass(/open/);
    }
    await page.getByRole("button", { name: "Grade 4", exact: true }).click();
    await page
      .getByRole("button", { name: "Mathematics", exact: true })
      .click();
    await page
      .getByLabel("Term / teaching sequence")
      .selectOption({ label: "Term 1" });
    await page
      .getByRole("button", { name: "Fractions in everyday life", exact: true })
      .click();
    await expect(
      page.getByRole("heading", {
        name: "Represent one half using familiar objects",
        exact: true,
      }),
    ).toBeVisible();
    await page
      .getByRole("link", { name: "Find linked lesson plans →", exact: true })
      .click();
    await expect(page).toHaveURL(/nodeType=LearningOutcome/);
  });
}
