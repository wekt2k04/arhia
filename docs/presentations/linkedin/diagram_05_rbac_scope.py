# -*- coding: utf-8 -*-
"""Diagramme anime #5/5 : RBAC a portee (2 portes, dans cet ordre)."""
import os
import frame_render as r

OUT = os.path.join(os.path.dirname(__file__), "diagram_05_rbac_scope.mp4")

STEPS = [
    ("ROLE", "Employee / HR / QualityAdmin"),
    ("RbacMatrix", "Ce role peut-il faire cette action, en general ?"),
    ("DepartmentScopeGuard", "A-t-il portee sur CETTE cible precise ?"),
    ("ACCES ACCORDE", "Les deux portes franchies, jamais une seule"),
]

Y0, PITCH, H = 160, 200, 150
X0, W = 90, 900
STACK_Y = 480

stack_state, final_state = [], []
for i, (name, desc) in enumerate(STEPS):
    off = i * 3
    stack_state.append({
        "box": {"x": 380 + off, "y": STACK_Y + off, "w": 320, "h": 100},
        "text": name, "text_size": 16, "sub_size": 12,
    })
    last = (i == len(STEPS) - 1)
    final_state.append({
        "box": {"x": X0, "y": Y0 + PITCH * i, "w": W, "h": H},
        "text": f"{name}\n{desc}", "text_size": 22, "sub_size": 16,
        "fill": r.GOOD_BG if last else r.ACCENT_BG,
        "outline": r.GOOD if last else r.PRIMARY,
    })


def draw_title_only(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 70, "RBAC A PORTEE", 32, color=r.PRIMARY)


def draw_title_and_arrows(draw, t):
    r.draw_centered_text(draw, r.SIZE / 2, 70, "RBAC A PORTEE", 32, color=r.PRIMARY)
    if t > 0.7:
        for i in range(len(STEPS) - 1):
            cx = X0 + W / 2
            y_top = Y0 + PITCH * i + H
            r.draw_arrow_down(draw, cx, y_top, PITCH - H - 6)
    if t >= 0.999:
        r.draw_centered_text(draw, r.SIZE / 2, 1000,
                              "RBAC toujours verifie avant la portee, jamais l'inverse", 18,
                              bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_video(
        OUT,
        segments=[(stack_state, draw_title_only), (final_state, draw_title_and_arrows)],
        hold_frames=30, transition_frames=36, loop=True,
    )
    print(f"OK -> {OUT}")
