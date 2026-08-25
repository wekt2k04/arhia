# -*- coding: utf-8 -*-
"""Diagramme anime #2/5 : Pipeline RAG en 4 phases."""
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
STACK_Y = 480

stack_state, final_state = [], []
for i, (name, desc) in enumerate(PHASES):
    off = i * 3
    stack_state.append({
        "box": {"x": 380 + off, "y": STACK_Y + off, "w": 320, "h": 110},
        "text": name, "text_size": 18, "sub_size": 13,
    })
    final_state.append({
        "box": {"x": X0, "y": Y0 + PITCH * i, "w": W, "h": H},
        "text": f"{name}\n{desc}", "text_size": 24, "sub_size": 17,
    })


def draw_title_only(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 90, "PIPELINE RAG", 32, color=r.PRIMARY)


def draw_title_and_arrows(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 90, "PIPELINE RAG", 32, color=r.PRIMARY)
    if t > 0.7:
        for i in range(len(PHASES) - 1):
            cx = X0 + W / 2
            y_top = Y0 + PITCH * i + H
            r.draw_arrow_down(draw, cx, y_top, PITCH - H - 6)
    if t >= 0.999:
        r.draw_centered_text(draw, r.SIZE / 2, 990,
                              "Jamais d'appel au generateur sans passage valide", 20,
                              bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_video(
        OUT,
        segments=[(stack_state, draw_title_only), (final_state, draw_title_and_arrows)],
        hold_frames=30, transition_frames=36, loop=True,
    )
    print(f"OK -> {OUT}")
