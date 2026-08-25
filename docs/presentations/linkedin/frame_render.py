# -*- coding: utf-8 -*-
"""
Rendu video pur Python pour les diagrammes animes LinkedIn -- remplace la
piste PowerPoint COM/Morph abandonnee apres echec reproductible de
Presentation.CreateVideo sur ce poste.

Modele de rendu (v2) : le diagramme (boites + fleches) est FIXE et toujours
visible -- plus de "montage" de boites qui se deplacent. L'animation est un
point lumineux (pulse) qui voyage a travers les centres de boites dans
l'ordre du flux reel (donnees dans un pipeline, requete dans un circuit de
validation, etc.), avec la boite visitee qui s'illumine en synchronisation.
Boucle par construction : fondu de sortie apres le dernier arret, fondu
d'entree avant le premier -- aucun aller-retour, donc aucune ambiguite de
sens (contrairement au v1, ping-pong, ou une meme fonction de dessin
recevait tantot une progression "vers" l'etat assemble, tantot "depuis" cet
etat, ce qui faisait clignoter les fleches en fin de boucle -- v2 n'a plus
cette classe de bug par construction : les fleches ne sont plus jamais
dessinees conditionnellement).

Toutes les coordonnees passees aux scripts diagram_*.py restent en espace
"logique" 1080x1080 -- le supersampling (2x, antialiasing par downsample
LANCZOS) est entierement interne a ce module.
"""
import math
import subprocess
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps
import imageio_ffmpeg

SIZE = 1080
SS = 2  # supersampling (rendu a SIZE*SS, downsample LANCZOS -> antialiasing)
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
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def lighten(color, amount=0.15):
    return tuple(min(255, int(c + (255 - c) * amount)) for c in color)


def _vertical_gradient(w, h, top_color, bottom_color):
    base = Image.linear_gradient("L").resize((max(1, w), max(1, h)))
    return ImageOps.colorize(base, black=top_color, white=bottom_color).convert("RGB")


def box_center(box):
    return (box["x"] + box["w"] / 2, box["y"] + box["h"] / 2)


def draw_box(canvas, box, text=None, text_size=24, sub_size=18, fill=ACCENT_BG, outline=PRIMARY,
             radius_ratio=0.1, shadow=True):
    """Rendu complet (ombre + degrade + bordure + texte) -- appele UNE SEULE FOIS par boite
    dans le modele v2 (les boites sont statiques, jamais redessinees frame par frame). La
    surbrillance animee est geree separement par draw_box_glow_overlay, superposee sur un
    rendu deja en place plutot que recalculee a chaque frame (cout degrade/ombre/texte
    repaye 1x, pas ~130x par diagramme -- optimisation apres un premier rendu a 65s/diagramme)."""
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


def draw_box_glow_overlay(canvas, box, outline=PRIMARY, radius_ratio=0.1, glow=1.0):
    """Ne redessine PAS la boite (fond/degrade/texte) -- ajoute seulement le halo et
    une bordure plus vive PAR-DESSUS un rendu deja en place. Utilise pour animer la
    surbrillance d'une boite statique sans repayer le cout du degrade/ombre/texte a
    chaque frame (la boite ne bouge jamais dans le modele v2 -- seul glow varie)."""
    if glow <= 0.001:
        return
    x = int(box["x"] * SS); y = int(box["y"] * SS)
    w = max(1, int(box["w"] * SS)); h = max(1, int(box["h"] * SS))
    radius = max(4, int(min(w, h) * radius_ratio))

    gpad = int(26 * SS * glow) + 4 * SS
    glow_layer = Image.new("RGBA", (w + gpad * 2, h + gpad * 2), (0, 0, 0, 0))
    ImageDraw.Draw(glow_layer).rounded_rectangle(
        [gpad, gpad, gpad + w, gpad + h], radius=radius, fill=GOOD + (int(120 * glow),))
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(int(14 * SS * glow) + 1))
    canvas.alpha_composite(glow_layer, (x - gpad, y - gpad))

    edge_color = tuple(int(lerp(outline[c], GOOD[c], glow)) for c in range(3))
    ImageDraw.Draw(canvas).rounded_rectangle(
        [x, y, x + w - 1, y + h - 1], radius=radius, outline=edge_color, width=int((3 + 2 * glow) * SS))


def draw_arrow(canvas, p_from, p_to, color=PRIMARY, width=4):
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


def draw_arrow_down(canvas, cx, y_top, length, color=PRIMARY, width=4):
    draw_arrow(canvas, (cx, y_top), (cx, y_top + length), color=color, width=width)


def draw_centered_text(canvas, cx, cy, text, size, bold=True, color=TEXT):
    draw = canvas if isinstance(canvas, ImageDraw.ImageDraw) else ImageDraw.Draw(canvas)
    f = font(size, bold=bold)
    bbox = draw.textbbox((0, 0), text, font=f)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    draw.text((cx * SS - w / 2, cy * SS - h / 2), text, font=f, fill=color)


def draw_glow_dot(canvas, cx, cy, radius, color, intensity=1.0):
    """Point lumineux avec halo doux (cercles concentriques, opacite decroissante) --
    represente le pulse voyageant a travers le diagramme. Rendu sur une tuile locale
    (pas le canevas entier) pour rester rapide."""
    if intensity <= 0.001:
        return
    cx, cy, radius = cx * SS, cy * SS, radius * SS
    pad = int(radius * 3.5) + 2
    size = pad * 2
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for mult, alpha in [(3.0, 0.07), (2.0, 0.15), (1.3, 0.32), (1.0, 0.9)]:
        r = radius * mult
        a = int(255 * alpha * intensity)
        d.ellipse([pad - r, pad - r, pad + r, pad + r], fill=color + (a,))
    layer = layer.filter(ImageFilter.GaussianBlur(int(radius * 0.35)))
    canvas.alpha_composite(layer, (int(cx - pad), int(cy - pad)))


