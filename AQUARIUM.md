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
een z-waarde waar formaat, waas, snelheid en tekenvolgorde uit volgen. Op 21 nov
kwam het roofdier uit de diepte erbij, en zetten de dieren zich tegen de stroming;
thema 2 is daarmee af.)*

**Er is geen regie.** *(Skelet staat sinds 21 nov: `Core/Director.cs` laat hooguit
één groot ding tegelijk toe, legt er 75 seconden rust achter, hurry't de klokken als
het te lang stil is, en ensceneert in de buurt van waar je cursor was. Wat er nog
niet onder valt is alles wat twee dieren onderling doen — zie thema 3.)*

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
- ~~Het roofdier komt uit de diepte aanzetten in plaats van van opzij.~~ **Af**
  (21 nov). Een bezoek begint nu binnen beeld op diepte 0,02 — achter de
  lichtbundels, op 0,57x formaat en 0,31 dekking — kruist diep terwijl het rondkijkt,
  komt naar voren zodra het iemand uitkiest, en slaat toe in het vlak van dat dier.
  `Core/Approach.cs`, debug-commando `rise`. Drie dingen geleerd: een rijzing op
  een ease komt nooit aan (het venster tussen vastzetten en uitvallen is 1,44s op
  zijn kortst, en een ease kruipt de laatste tiende), het roofdier moet binnen het
  water dat de dieren gebruiken volgens hún regels getekend worden (anders staat er
  een spook naast een massief dier in hetzelfde vlak), en de band áchter die
  dierenbodem draagt bijna de hele verandering in dekking — op één snelheid was die
  in 0,19s doorkruist, wat geen fade is maar een knip tussen twee frames.
- ~~Dieren zetten zich schuin tegen de stroming in.~~ **Af** (21 nov).
  `Core/Crabbing.cs`: de drift gaat in de positie (nooit in de snelheid — de drag
  eet hem op, koersen pikken hem op en de helft van de gedragingen gooit hem weg),
  toegepast ná het gedrag, zodat alle tachtig gedragingen hem erven en ieder gedrag
  dat op een punt stuurt vanzelf corrigeert. Dat corrigeren ís het schuin zetten.
  Zichtbaar is de houding: tot 9° neus-omhoog tegen het verticale deel van de
  stroming, alleen zolang het dier zelf onder de 95px/s zit. Een zijwaartse stroming
  wordt expres niet beantwoord: een zijaanzicht kan niet stroomopwaarts wijzen door
  te kantelen, dat zou omdraaien vragen, en dat gevecht hoort het gedrag te winnen.

#### De jacht zelf

- ~~De jacht heeft een vorm: kruisen, sluipen, stilstaan, uitvallen, bijkomen.~~
  **Af** (13 nov). `Core/Hunt.cs` is een losse rekenklasse zoals `Depths`, dus de
  hele pacing is zonder scherm te draaien in DriveTests. Het dier *vertraagt*
  naar 0,46× kruissnelheid om de laatste 170px te sluipen, staat dan een derde
  seconde bijna stil, en gaat pas dán — op 760px/s, over ongeveer een vijfde
  seconde. Ongeveer twee seconden spanning, een fractie strijd. Debug-commando
  `stalk`: een roofdier dat gegarandeerd mist, zodat je de vorm twee keer per
  bezoek kunt bekijken.
- ~~De prooi weet het pas als hij uitvalt.~~ **Af** (13 nov). De alarmstraal
  krimpt van 300 naar 95px tijdens het sluipen en klapt open bij de uitval. Dit
  was de echte fout: de prooi kreeg het vluchtbevel op het moment van
  vastzetten, dus er bestond geen seconde waarin het roofdier dichtbij was en het
  dier het nog niet wist — en dat is het enige enge moment dat er is. De
  dubbelganger (inkt) gaat nu ook pas op het moment van uitvallen weg.
- ~~De staartslag hoort bij de snelheid, en het lijf kantelt in de duik.~~
  **Af** (13 nov). De spriteklok liep op `Age`, dus een sluipende haai sloeg zijn
  staart even snel als een sprintende. Nu 0,45 tot 2,2 slagen per seconde naar
  snelheid (wortel, geen recht verband), en de staart stáát stil tijdens het
  coilen. Plus een helling tot 38° naar de koersrichting, met vertraging, zodat
  een duik ook als duik leest in plaats van als een plaatje dat schuin wegglijdt.
- ~~Zes zwemframes in plaats van vier, kop draait tegen de staart in, staartpunt
  niet meer afgesneden.~~ **Af** (13 nov).
