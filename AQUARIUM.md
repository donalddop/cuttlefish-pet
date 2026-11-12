# Waar dit naartoe gaat

Het doel is geen bureaubladbeestje met trucjes. Het doel is een **aquarium**: een
stuk water dat links van je browser gewoon staat te bestaan, waarin op een rustig
tempo dingen gebeuren die de moeite waard zijn om op te kijken. Bijna-realistisch
in gedrag, Pixar-achtig in verzorging — niet fotorealistisch, maar doordacht. Elk
beeld, elke timing en elke pose is een keuze.

De lat: je bent aan het browsen, je kijkt opzij, en er gebeurt iets dat je even
laat kijken. Daarna is het weer rustig. Nooit druk, nooit dood.

Dit bestand is het geheugen van dat project. Een sessie die hier binnenkomt weet
niets van gisteren — dit is wat ze moet lezen.

## Wat het nog niet is

Drie dingen ontbreken, en ze zijn alle drie fundamenteel.

**Er is geen water.** De achtergrond is leeg. Dieren zweven in het niets. Alles
wat een aquarium tot aquarium maakt — zwevend stof, lichtbundels van boven, een
stroming die alles deelt — bestaat niet. Dit is de grootste afstand tussen wat
het is en wat het moet worden, en het is puur sfeer: precies wat je wil terwijl
je iets anders aan het doen bent.

**Er is geen diepte.** Alles zit op één vlak, even groot en even scherp. Het is
een vel stickers, geen volume.

**Er is geen regie.** Elk gedrag vuurt op zijn eigen timer. Het resultaat is óf
drie dingen tegelijk óf tien minuten niets. Een echte scène heeft iemand die
bepaalt wanneer er iets gebeurt, en waar je op dat moment naar kijkt.

## De volgorde

### 1. Water en licht

- ~~Zwevend stof (marine snow): enkele tientallen deeltjes, heel traag dalend en
  drijvend, parallax naar diepte.~~ **Af** (11 nov). 37 deeltjes op 1080p, elk met
  een vaste diepte waar formaat, helderheid, daalsnelheid en stromingsgevoel uit
  volgen. Lichte kern in een donkere halo, anders bestaat het niet op een licht
  bureaublad. Draait op 12fps, niet op 30 — zie de meting in `TickWater`.
- ~~Lichtbundels van de bovenrand, langzaam wiegend, sterker bovenin.~~ **Af**
  (11 nov). Vier tot zes brede, zachte banen die allemaal dezelfde kant op hellen
  (één zon), over anderhalve minuut heen en weer wiegen, apart van elkaar
  op- en afademen tussen 42% en 100%, en halverwege het scherm oplossen. Een
  heldere kern met schaduw ernaast, net als het stof: op donker zie je de bundel,
  op een witte pagina de schaduw. `Core/LightShafts.cs`,
  `Rendering/LightShaftLayer.cs`, debug-commando `light`. Draait op 4fps en kost
  niets meetbaars.
- ~~Eén gedeelde stroming: een vector die over minuten verloopt. Alles wat zweeft
  erft hem~~ **Af** (11 nov): `Core/Current.cs`, uitgelezen via `WorldState.Drift`.
  Nog niet gedaan: **dieren zetten zich er schuin tegenin**. Dat verandert hoe ze
  sturen, dus dat hoort bij de diepte-ronde hieronder.
- Glinstering langs de bovenrand van het scherm.

Harde eis: het mag niets kosten op de processor en nooit tekst onleesbaar maken
waar je in zit te lezen. Subtiliteit ís de functie.

### 2. Diepte

- Een z-waarde per dier: schaal, waas, snelheid en tekenvolgorde volgen eruit.
- Dieren wisselen af en toe van diepte.
- Het roofdier komt uit de diepte aanzetten in plaats van van opzij.

### 3. De regisseur

Een laag boven de gedragsmachine die het ritme stuurt:

- hooguit één groot ding tegelijk (roofdier, ammoniet, hypnose, balts, gevecht,
  kleurenshow);
- verplichte rust na zo'n ding;
- bij voorkeur ensceneren in de buurt van waar de cursor de laatste tijd was —
  een gebeurtenis op je tweede monitor is weggegooid;
- een trage hartslag, zodat het nooit tien minuten dood is.

### 4. Het dier zelf echter maken

- Armhoudingen als vocabulaire: alert, ontspannen, etend, dreigend, baltsend —
  consequent gebruikt, zodat het één soort met één taal wordt.
- Camouflage die achterloopt: bij verplaatsing eerst even niet kloppen en dan
  bijtrekken. Dat naijlen is precies hoe het er in het echt uitziet.
- Vinrimpeling gekoppeld aan snelheid; bij jetten een zichtbare trechterpuls met
  terugslag.
- Poses lang genoeg vasthouden om te lezen.

### 5. De bewoners

