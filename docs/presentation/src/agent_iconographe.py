# -*- coding: utf-8 -*-
"""Agent 2 : L'Iconographe (ETAPE 3).

Mission : preparer les ressources visuelles dans assets/ :
  - Generer des icones vectorielles SVG + PNG transparents (trait uniforme).
  - Detourer les diagrammes (fond blanc -> transparent) pour le theme sombre.
  - Normaliser les captures d'ecran deposees par l'utilisateur.
  - Verifier les logos (variantes _rgba, fond transparent).
  - Produire le manifest JSON lu par l'Architecte (GO 4) + bandeau de stack.

Regles PPT appliquees : 4, 31, 42, 45, 51, 52, 53, 56.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from agent_base import AgentBase

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:  # pragma: no cover
    Image = None

from icon_factory import (ACCENT_BLEU, ACCENT_ORANGE, NEUTRE_BLANC,
                          generate_icons)  # noqa: E402

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

CAPTURES_ATTENDUES = ["capture_chat.png", "capture_widget.png", "capture_login.png"]
DIAGRAMMES = ["D1_Topologie_V6.png", "D2_Pipeline_SSE.png", "D3_Securite_TOCTOU.png", "D4_Modele_Donnees.png"]


class IconographeAgent(AgentBase):
    agent_id = "agent_iconographe"
    name = "L'Iconographe"

    # ------------------------------------------------------------------
    # Icônes vectorielles (regles 45, 53)
    # ------------------------------------------------------------------
    def _generate_icons(self) -> dict:
        icons_dir = self.root / "assets" / "img" / "icons"
        icons_dir.mkdir(parents=True, exist_ok=True)

        bleu = generate_icons(icons_dir, ACCENT_BLEU)
        blanc = generate_icons(icons_dir / "white", NEUTRE_BLANC)
        orange_names = {"warning", "lock", "shield", "bolt", "check"}
        orange = [i for i in generate_icons(icons_dir / "orange", ACCENT_ORANGE)
                  if i["name"] in orange_names]

        self.ensure_rule(45, "icones vectorielles SVG + PNG, trait uniforme")
        self.ensure_rule(53, "aucun melange d'icones 3D/pleines/filaires")
        self.info(f"Icones generees: {len(bleu)} bleu clair + {len(blanc)} blanc + {len(orange)} orange")

        def _to_map(items):
            return {i["name"]: {"png": f"assets/img/icons/{i['png']}",
                                "svg": f"assets/img/icons/{i['svg']}",
                                "couleur": i["couleur"]} for i in items}

        return {
            "bleu": _to_map(bleu),
            "blanc": {i["name"]: {"png": f"assets/img/icons/white/{i['png']}",
                                  "svg": f"assets/img/icons/white/{i['svg']}",
                                  "couleur": NEUTRE_BLANC} for i in blanc},
            "orange": _to_map(orange),
        }

    # ------------------------------------------------------------------
    # Détourage (regle 52) : fond blanc -> transparent
    # ------------------------------------------------------------------
    @staticmethod
    def _detour(img: "Image.Image", tolerance: int = 64) -> "Image.Image":
        img = img.convert("RGBA")
        px = img.load()
        w, h = img.size
        for y in range(h):
            for x in range(w):
                r, g, b, a = px[x, y]
                dist = max(255 - r, 255 - g, 255 - b)  # distance au blanc
                if dist < tolerance:
                    alpha = max(0, int(255 * dist / tolerance))
                    px[x, y] = (r, g, b, min(a, alpha))
        return img

    def _detour_diagrams(self) -> dict:
        src_dir = self.root / "assets" / "diagrams"
        dst_dir = self.root / "assets" / "img" / "detoure"
        dst_dir.mkdir(parents=True, exist_ok=True)
        mapping = {}
        for name in DIAGRAMMES:
            src = src_dir / name
            if not src.exists():
                self.warn(f"Diagramme absent: {name}")
                continue
            if Image is None:
                self.warn("PIL indisponible - detourage ignore")
                dst = src
            else:
                img = Image.open(src)
                detoured = self._detour(img)
                dst = dst_dir / name
                detoured.save(dst)
                self.info(f"Detourage: {name} ({img.size[0]}x{img.size[1]}) -> transparent")
            mapping[name] = f"assets/img/detoure/{name}"
        self.ensure_rule(52, "fonds blancs detoures pour theme sombre")
        return mapping

    # ------------------------------------------------------------------
    # Captures d'écran (regles 31, 50)
    # ------------------------------------------------------------------
    def _process_captures(self) -> dict:
        img_dir = self.root / "assets" / "img"
        img_dir.mkdir(parents=True, exist_ok=True)
        report = {}
        for name in CAPTURES_ATTENDUES:
            path = img_dir / name
            if not path.exists():
                report[name] = {"present": False, "path": None, "note": "a deposer par l'utilisateur"}
                continue
            img = Image.open(path)
            w, h = img.size
            max_w = 1280
            if w > max_w:
                ratio = max_w / w
                img = img.resize((max_w, int(h * ratio)), Image.LANCZOS)
                img.convert("RGB").save(path)
                self.info(f"Capture normalisee: {name} -> {max_w}px max")
            report[name] = {"present": True, "path": f"assets/img/{name}", "width": min(w, max_w),
                            "ratio": f"{w}x{h}"}
        self.ensure_rule(31, "aucun etirement/ecrasement (normalisation proportionnelle)")
        return report

    # ------------------------------------------------------------------
    # Logos (page de garde)
    # ------------------------------------------------------------------
    def _check_logos(self) -> dict:
        img_dir = self.root / "assets" / "img"
        logos = {
            "ecole": img_dir / "ensa_white_rgba.png",
            "entreprise": img_dir / "agirh_white_rgba.png",
        }
        result = {}
        for key, path in logos.items():
            present = path.exists()
            result[key] = {
                "present": present,
                "path": f"assets/img/{path.name}" if present else None,
                "note": "variante _rgba (fond transparent, logos blancs)" if present else "ABSENT",
            }
            self.info(f"Logo {key}: {'present' if present else 'ABSENT'} "
                      f"({path.name if present else ''})")
        return result

    # ------------------------------------------------------------------
    # Bandeau de stack (regle 4) - aide visuelle contexte
    # ------------------------------------------------------------------
    def _build_stack_strip(self) -> str:
        if Image is None:
            return ""
        icons = ["chat", "server", "db", "ai", "shield"]
        labels = ["Assistant IA", "API .NET 8", "SQL Server 2025", "Ollama", "Sécurité RBAC"]
        cell_w, cell_h, pad = 200, 150, 18
        strip = Image.new("RGBA", (cell_w * len(icons), cell_h), (0, 0, 0, 0))
        draw = ImageDraw.Draw(strip)
        try:
            font = ImageFont.truetype("arial.ttf", 22)
        except Exception:
            font = ImageFont.load_default()

        for idx, (name, label) in enumerate(zip(icons, labels)):
            x0 = idx * cell_w
            icon = Image.open(self.root / "assets" / "img" / "icons" / f"{name}.png")
            icon = icon.resize((72, 72), Image.LANCZOS)
            strip.alpha_composite(icon, (x0 + (cell_w - 72) // 2, pad))
            if hasattr(font, "getbbox"):
                tw = font.getbbox(label)[2] - font.getbbox(label)[0]
            else:
                tw = font.getsize(label)[0]
            tx = x0 + (cell_w - tw) // 2
            draw.text((tx, 96), label, fill=NEUTRE_BLANC, font=font)

        out = self.root / "assets" / "img" / "stack_strip.png"
        strip.save(out)
        self.info(f"Bandeau de stack: {out.name} ({strip.size[0]}x{strip.size[1]})")
        return "assets/img/stack_strip.png"

    # ------------------------------------------------------------------
    def run(self) -> dict:
        self.info(f"=== {self.name} - ETAPE 3 ===")
        self.ensure_rule(4, "slide ecosysteme technique preparee")
        self.ensure_rule(42, "diagrammes simplifies pour la soutenance")

        icones = self._generate_icons()
        diagrammes = self._detour_diagrams()
        captures = self._process_captures()
        logos = self._check_logos()
        stack_strip = self._build_stack_strip()

        manifest = {
            "logos": logos,
            "icones": icones,
            "diagrammes": diagrammes,
            "captures": captures,
            "stack_strip": stack_strip,
            "note": "Le chemin detoure (assets/img/detoure/) est prioritaire sur l'original "
                    "pour le theme sombre (regle 52).",
        }
        out = self.write_json("assets/img/manifest.json", manifest)

        return self.make_report("OK", {
            "manifest": str(out),
            "icones_generees": len(icones["bleu"]) + len(icones["blanc"]) + len(icones["orange"]),
            "diagrammes_detoures": list(diagrammes.keys()),
            "captures": {k: v["present"] for k, v in captures.items()},
            "logos": {k: v["present"] for k, v in logos.items()},
            "stack_strip": stack_strip,
        })
