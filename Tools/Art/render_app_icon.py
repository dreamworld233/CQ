"""Render the small, owned SVG subset used by CQ's temporary icon (Pillow only)."""
import argparse
import re
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageColor, ImageDraw

SCALE = 2
SIZE = 1024 * SCALE
NS = '{http://www.w3.org/2000/svg}'


def path_points(data):
    tokens = re.findall(r'[MCLZ]|-?\d+(?:\.\d+)?', data)
    points = []
    i = 0
    while i < len(tokens):
        command = tokens[i]
        i += 1
        if command in ('M', 'L'):
            points.append((float(tokens[i]) * SCALE, float(tokens[i + 1]) * SCALE))
            i += 2
        elif command == 'C':
            p0 = points[-1]
            p1, p2, p3 = [(float(tokens[i + j]) * SCALE, float(tokens[i + j + 1]) * SCALE) for j in (0, 2, 4)]
            for step in range(1, 65):
                t = step / 64
                u = 1 - t
                points.append(tuple(u**3 * p0[k] + 3 * u*u*t * p1[k] + 3 * u*t*t * p2[k] + t**3 * p3[k] for k in (0, 1)))
            i += 6
        elif command == 'Z':
            points.append(points[0])
        else:
            raise ValueError('Unsupported SVG command: ' + command)
    return points


def paint(canvas, mask, value, gradients):
    if value == 'none':
        return
    if not value.startswith('url('):
        canvas.paste(ImageColor.getrgb(value) + (255,), (0, 0), mask)
        return
    top, bottom = gradients[value[5:-1]]
    bounds = mask.getbbox()
    if bounds is None:
        return
    start, end = bounds[1], bounds[3]
    layer = Image.new('RGBA', canvas.size)
    draw = ImageDraw.Draw(layer)
    for y in range(start, end):
        t = (y - start) / max(1, end - start - 1)
        color = tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,)
        draw.line((0, y, SIZE, y), fill=color)
    canvas.paste(layer, (0, 0), mask)


def render_group(group, gradients):
    image = Image.new('RGBA', (SIZE, SIZE))
    for node in group:
        kind = node.tag.removeprefix(NS)
        mask = Image.new('L', image.size)
        draw = ImageDraw.Draw(mask)
        if kind == 'rect':
            draw.rectangle((0, 0, SIZE, SIZE), fill=255)
        elif kind == 'path':
            points = path_points(node.attrib['d'])
            draw.polygon(points, fill=255)
        else:
            raise ValueError('Unsupported SVG shape: ' + kind)
        paint(image, mask, node.get('fill', '#000000'), gradients)
        if node.get('stroke'):
            mask = Image.new('L', image.size)
            draw = ImageDraw.Draw(mask)
            width = round(float(node.get('stroke-width', '1')) * SCALE)
            draw.line(points, fill=255, width=width, joint='curve')
            if node.get('stroke-linecap') == 'round':
                radius = width / 2
                for x, y in (points[0], points[-1]):
                    draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=255)
            paint(image, mask, node.get('stroke'), gradients)
    return image.resize((1024, 1024), Image.Resampling.LANCZOS)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--preview', type=Path, required=True)
    args = parser.parse_args()
    root = ET.parse(args.source).getroot()
    gradients = {}
    for gradient in root.findall('.//' + NS + 'linearGradient'):
        gradients[gradient.get('id')] = tuple(ImageColor.getrgb(stop.get('stop-color')) for stop in gradient)
    groups = {g.get('id'): g for g in root.findall(NS + 'g')}
    background = render_group(groups['background-layer'], gradients)
    foreground = render_group(groups['foreground-layer'], gradients)
    full = Image.alpha_composite(background, foreground)
    # Adaptive launchers reserve the outer 18/108 on each side for masks and motion.
    adaptive = Image.new('RGBA', (1024, 1024))
    inset = foreground.resize((920, 920), Image.Resampling.LANCZOS)
    adaptive.paste(inset, (52, 52))
    args.output.mkdir(parents=True, exist_ok=True)
    full.convert('RGB').save(args.output / 'AppIcon.png')
    background.convert('RGB').save(args.output / 'AppIconBackground.png')
    adaptive.save(args.output / 'AppIconForeground.png')

    preview = Image.new('RGB', (960, 490), '#eef0f3')
    draw = ImageDraw.Draw(preview)
    examples = [('Default / legacy', full), ('Round launcher', full), ('Adaptive safe area', Image.alpha_composite(background, adaptive))]
    for index, (label, icon) in enumerate(examples):
        tile = icon.resize((256, 256), Image.Resampling.LANCZOS)
        mask = Image.new('L', tile.size)
        shape = ImageDraw.Draw(mask)
        if index == 1:
            shape.ellipse((0, 0, 255, 255), fill=255)
        else:
            shape.rounded_rectangle((0, 0, 255, 255), radius=58, fill=255)
        x = 32 + index * 320
        preview.paste(tile, (x, 36), mask)
        draw.text((x, 310), label, fill='#263449')
        for j, size in enumerate((96, 48, 32)):
            small = tile.resize((size, size), Image.Resampling.LANCZOS)
            small_mask = mask.resize((size, size), Image.Resampling.LANCZOS)
            preview.paste(small, (x + j * 104, 348), small_mask)
    args.preview.parent.mkdir(parents=True, exist_ok=True)
    preview.save(args.preview)
    print('Rendered 3 icon textures at 1024x1024 and launcher preview:', args.preview)


if __name__ == '__main__':
    main()
