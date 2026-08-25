# -*- coding: utf-8 -*-
"""
Diagramme anime #4/5 : circuit de validation des templates.
Le pulse suit Draft -> InReview puis SE SCINDE en 2 (Approved et Rejected
s'illuminent simultanement) -- represente honnetement qu'InReview est un
point de decision a 2 issues possibles, pas un choix arbitraire entre elles.
"""
import os
import frame_render as r

OUT = os.path.join(os.path.dirname(__file__), "diagram_04_validation_circuit.mp4")

boxes = [
    {"box": {"x": 90, "y": 260, "w": 900, "h": 150}, "text": "DRAFT\nRedacteur cree le template",
     "text_size": 26, "sub_size": 17},
    {"box": {"x": 90, "y": 470, "w": 900, "h": 150}, "text": "IN REVIEW\nVerificateur != redacteur",
     "text_size": 26, "sub_size": 17},
    {"box": {"x": 90, "y": 700, "w": 420, "h": 170}, "text": "APPROVED\nApprobateur distinct des 2 autres",
     "text_size": 22, "sub_size": 15, "fill": r.GOOD_BG, "outline": r.GOOD},
    {"box": {"x": 570, "y": 700, "w": 420, "h": 170}, "text": "REJECTED\nMotif obligatoire",
     "text_size": 22, "sub_size": 15, "fill": r.WARN_BG, "outline": r.WARN},
]

arrows = [
    ((540, 410), (540, 464)),
    ((460, 620), (300, 694)),
    ((620, 620), (780, 694)),
]

stops = [[0], [1], [2, 3]]


def draw_static(canvas):
    r.draw_centered_text(canvas, r.SIZE / 2, 90, "CIRCUIT DE VALIDATION", 32, color=r.PRIMARY)
    r.draw_centered_text(canvas, r.SIZE / 2, 980,
                          "3 identites distinctes : redacteur, verificateur, approbateur", 18,
                          bold=False, color=r.MUTED)


if __name__ == "__main__":
    r.render_pulse_loop(OUT, boxes, arrows, stops, draw_static_fn=draw_static,
                         hold_frames=22, travel_frames=16, fade_frames=12)
    print(f"OK -> {OUT}")
