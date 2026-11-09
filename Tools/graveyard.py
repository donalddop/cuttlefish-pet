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
    uv run Tools/graveyard.py --since "2026-11-03 03:31" --until "2026-11-05 12:00"
    uv run Tools/graveyard.py --selftest

Met --since telt het alleen dieren die op of na dat moment geboren zijn (lokale
tijd), bv. de basislijn na een release: wie al leefde vóór de wissel telt niet mee.
Met --until telt het alleen dieren die vóór dat moment geboren zijn; samen bakenen
ze een venster af, bv. alleen de 2.10.0-periode tegenover 2.10.1.
--selftest controleert since/until, het inlezen van tijden en regels en de
samenvattingsregel op nepdata en raakt het echte kerkhof niet aan.
Bovenaan staat altijd één samenvattingsregel (dieren, % legde ooit, eieren per
dier), zodat je twee vensters makkelijk naast elkaar zet.

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
    ap.add_argument("--until", type=since_moment, default=None, metavar="DATUM[ TIJD]",
                    help='alleen dieren geboren vóór dit moment, bv. "2026-11-05 12:00"')
    ap.add_argument("--selftest", action="store_true",
                    help="controleer --since/--until op nepgraven en stop")
    opts = ap.parse_args(argv)
    if opts.since and opts.until and opts.until <= opts.since:
        ap.error("--until moet na --since liggen")
    return opts


def since_moment(text):
    try:
        return datetime.fromisoformat(text.strip())
    except ValueError:
        raise argparse.ArgumentTypeError(f"geen datum/tijd: {text!r} (gebruik bv. 2026-11-03 03:31)")


def born_between(dead, since=None, until=None):
    """Keep the graves of animals born at or after `since` and before `until`.

    Either end may be None (open). Times are local. Graves without a birth stamp
    (from before it was recorded) are left out, because there is no telling which
    side of the line they were born on.
    """
    kept, unknown = [], 0
    for d in dead:
        born = moment(d.get("Born"))
        if born is None:
            unknown += 1
        elif (since is None or born >= since) and (until is None or born < until):
            kept.append(d)
    return kept, unknown


def born_since(dead, since):
    """Keep the graves of animals born at or after `since` (local time)."""
    return born_between(dead, since=since)


def selftest_moment_load_summary():
    """Check moment(), load() and summary() on made-up input; returns the number of failures."""
    import tempfile

    failed = 0

    def check(name, ok, got):
        nonlocal failed
        failed += not ok
        print(f"  {'ok  ' if ok else 'FOUT'} {name}: {got}")

    m = moment("2026-11-03T03:31:00.1234567")
    check("moment 7 cijfers", m == datetime(2026, 11, 3, 3, 31, 0, 123456), m)
    m = moment("2026-11-03T03:31:00Z")
    check("moment met Z", m == datetime(2026, 11, 3, 3, 31), m)
    m = moment("2026-11-03T03:31:00.5Z")
    check("moment fractie + Z", m == datetime(2026, 11, 3, 3, 31, 0, 500000), m)
    for junk in ("rommel", "", None, "2026-13-40T99:00:00"):
        m = moment(junk)
        check(f"moment {junk!r}", m is None, m)

    with tempfile.TemporaryDirectory() as tmp:
        fn = os.path.join(tmp, "graveyard.jsonl")
        with open(fn, "w", encoding="utf-8") as f:
            f.write('{"Offspring": 1}\n\n   \n{"Offspring": 0}\n{"Offspring": 2, "Bo')
        dead = load(fn)
        check("load slaat lege + afgekapte regel over", [d["Offspring"] for d in dead] == [1, 0], dead)
        with open(fn, "w", encoding="utf-8") as f:
            f.write("")
        check("load leeg bestand", load(fn) == [], load(fn))

    fake = [{"Offspring": 0}, {"Offspring": 3}, {"Offspring": 1}, {}]
    got = summary(fake)
    check("samenvatting", got == "samenvatting: 4 dieren · 50.0% legde ooit · 1.00 eieren per dier", got)
    got = summary([])
    check("samenvatting leeg", got == "samenvatting: 0 dieren", got)
    return failed


def selftest():
    """Check the since/until window on a handful of fake graves."""
    fake = [
        {"Born": "2026-11-03T03:30:59.9999999", "Offspring": 0},   # just before since
        {"Born": "2026-11-03T03:31:00.0000000", "Offspring": 1},   # exactly since: in
        {"Born": "2026-11-04T10:00:00.1234567", "Offspring": 2},   # inside
        {"Born": "2026-11-05T11:59:59.0000000", "Offspring": 0},   # just before until: in
        {"Born": "2026-11-05T12:00:00.0000000", "Offspring": 3},   # exactly until: out
        {"Offspring": 5},                                         # no birth stamp
        {"Born": "rommel", "Offspring": 0},                        # unreadable stamp
    ]
    since = datetime(2026, 11, 3, 3, 31)
    until = datetime(2026, 11, 5, 12, 0)
    checks = [
        ("since + until", born_between(fake, since, until), [1, 2, 0], 2),
        ("alleen since", born_between(fake, since=since), [1, 2, 0, 3], 2),
        ("alleen until", born_between(fake, until=until), [0, 1, 2, 0], 2),
        ("geen grenzen", born_between(fake), [0, 1, 2, 0, 3], 2),
        ("born_since", born_since(fake, since), [1, 2, 0, 3], 2),
    ]
    failed = 0
    for name, (kept, unknown), want, want_unknown in checks:
        got = [d["Offspring"] for d in kept]
        ok = got == want and unknown == want_unknown
        failed += not ok
        print(f"  {'ok  ' if ok else 'FOUT'} {name}: {got} (zonder datum {unknown})")
    failed += selftest_moment_load_summary()
    try:
        args(["--since", "2026-11-05", "--until", "2026-11-03"])
        print("  FOUT until vóór since werd geaccepteerd")
        failed += 1
    except SystemExit:
        print("  ok   until vóór since wordt geweigerd")
    print("selftest geslaagd" if not failed else f"selftest: {failed} fout(en)")
    return 1 if failed else 0


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


def summary(dead):
    """One line to hold two windows side by side: animals, % that ever laid, eggs per animal."""
    if not dead:
        return "samenvatting: 0 dieren"
    eggs = [d.get("Offspring", 0) for d in dead]
    laid = sum(1 for e in eggs if e > 0)
    return (f"samenvatting: {len(dead)} dieren · {100.0 * laid / len(dead):.1f}% legde ooit"
            f" · {statistics.mean(eggs):.2f} eieren per dier")


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
    if opts.selftest:
        return selftest()
    fn = opts.file or default_path()
    if not os.path.exists(fn):
        print(f"nog geen kerkhof op {fn}")
        return 0

    dead = load(fn)
    window = opts.since or opts.until
    if window:
        total = len(dead)
        dead, unknown = born_between(dead, opts.since, opts.until)
        parts = []
        if opts.since:
            parts.append(f"sinds {opts.since:%Y-%m-%d %H:%M}")
        if opts.until:
            parts.append(f"vóór {opts.until:%Y-%m-%d %H:%M}")
        print(f"alleen geboren {' en '.join(parts)}: {len(dead)} van de {total}"
              f" (zonder geboortedatum: {unknown})")
    if not dead:
        print("kerkhof is leeg" if not window else "geen dieren geboren in dat venster")
        return 0

    print(summary(dead))
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
