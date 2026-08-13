# -*- coding: utf-8 -*-
"""icon_factory.py - Fabrique d'icônes vectorielles unifiées (Agent 2).

Chaque icône est definie par une liste de primitives geometriques (DSL),
rendue en SVG (vectoriel - regle 45) ET en PNG transparent (embed python-pptx).
Trait uniforme (meme epaisseur) sur toutes les icones.
"""
from __future__ import annotations

import math
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:  # pragma: no cover
    Image = None

SIZE = 256
STROKE = 22
ROUND = "round"
ACCENT_BLEU = "#659ED4"
ACCENT_ORANGE = "#B55927"
NEUTRE_BLANC = "#FFFFFF"

# ----------------------------------------------------------------------
# Primitives : ("kind", dict, fill_bool)
#   line    : {"x1","y1","x2","y2"}
#   poly    : {"pts": [(x,y)...], "closed": bool}
#   rect    : {"x","y","w","h","r"}
#   ellipse : {"box": (x1,y1,x2,y2)}
#   arc     : {"box": (x1,y1,x2,y2), "start": deg, "end": deg}
#   circle  : {"cx","cy","r"}  (rempli si fill)
# ----------------------------------------------------------------------


def _arc_pts(box, start, end, n=48):
    x1, y1, x2, y2 = box
    cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
    rx, ry = (x2 - x1) / 2, (y2 - y1) / 2
    pts = []
    for i in range(n + 1):
        a = math.radians(start + (end - start) * i / n)
        pts.append((cx + rx * math.cos(a), cy + ry * math.sin(a)))
    return pts


# ----------------------------------------------------------------------
# Definitions des icones
# ----------------------------------------------------------------------
def icon_db():
    return [
        ("ellipse", {"box": (58, 74, 198, 130)}, False),
        ("line", {"x1": 58, "y1": 102, "x2": 58, "y2": 184}, False),
        ("line", {"x1": 198, "y1": 102, "x2": 198, "y2": 184}, False),
        ("arc", {"box": (58, 128, 198, 240), "start": 0, "end": 180}, False),
    ]


def icon_server():
    return [
        ("rect", {"x": 38, "y": 56, "w": 180, "h": 144, "r": 14}, False),
        ("line", {"x1": 62, "y1": 104, "x2": 194, "y2": 104}, False),
        ("line", {"x1": 62, "y1": 152, "x2": 194, "y2": 152}, False),
        ("circle", {"cx": 78, "cy": 104, "r": 9}, True),
        ("circle", {"cx": 78, "cy": 152, "r": 9}, True),
    ]


def icon_api():
    return [
        ("poly", {"pts": [(70, 70), (30, 128), (70, 186)], "closed": False}, False),
        ("poly", {"pts": [(186, 70), (226, 128), (186, 186)], "closed": False}, False),
        ("line", {"x1": 150, "y1": 60, "x2": 106, "y2": 196}, False),
    ]


def icon_chip():
    return [
        ("rect", {"x": 56, "y": 56, "w": 144, "h": 144, "r": 14}, False),
        ("rect", {"x": 88, "y": 88, "w": 80, "h": 80, "r": 8}, False),
        ("line", {"x1": 96, "y1": 30, "x2": 96, "y2": 56}, False),
        ("line", {"x1": 128, "y1": 30, "x2": 128, "y2": 56}, False),
        ("line", {"x1": 160, "y1": 30, "x2": 160, "y2": 56}, False),
        ("line", {"x1": 96, "y1": 200, "x2": 96, "y2": 226}, False),
        ("line", {"x1": 128, "y1": 200, "x2": 128, "y2": 226}, False),
        ("line", {"x1": 160, "y1": 200, "x2": 160, "y2": 226}, False),
        ("line", {"x1": 30, "y1": 96, "x2": 56, "y2": 96}, False),
        ("line", {"x1": 30, "y1": 128, "x2": 56, "y2": 128}, False),
        ("line", {"x1": 30, "y1": 160, "x2": 56, "y2": 160}, False),
        ("line", {"x1": 200, "y1": 96, "x2": 226, "y2": 96}, False),
        ("line", {"x1": 200, "y1": 128, "x2": 226, "y2": 128}, False),
        ("line", {"x1": 200, "y1": 160, "x2": 226, "y2": 160}, False),
    ]


def icon_lock():
    return [
        ("arc", {"box": (96, 32, 160, 96), "start": 180, "end": 360}, False),
        ("rect", {"x": 64, "y": 88, "w": 128, "h": 112, "r": 12}, False),
        ("circle", {"cx": 128, "cy": 140, "r": 16}, False),
        ("line", {"x1": 128, "y1": 152, "x2": 128, "y2": 176}, False),
    ]


def icon_shield():
    return [
        ("poly", {"pts": [(64, 64), (192, 64), (192, 128), (128, 208), (64, 128)], "closed": True}, False),
        ("poly", {"pts": [(92, 128), (118, 154), (168, 102)], "closed": False}, False),
    ]


def icon_chat():
    return [
        ("rect", {"x": 40, "y": 48, "w": 176, "h": 120, "r": 28}, False),
        ("poly", {"pts": [(72, 168), (56, 208), (104, 168)], "closed": True}, False),
        ("circle", {"cx": 96, "cy": 108, "r": 10}, True),
        ("circle", {"cx": 128, "cy": 108, "r": 10}, True),
        ("circle", {"cx": 160, "cy": 108, "r": 10}, True),
    ]


def icon_users():
    return [
        ("ellipse", {"box": (58, 54, 110, 106)}, False),
        ("arc", {"box": (44, 112, 124, 192), "start": 0, "end": 180}, False),
        ("ellipse", {"box": (146, 54, 198, 106)}, False),
        ("arc", {"box": (132, 112, 212, 192), "start": 0, "end": 180}, False),
    ]


