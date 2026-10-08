using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Tweaks
{
    // Talente & Forschung zurueckerstatten (nicht ganz Vanilla, standardmaessig aus).
    // - Talente (Spieler) und Zombie-Perks: Rechtsklick auf einen freigeschalteten Knoten (Controller: Taste laut Hinweis)
    // - Forschung: Klick auf eine erforschte Technik -> im Fenster des Spiels "Zurückerstatten"
    // Immer mit Rueckfrage im Dialog des Spiels. Abhaengige Knoten werden mit erstattet, damit nichts "in der Luft haengt".
    // Start-Knoten und Ruf-Forschung werden nie erstattet. Effekte beim Kauf (z. B. Gegenstaende) bleiben.
    // Idee: "Talent & Tech Refund" (Steam Workshop) - eigene Umsetzung. Ist diese Mod installiert, bleibt unsere Funktion aus.
    internal static class Respec
    {
        private static int otherMod = -1;
        internal static bool OtherMod
        {
            get
            {
                if (otherMod < 0) { otherMod = ModCompat.Has("respec") ? 1 : 0; if (otherMod == 1) Plugin.Log.LogInfo("Respec: other respec mod installed, own function stays off"); }
                return otherMod == 1;
            }
        }
        internal static bool On => Plugin.Respec.Value && SafeMode.On("Respec") && !OtherMod;

        internal static TalentLevelUpWidget Hovered;

        private static string Name(string id)
        {
            string n = Pins.Loc(id);
            return string.IsNullOrEmpty(n) ? id : n;
        }

        // Elternbedingung ohne die zu entfernenden Knoten (keine Eltern = haengt von nichts ab)
        private static bool ParentsOk(List<string> parents, bool all, Func<string, bool> has)
        {
            if (parents == null || parents.Count == 0) return true;
            if (all) { foreach (string p in parents) if (!has(p)) return false; return true; }
            foreach (string p in parents) if (has(p)) return true;
            return false;
        }

        // ---------- Talente (Spieler + Zombie) ----------
        internal static bool IsStudied(TalentLevelUpWidget w)
        {
            TalentLevelUpDef def = w?.Data?.Def;
            if (def == null) return false;
            if (def.isZombiePerk) return w.Data.ZombieWgoData != null && w.Data.ZombieWgoData.IsTalentLevelUpStudied(def.id);
            TalentData br = MainGame.Instance.GameSave.talentSystemData.GetTalentBranch(def.talentId);
            return br != null && br.studiedLevelUps.Contains(def.id);
        }

        // Versteckte Quest-Talente (z. B. Suenden/Wochentage) und Talente mit Kauf-Effekten (expressionsOnBuy) nicht erstatten:
        // deren Effekte (Quest-Fortschritt, Flags) laesst sich nicht sauber rueckgaengig machen
        internal static bool QuestLocked(TalentLevelUpDef d) => d != null && (d.isHidden || (d.expressionsOnBuy != null && d.expressionsOnBuy.Count > 0));
        internal static bool IsQuestTalent(TalentLevelUpWidget w) => w != null && w.Data?.Def != null && QuestLocked(w.Data.Def) && IsStudied(w);
        internal static bool CanRefund(TalentLevelUpWidget w) => w != null && w.Data?.Def != null && !w.Data.Def.availableAtStart && !QuestLocked(w.Data.Def) && IsStudied(w);

        private static List<TalentLevelUpDef> Cascade(TalentLevelUpDef root, List<string> studied)
        {
            var removed = new List<TalentLevelUpDef> { root };
            var ids = new HashSet<string> { root.id };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (string id in studied)
                {
                    if (ids.Contains(id)) continue;
                    TalentLevelUpDef d = ((GameBalanceBase)GameBalance.Me).GetDataOrNull<TalentLevelUpDef>(id);
                    if (d == null || d.availableAtStart) continue;
                    if (!ParentsOk(d.parents, d.lockType == TalentLevelUpDef.LockType.All, p => studied.Contains(p) && !ids.Contains(p)))
                    { removed.Add(d); ids.Add(id); changed = true; }
                }
            }
            return removed;
        }

        internal static void AskTalent(TalentLevelUpWidget w)
        {
            if (!CanRefund(w)) return;
            TalentLevelUpDef def = w.Data.Def;
            ZombieWgoData z = def.isZombiePerk ? w.Data.ZombieWgoData : null;
            List<string> studied = z != null ? z.GetTalentBranch(def.talentId).studiedLevelUps : MainGame.Instance.GameSave.talentSystemData.GetTalentBranch(def.talentId).studiedLevelUps;
            List<TalentLevelUpDef> list = Cascade(def, studied);
            if (list.Any(QuestLocked))
            {
                ManualSave.Toast(Labels.T("Geht nicht: Ein Quest-Talent hängt davon ab.", "Not possible: a quest talent depends on it."), 3f);
                return;
            }
            string back;
            if (z != null)
            {
                int r = list.Sum(d => d.techRed), g = list.Sum(d => d.techGreen), b = list.Sum(d => d.techBlue);
                back = Spheres(r, g, b);
            }
            else back = list.Sum(d => d.talentExpPointsPrice) + " " + Labels.T("Talentpunkte", "talent points");
            Confirm(Name(def.id), list, back, () =>
            {
                if (z != null) RefundZombie(z, list); else RefundPlayer(list);
                RedrawTalents(w, def, z);
                ManualSave.Toast(string.Format(Labels.T("Zurückerstattet: {0}", "Refunded: {0}"), list.Count), 3f);
            });
        }

        private static void RefundPlayer(List<TalentLevelUpDef> list)
        {
            var ts = MainGame.Instance.GameSave.talentSystemData;
            var perks = MainGame.Instance.GameSave.perkSystemData;
            foreach (TalentLevelUpDef d in list)
            {
                TalentData br = ts.GetTalentBranch(d.talentId);
                if (br == null || !br.studiedLevelUps.Remove(d.id)) continue;
                br.talentExpPoints += d.talentExpPointsPrice;
                if (d.talentValueAdd > 0) br.curTalentValue -= d.talentValueAdd;
                Plugin.Log.LogInfo("Respec: talent " + d.id + " (+" + d.talentExpPointsPrice + " points)");
            }
            foreach (TalentLevelUpDef d in list)
                if (!string.IsNullOrEmpty(d.linkedPerk) && !PlayerPerkStillGranted(d.linkedPerk) && perks.HasPerk(d.linkedPerk))
                    perks.RemovePerk(d.linkedPerk);
        }

        private static bool PlayerPerkStillGranted(string perk)
        {
            foreach (TalentData br in MainGame.Instance.GameSave.talentSystemData.talentData)
                foreach (string id in br.studiedLevelUps)
                {
                    TalentLevelUpDef d = ((GameBalanceBase)GameBalance.Me).GetDataOrNull<TalentLevelUpDef>(id);
                    if (d != null && d.linkedPerk == perk) return true;
                }
            foreach (TechDef t in GameBalance.Me.techDefs)
                if (t.perksAfterUnlock != null && t.perksAfterUnlock.Contains(perk) && MainGame.Instance.GameSave.knowledgeSystem.IsTechUnlocked(t.id)) return true;
            return false;
        }

        private static void RefundZombie(ZombieWgoData z, List<TalentLevelUpDef> list)
        {
            foreach (TalentLevelUpDef d in list)
            {
                var br = z.GetTalentBranch(d.talentId);
                if (br == null || !br.studiedLevelUps.Remove(d.id)) continue;
                // deaktivierte Perks (zu wenig Schaedel) haben Wert und Perk schon abgegeben
                if (!z.disabledTalentLevelUps.Remove(d.id))
                {
                    br.curTalentValue -= d.talentValueAdd;
                    if (!string.IsNullOrEmpty(d.linkedPerk)) z.RemovePerk(d.linkedPerk);
                }
                z.techRed += d.techRed; z.techGreen += d.techGreen; z.techBlue += d.techBlue;
                Plugin.Log.LogInfo("Respec: zombie perk " + d.id);
            }
            try { z.CheckRedSkulls(true); } catch { }
        }

        private static void RedrawTalents(TalentLevelUpWidget w, TalentLevelUpDef def, ZombieWgoData z)
        {
            try
            {
                if (z != null)
                {
                    UIZombieWorkerWindow zw = TradeHelper.Cached<UIZombieWorkerWindow>();
                    if (zw != null)
                        foreach (string m in new[] { "RedrawPerksTab", "RedrawPerksTabLabel", "RedrawSpheres", "RedrawSkulls", "RedrawTalentIcons" })
                            try { Traverse.Create(zw).Method(m).GetValue(); } catch { }
                }
                else
                {
                    var page = w != null ? w.GetComponentInParent<CharInspirationPageWidget>() : null;
                    if (page != null) Traverse.Create(page).Method("OnTalentLevelPurchased", new[] { typeof(string), typeof(string) }).GetValue(def.talentId, def.id);
                }
                var tree = w != null ? w.GetComponentInParent<TalentLevelUpsWidget>() : null;
                if (tree != null) ((LazyWidgetBase)tree).Redraw();
            }
            catch (Exception e) { Plugin.Log.LogWarning("Respec redraw: " + e.Message); }
            try { LazyAudio.PlayAndForget("coins_sound"); } catch { }
        }

        // ---------- Forschung ----------
        internal static bool Refundable(TechDef t) =>
            t != null && t.techDefType == TechDefType.Common && !t.availableAtStart && Price(t) > 0 &&
            MainGame.Instance.GameSave.knowledgeSystem.IsTechUnlocked(t.id);

        private static float Price(TechDef t)
        {
            GameRes p = t.PriceRes;
            return p.GetWithoutSystemsCheck("tech_red") + p.GetWithoutSystemsCheck("tech_green") + p.GetWithoutSystemsCheck("tech_blue");
        }

        private static List<TechDef> Cascade(TechDef root)
        {
            var ks = MainGame.Instance.GameSave.knowledgeSystem;
            var removed = new List<TechDef> { root };
            var ids = new HashSet<string> { root.id };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (TechDef t in GameBalance.Me.techDefs)
                {
                    if (ids.Contains(t.id) || !Refundable(t)) continue;
                    if (!ParentsOk(t.parents, t.techLockType == TechLockType.All, p => ks.unlockedTechs.Contains(p) && !ids.Contains(p)))
                    { removed.Add(t); ids.Add(t.id); changed = true; }
                }
            }
            return removed;
        }

        internal static void AskTech(TechDef def, Action redraw)
        {
            if (!Refundable(def)) return;
            List<TechDef> list = Cascade(def);
            GameRes sum = new GameRes();
            foreach (TechDef t in list) sum.Add(t.PriceRes);
            string back = Spheres((int)sum.GetWithoutSystemsCheck("tech_red"), (int)sum.GetWithoutSystemsCheck("tech_green"), (int)sum.GetWithoutSystemsCheck("tech_blue"));
            Confirm(Name(def.id), list.Select(t => t.id).ToList(), back, () =>
            {
                RefundTechs(list);
                redraw?.Invoke();
                try { LazyAudio.PlayAndForget("coins_sound"); } catch { }
                ManualSave.Toast(string.Format(Labels.T("Zurückerstattet: {0}", "Refunded: {0}"), list.Count), 3f);
            }, true);
        }

        private static void RefundTechs(List<TechDef> list)
        {
            var ks = MainGame.Instance.GameSave.knowledgeSystem;
            foreach (TechDef t in list)
            {
                if (!ks.IsTechUnlocked(t.id)) continue;
                ks.RemoveTech(t.id);
                MainGame.PlayerData.AddRes(t.PriceRes);
                Plugin.Log.LogInfo("Respec: tech " + t.id);
            }
            // was die Forschung freigeschaltet hat, wieder wegnehmen (ausser eine andere erforschte Technik gibt es auch)
            Func<Func<TechDef, List<string>>, string, bool> other = (sel, id) =>
                GameBalance.Me.techDefs.Any(o => ks.IsTechUnlocked(o.id) && sel(o) != null && sel(o).Contains(id));
            foreach (TechDef t in list)
            {
                foreach (string c in t.craftsAfterUnlock ?? new List<string>()) if (!other(o => o.craftsAfterUnlock, c)) ks.RemoveUnlockedCraft(c);
                foreach (string a in t.alchemyFormulasAfterUnlock ?? new List<string>()) if (!other(o => o.alchemyFormulasAfterUnlock, a)) ks.unlockedAlchemyFormulas.Remove(a);
                foreach (string b in t.buildingsAfterUnlock ?? new List<string>()) if (!other(o => o.buildingsAfterUnlock, b)) ks.unlockedBuildings.Remove(b);
                foreach (string p in t.perksAfterUnlock ?? new List<string>())
                    if (!PlayerPerkStillGranted(p) && MainGame.Instance.GameSave.perkSystemData.HasPerk(p)) MainGame.Instance.GameSave.perkSystemData.RemovePerk(p);
            }
        }

        // ---------- gemeinsam ----------
        private static string Spheres(int r, int g, int b)
        {
            var parts = new List<string>();
            if (r > 0) parts.Add(r + " " + Labels.T("rot", "red"));
            if (g > 0) parts.Add(g + " " + Labels.T("grün", "green"));
            if (b > 0) parts.Add(b + " " + Labels.T("blau", "blue"));
            return parts.Count == 0 ? Labels.T("nichts", "nothing") : string.Join(", ", parts.ToArray()) + " " + Labels.T("Sphären", "spheres");
        }

        private static void Confirm(string name, List<TalentLevelUpDef> list, string back, Action yes) =>
            Confirm(name, list.Select(d => d.id).ToList(), back, yes, false);

        private static void Confirm(string name, List<string> ids, string back, Action yes, bool tech)
        {
            string info = string.Format(Labels.T("Du bekommst zurück: {0}", "You get back: {0}"), back);
            if (ids.Count > 1)
                info += "\n" + Labels.T("Hängt davon ab und wird mit erstattet: ", "Depends on it and is refunded too: ") + string.Join(", ", ids.Skip(1).Select(Name).ToArray());
            info += "\n" + (tech ? Labels.T("Was die Forschung freigeschaltet hat (Rezepte, Baupläne), ist danach wieder gesperrt.", "What it unlocked (recipes, blueprints) is locked again.")
                                 : Labels.T("Der verbundene Perk wird entfernt.", "The linked perk is removed."));
            UIDialogWindow dw = LazyUI.GetWindow<UIDialogWindow>();
            Action close = () => { try { ((LazyWindow<UIDialogWindowData>)dw).Close(); } catch { } };
            ((LazyWindow<UIDialogWindowData>)dw).Open(new UIDialogWindowData(
                string.Format(Labels.T("„{0}“ zurückerstatten?", "Refund \"{0}\"?"), name), info,
                () => { close(); try { yes(); } catch (Exception e) { SafeMode.Fail("Respec", e); } }, close, true));
        }
    }

    // Maus/Controller ueber einem Talent-Knoten merken
    [HarmonyPatch(typeof(TalentLevelUpWidget), "OnOver")]
    internal static class RespecOverPatch
    {
        private static void Postfix(TalentLevelUpWidget __instance) => Respec.Hovered = __instance;
    }

    [HarmonyPatch(typeof(TalentLevelUpWidget), "OnOut")]
    internal static class RespecOutPatch
    {
        private static void Postfix(TalentLevelUpWidget __instance) { if (Respec.Hovered == __instance) Respec.Hovered = null; }
    }

    // Erforschte Technik anklicken: im Fenster des Spiels zusaetzlich "Zurückerstatten"
    [HarmonyPatch(typeof(TechTreePageWidget), "OnTechClicked")]
    internal static class RespecTechPatch
    {
        private static bool Prefix(TechTreePageWidget __instance, TechTreeElementBaseWidgetData techData)
        {
            try
            {
                if (!Respec.On) return true;
                TechDef def = techData?.techDef;
                if (!Respec.Refundable(def)) return true;
                TechTreePageWidget page = __instance;
                var win = LazyUI.GetWindow<UITechTreeElementWindow>();
                Action close = () => ((LazyWindow<UITechTreeElementWindowData>)win).Close();
                Action redraw = () =>
                {
                    foreach (string m in new[] { "RedrawSpheres", "UpdateElements", "UpdateConnectors" })
                        try { Traverse.Create(page).Method(m).GetValue(); } catch { }
                };
                var ok = new UIDialogWindowData.ButtonData(close, LLBase.L("btn_ok"), null, true, GameKey.Select, "");
                var refund = new UIDialogWindowData.ButtonData(() => { close(); Respec.AskTech(def, redraw); }, Labels.T("Zurückerstatten", "Refund"), null, true, GameKey.ItemMove, "");
                ((LazyWindow<UITechTreeElementWindowData>)win).Open(new UITechTreeElementWindowData(def, new List<UIDialogWindowData.ButtonData> { refund, ok }));
                return false;
            }
            catch (Exception e) { SafeMode.Fail("Respec", e); return true; }
        }
    }
}
