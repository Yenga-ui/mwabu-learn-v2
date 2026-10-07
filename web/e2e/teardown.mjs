// Await disposal before Playwright terminates the harness process on Windows.
export default async function teardown() {
  const response = await fetch("http://127.0.0.1:7444/shutdown", {
    method: "POST",
  });
  if (!response.ok || (await response.text()) !== "stopped")
    throw new Error("Isolated browser database cleanup did not complete.");
}
