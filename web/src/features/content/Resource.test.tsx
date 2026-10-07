import { render, screen, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { MediaViewer } from "./Resource";
import type { Asset } from "../../api/types";
const asset = (mimeType: string): Asset => ({
  id: "asset",
  fileName: "learning-media",
  mimeType,
  fileSizeBytes: 200,
  assetType: "Audio",
  checksum: "checksum",
  isPrimary: true,
  viewUrl: "/api/learning/content/resource/assets/asset/view",
  downloadUrl: "/api/learning/content/resource/assets/asset/download",
});
describe("authenticated media viewers", () => {
  it("audio exposes native play, pause, seek and volume controls without autoplay", () => {
    const { container } = render(<MediaViewer asset={asset("audio/mpeg")} />);
    const audio = container.querySelector("audio")!;
    expect(audio.controls).toBe(true);
    expect(audio.autoplay).toBe(false);
    expect(audio.preload).toBe("none");
    audio.currentTime = 12;
    const pause = vi.spyOn(audio, "pause").mockImplementation(() => {});
    fireEvent.click(screen.getByRole("button", { name: "Restart audio" }));
    expect(pause).toHaveBeenCalledOnce();
    expect(audio.currentTime).toBe(0);
  });
  it("shows a retryable error when media cannot load", () => {
    const { container } = render(<MediaViewer asset={asset("audio/ogg")} />);
    fireEvent.error(container.querySelector("audio")!);
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Unable to open this file",
    );
    expect(screen.getByRole("button", { name: /Try again/ })).toBeVisible();
  });
  it("video does not autoplay and executable formats have no inline viewer", () => {
    const { container, rerender } = render(
      <MediaViewer asset={asset("video/mp4")} />,
    );
    expect(container.querySelector("video")!.autoplay).toBe(false);
    rerender(<MediaViewer asset={asset("text/html")} />);
    expect(container.querySelector("iframe")).toBeNull();
  });
});
