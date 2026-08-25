# -*- coding: utf-8 -*-
"""
Diagramme anime #1/5 : Architecture hexagonale.
Diagramme fixe (jamais de montage/demontage) ; un pulse voyage Domain -> Core
-> Infrastructure -> Api, boite illuminee en synchronisation -- represente la
regle de dependance reelle (le metier au centre, la technique en peripherie).
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

Y0, PITCH, H = 260, 150, 120
X0, INDENT, W0, W_SHRINK = 95, 35, 890, 70

boxes = []
arrows = []
for i, (name, desc) in enumerate(LAYERS):
    x = X0 + INDENT * i
    w = W0 - W_SHRINK * i
    y = Y0 + PITCH * i
    boxes.append({"box": {"x": x, "y": y, "w": w, "h": H}, "text": f"{name}\n{desc}",
                  "text_size": 26, "sub_size": 18})
    if i > 0:
        prev_x = X0 + INDENT * (i - 1)
        cx = prev_x + 170
        arrows.append(((cx, Y0 + PITCH * (i - 1) + H), (cx, y)))

stops = [[0], [1], [2], [3]]


def draw_static(canvas):
    r.draw_centered_text(canvas, r.SIZE / 2, 90, "ARCHITECTURE HEXAGONALE", 32, color=r.PRIMARY)
    r.draw_centered_text(canvas, r.SIZE / 2, 950, "Le metier ne depend jamais de la technique", 22,
                          bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_pulse_loop(OUT, boxes, arrows, stops, draw_static_fn=draw_static,
                         hold_frames=22, travel_frames=16, fade_frames=12)
    print(f"OK -> {OUT}")
