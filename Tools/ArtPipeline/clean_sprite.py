"""Pixel cleanup for AI-generated sprites (Art Wrangler pipeline, D-010/D-013).

Turns high-resolution generator output into game-size pixel art:
crop -> box downscale -> color reduction (gently snapped toward the Style Guide
palette, skin exempt) -> hard alpha -> 1px dark outline -> feet on bottom row.

Single sprite (fit to a height):
    python clean_sprite.py in.png out.png --height 240

Animation frames (same scale and colors as an approved base sprite, packed
into one sheet with a shared baseline):
    python clean_sprite.py f1.png f2.png ... --sheet out.png \
        --scale-from base_source.png --height 240 --palette-from base_clean.png

--scale-from is the full-resolution source the base sprite was made from; the
frames are scaled by the same factor so the character stays the same size.
Requires Pillow.
"""
import argparse

from PIL import Image, ImageDraw, ImageFilter

STYLE_PALETTE = [tuple(bytes.fromhex(h)) for h in (
    "10151A 17243A 25372C 35523A 657A3B 795037 A74727 D26938 E19A36 "
    "FFD65A 2872B8 7435A8 C7352C E6DFCC").split()]
OUTLINE = STYLE_PALETTE[0]
ALPHA_CROP = 64


def opaque_bbox(im):
    return im.getchannel("A").point(lambda a: 255 if a > ALPHA_CROP else 0).getbbox()


def downscale(im, factor):
    im = im.crop(opaque_bbox(im))
    size = (max(1, round(im.width * factor)), max(1, round(im.height * factor)))
    small = im.resize(size, Image.BOX)
    alpha = small.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    rgb = Image.new("RGB", small.size)
    rgb.paste(small.convert("RGB"), mask=alpha)
    return rgb, alpha


def drop_islands(alpha, min_size):
    """Clear opaque specks not connected to the main body (motion-blur leftovers)."""
    a = alpha.load()
    w, h = alpha.size
    seen = bytearray(w * h)
    for y0 in range(h):
        for x0 in range(w):
            if seen[y0 * w + x0] or not a[x0, y0]:
                continue
            region, stack = [], [(x0, y0)]
            seen[y0 * w + x0] = 1
            while stack:
                x, y = stack.pop()
                region.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx] and a[nx, ny]:
                        seen[ny * w + nx] = 1
                        stack.append((nx, ny))
            if len(region) < min_size:
                for x, y in region:
                    a[x, y] = 0
    return alpha


def adaptive_palette(rgb, colors, snap_dist):
    """Octree palette for one image; colors near a Style Guide color snap to it."""
    q = rgb.quantize(colors=colors, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)
    raw = q.getpalette()[:colors * 3]
    out = []
    for i in range(0, len(raw), 3):
        c = tuple(raw[i:i + 3])
        best = min(STYLE_PALETTE, key=lambda p: sum((a - b) ** 2 for a, b in zip(c, p)))
        dist = sum((a - b) ** 2 for a, b in zip(c, best)) ** 0.5
        out.append(best if dist <= snap_dist else c)  # far colors (skin) stay as-is
    return out


def palette_from_image(path):
    im = Image.open(path).convert("RGBA")
    return sorted({p[:3] for p in im.get_flattened_data() if p[3]})


def apply_palette(rgb, alpha, colors):
    pimg = Image.new("P", (1, 1))
    flat = [v for c in colors[:256] for v in c]
    pimg.putpalette(flat + [0] * (768 - len(flat)))
    res = rgb.quantize(palette=pimg, dither=Image.Dither.NONE).convert("RGBA")
    res.putalpha(alpha)
    return res


def outline(res, alpha):
    px, a = res.load(), alpha.load()
    w, h = res.size
    edge = [(x, y) for y in range(h) for x in range(w) if a[x, y] and any(
        not (0 <= x + dx < w and 0 <= y + dy < h) or not a[x + dx, y + dy]
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in edge:
        px[x, y] = OUTLINE + (255,)
    return res


def clean(im, factor, colors=None, n_colors=32, snap_dist=25, min_island=24):
    rgb, alpha = downscale(im, factor)
    alpha = drop_islands(alpha, min_island)
    pal = colors or adaptive_palette(rgb, n_colors, snap_dist)
    return outline(apply_palette(rgb, alpha, pal), alpha)


def pack_sheet(frames, margin=2):
    """Frames in one row, each cell the same size, feet on the bottom row, centered."""
    cw = max(f.width for f in frames) + margin * 2
    cw += cw % 2
    ch = max(f.height for f in frames)
    sheet = Image.new("RGBA", (cw * len(frames), ch), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * cw + (cw - f.width) // 2, ch - f.height))
    return sheet, (cw, ch)


def preview(img, path, scale=4, bg=(101, 122, 59)):
    big = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    canvas = Image.new("RGB", (big.width + 20, big.height + 20), bg)
    canvas.paste(big, (10, 10), big)
    canvas.save(path)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("inputs", nargs="+")
    ap.add_argument("--height", type=int, required=True, help="game-size standing height in px (Barry 240, Peaux 215)")
    ap.add_argument("--scale-from", help="full-res source of the base sprite; frames use its scale factor")
    ap.add_argument("--palette-from", help="approved base sprite whose colors every frame is mapped to")
    ap.add_argument("--colors", type=int, default=32)
    ap.add_argument("--snap", type=float, default=25, help="max RGB distance to snap toward the Style Guide palette")
    ap.add_argument("--sheet", help="pack all inputs into this sheet instead of writing single files")
    ap.add_argument("--preview", help="also write a 4x preview here")
    args = ap.parse_args()

    if not args.sheet and len(args.inputs) != 2:
        ap.error("single mode takes exactly: input.png output.png")
    sources = args.inputs if args.sheet else args.inputs[:1]
    images = [Image.open(p).convert("RGBA") for p in sources]
    ref =Image.open(args.scale_from).convert("RGBA") if args.scale_from else None
    colors = palette_from_image(args.palette_from) if args.palette_from else None

    def factor_for(im):
        base = ref or im
        bbox = opaque_bbox(base)
        return args.height / (bbox[3] - bbox[1])

    if args.sheet:
        frames = [clean(im, factor_for(im), colors, args.colors, args.snap) for im in images]
        sheet, cell = pack_sheet(frames)
        sheet.save(args.sheet)
        print(f"sheet {sheet.size}, {len(frames)} frames, cell {cell}")
        if args.preview:
            preview(sheet, args.preview)
        return

    im, out = images[0], args.inputs[1]
    res = clean(im, factor_for(im), colors, args.colors, args.snap)
    sheet, _ = pack_sheet([res])
    sheet.save(out)
    print(f"sprite {sheet.size}")
    if args.preview:
        preview(sheet, args.preview)


if __name__ == "__main__":
    main()