- ~~Raakt hij zijn prooi kwijt tijdens het sluipen, dan geeft hij op.~~ **Af**
  (13 nov, `Hunt.GiveUpAt`). Eén live bezoek viel uit vanaf 816px — vijf keer de
  bedoelde afstand — na een wissel van prooi halverwege, en vier pogingen om het
  te herhalen kwamen schoon terug. Niet kunnen afleiden, niet kunnen uitlokken;
  wel zeker dat zo'n sprong geen jacht is maar een dier dat naar niets sprint.
  Voorbij tweemaal de wachtafstand breekt hij het sluipen nu af. Daarna zeven
  jachten achter elkaar met de uitval tussen 86 en 134px.
- ~~Een roofdier wordt nergens binnen het glas gehouden.~~ **Af** (22 nov).
  `Core/Glass.cs`, twee delen die allebei nodig zijn. Het zichtbare deel is een
  klim terug naar binnen (`Return`), opgeteld bij de kruiskoers: nul zolang het
  hele lijf in beeld is, oplopend tot 240px/s aan de schermrand, en de neus-omhoog
  komt er gratis uit omdat `Hunt.Animate` al naar de koers kantelt. Het tweede deel
  is een harde lijn (`Hold`) die het ankerpunt nooit voorbij de rand laat: een
  koers is geen rem, en 760px/s keer je niet in een tiende seconde. Marges van 112
  boven en 78 onder, gemeten aan de alfa van frame 0 (de rugvin zit 87px boven het
  anker, de buikvinnen 52 eronder) en afgerond voor de 38° kanteling. Live gemeten
  met het nieuwe `dive`-commando: burst naar (1185,991), dan y=1078 van 1080 in
  plaats van 1344, terug=-234px/s op het diepste punt, en 1,1s later volledig in
  beeld. Het bezoek eindigde na 7,3s zijwaarts in plaats van vijftien seconden
  onzichtbaar.
- **Gevonden op 22 nov, niet opgelost:** een uitval vanaf 1324px. De dolfijn
  sloop op #4888 tot `gat=31px`, ging coilen, en viel een halve seconde later uit
  op 1324px — dus de prooi verplaatste zich ruim 1300px tussen het coilen en het
  uitvallen, wat geen dier kan zwemmen. Dit is dezelfde soort als de onverklaarde
  816px van 13 nov die `Hunt.GiveUpAt` opleverde. Vermoeden (niet nagekeken): die
  wachtafstand wordt alleen tijdens het sluipen getoetst, en de coil van 0,34s
  toetst niets, dus een prooi die daarbinnen verspringt glipt erdoor. Wat de prooi
  laat verspringen weet ik niet.
- De vangkans is met opzet niet aangeraakt. Een besloop dier is nu kansloos en
  hoort makkelijker te pakken te zijn, maar de vangkans bepaalt wie er voortplant
  — dat verander je op een dag dat je `uv run Tools/graveyard.py` voor en na
  draait, niet terwijl je naar een animatie zit te kijken.

### 3. De regisseur

**Het skelet staat** (21 nov, `Core/Director.cs`). De poortjes zijn met opzet
eenregelig op de plek waar een gebeurtenis afgaat — een vermenigvuldiger op een
aftelklok en een vraag vooraf — want het alternatief is tachtig gedragingen en vijf
gebeurtenislussen onder een scheduler schuiven, en dat is een herschrijving.

- ~~Hooguit één groot ding tegelijk.~~ **Af**: roofdier, ammoniet en ritueel
  claimen het toneel. Zolang het bezet is stáán de andere klokken stil in plaats van
  achter het gordijn af te lopen en samen af te gaan zodra het opgaat — die opstopping
  is precies waar dit voor bestaat.
- ~~Verplichte rust na zo'n ding.~~ **Af**: 75 seconden.
- ~~Een trage hartslag.~~ **Af**: na zes minuten niks lopen de klokken 3x, en per
  verdere zes minuten stilte nog eens 3x, tot maximaal 9x. Een vlakke 3x was niet
  genoeg — in zes uur zat er dan nog een gat van twaalf minuten in. Nu: 43
  gebeurtenissen in zes uur, langste gat tien minuten, nul overlappingen (gemeten in
  DirectorTests).
- ~~Ensceneren waar de cursor de laatste tijd was.~~ **Af**: 26 seconden ease op de
  cursor, en alleen terwijl er iemand aan de machine zit. Naast de cursor, niet
  erop — een roofdier dat altijd onder je muis opduikt is geen weer meer maar een
  schrikmoment.