Dingen die weinig doen maar er een plek van maken: een krab die over de taakbalk
loopt, een anemoon op een vensterhoek die meewiegt met de stroming, een
poetsgarnaal die een zeekat bedient.

### 6. Het sociale

Man en vrouw (uiterlijk, baltsrollen, keuze door het vrouwtje, bewaken, en een
klep tegen een tank die toevallig helemaal mannelijk wordt), en daarna het
sluipende mannetje dat zich voordoet als vrouwtje. Het ontwerp hiervoor staat in
de geschiedenis van dit project; het addertje is dat instroom van buitenaf alleen
werkt onder de twee dieren.

### 7. Snoeien

Er zijn zo'n 85 gedragingen en een deel is eSheep-erfenis die de illusie breekt
(spook, elektrische schok, meeliften, minimaliseren). Alles moet zijn plek
verdienen. Weggooien telt als vooruitgang.

## Huisregels

Deze zijn duur geleerd. Ze gelden elke dag.

- **Eén verbetering per keer**, klein genoeg om af te maken, te testen en met
  eigen ogen te zien binnen één sessie.
- **Bouw een debug-commando voor alles wat zeldzaam is.** Wachten tot het
  toevallig gebeurt kost meer tijd dan het commando schrijven. Zie `strike`,
  `reach`, `decoy`, `bones`, `lurk`, `shove`.
- **Kijk er echt naar.** Schermafdruk, uitsnede, eigen ogen. Niet aannemen.
- **Geen enkele uitzondering mag de UI-thread bereiken.** Een tikfout in een
  animatienaam heeft de app ooit omgelegd, met verlies van het aquarium tot de
  laatste opslag.
- **Raak `%LocalAppData%\CuttlefishPet` nooit aan.** Dat is Donalds aquarium:
  dagen aan geërfd gedrag en opgebouwde banden, en het enige in dit project dat
  niet opnieuw te maken is.
- **Meet voor en na** als je aan voortplanting of populatie komt:
  `uv run Tools/graveyard.py`.
- **Tests groen** (`dotnet run --project Tools/DriveTests -c Release`) voor je
  klaar bent.
- **Prestatie is een functie.** Hij draait de hele dag naast een browser.
- Commits en codecommentaar in het Engels, documentatie voor Donald in het
  Nederlands. Commentaar legt uit *waarom*, inclusief wat er is geprobeerd en
  niet werkte.
- **Geen release zonder het te vragen.**

## Het dagelijkse rondje

1. Lees dit bestand en `git log --oneline -15`.
2. Kies één ding uit de volgorde hierboven — het bovenste thema dat nog niet af
   is, tenzij Donald iets anders heeft gevraagd.
3. Bouw het, test het, kijk ernaar, commit en push.
4. Werk Donalds geïnstalleerde versie bij, zodat zijn snelkoppeling klopt.
5. Werk dit bestand bij: streep af, noteer wat je geleerd hebt.
6. Sluit af met **één** vraag aan Donald: een concrete keuze voor morgen. Geef
   hem altijd ook de optie om iets weg te gooien.

## Logboek

Nieuwste bovenaan. Eén regel per dag: wat er gebeurde, en wat het leerde.

- **11 nov (2)** — Licht: lichtbundels van de bovenrand. Geleerd: een mooie
  preview op diep water bewijst niets — op ware sterkte boven een echt scherm
  (een game in de schemering) was er één vage baan en verder niets, dus de
  sterkte moest 40% omhoog; dezelfde les als met het stof, en ik liep er toch
  weer in. Verder: vijf brede rechthoeken die 4× per seconde kantelen kosten
  géén meetbare processortijd terwijl 37 stofjes op 12fps +6 punten kostten —
  het aantal visuals dat je aanraakt telt, niet het aantal pixels. En de
  sprongtest ving een echte discontinuïteit: `Fit` kende de stroming niet, dus
  de eerste tick verzette elke bundel in één keer 34px. Live nagemeten op het
  zwarte bureaublad: pieken van 25/255 bovenin, 15 halverwege — dat is de
  bedoelde sterkte. Geïnstalleerde versie bijgewerkt.

- **11 nov** — Water: gedeelde stroming + zwevend stof. Geleerd: bleke deeltjes
  bestaan niet op een licht bureaublad (vandaar de donkere halo), "over een
  venster dus wegdimmen" was fout omdat dit scherm bijna altijd venster is, en
  37 extra visuals op 30fps kostten méér processor dan twaalf dieren — op 12fps
  valt dat terug van +14 naar +6 procentpunt van één kern. En: dit water is zo
  subtiel dat een schermafdruk van een willekeurig bureaublad niets bewijst,
  dus er staat nu een `Tools/WaterPreview` die het op gekozen achtergronden
  tekent. Zonder die tool had ik drie keer verkeerd gegokt in plaats van gemeten.