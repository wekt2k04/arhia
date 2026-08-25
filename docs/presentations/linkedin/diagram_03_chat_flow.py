# -*- coding: utf-8 -*-
"""Diagramme anime #3/5 : RAG + chat, du message a la reponse sourcee."""
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
STACK_Y = 480

stack_state, final_state = [], []
for i, (name, desc) in enumerate(STEPS):
    off = i * 3
    stack_state.append({
        "box": {"x": 380 + off, "y": STACK_Y + off, "w": 320, "h": 90},
        "text": name, "text_size": 17, "sub_size": 12,
    })
    fill_good = (i == len(STEPS) - 1)
    final_state.append({
        "box": {"x": X0, "y": Y0 + PITCH * i, "w": W, "h": H},
        "text": f"{name}\n{desc}", "text_size": 21, "sub_size": 15,
        "fill": r.GOOD_BG if fill_good else r.ACCENT_BG,
        "outline": r.GOOD if fill_good else r.PRIMARY,
    })


def draw_title_only(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 60, "DE LA QUESTION A LA REPONSE", 28, color=r.PRIMARY)


def draw_title_and_arrows(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 60, "DE LA QUESTION A LA REPONSE", 28, color=r.PRIMARY)
    if t > 0.7:
        for i in range(len(STEPS) - 1):
            cx = X0 + W / 2
            y_top = Y0 + PITCH * i + H
            r.draw_arrow_down(draw, cx, y_top, PITCH - H - 6)
    if t >= 0.999:
        r.draw_centered_text(draw, r.SIZE / 2, 1000,
                              "0 candidat ou score trop bas -> refus, jamais d'hallucination", 18,
                              bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_video(
        OUT,
        segments=[(stack_state, draw_title_only), (final_state, draw_title_and_arrows)],
        hold_frames=30, transition_frames=36, loop=True,
    )
    print(f"OK -> {OUT}")
