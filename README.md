# Cuttlefish Pet 🦑

Een eSheep/Shimeji-achtige desktop pet voor Windows — maar dan een **zeekat**.
Zwemt over je scherm, zit op de taskbar, klimt tegen vensters op, rijdt mee als je ze
versleept, reageert op je muis en toetsenbord, en camoufleert zich door letterlijk de
pixels achter zich als huid aan te trekken.

## Bouwen & draaien

```bash
dotnet run --project CuttlefishPet
```

Vereist .NET 8 SDK (Windows). De app verschijnt als tray-icoon (het zeekatje):
links- of rechtsklik voor **Add cuttlefish / Remove one / Toss a shrimp / Mute sounds / Exit**.

Alles is ook vanaf de commandline te doen — de tweede start stuurt het commando naar de
draaiende instantie (handig vanaf je telefoon via dispatch):

```bash
CuttlefishPet.exe shrimp
```

Commando's: `add`, `remove`, `shrimp`, `mute`, `exit`.

## Gedrag

**Met jou**

| Interactie | Wat er gebeurt |
|---|---|
| Cursor bewegen | de pupillen volgen je cursor; hij knippert af en toe |
| Cursor stil laten staan | hij besluipt hem met de *passing cloud*-display en slaat toe met zijn vangtentakels |
| Snel op hem afvliegen | schrikt en schiet weg met jet-aandrijving |
| Oppakken & gooien | hangt aan je cursor; bij een harde worp een inktwolk |
| Dubbelklikken | aaien → roze blos, luchtbelletjes en een blij dansje |
| Typen | tentakel-gewiebel op het ritme |
| Garnaal gooien (tray/CLI) | alle zeekatten duiken erop af, de winnaar eet hem op |
| Een tijdje wegblijven | ze gaan slapen; bij terugkomst rekken ze zich uit |

**Met je systeem**

| Interactie | Wat er gebeurt |
|---|---|
| Vensterranden | klimt tegen de zijkant op en gaat op de titelbalk zitten |
| Venster verslepen | rijdt mee; sluit je het, dan valt hij |
| Nieuw venster opent naast hem | schrikt zich een hoedje |
| Vensterrand / taskbar | gluurt over de rand naar beneden |
| Taskbar | wandelt erop rond en camoufleert zich soms als nep-icoontje |

**Onderling**

Twee zeekatten die elkaar tegenkomen doen een zebra-strepen imponeerdisplay tot er
één met een inktwolk afdruipt.

## Structuur

- `CuttlefishPet/Core` — Pet, physics, PetManager, WorldState, garnalen, commandoserver
- `CuttlefishPet/Behaviors` — statemachine + alle gedragingen
- `CuttlefishPet/Interop` — Win32 P/Invoke: venster-enumeratie, taskbar, globale muis/toetsenbord-hooks
- `CuttlefishPet/Rendering` — transparant klikdoorlatend overlay-venster, sprite-renderer met oog-overlay, screen sampler (camouflage)
- `Tools/generate_sprites.py` — alle sprites, het tray-icoon en een contactsheet (`uv run`)
- `Tools/generate_sounds.py` — procedurele geluidjes (standaard gedempt)
- `Tools/check_eye_alignment.py` — controleert of de pupil-overlay in elk gedrag netjes in het oog valt

## Eigen art

Sprites zijn 64×64 frame-strips (`Assets/sprites/*.png`) met metadata in `animations.json`:
`fps`, `loop`, `anchor` (voetpunt) en optioneel `eye` (\[x, y, straal\]) voor de losse
pupil-overlay. Vervang de PNG's en pas de metadata aan — geen code-wijziging nodig.
Gedrag-gewichten staan in `Assets/behaviors.json`. Debuglog: `%TEMP%\cuttlefishpet-debug.log`.
