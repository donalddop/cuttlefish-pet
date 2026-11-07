# /// script
# requires-python = ">=3.11"
# ///
"""Lees het kerkhof uit.

Een levende tank laat je alleen de overlevenden zien, dus daar is selectie per
definitie onzichtbaar: de dieren die het slecht deden zijn juist degene die er niet
meer zijn. Dit leest de andere helft van het verhaal -- wat elk dier meekreeg, hoe
lang het het volhield, hoe goed het at en hoeveel eieren het naliet.

    uv run Tools/graveyard.py
    uv run Tools/graveyard.py --since "2026-11-03 03:31"

Met --since telt het alleen dieren die op of na dat moment geboren zijn (lokale
tijd), bv. de basislijn na een release: wie al leefde vóór de wissel telt niet mee.

De vraag die het beantwoordt: welke eigenschappen komen bovendrijven op dit
bureaublad? Daarvoor vergelijkt het de dieren die nakomelingen kregen met de rest.
Bij weinig doden zegt dat nog niets -- de spreiding staat erbij, zodat je ziet
wanneer een verschil groter is dan de ruis.
"""

import argparse
import json
import os
import statistics
from datetime import datetime
from collections import Counter

TRAITS = ["Boldness", "Sociability", "Curiosity", "Metabolism", "Restlessness"]
DUTCH = {
    "Boldness": "lef",
    "Sociability": "sociaal",
    "Curiosity": "nieuwsgierig",
    "Metabolism": "stofwisseling",
    "Restlessness": "rusteloos",
}


def default_path() -> str:
    return os.path.join(os.environ["LOCALAPPDATA"], "CuttlefishPet", "graveyard.jsonl")


def args(argv=None):
    ap = argparse.ArgumentParser(description="Lees het kerkhof uit.")
    ap.add_argument("file", nargs="?", default=None, help="graveyard.jsonl (standaard: %%LOCALAPPDATA%%)")
    ap.add_argument("--since", type=since_moment, default=None, metavar="DATUM[ TIJD]",
                    help='alleen dieren geboren op of na dit moment, bv. "2026-11-03 03:31"')
    return ap.parse_args(argv)


def since_moment(text):
    try:
        return datetime.fromisoformat(text.strip())
    except ValueError:
        raise argparse.ArgumentTypeError(f"geen datum/tijd: {text!r} (gebruik bv. 2026-11-03 03:31)")


def born_since(dead, since):
    """Keep the graves of animals born at or after `since` (local time).

    Graves without a birth stamp (from before it was recorded) are left out,
    because there is no telling which side of the line they were born on.
    """
    kept, unknown = [], 0
    for d in dead:
        born = moment(d.get("Born"))
        if born is None:
            unknown += 1
        elif born >= since:
            kept.append(d)
    return kept, unknown


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


def moment(stamp):
    """Parse a timestamp from the file, or None.

    The app writes seven digits of fractional seconds and Python will take six,
    so the tail is trimmed rather than the whole line thrown away.
    """
    if not stamp:
        return None
    text = stamp.rstrip("Z")
    if "." in text:
        head, _, frac = text.partition(".")
        text = head + "." + frac[:6]
    try:
        return datetime.fromisoformat(text)
    except ValueError:
        return None


def reproduction(dead):
    """The baseline that says whether a change to breeding broke anything.

    Births are not recorded anywhere, but in a tank that is neither growing nor
    collapsing deaths are the same number, and deaths are recorded exactly. So
    the rate of turnover stands in for the rate of breeding -- and it is the
    number to write down before touching how breeding works, because afterwards
    there is no way back to it.
    """
    stamps = sorted(m for m in (moment(d.get("Died")) for d in dead) if m)
    if len(stamps) < 2:
        print("\nvoortplanting: te weinig data")
        return

    span = (stamps[-1] - stamps[0]).total_seconds() / 3600
    recent = [m for m in stamps if (stamps[-1] - m).total_seconds() <= 24 * 3600]
    recent_span = max(0.02, (stamps[-1] - recent[0]).total_seconds() / 3600)

    ages = [d["Age"] / 60 for d in dead if d.get("Age")]
    eggs = [d.get("Offspring", 0) for d in dead]
    breeders = [e for e in eggs if e > 0]

    print("\nvoortplanting")
    print(f"  laatste 24 uur : {len(recent)} doden over {recent_span:.1f} uur"
          f"  =  {len(recent) / recent_span:.1f} per uur")
    if span > 0:
        print(f"  hele kerkhof   : {len(stamps)} doden over {span:.1f} uur"
              f"  =  {len(stamps) / span:.1f} per uur")
    if ages:
        print(f"  leeftijd       : gemiddeld {statistics.mean(ages):.1f} min"
              f"  (spreiding {spread(ages):.1f})")
    share = 100.0 * len(breeders) / len(eggs) if eggs else 0
    print(f"  eieren         : gemiddeld {statistics.mean(eggs):.2f} per dier,"
          f" {share:.0f}% liet er ooit een na")
    if breeders:
        print(f"                   en wie dat deed, gemiddeld {statistics.mean(breeders):.1f}")

    # Since 2 Nov only females lay. Graves from before that carry no sex, so
    # this line only counts the animals that lived under the new rules -- it is
    # the number to hold against the old 36% and the predicted 18%.
    sexed = [d for d in dead if d.get("Female") is not None]
    if sexed:
        females = [d for d in sexed if d["Female"]]
        laid = [d for d in sexed if d.get("Offspring", 0) > 0]
        print(f"  met geslacht   : {len(sexed)} dieren, {len(females)} vrouwtjes;"
              f" {100.0 * len(laid) / len(sexed):.0f}% liet ooit een ei na"
              f" (oud: 36%, zonder compensatie verwacht ~18%)")
        if females:
            eggs_f = [d.get("Offspring", 0) for d in females]
            print(f"                   vrouwtjes gemiddeld {statistics.mean(eggs_f):.2f} eieren")


def main():
    opts = args()
    fn = opts.file or default_path()
    if not os.path.exists(fn):
        print(f"nog geen kerkhof op {fn}")
        return 0

    dead = load(fn)
    if opts.since:
        total = len(dead)
        dead, unknown = born_since(dead, opts.since)
        print(f"alleen geboren sinds {opts.since:%Y-%m-%d %H:%M}: {len(dead)} van de {total}"
              f" (zonder geboortedatum: {unknown})")
    if not dead:
        print("kerkhof is leeg" if not opts.since else "geen dieren geboren sinds dat moment")
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

    reproduction(dead)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
