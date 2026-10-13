# Halls of the Things (ZX Spectrum, 1983): analisi del sorgente originale

Documento di progetto per la riscrittura in BBC BASIC V (+ assembler ARM) su Archimedes.
Fonte di riferimento: `original/halls83_main.asm` (versione "10.10 3/3/83"),
`original/halls83_front.asm`, `original/halls83_chars.asm`. I numeri di riga citati
(`main:NNNN`, `front:NNN`, `chars:NNN`) sono quelli dei file `.asm` convertiti.
Dove il significato è dedotto e non scritto nel sorgente lo dico esplicitamente
("dedotto"). Le etichette fra parentesi sono quelle del sorgente; per molte la versione
2006/2007 (`halls_main.asm`, "version 11 19/2/06") ha commenti e nomi di campo che
confermano l'interpretazione: li cito come "(v11: ...)".

Nota importante sui nomi: `XW=28` e `YW=21` **non** sono le dimensioni del labirinto ma il
passo di scorrimento a schermate (vedi §2.6). Il gioco **non ha suoni** (vedi §6.3).

---

## 1. Struttura generale

### 1.1 Mappa della memoria (Spectrum 48K)

| Indirizzo | Contenuto |
|---|---|
| `#4000-#57FF` | bitmap dello schermo; `#5800-#5AFF` attributi (`pAttrib`) |
| `#6000` | front end (`halls83_front.asm`, `org #6000`) |
| `#6800` | set di caratteri `chars.bin`, 2048 byte (`Characters equ #6800`, front:786) |
| `#7000` | programma principale (`org #7000`, main:19). `#7000: jp ITB`, `#7003: jp OutIt` (usato dal front end come `vOutIt`) |
| dopo il codice | `BigM` 16384 byte (mappa a caratteri 128×128), `MazeList` 4096 (salvataggio degli 8 piani, 512 byte ciascuno), `ML` 2048 (32 mostri × 64 byte), `Maze` 256 (allineato a 256: griglia logica 16×16), `PL` 1024 (32 proiettili × 32 byte) (main:4792-4799) |

### 1.2 Flusso del programma

1. **Front end** `Start` (front:15): `FuckBasic` disabilita gli interrupt (`di`, `im 2` ma
   interrupt mai riabilitati), bordo nero. Il seme del generatore casuale viene preso **una
   volta sola** al caricamento: `Seed+0..1` = variabile di sistema FRAMES (23672),
   `Seed+2` = byte alto di FRAMES (23674), `Seed+3` = registro R (front:17-23).
2. `FrontLoop` (front:25): `Screen1` stampa la schermata di istruzioni con il record
   ("Kickaha 100"), aspetta SPAZIO, stampa "Please wait for a second", poi `call MainP`
   (= `#7000` → `ITB`).
3. **`ITB`** (main:4364): salva SP e indirizzo di ritorno, poi
   `InitSanctuary` (disegna il Santuario, una volta per partita) → `InitGame` (variabili del
   giocatore) → `Gen8` (genera gli 8 piani e li salva) → `ClearSc` → `Telep0`.
4. **`Telep0`** (main:4407): piano corrente 0, `Recreate` (rigenera il piano 0 dal seme
   salvato), `PlaceMon`, uomo in `BigM+14077` (riga 109, colonna 125: in basso a destra nel
   Santuario), `JustifyT` (inquadratura), poi `NewFloor` → `GameLoop`.
5. **`GameLoop`** (main:4386), ciclo infinito senza sincronizzazione video:
   `MoveMan`, `MoveMonsters`, `MoveProj`, `ScanKeys`, `JustifyScreen`, `TryHealing`,
   `DisplayScore`, `ArrowDir`, poi `ld r,a` con un numero casuale (`Rnd7`).
6. **Fine partita**: `ManKilled` (ferite ≥ 127), `DunIt` (vittoria), `Quit` (Q+T+U+I).
   Tutte passano da `FOut` (main:4338), che ripristina SP e torna al front end spingendo
   sullo stack punteggio, anelli, mostri uccisi e tipo di fine (0 = ucciso, 1 = vittoria,
   2 = abbandono).
7. Il front end (`FrontScore`, front:529) mostra l'esito, il confronto col record ed
   eventualmente chiede il nome; SPAZIO → "Please wait" → nuova partita (`FrontLoop`).
   Il seme NON viene reinizializzato: la sequenza casuale prosegue.

### 1.3 Routine principali

| Etichetta | Riga | Funzione |
|---|---|---|
| `Init` | main:39 | azzera `Maze` (256) e riempie di spazi la parte labirinto di `BigM` (113 righe × 113 colonne, col. 0-112); **non tocca** le colonne 113-127 (il Santuario) |
| `Main` / `MAIN` | 78 / 4122 | generatore del labirinto logico 16×16 (hunt-and-kill + anelli); `MAIN` imposta `iy=ConTab+piano` |
| `FindMoves`, `TMU/TMD/TML/TMR` | 104-182 | vicini non visitati |
| `Move` | 184 | collega la cella corrente a un vicino scelto a caso, con tipo di passaggio da `Rnd6` |
| `FindNext` | 276 | cerca la prossima cella non visitata (scansione lineare) |
| `FindLinks`, `TryLink*`, `Link` | 287-384 | vicini visitati non ancora collegati; crea un collegamento senza spostarsi |
| `Rand`, `Rand1` | 386, 391 | generatore pseudo-casuale a 32 bit |
| `Rnd2`, `Random`, `Rnd6`, `Rnd7`, `RandRange` | 418-488, 1517 | derivati del generatore |
| `Page` / `PAGE` | 490 / 4191 | rende il labirinto logico come mappa di caratteri in `BigM` (piano 0: sala aperta) |
| `PageC22`, `SideWall` | 520, 3429 | muro inferiore e destro, porta "campo di forza" verso il Santuario |
| `DrawWall`, `DrawDown`, `DrawLeft`, `Corner` | 547-636 | segmenti di muro e caratteri d'angolo |
| `MoveMan` | 1066 | logica dell'uomo (porte, prendi/lascia, armi, movimento) |
| `MMoveLR`, `MMoveUD` | 638, 661 | lettura diretta dei tasti di movimento |
| `MMoveDoor` | 684 | apre (O) / chiude (C) porte |
| `MMKeepDrop`, `MMKeep2`, `MMDrop` | 796-952 | prendi (K) / lascia (D) |
| `MSword` | 954 | spada (S) |
| `ManValid`, `MManRE1`, `OpenPlan` | 1192, 1203, 4214 | caselle percorribili, pianerottoli, campo di forza/vittoria |
| `PM`, `PMD` | 1211, 1233 | disegna l'uomo (mappa+schermo / solo schermo) |
| `MManNewFrame` | 1238 | cambio schermata quando l'uomo esce dal video |
| `SCV`, `CFormat`, `XYC` | 1276-1317 | indirizzo `BigM` → coordinate schermo/mappa |
| `OutIt` | 1319 | disegna un carattere 8×8 + attributo in una cella dello schermo |
| `InitTreasure`, `InitMagic`, `FindPosition` | 1390-1445 | 40 tesori, 40 pozioni per piano, in caselle vuote casuali |
| `InitMonsters`, `IMons1`, `PlaceMon` | 1447-1515 | 32 mostri per piano |
| `MoveMonsters`, `MMons2` | 1542, 1554 | aggiornamento mostri |
| `CheckCombat` | 1654 | il mostro vede l'uomo? frecce, fulmini, palle di fuoco |
| `MoveThing1/2/3`, `MTNewDir`, `MonKeep`, `MonODoor` | 2052-2270, 4058 | movimento dei mostri |
| `SteamRise` | 2000 | animazione dei cadaveri bruciati |
| `MoveProj`, `MProj2..4` | 2274-2358 | motore dei proiettili |
| `MFireArrow`, `MoveArrow` | 2368, 2493 | frecce |
| `FireBall`, `MoveFire` | 2879, 2946 | palla di fuoco (a ricerca) |
| `Lightning`, `MoveLight`, `MLightRefl` | 3064, 3116, 3187 | fulmine (rimbalza) |
| `HitMan`, `HitMonster` | 2527, 2550 | danni |
| `InitLine`, `NextPoint` | 2604, 2720 | linea di Bresenham tra due celle |
| `RangeDoor`, `RangeLook`, `MonsterSearch` | 2802, 4231, 2760 | linea di vista, bersaglio più vicino |
| `ScanKeys`, `DWKey`, `ReadKey` | 3291-3427 | tastiera con memoria del fronte di pressione |
| `JustifyScreen`, `JustifyT` | 1813, 1821 | centratura (SPAZIO) |
| `NewScreen`, `NewSGet` | 1880, 1912 | ridisegna tutte le 32×24 celle |
| `Gen8`, `DefRing` | 3570, 3638 | genera gli 8 piani, posa anelli e chiave |
| `SaveFloor`, `Recreate`, `OpenIfNess` | 3666, 3731, 3824 | salva/ricrea lo stato di un piano |
| `InitSanctuary`, `Rung`, `SMess` | 3849, 4017, 4000 | il Santuario con le scale |
| `NFloor` | 4030 | cambio di piano |
| `MMKRing`, `GoldKey` | 4139, 4175 | raccolta anelli/chiave, apertura della porta del piano 0 |
| `CTres`, `DoScor`, `DoS1` | 4268-4309 | punteggio |
| `CQuit`, `ManKilled`, `DunIt`, `Quit`, `FOut` | 4311-4362 | fine partita |
| `TryHealing` | 4457 | guarigione (H) |
| `DisplayScore`, `InitScore`, `PrintBar` | 3498, 4480, 4598 | schermata di stato (tasto 1) |
| `ArrowDir` | 3524 | direzione delle frecce (5/6/7/8) |
| `DoorHackOpen`, `BWorC` | 4247, 4256 | trucco del tasto 2, bianco/nero col tasto W |

