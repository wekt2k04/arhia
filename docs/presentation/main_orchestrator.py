# -*- coding: utf-8 -*-
"""Orchestrateur principal du pipeline multi-agents de generation PPTX AGIRH.

Usage :
    C:\\ProgramData\\anaconda3\\python.exe main_orchestrator.py --help
    C:\\ProgramData\\anaconda3\\python.exe main_orchestrator.py init   # GO 1
    C:\\ProgramData\\anaconda3\\python.exe main_orchestrator.py go2    # Agent 1 (Rédacteur)
    C:\\ProgramData\\anaconda3\\python.exe main_orchestrator.py go3    # Agent 2 (Iconographe)
    C:\\ProgramData\\anaconda3\\python.exe main_orchestrator.py go4    # Agent 3 (Architecte)
    C:\\ProgramData\\anaconda3\\python.exe main_orchestrator.py go5    # Agent 3 (Animations)
    C:\\ProgramData\\anaconda3\\python.exe main_orchestrator.py go6    # Agent 4+5 (Linter + Évaluateur)
    C:\\ProgramData\\anaconda3\\python.exe main_orchestrator.py status  # etat du pipeline
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent / "src"))

from agent_base import load_config, project_root  # noqa: E402
from agent_redacteur import RedacteurAgent  # noqa: E402
from agent_iconographe import IconographeAgent  # noqa: E402
from agent_architecte import ArchitecteAgent  # noqa: E402
from agent_animateur import AnimateurAgent  # noqa: E402
from agent_linter import LinterAgent  # noqa: E402
from agent_evaluateur import EvaluateurAgent  # noqa: E402

STATUS_FILE = "logs/status.json"


def _load_status() -> dict:
    path = project_root() / STATUS_FILE
    if path.exists():
        return json.loads(path.read_text(encoding="utf-8"))
    return {"created": None, "steps": {}}


def _save_status(status: dict) -> None:
    path = project_root() / STATUS_FILE
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(status, ensure_ascii=False, indent=2), encoding="utf-8")


def _mark(step: str, report: dict) -> None:
    status = _load_status()
    status["created"] = status["created"] or _dt.datetime.now().isoformat(timespec="seconds")
    status["steps"][step] = {"at": _dt.datetime.now().isoformat(timespec="seconds"), "report": report}
    _save_status(status)


def cmd_init() -> int:
    config = load_config()
    print(f"[init] Ecosysteme AGIRH initialise (env base conda, {config['meta']['environnement']})")
    print(f"[init] Structure: {sorted(p.name for p in (project_root()).iterdir() if p.is_dir())}")
    _mark("GO1", {"ok": True, "env": "base"})
    return 0


def cmd_go2() -> int:
    report = RedacteurAgent().run()
    _mark("GO2", report)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report["status"] == "OK" else 1


def cmd_go3() -> int:
    report = IconographeAgent().run()
    _mark("GO3", report)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report["status"] == "OK" else 1


def cmd_go4() -> int:
    report = ArchitecteAgent().run()
    _mark("GO4", report)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report["status"] == "OK" else 1


def cmd_go5() -> int:
    report = AnimateurAgent().run()
    _mark("GO5", report)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report["status"] == "OK" else 1


def cmd_go6() -> int:
    linter = LinterAgent().run()
    _mark("GO6a_linter", linter)
    evaluator = EvaluateurAgent().run()
    _mark("GO6b_evaluateur", evaluator)
    print(json.dumps({"linter": linter, "evaluateur": evaluator}, ensure_ascii=False, indent=2))
    return 0 if linter["status"] == "OK" else 2


def cmd_status() -> int:
    status = _load_status()
    print(json.dumps(status, ensure_ascii=False, indent=2))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Orchestrateur du pipeline multi-agents AGIRH (docs/presentation).")
    parser.add_argument("step", choices=["init", "go2", "go3", "go4", "go5", "go6", "status"],
                        help="Etape a executer (GO 1 -> init, GO 2..GO 6 -> agents).")
    args = parser.parse_args()
    return {
        "init": cmd_init,
        "go2": cmd_go2,
        "go3": cmd_go3,
        "go4": cmd_go4,
        "go5": cmd_go5,
        "go6": cmd_go6,
        "status": cmd_status,
    }[args.step]()


if __name__ == "__main__":
    raise SystemExit(main())
