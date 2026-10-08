# Changelog

## 1.7.3 – 2026-10-08 · first stable release

**EN**
> **⚠ Important – workbench repair changed:** in 1.7.2b the automatic repair also removed **normal waiting crafts** from workbench queues (sorry!). It is now **off by default** and only removes the **one waiting craft at the front** when nothing is running.
> **Is one of your workbenches still stuck** (F does nothing, it cannot be removed)? Mod menu (F9) → Interface & keys → turn on **"Repair stuck workbench"**, press F at that workbench (or remove it), then turn the setting off again. Nothing is lost – ingredients are only used when a round starts.

- **Stable and Beta:** the installer now lets you choose – **Stable** (tested, recommended, installed straight from the package) or **Beta** (newest pre-release from GitHub, to try new features early; may contain bugs). The channel can be changed later in the mod menu (F9) under "Update channel", and the update check follows it.
- **Installer:** new look with the Vanilla+ logo. It is now really in English on non-German Windows (it always showed German before) and recognises the installed version correctly (it always offered an "update" before).
- **Installer:** clearer uninstall – "Remove everything" (Vanilla+ and BepInEx, game back to original) or "Vanilla+ only"; read-only files no longer stop it, leftovers are listed, and a log is written to Documents\GK2 Vanilla+\uninstall.log.
- **Fix:** the pin icon is smaller and smoother (it looked big and pixelated at 4K).
- **Option:** pin with a **key or mouse button** instead of clicking the pin icon (mod menu → Pins → "Pin with"): point at a recipe, order or blueprint and press it – then clicks never pin by accident. Middle and side mouse buttons work too. Left click on the pin stays the default (Esc switches back).
- **Fix:** Max when crafting counts carefully: ingredients already reserved by other queued crafts and tool durability (e.g. saw) are taken into account, and it never adds an extra round – so no crafts get stuck "waiting" at the workbench any more.
- **Fix:** battle-safe backups – the last save before a battle is always backed up and never pushed out by autosaves made during the battle (those are marked "in battle" in the backup list).
- **Fix:** deleted backups no longer leave empty folders behind (Linux/Steam Deck/Mac).
- **Fix:** the buttons in the backup list were cut off – the list fits the window again.
- Moving factory machines is blocked for now – they lost their output conveyor. Normal conveyors can still be moved.

**DE**
> **⚠ Wichtig – Werkbank-Reparatur geändert:** In 1.7.2b hat die automatische Reparatur auch **normal wartende Aufträge** aus Warteschlangen entfernt (sorry!). Sie ist jetzt **standardmäßig aus** und nimmt nur noch **den einen wartenden Auftrag ganz vorne** heraus, wenn nichts läuft.
> **Hängt bei dir noch eine Werkbank** (F ohne Wirkung, nicht abbaubar)? Mod-Menü (F9) → Anzeige & Tasten → **„Hängende Werkbank reparieren“** einschalten, an der Werkbank F drücken (oder sie abbauen), danach die Einstellung wieder ausschalten. Es geht nichts verloren – Zutaten werden erst beim Start eines Durchgangs verbraucht.

- **Stabil und Beta:** Der Installer lässt dich jetzt wählen – **Stabil** (getestet, empfohlen, direkt aus dem Paket) oder **Beta** (neueste Vorabversion von GitHub, zum frühen Ausprobieren neuer Funktionen; kann Fehler enthalten). Den Kanal kannst du später im Mod-Menü (F9) unter „Update-Kanal“ ändern, die Update-Prüfung richtet sich danach.
- **Installer:** neues Aussehen mit dem Vanilla+-Logo. Auf nicht-deutschem Windows ist er jetzt wirklich englisch (vorher immer deutsch) und erkennt die installierte Version richtig (vorher bot er immer ein „Update“ an).
- **Installer:** klareres Deinstallieren – „Alles entfernen“ (Vanilla+ und BepInEx, Spiel wieder original) oder „Nur Vanilla+“; schreibgeschützte Dateien stoppen nichts mehr, Reste werden aufgelistet, ein Protokoll liegt unter Dokumente\GK2 Vanilla+\uninstall.log.
- **Fix:** Die Pin-Nadel ist kleiner und weicher (sie wirkte bei 4K groß und verpixelt).
- **Option:** Mit einer **Taste oder Maustaste** statt per Klick auf die Nadel anpinnen (Mod-Menü → Anpinnen → „Anpinnen mit“): Maus auf Rezept, Auftrag oder Bauplan und drücken – dann pinnt ein Klick nie mehr versehentlich. Mittlere und Seitentasten der Maus gehen auch. Standard bleibt der Linksklick auf die Nadel (Esc stellt zurück).
- **Fix:** Max beim Herstellen rechnet vorsichtig: Zutaten, die andere Aufträge in der Warteschlange schon brauchen, und die Haltbarkeit von Werkzeug (z. B. Säge) zählen mit, und es kommt kein Extra-Durchgang mehr dazu – so bleibt nichts mehr als „wartet“ an der Werkbank hängen.
- **Fix:** Kampf-sichere Backups – der letzte Stand vor einem Kampf wird immer gesichert und nie von Autosaves aus dem Kampf verdrängt (diese sind in der Backup-Liste mit „im Kampf“ markiert).
- **Fix:** Gelöschte Backups hinterlassen keine leeren Ordner mehr (Linux/Steam Deck/Mac).
- **Fix:** Die Knöpfe in der Backup-Liste waren abgeschnitten – die Liste passt wieder ins Fenster.
- Fabrik-Maschinen lassen sich vorerst nicht verschieben – sie verloren ihr Ausgabe-Förderband. Normale Förderbänder lassen sich weiter verschieben.