---

## 2. Il labirinto

### 2.1 Geometria della mappa (`BigM`)

`BigM` è una matrice di **byte = codici carattere** di 128 colonne × 128 righe (indirizzo =
`BigM + riga*128 + colonna`); ne sono usate 113 righe (0-112). La mappa *è* direttamente
l'immagine a caratteri: ogni cella contiene il codice del glifo da disegnare, e gli oggetti
sono riconosciuti dall'intervallo del codice (§7.3). Non esiste altra rappresentazione dello
stato visibile.

- Colonne 0-112, righe 0-112: il piano corrente (labirinto 16×16 stanze). Rigenerato a ogni
  cambio di piano.
- Colonne 113-127, righe 0-112: **il Santuario**, disegnato una volta per partita da
  `InitSanctuary` e mai cancellato da `Init` (che pulisce solo le colonne 0-112): è comune a
  tutti i piani e conserva quello che vi si lascia.
- Fuori mappa (riga ≥ 113 o colonna ≥ 128, o coordinate "negative") lo schermo mostra il
  carattere `#FF` (una X blu brillante) (`NewSGet`, main:1912).

`CFormat`/`XYC` (main:1304) convertono un indirizzo in H = riga (y), L = colonna (x).

### 2.2 Il labirinto logico (`Maze`, 16×16)

Un byte per stanza, indice `l = y*16 + x` (nibble alto = y, basso = x). 0 = non visitata.
Bit di collegamento (dedotto da `MoveRIGHT/LEFT/DOWN/UP`, main:208-274):

| bit | verso |
|---|---|
| 0-1 | destra |
| 2-3 | sinistra |
| 4-5 | giù |
| 6-7 | su |

Ogni campo di 2 bit vale: **0 = muro pieno** (nessun collegamento), **1 = porta**,
**2 = varco** (apertura di 4 celle nel muro), **3 = nessun muro**. Lo stesso valore è scritto
nelle due stanze collegate (campo opposto).

### 2.3 Generazione (`Main`, main:78; `MAIN`, main:4122)

Algoritmo *hunt-and-kill* con anelli aggiuntivi, interamente guidato da `Rand`
(deterministico dato il seme):

```
azzera Maze; cur = Rand              ' stanza di partenza (0..255)
LOOP:
  ' passeggiata casuale (Draw)
  ripeti:
    lista = vicini NON visitati di cur, in ordine Su(1), Giù(2), Sin(3), Des(4)
    se lista vuota: esci
    i = Random(len(lista))            ' vedi §8
    tipo = Rnd6()                     ' 1 porta, 2 varco, 3 aperto (soglie per piano)
    collega cur -> lista(i) con tipo; cur = quella stanza
  ' anello extra a fine passeggiata
  se Rand < T0[piano]:
    lista = vicini VISITATI di cur non già collegati a cur (stesso ordine)
    collega cur a lista(Random(len)) con tipo Rnd6()  (cur non cambia)
NEXT:
  scorri cur = (cur+1) AND 255 fino a trovare una stanza con Maze=0 (max 256 passi)
  se nessuna: FINE
  lista = vicini visitati non collegati a cur; se vuota: torna a NEXT (continua la scansione)
  collega cur a uno di essi (Random, Rnd6); vai a LOOP (la passeggiata riparte da cur)
```

Ordine delle chiamate casuali in `Move`: prima `Random` (scelta della direzione), poi `Rnd6`
(tipo di passaggio). "Già collegato" si verifica guardando il campo del vicino rivolto verso
`cur` (`TryLinkUP`: `and #30` sul vicino sopra, ecc.).

Nota (dedotta): se all'anello extra la lista fosse vuota, `Link` userebbe voci vecchie di
`MoveList` senza controllo dei bordi; in una griglia 16×16 non può succedere (una stanza
raggiunta da una passeggiata ha sempre almeno un altro vicino visitato non collegato).

**Tabella `ConTab`** (main:4786), indicizzata con il piano (0-7):

| piano | T0 (prob. anello extra /256) | T1 | T2 | P(porta) | P(varco) | P(nessun muro) |
|---|---|---|---|---|---|---|
| 0 | 0 | 0 | 0 | (sala aperta, labirinto non usato) | | |
| 1 | 255 | 150 | 200 | 150/256 | 50/256 | 56/256 |
| 2 | 200 | 100 | 100 | 100/256 | 0 | 156/256 |
| 3 | 150 | 40 | 50 | 40/256 | 10/256 | 206/256 |
| 4 | 127 | 23 | 30 | 23/256 | 7/256 | 226/256 |
| 5 | 100 | 13 | 20 | 13/256 | 7/256 | 236/256 |
| 6 | 60 | 7 | 10 | 7/256 | 3/256 | 246/256 |
| 7 | 20 | 4 | 5 | 4/256 | 1/256 | 251/256 |

`Rnd6`: `r = Rand`; `r < T1` → 1 (porta); `r < T2` → 2 (varco); altrimenti 3 (nessun muro).
Il piano 1 è quindi un labirinto pieno di porte e anelli; salendo i muri spariscono e le
stanze si fondono in grandi "sale" (soprattutto al 7), mentre gli anelli extra diminuiscono.

### 2.4 Resa del labirinto in `BigM` (`Page`, main:490)

Ogni stanza (x, y) occupa un blocco 7×7 con l'angolo in alto a sinistra in
`(col 7x, riga 7y)`: interno 6×6, muri sulla riga 7y e sulla colonna 7x. Totale
16×7+1 = 113 celle per lato.

Per ogni stanza: `Corner` scrive il carattere d'angolo in (7x, 7y); `DrawDown` scrive il muro
**sinistro** (colonna 7x, righe 7y+1..7y+6) secondo i bit 2-3; `DrawLeft` scrive il muro
**superiore** (riga 7y, colonne 7x+1..7x+6) secondo i bit 6-7. Segmenti da `WallDefs`
(main:4672), 6 celle ciascuno:

| indice | muro verticale (bit 2-3) | indice | muro orizzontale (bit 6-7) |
|---|---|---|---|
| 0 pieno | `9C 9C 9C 9C 9C 9C` | 4 pieno | `93 93 93 93 93 93` |
| 1 porta | `98 DD DD DD DD 94` | 5 porta | `92 DC DC DC DC 91` |
| 2 varco | `98 20 20 20 20 94` | 6 varco | `92 20 20 20 20 91` |
| 3 niente | sei spazi | 7 niente | sei spazi |

Le porte e i varchi sono larghi **4 celle**, con le estremità del muro arrotondate.

`Corner` (main:588) calcola `#90 | U<<3 | D<<2 | L<<1 | R` dove U/D/L/R indicano se dall'angolo
parte un muro (campo < 3, cioè pieno, porta o varco) verso su (muro sinistro della stanza
sopra), giù (muro sinistro di questa), sinistra (muro superiore della stanza a sinistra),
destra (muro superiore di questa). Se nessun muro → spazio `#20`. Da qui i caratteri di muro
`#91-#9F` = "connettori" (bit3 su, bit2 giù, bit1 sinistra, bit0 destra; es. `#93` = orizzontale,
`#9C` = verticale, `#95` = angolo in alto a sinistra, `#9F` = croce). Test "è un muro":
`(codice AND #F0) = #90`.

