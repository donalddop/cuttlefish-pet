# /// script
# requires-python = ">=3.11"
# ///
"""Lees het kerkhof uit.

Een levende tank laat je alleen de overlevenden zien, dus daar is selectie per
definitie onzichtbaar: de dieren die het slecht deden zijn juist degene die er niet
meer zijn. Dit leest de andere helft van het verhaal -- wat elk dier meekreeg, hoe
lang het het volhield, hoe goed het at en hoeveel eieren het naliet.

    uv run Tools/graveyard.py

De vraag die het beantwoordt: welke eigenschappen komen bovendrijven op dit
bureaublad? Daarvoor vergelijkt het de dieren die nakomelingen kregen met de rest.
Bij weinig doden zegt dat nog niets -- de spreiding staat erbij, zodat je ziet
wanneer een verschil groter is dan de ruis.
"""

import json
import os
import statistics
import sys
from collections import Counter

TRAITS = ["Boldness", "Sociability", "Curiosity", "Metabolism", "Restlessness"]
DUTCH = {
    "Boldness": "lef",
    "Sociability": "sociaal",
    "Curiosity": "nieuwsgierig",
    "Metabolism": "stofwisseling",
    "Restlessness": "rusteloos",
}


def path() -> str:
    if len(sys.argv) > 1:
        return sys.argv[1]
    return os.path.join(os.environ["LOCALAPPDATA"], "CuttlefishPet", "graveyard.jsonl")


def load(fn):
    dead = []
    with open(fn, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    dead.append(json.loads(line))
                except json.JSONDecodeError:
                    pass          # een afgekapte laatste regel is geen ramp
    return dead


def spread(values):
    return statistics.pstdev(values) if len(values) > 1 else 0.0


def main():
    fn = path()
    if not os.path.exists(fn):
        print(f"nog geen kerkhof op {fn}")
        return 0

    dead = load(fn)
    if not dead:
        print("kerkhof is leeg")
        return 0

    print(f"{len(dead)} zeekatten begraven")
    print(f"  van {dead[0].get('Died', '?')[:19]} tot {dead[-1].get('Died', '?')[:19]}")
    print()

    print("hoe het afliep")
    for fate, n in Counter(d.get("Fate", "onbekend") for d in dead).most_common():
        print(f"  {fate:<16} {n:>4}  {n / len(dead):>5.0%}")
    print()

    ages = [d["Age"] / 60 for d in dead if d.get("Age")]
    meals = [d.get("Meals", 0) for d in dead]
    eggs = [d.get("Offspring", 0) for d in dead]
    print("leven")
    print(f"  leeftijd      {statistics.mean(ages):>6.1f}m  (spreiding {spread(ages):.1f})")
    print(f"  maaltijden    {statistics.mean(meals):>6.1f}")
    print(f"  eieren        {statistics.mean(eggs):>6.1f}   "
          f"{sum(1 for e in eggs if e > 0)} van de {len(dead)} kwam aan voortplanting toe")
    print()

    bred = [d for d in dead if d.get("Offspring", 0) > 0]
    barren = [d for d in dead if d.get("Offspring", 0) == 0]
    print("wat drijft er boven")
    if len(bred) < 5 or len(barren) < 5:
        print(f"  te weinig om iets te zeggen ({len(bred)} met nakomelingen, "
              f"{len(barren)} zonder) -- laat de tank langer lopen")
    else:
        print(f"  {'eigenschap':<16}{'met eieren':>11}{'zonder':>9}{'verschil':>10}   ruis")
        for t in TRAITS:
            a = [d["Genome"][t] for d in bred if t in d.get("Genome", {})]
            b = [d["Genome"][t] for d in barren if t in d.get("Genome", {})]
            if not a or not b:
                continue
            diff = statistics.mean(a) - statistics.mean(b)
            noise = (spread(a) + spread(b)) / 2
            mark = "  <-- " if noise and abs(diff) > noise * 0.5 else ""
            print(f"  {DUTCH[t]:<16}{statistics.mean(a):>11.2f}{statistics.mean(b):>9.2f}"
                  f"{diff:>+10.2f}   {noise:.2f}{mark}")
        print()
        print("  een pijl betekent: het verschil is groter dan de helft van de spreiding.")
        print("  geen pijl betekent niet 'geen effect', alleen 'nog niet te onderscheiden'.")
    print()

    held = Counter()
    for d in dead:
        for behaviour, worth in (d.get("Learned") or {}).items():
            if abs(worth) > 0.15:
                held[behaviour] += 1
    if held:
        print("waar ze het over eens werden")
        for behaviour, n in held.most_common(8):
            worths = [d["Learned"][behaviour] for d in dead
                      if behaviour in (d.get("Learned") or {})]
            print(f"  {behaviour:<14} {n:>4} dieren   gemiddeld {statistics.mean(worths):+.2f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
