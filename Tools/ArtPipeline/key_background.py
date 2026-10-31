"""Remove a flat background from generated frames (Art Wrangler pipeline).

Video-generated sprite frames come back on a flat gray background with a soft
floor shadow. This flood-fills from the image border: pixels close to the
background color, or neutral-gray shadow pixels touching it, become
transparent. The character's dark outline stops the fill, so light colors
inside the character (white socks) are kept. A second pass clears enclosed
pockets (1000+ px, sized for ~1440 px source frames) that match the background
color almost exactly.

    python key_background.py out_dir frame1.png frame2.png ...

Use the Unity AI background removal instead when the background is not flat.
Requires Pillow.
"""
import argparse
import os
from collections import deque

from PIL import Image


def key(im, tol=10, shadow_sat=14, shadow_min=45, hole_tol=6, min_hole=1000):
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()
    corners = [px[0, 0], px[w - 1, 0], px[0, h - 1], px[w - 1, h - 1]]
    bg = tuple(sorted(c[i] for c in corners)[1] for i in range(3))

    def is_bg(c):
        r, g, b = c[:3]
        if abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2]) <= tol:
            return True
        # Floor shadow: neutral gray, darker than the background but not outline-black.
        # Never lighter than the background: light grays are shading on white
        # clothing (cap front, socks), which the fill can reach through outline gaps.
        return max(r, g, b) - min(r, g, b) <= shadow_sat and shadow_min <= r < bg[0] - 4

    seen = bytearray(w * h)
    queue = deque()
    for x in range(w):
        queue.extend(((x, 0), (x, h - 1)))
    for y in range(h):
        queue.extend(((0, y), (w - 1, y)))
    while queue:
        x, y = queue.popleft()
        i = y * w + x
        if seen[i]:
            continue
        seen[i] = 1
        if not is_bg(px[x, y]):
            continue
        px[x, y] = (0, 0, 0, 0)
        if x > 0: queue.append((x - 1, y))
        if x < w - 1: queue.append((x + 1, y))
        if y > 0: queue.append((x, y - 1))
        if y < h - 1: queue.append((x, y + 1))
    # Background pockets the border fill can't reach (e.g. between an arm and the
    # body): connected regions of near-exact background color. Small regions are
    # shading inside the character (sock folds, highlights) and are kept.
    def is_hole(x, y):
        r, g, b, a = px[x, y]
        return a and abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2]) <= hole_tol

    seen = bytearray(w * h)
    for y0 in range(h):
        for x0 in range(w):
            if seen[y0 * w + x0] or not is_hole(x0, y0):
                continue
            region, queue = [], deque([(x0, y0)])
            seen[y0 * w + x0] = 1
            while queue:
                x, y = queue.popleft()
                region.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx] and is_hole(nx, ny):
                        seen[ny * w + nx] = 1
                        queue.append((nx, ny))
            if len(region) >= min_hole:
                for x, y in region:
                    px[x, y] = (0, 0, 0, 0)
    return im


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out_dir")
    ap.add_argument("inputs", nargs="+")
    ap.add_argument("--tol", type=int, default=10, help="max summed RGB distance from the background color")
    args = ap.parse_args()
    os.makedirs(args.out_dir, exist_ok=True)
    for p in args.inputs:
        out = os.path.join(args.out_dir, os.path.basename(p))
        key(Image.open(p), args.tol).save(out)
        print(out)


if __name__ == "__main__":
    main()