Bordi: riga 0 e colonna 0 vengono dai muri delle stanze (i campi verso l'esterno restano 0 =
pieni). `PageC22` (main:520) disegna la riga 112: `#99`, poi `#93` (o `#9B` se sopra arriva un
muro verticale `#9C`/`#94`), poi `#9A` in colonna 112. `SideWall` (main:3429) disegna la
colonna 112: `#96` in riga 0, `#9C` (o `#9E` se a sinistra c'è un muro) e `#9A` in fondo.

**Porta verso il Santuario** (fine di `SideWall`): sul piano f (0-7), colonna 112:
`#94` in riga 104-7f, **`#90` (campo di forza) nelle righe 100-7f..103-7f**, `#98` in riga 99-7f.

**Piano 0** (`PAGE`, main:4191): niente labirinto. `Init` + bordo (`#95`, `#93`, `#9C`) +
`PageC22` + `SideWall`: è un'unica **sala aperta** 111×111 con la porta a campo di forza nelle
righe 100-103.

### 2.5 Il Santuario (`InitSanctuary`, main:3849)

Colonne 113-127 (`TopLeftSanc = BigM+113`), righe 0-112:

- Perimetro: riga 0 (`#95`, 13×`#93`, `#96`), colonna 127 (`#9C`), riga 112 (`#93`, angoli
  `#9A`/`#99`), colonna 113 a sinistra.
- **Muro sinistro (colonna 113)**: dal basso, righe 111..99 muro `#9C`; poi 7 volte il blocco
  `#9C, #94, #90, #90, #90, #90, #98` (7 righe ciascuno, k = 1..7) → per il piano k = 1..7 una
  porta a campo di forza nelle righe **100-7k..103-7k**, allineata con la porta della
  colonna 112 dello stesso piano; sopra (righe 49..1) muro pieno. La porta del **piano 0**
  (righe 100-103) è chiusa da muro finché non si raccolgono 7 oggetti fra anelli e chiave
  (`GoldKey`, §4.6).
- Muro orizzontale in riga 22 (`#9D`, `#93`…, `#9E`): chiude la zona del titolo. Nelle righe
  2-17, colonne 115-125, c'è il testo `SMess` scritto **dentro la mappa** come caratteri:
  "Halls / Of / The / Things / Things trained by / Martin Horsley / Neil Mottershead /
  Simon Brattel" (main:4000). Si vede scorrendo lo schermo fin lassù.
- Riga 50: muro `#99`, `#93`×5, `#96` (colonne 113-119); colonna 119: muro `#9C` righe 51-105,
  terminato da `#98` in riga 106. Si forma il **pozzo delle scale**: colonne 114-118, righe
  51-111.
- Scale (`Rung` scrive 5 celle in orizzontale, main:4017), dall'alto, per c = `#87` giù fino a
  `#80` (piano f = c AND 7, da 7 a 0), blocchi di 7 righe:
  - 1 riga `#FC` (pianerottolo vuoto) in riga 100-7f,
  - 3 righe del **marcatore di piano** `#8f` (`#80+f`, invisibile) nelle righe 101-7f..103-7f,
  - 3 righe di gradini `#FD` (righe a strisce) nelle righe 104-7f..106-7f;
  - in fondo 5 righe `#FC` (107-111).
- Il marcatore di piano f è esattamente accanto alla porta del piano f nella colonna 113.
  Stando sul marcatore (con i piedi), il piano visibile a sinistra diventa f (§3.4).
- Zona libera: colonne 120-126 righe 23-111, colonne 114-126 righe 23-49 e 107-111.
- Partenza dell'uomo: riga 109 (testa) / 110 (piedi), colonna 125.

I mostri non entrano mai nel Santuario (si muovono solo su `#20` e il campo di forza `#90`
blocca anche frecce, magie e linea di vista): è il rifugio sicuro.

### 2.6 Viewport e scorrimento

- Lo schermo di gioco è **tutto** lo schermo Spectrum: 32×24 celle = `(XW+4)×(YW+3)`; durante
  il gioco non c'è nessuna barra di stato.
- `pFrameX/pFrameY` = cella della mappa nell'angolo in alto a sinistra dello schermo. `SCV`
  (main:1276) è "visibile" se `0 ≤ col-pFrameX < 32` e `0 ≤ riga-pFrameY < 24` (aritmetica a
  8 bit senza segno).
- **Cambio di schermata a scatti** (`MManNewFrame`, main:1238): se dopo un passo la testa o i
  piedi sono fuori dallo schermo, l'inquadratura si sposta nella direzione del moto di
  **28 colonne (XW)** o **21 righe (YW)**: restano 4 colonne / 3 righe di sovrapposizione.
  Poi `NewScreen` ridisegna tutte le 768 celle.
- **Centratura con SPAZIO** (`JustifyScreen`/`JustifyT`, main:1813): allinea la griglia delle
  stanze: `pFrameX = 7*((x-1) DIV 7) - 8`, `pFrameY = 7*(y DIV 7) - 8` (`Bound7`, main:3472),
  quindi il muro della stanza dell'uomo cade alla colonna/riga 8 dello schermo. Se
  l'inquadratura non cambia non ridisegna (salvo `JFLAG`). Usata anche all'avvio.
- Il disegno è a celle: ogni cambiamento nella mappa visibile viene scritto anche a video con
  `OutIt`. Non c'è double buffering.

### 2.7 Salvataggio dei piani (`SaveFloor`/`Recreate`)

`Gen8` (main:3570) genera tutti gli 8 piani all'inizio: per ciascuno salva il seme (4 byte) in
`MazeList + piano*512`, poi `MAIN`, `PAGE`, `InitMonsters`, `InitTreasure`, `InitMagic`,
`SaveFloor`, e posa anelli/chiave nella mappa (dopo il salvataggio).

Formato del blocco di 512 byte (`SaveFloor`, main:3666):
1. 4 byte: seme del piano;
2. 32 × 4 byte per i mostri: posizione (lo, hi), carattere, `(flag morto AND #80) OR tipo di
   movimento`;
3. terne (lo, hi, codice) per ogni cella con codice `#B0-#CF` (tesori e pozioni) nelle righe
   0-111, colonne 0-111; terminatore `0,0,0`.

`Recreate` (main:3731): ripristina il seme, **rigenera** labirinto (`MAIN`, `PAGE`) e mostri
(`InitMonsters`, con lo stesso seme → stessi valori iniziali), poi sovrascrive posizione,
carattere, tipo di movimento e flag "morto" dei 32 mostri e rimette tesori/pozioni salvati
(se la cella non è vuota, ad esempio una porta, `OpenIfNess` la apre). Svuota i proiettili
(`IPL`), rimette gli anelli non ancora presi (`RingTab`), azzera `Virgin`.

Conseguenze (da riprodurre se si vuole la stessa sensazione):
- le **porte** tornano allo stato generato (chiuse/varchi) a ogni ritorno sul piano;
- i mostri vivi ritrovano **punti vita, magia, frecce e velocità iniziali**; perdono i tesori
  che avevano raccolto;
- i cadaveri restano (carattere salvato + flag morto);
- il Santuario non viene salvato né rigenerato: conserva ciò che vi si lascia.

Il formato a 512 byte potrebbe traboccare se si accumulano oltre ~126 oggetti in un piano
(4 + 128 + 3n ≤ 512): limite irrilevante per la riscrittura.

---

## 3. L'uomo

### 3.1 Rappresentazione

Alto **2 celle** (testa in `pMan`, piedi in `pMan+128`), largo 1. Caratteri per direzione
(`MA`/v11 `ManAngle`: 0 su, 1 destra, 2 giù, 3 sinistra): testa `#A8+2*MA`, piedi
`#A9+2*MA` (`PM`, main:1211): su `A8/A9`, destra `AA/AB`, giù `AC/AD`, sinistra `AE/AF`.
L'uomo è scritto anche nella mappa (così mostri e proiettili lo "vedono" con
`(codice AND #F8) = #A8`), tranne quando la cella della testa contiene un codice ≥ `#80`
(scale, pianerottoli, campo di forza): lì è disegnato solo a video (`UnderMan`, main:1122) e
quindi è invisibile ai proiettili.

### 3.2 Comandi (tutti da tastiera, nessun joystick)

Movimento (lettura diretta delle porte, tasto tenuto premuto = movimento continuo):

| tasto | azione | porta/bit |
|---|---|---|
| Z / X | sinistra / destra | `#FEFE` bit 1 / bit 2 (`MMoveLR`) |
| N / M | su / giù | `#7FFE` bit 3 / bit 2 (`MMoveUD`) |

Z+X insieme o N+M insieme = fermo. Sinistra/destra hanno precedenza su su/giù.

Comandi a impulso (`ScanKeys`, main:3291: memorizzano il fronte di pressione in `KeyxP`,
consumato da `ReadKey`; `ScanKeys` gira **un ciclo sì e uno no**):

| tasto | azione |
|---|---|
| O / C | apri / chiudi porta davanti |
| K / D | prendi (Keep) / lascia (Drop) |
| S | spada |
| A | tira una freccia nella direzione delle frecce |
| 5 / 8 / 7 / 6 | direzione delle frecce: sinistra / destra / su / giù (`ArrowDir`, letta ogni 8 cicli; iniziale: destra) |
| F | palla di fuoco (2 magia) |
| L | fulmine (1 magia) |
| H | guarigione (30 magia) |
| 1 | schermata di stato (pausa; 1 di nuovo per tornare) |
| SPAZIO | centra lo schermo; se prima era stato premuto **W** inverte colore/bianco-nero (`CColour`: tutti gli attributi = 7); se prima era stato premuto **2** apre la porta del piano 0 (**trucco di debug** rimasto nella versione 1983, `DoorHackOpen`, main:4247) |
| Q+T+U+I insieme | abbandona ("QUIT", `CQuit`, main:4311) |

