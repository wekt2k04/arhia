# -*- coding: utf-8 -*-
"""AgentBase - classe commune des 5 agents de generation PPTX AGIRH.

Conformite : bonnes_pratique_rédaction_ppt.txt (100 regles).
Chaque agent = module Python deterministe (pas de dependance LLM/langchain).
"""
from __future__ import annotations

import abc
import datetime as _dt
import json
import logging
import os
import re
import sys
from pathlib import Path


def project_root() -> Path:
    """Racine du pipeline = dossier contenant ce package (docs/presentation/)."""
    return Path(__file__).resolve().parents[1]


def load_config() -> dict:
    cfg_path = project_root() / "agents_config.json"
    with open(cfg_path, encoding="utf-8") as fh:
        return json.load(fh)


class AgentBase(abc.ABC):
    """Contrat commun : config + logging + interface run() + rapport."""

    name = "agent_base"

    def __init__(self, config: dict | None = None, log_level: int = logging.INFO):
        self.config = config or load_config()
        self.root = project_root()
        self.agent_id = getattr(self, "agent_id", self.name)
        # Console Windows cp1252 : ne jamais crasher sur un caractere non encodable
        for stream in (sys.stdout, sys.stderr):
            try:
                stream.reconfigure(errors="replace")
            except Exception:
                pass
        self.logger = logging.getLogger(self.agent_id)
        self.logger.setLevel(log_level)
        if not self.logger.handlers:
            _console = logging.StreamHandler(sys.stdout)
            _console.setFormatter(logging.Formatter("[%(name)s] %(levelname)s - %(message)s"))
            self.logger.addHandler(_console)
        self.logs_dir = self.root / "logs"
        self.logs_dir.mkdir(parents=True, exist_ok=True)
        self._file_handler: logging.FileHandler | None = None
        self._enable_file_log()

    # ------------------------------------------------------------------
    # Logging
    # ------------------------------------------------------------------
    def _enable_file_log(self) -> None:
        stamp = _dt.datetime.now().strftime("%Y%m%d_%H%M%S")
        path = self.logs_dir / f"{self.agent_id}_{stamp}.log"
        self._file_handler = logging.FileHandler(path, encoding="utf-8")
        self._file_handler.setFormatter(
            logging.Formatter("%(asctime)s - %(name)s - %(levelname)s - %(message)s")
        )
        self.logger.addHandler(self._file_handler)

    def log(self, msg: str, level: int = logging.INFO) -> None:
        self.logger.log(level, msg)

    def info(self, msg: str) -> None:
        self.log(msg, logging.INFO)

    def warn(self, msg: str) -> None:
        self.log(msg, logging.WARNING)

    def error(self, msg: str) -> None:
        self.log(msg, logging.ERROR)

    # ------------------------------------------------------------------
    # Regles PPT (references vers bonnes_pratique_rédaction_ppt.txt)
    # ------------------------------------------------------------------
    @staticmethod
    def _rules_text() -> str:
        rules_path = project_root().parent / "bonnes_pratique_rédaction_ppt.txt"
        return rules_path.read_text(encoding="utf-8")

    def load_rules(self, numbers: list[int]) -> dict[int, str]:
        """Extrait le texte des regles demandees (ex: [10, 21]) du fichier TXT."""
        text = self._rules_text()
        out: dict[int, str] = {}
        for n in numbers:
            m = re.search(rf"^{n}\.\s+(.*)$", text, flags=re.MULTILINE)
            out[n] = m.group(1) if m else "(regle introuvable)"
        return out

    def ensure_rule(self, rule_number: int, requirement: str) -> None:
        """Journalise qu'une regle du fichier PPT est appliquee/verifiee."""
        self.info(f"Regle {rule_number}: {requirement}")

    # ------------------------------------------------------------------
    # Persistance
    # ------------------------------------------------------------------
    def write_json(self, relative_path: str, data: object) -> Path:
        target = self.root / relative_path
        target.parent.mkdir(parents=True, exist_ok=True)
        with open(target, "w", encoding="utf-8") as fh:
            json.dump(data, fh, ensure_ascii=False, indent=2)
        self.info(f"Ecriture JSON: {target}")
        return target

    def read_json(self, relative_path: str) -> object:
        target = self.root / relative_path
        with open(target, encoding="utf-8") as fh:
            return json.load(fh)

    def make_report(self, status: str, detail: dict) -> dict:
        return {"agent": self.agent_id, "status": status, "detail": detail}

    # ------------------------------------------------------------------
    # Interface
    # ------------------------------------------------------------------
    @abc.abstractmethod
    def run(self) -> dict:
        """Execute la mission de l'agent. Retourne un rapport dict."""
