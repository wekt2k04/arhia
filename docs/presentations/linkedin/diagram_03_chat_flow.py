# -*- coding: utf-8 -*-
"""Diagramme anime #3/5 : un pulse suit la question, du chat jusqu'a la reponse sourcee."""
import os
import frame_render as r

OUT = os.path.join(os.path.dirname(__file__), "diagram_03_chat_flow.mp4")

STEPS = [
    ("Question", "Utilisateur pose sa question dans le chat"),
    ("Router", "Classification d'intention (fail-safe)"),
    ("RAG", "Recherche Qdrant + reranking cross-encodeur"),
    ("Generator", "Reponse en streaming (SSE)"),
    ("Reponse sourcee", "Sources citees, jamais inventees"),
]

Y0, PITCH, H = 130, 155, 115
X0, W = 90, 900

boxes = []
arrows = []
for i, (name, desc) in enumerate(STEPS):
    y = Y0 + PITCH * i
    last = (i == len(STEPS) - 1)
    boxes.append({"box": {"x": X0, "y": y, "w": W, "h": H}, "text": f"{name}\n{desc}",
                  "text_size": 21, "sub_size": 15,
                  "fill": r.GOOD_BG if last else r.ACCENT_BG,
                  "outline": r.GOOD if last else r.PRIMARY})
    if i > 0:
        cx = X0 + W / 2
        arrows.append(((cx, Y0 + PITCH * (i - 1) + H), (cx, y)))

stops = [[0], [1], [2], [3], [4]]


def draw_static(canvas):
    r.draw_centered_text(canvas, r.SIZE / 2, 60, "DE LA QUESTION A LA REPONSE", 28, color=r.PRIMARY)
    r.draw_centered_text(canvas, r.SIZE / 2, 1000,
                          "0 candidat ou score trop bas -> refus, jamais d'hallucination", 18,
                          bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_pulse_loop(OUT, boxes, arrows, stops, draw_static_fn=draw_static,
                         hold_frames=18, travel_frames=13, fade_frames=12)
    print(f"OK -> {OUT}")
