# -*- coding: utf-8 -*-
"""Diagramme anime #2/5 : Pipeline RAG, un pulse traverse les 4 phases dans l'ordre reel."""
import os
import frame_render as r

OUT = os.path.join(os.path.dirname(__file__), "diagram_02_rag_pipeline.mp4")

PHASES = [
    ("1. CHUNKING", "Decoupage structurel Markdown + recouvrement"),
    ("2. EMBEDDING", "ONNX multilingue -> vecteur 768d"),
    ("3. RECHERCHE", "Qdrant, ANN cosinus (HNSW)"),
    ("4. RERANKING", "Cross-encodeur, requete+document ensemble"),
]

Y0, PITCH, H = 220, 190, 150
X0, W = 90, 900

boxes = []
arrows = []
for i, (name, desc) in enumerate(PHASES):
    y = Y0 + PITCH * i
    boxes.append({"box": {"x": X0, "y": y, "w": W, "h": H}, "text": f"{name}\n{desc}",
                  "text_size": 24, "sub_size": 17})
    if i > 0:
        cx = X0 + W / 2
        arrows.append(((cx, Y0 + PITCH * (i - 1) + H), (cx, y)))

stops = [[0], [1], [2], [3]]


def draw_static(canvas):
    r.draw_centered_text(canvas, r.SIZE / 2, 90, "PIPELINE RAG", 32, color=r.PRIMARY)
    r.draw_centered_text(canvas, r.SIZE / 2, 990,
                          "Jamais d'appel au generateur sans passage valide", 20,
                          bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_pulse_loop(OUT, boxes, arrows, stops, draw_static_fn=draw_static,
                         hold_frames=20, travel_frames=14, fade_frames=12)
    print(f"OK -> {OUT}")
