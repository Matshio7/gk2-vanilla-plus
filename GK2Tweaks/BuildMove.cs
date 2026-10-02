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

        internal static bool InRemoveMode
        {
            get
            {
                try
                {
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
            switch (Def(d).interactionType)
            {
                case WGODef.InteractionType.TownBuildingPlace: case WGODef.InteractionType.FighterContainer: case WGODef.InteractionType.FightBuilder:
                    return Labels.T("Das lässt sich nicht verschieben.", "This can't be moved.");
            }
            return null;
        }

        private static void Switch(BuildController bc, BuildData bd)
        {
            WorldZone zone = bc.CurrentWorldZone;
            switching = true;
            try { bc.DisableBuildMode(); bc.EnableBuildMode(bd, zone); }
            finally { switching = false; }
        }

        private static void End()
        {
            try { if (orig != null) orig.SetSelectionTint(Color.white, 0f); } catch { }
            orig = null;
        }

        // Prefix BuildController.UpdateBuildModeInput: Aufnehmen (Dreh-Taste im Abriss-Modus) und Abbrechen
        internal static bool Input(BuildController bc)
        {
            if (!Plugin.MoveObjects.Value) { if (Active) End(); return true; }
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
            Plugin.Log.LogInfo("Move: picked " + sel.Id);
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
                lastMoved = Relocate(orig.Data, p.Target.Data.Position, part?.variationId, part != null ? part.rotationIndex : -1, p);
                LazyAudio.PlayAndForget("build_place");
                Plugin.Log.LogInfo("Move: placed " + orig.Id);
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
            if (!backToRemove) return;
            backToRemove = false;
            BuildController bc = BuildController.Instance;
            if (bc != null && bc.IsBuildModeActive) Switch(bc, BuildData.GetDataForRemove());
            Untint(lastMoved);
        }

        private static Wgo Relocate(WgoData d, Vector3 pos, string varId, int rot, WgoBuildPointer p)
        {
            GameSave save = MainGame.Instance.GameSave;
            WGODef def = Def(d);

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
            save.worldData.RemoveWgoDataFromGameScene(d, false);   // Foerderband: trennt dabei seine Verbindungen
            // was OnRemove rueckgaengig macht wiederherstellen (wie bei einem frisch gebauten Objekt)
            d.isRemovingFromData = false;
            d.gdPointsRegistered = false;
            d.Position = pos;
            if (rot != -1 && d.MainWgoPartData != null) { d.MainWgoPartData.variationId = varId; d.MainWgoPartData.rotationIndex = rot; }
            if (def.townQuality > 0) save.townSystem.Quality += def.townQuality;
            if (!string.IsNullOrEmpty(def.npcLifeSimGroup)) save.npcLifeSimulatorData.GetGroupById(def.npcLifeSimGroup)?.AddWgoToGroup(d);
            if (!string.IsNullOrEmpty(def.attachedScript)) WgoDataScriptsManager.CreateScript(d, def.attachedScript);
            if (delayed && !save.wgoDelayedEventSystemData.wgoUniqueIds.Contains(d.UniqueId)) save.wgoDelayedEventSystemData.wgoUniqueIds.Add(d.UniqueId);

            GameScene scene = Traverse.Create(p).Field("gameScene").GetValue<GameScene>() ?? MainGame.PlayerController.CurrentGameScene;
            Wgo wgo = scene.AddWgoData(d);
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
                if (!Plugin.MoveObjects.Value || buildData == null || buildData.BuildingMode != BuildingDef.BuildingMode.Remove) return;
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
            catch (Exception e) { Plugin.Log.LogWarning("Move input: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(WgoBuildPointer), nameof(WgoBuildPointer.SetTarget))]
    internal static class MoveTargetPatch
    {
        private static void Postfix(WgoBuildPointer __instance, Wgo target)
        {
            try { BuildMove.SetTarget(__instance, target); } catch (Exception e) { Plugin.Log.LogWarning("Move target: " + e.Message); }
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
            try { return BuildMove.Build(__instance, ref __result); }
            catch (Exception e) { Plugin.Log.LogWarning("Move build: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(BuildController), nameof(BuildController.DisableBuildMode))]
    internal static class MoveDisablePatch
    {
        private static void Postfix() { BuildMove.OnDisable(); }
    }
}