def icon_check():
    return [
        ("ellipse", {"box": (40, 40, 216, 216)}, False),
        ("poly", {"pts": [(76, 132), (112, 168), (180, 92)], "closed": False}, False),
    ]


def icon_warning():
    return [
        ("poly", {"pts": [(128, 44), (224, 204), (32, 204)], "closed": True}, False),
        ("line", {"x1": 128, "y1": 104, "x2": 128, "y2": 156}, False),
        ("circle", {"cx": 128, "cy": 182, "r": 10}, True),
    ]


def icon_bolt():
    return [
        ("poly", {"pts": [(132, 36), (72, 140), (116, 140), (124, 220), (184, 116), (140, 116)], "closed": True}, False),
    ]


def icon_layers():
    return [
        ("poly", {"pts": [(40, 96), (128, 56), (216, 96), (128, 136)], "closed": True}, False),
        ("poly", {"pts": [(40, 140), (128, 100), (216, 140), (128, 180)], "closed": True}, False),
        ("poly", {"pts": [(40, 184), (128, 144), (216, 184), (128, 224)], "closed": True}, False),
    ]


ICONS = {
    "db": icon_db,
    "server": icon_server,
    "api": icon_api,
    "ai": icon_chip,
    "lock": icon_lock,
    "shield": icon_shield,
    "chat": icon_chat,
    "users": icon_users,
    "check": icon_check,
    "warning": icon_warning,
    "bolt": icon_bolt,
    "layers": icon_layers,
}


# ----------------------------------------------------------------------
# Rendus
# ----------------------------------------------------------------------
def _svg_path(pts):
    d = "M " + " L ".join(f"{x:.1f} {y:.1f}" for x, y in pts)
    return d + " Z"


def render_svg(prims, color: str, name: str) -> str:
    lines = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {SIZE} {SIZE}" '
        f'width="{SIZE}" height="{SIZE}">',
        f'  <g fill="none" stroke="{color}" stroke-width="{STROKE}" '
        f'stroke-linecap="{ROUND}" stroke-linejoin="{ROUND}">',
    ]
    for kind, p, fill in prims:
        if kind == "line":
            lines.append(f'    <line x1="{p["x1"]}" y1="{p["y1"]}" x2="{p["x2"]}" y2="{p["y2"]}"/>')
        elif kind == "poly":
            d = _svg_path(p["pts"])
            lines.append(f'    <path d="{d}" fill="none"/>' if not p["closed"] else f'    <path d="{d}"/>')
        elif kind == "rect":
            r = p.get("r") or 0
            lines.append(f'    <rect x="{p["x"]}" y="{p["y"]}" width="{p["w"]}" height="{p["h"]}" rx="{r}"/>')
        elif kind == "ellipse":
            x1, y1, x2, y2 = p["box"]
            lines.append(f'    <ellipse cx="{(x1 + x2) / 2:.1f}" cy="{(y1 + y2) / 2:.1f}" '
                         f'rx="{(x2 - x1) / 2:.1f}" ry="{(y2 - y1) / 2:.1f}"/>')
        elif kind == "arc":
            d = _svg_path(_arc_pts(p["box"], p["start"], p["end"]))
            lines.append(f'    <path d="{d}"/>')
        elif kind == "circle":
            fill_attr = f' fill="{color}"' if fill else ""
            lines.append(f'    <circle cx="{p["cx"]}" cy="{p["cy"]}" r="{p["r"]}"{fill_attr}/>')
    lines.append("  </g>")
    lines.append("</svg>")
    return "\n".join(lines)


def render_png(prims, color: str, size: int = SIZE) -> "Image.Image":
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for kind, p, fill in prims:
        if kind == "line":
            d.line((p["x1"], p["y1"], p["x2"], p["y2"]), fill=color, width=STROKE)
        elif kind == "poly":
            d.line(p["pts"], fill=color, width=STROKE, joint="curve")
            if p["closed"]:
                d.line((p["pts"][-1], p["pts"][0]), fill=color, width=STROKE)
        elif kind == "rect":
            r = p.get("r") or 0
            if r:
                d.rounded_rectangle((p["x"], p["y"], p["x"] + p["w"], p["y"] + p["h"]),
                                    radius=r, outline=color, width=STROKE)
            else:
                d.rectangle((p["x"], p["y"], p["x"] + p["w"], p["y"] + p["h"]),
                            outline=color, width=STROKE)
        elif kind == "ellipse":
            d.ellipse(p["box"], outline=color, width=STROKE)
        elif kind == "arc":
            d.arc(p["box"], p["start"], p["end"], fill=color, width=STROKE)
        elif kind == "circle":
            x, y, r = p["cx"], p["cy"], p["r"]
            box = (x - r, y - r, x + r, y + r)
            if fill:
                d.ellipse(box, fill=color)
            else:
                d.ellipse(box, outline=color, width=STROKE)
    return img


def generate_icons(out_dir: Path, color: str = ACCENT_BLEU) -> list[dict]:
    out_dir.mkdir(parents=True, exist_ok=True)
    produced = []
    for name, factory in ICONS.items():
        prims = factory()
        svg = render_svg(prims, color, name)
        svg_path = out_dir / f"{name}.svg"
        svg_path.write_text(svg, encoding="utf-8")
        img = render_png(prims, color)
        png_path = out_dir / f"{name}.png"
        img.save(png_path)
        produced.append({"name": name, "svg": str(svg_path.name), "png": str(png_path.name),
                         "couleur": color})
    return produced
