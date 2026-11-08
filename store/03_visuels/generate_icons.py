#!/usr/bin/env python3
"""
Generates every KeepLid image from the master SVG (store/03_visuels/keeplid.svg):

  * MSIX visual assets (all scale-* and targetsize-* variants)  -> store/03_visuels/icones/msix/
                                                                 and src/KeepLid.Package/Images/
  * Store listing logos (300x300, 1080x1080, 2160x2160)         -> store/03_visuels/icones/store/
  * Application icon (multi-size .ico)                          -> src/KeepLid.App/Assets/KeepLid.ico
  * Tray icons for the 4 states (multi-size .ico)               -> src/KeepLid.App/Assets/Tray/*.ico

Usage (from the repository root):
    pip install cairosvg pillow
    python store/03_visuels/generate_icons.py

The script is deterministic: re-run it after editing the SVG and commit the outputs.
"""
from __future__ import annotations

import copy
import io
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

import cairosvg
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "store" / "03_visuels" / "keeplid.svg"
OUT = ROOT / "store" / "03_visuels" / "icones"
PACKAGE_IMAGES = ROOT / "src" / "KeepLid.Package" / "Images"
APP_ASSETS = ROOT / "src" / "KeepLid.App" / "Assets"

NS = "http://www.w3.org/2000/svg"
ET.register_namespace("", NS)

SCALES = {100: 1.0, 125: 1.25, 150: 1.5, 200: 2.0, 400: 4.0}

# MSIX logo name -> base size (width, height) at scale-100
MSIX_LOGOS = {
    "Square44x44Logo": (44, 44),
    "Square71x71Logo": (71, 71),
    "Square150x150Logo": (150, 150),
    "Square310x310Logo": (310, 310),
    "Wide310x150Logo": (310, 150),
    "StoreLogo": (50, 50),
    "SplashScreen": (620, 300),
}
TARGET_SIZES = [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256]
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 256]
TRAY_SIZES = [16, 20, 24, 32, 40, 48, 64]

# Tray states: (screen colour, inner colour, show spark, show exclamation, show lid badge)
TRAY_STATES = {
    "idle": ("#8A8F98", "#8A8F98", False, False, False),
    "active": ("#2F7CF6", "#1D3A8A", True, False, False),
    "active-lid": ("#2F7CF6", "#1D3A8A", True, False, True),
    "guard": ("#F59E0B", "#B45309", False, True, False),
}


def load_master() -> ET.ElementTree:
    return ET.parse(MASTER)


def find(tree: ET.ElementTree, element_id: str) -> ET.Element:
    for el in tree.iter():
        if el.get("id") == element_id:
            return el
    raise KeyError(element_id)


def variant(*, background: bool, screen: str | None = None, inner: str | None = None,
            spark: bool = True, exclamation: bool = False, badge: bool = False,
            base: str | None = None) -> bytes:
    """Returns SVG bytes for one variant of the master."""
    tree = copy.deepcopy(load_master())
    find(tree, "background").set("display", "inline" if background else "none")
    if screen:
        find(tree, "screen").set("fill", screen)
    if base:
        find(tree, "base").set("fill", base)
    if inner:
        find(tree, "screen-inner").set("fill", inner)
    find(tree, "spark").set("display", "inline" if spark else "none")
    find(tree, "exclamation").set("display", "inline" if exclamation else "none")
    find(tree, "badge-lid").set("display", "inline" if badge else "none")
    buf = io.BytesIO()
    tree.write(buf, xml_declaration=True, encoding="utf-8")
    return buf.getvalue()


def render(svg: bytes, size: int) -> Image.Image:
    png = cairosvg.svg2png(bytestring=svg, output_width=size, output_height=size)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def render_on_canvas(svg: bytes, width: int, height: int, mark_ratio: float) -> Image.Image:
    """Centers the square mark on a transparent canvas (wide tiles, splash screen)."""
    side = max(1, int(round(min(width, height) * mark_ratio)))
    mark = render(svg, side)
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    canvas.paste(mark, ((width - side) // 2, (height - side) // 2), mark)
    return canvas


def save_ico(images: dict[int, Image.Image], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    sizes = sorted(images)
    largest = images[sizes[-1]]
    largest.save(path, format="ICO", sizes=[(s, s) for s in sizes],
                 append_images=[images[s] for s in sizes[:-1]])


def main() -> None:
    msix_dir = OUT / "msix"
    store_dir = OUT / "store"
    for d in (msix_dir, store_dir):
        if d.exists():
            shutil.rmtree(d)
        d.mkdir(parents=True)

    plated = variant(background=True)      # tile: white laptop on blue gradient
    unplated = variant(background=False, screen="#2F7CF6", base="#2F7CF6", inner="#1D3A8A")  # taskbar / Start

    # --- MSIX scale-* assets -------------------------------------------------------------------------
    for name, (w, h) in MSIX_LOGOS.items():
        for scale, factor in SCALES.items():
            pw, ph = int(round(w * factor)), int(round(h * factor))
            if w == h and name != "SplashScreen":
                img = render(plated if name != "Square44x44Logo" else unplated, pw)
            else:
                img = render_on_canvas(unplated, pw, ph, mark_ratio=0.8)
            img.save(msix_dir / f"{name}.scale-{scale}.png")

    # --- Square44x44 target sizes (taskbar, Start, Explorer), plated and unplated --------------------
    for size in TARGET_SIZES:
        render(unplated, size).save(msix_dir / f"Square44x44Logo.targetsize-{size}.png")
        render(unplated, size).save(msix_dir / f"Square44x44Logo.targetsize-{size}_altform-unplated.png")
        render(unplated, size).save(msix_dir / f"Square44x44Logo.altform-lightunplated_targetsize-{size}.png")

    # --- Store listing logos ---------------------------------------------------------------------------
    for size in (300, 1080, 2160):
        render(plated, size).save(store_dir / f"KeepLid-StoreLogo-{size}x{size}.png")

    # --- Application .ico ------------------------------------------------------------------------------
    save_ico({s: render(unplated, s) for s in ICO_SIZES}, APP_ASSETS / "KeepLid.ico")

    # --- Tray icons ------------------------------------------------------------------------------------
    for state, (screen, inner, spark, excl, badge) in TRAY_STATES.items():
        svg = variant(background=False, screen=screen, base=screen, inner=inner,
                      spark=spark, exclamation=excl, badge=badge)
        images = {s: render(svg, s) for s in TRAY_SIZES}
        save_ico(images, APP_ASSETS / "Tray" / f"{state}.ico")
        images[256 if 256 in images else 64].save(OUT / f"tray-{state}-64.png")

    # --- Copy MSIX assets into the packaging project ---------------------------------------------------
    if PACKAGE_IMAGES.exists():
        shutil.rmtree(PACKAGE_IMAGES)
    shutil.copytree(msix_dir, PACKAGE_IMAGES)

    count = len(list(msix_dir.iterdir()))
    print(f"{count} MSIX assets, 3 store logos, app icon and {len(TRAY_STATES)} tray icons generated.")


if __name__ == "__main__":
    main()
