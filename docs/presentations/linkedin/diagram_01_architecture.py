# -*- coding: utf-8 -*-
"""
Diagramme anime #1/5 (preuve de concept) : Architecture hexagonale.
Usage : python diagram_01_architecture.py
"""
import os
import frame_render as r

OUT = os.path.join(os.path.dirname(__file__), "diagram_01_architecture.mp4")

LAYERS = [
    ("Agirh.Domain", "Entites pures"),
    ("Agirh.Core", "Ports + use cases + RBAC"),
    ("Agirh.Infrastructure", "Un adaptateur par port"),
    ("Agirh.Api", "Controllers + composition root"),
]

# Etat final : escalier
FY0, PITCH, FH = 260, 150, 120
FX0, INDENT, FW0, FSHRINK = 95, 35, 890, 70

# Etat de depart : empilees au centre
STACK_X, STACK_Y, STACK_W, STACK_H = 380, 480, 320, 100

stack_state = []
final_state = []
for i, (name, desc) in enumerate(LAYERS):
    off = i * 3
    stack_state.append({
        "box": {"x": STACK_X + off, "y": STACK_Y + off, "w": STACK_W, "h": STACK_H},
        "text": name, "text_size": 20, "sub_size": 14,
    })
    final_state.append({
        "box": {"x": FX0 + INDENT * i, "y": FY0 + PITCH * i, "w": FW0 - FSHRINK * i, "h": FH},
        "text": f"{name}\n{desc}", "text_size": 26, "sub_size": 18,
    })


def draw_title_only(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 90, "ARCHITECTURE HEXAGONALE", 32, color=r.PRIMARY)


def draw_title_and_arrows(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 90, "ARCHITECTURE HEXAGONALE", 32, color=r.PRIMARY)
    if t > 0.7:
        for i in range(len(LAYERS) - 1):
            x = FX0 + INDENT * i
            cx = x + 170
            y_top = FY0 + PITCH * i + FH
            r.draw_arrow_down(draw, cx, y_top, PITCH - FH - 8)
    if t >= 0.999:
        r.draw_centered_text(draw, r.SIZE / 2, 950, "Le metier ne depend jamais de la technique",
                              22, bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_video(
        OUT,
        segments=[
            (stack_state, draw_title_only),
            (final_state, draw_title_and_arrows),
        ],
        hold_frames=30,
        transition_frames=36,
    )
    print(f"OK -> {OUT}")