- Nog **niet** onder de regie, met opzet: alles wat twee dieren onderling doen
  (rivaliteit, balts, spiegelen, vechten om een garnaal). Dat is het weer van de
  tank en niet zijn gebeurtenissen, en de helft ervan bepaalt wie zich voortplant en
  wie doodgaat — dat verander je op een dag dat populatie het onderwerp is en
  `uv run Tools/graveyard.py` ervoor en erna draait.
- Nog niet gedaan: hypnose, balts, gevecht en kleurenshow onder dezelfde regie
  brengen zodra het bovenstaande voorbehoud is opgelost.

### 4. Het dier zelf echter maken

- Armhoudingen als vocabulaire: alert, ontspannen, etend, dreigend, baltsend —
  consequent gebruikt, zodat het één soort met één taal wordt.
- ~~De armen zijn geen geschilderde krans meer.~~ **Af** (14 nov). Voor 21 acties
  exporteert de generator een rig in plaats van geschilderde armen, en
  `Rendering/ArmPhysics.cs` rekent per dier zes kettinkjes door: verlet met
  waterweerstand, zachte poseveren (stevig bij de wortel, los bij de punt) en
  linkjes die hun lengte houden. Daardoor slepen de armen na als het dier klimt
  of draait, zwaaien ze door als het stopt, en vloeien ze van de ene pose in de
  andere. Grijpende en gevouwen poses (plafond, hangen, graven, verstoppen,
  mimicry, slapen) houden hun geschilderde armen. Let op: het *vocabulaire*
  hierboven staat hiermee niet af — de armen bewégen nu, de houdingen zijn nog
  niet per situatie consequent gekozen.
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

Er zijn zo'n 85 gedragingen en een deel is eSheep-erfenis die de illusie breekt.
Alles moet zijn plek verdienen. Weggooien telt als vooruitgang.

- ~~Spook, elektrische schok, meeliften op de cursor, meeliften met een
  geminimaliseerd venster.~~ **Weg** (21 nov, 211 regels eruit, 11 erin). Het spook
  was de ergste: dat verwijderde stilletjes een dier met dagen geërfd gedrag en
  opgebouwde banden — het enige hier dat niet opnieuw te maken is — en zette er een
  vreemde voor in de plaats. De leidingen die alleen voor 'minimaliseren' bestonden
  zijn mee weggegaan (`WorldState.MinimisedWindows`, `WindowTracker.TakeMinimised`).
  De *ghost-animatie* blijft: doodgaan gebruikt hem nog.
- Blijft staan om naar te kijken: `blot` (de inktvlek die eSheep als keutel bedoelde).
  En de `shock`-frames worden nog steeds gegenereerd en staan nog in de sheets; dat
  kost een beetje geheugen en verder niets, en eruit halen betekent elke sheet
  opnieuw maken — werk voor een dag waarop sprites het onderwerp zijn.

## Huisregels

Deze zijn duur geleerd. Ze gelden elke dag.

- **Eén verbetering per keer**, klein genoeg om af te maken, te testen en met
  eigen ogen te zien binnen één sessie.
- **Bouw een debug-commando voor alles wat zeldzaam is.** Wachten tot het
  toevallig gebeurt kost meer tijd dan het commando schrijven. Zie `strike`,
  `reach`, `decoy`, `bones`, `lurk`, `shove`, `dive`.
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

- **22 nov** — Het roofdier wordt binnen het glas gehouden (`Core/Glass.cs`), plus
  een `dive`-commando dat de overschietende duik op afroep levert. Geleerd, in
  volgorde van nut: (1) een debug-commando dat te goed mikt, mikt nergens meer op
  — `dive` kwam eerst 430px bóven zijn doel binnen om de duik steil te maken, maar
  vastlokken wordt op afstand gescoord en met 780px aanloop erbij haalde de prooi
  de drempel nooit; de dolfijn kruiste het hele scherm vlak op y=572 met zijn doel
  430px eronder en vertrok zonder ooit iemand aan te kijken. Op 220px werkt het.
  (2) Twee tests faalden op hun eigen opzet en niet op de code: `deepest` begon op
  de startwaarde, dus het beginpunt (de bug zelf) telde mee als meetresultaat, en
  de twee scenario's deelden één klok, dus het tweede rapporteerde de tijd op de
  wand in plaats van de tijd die het kostte. Een meetfout leest precies zoals een
  echte fout. (3) De eerste live `dive` sloeg aan aan de bóvenrand, niet de onder-
  — de jacht kiest zelf zijn prooi en trok het roofdier omhoog. Dat was geen
  tegenvaller: beide randen zijn nu in het echt gezien. Tank: 17 zeekatten voor,
  15 na; allebei `ouderdom`, om 20:26:29 en 20:26:31, en er gingen er om 20:20-21
  al vier zo weg terwijl de geïnstalleerde app draaide en ik nog niets had
  aangeraakt. Het is een lichting die uitsterft, niet het roofdier — `dive` kan
  niet doden.

