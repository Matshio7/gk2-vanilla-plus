using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Objekte verschieben (nicht mehr ganz Vanilla, oft gewuenscht): Im Abriss-Modus mit der Dreh-Taste ein Objekt
    // aufnehmen, dann mit dem normalen Bau-Zeiger des Spiels neu platzieren (Gitter, Drehen, Gueltigkeit macht das Spiel).
    // Es bleibt DASSELBE Objekt (gleiche WgoData): Inventar, Herstell-Warteschlange, Brennstoff usw. bleiben erhalten,
    // es wird kein Material verbraucht oder erstattet. Esc / Rechtsklick bricht ab, das Objekt bleibt dann wo es war.
    internal static class BuildMove
    {
        internal static bool Active => orig != null;
        private static Wgo orig;
        private static bool switching, backToRemove;
        private static Wgo lastMoved;

        private static void Untint(Wgo w) { try { if (w != null) w.SetSelectionTint(Color.white, 0f); } catch { } }

        // Spiel-Update 1.011: Das Spiel kann platzierte Objekte jetzt selbst verschieben. Wird das erkannt, bleibt unsere
        // Funktion still (keine Eingabe, kein Umbenennen, keine Hinweise) - Option und Code bleiben fuer aeltere
        // Spielversionen erhalten. Erkennung einmalig per Reflection, ohne harte Verweise auf neue Spiel-Typen.
        private static int native = -1;   // -1 = noch nicht geprueft, 0 = nein, 1 = ja
        private static bool nativeToastShown;
        internal static bool GameHasNativeMove
        {
            get
            {
                if (native < 0)
                {
                    string hit = null;
                    bool failed = false;
                    try { hit = DetectNativeMove(); }
                    catch (Exception e)
                    {
                        // Fehlt ein hier verwendeter Spiel-Typ, ist der Bau-Modus umgebaut - dann ist unser Verschieben ohnehin
                        // nicht sicher: lieber aus (ohne den Hinweis "das Spiel kann es selbst", der waere geraten).
                        failed = true;
                        Plugin.Log.LogWarning("Move: native move check failed (" + e.GetType().Name + ": " + e.Message + ") - Vanilla+ MoveObjects stays inactive to be safe");
                    }
                    // 1.7.6: Ab Spielversion 1.011 verschiebt das Spiel selbst - fest aus, auch wenn die Namens-Erkennung
                    // nichts findet (umbenannte Spiel-Methoden duerfen unser Verschieben nicht neben dem des Spiels einschalten).
                    if (!failed && hit == null && Plugin.GameVersionAtLeast(1.011))
                    {
                        native = 1;
                        Plugin.Log.LogInfo("Move: game >= 1.011 has native move (not found by name) - Vanilla+ MoveObjects stays inactive");
                        return true;
                    }
                    native = failed || hit != null ? 1 : 0;
                    if (failed) nativeToastShown = true;
                    else if (hit != null) Plugin.Log.LogInfo("Move: the game has its own 'move objects' (" + hit + ") - Vanilla+ MoveObjects stays inactive");
                    else Plugin.Log.LogInfo("Move: no native 'move objects' found in the game - Vanilla+ MoveObjects available");
                }
                return native == 1;
            }
        }

        // Eigene Methode: fehlt ein hier verwendeter Typ, scheitert nur dieser Aufruf (Getter faengt das ab).
        // Rueckgabe = Fundstelle fuer das Log, null = nichts gefunden.
        // Regeln 1 und 2 sind eng (eigene Zeiger-Klasse mit passender Basis, eigener Bau-Modus). Die Namens-Heuristiken 3 und 4
        // gelten nur ab Spielversion 1.011 (dort ist das Verschieben laut Patch-Notes im Spiel) - auf aelteren Versionen
        // koennten sie faelschlich treffen und MoveObjects grundlos abschalten.
        private static string DetectNativeMove()
        {
            System.Reflection.Assembly asm = typeof(BuildController).Assembly;
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types ?? new Type[0]; }

            // 1. eigene Zeiger-Klasse fuer das Verschieben (Geschwister von RemovePointer/WgoBuildPointer). Typen per Name
            //    (kein typeof(RemovePointer): fehlt der Typ, soll nicht die ganze Pruefung scheitern).
            Type removePointer = null;
            foreach (Type t in types) if (t != null && t.Name == "RemovePointer") { removePointer = t; break; }
            // Basis = Typ von BuildPointer.PointerObject (Property oder Feld), sonst die Basisklasse von RemovePointer.
            // Ohne bekannte Basis wird Regel 1 uebersprungen (eine alte Hilfsklasse gleichen Namens darf nicht treffen).
            Type pointerBase = null;
            try
            {
                System.Reflection.PropertyInfo pp = AccessTools.Property(typeof(BuildPointer), "PointerObject");
                if (pp != null) pointerBase = pp.PropertyType;
                else
                {
                    System.Reflection.FieldInfo pf = AccessTools.Field(typeof(BuildPointer), "PointerObject");
                    if (pf != null) pointerBase = pf.FieldType;
                }
            }
            catch { }
            if ((pointerBase == null || pointerBase == typeof(object)) && removePointer != null) pointerBase = removePointer.BaseType;
            if (pointerBase != null && pointerBase != typeof(object))
            {
                var names = new HashSet<string> { "MovePointer", "BuildMovePointer", "MoveBuildPointer", "WgoMovePointer", "WgoMoveBuildPointer", "MoveWgoPointer" };
                foreach (Type t in types)
                {
                    if (t == null || !t.IsClass || t.IsAbstract || !names.Contains(t.Name)) continue;
                    if (!pointerBase.IsAssignableFrom(t)) continue;
                    return "type " + t.FullName;
                }
            }

            // 2. eigener Bau-Modus "Move" (bisher gibt es Place, ConveyorPlace, Remove ...)
            foreach (string n in Enum.GetNames(typeof(BuildingDef.BuildingMode)))
                if (n.IndexOf("Move", StringComparison.Ordinal) >= 0 && n.IndexOf("Remove", StringComparison.OrdinalIgnoreCase) < 0)
                    return "BuildingMode." + n;

            if (!Plugin.GameVersionAtLeast(1.011)) return null;

            // 3. BuildData.GetDataForMove... (wie GetDataForBuild/GetDataForRemove)
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
            foreach (System.Reflection.MethodInfo m in typeof(BuildData).GetMethods(all))
                if (IsMoveName(m.Name)) return "BuildData." + m.Name;

            // 4. Methoden an BuildController/BuildPointer, die nach "Objekt verschieben" klingen. Bewusst eng: Zeiger-/Kamera-
            //    Bewegung (MovePointer, OnCursorMoved ...) gab es schon vorher und darf nicht treffen.
            foreach (Type t in new[] { typeof(BuildController), typeof(BuildPointer) })
                foreach (System.Reflection.MethodInfo m in t.GetMethods(all))
                {
                    string n = m.Name;
                    if (!IsMoveName(n)) continue;
                    if (n.IndexOf("Wgo", StringComparison.Ordinal) >= 0 || n.IndexOf("Object", StringComparison.Ordinal) >= 0
                        || n.IndexOf("Building", StringComparison.Ordinal) >= 0 || n.IndexOf("Placed", StringComparison.Ordinal) >= 0
                        || n.IndexOf("MoveMode", StringComparison.Ordinal) >= 0 || n.IndexOf("ForMove", StringComparison.Ordinal) >= 0)
                        return t.Name + "." + n;
                }
            return null;
        }

        // "Move" (Gross-M, also nicht "Remove"), ohne Zeiger/Kamera/Maus-Bewegung
        private static bool IsMoveName(string n)
        {
            if (string.IsNullOrEmpty(n) || n.IndexOf("Move", StringComparison.Ordinal) < 0) return false;
            string l = n.ToLowerInvariant();
            return l.IndexOf("remove", StringComparison.Ordinal) < 0 && l.IndexOf("pointer", StringComparison.Ordinal) < 0
                && l.IndexOf("cursor", StringComparison.Ordinal) < 0 && l.IndexOf("camera", StringComparison.Ordinal) < 0
                && l.IndexOf("mouse", StringComparison.Ordinal) < 0 && l.IndexOf("player", StringComparison.Ordinal) < 0;
        }

        internal static bool InRemoveMode
        {
            get
            {
                try
                {
                    if (GameHasNativeMove) return false;
                    BuildController bc = BuildController.Instance;
                    return bc != null && bc.IsBuildModeActive && Pointer(bc)?.PointerObject is RemovePointer;
                }
                catch { return false; }
            }
        }

        private static BuildPointer Pointer(BuildController bc) => Traverse.Create(bc).Field("buildPointer").GetValue<BuildPointer>();
        private static WGODef Def(WgoData d) => ((ObjectLinkedToDefinition<WGODef>)d).Definition;

        // null = darf verschoben werden, sonst Grund fuer den Spieler ("" = still ablehnen)
        // Fast alles laesst sich verschieben: Zombie-Arbeiter landen beim Umsetzen auf dem Boden, Erweiterungen bleiben
        // stehen (Verbindung wird geloest), Foerderbaender werden am neuen Platz neu verbunden.
        // Nur Militaer-/Kampfgebaeude (eigenes System) und Stadtgebaeude-Plaetze bleiben aussen vor.
        private static string Why(Wgo w, out BuildingDef def)
        {
            def = null;
            if (w == null || w.Data == null) return "";
            WgoData d = w.Data;
            GameBalance.Me.buildableWgos.TryGetValue(w.Id, out def);
            if (def == null || (def.buildingMode != BuildingDef.BuildingMode.Place && def.buildingMode != BuildingDef.BuildingMode.ConveyorPlace))
                return Labels.T("Das lässt sich nicht verschieben.", "This can't be moved.");
            if (!w.IsBuildRemovable() || d.CraftComponent == null || d.CraftComponent.IsDestroyingCraftActive || d.CraftComponent.IsPreFinishHeld)
                return Labels.T("Das lässt sich gerade nicht verschieben.", "This can't be moved right now.");
            if (d.WorldId != MainGame.PlayerController.CurrentGameScene.Id) return "";
            // 1.7.2: Fabrik-Maschinen (eigene Foerderband-Ein-/Ausgabe) verlieren beim Umsetzen ihr Ausgabe-Band (Xylem) -
            // bis das sauber neu verbunden wird, bleiben sie stehen. Normale Foerderbaender lassen sich weiter verschieben.
            if (d.CraftableType == CraftableType.ConveyorWorkbench || Def(d).conveyorType == ConveyorElementType.Workbench)
                return Labels.T("Fabrik-Maschinen lassen sich noch nicht verschieben.", "Factory machines can't be moved yet.");
            switch (Def(d).interactionType)
            {
                case WGODef.InteractionType.TownBuildingPlace: case WGODef.InteractionType.FighterContainer: case WGODef.InteractionType.FightBuilder:
                    return Labels.T("Das lässt sich nicht verschieben.", "This can't be moved.");
            }
            // 1.7.6: Graeber und Grab-Zonen nicht verschieben (podostro: nach "Entfernen & Verschieben" einer Grab-Zone hing die
            // Kamera, bis zum Neustart wiederholter Fehler). Erkennung nur ueber Namen, ohne neue Spiel-Typen - lehnt im Zweifel
            // eher zu viel ab als zu wenig.
            if (IsGraveOrZone(w, d, def))
                return Labels.T("Gräber und Zonen lassen sich nicht verschieben.", "Graves and zones can't be moved.");
            return null;
        }

        private static bool IsGraveOrZone(Wgo w, WgoData d, BuildingDef def)
        {
            string it = "";
            try { it = Def(d).interactionType.ToString(); } catch { }
            // bewusst ohne "Body"/"Corpse": Leichen-Tische und -Lager sollen verschiebbar bleiben
            if (Has(it, "Grave", "Zone", "Burial")) return true;
            if (Has(d.GetType().Name, "Grave", "Zone", "Burial")) return true;
            if (Has(w.GetType().Name, "Grave", "Zone", "Burial")) return true;
            if (def != null && Has(def.customBuildAreaId, "grave", "zone", "burial")) return true;
            string id = d.id ?? "";
            // "graveyard_..." (Zaeune, Deko) bleibt verschiebbar, "grave..." (Grab, Grabstein, Grab-Zone) nicht
            string idNoYard = id.ToLowerInvariant().Replace("graveyard", "");
            return Has(idNoYard, "grave", "burial", "zone");
        }

        private static bool Has(string s, params string[] words)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (string x in words) if (s.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Bau-Modus umschalten (Abriss <-> Bau-Zeiger). Scheitert das mittendrin (Eingabe gesperrt, Kamera beim Bau-Zeiger),
        // verlassen wir den Bau-Modus ueber den normalen Ausgang des Spiels - der gibt Steuerung und Kamera zurueck.
        // Danach wird weitergeworfen, damit SafeMode den Fehler zaehlt.
        private static void Switch(BuildController bc, BuildData bd)
        {
            WorldZone zone = bc.CurrentWorldZone;
            switching = true;
            try { bc.DisableBuildMode(); bc.EnableBuildMode(bd, zone); }
            catch (Exception e)
            {
                switching = false;
                Plugin.Log.LogWarning("Move: switching build mode failed (" + e.GetType().Name + ": " + e.Message + ") - leaving build mode");
                End();
                backToRemove = false;
                Untint(lastMoved); lastMoved = null;
                try { bc.DisableBuildMode(); }
                catch (Exception e2) { Plugin.Log.LogWarning("Move: leaving build mode failed too: " + e2.GetType().Name + ": " + e2.Message); }
                throw;
            }
            finally { switching = false; }
        }

        // Verschieben abbrechen und, falls gerade verschoben wurde, den Bau-Modus verlassen - damit nie ein Bau-Zeiger mit
        // unseren Aenderungen (nichts verbrauchen) uebrig bleibt und Steuerung/Kamera wieder beim Spieler sind.
        // Rueckgabe: true = Bau-Modus wurde verlassen.
        internal static bool Abort(BuildController bc, string reason)
        {
            bool moving = Active || backToRemove;
            End();
            backToRemove = false;
            switching = false;
            Untint(lastMoved); lastMoved = null;
            if (!moving) return false;
            Plugin.Log.LogWarning("Move: aborted (" + reason + ") - leaving build mode");
            try
            {
                if (bc == null) bc = BuildController.Instance;
                if (bc != null && bc.IsBuildModeActive) { bc.DisableBuildMode(); return true; }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Move: leaving build mode failed: " + e.GetType().Name + ": " + e.Message); }
            return false;
        }

        // Fuer den catch von MoveBuildPatch: Verschieben beenden, im naechsten Frame zurueck in den Abriss-Modus (Tick) -
        // oder, falls MoveObjects inzwischen aus ist, Bau-Modus verlassen (Input).
        internal static void CancelAfterBuildError()
        {
            Plugin.Log.LogWarning("Move: placing failed - move cancelled, the object stays where it was");
            End();
            backToRemove = true;
            try { ManualSave.Toast(Labels.T("Verschieben fehlgeschlagen.", "Moving failed."), 3f); } catch { }
        }

        // Fuer den catch von MoveInputPatch: aufraeumen; das Original nur laufen lassen, wenn der Bau-Modus noch aktiv ist.
        internal static bool AfterInputError(BuildController bc)
        {
            Abort(bc, "error in input");
            try { return bc == null || bc.IsBuildModeActive; }
            catch { return true; }
        }

        private static void End()
        {
            try { if (orig != null) orig.SetSelectionTint(Color.white, 0f); } catch { }
            orig = null;
        }

        // Prefix BuildController.UpdateBuildModeInput: Aufnehmen (Dreh-Taste im Abriss-Modus) und Abbrechen
        internal static bool Input(BuildController bc)
        {
            if (!Plugin.MoveObjects.Value || GameHasNativeMove || !SafeMode.On("MoveObjects"))
            {
                // 1.7.6: mitten im Verschieben abgeschaltet (F9 oder SafeMode) -> Bau-Modus verlassen statt einen
                // Gratis-Bau-Zeiger stehen zu lassen. Ohne laufendes Verschieben passiert hier nichts.
                if (Active || backToRemove) return !Abort(bc, "feature turned off");
                return true;
            }
            if (Traverse.Create(bc).Field("isBuildModeInputLocked").GetValue<bool>()) return true;
            BuildPointer bp = Pointer(bc);
            if (bp == null) return true;
            if (Active)
            {
                if (LazyInput.GetKeyDown(GameKey.Back) || LazyInput.GetKeyDown(GameKey.RightClick))
                {
                    End();
                    Switch(bc, BuildData.GetDataForRemove());
                    try { LazyAudio.PlayAndForget("gui_click"); } catch { }
                    return false;
                }
                return true;
            }
            if (!(bp.PointerObject is RemovePointer) || !LazyInput.GetKeyDown(GameKey.Rotate)) return true;
            var sel = Traverse.Create(typeof(RemovePointer)).Field("currentRemovingSelection").GetValue<IBuildRemovable>() as Wgo;
            if (sel == null) return false;
            string why = Why(sel, out BuildingDef def);
            if (why != null)
            {
                try
                {
                    GameBalance.Me.buildableWgos.TryGetValue(sel.Id, out BuildingDef bd);
                    Plugin.Log.LogInfo("Move: refused " + sel.Id + " (" + why + ") mode=" + (bd != null ? bd.buildingMode.ToString() : "not buildable") + " type=" + Def(sel.Data).interactionType + " worker=" + (sel.Data.Worker != null) + " ext=" + sel.Data.AttachedWorkbenchExtensions.Count + "/" + sel.Data.WorkbenchParents.Count + " area=" + (bd != null ? bd.customBuildAreaId : ""));
                }
                catch { }
                if (why.Length > 0) ManualSave.Toast(why, 3f);
                return false;
            }
            orig = sel;
            string pickType = "";
            try { pickType = " type=" + Def(sel.Data).interactionType + " area=" + (def.customBuildAreaId ?? ""); } catch { }
            Plugin.Log.LogInfo("Move: picked " + sel.Id + pickType);
            Switch(bc, BuildData.GetDataForBuild(def));
            try { orig.SetSelectionTint(Color.gray, 0.45f); } catch { }
            return false;
        }

        // Postfix WgoBuildPointer.SetTarget: nichts verbrauchen, Bau-Limit ignorieren (es ist ja dasselbe Objekt), Drehung uebernehmen
        internal static void SetTarget(WgoBuildPointer p, Wgo target)
        {
            if (!Active) return;
            Traverse t = Traverse.Create(p);
            t.Field("canTakeResources").SetValue((Func<bool>)(() => true));
            t.Field("takeResourcesAction").SetValue(null);
            try
            {
                WgoPartData od = orig.Data.MainWgoPartData;
                if (target != null && target.CanBeRotated() && od != null && od.rotationIndex != -1 && target.MainWgoPart != null)
                {
                    target.MainWgoPart.ApplyWgoPartState(od.variationId, od.rotationIndex);
                    if (target.MainWgoPart.WgoPartData != null) { target.MainWgoPart.WgoPartData.variationId = od.variationId; target.MainWgoPart.WgoPartData.rotationIndex = od.rotationIndex; }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Move: rotation " + e.Message); }
        }

        // Postfix WgoBuildPointer.SetupSelectionCells: das Original blockiert seinen eigenen (alten) Platz nicht
        internal static void SetupCells(WgoBuildPointer p)
        {
            if (!Active || orig == null) return;
            var list = Traverse.Create(p).Field("buildColliders").GetValue<List<Collider>>();
            if (list == null) return;
            foreach (Collider c in orig.GetComponentsInChildren<Collider>(true))
                if (c != null && !list.Contains(c)) list.Add(c);
        }

        // Prefix BuildPointer.TryBuildActionInput: statt neu bauen -> Original umsetzen
        internal static bool Build(BuildPointer bp, ref bool result)
        {
            if (!Active) return true;
            var p = bp.PointerObject as WgoBuildPointer;
            if (p == null) return true;
            result = false;
            if (!Traverse.Create(p).Field("shownAsActive").GetValue<bool>()) return false;
            try
            {
                WgoPartData part = p.Target.MainWgoPart?.WgoPartData;
                try { orig.SetSelectionTint(Color.white, 0f); } catch { }
                bool rolledBack;
                lastMoved = Relocate(orig.Data, p.Target.Data.Position, part?.variationId, part != null ? part.rotationIndex : -1, p, out rolledBack);
                if (rolledBack)
                    ManualSave.Toast(Labels.T("Verschieben fehlgeschlagen – das Objekt wurde zurückgestellt. Bei Problemen speichern und neu laden.",
                        "Moving failed – the object was put back. Save and reload if something looks wrong."), 5f);
                else
                {
                    try { LazyAudio.PlayAndForget("build_place"); } catch { }
                    Plugin.Log.LogInfo("Move: placed " + orig.Id);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Move: " + e); ManualSave.Toast(Labels.T("Verschieben fehlgeschlagen.", "Moving failed."), 3f); }
            orig = null;
            backToRemove = true; // naechster Frame: zurueck in den Abriss-/Verschiebe-Modus
            return false;
        }

        internal static void OnDisable()
        {
            if (!switching && Active) End();
            if (!switching) { Untint(lastMoved); lastMoved = null; }
        }

        internal static void Tick()
        {
            if (GameHasNativeMove) { NativeNotice(); backToRemove = false; return; }
            if (!backToRemove) return;
            backToRemove = false;
            BuildController bc = BuildController.Instance;
            if (bc != null && bc.IsBuildModeActive) Switch(bc, BuildData.GetDataForRemove());
            Untint(lastMoved);
        }

        // einmaliger Hinweis, nur fuer Spieler, die unser Verschieben eingeschaltet haben, und erst im Spiel
        // (im Hauptmenue wuerde er den Update-/News-Hinweis ueberschreiben)
        private static void NativeNotice()
        {
            if (nativeToastShown || !Plugin.MoveObjects.Value) return;
            MainGame mg = MainGame.Instance;
            if (mg == null || mg.gameState != MainGame.GameState.InGame) return;
            nativeToastShown = true;
            ManualSave.Toast(Labels.T("Vanilla+: Das Spiel kann platzierte Objekte jetzt selbst verschieben – „Objekte verschieben“ von Vanilla+ bleibt deshalb aus.",
                "Vanilla+: The game can now move placed objects by itself, so Vanilla+ \"Move objects\" stays off."), 8f);
        }

        // 1.7.6: Nach dem Entfernen aus der Szene wird jeder Schritt einzeln abgesichert, damit AddWgoData immer erreicht
        // wird. Scheitert das Wiedereinsetzen am neuen Platz, kommt das Objekt an den alten Platz zurueck (rolledBack).
        // Ein Objekt darf nie ausserhalb der Szene bleiben (sonst greifen Spiel-Systeme jeden Frame ins Leere).
        private static Wgo Relocate(WgoData d, Vector3 pos, string varId, int rot, WgoBuildPointer p, out bool rolledBack)
        {
            rolledBack = false;
            GameSave save = MainGame.Instance.GameSave;
            WGODef def = Def(d);
            // Szene vor jeder Aenderung bestimmen - ohne Szene bleibt alles wie es ist
            GameScene scene = null;
            try { scene = Traverse.Create(p).Field("gameScene").GetValue<GameScene>(); } catch { }
            if (scene == null) scene = MainGame.PlayerController.CurrentGameScene;
            if (scene == null) throw new InvalidOperationException("no game scene - object left where it was");

            // Zombie-Arbeiter: wie beim Abbauen im Spiel als Gegenstand auf den Boden legen
            if (d.CraftableAttachedWorker is ZombieWgoData z)
            {
                try
                {
                    MainGame.Instance.dropSystem.DropItem(z.ZombieItem, z.AttachedWgoData.WorldId, z.AttachedWgoData.GetDropPos(z.ZombieItem));
                    z.UnAttachFromWgoData();
                    MainGame.ZombieSystemData.PutZombieFromGameSceneToStore(z);
                    Plugin.Log.LogInfo("Move: zombie worker put on the ground");
                }
                catch (Exception e) { Plugin.Log.LogWarning("Move: zombie " + e.Message); }
            }
            else if (d.Worker != null) { try { d.ClearWorker(); } catch { } }

            // Erweiterungen bleiben stehen, nur die Verbindung wird geloest (in beide Richtungen)
            foreach (SGuid ext in d.AttachedWorkbenchExtensions.ToList())
            {
                save.worldData.GetWgoData(ext)?.RemoveWorkbenchParent(d.UniqueId);
                d.RemoveWorkbenchExtension(ext);
            }
            foreach (SGuid par in d.WorkbenchParents.ToList())
            {
                save.worldData.GetWgoData(par)?.RemoveWorkbenchExtension(d.UniqueId);
                d.RemoveWorkbenchParent(par);
            }

            bool delayed = save.wgoDelayedEventSystemData.wgoUniqueIds.Contains(d.UniqueId);
            // alter Platz fuer die Rueckstellung
            Vector3 oldPos = d.Position;
            WgoPartData oldPart = d.MainWgoPartData;
            string oldVar = oldPart != null ? oldPart.variationId : null;
            int oldRot = oldPart != null ? oldPart.rotationIndex : -1;

            try { save.worldData.RemoveWgoDataFromGameScene(d, false); }   // Foerderband: trennt dabei seine Verbindungen
            catch (Exception e)
            {
                // Halb entfernt? Nicht raten (doppeltes Objekt waere schlimmer) - melden, Speichern & Neuladen raet der Toast.
                Plugin.Log.LogWarning("Move: removing from the scene failed, object may be half removed - save and reload: " + e);
                throw;
            }
            // was OnRemove rueckgaengig macht wiederherstellen (wie bei einem frisch gebauten Objekt) - jeder Schritt einzeln
            d.isRemovingFromData = false;
            d.gdPointsRegistered = false;
            d.Position = pos;
            if (rot != -1 && d.MainWgoPartData != null) { d.MainWgoPartData.variationId = varId; d.MainWgoPartData.rotationIndex = rot; }
            try { if (def.townQuality > 0) save.townSystem.Quality += def.townQuality; }
            catch (Exception e) { Plugin.Log.LogWarning("Move: town quality " + e.Message); }
            try { if (!string.IsNullOrEmpty(def.npcLifeSimGroup)) save.npcLifeSimulatorData.GetGroupById(def.npcLifeSimGroup)?.AddWgoToGroup(d); }
            catch (Exception e) { Plugin.Log.LogWarning("Move: npc group " + e.Message); }
            try { if (!string.IsNullOrEmpty(def.attachedScript)) WgoDataScriptsManager.CreateScript(d, def.attachedScript); }
            catch (Exception e) { Plugin.Log.LogWarning("Move: attached script " + def.attachedScript + ": " + e.Message); }
            try { if (delayed && !save.wgoDelayedEventSystemData.wgoUniqueIds.Contains(d.UniqueId)) save.wgoDelayedEventSystemData.wgoUniqueIds.Add(d.UniqueId); }
            catch (Exception e) { Plugin.Log.LogWarning("Move: delayed events " + e.Message); }

            Wgo wgo = null;
            try { wgo = scene.AddWgoData(d); }
            catch (Exception e) { Plugin.Log.LogWarning("Move: placing at the new spot failed: " + e); }
            if (wgo == null)
            {
                // Rueckstellung: alter Platz, alte Drehung, nochmal einsetzen
                rolledBack = true;
                Plugin.Log.LogWarning("Move: rollback to old place");
                d.isRemovingFromData = false;
                d.Position = oldPos;
                if (oldPart != null && d.MainWgoPartData != null) { d.MainWgoPartData.variationId = oldVar; d.MainWgoPartData.rotationIndex = oldRot; }
                try { wgo = scene.AddWgoData(d); }
                catch (Exception e) { Plugin.Log.LogWarning("Move: rollback failed too - save and reload: " + e); }
                if (wgo == null) Plugin.Log.LogWarning("Move: object could not be put back into the scene - save and reload");
            }
            try { if (wgo != null) wgo.SetSelectionTint(Color.white, 0f); } catch { }

            // Erweiterung auf einen Tisch gesetzt -> wie beim Bauen verbinden
            try
            {
                GameBalance.Me.buildableWgos.TryGetValue(d.id, out BuildingDef bdef);
                if (wgo != null && bdef != null && bdef.chooseCustomBuildAreaType == BuildingDef.BuildAreaChoosingType.FullCoverSoft && BuildController.Instance.CurrentFullCoverSoftHintArea != null)
                {
                    Wgo host = BuildController.Instance.CurrentFullCoverSoftHintArea.GetComponentInParent<Wgo>();
                    if (host != null && host.Data != d) { host.Data.AddWorkbenchExtension(d.UniqueId); d.AddWorkbenchParent(host.Data.UniqueId); }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Move: extension link " + e.Message); }

            // Erweiterungen in Reichweite (Werkzeugkiste, Haertungseimer ...) am neuen Platz neu verbinden.
            // Das Spiel macht das selbst nur beim allerersten Erscheinen eines Objekts - ein verschobenes Objekt
            // ist schon einmal erschienen, darum hier derselbe Aufruf wie beim Bauen.
            try
            {
                if (wgo != null)
                {
                    WGODef logic = GameBalance.Me.GetWorkbenchExtensionLogicDef(d.id);
                    bool parent = logic != null && GameBalance.Me.workbenchesWhichUseExtensions.Contains(logic);
                    if (parent || GameBalance.Me.IsWorkbenchExtensionId(d.id))
                    {
                        var m = AccessTools.Method(typeof(Wgo), "TryRegisterWorkbenchExtensionDelayed");
                        if (m != null) wgo.StartCoroutine((System.Collections.IEnumerator)m.Invoke(wgo, new object[] { parent }));
                        Plugin.Log.LogInfo("Move: re-linking extensions of " + d.id + (parent ? " (workbench)" : " (extension)"));
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Move: extension relink " + e.Message); }

            // Foerderband: am neuen Platz neu verbinden (wie beim Bauen)
            if (wgo != null && d is ConveyorWgoData && p is ConveyorBuildPointer cp)
            {
                try { cp.MakeConnections(wgo); } catch (Exception e) { Plugin.Log.LogWarning("Move: conveyor " + e.Message); }
            }
            return wgo;
        }
    }

    // Bau-Fenster: "Entfernen" -> "Entfernen & Verschieben", solange Verschieben an ist
    [HarmonyPatch(typeof(UIBuildingWidgetData), MethodType.Constructor, new[] { typeof(BuildData), typeof(MultiInventory), typeof(Action<BuildData, List<NeedItemData>>), typeof(Func<BuildData, List<NeedItemData>, bool>), typeof(Action), typeof(Action), typeof(WorldZoneData) })]
    internal static class MoveRemoveLabelPatch
    {
        private static void Postfix(UIBuildingWidgetData __instance, BuildData buildData)
        {
            try
            {
                if (!Plugin.MoveObjects.Value || BuildMove.GameHasNativeMove || buildData == null || buildData.BuildingMode != BuildingDef.BuildingMode.Remove) return;
                Traverse.Create(__instance).Property("Name").SetValue(Labels.T("Entfernen & Verschieben", "Remove & move"));
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(BuildController), "UpdateBuildModeInput")]
    internal static class MoveInputPatch
    {
        private static bool Prefix(BuildController __instance)
        {
            try { return BuildMove.Input(__instance); }
            catch (Exception e)
            {
                SafeMode.Fail("MoveObjects", e);
                // 1.7.6: Verschiebe-Zustand zuruecksetzen, Bau-Modus ggf. verlassen (Steuerung/Kamera zurueck)
                try { return BuildMove.AfterInputError(__instance); } catch { return true; }
            }
        }
    }

    [HarmonyPatch(typeof(WgoBuildPointer), nameof(WgoBuildPointer.SetTarget))]
    internal static class MoveTargetPatch
    {
        private static void Postfix(WgoBuildPointer __instance, Wgo target)
        {
            try { BuildMove.SetTarget(__instance, target); } catch (Exception e) { SafeMode.Fail("MoveObjects", e); }
        }
    }

    [HarmonyPatch(typeof(WgoBuildPointer), nameof(WgoBuildPointer.SetupSelectionCells))]
    internal static class MoveCellsPatch
    {
        private static void Postfix(WgoBuildPointer __instance)
        {
            try { BuildMove.SetupCells(__instance); } catch { }
        }
    }

    [HarmonyPatch(typeof(BuildPointer), nameof(BuildPointer.TryBuildActionInput))]
    internal static class MoveBuildPatch
    {
        private static bool Prefix(BuildPointer __instance, ref bool __result)
        {
            bool wasActive = BuildMove.Active;
            try { return BuildMove.Build(__instance, ref __result); }
            catch (Exception e)
            {
                SafeMode.Fail("MoveObjects", e);
                // 1.7.6: Lief gerade ein Verschieben, darf das Original NICHT bauen (unser Zeiger verbraucht nichts ->
                // Gratis-Doppel). Stattdessen abbrechen; das Objekt steht noch am alten Platz.
                // Den Bau-Modus erst im naechsten Frame wechseln (wie nach normalem Platzieren), nicht mitten in diesem Aufruf.
                if (!wasActive) return true;
                try { BuildMove.CancelAfterBuildError(); } catch { }
                __result = false;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(BuildController), nameof(BuildController.DisableBuildMode))]
    internal static class MoveDisablePatch
    {
        private static void Postfix() { try { BuildMove.OnDisable(); } catch (Exception e) { SafeMode.Fail("MoveObjects", e); } }
    }
}
