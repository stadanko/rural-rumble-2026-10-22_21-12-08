"""Build the D-049 footstep surface map for the D-048 arena backdrop.

Owner: Systems Architect (ARCHITECTURE 4.8). Presentation data only; no combat rule reads it.

The map is hand-traced: the polygons below are in native canvas px (origin top-left of the 2160x1080
`Assets/Art/Arena/arena_backdrop_large.png`, x right, row down), traced on gridded zoom crops of the art.
Colour classification of the art was tried first and rejected: the golden-hour grade makes grass and
dirt the same orange, and the puddles are mostly reflections of the trailer.

Output: `Assets/Data/Arena/ArenaSurfaceMap.png`, 540x270 (1 texel = 4x4 native px, whole canvas), each
texel pure black (Dirt), pure green (Grass) or pure blue (Puddle). Only the yard from row 720 down (the
feet band, rows 735-1040, plus margin) is marked; everything above it (trailer, porch, truck) is Dirt. Each texel takes the surface that covers
most of its 16 native px (ties go to the later layer: puddle over grass over dirt), then grass/puddle
specks under MIN_COMPONENT texels are turned back to dirt.

Usage (from any folder; paths are resolved from this file):
    python Tools/SurfaceMap/build_surface_map.py [--overlay OUT_DIR]
--overlay writes surface_map_overlay.png (whole backdrop, grass green, puddle blue, feet band outlined)
and surface_map_default_view_x2.png (the camera's default 960x540 view at the spawns, x2).

To edit the map: change the polygons, rerun, check the overlay, rerun the EditMode SurfaceMap tests
(their probe points must stay inside / outside the shapes).
"""
import argparse
import os
from collections import Counter

from PIL import Image, ImageDraw

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
BACKDROP = os.path.join(ROOT, "Assets", "Art", "Arena", "arena_backdrop_large.png")
OUTPUT = os.path.join(ROOT, "Assets", "Data", "Arena", "ArenaSurfaceMap.png")

CANVAS_W, CANVAS_H = 2160, 1080
TEXEL_PX = 4
MIN_COMPONENT = 4  # texels; smaller grass/puddle islands become dirt

DIRT, GRASS, PUDDLE = 0, 1, 2
COLOURS = {DIRT: (0, 0, 0), GRASS: (0, 255, 0), PUDDLE: (0, 0, 255)}

# Walkable feet band (ARCHITECTURE 3.1, D-048): feet point x 120-2040, rows 735-1040.
FEET_BAND = (120, 735, 2040, 1040)
# Default camera view at the spawns: centre (18, -13) world = canvas (1080, 780), 960x540.
DEFAULT_VIEW = (600, 510, 1560, 1050)

GRASS_POLYGONS = [
    # Left field and the back-edge grass strip up to the porch path (x 0-1300), above/around the left puddle,
    # the clump right of the left puddle (x 920-1060) and the tall striped grass left of the porch steps.
    [
        (0, 720), (1300, 720), (1300, 735), (1292, 770), (1296, 805), (1286, 838),
        (1240, 843), (1150, 846), (1080, 849), (1060, 862), (1000, 868), (935, 862),
        (922, 830), (918, 812), (900, 800), (868, 790), (832, 774), (808, 764),
        (700, 758), (560, 764), (500, 774), (450, 788), (400, 808), (340, 816),
        (290, 824), (276, 840), (274, 900), (250, 912), (110, 916), (96, 940),
        (100, 1000), (104, 1080), (0, 1080),
    ],
    # Right lawn with the fence-shadow stripes, behind and right of the right puddle, down the right edge.
    [
        (1562, 720), (2160, 720), (2160, 1046), (2060, 1040), (1976, 1032), (1962, 1000),
        (1974, 962), (1962, 930), (1940, 906), (1900, 896), (1820, 894), (1762, 893),
        (1722, 876), (1700, 858), (1650, 826), (1612, 790), (1580, 760), (1566, 735),
    ],
]

PUDDLE_POLYGONS = [
    # Left puddle: the big trailer reflection (lit windows at x 507-600), from its grassy left tip at x ~282
    # to the small right lobe at x ~915 that the diagonal shadow crosses.
    [
        (282, 850), (290, 835), (310, 827), (360, 822), (400, 815), (450, 795), (500, 781),
        (560, 772), (640, 768), (700, 764), (780, 766), (802, 772), (830, 790), (860, 810),
        (905, 815), (916, 830), (906, 858), (840, 862), (800, 866), (776, 880), (752, 899),
        (700, 902), (600, 898), (520, 893), (450, 890), (400, 893), (320, 893), (292, 880),
    ],
    # Middle puddle (bright reflection, about canvas (1120, 875)).
    [
        (1018, 882), (1035, 876), (1060, 870), (1095, 862), (1150, 860), (1185, 866),
        (1208, 876), (1201, 890), (1190, 898), (1045, 898), (1030, 891),
    ],
    # Right puddle (sun reflection at x 1550-1700, trailer reflection to x ~1895).
    [
        (1488, 905), (1520, 885), (1550, 866), (1600, 858), (1690, 861), (1720, 880),
        (1762, 898), (1880, 903), (1898, 915), (1898, 960), (1890, 982), (1700, 985),
        (1660, 975), (1600, 950), (1540, 930), (1500, 916),
    ],
]


