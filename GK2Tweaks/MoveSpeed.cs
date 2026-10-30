using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace GK2Tweaks
{
    // Laufgeschwindigkeit (nicht Vanilla, Wunsch eines Ko-fi-Unterstuetzers): 100-200 %, nur fuer die eigene Figur.
    // Das Spiel rechnet in PlayerPhysicalBody.MoveByDirection "physicsConfig.speed * SpeedMultiplier". Dort ersetzen wir
    // nur das Lesen von SpeedMultiplier durch MoveSpeed.Scaled (= SpeedMultiplier * Faktor). Es wird nichts gespeichert
    // oder dauerhaft veraendert: die Verlangsamungen des Spiels (z. B. Bogen zielen = 0,5) wirken weiter, bei 100 % ist
    // alles wie im Original. Die Laufanimation wird ueber den Animator-Wert "walk_speed" passend schneller.
    internal static class MoveSpeed
    {
        internal static int Hooked;   // Anzahl ersetzter Stellen (erwartet: 1) - fuer Log und Dev-Test

        internal static float Factor
        {
            get
            {
                int v = Plugin.MoveSpeed != null ? Plugin.MoveSpeed.Value : 100;
                return v <= 100 ? 1f : Mathf.Min(v, 200) / 100f;
            }
        }

        // ersetzt "this.SpeedMultiplier" in MoveByDirection
        internal static float Scaled(PlayerPhysicalBody body)
        {
            float m = body.SpeedMultiplier;
            try
            {
                float f = SafeMode.On("MoveSpeed") ? Factor : 1f;
                if (f > 1f && IsPlayer(body)) m *= f;
                Animate(body, f);
            }
            catch { }
            return m;
        }

        private static bool IsPlayer(PlayerPhysicalBody body)
        {
            PlayerController pc = MainGame.PlayerController;
            return pc != null && pc.PhysicalBody == body;
        }

        // ---------- Laufanimation ----------
        private static AccessTools.FieldRef<PlayerPhysicalBody, PlayerView> viewRef;
        private static readonly int walkHash = Animator.StringToHash("walk_speed");
        private static Animator anim;
        private static bool hasParam, animOff;
        private static float baseWalk = 1f, setWalk = -1f;

        private static void Animate(PlayerPhysicalBody body, float f)
        {
            if (animOff) return;
            try { AnimateCore(body, f); }
            catch (Exception e) { animOff = true; Plugin.Log.LogWarning("Move speed: walk animation not adjusted - " + e.Message); }
        }

        private static void AnimateCore(PlayerPhysicalBody body, float f)
        {
            if (anim == null)
            {
                if (f <= 1f) return;   // nie angefasst -> nichts zu tun
                if (viewRef == null) viewRef = AccessTools.FieldRefAccess<PlayerPhysicalBody, PlayerView>("playerView");
                PlayerView v = viewRef(body);
                Animator a = v != null && v.PlayerAnimation != null ? v.PlayerAnimation.Animator : null;
                if (a == null) return;
                anim = a;
                hasParam = false;
                foreach (AnimatorControllerParameter p in a.parameters)
                    if (p.nameHash == walkHash && p.type == AnimatorControllerParameterType.Float) { hasParam = true; break; }
                baseWalk = hasParam ? a.GetFloat(walkHash) : 1f;
                if (baseWalk <= 0.01f) baseWalk = 1f;
                setWalk = -1f;
                Plugin.Log.LogInfo("Move speed: walk animation " + (hasParam ? "follows the speed (walk_speed " + baseWalk.ToString("0.##") + ")" : "has no walk_speed value - only the speed changes"));
            }
            if (!hasParam) return;
            float want = baseWalk * f;
            if (Mathf.Abs(want - setWalk) < 0.001f) return;
            anim.SetFloat(walkHash, want);
            setWalk = want;
        }
    }

    [HarmonyPatch(typeof(PlayerPhysicalBody), nameof(PlayerPhysicalBody.MoveByDirection))]
    internal static class MoveSpeedPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(PlayerPhysicalBody), nameof(PlayerPhysicalBody.SpeedMultiplier));
            MethodInfo scaled = AccessTools.Method(typeof(MoveSpeed), nameof(MoveSpeed.Scaled));
            var list = new List<CodeInstruction>(code);
            int n = 0;
            foreach (CodeInstruction ci in list)
            {
                if (getter == null || !ci.Calls(getter)) continue;
                ci.opcode = OpCodes.Call;   // gleicher Stack: (this) -> float
                ci.operand = scaled;
                n++;
            }
            MoveSpeed.Hooked = n;
            if (n == 1) Plugin.Log.LogInfo("Move speed: hooked into the player movement");
            else Plugin.Log.LogWarning("Move speed: expected 1 place in MoveByDirection, found " + n + (n == 0 ? " - option has no effect (game update?)" : ""));
            return list;
        }
    }
}
