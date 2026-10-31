"""Narrow a wrestler's torso/hip band toward a target waistband width (Art Wrangler pipeline).

Video-generated walk frames sometimes swell at the waist on some poses. This
rescales each row of the band horizontally about the centre of the waistband
(the top rows of the blue denim), so the waistband lands near a target width
in game pixels. The squeeze ramps in smoothly:

    1.0 at 20% down the figure (below the shoulders)
    -> full factor at the waistband
    -> full factor until 40% of the way down the shorts
    -> back to 1.0 at the shorts hem

Head, cap, hair, shoulders, legs and feet are untouched; arms beside the torso
move slightly inward with the body edge. Use it only for small residual width
differences (factor above ~0.85); it narrows the body but cannot remove shapes
that are drawn in, such as a belly roll. Judge the result animated, not just as
stills.

Run on background-removed, full-resolution frames BEFORE clean_sprite.py, then
clean the output as usual (clean_sprite.py redoes the outline):

    python slim.py out_dir frame1.png frame2.png ... \
        --ref-height 466 --target 54

--ref-height is the opaque height in px of the keyed reference frame used for
clean_sprite.py --scale-from (so widths convert to game px at 240 tall);
--target is the waistband width in game px. Frames already at or under the
target are copied unchanged. Requires Pillow.
"""
import argparse
import os

from PIL import Image

GAME_HEIGHT = 240


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def is_denim(p):
    r, g, b, a = p
    return a > 128 and b > r + 25 and b > 90


def measure(im):
    """Return (waistband width px, waistband top row, shorts bottom row, centre x, bbox)."""
    px = im.load()
    w, h = im.size
    bb = im.getchannel("A").getbbox()
    height = bb[3] - bb[1]
    rows = {}
    for y in range(bb[1] + int(height * .30), bb[1] + int(height * .75)):
        xs = [x for x in range(w) if is_denim(px[x, y])]
        if len(xs) > 15:
            rows[y] = (min(xs), max(xs))
    if not rows:
        raise ValueError("no denim rows found")
    top = min(rows)
    band = [(rows[y][1] - rows[y][0] + 1, rows[y]) for y in range(top, top + 12) if y in rows]
    width, span = max(band)
    return width, top, max(rows), (span[0] + span[1]) / 2, bb


def slim(im, factor, top, bottom, cx, bb):
    w, _ = im.size
    start = bb[1] + int((bb[3] - bb[1]) * 0.20)
    knee = top + int((bottom - top) * 0.4)
    out = im.copy()
    for y in range(start, bottom + 1):
        if y < top:
            f = 1 - (1 - factor) * smooth((y - start) / max(1, top - start))
        elif y <= knee:
            f = factor
        else:
            f = 1 - (1 - factor) * (1 - smooth((y - knee) / max(1, bottom - knee)))
        if f >= 0.999:
            continue
        row = im.crop((0, y, w, y + 1)).resize((max(1, round(w * f)), 1), Image.LANCZOS)
        line = Image.new("RGBA", (w, 1), (0, 0, 0, 0))
        line.paste(row, (round(cx - cx * f), 0))
        out.paste(line, (0, y))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out_dir")
    ap.add_argument("inputs", nargs="+")
    ap.add_argument("--ref-height", type=float, required=True, help="opaque height of the --scale-from frame, px")
    ap.add_argument("--target", type=float, required=True, help="target waistband width in game px")
    args = ap.parse_args()
    os.makedirs(args.out_dir, exist_ok=True)
    k = GAME_HEIGHT / args.ref_height
    for path in args.inputs:
        im = Image.open(path).convert("RGBA")
        width, top, bottom, cx, bb = measure(im)
        factor = min(1.0, args.target / (width * k))
        res = im if factor >= 0.999 else slim(im, factor, top, bottom, cx, bb)
        res.save(os.path.join(args.out_dir, os.path.basename(path)))
        print(f"{os.path.basename(path)}: waistband {width * k:.1f} game px, factor {factor:.3f}")


if __name__ == "__main__":
    main()