def rasterize_native():
    """Native-resolution label image (mode L): 0 dirt, 1 grass, 2 puddle."""
    labels = Image.new("L", (CANVAS_W, CANVAS_H), DIRT)
    draw = ImageDraw.Draw(labels)
    for poly in GRASS_POLYGONS:
        draw.polygon(poly, fill=GRASS)
    for poly in PUDDLE_POLYGONS:
        draw.polygon(poly, fill=PUDDLE)
    return labels


def downsample(labels):
    """Majority vote per TEXEL_PX block; ties go to the higher label (puddle > grass > dirt)."""
    w, h = CANVAS_W // TEXEL_PX, CANVAS_H // TEXEL_PX
    src = labels.load()
    grid = [[DIRT] * w for _ in range(h)]
    for ty in range(h):
        for tx in range(w):
            counts = Counter(
                src[tx * TEXEL_PX + dx, ty * TEXEL_PX + dy]
                for dy in range(TEXEL_PX) for dx in range(TEXEL_PX))
            best = max(counts.items(), key=lambda kv: (kv[1], kv[0]))
            grid[ty][tx] = best[0]
    return grid


def remove_specks(grid):
    """Turn grass/puddle 4-connected components smaller than MIN_COMPONENT into dirt."""
    h, w = len(grid), len(grid[0])
    seen = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if seen[y][x] or grid[y][x] == DIRT:
                continue
            label, stack, comp = grid[y][x], [(x, y)], []
            seen[y][x] = True
            while stack:
                cx, cy = stack.pop()
                comp.append((cx, cy))
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and grid[ny][nx] == label:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            if len(comp) < MIN_COMPONENT:
                for cx, cy in comp:
                    grid[cy][cx] = DIRT
    return grid


def to_image(grid):
    h, w = len(grid), len(grid[0])
    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        for x in range(w):
            px[x, y] = COLOURS[grid[y][x]]
    return img


def write_overlay(mask, out_dir):
    art = Image.open(BACKDROP).convert("RGB")
    big = mask.resize((CANVAS_W, CANVAS_H), Image.NEAREST)
    tint = Image.new("RGB", art.size)
    tp, mp, ap = tint.load(), big.load(), art.load()
    alpha = 0.45
    for y in range(CANVAS_H):
        for x in range(CANVAS_W):
            r, g, b = ap[x, y]
            m = mp[x, y]
            if m == COLOURS[GRASS]:
                tp[x, y] = (int(r * (1 - alpha)), int(g * (1 - alpha) + 255 * alpha), int(b * (1 - alpha)))
            elif m == COLOURS[PUDDLE]:
                tp[x, y] = (int(r * (1 - alpha)), int(g * (1 - alpha) + 90 * alpha), int(b * (1 - alpha) + 255 * alpha))
            else:
                tp[x, y] = (r, g, b)
    d = ImageDraw.Draw(tint)
    x0, y0, x1, y1 = FEET_BAND
    for i in range(2):
        d.rectangle((x0 - i, y0 - i, x1 + i, y1 + i), outline=(255, 255, 255))
    vx0, vy0, vx1, vy1 = DEFAULT_VIEW
    d.rectangle((vx0, vy0, vx1 - 1, vy1 - 1), outline=(255, 0, 255))
    os.makedirs(out_dir, exist_ok=True)
    full = os.path.join(out_dir, "surface_map_overlay.png")
    tint.save(full)
    crop = tint.crop(DEFAULT_VIEW)
    crop = crop.resize((crop.width * 2, crop.height * 2), Image.NEAREST)
    view = os.path.join(out_dir, "surface_map_default_view_x2.png")
    crop.save(view)
    return full, view


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--overlay", help="folder for the review overlays")
    args = parser.parse_args()
    grid = remove_specks(downsample(rasterize_native()))
    mask = to_image(grid)
    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    mask.save(OUTPUT)
    counts = Counter(v for row in grid for v in row)
    print(f"wrote {OUTPUT} {mask.size} dirt={counts[DIRT]} grass={counts[GRASS]} puddle={counts[PUDDLE]}")
    if args.overlay:
        for p in write_overlay(mask, args.overlay):
            print("wrote", p)


if __name__ == "__main__":
    main()