## 1.7.2 – 2026-10-07 · Hotfix 1.7.2b

**EN**
- Quick hotfix (1.7.2b): released fast to help everyone with stuck workbenches, not fully tested yet – please report anything odd.
- **Fix:** workbenches could stop reacting (F did nothing, couldn't be removed) after crafting with the Max button. Cause: a craft queued with more rounds than ingredients stays "waiting" at the workbench – the game allows this with "+" too, Max made it easy. Affected workbenches now **repair themselves** as soon as you use or remove them – no rebuilding needed, nothing is lost (ingredients are only used when a round starts).
- **Fix:** Max now counts carefully: ingredients already reserved by other queued crafts and tool durability (e.g. saw) are taken into account, and it never adds an extra round.
- Moving factory machines is blocked for now – they lost their output conveyor. Normal conveyors can still be moved.

**DE**
- Schneller Hotfix (1.7.2b): rasch veröffentlicht, damit hängende Werkbänke wieder gehen, noch nicht vollständig getestet – meldet bitte alles Auffällige.
- **Fix:** Werkbänke konnten nach dem Herstellen mit dem Max-Knopf nicht mehr reagieren (F ohne Wirkung, Abbauen ging nicht). Ursache: Ein Auftrag mit mehr Durchgängen als Zutaten bleibt an der Werkbank als „wartet“ stehen – das geht im Spiel auch mit „+“, mit Max aber leicht. Betroffene Werkbänke **reparieren sich jetzt selbst**, sobald du sie benutzt oder abbaust – kein Neubau nötig, nichts geht verloren (Zutaten werden erst beim Start eines Durchgangs verbraucht).
- **Fix:** Max rechnet jetzt vorsichtig: Zutaten, die andere Aufträge in der Warteschlange schon brauchen, und die Haltbarkeit von Werkzeug (z. B. Säge) zählen mit, und es kommt kein Extra-Durchgang mehr dazu.
- Fabrik-Maschinen lassen sich vorerst nicht verschieben – sie verloren ihr Ausgabe-Förderband. Normale Förderbänder lassen sich weiter verschieben.

## 1.7.1 – 2026-10-06

## 1.7.1 – 2026-10-06

**EN**
- **Fix:** crafting windows could get stuck (could not be closed, nothing could be crafted) with the new Max button. The Max button no longer touches the game's own button bar; on a controller a small "[Y] Max" hint is shown next to the amount instead.
- New: **Support** button at the bottom of the mod menu – opens the Ko-fi page. The mod stays free, nothing depends on it.

**DE**
- **Fix:** Herstellen-Fenster konnten mit dem neuen Max-Knopf hängen bleiben (ließen sich nicht schließen, nichts herstellen). Der Max-Knopf greift nicht mehr in die Tastenleiste des Spiels ein; am Controller steht stattdessen ein kleiner Hinweis „[Y] Max“ neben der Menge.
- Neu: Knopf **„Unterstützen“** unten im Mod-Menü – öffnet die Ko-fi-Seite. Der Mod bleibt kostenlos, nichts hängt daran.

## 1.7.0 – 2026-10-05

**EN**
- New: **Main menu backgrounds** – instead of the game's scene: six Vanilla+ pictures (graveyard in the rain, flooded quarter, town at night, your yard, the village, vineyard by the pond) or **your own picture from your save** – in game, mod menu → "Use current view as menu background" (taken without HUD). Filters on top: style (natural, gloomy, sepia, night, painting), blur, darken and slowly drifting **fog** (dark or light). Pictures are in up to 4K and 32:9.
- New: **Pin town orders** – a pin button on every order, in the order window and in the order list at the warehouse. The pin shows the item and how many are still missing; it disappears by itself once the order is done.
- New: **Pin alchemy recipes** – the Folio in the alchemy lab now has pin buttons too. Alchemy has no fixed ingredients, so the pin shows the runes you need (red / green / blue).
- New: **Max button when crafting** – next to the amount, sets it to as many as your ingredients allow (counted like the game: inventory and reachable chests). Controller: "Max" in the button bar at the bottom of the window.
- New, **not fully vanilla** (off by default): **Refund talents & research** – right click an unlocked talent or zombie perk (controller: the button shown below it), or click a researched tech and choose "Refund". Always asks first, nodes that depend on it are refunded too. Idea from "Talent & Tech Refund" – own implementation; if that mod is installed, Vanilla+ leaves it to it.
- New: **Rain amount** (100 / 75 / 50 / 25 % / off) – less rain helps on weak PCs and the Steam Deck.
- New: **Keep backups** – "Keep" on a save backup protects it from automatic deletion (kept backups don't count towards "Backups per save").
- Fix: **moving a workbench with extensions** – after moving, the extensions are connected again (before, the workbench could lose them until the next reload).
- Fix: **controller lost after Alt-Tab / Windows key** – "Pause in background" no longer stops the whole application, the game just stands still, so the controller stays connected.
- Fix: mod menu opened with the controller – the **mouse** works again as soon as you move it.
- Nexus download is now a plain zip with the files only (no installer scripts), so it isn't flagged anymore.

**DE**
- Neu: **Hauptmenü-Hintergründe** – statt der Szene des Spiels: sechs Vanilla+-Motive (Friedhof im Regen, überflutetes Viertel, Stadt bei Nacht, dein Hof, das Dorf, Weinberg am Teich) oder **dein eigenes Bild aus deinem Spielstand** – im Spiel Mod-Menü → „Aktuelle Ansicht als Menü-Hintergrund“ (ohne HUD aufgenommen). Darüber Filter: Stil (natürlich, düster, Sepia, Nacht, Gemälde), Weichzeichnen, Abdunkeln und langsam ziehender **Nebel** (düster oder hell). Bilder in bis zu 4K und 32:9.
- Neu: **Stadt-Aufträge anpinnen** – an jedem Auftrag gibt es einen Pin-Knopf, im Auftragsfenster und in der Auftragsliste im Lagerhaus. Der Pin zeigt die Ware und wie viel noch fehlt und verschwindet von selbst, sobald der Auftrag erledigt ist.
- Neu: **Alchemie-Rezepte anpinnen** – auch das Folio im Alchemielabor hat jetzt Pin-Knöpfe. Alchemie hat keine festen Zutaten, deshalb zeigt der Pin die benötigten Runen (rot / grün / blau).
- Neu: **Max-Knopf beim Herstellen** – neben der Menge, stellt so viele ein, wie deine Zutaten hergeben (gezählt wie im Spiel: Inventar und erreichbare Truhen). Controller: „Max“ in der Tastenleiste unten im Fenster.
- Neu, **nicht ganz Vanilla** (standardmäßig aus): **Talente & Forschung zurückerstatten** – Rechtsklick auf ein freigeschaltetes Talent oder einen Zombie-Perk (Controller: die Taste, die darunter steht), oder erforschte Technik anklicken → „Zurückerstatten“. Fragt immer vorher, was davon abhängt, wird mit erstattet. Idee von „Talent & Tech Refund“ – eigene Umsetzung; ist diese Mod installiert, überlässt Vanilla+ ihr das.
- Neu: **Regen: Menge** (100 / 75 / 50 / 25 % / aus) – weniger Regen hilft auf schwachen PCs und dem Steam Deck.
- Neu: **Backups behalten** – „Behalten“ an einem Backup schützt es vor dem automatischen Löschen (behaltene zählen nicht zu „Backups pro Spielstand“).
- Fix: **Werkbank mit Erweiterungen verschieben** – nach dem Verschieben sind die Erweiterungen wieder verbunden (vorher konnte die Werkbank sie bis zum nächsten Laden verlieren).
- Fix: **Controller weg nach Alt-Tab / Windows-Taste** – „Im Hintergrund pausieren“ hält nicht mehr die ganze Anwendung an, nur das Spiel steht still, der Controller bleibt verbunden.
- Fix: Mod-Menü per Controller geöffnet – die **Maus** funktioniert wieder, sobald du sie bewegst.
- Nexus-Download ist jetzt ein reines Zip nur mit den Dateien (keine Installer-Skripte) und wird nicht mehr markiert.

## 1.6.2 – 2026-10-02

**EN**
- **Controller fix:** holding RT inside game windows (e.g. switching tabs) no longer takes over the pinned list – RT only controls pins during normal play. The button can be freely assigned or turned off: Pins → "Controller: button for pins", click and press any controller button.
- Pins: recipes without their own name now show the item name instead of an internal ID.
- The menu button is now called **"Vanilla+"** instead of "Mods", so it's clear next to the "Mods" button of other mods (e.g. GK2 Mod Framework).

**DE**
- **Controller-Fix:** RT in Spiel-Fenstern (z. B. Reiter wechseln) übernimmt nicht mehr die Pin-Liste – RT steuert Pins nur im normalen Spiel. Die Taste ist frei belegbar oder abschaltbar: Anpinnen → „Controller: Taste für Pins“ anklicken und beliebige Controller-Taste drücken.
- Pins: Rezepte ohne eigenen Namen zeigen jetzt den Gegenstand statt einer internen ID.
- Der Menü-Button heißt jetzt **„Vanilla+“** statt „Mods“ – so ist er neben dem „Mods“-Button anderer Mods (z. B. GK2 Mod Framework) eindeutig.

## 1.6.1 – 2026-10-02

**EN**
- **Hotfix:** the game could close on startup when **GK2 Mod Framework** was installed as well. Vanilla+ no longer hooks into the framework's Mods menu and shows its own "Mods" button again, like before 1.6.0. Both mods work side by side.

**DE**
- **Hotfix:** Das Spiel konnte sich beim Start schließen, wenn zusätzlich **GK2 Mod Framework** installiert war. Vanilla+ klinkt sich nicht mehr in dessen Mods-Menü ein und zeigt wieder seinen eigenen „Mods“-Button wie vor 1.6.0. Beide Mods laufen nebeneinander.

## 1.6.0 – 2026-10-02

**EN**
- **1,000+ players – thank you!** A small thank-you in the main menu (once), then just a little note next to "modded".
- New: **Rename zombies** – a Rename button next to the name in the zombie window: type your own name or roll a new one, any time.
- New: **Trade the right amount** – with town vendors the amount slider starts at exactly the amount that still gives happiness (thumbs up), and "Add liked goods" puts all of them in at once in the right amount. You still confirm the deal yourself. (Idea from the Steam discussions.)
- New, **not fully vanilla** (often requested, off by default): **Full refund** when removing buildings, and **Move objects** – in remove mode press the rotate key on an object and place it somewhere else. It stays the same object, contents and crafting queue are kept – works for workbenches, conveyors and everything else; zombie workers are put on the ground, extensions stay until you move them too.
- New: **Day & time on the HUD** as a second line in the area name box at the top right – where and when in one place (day + time, weekday + time or only the time; 12/24 h).
- New: **Leave conversations with Esc / B**, **faster transitions** through doors and on the map, **Pinned list 2.0** (workbench, recipe variants, ingredient tree, fuel, collapsible) and pins can count **only your inventory**.
- New: **Portuguese** (pt-BR). The translation template is now always in BepInEx/GK2VanillaPlus/lang/translation-template.txt (it was missing in the Workshop version).
- Steam Deck / Linux: **no more tearing** with an FPS limit (VSync stays on, setting "No tearing" in Graphics & speed).
- **Controller:** hold **RT** to navigate the pinned list (A expand, ←→ amount, LB/RB variant, Y unpin); at vendors **LT** adds the liked goods; in the zombie window **Y** rolls a new name; pins can also be managed in the mod menu.
- Pins: craft a recipe **2–10 times** (×1…×10 button, all amounts scale), pinned list redesigned and hidden while the character/inventory window is open.
- **Mod menu reorganized** into tabs, plus **All off (vanilla)** – turn everything off and switch on only the 1–2 things you want. "Default" brings back the recommended setup.
- Ideas from the Nexus mods "What time is it", "ESC to Leave" (OrionAF) and "Instant Transitions" (LeBetoven) – own implementation. If one of them is installed, Vanilla+ leaves that part to it.

**DE**
- **1.000+ Spieler – danke!** Ein kleines Dankeschön im Hauptmenü (einmalig), danach nur noch ein kleiner Hinweis neben „modded“.
- Neu: **Zombies umbenennen** – ein „Umbenennen“-Knopf neben dem Namen im Zombie-Fenster: eigenen Namen eintippen oder neu würfeln, jederzeit.
- Neu: **Handel mit der passenden Menge** – bei Stadt-Händlern startet der Mengen-Regler genau bei der Menge, die noch Zufriedenheit (Daumen hoch) bringt, und „Daumen-hoch-Waren einlegen“ legt alle auf einmal in der richtigen Menge hinein. Bestätigen musst du weiterhin selbst. (Idee aus den Steam-Diskussionen.)
- Neu, **nicht mehr ganz Vanilla** (oft gewünscht, standardmäßig aus): **Volle Erstattung** beim Abbauen und **Objekte verschieben** – im Abriss-Modus mit der Dreh-Taste ein Objekt aufnehmen und woanders hinstellen. Es bleibt dasselbe Objekt, Inhalt und Herstell-Warteschlange bleiben erhalten – geht für Werkbänke, Förderbänder und alles andere; Zombie-Arbeiter landen auf dem Boden, Erweiterungen bleiben stehen, bis du sie auch verschiebst.
- Neu: **Tag & Uhrzeit am HUD** als zweite Zeile in der Gebietsanzeige oben rechts – Wo und Wann an einer Stelle (Tag + Uhrzeit, Wochentag + Uhrzeit oder nur Uhrzeit; 12/24 h).
- Neu: **Gespräch mit Esc / B verlassen**, **schnellere Übergänge** durch Türen und auf der Karte, **Pin-Liste 2.0** (Werkbank, Rezept-Varianten, Zutatenbaum, Brennstoff, einklappbar) und Pins können **nur das Inventar** zählen.
- Neu: **Portugiesisch** (pt-BR). Die Übersetzungsvorlage liegt jetzt immer in BepInEx/GK2VanillaPlus/lang/translation-template.txt (fehlte in der Workshop-Version).
- Steam Deck / Linux: **kein Tearing mehr** mit FPS-Limit (VSync bleibt an, Einstellung „Kein Tearing“ unter Grafik & Leistung).
- **Controller:** **RT** gedrückt halten steuert die Pin-Liste (A auf/zu, ←→ Menge, LB/RB Variante, Y lösen); beim Händler legt **LT** die Daumen-hoch-Waren ein; im Zombie-Fenster würfelt **Y** einen neuen Namen; Pins lassen sich auch im Mod-Menü verwalten.
- Pins: Rezept **2–10-mal** herstellen (Knopf ×1…×10, alle Mengen rechnen mit), Pin-Liste neu gestaltet und ausgeblendet, solange das Charakter-/Inventar-Fenster offen ist.
- **Mod-Menü neu sortiert** in Reiter, dazu **Alles aus (Vanilla)** – alles abschalten und nur die 1–2 Dinge einschalten, die du willst. „Standard“ holt die Empfehlung zurück.
- Ideen aus den Nexus-Mods „What time is it“, „ESC to Leave“ (OrionAF) und „Instant Transitions“ (LeBetoven) – eigene Umsetzung. Ist einer davon installiert, überlässt Vanilla+ ihm diesen Teil.

## 1.5.3 – 2026-09-28

**EN**
- New: **Instant removal** (mod menu → Comfort, off by default) – in the building remove mode, workbenches, chests, furnaces etc. are removed right away instead of your character walking there first. You get the same materials back. Fixes objects placed next to the ruins that your character could never reach. Thanks for the suggestion!

**DE**
- Neu: **Sofort abbauen** (Mod-Menü → Komfort, standardmäßig aus) – im Abriss-Modus beim Bauen werden Werkbänke, Truhen, Öfen usw. sofort entfernt, statt dass deine Figur erst hinläuft. Du bekommst dieselben Materialien zurück. Hilft bei Objekten neben den Ruinen, die deine Figur nie erreicht. Danke für den Vorschlag!

## 1.5.2 – 2026-09-27 <!-- silent -->

**EN**
- Small fixes: more robust after game updates, controller fixes in the mod menu (removing pins, restoring backups, B cancels a key assignment), Esc always closes the topmost mod window, quickly toggling the FPS display no longer starts extra background threads, auto-unpin after crafting picks the right recipe
- Uninstall now keeps the mod's save backups and screenshots (moved to Documents\GK2 Vanilla+)
- Known issue: switching between controller and mouse/keyboard while a menu is open can show an error message – just close it, everything keeps working normally

**DE**
- Kleine Korrekturen: robuster nach Spiel-Updates, Controller-Fixes im Mod-Menü (Pins entfernen, Backups wiederherstellen, B bricht eine Tastenbelegung ab), Esc schließt immer das oberste Mod-Fenster, schnelles An/Aus der FPS-Anzeige startet keine zusätzlichen Hintergrund-Threads mehr, automatisches Lösen nach dem Herstellen erwischt das richtige Rezept
- Deinstallieren behält jetzt die Spielstand-Backups und Screenshots des Mods (werden nach Dokumente\GK2 Vanilla+ verschoben)
- Bekannter Fehler: Wechselt man bei offenem Menü zwischen Controller und Maus/Tastatur, kann eine Fehlermeldung erscheinen – einfach wegklicken, danach läuft alles normal weiter

## 1.5.1 – 2026-09-27

**EN**
- 🎉 **200 subscribers on the Steam Workshop!** Genuinely did not expect that this fast – thank you all so much for trying the mod out, for the kind comments, and for the bug reports that help make it better. Means a lot to me.
- New: the main menu now also shows the mod's version number next to "modded"
- New: a small one-time popup asks (after a few play sessions) if you'd like to leave a rating on the Steam Workshop – with "Rate it now", "Remind me later" and "Don't ask again"
- Fix: a rare error window that could appear over the mod menu (controller navigation / opening "What's new")

**DE**
- 🎉 **200 Abonnenten im Steam Workshop!** Damit hatte ich ehrlich gesagt nicht so schnell gerechnet – vielen Dank euch allen fürs Ausprobieren, die netten Kommentare und die Bug-Reports, die den Mod besser machen. Das bedeutet mir viel.
- Neu: Im Hauptmenü steht jetzt auch die Versionsnummer des Mods neben "modded"
- Neu: Ein kleines, einmaliges Fenster fragt (nach ein paar Spielsitzungen) nach einer Bewertung im Steam Workshop – mit „Jetzt bewerten“, „Später erinnern“ und „Nicht mehr fragen“
- Fix: Ein seltenes Fehlerfenster, das über dem Mod-Menü erscheinen konnte (Controller-Navigation / "Was ist neu?" öffnen)

## 1.5.0 – 2026-09-27

**EN**
- New: **safe mode** – after a game update the mod checks everything it relies on. If something changed, only the affected feature is turned off (with a note in the mod menu) instead of causing errors – the rest keeps working. Features that keep throwing errors are turned off automatically too.
- Fix: the pinned list no longer covers the reputation window of the main NPCs (it moves below it)
- New: FPS display – **order of the values** can be changed with arrows in the mod menu, optional **separator** between the values (long dash, bar or dot)
- New: **controller support for the mod menu** – D-pad / left stick to select and change, A to confirm, B or Start to close, LB/RB to reorder the FPS display. While a mod window is open, the game ignores the controller.
- New: **one-click profiles** in the mod menu – "Steam Deck / battery", "Performance", "Quality" and "Game default"
- New: **HUD to the center** (ultrawide, optional) – HUD, area name, NPC window and the mod displays move into the 16:9 area in the middle
- New: pins – optional counting of **all chests on the map**, a **short message with sound** when a pinned item is ready, and **automatic unpinning** when you start crafting it
- New: FPS display can show the **GPU temperature** (NVIDIA graphics cards)
- Fix: the FPS display no longer covers the reputation window of the main NPCs either
- Mod menu: longer explanations at the bottom are no longer cut off; the Mac/Linux controller fix is explained in detail

**DE**
- Neu: **Sicherer Modus** – nach einem Spiel-Update prüft der Mod alles, worauf er zugreift. Hat sich etwas geändert, wird nur die betroffene Funktion abgeschaltet (mit Hinweis im Mod-Menü), statt Fehler zu verursachen – der Rest läuft weiter. Funktionen, die wiederholt Fehler werfen, werden ebenfalls automatisch abgeschaltet.
- Fix: Die Pin-Liste verdeckt nicht mehr das Ansehen-Fenster der Haupt-NPCs (sie rückt darunter)
- Neu: FPS-Anzeige – **Reihenfolge der Werte** im Mod-Menü per Pfeil einstellbar, optional **Trennzeichen** zwischen den Werten (langer Strich, senkrechter Strich oder Punkt)
- Neu: **Controller-Bedienung des Mod-Menüs** – Steuerkreuz / linker Stick zum Auswählen und Ändern, A bestätigt, B oder Start schließt, LB/RB verschiebt die Werte der FPS-Anzeige. Solange ein Mod-Fenster offen ist, ignoriert das Spiel den Controller.
- Neu: **Ein-Klick-Profile** im Mod-Menü – „Steam Deck / Akku“, „Leistung“, „Qualität“ und „Spiel-Standard“
- Neu: **HUD zur Mitte** (Ultrawide, optional) – HUD, Gebietsname, NPC-Fenster und die Mod-Anzeigen rücken in den 16:9-Bereich in der Mitte
- Neu: Pins – optional **alle Truhen auf der Karte** mitzählen, **kurze Meldung mit Ton**, wenn ein Pin fertig ist, und **automatisch lösen**, sobald du das Rezept herstellst
- Neu: FPS-Anzeige kann die **GPU-Temperatur** zeigen (NVIDIA-Grafikkarten)
- Fix: Auch die FPS-Anzeige verdeckt das Ansehen-Fenster der Haupt-NPCs nicht mehr
- Mod-Menü: lange Erklärungen unten werden nicht mehr abgeschnitten; der Controller-Fix für Mac/Linux ist ausführlich erklärt

## 1.4.3 – 2026-09-27

**EN**
- New: **full-width rain** – rain and snow now cover the whole screen on ultrawide monitors and when zoomed out (the game only fills a 16:9 area). Inspired by "GK2 Ultrawide Rain Fix" by Dry Bones (own implementation)
- OLED black is a bit darker (near-black areas are now fully black)
- Fix: quests can now be pinned with the controller (both sticks)
- Fix: blockages like the tunnel breakthrough can now be pinned

**DE**
- Neu: **Regen über die ganze Breite** – Regen und Schnee füllen jetzt auf Ultrawide-Monitoren und beim Herauszoomen den ganzen Bildschirm (das Spiel füllt nur einen 16:9-Bereich). Angeregt durch „GK2 Ultrawide Rain Fix“ von Dry Bones (eigene Umsetzung)
- OLED-Schwarz ist etwas dunkler (fast schwarze Bereiche sind jetzt ganz schwarz)
- Fix: Quests lassen sich jetzt auch mit dem Controller anpinnen (beide Sticks)
- Fix: Hindernisse wie der Durchbruch im Tunnel lassen sich jetzt anpinnen

## 1.4.2 – 2026-09-26

**EN**
- Fix: pins in the build menu (e.g. blueprints in the yard like the workbench) and for town buildings can now be clicked with the mouse
- New: **OLED black** (Graphics) – pure black instead of dark gray around the map, e.g. outside the church or at the level edge

**DE**
- Fix: Pins im Baumenü (z. B. Baupläne im Hof wie die Werkbank) und bei Stadtgebäuden lassen sich jetzt auch mit der Maus anklicken
- Neu: **OLED-Schwarz** (Grafik) – reines Schwarz statt Dunkelgrau um die Karte herum, z. B. außerhalb der Kirche oder am Levelrand

## 1.4.1 – 2026-09-26

**EN**
- Fix: the build menu (builder desk) threw an error and did not open correctly. Pins in the build menu and for town buildings now work without patching those windows.

**DE**
- Fix: Das Baumenü (Bauplan-Tisch) warf einen Fehler und öffnete nicht richtig. Pins im Baumenü und bei Stadtgebäuden funktionieren jetzt, ohne diese Fenster zu patchen.

## 1.4.0 – 2026-09-26

**EN**
- New: **pin recipes** – a small pin in the top right corner of recipes (workbenches), blueprints (build menu) and town buildings. Pinned items are listed at the screen edge with have/need per ingredient (your inventory, not chests), green when you have enough. Corner and size selectable. With a controller: press both sticks (L3 + R3) on a recipe.
- New: pin **quests** and **single crafts** (e.g. clearing blockages) – pin in the header of the recipe or quest window; quest pins show the current task
- New: pins are **kept per save**
- New: **save overview** in the mod menu in the main menu – day, date, graveyard/church quality, "Play" button and the backups of each save
- New: **camera** – zoom presets on F5, separate zoom indoors, smooth zoom with the mouse wheel, smooth transitions
- New: **screenshot key** (F12) in up to 4× resolution, optionally without HUD
- New: **readability** – size of the mod displays (80–200 %), high contrast mode, larger tooltips
- New: **languages** – French, Spanish, Russian and Chinese; more languages via a simple text file (BepInEx/GK2VanillaPlus/lang/_template.txt)
- New: **"Mods" button** in the main menu and the pause menu (Esc) – opens the mod menu, closing it returns to the game menu
- New: **graphics benchmark** in the mod menu – runs through all graphics tiers (plus your own settings), measures FPS without a limit and shows a score and a recommendation. Nothing is changed, results are also saved to BepInEx/GK2VanillaPlus/benchmark.txt
- New: **What's new** window – shown once after an update, and anytime via the button in the mod menu
- New: FPS display can show the **in-game weekday** and the **in-game time**
- Fix: the camera zoom no longer affects the main menu (always 100 % there)
- Fix: clicks in mod windows no longer reach game buttons underneath
- Pinning inspired by "Recipe Pin" by farfars (own implementation)

**DE**
- Neu: **Rezepte anpinnen** – eine kleine Pinnadel oben rechts an Rezepten (Werkbänke), Bauplänen (Baumenü) und Stadtgebäuden. Angepinntes steht als Liste am Bildschirmrand mit Haben/Brauchen je Zutat (dein Inventar, ohne Truhen), grün sobald genug da ist. Ecke und Größe einstellbar. Mit Controller: auf dem Rezept beide Sticks drücken (L3 + R3).
- Neu: **Quests** und **Einzel-Handwerk** (z. B. Hindernisse räumen) anpinnen – Pinnadel in der Überschrift des Rezept- oder Quest-Fensters; Quest-Pins zeigen die aktuelle Aufgabe
- Neu: Pins werden **pro Spielstand gespeichert**
- Neu: **Spielstand-Übersicht** im Mod-Menü im Hauptmenü – Tag, Datum, Friedhof-/Kirchen-Qualität, Button „Spielen“ und die Backups je Spielstand
- Neu: **Kamera** – Zoom-Stufen auf F5, eigener Zoom in Innenräumen, stufenloser Zoom mit dem Mausrad, weiche Übergänge
- Neu: **Screenshot-Taste** (F12) in bis zu 4-facher Auflösung, wahlweise ohne HUD
- Neu: **Lesbarkeit** – Größe der Mod-Anzeigen (80–200 %), Modus „Hoher Kontrast“, größere Tooltips
- Neu: **Sprachen** – Französisch, Spanisch, Russisch und Chinesisch; weitere Sprachen über eine einfache Textdatei (BepInEx/GK2VanillaPlus/lang/_template.txt)
- Neu: **Button „Mods“** im Hauptmenü und im Pausenmenü (Esc) – öffnet das Mod-Menü, beim Schließen geht es zurück ins Spielmenü
- Neu: **Grafik-Benchmark** im Mod-Menü – geht alle Grafikstufen durch (plus deine eigenen Einstellungen), misst die FPS ohne Limit und zeigt Score und Empfehlung. Es wird nichts verändert, das Ergebnis steht zusätzlich in BepInEx/GK2VanillaPlus/benchmark.txt
- Neu: Fenster **„Was ist neu?“** – erscheint einmal nach einem Update und jederzeit über den Button im Mod-Menü
- Neu: FPS-Anzeige kann den **Wochentag** und die **Uhrzeit im Spiel** zeigen
- Fix: Der Kamera-Zoom wirkt nicht mehr im Hauptmenü (dort immer 100 %)
- Fix: Klicks in Mod-Fenstern lösen keine Spiel-Buttons darunter mehr aus
- Anpinnen angeregt durch „Recipe Pin“ von farfars (eigene Umsetzung)

## 1.3.0 – 2026-09-26

**EN**
- New: **week plan** (F6, key configurable) – what is possible on which weekday, today and tomorrow highlighted, with the game's day icons. Spoiler-free: only features you have already unlocked.
- New: **daily reminder** – each morning a game notification shows what is possible today (shown three times as long as normal notifications) (can be turned off)
- New: **save backups** – before the game overwrites a save, the previous one is backed up (default: keep 5 per slot, at most every 10 min). Restore from the mod menu in the main menu. Stored in the game folder, not in Steam Cloud.
- New: **hide HUD** (F7) for clean screenshots, Esc shows it again
- Idea for the daily reminder inspired by "Daily Reminder" by MrsKiraSayers (own implementation)

**DE**
- Neu: **Wochenplan** (F6, Taste frei wählbar) – was an welchem Wochentag möglich ist, heute und morgen hervorgehoben, mit den Tagessymbolen des Spiels. Spoilerfrei: nur bereits Freigeschaltetes.
- Neu: **Tagesübersicht am Morgen** – jeden Morgen eine Spiel-Benachrichtigung, was heute geht (dreimal so lange sichtbar wie normale Meldungen) (abschaltbar)
- Neu: **Spielstand-Backups** – bevor das Spiel einen Stand überschreibt, wird der alte gesichert (Standard: 5 pro Spielstand, höchstens alle 10 min). Wiederherstellen im Mod-Menü im Hauptmenü. Liegen im Spielordner, nicht in der Steam Cloud.
- Neu: **HUD ausblenden** (F7) für saubere Screenshots, Esc blendet es wieder ein
- Idee zur Tagesübersicht angeregt durch „Daily Reminder“ von MrsKiraSayers (eigene Umsetzung)

## 1.2.0 – 2026-09-25 – GK2 Vanilla+

**EN**
- New name: **GK2 Vanilla+** (Ultrawide + Tweaks), first public release on GitHub
- Update check: the mod menu shows new versions; "Save & update" installs them in one click (Windows)
- Installer: "Check online for updates" button
- New license: free redistribution in mod packs, no modifications (see LICENSE.md)

**DE**
- Neuer Name: **GK2 Vanilla+** (Ultrawide + Tweaks), erste öffentliche Version auf GitHub
- Update-Prüfung: Das Mod-Menü zeigt neue Versionen an, „Speichern & aktualisieren“ installiert sie mit einem Klick (Windows)
- Installer: Button „Online nach Updates suchen“
- Neue Lizenz: Weitergabe in Modpacks erlaubt, keine Veränderungen (siehe LICENSE.md)

## 1.1.0 – 2026-09-25 (Ultrawide + Tweaks bundle, private test)

**EN**
- GK2 Ultrawide and GK2 Tweaks now ship as one package with a Windows installer (Installieren.bat)
- New: GK2 Tweaks – in-game mod menu (F9) in the game's own look, FPS display (F10)
- Individual graphics overrides (shadows, HBAO, lights, water, clouds, render mode), frame rate cap/VSync, physics rate
- Comfort: camera zoom, extra autosave, pause in background, skip logos, widened main menu on ultrawide, "modded" note
- Mod menu in German and English (follows the game language)
- Mac/Linux: optional controller fix against stutter with PlayStation controllers
- Manual save: "Save now" button in the mod menu, optional save key
- FPS display: choose the corner and what to show (FPS, 1% low, frame time, CPU, GPU, RAM, VRAM, resolution, clock)

**DE**
- GK2 Ultrawide und GK2 Tweaks kommen jetzt als ein Paket mit Windows-Installer (Installieren.bat)
- Neu: GK2 Tweaks – Mod-Menü im Spiel (F9) in der Optik des Spiels, FPS-Anzeige (F10)
- Einzelne Grafik-Einstellungen (Schatten, HBAO, Lichter, Wasser, Wolken, Render-Modus), Bildraten-Limit/VSync, Physik-Takt
- Komfort: Kamera-Zoom, zusätzlicher Autosave, Pause im Hintergrund, Logos überspringen, verbreitertes Hauptmenü auf Ultrawide, Hinweis „modded“
- Mod-Menü auf Deutsch und Englisch (folgt der Spielsprache)
- Mac/Linux: optionaler Controller-Fix gegen Ruckler mit PlayStation-Controllern
- Manuelles Speichern: Button „Jetzt speichern“ im Mod-Menü, optional eigene Taste
- FPS-Anzeige: Ecke und Inhalt wählbar (FPS, 1%-Low, Frametime, CPU, GPU, RAM, VRAM, Auflösung, Uhrzeit)

## 1.0.0 – 2026-09-23

**EN**
- Initial release
- Unlocks resolutions with an aspect ratio above 2:1 and widths above 5120 px
- `CustomResolution` config option for extra resolutions
- `MainMenuScaleX2` config option (Auto/On/Off)
- Keeps the game's built-in fallback resolution list if the monitor reports no resolutions (e.g. under Wine)

**DE**
- Erste Veröffentlichung
- Schaltet Auflösungen mit Seitenverhältnis über 2:1 und über 5120 px Breite frei
- Config-Option `CustomResolution` für zusätzliche Auflösungen
- Config-Option `MainMenuScaleX2` (Auto/On/Off)
- Behält die eingebaute Ersatzliste des Spiels, falls der Monitor keine Auflösungen meldet (z. B. unter Wine)