I flag di S, A, F, L vengono azzerati a ogni passo dell'uomo (`MMan5`, main:1094); quelli di
O, C, K, D, H, 1, SPAZIO, W, 2 restano finché non vengono consumati.

### 3.3 Temporizzazione del movimento (`MoveMan`, main:1066)

- `MManDelay` si decrementa a ogni ciclo di `GameLoop`; a 0 viene ricaricato con
  `MMID = 10` e l'uomo agisce: **un passo ogni 10 cicli**.
- Se la direzione richiesta è diversa da quella attuale, l'uomo **si gira senza muoversi** e
  il ritardo diventa **30 cicli** (main:1106-1117). Quindi cambiare direzione costa 3 passi.
- Ordine dentro un passo: `CQuit`, porte, prendi/lascia, poi (se la testa non è su `#90`)
  freccia, palla di fuoco, fulmine, spada (se la spada è in corso l'uomo non si muove), poi
  il movimento.
- Il passo è valido se testa e piedi nella nuova posizione passano `ManValid` (main:1192):
  `#20`, `#FC`/`#FD`, `#80-#87`, oppure `#90` secondo `OpenPlan`. Tutto il resto (muri,
  porte chiuse, oggetti, mostri, cadaveri, proiettili) blocca. Gli oggetti si raccolgono
  restando fermi davanti (K).

### 3.4 Cambio di piano (`MManRE1` main:1203, `NFloor` main:4030)

Se la cella dei **piedi** di destinazione è `#80-#87`, `NFloorFl = (codice AND 7)+1`; dopo il
passo `NFloor` (se il piano è diverso e se non si possiede la chiave, `cFloor = 8`) salva il
piano attuale, imposta il nuovo, `Recreate`, ridisegna, `PlaceMon`. L'uomo resta nel
Santuario; cambia il labirinto a sinistra. Con la chiave d'oro (`cFloor = 8`) le scale non
cambiano più piano.

### 3.5 Stato del giocatore (`InitGame`, main:4426; variabili main:4752-4775)

| variabile | iniziale | massimo | note |
|---|---|---|---|
| `Wounds` (ferite) | 0 | `MaxWounds = 127` | a ≥ 127 → morte (`HitMan`, main:2527) |
| `CurrentMagic` | 120 | 255 (`MaxMagic`) | munizioni per magie e guarigione |
| `ArrowCount` | 255 | 255 | frecce (in pratica illimitate) |
| `TreasureCount` + `SwagBag` | 0 | `MaxTreasure = 5` | tesori trasportati, pila LIFO |
| `nDTs` | 0 | 255 | "Dead Things", mostri uccisi (anche da altri mostri!) |
| `NRings` | 0 | 8 | anelli + chiave |

Non c'è forza/energia che cala col tempo, né vite multiple: una sola vita.
**Cosa uccide l'uomo**: solo i proiettili dei mostri (e i propri fulmini/palle di fuoco di
rimbalzo): frecce 21-40 ferite, palla di fuoco 15, fulmine 10 (§4). I mostri non fanno danno
al contatto.

### 3.6 Guarigione (`TryHealing`, main:4457)

H: se magia ≥ 30, magia −30 e ferite −(16..31) (`(R AND 15)+16`), minimo 0. Funziona anche
nella schermata di stato.

### 3.7 Prendere e lasciare (`MMKeepDrop`, main:796)

Cella "davanti": su → `pMan-128`; giù → `pMan+256` (sotto i piedi); sinistra/destra →
`pMan±1` (all'altezza della testa) e, se lì non c'è nulla da prendere, anche all'altezza dei
piedi.

- `#B0-#BF` tesoro: se meno di 5, aggiunto alla borsa.
- `#C0-#CF` pozione: magia += 2×(codice AND 15) (0..30), rifiutata se il totale arriverebbe a
  ≥ 255.
- `#D8-#DB` fascio di frecce: +1 freccia (codice gestito ma **mai generato** nel 1983).
- `#88-#8F` chiave/anello (`MMKRing`, main:4139): segnato come preso in `RingTab`,
  `NRings+1`; la chiave `#88` imposta `cFloor = 8`; quando `NRings` arriva a 7 → `GoldKey`.
- D lascia l'ultimo tesoro preso nella cella davanti (testa, o piedi per sinistra/destra),
  solo se è `#20`.

### 3.8 Porte (`MMoveDoor`, main:684)

- **O**: se la cella davanti è una porta (`#DC`/`#DD`), scorre lungo la porta in direzione
  perpendicolare fino alla fine e sostituisce le 4 celle con `#20`.
- **C**: la cella davanti deve essere `#20`; scorrendo in perpendicolare deve trovare un muro
  (`#9x`), e dal muro esattamente 4 spazi seguiti da un altro muro: allora riempie le 4 celle
  con `#DC` (porta in un muro orizzontale) o `#DD` (in un muro verticale). Quindi si possono
  chiudere i varchi e le porte aperte, non i passaggi senza muro.
- I mostri aprono le porte (§5.4), non le chiudono. Lo stato delle porte non è salvato (§2.7).

---

## 4. Armi, oggetti, vittoria

### 4.1 Motore dei proiettili (`PL`, 32 slot × 32 byte; `MoveProj`, main:2274)

Slot condiviso fra uomo e mostri (`FPS`, main:3047 = primo slot con vita 0; se sono tutti
occupati non si spara e i mostri non controllano nemmeno la linea di vista). Campi (v11 li
nomina `dp_*`):

| off | nome v11 | uso |
|---|---|---|
| 0, 1 | Delay, iDelay | contatore e ricarica: il proiettile avanza ogni `iDelay` cicli |
| 2, 3 | pMaze | posizione in `BigM` |
| 4, 5 | Off | passo (freccia) / passo diagonale di Bresenham |
| 6, 7 | pCode | routine di movimento (`MoveArrow`, `MoveFire`, `MoveLight`) |
| 8 | Life | vita in passi (0 = slot libero) |
| 9 | MissileChar | carattere |
| 10 | BackingChar | 1 se il carattere è stato scritto nella cella (da cancellare al passo dopo) |
| 11, 12 | Step1 | passo sull'asse maggiore |
| 13, 14, 15 | ErrR, ErrS, ErrM | errore, delta minore, delta maggiore (Bresenham) |
| 16, 17 | pOwner | puntatore alla posizione di chi ha sparato |
| 18, 19 | pTarget | puntatore alla posizione del bersaglio (palla di fuoco a ricerca) |
| 22 | ManF | 1 = sparato dall'uomo |
| 23 | UpdateRate | intervallo di riaggancio del bersaglio (sempre 1) |
| 24, 25 | TargetiPos | ultima posizione nota del bersaglio |
| 26, 27 | pTarget2 | record del mostro preso di mira dall'uomo (flag da azzerare) |
| 28-30 | RSave, pMazeSave | stato salvato per il rimbalzo del fulmine |

Ogni passo: cancella il carattere precedente (se scritto), sposta, e se la nuova cella è
`#20` vi scrive il carattere (nella **mappa** e a video). Se la cella è occupata da altro
(oggetti, altri proiettili) il proiettile ci passa sopra invisibile. I proiettili nella
mappa bloccano il movimento di uomo e mostri. Vita: un passo in meno per ciclo di movimento;
a 0 lo slot si libera.

`InitLine` (main:2604) + `NextPoint` (main:2720): Bresenham classico fra cella di partenza
e bersaglio (8 direzioni), che continua oltre il bersaglio sulla stessa retta. Il conteggio
iniziale è la distanza di Chebyshev max(|dx|,|dy|).

### 4.2 Frecce (`MFireArrow` main:2368, `MoveArrow` main:2493)

- Tasto A, serve uno slot libero e `ArrowCount > 0` (−1). Direzione = `ArrDir` (5/6/7/8),
  indipendente da dove guarda l'uomo.
- Partenza: su → dalla testa; giù → dai piedi; sinistra/destra → dalla testa o dai piedi.
  Scelta della riga (sinistra/destra): scansione in avanti di entrambe le righe; se nella
  riga dei piedi compare per primo un mostro la freccia parte dai piedi, altrimenti dalla
  testa; quando una riga incontra un muro la scansione continua sull'altra (con un'asimmetria:
  se si blocca prima la riga della testa, un mostro trovato poi nella riga dei piedi fa
  partire comunque la freccia dalla testa). In pratica: "la freccia va nella riga in cui c'è
  il mostro più vicino, di default all'altezza della testa".
- Velocità **5 cicli/passo**, vita **32** (≈31 celle), carattere `#A0` (orizzontale) o
  `#A1` (verticale).
- Si ferma su muri `#9x` (compreso il campo di forza) e porte. Su un mostro: **70 danni**
  (`HMon`, main:2546), cadavere `#DF`. Sull'uomo: ferite = `(vita_rimasta + 25) DIV 2`.