- **21 nov** — Vier dingen op jouw verzoek, tegen de huisregel van één per keer in,
  elk met eigen tests en eigen commit: het roofdier komt uit de diepte (thema 2),
  dieren zetten zich tegen de stroming (thema 2 af), het skelet van de regisseur
  (thema 3) en vier stukken eSheep-erfenis eruit (thema 7). Geleerd, in volgorde van
  pijn: (1) de dieren hebben een expres smal bereik voor formaat en dekking omdat ze
  het onderwerp zijn, en toen het roofdier zijn eigen veel bredere bereik kreeg stond
  er een spook naast een massief dier in hetzelfde vlak — binnen het water dat de
  dieren gebruiken gelden hún regels, en de dramatiek hoort áchter hun bodem waar
  niets anders kan komen. (2) Ik heb twee runs volledig verkeerd gelezen doordat de
  logregel `_world.Drift` las, die TickWater één keer per tick schrijft: er stond
  'stroming x0' naast '33px/s'. Een logregel die één tick achterloopt is erger dan
  geen logregel. (3) De eerste getraceerde nadering kwam linksonder boven, kruiste de
  hele tank achterlangs zonder dat er ooit iets binnen zijn 520px kwam, en vertrok —
  acht seconden volkomen correct gedrag en niets om naar te kijken; een debug-summons
  moet écht richten, niet alleen de hoogte lenen en voor de kant een munt opgooien.
  (4) Het zichtbare bereik van een fade zit niet in het getal maar in de tíjd: 0,24
  naar 0,90 in 0,19 seconde is geen fade maar een knip, en dat zie je alleen op een
  opname terug. Tank: 13 zeekatten voor, 13 na (plus #4309 die er vanzelf bij kwam);
  niemand is onderweg doodgegaan.

- **14 nov** — Drie dingen op één dag: scherpere sprites op 2× met loops die
  niet meer haperen aan de naad, een voorbijtrekkende pijlinktvisschool (8-14
  boids, elke 14-36 minuten, achter de dieren langs, kijkende zeekatten) en
  springende armen (zie thema 4). Geleerd: bestanden wegschrijven met relatieve
  paden vanuit een los proces landt in de map van dat proces en niet in de repo,
  dus de eerste versiebump kwam nooit aan en de binaries noemden zichzelf nog
  2.10.2 — vandaar een tweede commit die hetzelfde nog eens goed doet. En: op 2×
  zouden veertien kleurpaletten van álles een paar honderd MB kosten, dus
  `PaletteFrames` rekent een variant pas uit bij eerste gebruik. Dit logboek
  bleef die dag liggen; deze regel is op 16 nov nageschreven uit de commits, dus
  hij vertelt wat er gebouwd is en niet wat er live te zien was.

- **13 nov** — De jacht kreeg een vorm (sluipen, stilstaan, uitvallen) en de
  roofdieren kregen een animatie die bij hun snelheid hoort. Geleerd: snel is
  niet hetzelfde als spannend — de oude jacht was één versnelling en 0,8s
  aanloop, en de oplossing was níet harder maar eerst *langzamer*. De echte fout
  zat trouwens niet in de snelheid maar in één regel die de prooi liet vluchten
  op het moment van vastzetten; daardoor bestond het enige spannende moment
  simpelweg niet. Verder: twee bugs kwamen alleen boven water door het écht te
  draaien en niet door de tests — bij wisselen van prooi liep de sluipklok door
  (haai zag iets dichterbij en viel ter plekke uit, zonder aanloop), en een te
  vroege `return` bij een geslaagde dubbelganger sloeg de hele uitvalfase uit het
  logboek, waardoor ik een run lang dacht dat hij niet gebeurd was. Beide zitten
  nu als test in DriveTests. En bij het vergroten van één frame bleek de
  staartpunt van de haai al die tijd vlak afgesneden tegen de canvasrand — op
  ware grootte zie je dat niet, in de alfawaarde van de randkolom wel (255 =
  hard afgekapt, nu 0). Tank: 13 zeekatten voor, 12 na; die ene stierf van
  ouderdom, niet door mij — `stalk` kan niet doden.
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