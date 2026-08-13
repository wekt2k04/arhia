# -*- coding: utf-8 -*-
"""Agent 3 : L'Architecte python-pptx — VERSION 3 (design keynote Vercel/OpenAI).

Design system v3 (vision utilisateur) :
  - Dark sophistique #0B132B (jamais noir/blanc pur), glassmorphism + mesh gradient.
  - Inter (titre ExtraBold 800 / corps Regular 300-400), JetBrains Mono (code, ligatures).
  - Titres Assertion-Evidence, layout asymetrique 1/3 - 2/3, cartes Bento.
  - Data storytelling (KPI 80-100pt), editeur code One Dark Pro, mockups + spotlight.
  - Diagrammes redessines en vectoriel (blocs arrondis, code couleur, ombres douces).
  - Logos symetriques garde/cloture ; filigrane monochrome en pied.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from agent_base import AgentBase

try:
    from pptx import Presentation
    from pptx.util import Inches, Pt, Emu
    from pptx.dml.color import RGBColor
    from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
    from pptx.enum.shapes import MSO_SHAPE
    from pptx.oxml.ns import qn
except ImportError:  # pragma: no cover
    Presentation = None

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

# ----------------------------------------------------------------------
# Charte v3
# ----------------------------------------------------------------------
BG = RGBColor(0x0B, 0x13, 0x2B)
BG_DEEP = RGBColor(0x0A, 0x0F, 0x1C)
SURFACE = RGBColor(0x11, 0x1B, 0x33)
SURFACE2 = RGBColor(0x16, 0x21, 0x3F)
BORDER = RGBColor(0x23, 0x32, 0x52)
NAVY1 = RGBColor(0x12, 0x36, 0x80)
NAVY2 = RGBColor(0x2C, 0x4C, 0x90)
ACCENT = RGBColor(0x65, 0x9E, 0xD4)
ACCENT_TXT = RGBColor(0x8F, 0xC1, 0xF0)
ALERT = RGBColor(0xB5, 0x59, 0x27)
ALERT_TXT = RGBColor(0xE5, 0x9B, 0x5F)
TEXT = RGBColor(0xE6, 0xED, 0xF7)
TEXT_SEC = RGBColor(0x93, 0xA0, 0xBF)
TEXT_MUTED = RGBColor(0x5B, 0x6B, 0x8C)

CODE_BG = RGBColor(0x0D, 0x11, 0x17)
CODE_HDR = RGBColor(0x21, 0x25, 0x2B)
CODE_KW = RGBColor(0xC7, 0x92, 0xEA)
CODE_STR = RGBColor(0x89, 0xDD, 0xFF)
CODE_NUM = RGBColor(0xF7, 0x8C, 0x6C)
CODE_CMT = RGBColor(0x63, 0x70, 0x80)
CODE_TYPE = RGBColor(0xFF, 0xCB, 0x6B)
CODE_FN = RGBColor(0x82, 0xAA, 0xFF)
CODE_TXT = RGBColor(0xEE, 0xFF, 0xFF)

FONT = "Inter"
MONO = "JetBrains Mono"
SLIDE_W, SLIDE_H = 13.333, 7.5
GUT = 0.75

KEYWORDS = {
    "var", "return", "if", "else", "await", "new", "using", "public", "private",
    "class", "static", "async", "select", "from", "where", "order", "by", "top",
    "create", "unique", "index", "on", "and", "as", "not", "null", "int", "string",
    "void", "bool", "get", "set", "namespace", "foreach", "in", "is", "throw",
}
_SYNTAX_RE = re.compile(r'("[^"]*"|\'[^\']*\')|(\d+(?:\.\d+)?)|([A-Za-z_][A-Za-z0-9_]*)|([^\w\s])')


class ArchitecteAgent(AgentBase):
    agent_id = "agent_architecte"
    name = "L'Architecte python-pptx v3"

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.manifest = {}
        self.icons = {}
        self.page_no = 0
        self.total = 26

    # ------------------------------------------------------------------
    # Primitives bas niveau
    # ------------------------------------------------------------------
    @staticmethod
    def _run(p, text, size, color, bold=False, font=FONT, spc=None, weight=None):
        r = p.add_run()
        r.text = text
        r.font.size = Pt(size)
        r.font.color.rgb = color
        r.font.bold = bold
        r.font.name = font
        rpr = r._r.get_or_add_rPr()
        if spc:
            rpr.set("spc", str(spc))
        if weight:
            rpr.set("w", str(weight))
        return r

    def _txtbox(self, slide, x, y, w, h, name="corps"):
        tb = slide.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
        tf = tb.text_frame
        tf.word_wrap = True
        tf.margin_left = tf.margin_right = Emu(0)
        tf.margin_top = tf.margin_bottom = Emu(0)
        tb.name = name
        return tf

    def _rect(self, slide, x, y, w, h, fill, line=None, name="forme", radius=0.10):
        shape = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE,
                                       Inches(x), Inches(y), Inches(w), Inches(h))
        shape.name = name
        shape.adjustments[0] = radius
        shape.fill.solid()
        shape.fill.fore_color.rgb = fill
        if line is None:
            shape.line.fill.background()
        else:
            shape.line.color.rgb = line
            shape.line.width = Pt(1.25)
        shape.shadow.inherit = False
        return shape

    @staticmethod
    def _alpha(shape, pct: int):
        """pct = transparence en % (ex: 78 = 78% transparent)."""
        amt = 100 - pct  # opacite restante
        el = shape._element
        tag = el.tag.rsplit("}", 1)[-1]
        if tag == "pic":
            blip = el.find(qn("a:blip")) if el.find(qn("a:blip")) is not None else None
            blip = blip or el.find(".//" + qn("a:blip"))
            if blip is None:
                return
            for old in blip.findall(qn("a:alphaModFix")):
                blip.remove(old)
            amf = blip.makeelement(qn("a:alphaModFix"), {"amt": str(int(amt * 1000))})
            blip.append(amf)
            return
        sf = shape.fill._xPr.find(qn("a:solidFill")) if shape.fill._xPr is not None else None
        if sf is not None:
            clr = sf.find(qn("a:srgbClr"))
            if clr is not None:
                a = clr.makeelement(qn("a:alpha"), {"val": str(int(amt * 1000))})
                clr.append(a)

    def _blob(self, slide, x, y, w, h, color, trans=85, line=False):
        shp = slide.shapes.add_shape(MSO_SHAPE.OVAL, Inches(x), Inches(y), Inches(w), Inches(h))
        shp.name = "decor"
        if line:
            shp.fill.background()
            shp.line.color.rgb = color
            shp.line.width = Pt(2)
        else:
            shp.fill.solid()
            shp.fill.fore_color.rgb = color
            shp.line.fill.background()
        shp.shadow.inherit = False
        if not line:
            self._alpha(shp, trans)
        return shp

    def _mesh_backdrop(self, slide, deep=False):
        base = BG_DEEP if deep else BG
        rect = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SLIDE_W, SLIDE_H)
        rect.name = "fond"
        rect.fill.solid()
        rect.fill.fore_color.rgb = base
        rect.line.fill.background()
        rect.shadow.inherit = False
        # mesh gradient : blobs alpha (bleus institutionnels + ocre discret)
        self._blob(slide, -1.5, -1.2, 6.5, 6.5, NAVY1, 82)
        self._blob(slide, 9.5, -2.0, 6.0, 6.0, NAVY2, 84)
        self._blob(slide, 10.2, 4.5, 5.0, 5.0, ACCENT, 90)
        self._blob(slide, -2.0, 4.8, 5.5, 5.5, ALERT, 94)

    def _h_line(self, slide, x, y, w, color, h=0.025):
        ln = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(x), Inches(y), Inches(w), Inches(h))
        ln.name = "decor"
        ln.fill.solid()
        ln.fill.fore_color.rgb = color
        ln.line.fill.background()
        ln.shadow.inherit = False
        return ln

    # ------------------------------------------------------------------
    # Chrome v3
    # ------------------------------------------------------------------
    def _chrome(self, slide, kicker, title):
        if kicker:
            ktf = self._txtbox(slide, GUT, 0.5, 10.0, 0.32, name="breadcrumb")
            kp = ktf.paragraphs[0]
            self._run(kp, kicker.upper(), 13, ACCENT_TXT, weight=600, spc=250)
        ttf = self._txtbox(slide, GUT, 0.82, 11.6, 0.9, name="titre")
        tp = ttf.paragraphs[0]
        self._run(tp, title, 38, TEXT, weight=800)
        # filigrane logo en pied (discret)
        self._watermark(slide)

    def _watermark(self, slide):
        logo = self.manifest.get("logos", {}).get("entreprise", {})
        path = self.root / logo["path"] if logo.get("path") else None
        if path and path.exists():
            from PIL import Image
            with Image.open(path) as im:
                w_i, h_i = im.size
            h = 0.34
            w = h * w_i / h_i
            pic = slide.shapes.add_picture(str(path), Inches(GUT), Inches(SLIDE_H - GUT - h),
                                           Inches(w), Inches(h))
            pic.name = "decor"
            self._alpha(pic, 78)

    def _page_number(self, slide):
        pn = self._txtbox(slide, SLIDE_W - GUT - 1.6, SLIDE_H - GUT - 0.26, 1.6, 0.26, name="pagenum")
        pp = pn.paragraphs[0]
        pp.alignment = PP_ALIGN.RIGHT
        self._run(pp, f"{self.page_no:02d} / {self.total}", 11, TEXT_MUTED)

    def _icon(self, slide, name, x, y, size=0.34):
        icon = self.icons.get(name, {}).get("png")
        path = self.root / icon if icon else None
        if path and path.exists():
            return slide.shapes.add_picture(str(path), Inches(x), Inches(y),
                                            Inches(size), Inches(size))
        return None

    # ------------------------------------------------------------------
    # Carte Bento
    # ------------------------------------------------------------------
    def _card(self, slide, x, y, w, h, icon, title, text, accent=None):
        card = self._rect(slide, x, y, w, h, SURFACE, BORDER, name="carte", radius=0.12)
        if accent:
            self._h_line(slide, x + 0.12, y, w - 0.24, accent, 0.03)
        self._icon(slide, icon, x + 0.16, y + 0.16, 0.3)
        ttf = self._txtbox(slide, x + 0.16, y + 0.52, w - 0.32, 0.32, name="carte_t")
        tp = ttf.paragraphs[0]
        self._run(tp, title, 15, TEXT, weight=700)
        dtf = self._txtbox(slide, x + 0.16, y + 0.84, w - 0.32, h - 0.95, name="carte_d")
        dp = dtf.paragraphs[0]
        self._run(dp, text, 13, TEXT_SEC)
        return card

    # ------------------------------------------------------------------
    # Editeur code One Dark Pro (regle : 5-10 lignes, boilerplate grise)
    # ------------------------------------------------------------------
    def _syntax(self, line: str):
        if line.strip().startswith("//") or line.strip().startswith("--"):
            return [(line, CODE_CMT)]
        tokens = []
        for m in _SYNTAX_RE.finditer(line):
            txt = m.group(0)
            if m.group(1):
                tokens.append((txt, CODE_STR))
            elif m.group(2):
                tokens.append((txt, CODE_NUM))
            elif m.group(3):
                low = txt.lower()
                if low in KEYWORDS:
                    tokens.append((txt, CODE_KW))
                elif low in ("await",):
                    tokens.append((txt, CODE_KW))
                else:
                    tokens.append((txt, CODE_TXT))
            else:
                tokens.append((txt, CODE_TXT))
        return tokens

    def _editor(self, slide, x, y, w, code, size=16):
        lines = code.get("lines", [])
        header_h = 0.5
        body_h = 0.24 + len(lines) * 0.34
        panel_h = header_h + body_h + 0.05
        panel = self._rect(slide, x, y, w, panel_h, CODE_BG, BORDER, name="code", radius=0.06)
        hdr = self._rect(slide, x, y, w, header_h, CODE_HDR, name="code_header", radius=0.10)
        for i, (dx, col) in enumerate([(0.35, ALERT), (0.66, ACCENT), (0.97, TEXT_MUTED)]):
            dot = slide.shapes.add_shape(MSO_SHAPE.OVAL, Inches(x + dx), Inches(y + 0.17),
                                         Inches(0.13), Inches(0.13))
            dot.name = "decor"
            dot.fill.solid()
            dot.fill.fore_color.rgb = col
            dot.line.fill.background()
        ftf = self._txtbox(slide, x + 1.5, y + 0.12, w - 2.4, 0.28, name="kicker")
        fp = ftf.paragraphs[0]
        self._run(fp, code.get("file", ""), 12, TEXT_SEC, font=MONO)
        body = self._rect(slide, x + 0.02, y + header_h, w - 0.04, body_h,
                          CODE_BG, name="code_body", radius=0.03)
        tf = body.text_frame
        tf.word_wrap = True
        tf.margin_left = Inches(0.3)
        tf.margin_top = Inches(0.14)
        tf.vertical_anchor = MSO_ANCHOR.TOP
        first = True
        for ln in lines:
            p = tf.paragraphs[0] if first else tf.add_paragraph()
            first = False
            p.line_spacing = 1.15
            txt = ln.get("t", "")
            if ln.get("muted"):
                self._run(p, txt, size, TEXT_MUTED, font=MONO)
            else:
                for tok, col in self._syntax(txt):
                    self._run(p, tok, size, col, font=MONO)
        return y + panel_h

    # ------------------------------------------------------------------
    # Mockup + spotlight
    # ------------------------------------------------------------------
    def _mockup(self, slide, x, y, w, h, spec):
        frame = self._rect(slide, x, y, w, h, SURFACE2, BORDER, name="mockup", radius=0.06)
        bar = self._rect(slide, x + 0.04, y + 0.04, w - 0.08, 0.34, CODE_HDR, name="mockup_bar",
                         radius=0.12)
        for i, (dx, col) in enumerate([(0.35, ALERT), (0.66, ACCENT), (0.97, TEXT_MUTED)]):
            dot = slide.shapes.add_shape(MSO_SHAPE.OVAL, Inches(x + dx), Inches(y + 0.13),
                                         Inches(0.11), Inches(0.11))
            dot.name = "decor"
            dot.fill.solid()
            dot.fill.fore_color.rgb = col
            dot.line.fill.background()
        img_path = self.root / spec.get("img", "") if spec.get("img") else None
        inner_y = y + 0.44
        inner_h = h - 0.5
        if img_path and img_path.exists():
            from PIL import Image
            with Image.open(img_path) as im:
                iw, ih = im.size
            ratio = min((w - 0.16) / iw, inner_h / ih)
            nw, nh = iw * ratio, ih * ratio
            ix, iy = x + (w - nw) / 2, inner_y + (inner_h - nh) / 2
            pic = slide.shapes.add_picture(str(img_path), Inches(ix), Inches(iy),
                                           Inches(nw), Inches(nh))
            pic.name = "mockup_img"
            # assombrissement global
            dim = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(ix), Inches(iy),
                                         Inches(nw), Inches(nh))
            dim.name = "decor"
            dim.fill.solid()
            dim.fill.fore_color.rgb = RGBColor(0, 0, 0)
            self._alpha(dim, 45)
            dim.line.fill.background()
            # spotlight : anneau accent sur la zone de focus
            ring = slide.shapes.add_shape(MSO_SHAPE.OVAL,
                                          Inches(x + w / 2 - 0.55), Inches(inner_y + inner_h / 2 - 0.55),
                                          Inches(1.1), Inches(1.1))
            ring.name = "decor"
            ring.fill.background()
            ring.line.color.rgb = ACCENT
            ring.line.width = Pt(3)
            ring.shadow.inherit = False
        else:
            ph = self._rect(slide, x + 0.1, inner_y + 0.15, w - 0.2, inner_h - 0.3,
                            SURFACE, BORDER, name="carte")
            icon_name = spec.get("ph_icon", "image")
            icon = self.icons.get(icon_name, {}).get("png")
            icon_path = self.root / icon if icon else None
            if icon_path and icon_path.exists():
                slide.shapes.add_picture(str(icon_path),
                                         Inches(x + w / 2 - 0.3), Inches(inner_y + 0.5),
                                         Inches(0.6), Inches(0.6))
            ptf = ph.text_frame
            ptf.margin_top = Inches(1.1)
            ptf.vertical_anchor = MSO_ANCHOR.MIDDLE
            pp = ptf.paragraphs[0]
            pp.alignment = PP_ALIGN.CENTER
            self._run(pp, "Emplacement réservé — capture à insérer", 13, TEXT_SEC)
        if spec.get("caption"):
            ctf = self._txtbox(slide, x, y + h + 0.04, w, 0.26, name="caption")
            cp = ctf.paragraphs[0]
            cp.alignment = PP_ALIGN.CENTER
            self._run(cp, spec["caption"], 12, TEXT_MUTED)

    # ------------------------------------------------------------------
    # Diagrammes vectoriels redessines
    # ------------------------------------------------------------------
    def _box(self, slide, x, y, w, h, fill, line, t1, t2="", name="box", t1c=None, t1w=600):
        b = self._rect(slide, x, y, w, h, fill, line, name=name, radius=0.10)

        def _tw(t):
            return max(0.5, min(w - 0.2, 0.115 * len(t) + 0.35))

        if t1:
            t1f = self._txtbox(slide, x + 0.1, y + 0.12, _tw(t1), 0.3, name="corps")
            p = t1f.paragraphs[0]
            self._run(p, t1, 13, t1c or TEXT, weight=t1w)
        if t2:
            t2f = self._txtbox(slide, x + 0.1, y + 0.42, _tw(t2), 0.3, name="corps")
            p2 = t2f.paragraphs[0]
            self._run(p2, t2, 10.5, TEXT)
        return b

    def _arrow(self, slide, x1, y1, x2, y2, color=ACCENT, vertical=False):
        if vertical:
            ax, ay = x1 - 0.12, min(y1, y2)
            aw, ah = 0.24, abs(y2 - y1)
            kind = MSO_SHAPE.DOWN_ARROW if y2 > y1 else MSO_SHAPE.UP_ARROW
        else:
            ax, ay = min(x1, x2), y1 - 0.12
            aw, ah = abs(x2 - x1), 0.24
            kind = MSO_SHAPE.RIGHT_ARROW if x2 > x1 else MSO_SHAPE.LEFT_ARROW
        a = slide.shapes.add_shape(kind, Inches(ax), Inches(ay), Inches(aw), Inches(ah))
        a.name = "decor"
        a.fill.solid()
        a.fill.fore_color.rgb = color
        a.line.fill.background()
        a.shadow.inherit = False
        return a

    def _draw_hexagonal(self, slide, x, y, w, h):
        # pyramide de couches : Api (large) -> Domain (etroit, centre bas)
        bands = [
            ("Agirh.Api", "composition root · DI", NAVY2, 0.0),
            ("Infrastructure", "adaptateurs · EF · agents IA", NAVY1, 0.0),
            ("Core", "ports · use cases · RBAC", SURFACE2, 0.08),
            ("Domain", "pur · 0 dépendance", ACCENT, 0.16),
        ]
        bh, gap = 0.82, 0.14
        y0 = y + 0.15
        for i, (t1, t2, fill, shrink) in enumerate(bands):
            bw = w * (1.0 - 0.18 * i)
            bx = x + (w - bw) / 2 + shrink * 0
            by = y0 + i * (bh + gap)
            self._box(slide, bx, by, bw, bh, fill, BORDER, t1, t2,
                      name=f"ring_{i}", t1c=TEXT, t1w=700)
        # ports adaptateurs sur la bande Infrastructure (cote droit)
        self._box(slide, x + w - 1.9, y0 + bh + gap + 0.08, 1.6, 0.5, SURFACE, ACCENT,
                  "Repos EF", name="port")
        self._box(slide, x + w - 3.6, y0 + bh + gap + 0.08, 1.6, 0.5, SURFACE, ACCENT,
                  "Agents IA", name="port")

    def _draw_pipeline(self, slide, x, y, w, h):
        n = 5
        bw, gap = 1.28, 0.22
        boxes = [
            ("Profiler", "intention JSON", NAVY1),
            ("Dispatcher", "RBAC C# · 0 LLM", ALERT),
            ("Worker", "outils MAF", SURFACE2),
            ("Synthesizer", "reformulation", NAVY1),
            ("Checker", "fail-closed", ALERT),
        ]
        y0 = y + 0.3
        for i, (t1, t2, fill) in enumerate(boxes):
            bx = x + i * (bw + gap)
            self._box(slide, bx, y0, bw, 1.15, fill, BORDER, t1, t2,
                      name=f"pipe_{i}", t1c=TEXT, t1w=700)
            if i < n - 1:
                self._arrow(slide, bx + bw, y0 + 0.57, bx + bw + gap, y0 + 0.57)
        # flux SSE vers le frontend
        sse = self._box(slide, x, y0 + 1.55, w, 0.6, SURFACE, BORDER,
                        "SSE → token · done · denied", name="sse")
        self._arrow(slide, x + 0.5, y0 + 1.5, x + 0.5, y0 + 1.15, vertical=True)

    def _draw_toctou(self, slide, x, y, w, h):
        y0 = y + 0.2
        self._box(slide, x + 0.1, y0, w / 2 - 0.5, 0.7, SURFACE, BORDER,
                  "Requête 1 — 2 000 €", name="req1")
        self._box(slide, x + w / 2 + 0.3, y0, w / 2 - 0.5, 0.7, SURFACE, BORDER,
                  "Requête 2 — 2 000 €", name="req2")
        self._box(slide, x + w / 4, y0 + 1.15, w / 2, 0.9, SURFACE, ALERT,
                  "Index unique filtré", "(EmployeeId) WHERE Pending", name="idx",
                  t1c=ALERT_TXT, t1w=700)
        self._box(slide, x + 0.1, y0 + 2.6, w / 2 - 0.5, 0.8, SURFACE2, BORDER,
                  "SalaryAdvanceRequest", name="db")
        self._box(slide, x + w / 2 + 0.3, y0 + 2.6, w / 2 - 0.5, 0.6, SURFACE, ALERT,
                  "✗ REJET (TOCTOU)", name="rej", t1c=ALERT_TXT, t1w=700)
        self._arrow(slide, x + w / 4 + 0.7, y0 + 0.7, x + w / 4 + 0.7, y0 + 1.15,
                    vertical=True)
        self._arrow(slide, x + w / 4 + 0.7, y0 + 2.05, x + w / 4 + 0.7, y0 + 2.6,
                    vertical=True)
        self._arrow(slide, x + 0.68 * w, y0 + 0.7, x + 0.68 * w, y0 + 2.6,
                    color=ALERT, vertical=True)

    def _draw_avance(self, slide, x, y, w, h):
        y0 = y + 0.4
        steps = [
            ("Chat", "« avance de 2000 € »", SURFACE),
            ("Profiler", "intention + entités", NAVY1),
            ("Dispatcher", "RBAC ✓", ALERT),
            ("Use Case Core", "cap 50 % + unicité", NAVY1),
            ("WIDGET", "carte dans le chat", ACCENT),
        ]
        bw, gap = 1.32, 0.16
        for i, (t1, t2, fill) in enumerate(steps):
            bx = x + i * (bw + gap)
            self._box(slide, bx, y0, bw, 1.1, fill, BORDER, t1, t2,
                      name=f"av_{i}", t1c=TEXT, t1w=700)
            if i < len(steps) - 1:
                self._arrow(slide, bx + bw, y0 + 0.55, bx + bw + gap, y0 + 0.55)
        b = self._box(slide, x, y0 + 1.6, w, 0.6, SURFACE, ACCENT,
                      "5 000 € accepté  ·  5 000,01 € refusé", name="cap",
                      t1c=ACCENT_TXT, t1w=700)

    def _draw_modele(self, slide, x, y, w, h):
        cols = 2
        cards = [
            ("Employee", "rôles · hiérarchie · solde", NAVY1),
            ("LeaveRequest", "congés / CET · statuts", SURFACE2),
            ("SalaryAdvanceRequest", "plafond 50 % · 1 Pending", ALERT),
            ("KnowledgeDocument", "vector(768) · RAG", NAVY2),
        ]
        cw, ch, gx, gy = (w - 0.5) / 2, 0.95, 0.5, 0.5
        for i, (t1, t2, fill) in enumerate(cards):
            cx = x + (i % cols) * (cw + gx)
            cy = y + (i // cols) * (ch + gy)
            self._box(slide, cx, cy, cw, ch, fill, BORDER, t1, t2,
                      name=f"ent_{i}", t1c=TEXT, t1w=700)

    # ------------------------------------------------------------------
    # Builders de slides
    # ------------------------------------------------------------------
    def _build_garde(self, prs, meta):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide, deep=True)
        self._h_line(slide, 0, 0, SLIDE_W, ACCENT, 0.05)
        for key, (side, y) in {"ecole": (0, 0.6), "entreprise": (1, 0.6)}.items():
            info = self.manifest.get("logos", {}).get(key, {})
            path = self.root / info["path"] if info.get("path") else None
            if path and path.exists():
                from PIL import Image
                with Image.open(path) as im:
                    wi, hi = im.size
                h = 0.95
                w = h * wi / hi
                slide.shapes.add_picture(str(path), Inches(GUT if side == 0 else SLIDE_W - GUT - w),
                                         Inches(y), Inches(w), Inches(h))
        center = SLIDE_W / 2
        ktf = self._txtbox(slide, center - 5, 2.1, 10, 0.4, name="breadcrumb")
        kp = ktf.paragraphs[0]
        kp.alignment = PP_ALIGN.CENTER
        self._run(kp, meta.get("role_stage", "").upper(), 15, ACCENT_TXT, weight=700, spc=300)
        ttf = self._txtbox(slide, center - 5, 2.5, 10, 1.4, name="titre")
        tp = ttf.paragraphs[0]
        tp.alignment = PP_ALIGN.CENTER
        self._run(tp, "AGIRH", 84, TEXT, weight=800)
        stf = self._txtbox(slide, center - 5, 3.85, 10, 0.5, name="corps")
        sp = stf.paragraphs[0]
        sp.alignment = PP_ALIGN.CENTER
        self._run(sp, meta.get("sous_titre", ""), 22, TEXT_SEC, weight=400)
        self._h_line(slide, center - 1.5, 4.5, 3.0, ALERT, 0.03)

        def _c(y, text, size, color, weight=400, h=0.32):
            tf = self._txtbox(slide, center - 5.9, y, 11.8, h, name="corps")
            p = tf.paragraphs[0]
            p.alignment = PP_ALIGN.CENTER
            self._run(p, text, size, color, weight=weight)

        _c(4.75, meta.get("auteur", ""), 26, TEXT, weight=700)
        _c(5.15, meta.get("statut", ""), 17, TEXT_SEC)
        _c(5.5, meta.get("ecole", ""), 17, TEXT_SEC)
        _c(5.85, meta.get("entreprise", ""), 17, TEXT_SEC)
        tut = meta.get("tuteurs", {})
        ltf = self._txtbox(slide, center - 5.6, 6.3, 5.2, 0.32, name="corps")
        lp = ltf.paragraphs[0]
        lp.alignment = PP_ALIGN.RIGHT
        self._run(lp, f"Tuteur école : {tut.get('ecole', '')}", 15, TEXT_SEC)
        rtf = self._txtbox(slide, center + 0.4, 6.3, 5.2, 0.32, name="corps")
        rp = rtf.paragraphs[0]
        rp.alignment = PP_ALIGN.LEFT
        self._run(rp, f"Tuteur entreprise : {tut.get('entreprise', '')}", 15, TEXT_SEC)
        dtf = self._txtbox(slide, center - 5.9, 6.75, 11.8, 0.32, name="date")
        dp = dtf.paragraphs[0]
        dp.alignment = PP_ALIGN.CENTER
        self._run(dp, meta.get("date", ""), 15, TEXT_MUTED)

    def _build_hero(self, prs, spec):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._watermark(slide)
        self._page_number(slide)
        ttf = self._txtbox(slide, GUT, 1.5, 11.8, 1.4, name="titre")
        tp = ttf.paragraphs[0]
        self._run(tp, spec["title"], 44, TEXT, weight=800)
        stf = self._txtbox(slide, GUT, 3.0, 11.8, 0.5, name="corps")
        sp = stf.paragraphs[0]
        self._run(sp, spec.get("subtitle", ""), 20, ACCENT_TXT, weight=500)
        m = spec.get("metrics", [])
        cw, gap, y = 3.7, 0.3, 4.0
        for i, met in enumerate(m):
            cx = GUT + i * (cw + gap)
            zone = self._rect(slide, cx, y, cw, 2.3, SURFACE, BORDER, name=f"kpi_{i}", radius=0.10)
            color = ACCENT_TXT if i % 2 == 0 else ALERT_TXT
            self._h_line(slide, cx + 0.2, y, cw - 0.4, color, 0.04)
            vtf = self._txtbox(slide, cx + 0.25, y + 0.45, cw - 0.5, 1.2, name="kpi_v")
            vp = vtf.paragraphs[0]
            self._run(vp, met.get("value", ""), 44, color, weight=800)
            ltf = self._txtbox(slide, cx + 0.25, y + 1.6, cw - 0.5, 0.7, name="kpi_l")
            lp = ltf.paragraphs[0]
            self._run(lp, met.get("label", ""), 14, TEXT_SEC)

    def _build_sommaire(self, prs, spec):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._chrome(slide, "Plan", "Trajectoire")
        items = spec.get("items", [])
        y = 1.9
        for it in items:
            badge = self._rect(slide, GUT, y, 0.62, 0.62, NAVY1, ACCENT, name="carte", radius=0.2)
            btf = badge.text_frame
            btf.margin_top = Inches(0.03)
            bp = btf.paragraphs[0]
            bp.alignment = PP_ALIGN.CENTER
            self._run(bp, it["n"], 20, ACCENT_TXT, weight=800)
            self._icon(slide, it.get("icon", "check"), GUT + 0.95, y + 0.14, 0.34)
            ltf = self._txtbox(slide, GUT + 1.5, y - 0.02, 3.0, 0.6, name="corps")
            lp = ltf.paragraphs[0]
            self._run(lp, it["label"], 24, TEXT, weight=800)
            dtf = self._txtbox(slide, GUT + 4.6, y + 0.06, 7.1, 0.5, name="corps")
            dp = dtf.paragraphs[0]
            self._run(dp, it.get("text", ""), 15, TEXT_SEC)
            self._h_line(slide, GUT + 4.6, y + 0.7, 7.1, BORDER, 0.015)
            y += 1.0
        self._page_number(slide)

    def _build_transition(self, prs, spec, index):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide, deep=True)
        self._h_line(slide, 0, 0, SLIDE_W, ACCENT, 0.05)
        ghost = self._txtbox(slide, SLIDE_W - 4.6, 1.4, 4.0, 1.6, name="decor")
        gp = ghost.paragraphs[0]
        gp.alignment = PP_ALIGN.RIGHT
        self._run(gp, f"0{index}", 120, SURFACE, weight=800)
        center = SLIDE_W / 2
        ktf = self._txtbox(slide, center - 5, 2.75, 10, 0.4, name="kicker")
        kp = ktf.paragraphs[0]
        kp.alignment = PP_ALIGN.CENTER
        self._run(kp, spec.get("kicker", "").upper(), 15, ALERT_TXT, weight=700, spc=300)
        ttf = self._txtbox(slide, center - 5, 3.15, 10, 1.2, name="titre")
        tp = ttf.paragraphs[0]
        tp.alignment = PP_ALIGN.CENTER
        self._run(tp, spec["title"], 50, TEXT, weight=800)
        self._h_line(slide, center - 1.0, 4.45, 2.0, ALERT, 0.03)
        stf = self._txtbox(slide, center - 5, 4.6, 10, 0.6, name="corps")
        sp = stf.paragraphs[0]
        sp.alignment = PP_ALIGN.CENTER
        self._run(sp, spec.get("subtitle", ""), 20, TEXT_SEC)
        self._page_number(slide)

    def _build_statement(self, prs, spec, breadcrumb):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._chrome(slide, breadcrumb, spec["title"])
        ltf = self._txtbox(slide, GUT, 2.1, 3.6, 3.5, name="corps")
        lp = ltf.paragraphs[0]
        self._run(lp, spec.get("left", ""), 19, TEXT_SEC, weight=400)
        cards = spec.get("cards", [])
        if cards:
            self._bento_grid(slide, cards, x=4.75, y=2.1, w=7.8, h=4.4)
        self._page_number(slide)

    def _bento_grid(self, slide, cards, x, y, w, h):
        n = len(cards)
        cols = 3 if n >= 3 else n
        rows = (n + cols - 1) // cols
        cw = (w - (cols - 1) * 0.25) / cols
        ch = (h - (rows - 1) * 0.25) / rows
        for i, card in enumerate(cards):
            cx = x + (i % cols) * (cw + 0.25)
            cy = y + (i // cols) * (ch + 0.25)
            accent = ACCENT if i % 2 == 0 else None
            self._card(slide, cx, cy, cw, ch, card.get("icon"), card.get("title"),
                       card.get("text"), accent=accent)

    def _build_bento(self, prs, spec, breadcrumb):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._chrome(slide, breadcrumb, spec["title"])
        self._bento_grid(slide, spec.get("cards", []), GUT, 2.0, SLIDE_W - 2 * GUT, 4.6)
        self._page_number(slide)

    def _build_diagram(self, prs, spec, breadcrumb):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._chrome(slide, breadcrumb, spec["title"])
        ltf = self._txtbox(slide, GUT, 2.1, 3.6, 3.5, name="corps")
        lp = ltf.paragraphs[0]
        self._run(lp, spec.get("left", ""), 19, TEXT_SEC, weight=400)
        draw = {"hexagonal": self._draw_hexagonal, "pipeline": self._draw_pipeline,
                "toctou": self._draw_toctou, "avance": self._draw_avance,
                "modele": self._draw_modele}.get(spec.get("diagram"))
        if draw:
            draw(slide, 4.75, 2.0, 7.8, 3.9)
        # KPI optionnels (slide avance) : chips bas de slide
        for i, met in enumerate(spec.get("metrics", [])):
            cx = GUT + i * 4.0
            zone = self._rect(slide, cx, 6.0, 3.7, 0.85, SURFACE, BORDER,
                              name=f"cap_{i}", radius=0.14)
            vtf = self._txtbox(slide, cx + 0.2, 6.05, 2.0, 0.5, name="cap_v")
            vp = vtf.paragraphs[0]
            self._run(vp, met["value"], 24, ACCENT_TXT, weight=800)
            ltf = self._txtbox(slide, cx + 0.2, 6.5, 3.4, 0.3, name="cap_l")
            lp = ltf.paragraphs[0]
            self._run(lp, met.get("label", ""), 11, TEXT_SEC)
        self._page_number(slide)

    def _build_code(self, prs, spec, breadcrumb):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._chrome(slide, breadcrumb, spec["title"])
        ltf = self._txtbox(slide, GUT, 2.1, 3.6, 3.5, name="corps")
        lp = ltf.paragraphs[0]
        self._run(lp, spec.get("left", ""), 19, TEXT_SEC, weight=400)
        if spec.get("code"):
            self._editor(slide, 4.75, 2.0, 7.8, spec["code"])
        self._page_number(slide)

    def _build_mockup(self, prs, spec, breadcrumb):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._chrome(slide, breadcrumb, spec["title"])
        ltf = self._txtbox(slide, GUT, 2.1, 3.6, 3.5, name="corps")
        lp = ltf.paragraphs[0]
        self._run(lp, spec.get("left", ""), 19, TEXT_SEC, weight=400)
        mockups = spec.get("mockups") or ([spec["mockup"]] if spec.get("mockup") else [])
        mw, mh, gap = 3.7, 3.2, 0.4
        for i, m in enumerate(mockups[:2]):
            self._mockup(slide, 4.75 + i * (mw + gap), 2.0, mw, mh, m)
        cards = spec.get("cards", [])
        if cards:
            cw = (7.8 - 2 * 0.25) / 3
            for j, card in enumerate(cards[:3]):
                cx = 4.75 + j * (cw + 0.25)
                self._card(slide, cx, 5.55, cw, 1.3, card.get("icon"),
                           card.get("title"), card.get("text"))
        self._page_number(slide)

    def _build_kpi(self, prs, spec, breadcrumb):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._chrome(slide, breadcrumb, spec["title"])
        m = spec.get("metrics", [])
        cw, gap, y = 2.75, 0.24, 2.3
        for i, met in enumerate(m):
            cx = GUT + i * (cw + gap)
            zone = self._rect(slide, cx, y, cw, 3.2, SURFACE, BORDER, name=f"kpi_{i}", radius=0.10)
            color = ACCENT_TXT if i % 2 == 0 else ALERT_TXT
            self._h_line(slide, cx + 0.2, y, cw - 0.4, color, 0.04)
            vtf = self._txtbox(slide, cx + 0.2, y + 0.5, cw - 0.4, 1.6, name="kpi_v")
            vp = vtf.paragraphs[0]
            vp.alignment = PP_ALIGN.LEFT
            self._run(vp, met["value"], 58, color, weight=800)
            ltf = self._txtbox(slide, cx + 0.2, y + 2.2, cw - 0.4, 0.9, name="kpi_l")
            lp = ltf.paragraphs[0]
            self._run(lp, met.get("label", ""), 14, TEXT_SEC)
        self._page_number(slide)

    def _build_thanks(self, prs, spec):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide, deep=True)
        self._h_line(slide, 0, 0, SLIDE_W, ACCENT, 0.05)
        center = SLIDE_W / 2
        ttf = self._txtbox(slide, center - 5, 2.9, 10, 1.4, name="titre")
        tp = ttf.paragraphs[0]
        tp.alignment = PP_ALIGN.CENTER
        self._run(tp, spec["title"], 80, TEXT, weight=800)
        stf = self._txtbox(slide, center - 5, 4.3, 10, 0.6, name="corps")
        sp = stf.paragraphs[0]
        sp.alignment = PP_ALIGN.CENTER
        self._run(sp, spec.get("subtitle", ""), 26, ACCENT_TXT, weight=600)
        # logos symetriques (cloture)
        for key, (side, y) in {"ecole": (0, 5.3), "entreprise": (1, 5.3)}.items():
            info = self.manifest.get("logos", {}).get(key, {})
            path = self.root / info["path"] if info.get("path") else None
            if path and path.exists():
                from PIL import Image
                with Image.open(path) as im:
                    wi, hi = im.size
                h = 0.75
                w = h * wi / hi
                slide.shapes.add_picture(str(path), Inches(center - 3.2 if side == 0 else center + 3.2 - w),
                                         Inches(y), Inches(w), Inches(h))
        self._page_number(slide)

    def _build_annexe(self, prs, spec):
        slide = prs.slides.add_slide(prs.slide_layouts[6])
        self.page_no += 1
        self._mesh_backdrop(slide)
        self._chrome(slide, "Annexes", spec["title"])
        if spec.get("diagram"):
            draw = {"modele": self._draw_modele}.get(spec["diagram"])
            if draw:
                draw(slide, GUT, 2.1, 7.8, 4.4)
        if spec.get("code"):
            self._editor(slide, GUT, 2.1, 7.8, spec["code"])
        if spec.get("cards"):
            self._bento_grid(slide, spec["cards"], GUT, 2.0, SLIDE_W - 2 * GUT, 4.6)
        self._page_number(slide)

    # ------------------------------------------------------------------
    def build_draft(self, structure: dict) -> Path:
        prs = Presentation()
        prs.slide_width = Inches(SLIDE_W)
        prs.slide_height = Inches(SLIDE_H)
        meta = structure["meta"]
        self._build_garde(prs, meta)
        slides = structure["slides"]
        self._build_hero(prs, slides[1])
        self._build_sommaire(prs, slides[2])
        for idx, section in enumerate(structure["sections"], start=1):
            self._build_transition(prs, section["intercalaire"], idx)
            br = section["breadcrumb"]
            for s in section["slides"]:
                b = self._build_content(prs, s, br)
        self._build_thanks(prs, structure["thanks"])
        for annexe in structure["annexes"]:
            self._build_annexe(prs, annexe)
        target = self.root / self.config["paths"]["draft"]
        prs.save(target)
        self.info(f"Draft v3 sauvegarde: {target} ({len(prs.slides._sldIdLst)} slides)")
        return target

    def _build_content(self, prs, spec, breadcrumb):
        kind = spec["type"]
        if kind == "statement":
            return self._build_statement(prs, spec, breadcrumb)
        if kind == "bento":
            return self._build_bento(prs, spec, breadcrumb)
        if kind == "diagram":
            return self._build_diagram(prs, spec, breadcrumb)
        if kind == "code":
            return self._build_code(prs, spec, breadcrumb)
        if kind == "mockup":
            return self._build_mockup(prs, spec, breadcrumb)
        if kind == "kpi":
            return self._build_kpi(prs, spec, breadcrumb)
        return self._build_bento(prs, spec, breadcrumb)

    # ------------------------------------------------------------------
    def run(self) -> dict:
        self.info(f"=== {self.name} - DESIGN v3 ===")
        manifest_path = self.root / "assets" / "img" / "manifest.json"
        if manifest_path.exists():
            self.manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            self.icons = self.manifest.get("icones", {}).get("blanc", {})
        structure = self.read_json(self.config["paths"]["content_structure"])
        self.total = structure["total_slides"]
        target = self.build_draft(structure)
        return self.make_report("OK", {"draft": str(target), "slides": self.page_no,
                                       "total_prevus": self.total})



