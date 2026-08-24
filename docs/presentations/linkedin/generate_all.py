# -*- coding: utf-8 -*-
"""
Regenere les 5 diagrammes animes en une commande.
Usage : python generate_all.py   (necessite `pip install --user imageio-ffmpeg pillow`)
"""
import runpy
import os

SCRIPTS = [
    "diagram_01_architecture.py",
    "diagram_02_rag_pipeline.py",
    "diagram_03_chat_flow.py",
    "diagram_04_validation_circuit.py",
    "diagram_05_rbac_scope.py",
]

if __name__ == "__main__":
    here = os.path.dirname(__file__)
    for script in SCRIPTS:
        runpy.run_path(os.path.join(here, script), run_name="__main__")
