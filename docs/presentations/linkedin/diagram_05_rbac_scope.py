# -*- coding: utf-8 -*-
"""Diagramme anime #5/5 : RBAC a portee -- le pulse suit les 2 portes, dans cet ordre."""
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

boxes = []
arrows = []
for i, (name, desc) in enumerate(STEPS):
    y = Y0 + PITCH * i
    last = (i == len(STEPS) - 1)
    boxes.append({"box": {"x": X0, "y": y, "w": W, "h": H}, "text": f"{name}\n{desc}",
                  "text_size": 22, "sub_size": 16,
                  "fill": r.GOOD_BG if last else r.ACCENT_BG,
                  "outline": r.GOOD if last else r.PRIMARY})
    if i > 0:
        cx = X0 + W / 2
        arrows.append(((cx, Y0 + PITCH * (i - 1) + H), (cx, y)))

stops = [[0], [1], [2], [3]]


def draw_static(canvas):
    r.draw_centered_text(canvas, r.SIZE / 2, 70, "RBAC A PORTEE", 32, color=r.PRIMARY)
    r.draw_centered_text(canvas, r.SIZE / 2, 1000,
                          "RBAC toujours verifie avant la portee, jamais l'inverse", 18,
                          bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_pulse_loop(OUT, boxes, arrows, stops, draw_static_fn=draw_static,
                         hold_frames=20, travel_frames=14, fade_frames=12)
    print(f"OK -> {OUT}")
