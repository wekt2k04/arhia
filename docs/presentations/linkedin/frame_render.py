# -*- coding: utf-8 -*-
"""
Rendu video pur Python pour les diagrammes animes LinkedIn -- remplace la
piste PowerPoint COM/Morph abandonnee apres echec reproductible de
Presentation.CreateVideo sur ce poste (CreateVideoStatus=Failed en ~3s,
constant quels que soient les parametres/contenu/emplacement du fichier --
cause probable : pipeline d'encodage video Office non fonctionnel sur cette
machine, hors de portee d'une correction depuis ce script).

Pipeline : etats de diagramme definis comme donnees (boites x/y/w/h/texte) ->
interpolation (easing) entre etats consecutifs, boucle parfaite optionnelle
(aller-retour) -> frames dessinees avec Pillow (rendu 2x supersample +
ombres portees + degrades, downsample final pour l'antialiasing) ->
encodage MP4 via le binaire ffmpeg fourni par imageio-ffmpeg (pip, aucune
installation systeme requise).

Toutes les coordonnees passees aux fonctions draw_* (par les scripts
diagram_*.py) restent en espace "logique" 1080x1080 -- le supersampling est
entierement interne a ce module, aucun script appelant n'a a en tenir compte.
"""
import math
import subprocess
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps
import imageio_ffmpeg

SIZE = 1080
SS = 2  # facteur de supersampling (rendu a SIZE*SS, downsample en LANCZOS -> antialiasing)
RENDER_SIZE = SIZE * SS
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
    px = max(1, int(size * SS))
    key = (px, bold)
    if key not in _font_cache:
        _font_cache[key] = ImageFont.truetype(FONT_BOLD if bold else FONT_REGULAR, px)
    return _font_cache[key]


def ease_in_out(t):
    """Smoothstep -- demarre et finit en douceur plutot qu'une interpolation lineaire brute."""
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def lerp_box(a, b, t):
    return {k: lerp(a[k], b[k], t) for k in ("x", "y", "w", "h")}


def lighten(color, amount=0.15):
    return tuple(min(255, int(c + (255 - c) * amount)) for c in color)


def darken(color, amount=0.15):
    return tuple(max(0, int(c * (1 - amount))) for c in color)


def _vertical_gradient(w, h, top_color, bottom_color):
    """Degrade rapide (C-accelere) : linear_gradient("L") va de 0 (haut) a 255 (bas)."""
    base = Image.linear_gradient("L").resize((max(1, w), max(1, h)))
    return ImageOps.colorize(base, black=top_color, white=bottom_color).convert("RGB")


def draw_box(canvas, box, text=None, text_size=24, sub_size=18, fill=ACCENT_BG, outline=PRIMARY,
             radius_ratio=0.1, shadow=True):
    """canvas : Image RGBA sur laquelle on compose (pas un ImageDraw -- necessaire pour
    alpha-blender l'ombre). Coordonnees de `box` en espace logique 1080x1080."""
    x = int(box["x"] * SS); y = int(box["y"] * SS)
    w = max(1, int(box["w"] * SS)); h = max(1, int(box["h"] * SS))
    radius = max(4, int(min(w, h) * radius_ratio))

    if shadow:
        pad = 20 * SS
        shadow_layer = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
        ImageDraw.Draw(shadow_layer).rounded_rectangle(
            [pad, pad + 5 * SS, pad + w, pad + 5 * SS + h], radius=radius, fill=(0, 0, 0, 100))
        shadow_layer = shadow_layer.filter(ImageFilter.GaussianBlur(9 * SS))
        canvas.alpha_composite(shadow_layer, (x - pad, y - pad))

    grad = _vertical_gradient(w, h, lighten(fill, 0.10), fill)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1], radius=radius, fill=255)
    canvas.paste(grad, (x, y), mask)

    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle([x, y, x + w - 1, y + h - 1], radius=radius, outline=outline, width=3 * SS)

    if text:
        lines = text.split("\n")
        f_main = font(text_size, bold=True)
        f_sub = font(sub_size, bold=False)
        heights = []
        for i, line in enumerate(lines):
            f = f_main if i == 0 else f_sub
            bbox = draw.textbbox((0, 0), line, font=f)
            heights.append((line, f, bbox[3] - bbox[1]))
        total_h = sum(hh for _, _, hh in heights) + (len(lines) - 1) * 6 * SS
        cy = y + h / 2 - total_h / 2
        for line, f, lh in heights:
            bbox = draw.textbbox((0, 0), line, font=f)
            lw = bbox[2] - bbox[0]
            color = TEXT if f is f_main else MUTED
            draw.text((x + w / 2 - lw / 2, cy), line, font=f, fill=color)
            cy += lh + 6 * SS


