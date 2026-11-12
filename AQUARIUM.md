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
een vel stickers, geen volume. *(Grotendeels verholpen op 12 nov: dieren hebben
een z-waarde waar formaat, waas, snelheid en tekenvolgorde uit volgen. Wat nog
ontbreekt is het roofdier dat uit de diepte komt.)*

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
- ~~Glinstering langs de bovenrand van het scherm.~~ **Af** (12 nov). Drie tot
  negen zachte lichtvlekken in een band van ~30px onder de bovenrand, die met de
  stroming meeschuiven (drie keer zo hard als een stofje: een rimpel lóópt over
  het water) en oplichten als ze onder de voet van een lichtbundel door gaan.
  Dat laatste is de reden dat het geen los effect is: licht komt binnen waar het
  oppervlak oplicht. `Core/SurfaceShimmer.cs`, `Rendering/SurfaceShimmerLayer.cs`,
  zit onder hetzelfde `light`-commando.

**Thema 1 is hiermee af.** Wat er van water en licht nog open staat is
verhuisd naar de diepte-ronde hieronder: dieren die zich schuin tegen de
stroming in zetten.

Harde eis: het mag niets kosten op de processor en nooit tekst onleesbaar maken
waar je in zit te lezen. Subtiliteit ís de functie.

### 2. Diepte

- ~~Een z-waarde per dier: schaal, waas, snelheid en tekenvolgorde volgen eruit.~~
  **Af** (12 nov). 0 = achterin de waas, 1 = tegen het glas, dezelfde kant op als
  het stof. Daaruit volgen vier dingen tegelijk: formaat ×0,70 tot ×1,06, snelheid
  in schermpixels ×0,70 tot ×1,08 (dezelfde verhouding, anders leest het als
  schaatsen), een waas in de kleur van wat er áchter het dier zit (niet in
  aquariumblauw — afstand haalt contrast weg, een blauwe sluier op een witte
  pagina voegt het juist toe), en de tekenvolgorde. Die volgorde loopt door de
  waterlagen heen: het stof pakt zijn z nu uit zijn eigen diepte, dus een mot
  drijft vóór een verre zeekat langs en achter een nabije. `Core/Depths.cs`,
  debug-commando `depth`, meetbeeld `preview/depth.png` uit `Tools/WaterPreview`.
- ~~Dieren wisselen af en toe van diepte.~~ **Af** (12 nov). Elk dier houdt een
  diepte 35 tot 110 seconden vast en drijft er dan in een seconde of zeven naar
  een andere toe. Niet opgeslagen in de tank: diepte is waar een dier vanmiddag
  toevallig zwemt, geen eigenschap.
- Het roofdier komt uit de diepte aanzetten in plaats van van opzij.
- Dieren zetten zich schuin tegen de stroming in (`WorldState.Drift` ligt klaar;
  dit verandert hoe ze sturen).

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

- **12 nov (2)** — Diepte: één z-waarde per dier, en formaat, snelheid, waas en
  tekenvolgorde die er alle vier uit volgen. Geleerd: op een live schermafdruk
  valt hier niets aan te zien — er stond een tekenfilm schermvullend aan en de
  dieren camoufleren zich dáártegen, dus het enige wat je ziet is precies niet wat
  je meet. Daarom staat de meetopstelling nu offscreen in `Tools/WaterPreview`
  (`depth.png`: vijf keer hetzelfde dier op vijf dieptes, boven diep water en
  boven een pagina tekst, met onder elk dier één seconde zwemmen als liniaal,
  want snelheid kun je in een stilstaand beeld niet zien). Daarop gemeten:
  piekcontrast op diep water 69 → 134 van 255 van achter naar voren, en op de
  pagina juist lichter in plaats van blauwer. En: de processormeting op een
  bureaublad dat video afspeelt heeft een spreiding van twaalf procentpunt, dus
  daar valt een verschil van een paar punten niet in te zien — in plaats van
  dóórmeten is de waas afgekapt op de achterste helft (de laag bestaat dan voor
  de meeste dieren niet) en wordt de tekenvolgorde alleen geschreven als hij
  echt verandert. Opnieuw nameten hoort op een stil bureaublad.

- **12 nov** — Waterlijn langs de bovenrand; thema 1 af. Geleerd: míjn ogen op
  een schermafdruk zijn geen meetinstrument voor iets van 20 op 255 — twee keer
  dacht ik "daar staat niets" terwijl de pixelwaardes lieten zien dat het er
  prima stond, dus het oordeel ging naar het profiel in getallen en de vorm naar
  het oog. Eerste poging gaf een lichte rand van twaalf pixels: precies de
  haarlijn die als beeldfout leest, nu een band van dertig die uitdooft. En de
  app ging gisteren drie keer uit tijdens het meten — dat bleek de meetopzet
  (hij stierf mee met het PowerShell-venster dat hem startte), niet de code;
  starten via `explorer.exe` lost het op. Tank heeft het gered, 13 zeekatten
  voor en na.

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