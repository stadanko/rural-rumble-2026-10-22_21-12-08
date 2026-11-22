"""Planted-violation demo: start the loop from deliberately off-style drafts instead of the Generator.

The two fixtures in fixtures/ break almost every rule on purpose (a neon city mobile home at
night under snowy mountains; trash talk about pinfalls, title belts, piledrivers and Hulk Hogan).
The loop gets no hints about what is wrong: the Evaluator and the automated checks have to find
each violation, and the Refiner has to fix it, with no human input. Results go to
reports/runs/planted_demo.{json,md}; outputs/ is left alone.

    python Tools/StyleAgent/demo_planted.py      # exit 0 = every planted draft was fixed and approved
"""
import json
import sys
from pathlib import Path

import loop

HERE = Path(__file__).resolve().parent
SEEDS = {"trailer_backdrop": HERE / "fixtures" / "off_style_trailer.json",
         "trash_talk_prematch": HERE / "fixtures" / "off_style_trash_talk.json"}


def main():
    seeds = {k: json.loads(p.read_text(encoding="utf-8")) for k, p in SEEDS.items()}
    report = loop.run(list(seeds), seeds=seeds, write_outputs=False, report_name="planted_demo",
                      note="**Planted-violation demo.** Iteration 0 of each task is a hand-written, deliberately "
                           "off-style draft from `fixtures/`, not Generator output. Everything after it is the "
                           "agent working alone.")
    return 0 if all(t["status"] == "approved" for t in report["tasks"]) else 1


if __name__ == "__main__":
    sys.exit(main())