def draw_arrow_down(canvas, cx, y_top, length, color=PRIMARY, width=4):
    draw_arrow(canvas, (cx, y_top), (cx, y_top + length), color=color, width=width)


def draw_arrow(canvas, p_from, p_to, color=PRIMARY, width=4):
    """Fleche generique entre deux points, tete orientee dans l'axe du segment.
    `canvas` : Image RGBA (coherent avec draw_box) ou ImageDraw -- les deux acceptes."""
    draw = canvas if isinstance(canvas, ImageDraw.ImageDraw) else ImageDraw.Draw(canvas)
    x0, y0 = p_from[0] * SS, p_from[1] * SS
    x1, y1 = p_to[0] * SS, p_to[1] * SS
    w = width * SS
    draw.line([(x0, y0), (x1, y1)], fill=color, width=w)
    angle = math.atan2(y1 - y0, x1 - x0)
    tip = 11 * SS
    spread = math.radians(28)
    for sign in (1, -1):
        a = angle + math.pi - sign * spread
        draw.line([(x1, y1), (x1 + tip * math.cos(a), y1 + tip * math.sin(a))], fill=color, width=w)


def draw_centered_text(canvas, cx, cy, text, size, bold=True, color=TEXT):
    draw = canvas if isinstance(canvas, ImageDraw.ImageDraw) else ImageDraw.Draw(canvas)
    f = font(size, bold=bold)
    bbox = draw.textbbox((0, 0), text, font=f)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    draw.text((cx * SS - w / 2, cy * SS - h / 2), text, font=f, fill=color)


def render_video(out_path, segments, hold_frames=30, transition_frames=30, loop=False):
    """
    segments : liste de (state, draw_extra_fn) representant les etats cles ; state
    est une liste de box-dicts ({"box": {x,y,w,h}, "text": ..., ...}), meme longueur
    et meme ordre d'un segment a l'autre (l'interpolation zippe positionnellement).
    Chaque etat est tenu `hold_frames` images, puis `transition_frames` images
    interpolees (easing) le relient au suivant. `draw_extra_fn(canvas, t)` dessine les
    elements non-interpoles (titre, fleches, legende) pour la progression t (0..1,
    valant 1.0 pendant un hold).

    loop=True : boucle parfaite -- apres le dernier etat, rejoue la sequence en sens
    inverse (meme easing) jusqu'au premier etat SANS le retenir a la fin (le redemarrage
    de la video, en lecture en boucle, joue deja ce hold) -- pas de coupure nette au
    rebouclage.
    """
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    proc = subprocess.Popen(
        [ffmpeg, "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{SIZE}x{SIZE}",
         "-r", str(FPS), "-i", "-", "-an", "-c:v", "libx264", "-pix_fmt", "yuv420p",
         "-crf", "18", out_path],
        stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE,
    )

    def render_frame_bytes(boxes, draw_extra_fn, t):
        canvas = Image.new("RGBA", (RENDER_SIZE, RENDER_SIZE), BG + (255,))
        for box in boxes:
            draw_box(canvas, box["box"], text=box.get("text"), text_size=box.get("text_size", 24),
                      sub_size=box.get("sub_size", 18), fill=box.get("fill", ACCENT_BG),
                      outline=box.get("outline", PRIMARY))
        if draw_extra_fn:
            draw_extra_fn(canvas, t)
        final = canvas.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
        return final.tobytes()

    def emit_hold(state, extra, count):
        # Le contenu d'un hold est identique frame apres frame -- rendu une seule fois.
        frame_bytes = render_frame_bytes(state, extra, 1.0)
        for _ in range(count):
            proc.stdin.write(frame_bytes)

    def emit_transition(state_a, extra_a, state_b, count):
        for f in range(1, count + 1):
            t = ease_in_out(f / count)
            boxes = [{**a, "box": lerp_box(a["box"], b["box"], t)} for a, b in zip(state_a, state_b)]
            proc.stdin.write(render_frame_bytes(boxes, extra_a, t))

    n = len(segments)
    forward = list(range(n))
    visit_order = forward + forward[-2::-1] if (loop and n > 1) else forward
    last_pos = len(visit_order) - 1

    for pos, i in enumerate(visit_order):
        state, extra = segments[i]
        skip_hold = loop and n > 1 and pos == last_pos
        if not skip_hold:
            emit_hold(state, extra, hold_frames)
        if pos < last_pos:
            next_state, next_extra = segments[visit_order[pos + 1]]
            emit_transition(state, extra, next_state, transition_frames)

    proc.stdin.close()
    stderr = proc.stderr.read()
    proc.wait()
    if proc.returncode != 0:
        raise RuntimeError(f"ffmpeg a echoue (code {proc.returncode}):\n{stderr.decode(errors='replace')[-2000:]}")
    return out_path
