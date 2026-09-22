# Cuttlefish Pet 🦑

Desktop pets voor Windows, maar dan **zeekatten**. Je scherm is hun aquarium: ze
zweven er vrij doorheen, strijken neer op je taskbar en vensterranden, klimmen langs
de schermrand omhoog, hangen ondersteboven aan het plafond, en veranderen voortdurend
van kleur en patroon zoals echte zeekatten dat doen.

## Installeren

Download `CuttlefishPet-setup.exe` bij de [laatste release](https://github.com/donalddop/cuttlefish-pet/releases/latest)
en dubbelklik hem. Geen .NET nodig, geen beheerdersrechten, geen UAC-prompt — hij
installeert per gebruiker in `%LOCALAPPDATA%`. Windows toont eenmalig een
SmartScreen-waarschuwing omdat het programma niet ondertekend is: *Meer informatie*
→ *Toch uitvoeren*.

Liever draagbaar? Pak `CuttlefishPet-standalone.zip` uit en start `CuttlefishPet.exe`.

De app verschijnt als tray-icoon (het zeekatje): links- of rechtsklik voor het menu.

## Zelf bouwen

```bash
dotnet run --project CuttlefishPet
```

Vereist .NET 8 SDK (Windows 10/11). De installer bouw je met
[Inno Setup 6](https://jrsoftware.org/isinfo.php):

```bash
dotnet publish CuttlefishPet -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\CuttlefishPet
copy Installer\LEES-MIJ.txt publish\LEES-MIJ.txt
ISCC.exe Installer\CuttlefishPet.iss
```

`publish/` is gitignored, dus de handleiding die met de release meegaat staat in
`Installer/LEES-MIJ.txt` en wordt bij het bouwen gekopieerd.

Ook aanstuurbaar vanaf de commandline — een tweede start stuurt het commando door
naar de draaiende instantie:

```bash
CuttlefishPet.exe shrimp
```

Commando's: `add`, `remove`, `cull`, `shrimp`, `population`, `hunter`, `mute`, `exit`.

En een paar die er zijn omdat zeldzame dingen anders niet te controleren zijn:
`strike` stuurt een jager die niet mis grijpt, `bones` legt de kleinste, middelste
en grootste cuttlebone naast elkaar, en `reach` laat de drie grootste hun
vangtentakel uitsteken. Alle drie duren in het wild een fractie van een seconde en
gebeuren wanneer het dier het wil.

## Wat ze doen

**Zwemmen en rusten**

Vrij rondzwemmen door het hele beeld, zweven, sprintjes trekken met straalaandrijving,
en bewust ergens neerstrijken: op de taskbar, op een titelbalk, tegen een schermrand of
ondersteboven aan het plafond. Vanaf een richel kunnen ze omhoog klimmen, omlaag
roetsjen, over de rand gluren, eraan bungelen en zich wegkatapulteren.

**Met jou**

| Interactie | Wat er gebeurt |
|---|---|
| Cursor bewegen | de pupillen volgen je cursor; ze knipperen uit zichzelf |
| Cursor stil laten staan | besluipen met de *passing cloud*-display, dan toeslaan met de vangtentakels |
| Snel op ze afvliegen | schrikken, inkt spuiten en wegschieten |
| Oppakken & gooien | hangen aan je cursor; hard gooien geeft een inktwolk, heel hard gooien sterretjes |
| Te vaak oppakken | ze worden pikzwart, flitsen zebrastrepen en stampvoeten |
| Dubbelklikken | aaien → roze blos, luchtbelletjes, blij dansje |
| Typen | ze komen bij je tekstcursor kijken en wiebelen mee op je toetsaanslagen |
| Scrollen | de "onderstroom" sleurt de zwemmers mee |
| Garnaal gooien | ze duiken er allemaal op af; de winnaar eet hem op |
| Wegblijven | na een paar minuten gaan ze slapen, en rekken zich uit als je terugkomt |

**Met je systeem**

Meeliften op je muiscursor, een waterstraaltje richting je cursor spuiten, tegen een
venster duwen zodat het écht verschuift, doen alsof ze de sluitknop indrukken, naar de
klok in je systeemvak zwemmen om geeuwend te kijken hoe laat het is, en schrikken als
er een venster naast ze opengaat. Zit een zeekat op een venster dat je versleept, dan
rijdt hij mee.

**Camouflage en kleur**

Elke zeekat heeft een eigen kleurenpalet (paars, blauw, groen, parelmoer, inktzwart —
dertien in totaal), een huidpatroon (vlekjes, blotches, dwarsbanden, netpatroon,
iriserende spikkels) en een parelmoerglans die over de huid schuift. Ze wisselen
continu van kleur, en hun stemming bepaalt mee welke: bleek van schrik, rood tijdens
de jacht, pikzwart bij ruzie. Soms geven ze een pure kleurenshow weg.

De echte camouflage gaat verder: dan maken ze een schermgreep van de achtergrond
achter zich en dragen die als huid, plat tegen de ondergrond gedrukt — op de taskbar
soms vermomd als icoontje.

**Klassieke eSheep-trucs**

Zich ingraven in de taskbar en elders weer opduiken, een eitros leggen waar een nieuwe
zeekat uit komt, zichzelf in een inktwolk laten verdwijnen en ergens anders
materialiseren, meeliften aan een grote luchtbel, als doorzichtig spook wegdrijven, een
inktvlekje achterlaten, knabbelen aan een vensterrand, en zomaar een statische schok
krijgen.

**Onderling**

Twee zeekatten die elkaar tegenkomen imponeren elkaar met zebrastrepen tot er één met
een inktwolk afdruipt. Anders zwemmen ze in formatie naast elkaar, houden ze een
sprintwedstrijd over het scherm, of kruipen ze tegen elkaar aan.

**Levenscyclus**

Wie komt aanzwemmen krijgt 26 tot 50 minuten, wie uit een tros kruipt 18 tot 28 --
en drukte laat die klok sneller lopen. Gaat een balts goed, dan zoekt ze een richel en legt een
eitros; dat kost haar bijna al haar resterende tijd, precies zoals bij echte zeekatten.
Uit de tros kruipt na een halve minuut een klein exemplaar op een derde van
volwassen formaat, dat langzaam uitgroeit — jonge dieren beheersen camouflage nog niet,
dus die zie je in kleur rondzwemmen. Als het einde komt trekt de kleur weg en zinkt hij
met een sliert luchtbelletjes uit beeld.

Drukte versnelt ieders klok en een tros komt alleen uit als er ruimte is, dus de groep
regelt zijn eigen omvang. Loopt het toch vol: **Thin them out** in het tray-menu.

**Er komt weleens iets groters langs**

Om de acht tot twintig minuten steekt er een haai of een dolfijn over -- om de beurt,
zodat geen van beide behang wordt. Hij kijkt twee keer per seconde rond en kiest het
dier dat het meest opvalt: felle huid, snelheid en formaat maken je zichtbaar,
camouflage en stilzitten op een richel verbergen je. Dan schiet hij toe.

Eerst een korte jacht -- hij kiest er eentje uit, die vlucht, en pas als hij binnen
bereik is gaat de bek open. Meestal klapt die op niets dicht. Lukt het wel, dan wordt
de zeekat de bek in getrokken en verdwijnt hij erin; het schelpje wordt er daarna
weer uitgespuugd, wat ook de reden is dat cuttlebones op stranden aanspoelen. Na een
vangst is het bezoek voorbij. Wie in de buurt is reageert naar eigen aard: de brutale dieren draaien zich breed en
imponeren, de rest inkt of vlucht, en wie al gecamoufleerd zit blijft simpelweg
doodstil -- wat meteen het meest werkt. Dit is de enige selectiedruk in de tank:
tot nu toe ging iedereen dood aan ouderdom, en dan valt er aan het kerkhof niets af
te lezen.

## Structuur

- `CuttlefishPet/Core` — Pet, aquarium-physics, PetManager, WorldState, garnalen, props, roofdieren, commandoserver
- `CuttlefishPet/Behaviors` — statemachine plus alle gedragingen, gegroepeerd per thema
- `CuttlefishPet/Interop` — Win32 P/Invoke: vensters, taskbar, klok, tekstcursor, globale muis/toetsenbord-hooks
- `CuttlefishPet/Rendering` — klikdoorlatend overlay-venster, sprite-renderer met oog-, huid- en glanslagen, kleurpaletten, screen sampler
- `Tools/` — generatoren voor sprites, huidtexturen, geluid en het tray-icoon, plus previewscripts (`uv run`)
- `Tools/DriveTests/` — controles op het genoom, de drijfveren en de gedragsweging (`dotnet run --project Tools/DriveTests`)
- `Tools/graveyard.py` — leest het kerkhof uit en laat zien welke eigenschappen bovendrijven (`uv run Tools/graveyard.py`)

De tank zelf staat in `%LocalAppData%\CuttlefishPet\tank.json`: genen, leeftijden, wat elk dier
geleerd heeft en wat ze van elkaar vinden. Hij wordt elke minuut weggeschreven en bij het
starten teruggezet. Verwijder dat bestand om met een verse genenpoel te beginnen.

## Zelf aanpassen

- **Gedrag**: `Assets/behaviors.json` bevat de kans per gedrag. Waardes worden over de
  ingebouwde standaarden heen gelegd, dus je hoeft alleen te noemen wat je wilt wijzigen;
  op `0` zetten schakelt iets uit.
- **Kleuren**: `Rendering/Palette.cs` — een palet is een hue-draaiing plus verzadiging en
  helderheid. `uv run Tools/palette_sheet.py` toont ze allemaal naast elkaar.
- **Art**: sprites zijn 64×64 frame-strips in `Assets/sprites/` met metadata in
  `animations.json` (fps, loop, `anchor` = contactpunt, optioneel `eye` voor de losse
  pupil-overlay). Vervangen kan zonder code te wijzigen.
- **Previews**: `uv run Tools/preview_skin.py` rendert exact wat de app componeert,
  `Tools/zoom_actions.py <actie>` vergroot losse animaties met hun ankerpunt.

Debuglog met posities en gedragswissels: `%TEMP%\cuttlefishpet-debug.log`.
