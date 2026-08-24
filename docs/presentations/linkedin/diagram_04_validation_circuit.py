# -*- coding: utf-8 -*-
"""Diagramme anime #4/5 : circuit de validation des templates."""
import os
import frame_render as r

OUT = os.path.join(os.path.dirname(__file__), "diagram_04_validation_circuit.mp4")

stack_state = [
    {"box": {"x": 380 + i * 3, "y": 480 + i * 3, "w": 320, "h": 100}, "text": t,
     "text_size": 18, "sub_size": 13}
    for i, t in enumerate(["Draft", "InReview", "Approved", "Rejected"])
]

final_state = [
    {"box": {"x": 90, "y": 260, "w": 900, "h": 150}, "text": "DRAFT\nRedacteur cree le template",
     "text_size": 26, "sub_size": 17},
    {"box": {"x": 90, "y": 470, "w": 900, "h": 150}, "text": "IN REVIEW\nVerificateur != redacteur",
     "text_size": 26, "sub_size": 17},
    {"box": {"x": 90, "y": 700, "w": 420, "h": 170}, "text": "APPROVED\nApprobateur distinct des 2 autres",
     "text_size": 22, "sub_size": 15, "fill": r.GOOD_BG, "outline": r.GOOD},
    {"box": {"x": 570, "y": 700, "w": 420, "h": 170}, "text": "REJECTED\nMotif obligatoire",
     "text_size": 22, "sub_size": 15, "fill": r.WARN_BG, "outline": r.WARN},
]


def draw_title_only(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 90, "CIRCUIT DE VALIDATION", 32, color=r.PRIMARY)


def draw_title_and_arrows(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 90, "CIRCUIT DE VALIDATION", 32, color=r.PRIMARY)
    if t > 0.7:
        r.draw_arrow(draw, (540, 410), (540, 464))
        r.draw_arrow(draw, (460, 620), (300, 694))
        r.draw_arrow(draw, (620, 620), (780, 694))
    if t >= 0.999:
        r.draw_centered_text(draw, r.SIZE / 2, 940 + 40,
                              "3 identites distinctes : redacteur, verificateur, approbateur", 18,
                              bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_video(
        OUT,
        segments=[(stack_state, draw_title_only), (final_state, draw_title_and_arrows)],
        hold_frames=30, transition_frames=36,
    )
    print(f"OK -> {OUT}")
