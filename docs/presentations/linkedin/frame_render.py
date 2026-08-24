# -*- coding: utf-8 -*-
"""
Rendu video pur Python pour les diagrammes animes LinkedIn -- remplace la
piste PowerPoint COM/Morph (docs/presentations/linkedin/export_video.ps1,
pptx_linkedin_helpers.py) abandonnee apres echec reproductible de
Presentation.CreateVideo sur ce poste (CreateVideoStatus=Failed en ~3s,
constant quels que soient les parametres/contenu/emplacement du fichier --
cause probable : pipeline d'encodage video Office non fonctionnel sur cette
machine, hors de portee d'une correction depuis ce script).

Pipeline : etats de diagramme definis comme donnees (boites x/y/w/h/texte) ->
interpolation (easing) entre etats consecutifs -> frames dessinees avec
Pillow -> encodage MP4 via le binaire ffmpeg fourni par imageio-ffmpeg (pip,
aucune installation systeme requise).
"""
import subprocess
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg

SIZE = 1080
FPS = 30
FONT_REGULAR = r"C:\Windows\Fonts\segoeui.ttf"
FONT_BOLD = r"C:\Windows\Fonts\segoeuib.ttf"

# ---------- Palette (identique a generate_pptx.py / frontend/app/globals.css) ----------
BG        = (0x12, 0x12, 0x16)
ACCENT_BG = (0x24, 0x22, 0x3D)
PRIMARY   = (0x7B, 0x75, 0xF0)
TEXT      = (0xF5, 0xF5, 0xF5)
MUTED     = (0xA6, 0xA6, 0xB3)
GOOD      = (0x4A, 0xDE, 0x80)
GOOD_BG   = (0x1A, 0x30, 0x1E)
WARN      = (0xF5, 0xA5, 0x24)
WARN_BG   = (0x3A, 0x1A, 0x1A)

_font_cache = {}


def font(size, bold=False):
    key = (size, bold)
    if key not in _font_cache:
        _font_cache[key] = ImageFont.truetype(FONT_BOLD if bold else FONT_REGULAR, size)
    return _font_cache[key]


def ease_in_out(t):
    """Smoothstep -- demarre et finit en douceur plutot qu'une interpolation lineaire brute."""
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def lerp_box(a, b, t):
    return {k: lerp(a[k], b[k], t) for k in ("x", "y", "w", "h")}


def draw_box(draw, box, text=None, text_size=24, sub_size=18, fill=ACCENT_BG, outline=PRIMARY, radius_ratio=0.1):
    x, y, w, h = box["x"], box["y"], box["w"], box["h"]
    radius = max(4, int(min(w, h) * radius_ratio))
    draw.rounded_rectangle([x, y, x + w, y + h], radius=radius, fill=fill, outline=outline, width=3)
    if text:
        lines = text.split("\n")
        f_main = font(text_size, bold=True)
        f_sub = font(sub_size, bold=False)
        heights = []
        for i, line in enumerate(lines):
            f = f_main if i == 0 else f_sub
            bbox = draw.textbbox((0, 0), line, font=f)
            heights.append((line, f, bbox[3] - bbox[1]))
        total_h = sum(hh for _, _, hh in heights) + (len(lines) - 1) * 6
        cy = y + h / 2 - total_h / 2
        for line, f, lh in heights:
            bbox = draw.textbbox((0, 0), line, font=f)
            lw = bbox[2] - bbox[0]
            color = TEXT if f is f_main else MUTED
            draw.text((x + w / 2 - lw / 2, cy), line, font=f, fill=color)
            cy += lh + 6


def draw_arrow_down(draw, cx, y_top, length, color=PRIMARY, width=4):
    draw_arrow(draw, (cx, y_top), (cx, y_top + length), color=color, width=width)


def draw_arrow(draw, p_from, p_to, color=PRIMARY, width=4):
    """Fleche generique entre deux points, tete orientee dans l'axe du segment."""
    import math
    x0, y0 = p_from
    x1, y1 = p_to
    draw.line([p_from, p_to], fill=color, width=width)
    angle = math.atan2(y1 - y0, x1 - x0)
    tip = 11
    spread = math.radians(28)
    for sign in (1, -1):
        a = angle + math.pi - sign * spread
        draw.line([p_to, (x1 + tip * math.cos(a), y1 + tip * math.sin(a))], fill=color, width=width)


def draw_centered_text(draw, cx, cy, text, size, bold=True, color=TEXT):
    f = font(size, bold=bold)
    bbox = draw.textbbox((0, 0), text, font=f)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    draw.text((cx - w / 2, cy - h / 2), text, font=f, fill=color)


def render_video(out_path, segments, hold_frames=30, transition_frames=30):
    """
    segments : liste de (state, draw_extra_fn) representant les etats cles ; state
    est une liste de box-dicts ({"box": {x,y,w,h}, "text": ..., ...}), meme longueur
    et meme ordre d'un segment a l'autre (l'interpolation zippe positionnellement).
    Chaque etat est tenu `hold_frames` images, puis `transition_frames` images
    interpolees (easing) le relient au suivant. `draw_extra_fn(draw, t)` dessine les
    elements non-interpoles (titre, fleches, legende) pour la progression t (0..1,
    valant 1.0 pendant un hold).
    """
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    proc = subprocess.Popen(
        [ffmpeg, "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{SIZE}x{SIZE}",
         "-r", str(FPS), "-i", "-", "-an", "-c:v", "libx264", "-pix_fmt", "yuv420p",
         "-crf", "18", out_path],
        stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE,
    )

    def emit_frame(boxes, draw_extra_fn, t):
        img = Image.new("RGB", (SIZE, SIZE), BG)
        draw = ImageDraw.Draw(img)
        for box in boxes:
            draw_box(draw, box["box"], text=box.get("text"), text_size=box.get("text_size", 24),
                      sub_size=box.get("sub_size", 18), fill=box.get("fill", ACCENT_BG),
                      outline=box.get("outline", PRIMARY))
        if draw_extra_fn:
            draw_extra_fn(draw, t)
        proc.stdin.write(img.tobytes())

    n = len(segments)
    for i, (state, extra) in enumerate(segments):
        for _ in range(hold_frames):
            emit_frame(state, extra, 1.0)
        if i < n - 1:
            next_state, next_extra = segments[i + 1]
            for f in range(1, transition_frames + 1):
                t = ease_in_out(f / transition_frames)
                boxes = [
                    {**a, "box": lerp_box(a["box"], b["box"], t)}
                    for a, b in zip(state, next_state)
                ]
                emit_frame(boxes, extra, t)

    proc.stdin.close()
    stderr = proc.stderr.read()
    proc.wait()
    if proc.returncode != 0:
        raise RuntimeError(f"ffmpeg a echoue (code {proc.returncode}):\n{stderr.decode(errors='replace')[-2000:]}")
    return out_path