- Freccia dei mostri (in `CheckCombat`): velocità **15**, vita **55** (`ArrowLife`), solo in
  orizzontale o verticale; il primo passo avviene subito senza consumare vita, poi −1 per
  passo: alla distanza k la vita è 56−k e le ferite sono `(56 − k + 25) DIV 2` (40 a
  bruciapelo, ~21 a 39 celle; oltre 39 i mostri non tirano frecce).

### 4.3 Palla di fuoco (`FireBall` main:2879, `MoveFire` main:2946)

- Tasto F: serve uno slot; costa **2 magia**, scalata **anche se non c'è un bersaglio**.
- Bersaglio: il mostro vivo più vicino (distanza di Chebyshev) entro **40 celle** (`FBSR`) in
  linea di vista senza porte, non già bersaglio di un'altra palla (`MonsterSearch`,
  main:2760). Il mostro viene marcato (bit 7 del suo byte 26) finché la palla esiste.
- **A ricerca**: a ogni passo, se il bersaglio si è mosso, la retta viene ricalcolata dalla
  posizione corrente. Velocità **24 cicli/passo** (`fx`), vita **50** passi (`FL`).
  Carattere animato `#A4-#A7`.
- Danni: mostro **40** (cadavere che brucia `#E0`); uomo **15**. Una palla dell'uomo non lo
  colpisce nei primi 3 passi (dopo sì). Si ferma su muri/porte e al primo colpo.
- I mostri lanciano palle di fuoco mirate all'uomo, con le stesse caratteristiche.
- Bug del 1983 (v11 lo corregge con `FixBug`): il controllo "non colpire chi l'ha lanciata
  nei primi 3 passi" legge il puntatore dagli offset 10-11 invece di 16-17 (`HFireMon`,
  main:3003; `HLightMon`, main:3165), quindi in pratica le magie colpiscono qualsiasi mostro
  incontrino, anche quello che le ha lanciate.

### 4.4 Fulmine (`Lightning` main:3064, `MoveLight` main:3116)

- Tasto L: serve uno slot; costa **1 magia** (anche senza bersaglio). Bersaglio: mostro più
  vicino entro **50** (`LtSR`) visibile.
- **Non** insegue: parte sulla retta verso il bersaglio e **rimbalza** su muri e porte
  (`MLightRefl`, main:3187: torna alla posizione precedente e inverte la componente di moto
  che urta un muro; se urta di spigolo inverte tutto). Velocità **14**, vita **100** (`Ll`).
  Carattere `#A2/#A3` alternati.
- Danni: mostro **40** (cadavere `#E0`), uomo **10** (escluso nei primi 3 passi se è suo).
  **Non si ferma** quando colpisce: attraversa e può colpire più volte, anche chi l'ha
  lanciato dopo un rimbalzo.
- I mostri lo lanciano con le stesse caratteristiche (il campo 22 non viene inizializzato:
  valore residuo, dettaglio trascurabile).

### 4.5 Spada (`MSword`, main:954)

- Tasto S: 5 fasi, una per passo dell'uomo (quindi 50 cicli), durante le quali l'uomo non si
  muove. La spada è disegnata **solo a video** (la mappa non cambia) nelle celle davanti
  secondo `SwdUpTab/SwdRtTab/SwdDnTab/SwdLfTab` (main:1042, 15 byte per direzione: 5 voci
  "spostamento rispetto alla posizione precedente, carattere"):
  - guardando su: celle sopra la testa centro, sinistra, centro, destra, centro con
    `#E5 | #E6 \ #E5 | #E4 / #E5 |` (spazzata);
  - destra/sinistra: cella davanti alla testa e davanti ai piedi con `#E4`/`#E6`/`#E7`;
  - giù: come su, sotto i piedi.
- Si interrompe su porta o muro; su un mostro **15 danni**, cadavere `#DE`, e si interrompe.
- Non utilizzabile stando sul campo di forza.

### 4.6 Tesori, pozioni, anelli, chiave

- Per ogni piano (compreso lo 0): **40 tesori** `#B0 | (Rand AND 15)` (valore 0-15, la
  dimensione del mucchio nel glifo cresce col valore) e **40 pozioni** `#C0 | (Rand AND 15)`
  (valore 2×nibble, il livello della bottiglia nel glifo indica il contenuto), in celle vuote
  casuali delle righe/colonne 0-111 (`FindPosition`, main:1422).
- **Anelli e chiave** (`Gen8`/`DefRing`, main:3570-3653; `RingTab` = 8 voci × 3 byte:
  posizione, piano; `#FF` = preso):
  - voce 0 = **chiave d'oro `#88`**, sempre sul **piano 0**;
  - voci 1-7 = **sette anelli `#89-#8F`** (colori diversi), ciascuno su un piano casuale 1-7
    (`RandRange(7)+1`: più anelli possono stare sullo stesso piano, alcuni piani possono non
    averne);
  - posizione casuale con **colonna < 30** (la parte sinistra del piano, lontana dal
    Santuario).
- Gli anelli non si possono lasciare. I mostri non li raccolgono.
- **`GoldKey`** (main:4175): quando `NRings` diventa 7 copia il blocco della porta del
  piano 1 sul muro sinistro del Santuario alle righe 99-104: si apre la porta del piano 0.
  (La chiave sta sul piano 0, raggiungibile solo dopo, quindi in pratica servono i 7 anelli.)

### 4.7 Come si vince (`OpenPlan` main:4214, `DunIt` main:4333)