def _stop_targets(stop, boxes):
    """boxes visees par un arret (liste d'indices) -> liste de (index, centre)."""
    return [(i, box_center(boxes[i]["box"])) for i in stop]


def render_pulse_loop(out_path, boxes, arrows, stops, draw_static_fn=None,
                       hold_frames=22, travel_frames=16, fade_frames=12,
                       pulse_radius=13, pulse_color=GOOD):
    """
    boxes : liste de box-dicts (voir draw_box) -- position FINALE, jamais interpolee,
            dessinees a l'identique a chaque frame.
    arrows : liste de (p_from, p_to) -- dessinees a l'identique a chaque frame, jamais
             animees en apparition/disparition (source du defaut signale sur la v1).
    stops : liste ordonnee de groupes d'indices de `boxes`. Un groupe de longueur > 1
            represente un embranchement (le pulse s'y scinde, ex. Approved+Rejected) --
            visite simultanee, un pulse par cible.
    draw_static_fn(canvas) : titre/legende, dessines a l'identique a chaque frame.
    Boucle : fondu d'entree sur le 1er arret, puis pour chaque arret suivant : trajet
    (fondu croise pulse+surbrillance depuis les boites precedentes vers les suivantes)
    puis arret (pulse et surbrillance pleinement visibles). Fondu de sortie apres le
    dernier arret. Pas de retour -- le redemarrage en boucle de la video fournit deja
    le fondu d'entree suivant, sans mouvement inverse jamais rendu.
    """
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    proc = subprocess.Popen(
        [ffmpeg, "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{SIZE}x{SIZE}",
         "-r", str(FPS), "-i", "-", "-an", "-c:v", "libx264", "-pix_fmt", "yuv420p",
         "-crf", "18", out_path],
        stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE,
    )

    # Rendu du fond UNE SEULE FOIS (boites en etat normal + fleches + titre/legende) --
    # rien de tout cela ne change plus jamais d'une frame a l'autre dans le modele v2.
    base = Image.new("RGBA", (RENDER_SIZE, RENDER_SIZE), BG + (255,))
    for b in boxes:
        draw_box(base, b["box"], text=b.get("text"), text_size=b.get("text_size", 24),
                  sub_size=b.get("sub_size", 18), fill=b.get("fill", ACCENT_BG),
                  outline=b.get("outline", PRIMARY))
    for p_from, p_to in arrows:
        draw_arrow(base, p_from, p_to)
    if draw_static_fn:
        draw_static_fn(base)

    def emit(active, intensity=1.0):
        """Part d'une COPIE du fond deja rendu -- ne repaye que le halo/bordure des
        boites actuellement en surbrillance et le(s) pulse(s), jamais le degrade/ombre/
        texte (deja dans `base`)."""
        canvas = base.copy()
        for idx, g in active.get("box_glow", {}).items():
            if g > 0.001:
                draw_box_glow_overlay(canvas, boxes[idx]["box"], outline=boxes[idx].get("outline", PRIMARY), glow=g)
        for cx, cy in active.get("pulses", []):
            draw_glow_dot(canvas, cx, cy, pulse_radius, pulse_color, intensity)
        final = canvas.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
        proc.stdin.write(final.tobytes())

    def hold_state(stop, count, fade_in=None, fade_out=None):
        targets = _stop_targets(stop, boxes)
        pulses = [c for _, c in targets]
        glow = {i: 1.0 for i, _ in targets}
        for f in range(count):
            intensity = 1.0
            if fade_in is not None and f < fade_in:
                intensity = ease_in_out((f + 1) / fade_in)
            if fade_out is not None and f >= count - fade_out:
                intensity = min(intensity, ease_in_out((count - f) / fade_out))
            active_glow = {i: g * intensity for i, g in glow.items()}
            emit({"pulses": pulses, "box_glow": active_glow}, intensity)

    def travel(stop_a, stop_b, count):
        src = _stop_targets(stop_a, boxes)
        dst = _stop_targets(stop_b, boxes)
        # Nombre de sources != nombre de cibles (embranchement) -> diffuse l'unique source
        # vers chaque cible (fork), ou l'unique cible depuis chaque source (merge).
        if len(src) == 1 and len(dst) > 1:
            pairs = [(src[0][1], c) for _, c in dst]
        elif len(dst) == 1 and len(src) > 1:
            pairs = [(c, dst[0][1]) for _, c in src]
        else:
            pairs = [(s[1], d[1]) for s, d in zip(src, dst)]

        for f in range(1, count + 1):
            t = ease_in_out(f / count)
            pulses = [(lerp(a[0], b[0], t), lerp(a[1], b[1], t)) for a, b in pairs]
            glow = {i: (1.0 - t) for i, _ in src}
            for i, _ in dst:
                glow[i] = max(glow.get(i, 0.0), t)
            emit({"pulses": pulses, "box_glow": glow}, 1.0)

    n = len(stops)
    hold_state(stops[0], hold_frames, fade_in=fade_frames)
    for i in range(n - 1):
        travel(stops[i], stops[i + 1], travel_frames)
        is_last = (i + 1 == n - 1)
        hold_state(stops[i + 1], hold_frames, fade_out=fade_frames if is_last else None)

    proc.stdin.close()
    stderr = proc.stderr.read()
    proc.wait()
    if proc.returncode != 0:
        raise RuntimeError(f"ffmpeg a echoue (code {proc.returncode}):\n{stderr.decode(errors='replace')[-2000:]}")
    return out_path
