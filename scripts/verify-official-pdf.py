"""Review aid: python verify-official-pdf.py REFERENCE NORMAL OVERFLOW RENDER_DIR.

Requires pdfplumber, Pillow and Poppler's pdftoppm. Does not modify the PDFs.
Checks the original template geometry/text and rendered pixels outside input cells.
"""
import collections
import json
import math
import pathlib
import re
import subprocess
import sys

import pdfplumber
from PIL import Image, ImageChops, ImageDraw

reference, normal, overflow, destination = map(pathlib.Path, sys.argv[1:])
destination.mkdir(parents=True, exist_ok=True)
cells = [(165, 137, 352, 22), (190, 167, 327, 22), (190, 197, 102, 22),
         (416, 197, 101, 22), (190, 227, 102, 22), (416, 227, 101, 22),
         (190, 256, 102, 22), (416, 256, 101, 22), (190, 334, 327, 11),
         (190, 350, 102, 22), (416, 350, 101, 22), (190, 468, 102, 21),
         (303, 468, 214, 21), (190, 497, 327, 22), (190, 527, 327, 22),
         (190, 557, 327, 22), (190, 586, 327, 36), (190, 686, 102, 36),
         (190, 730, 327, 22)]
checks = [(278, 382 + i * 14.65, 6, 6) for i in range(5)] + [(259, 632, 6, 6), (261, 645.2, 6, 6), (273.5, 658.5, 6, 6)]
progression = [(x, 133 + i * 29.75, w, 22) for i in range(7) for x, w in [(190, 102), (303, 102), (416, 101)]] + [(78, 405, 439, 22)]

def geometry(page):
    return collections.Counter((o["object_type"], *(round(o[k], 2) for k in ("x0", "top", "x1", "bottom"))) for o in page.lines + page.rects + page.curves)

def characters(page):
    return collections.Counter((c["text"], round(c["x0"], 2), round(c["top"], 2)) for c in page.chars)

with pdfplumber.open(reference) as source, pdfplumber.open(normal) as result, pdfplumber.open(overflow) as long:
    assert len(source.pages) == len(result.pages) == 2
    assert 2 < len(long.pages) < 20
    for output in (result, long):
        for p, original in zip(output.pages, source.pages):
            assert (p.width, p.height) == (original.width, original.height)
            assert not (geometry(original) - geometry(p)), "Original rules/artwork moved or removed"
            assert not (characters(original) - characters(p)), "Original labels moved or removed"
    text = "\n".join(p.extract_text() or "" for p in result.pages)
    for value in ["Fractions", "Presentation School", "Chipo Teacher", "20/10/2026", "Paper circles", "Equal parts", "Fold paper.", "Learners explained equal shares."]:
        assert re.sub(r"\s", "", value) in re.sub(r"\s", "", text), value
    long_text = "\n".join(p.extract_text() or "" for p in long.pages)
    assert "See continuation" in long_text
    assert "END-OF-REFERENCES" in long_text and "END-OF-ACTIVITIES" in long_text
    assert long_text.count("café materials supported discussion.") == 40
    assert "PRIVATE" not in text and "revision" not in text.lower()

for name, path in [("reference", reference), ("normal", normal), ("overflow", overflow)]:
    subprocess.run(["pdftoppm", "-scale-to", "1100", "-png", str(path), str(destination / name)], check=True)
pixel_report = []
for index, allowed in enumerate([cells + checks, progression], 1):
    original = Image.open(destination / f"reference-{index}.png").convert("RGB")
    generated = Image.open(destination / f"normal-{index}.png").convert("RGB")
    delta = ImageChops.difference(original, generated)
    draw = ImageDraw.Draw(delta)
    sx, sy = original.width / 595.25, original.height / 842
    for x, y, w, h in allowed:
        draw.rectangle((math.floor((x-1)*sx), math.floor((y-2)*sy), math.ceil((x+w+1)*sx), math.ceil((y+h+2)*sy)), fill="black")
    changed = sum(max(pixel) > 8 for pixel in delta.get_flattened_data())
    assert changed == 0, f"Page {index}: {changed} changed pixels outside value cells"
    pixel_report.append({"page": index, "changed_pixels_outside_values": changed})
print(json.dumps({"template_geometry_and_labels": "preserved", "normal_pages": 2, "overflow_end_markers": "preserved", "pixel_comparison": pixel_report}, indent=2))