Il campo di forza `#90` è percorribile sui piani 1-7. Sul piano 0:
- se `Virgin = 0` (nessun mostro del piano ha ancora visto l'uomo da quando ci è entrato)
  è percorribile;
- se `Virgin = 1` (un mostro lo ha visto, `CheckCombat`) blocca, **a meno che l'uomo abbia la
  chiave** (`cFloor = 8`): in quel caso toccarlo è la **vittoria**.

Percorso: raccogliere i 7 anelli sui piani 1-7 → la porta del piano 0 nel Santuario si apre →
scendere al marcatore del piano 0, attraversare il campo di forza ed entrare nella sala
aperta (32 mostri, linee di vista lunghe) → prendere la chiave (a sinistra, x < 30) → tornare
alla porta e toccare il campo di forza. Con la chiave le scale non funzionano più. (Dedotto:
il flag `Virgin` serve a non far scattare la vittoria appena si entra; in pratica i mostri
vedono sempre l'uomo prima che torni.)

---

## 5. Le "Things" (mostri)

### 5.1 Numero, tipo, aspetto

**32 mostri per piano** (`InitMonsters`, main:1447), compreso il piano 0. Non hanno nomi.
Il carattere (`#D0 + RandRange(8)`) è **solo estetico**: tutti i mostri condividono le stesse
regole e statistiche casuali. Glifi (magenta brillante `#43`): `#D0`=`#D4` (creatura con
occhi), `#D1`=`#D5` (piena), `#D2`, `#D3` (forme speculari), `#D7` (teschio/ragno).
**`#D6`** (giallo `#06`) è il mostro **travestito da tesoro** (glifo quasi identico a un
mucchio `#BC-#BF`): circa 1 su 8 nasce così.

### 5.2 Record (`ML`, 64 byte per mostro) e valori iniziali

`IMons1` (main:1474): per i primi 32 byte, `valore = base + RandRange(variazione)` dalla tabella
`MonsterTypes` (main:4681: 32 byte di base seguiti da 32 di variazione); poi posizione da
`FindPosition` (cella vuota nelle righe/colonne 0-111); byte 32-63 azzerati.

| off | significato (v11) | base | variazione | risultato |
|---|---|---|---|---|
| 0-1 | posizione in `BigM` | – | – | casuale |
| 2 | carattere | `#D0` | 8 | `#D0-#D7` |
| 4 | tipo di movimento | 0 | 3 | 0, 1 (casuale) o 2 (segui-muro) |
| 6 | "Strength" (non usato) | 100 | 20 | – |
| 7 | bit 7 = morto | 0 | – | |
| 8 | magia | 16 | 8 | 16-23 |
| 9 | magia massima | 32 | 16 | 32-47 |
| 10 | n. tesori portati | 0 | – | max 4 |
| 12 | (non usato) | 100 | 50 | – |
| 16 | contatore di ritardo | 0 | – | primo movimento dopo 256 cicli |
| 18 | ricarica (velocità) | 60 | 90 | **60-149 cicli per passo** |
| 20 | frecce | 4 | 3 | 4-6 |
| 22 | **punti vita** (v11 "Wounds") | 30 | 20 | **30-49** |
| 26 | flag: bit 0 ingaggiato (vede l'uomo), bit 7 bersaglio di una palla di fuoco | | | |
| 27 | direzione 0-7 | 0 | | |
| 28-31 | tesori portati | | | |
| 32-33 | posizione "segno" (segui-muro) | | | |
| 34 | contatore passi segui-muro | | | |
| 35 | flag "può diventare segui-muro" | | | |

I campi 6, 12, 21, 23, 24 non sono letti da nessuna parte nel 1983 (residui della versione
preliminare con la "forza", cfr. `index.htm`: "we did away with strength later").

**Difficoltà per piano** (main:1455-1470): sul piano f (1-7) i primi f mostri hanno **magia
255** e **ricarica 30** (il doppio più veloci dei più rapidi normali); il mostro f-1 ha anche
**255 punti vita**. Sul piano 0 nessun potenziamento.

### 5.3 Ciclo di aggiornamento (`MoveMonsters` main:1542, `MMons2` main:1554)

A ogni ciclo, per ciascuno dei 32: decrementa il contatore (byte 16); a 0 lo ricarica (byte 18)
e agisce:

1. **Morto** (bit 7 di 7): `SteamRise` → se il carattere è `#E0-#E3` lo fa ciclare
   (fiamme/vapore animati); i cadaveri `#DE`/`#DF` restano fermi. I cadaveri bloccano il
   passaggio.
2. **Travestimento**: se ingaggiato e travestito (`#D6`) si rivela con un carattere casuale
   `#D0-#D7` (se esce `#D6` diventa `#D7`). Se non ingaggiato, fuori dallo schermo e con
   probabilità ≈ 1/64 (`(Seed+1 OR R) AND #3F = 0`) si traveste da `#D6`. Un mostro travestito
   **non si muove** ma esegue comunque `CheckCombat` (può sparare).
3. Cancella sé stesso dalla mappa (`#20`) e dal video, si muove secondo il tipo (§5.4),
   esegue `CheckCombat` (§5.5), si riscrive nella nuova cella.

### 5.4 Movimento

Non c'è inseguimento: i mostri vagano e combattono a distanza.

- **Tipo 0/1, vagabondo in 8 direzioni** (`MoveThing1`, main:2052; `CDirect1`: 0 su, 1 destra,
  2 giù, 3 sinistra, 4 su-sinistra, 5 su-destra, 6 giù-destra, 7 giù-sinistra). Se la cella
  nella direzione corrente è `#20` ci va, poi con probabilità 1/8 sceglie una nuova direzione
  casuale. Se è bloccata: se è un tesoro/pozione prova a raccoglierlo (§5.6, e mantiene la
  direzione), altrimenti nuova direzione casuale 0-7; se l'ostacolo era un muro e il flag 35
  è attivo diventa segui-muro (tipo 2 o 3 a caso) e memorizza la posizione come "segno".
- **Tipo 2, segui-muro con la mano destra** (`MoveThing2`, main:2073): se davanti è libero,
  prova prima la cella alla sua destra (dalla posizione attuale): se libera ci va e gira a
  destra, altrimenti va avanti. Se davanti è bloccato (e non è un oggetto raccoglibile) gira a
  sinistra senza muoversi.
- **Tipo 3, segui-muro con la mano sinistra** (`MoveThing3`, main:2156): speculare.
- **Fine del segui-muro** (`WallFC`, main:2099): se torna alla cella "segno" diventa vagabondo
  (tipo 1, flag 35 = 1) con direzione diagonale casuale 4-7. Il segno è fissato al primo
  passo e ogni 256 passi (contatore 34), e quando apre una porta.
- I mostri dei tipi 0/1 iniziali non diventano mai segui-muro (flag 35 = 0); quelli nati di
  tipo 2 alternano segui-muro e vagabondaggio.
- **Porte** (`MonODoor`, main:4058): se la cella di destinazione è una porta, il mostro la
  apre (tutte e 4 le celle) se dalla sua posizione l'uomo è visibile entro 50 celle **oltre**
  quella porta (la porta è la prima cella della retta verso l'uomo); altrimenti la apre
  comunque con probabilità 1/4. In quel passo non avanza.

### 5.5 Combattimento (`CheckCombat`, main:1654)

Eseguito a ogni azione del mostro (anche se travestito):

1. Serve uno slot di proiettile libero (usato come area di lavoro); altrimenti niente.
2. **Linea di vista** (`RangeLook`/`RangeDoor`, main:4231/2802): |dx| < 50 e |dy| < 50, poi
   Bresenham dal mostro alla testa dell'uomo: muri `#9x` (compreso il campo di forza) e porte
   bloccano. Altri mostri, oggetti e proiettili non bloccano. `d` = distanza di Chebyshev.
3. Se non vede: azzera il flag "ingaggiato" e basta. Se vede: `Virgin = 1`, flag ingaggiato.
4. **Freccia**: solo se l'uomo è sulla stessa riga o colonna (delta minore 0), se
   `RandRange(40) ≥ d` (probabilità (40−d)/40) e se ha frecce (−1).
5. **Magie**: se magia ≥ 6 e `RandRange(12) < magia` (sempre vero da 12 in su):
   con `R AND 7` = 0 (1/8) **fulmine** (costo 6), = 1 (1/8) **palla di fuoco** (serve magia
   ≥ 10, costo 10), altrimenti niente. La magia si ricarica solo raccogliendo pozioni.

### 5.6 Raccolta di oggetti (`MonKeep`, main:2220)

Un mostro bloccato da un oggetto: pozione → magia += 2×valore se non supera il massimo
(byte 9) e non trabocca; tesoro → lo mette nei byte 28-31 se ne ha meno di 4. La cella viene
svuotata. Alla morte gli oggetti presi vanno persi (non c'è rilascio).

### 5.7 Morte (`HitMonster`, main:2550)

Danno sottratto ai punti vita (byte 22); se va sotto zero: flag morto, carattere del cadavere
(`#DE` spada, `#DF` freccia, `#E0` magia, quest'ultimo animato), `nDTs+1` (saturato a 255).
Conta anche se a uccidere è un altro mostro. Se il mostro colpito non si trova nella lista
(non dovrebbe succedere) il gioco ricomincia (`FuckUp` = `jp ITB`).

| arma | danno al mostro | colpi per un mostro normale (30-49 PV) |
|---|---|---|
| spada | 15 | 2-4 |
| freccia | 70 | 1 (il mostro da 255: 4) |
| palla di fuoco | 40 | 1-2 |
| fulmine | 40 (può colpire più volte) | 1-2 |

---

## 6. Punteggio, stato, suoni, tempi

### 6.1 Punteggio (`DoScor`/`DoS1`, main:4290)

- Morte o abbandono: `2×nDTs + 128×NRings` (+128 se `NRings = 8`, cioè 7 anelli + chiave).
- Vittoria: lo stesso **più la somma dei valori (0-15) di tutti i tesori presenti nella mappa
  corrente** (`CTres` scorre tutti i 16 KB di `BigM`): cioè i tesori lasciati nel
  **Santuario** durante la partita più quelli ancora per terra nella sala del piano 0. I
  tesori in borsa **non** contano: vanno posati (D). (Dedotto: lo scopo della borsa da 5 è
  riportare i tesori al Santuario.)
- La variabile `Score` (main:4757) non è usata.

### 6.2 Schermata di stato (tasto 1; `DisplayScore` main:3498, `InitScore` main:4480)

Il gioco è in pausa (si può solo guarire con H); 1 di nuovo torna alla mappa. Coordinate
(riga, colonna), `ScorePos = #1201` = riga 18 colonna 1:

| riga | contenuto |
|---|---|
| 0 | " -- Halls of the Things --" |
| 1 | "___________________________" |
| 4 | "(c) Design-Design Software" |
| 6, 7, 8 | "    Simon Brattel", "    Neil Mottershead", " &  Martin Horsley" |
| 15 | "Rings found" in col. 1; da col. 15 i glifi degli anelli presi, da `#8F` a `#88` |
| 18 | "100%          0%" nelle colonne 15-30 |
| 19 | "Magic" + barra (magia/2) |
| 20 | "Wounds" + barra (ferite) |
| 21 | "Dead Things" + barra (nDTs/2) |
| 22 | "Arrows" + barra (frecce/2) |

`PrintBar` (main:4598): valore v = 0..127 su 16 celle (colonne 15-30) da 8 pixel = 1 pixel per
unità; tacche `#F1` in colonna 14 e `#F2` in colonna 31. **La barra si riempie da destra
(0%) verso sinistra (100%)**: dalla colonna 30 verso sinistra `v DIV 8` blocchi pieni `#F0`,
poi una cella parziale `#E8 + (v AND 7)`, poi celle vuote `#E8`.

### 6.3 Suoni

**Nessuno.** In tutto il codice del 1983 l'unico `out (#FE)` imposta il bordo nero
(main:4427, front:69); non ci sono routine di beeper. Il gioco è muto.

### 6.4 Tempi (in cicli di `GameLoop`; vedi §9 per la durata reale di un ciclo)

| evento | cicli |
|---|---|
| passo dell'uomo | 10 (`MMID`) |
| rotazione dell'uomo | 30 |
| fase della spada | 10 (5 fasi = 50) |
| freccia dell'uomo | 5 per passo, 31 passi |
| freccia dei mostri | 15 per passo, 55 passi |
| fulmine | 14 per passo, 100 passi |
| palla di fuoco | 24 per passo, 50 passi |
| mostro normale | 60-149 per azione (primo movimento dopo 256) |
| mostro potenziato | 30 |
| lettura tasti a impulso | ogni 2 cicli |
| lettura direzione frecce | ogni 8 cicli |

Nessun timer di partita e nessuna difficoltà selezionabile: la difficoltà cresce col piano
(labirinto più aperto e quindi linee di vista più lunghe, più mostri potenziati) e col piano
0 finale (sala aperta).

---

## 7. Schermo e caratteri

### 7.1 Layout

Durante il gioco tutto lo schermo 256×192 (32×24 celle 8×8) mostra la mappa; bordo nero.
Ogni cella ha l'attributo del proprio carattere (paper sempre nero). La schermata di stato e
il front end usano lo stesso set di caratteri (lettere `#20-#7F` bianche).

### 7.2 Formato del file dei caratteri

`chars.bin` (2048 byte, caricato a `#6800`) e la sua versione testuale `halls83_chars.asm`:

- **Byte 0-255: attributo del carattere con codice = indice** (`OutIt`: `ld h,#68 / ld l,a /
  ld c,(hl)`, main:1326). Nel testo: dopo "Attributes for characters #00..#FF", 16 righe di
  16 `db #xx` (chars:3-19).
- **Byte 8·c .. 8·c+7: bitmap del carattere c**, per c = `#20..#FF` (i codici `#00-#1F` non
  hanno bitmap: la loro area coincide con la tabella degli attributi). Nel testo: dopo
  "Bitmaps for chars #20..#FF", per ogni carattere una riga `; #XX` seguita da 8 righe
  `db %bbbbbbbb ; disegno` (dall'alto in basso, bit 7 = pixel più a sinistra), 224 caratteri.
  Il file termina con un `\` spurio.
- Estrazione: regex `^\s*db\s+(#..(,#..)*)` per gli attributi, `^; #([0-9A-F]{2})` e
  `db\s+%([01]{8})` per le bitmap (verificato: 256 attributi, 224 caratteri da 8 righe).
- Attributo Spectrum: bit 7 FLASH, bit 6 BRIGHT, bit 5-3 PAPER, bit 2-0 INK
  (0 nero, 1 blu, 2 rosso, 3 magenta, 4 verde, 5 ciano, 6 giallo, 7 bianco). Nessun
  carattere usa FLASH; paper sempre 0.

### 7.3 Tabella dei codici (mappa e grafica)

| codici | attributo | significato |
|---|---|---|
| `#20` | `#07` | pavimento vuoto |
| `#21-#7F` | `#07` bianco | font ASCII (testi) |
| `#80-#87` | `#41` blu brillante, glifo vuoto | marcatore di piano 0-7 sulle scale (invisibile) |
| `#88` | `#06` giallo | chiave d'oro |
| `#89-#8F` | `#41`,`#42`,`#03`,`#04`,`#45`,`#06`,`#07` | 7 anelli (stesso glifo, colori blu br., rosso br., magenta, verde, ciano br., giallo, bianco) |
| `#90` | `#45` ciano brillante, puntini radi | campo di forza (porte del Santuario) |
| `#91-#9F` | `#04` verde | muri, connettori U/D/L/R (§2.4) |
| `#A0` / `#A1` | `#47` bianco br. | freccia orizzontale / verticale |
| `#A2`, `#A3` | `#47` | fulmine (2 fotogrammi) |
| `#A4-#A7` | `#42` rosso br. | palla di fuoco (4 fotogrammi) |
| `#A8-#AF` | `#07` | uomo: su A8/A9, destra AA/AB, giù AC/AD, sinistra AE/AF (testa/piedi) |
| `#B0-#BF` | `#06` | tesoro, valore = nibble basso; 4 dimensioni del mucchio (B0-3, B4-7, B8-B, BC-F) |
| `#C0-#CF` | `#45` | pozione, valore 2×nibble; 4 livelli della bottiglia |
| `#D0-#D5`, `#D7` | `#43` magenta br. | mostri |
| `#D6` | `#06` | mostro travestito da tesoro |
| `#D8-#DB` | `#07` | fascio di frecce (raccoglibile, mai generato) |
| `#DC` / `#DD` | `#04` | porta in muro orizzontale / verticale |
| `#DE` / `#DF` | `#42` | cadavere (spada / freccia) |
| `#E0-#E3` | `#42` | cadavere che brucia (animato, magia) |
| `#E4-#E7` | `#07` | spada: `/`, `|`, `\`, trattino in alto (solo a video) |
| `#E8-#EF` | `#06` | barra parzialmente piena (0-7 pixel, da destra) |
| `#F0` | `#06` | barra piena |
| `#F1`, `#F2` | `#06` | tacche agli estremi della barra |
| `#F3-#FB`, `#FE` | `#00` | vuoti, non usati |
| `#FC` | `#41`, vuoto | pianerottolo del Santuario (percorribile) |
| `#FD` | `#41`, 4 righe orizzontali | gradini (percorribili) |
| `#FF` | `#41`, X | fuori mappa |

Non esistono sprite grandi: tutto è a celle 8×8. L'unico oggetto multi-cella è l'uomo
(2 celle in verticale). `BigM` ("Big Maze") è la mappa, non uno sprite.

### 7.4 `OutIt` (main:1319)

Input: H = riga schermo (0-23), L = colonna (0-31), A = codice. Copia 8 byte nelle righe di
pixel della cella (indirizzo base da `SpecTab`, main:4777) e scrive l'attributo in
`#5800 + 32·riga + colonna` (o 7 in modalità bianco/nero).

---

## 8. Generatore casuale

### 8.1 `Rand1` (main:391), esatto

Seme di 32 bit `s` (byte `Seed+0` meno significativo). Con aritmetica a 32 bit di BBC BASIC V:

```
DEF FNrand
LOCAL w0%,w1%,bc%,de%,t%,neg%
w0% = s% AND &FFFF
w1% = (s% >>> 16) AND &FFFF
bc% = ((((s% AND &FF) << 1) OR ((w1% >> 14) AND 1)) AND &FF) << 8
bc% = bc% OR ((w1% >> 6) AND &FF)
de% = (s% >>> 7) AND &7FFF
t% = w0% + bc%
w0% = t% AND &FFFF
t% = w1% + de% + (t% >> 16)
neg% = (t% AND &8000) <> 0
w1% = t% AND &7FFF
s% = (w1% << 16) OR w0%
IF NOT neg% THEN s% += 1
= s% AND &FF
```

Derivazione: `HL=(Seed+2)` ruotato due volte a sinistra attraverso il carry (il carry
iniziale finisce nei bit bassi di L, che non vengono usati) → `C` = bit 13-6 di w1, carry =
bit 14 di w1; `B = (Seed+0)<<1 | carry`; `DE = (Seed+1..2)` ruotato di 1 a sinistra col bit 7
di `Seed+0` entrante, bit 15 azzerato; `w0 += BC`; `w1 = (w1 + DE + carry) AND &7FFF`;
se il bit 15 della somma era 0, incremento a 32 bit (`Rnd1`, propagazione byte per byte).
`Rand` restituisce il byte basso del nuovo seme.

### 8.2 Derivati

- `Rnd7` (main:477) = `Rand` (e confronta con `(iy)`, usato solo dal generatore).
- `Rnd2` = `Rand AND 3`.
- `Random(k)` (main:431), scelta fra k mosse: k=1 → 0; k=2 → `Rand AND 1`; k=3 → ripete
  `Rand AND 3` finché ≤ 2; k=4 → `Rand AND 3`.
- `Rnd6` (main:461): vedi §2.3.
- `RandRange(n)` (main:1517) = `Rand MOD n` (0 se n = 0).
- `FindPosition` (main:1422): `Rand`, poi dal seme basso a 16 bit: colonna = `l AND #7F`
  (< 112), riga = `2·(h AND #3F) + (l >> 7)` (< 112); ripete finché la cella non è `#20`.
- **Registro R**: molte decisioni minori usano `ld a,r` (direzioni dei mostri, porte,
  guarigione, scelta della magia, travestimento). `GameLoop` scrive in R un `Rand` a ogni
  ciclo, poi R avanza con le istruzioni eseguite: è un secondo generatore "povero". Nella
  riscrittura basta un normale `RND`.

Il seme di partenza viene da FRAMES all'avvio (§1.2). Ogni piano conserva il proprio seme per
rigenerarsi identico. Per la riscrittura non serve riprodurre i labirinti del Spectrum, ma
usare lo stesso generatore e lo stesso ordine di chiamate dà labirinti statisticamente
identici (e, a parità di seme, gli stessi).

---

## 9. Dettagli sottili per la riscrittura

### 9.1 Nessun clock: la velocità è quella della CPU

Gli interrupt sono disabilitati per tutta la partita (`di` in `ITB`; `im 2` del front end
non viene mai attivato); non c'è `HALT` né sincronismo col video. Ogni grandezza temporale è
espressa in **cicli di `GameLoop`**, la cui durata dipende dal lavoro svolto. Stima del
ciclo "a vuoto" (nessun mostro che agisce, nessun proiettile), contando gli stati T:
`MoveMonsters` 32×61 ≈ 1950, `MoveProj` 32×92 ≈ 2950, `ScanKeys` ≈ 1600 ogni due cicli
(≈ 800 in media), `Rnd7`+`Rand1` ≈ 400, il resto ≈ 500: **≈ 6.600 T**, più la contesa di
memoria (una parte del codice sta in `#7000-#7FFF`, RAM contesa) → circa 7.500-8.000 T ≈
**2,2 ms a 3,5 MHz (~450 cicli/s)**. Ogni azione di mostro aggiunge da qualche centinaio a
~10.000 T (la linea di vista fino a 50 celle con `NextPoint`), ogni passo di proiettile e ogni
`OutIt` (~400 T) si aggiungono; un cambio di schermata (`NewScreen`, 768 `OutIt`) costa
circa 0,1 s ed è una pausa visibile. Valore tipico stimato: **2,5-3,5 ms per ciclo,
300-400 cicli/s**, quindi:

- uomo ≈ 30-40 celle/s (è molto veloce: attraversa una schermata in meno di un secondo);
- frecce dell'uomo ≈ 60-80 celle/s, fulmine ≈ 25 celle/s, palla di fuoco ≈ 15 celle/s;
- mostri ≈ 2-7 celle/s, quelli potenziati ≈ 10-13.

Questi valori sono **stime da calibrare**: in `original/` c'è `halls - how she is played.rzx`
(partita registrata) e le immagini `halls.tzx.zip`/`halls_*.szx`; in un emulatore (Fuse)
si può misurare per esempio quanti fotogrammi servono all'uomo per attraversare 28 colonne.

Raccomandazione: simulare a **tick logici fissi** (un tick = un ciclo di `GameLoop`, tutti i
contatori come nel sorgente) a una frequenza costante calibrata (ordine di 300-450 tick/s),
eseguendo più tick per fotogramma video (es. 6-9 tick per frame a 50 Hz) e ridisegnando solo
le celle cambiate. In BBC BASIC conviene mettere in assembler ARM almeno `MoveMonsters`
(linea di vista), `MoveProj` e il disegno delle celle. L'originale rallentava un po' quando
c'erano molti mostri attivi o proiettili; un tick fisso è un'approssimazione accettabile.

### 9.2 Altri dettagli da riprodurre

- I proiettili e l'uomo stanno **nella mappa**: tutti i test di collisione sono letture di
  `BigM`. Riprodurre la mappa come matrice di codici con gli stessi intervalli semplifica tutto.
- I mostri vengono aggiornati in ordine fisso 0-31 e i proiettili 0-31 a ogni ciclo, con i
  loro contatori: l'ordine conta per le collisioni.
- L'uomo sulle scale/pianerottoli/campo di forza non è scritto nella mappa (§3.1).
- Effetto collaterale (dedotto): uscendo da una posizione in cui solo la testa era su `#FC`/
  `#FD`/`#8x` e i piedi su `#20`, le due celle vengono azzerate a `#20`, cancellando un pezzo
  di gradino. Innocuo, ma esiste.
- Girarsi costa 30 cicli; il movimento laterale ha precedenza su quello verticale.
- I comandi a impulso vengono registrati sul fronte di pressione e consumati una volta.
- Il campo di forza blocca linea di vista e proiettili: il Santuario è sicuro.
- `Virgin` si azzera a ogni `Recreate` (cambio di piano).
- Lo schermo cambia a scatti di 28/21 celle, non scorre in modo continuo.
- Le magie dell'uomo consumano magia anche se non trovano un bersaglio.
- Il fulmine attraversa i bersagli e rimbalza: può ferire l'uomo stesso.
- Tornando su un piano i mostri ritrovano le statistiche iniziali e le porte si richiudono.
- Con la chiave le scale smettono di funzionare.

---

## 10. Front end

- `Screen1` (front:86) stampa `MessS1` (front:425) con codici di controllo propri:
  `#0C` cancella, `#0D` a capo, `#0A n` salta n colonne, `#10 x y` posiziona, `#11-#14`
  cursore, `#08` cancella carattere. Contenuto (riga: testo):
  0 "Halls of the Things"; 2 "MOVEMENT  N:Up" + col. 21 "M:Down"; 3 col. 10 "Z:Left",
  col. 21 "X:Right"; 4 col. 2 "Doors", col. 10 "O:Open", col. 21 "C:Close"; 6 "OBJECTS",
  "K:Keep", "D:Drop"; 8 "WEAPONS", "S:Sword"; 10 col. 2 "Arrows  A:Shoot"; 11 "5:Left",
  "8:Right"; 12 "7:Up", "6:Down"; 14 col. 2 "Spells  F:Fireball L:Lightning"; 15 col. 10
  "H:Heal"; 17 "VARIOUS", col. 10 "1:Display status"; 18 col. 9 "sp:Centre screen";
  20 "High Score : " + nome + "  " + punti; 23 "Press SPACE to continue" (poi
  "Please wait for a second" durante la generazione).
- Fine partita (`FrontScore`, front:529): "You have been killed" / "You are victorious !" /
  "You gave up , coward"; "You collected no/one/N/all seven ring(s)" ("all seven" per 7 o 8);
  "and the golden key." se 8; "You killed no/one/N monster(s)"; "and scored N points."
  (o "no"). Se si batte il record: "Well done ! you have the high score and have bettered
  <nome> by N points.", poi "Please enter your name" (max 12 caratteri, INVIO; nome vuoto o
  di soli spazi → "someone"). Record iniziale: 100, "Kickaha". Il record vive solo in memoria.
- Tastiera del front end (`FrontReadKey`, front:254): scansione della matrice con
  antirimbalzo, `CharTab`; CAPS SHIFT fa le maiuscole, CAPS+0 = cancella.
- `Decode`/`Splodge` e le stringhe goliardiche sono codice morto o protezione del caricatore.

---

## 11. Differenze della versione 2007 (`halls_*.asm`, "version 11 19/2/06")

È la stessa base del 1983 ripulita da Simon Brattel (non il "remix" del 1986, che esiste solo
come `hallsremix.z80.zip`). Differenze principali:
- sorgente commentato, nomi simbolici dei campi (`dm_*`, `dp_*`), costanti nominate
  (`ManSpeed 10`, `FireLife 50`, ecc.); le regole di gioco sono le stesse;
- `FixBug`: corretto il controllo del lanciatore in `HFireMon`/`HLightMon` (§4.3);
- spada: 9 fasi invece di 5, danno 10 invece di 15, **colpo automatico** se un mostro è
  davanti (`MSword_Auto`), ignora i gradini, viene gestita dopo il movimento;
- tolti i tasti 2 (trucco della porta) e W (bianco/nero);
- `JustifyT` sposta l'inquadratura di 8 o 16 colonne secondo la direzione dell'uomo;
- tabella dei mostri compattata, contatore iniziale 60 (primo movimento più rapido);
- i muri `#91-#9F` disegnati come contorni vuoti invece che pieni;
- front end: seme mescolato chiamando `Rand1` mentre si aspetta SPAZIO, niente
  rilocazione né stringhe di disturbo.

Le versioni 8086 (`halls86.zip`, 1991, mostri più elaborati e scritti da più autori), 68000
(`halls68k_*.txt`, mappa 512×401) e Amstrad PCW (`halls8256.zip`) sono progetti diversi e
più grandi; utili solo come curiosità.

---

## 12. Domande aperte

1. **Velocità reale del ciclo**: la stima di §9.1 (300-450 cicli/s) va calibrata
   sull'emulatore o sulla registrazione `.rzx`.
2. **Riga di partenza delle frecce orizzontali**: l'asimmetria di `MFireArrow` (§4.2) è un
   bug o voluta? Proposta: "riga del primo mostro trovato, altrimenti testa".
3. **Bug del lanciatore** (§4.3): riprodurlo (magie che colpiscono anche chi le lancia) o
   applicare la correzione del 2007?
4. **Punteggio della vittoria**: conta anche i tesori mai raccolti della sala del piano 0;
   probabilmente non voluto ma è il comportamento originale.
5. **Condizione di vittoria con `Virgin`**: interpretazione dedotta (§4.7); va verificata
   giocando.
6. **Statistiche dei mostri ripristinate** a ogni ritorno sul piano (§2.7): effetto
   collaterale della rigenerazione dal seme, probabilmente accettato dagli autori.
7. **Campi del mostro non usati** (6, 12, 21, 23, 24) e **fasci di frecce `#D8-#DB`** mai
   generati: residui della versione preliminare; da ignorare.
8. **Trucco del tasto 2 e modalità bianco/nero (W)**: tenerli? (La versione 2007 li toglie.)
9. **Freccia dei mostri e campo 22** del fulmine dei mostri non inizializzato: irrilevante
   in pratica.
10. Il font `#20-#7F` è quello del file dei caratteri (sembra derivato dalla ROM Spectrum);
    usarlo tal quale.
