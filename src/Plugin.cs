using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AlarmClock
{
    [BepInPlugin(Id, "Alarm Clock", "0.2.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.skeptic043.sailwind.alarmclock";
        private static ConfigEntry<KeyboardShortcut> modifier;
        private static ConfigEntry<float> ringRange;
        private static ConfigEntry<float> ringDuration;
        private static ConfigEntry<float> volume;
        private static ConfigEntry<float> bellPitch;
        private static ConfigEntry<bool> controlsTooltip;
        private Harmony harmony;

        internal static ManualLogSource Log { get; private set; }
        internal static AlarmStore Store { get; } = new AlarmStore();

        // Read on every use so Configuration Manager edits apply immediately.
        internal static float RingRange => ringRange.Value;
        internal static float RingDuration => ringDuration.Value;
        internal static float Volume => volume.Value;
        internal static float BellPitch => bellPitch.Value;
        internal static bool ControlsTooltip => controlsTooltip.Value;

        // KeyboardShortcut.IsPressed fails while any other key is down, which rules out
        // the modifier with R or while walking. Only the shortcut's own keys matter here.
        internal static bool ModifierHeld()
        {
            var shortcut = modifier.Value;
            if (shortcut.MainKey == KeyCode.None || !Input.GetKey(shortcut.MainKey)) return false;
            foreach (var key in shortcut.Modifiers)
                if (!Input.GetKey(key)) return false;
            return true;
        }

        internal static string ModifierName() => modifier.Value.ToString();

        private void Awake()
        {
            Log = Logger;
            modifier = Config.Bind("Controls", "Alarm modifier", new KeyboardShortcut(KeyCode.Z),
                "Hold this key while holding or looking at a chronometer. Scroll to set the alarm time and press R to switch the alarm on or off.");
            ringRange = Config.Bind("Alarm", "Ring range", 25f, new ConfigDescription(
                "How close the chronometer must be, in meters, for its alarm to ring and wake you.",
                new AcceptableValueRange<float>(5f, 100f)));
            ringDuration = Config.Bind("Alarm", "Ring duration", 5f, new ConfigDescription(
                "How many real seconds an alarm rings before it stops by itself.",
                new AcceptableValueRange<float>(1f, 60f)));
            volume = Config.Bind("Alarm", "Volume", 0.3f, new ConfigDescription(
                "Alarm bell volume.", new AcceptableValueRange<float>(0f, 1f)));
            bellPitch = Config.Bind("Alarm", "Bell pitch", 2.5f, new ConfigDescription(
                "How small the bell sounds. Higher values sound like a smaller, brighter bell. 1 is the game's original bell.",
                new AcceptableValueRange<float>(1f, 3f)));
            controlsTooltip = Config.Bind("Display", "Show controls tooltip", true,
                "While you hold the alarm modifier and look at a chronometer, show how to set the alarm below it. The alarm time is always shown.");
            try
            {
                harmony = new Harmony(Id);
                harmony.PatchAll(typeof(Plugin).Assembly);
                gameObject.AddComponent<AlarmScheduler>();
                Logger.LogInfo("Alarm Clock loaded.");
            }
            catch (Exception exception)
            {
                // A changed Sailwind method can fail after earlier patches were installed.
                enabled = false;
                try { harmony?.UnpatchSelf(); }
                catch (Exception unpatchException)
                {
                    Logger.LogError("Alarm Clock could not remove its partial patches: " + unpatchException);
                }
                Logger.LogError("Alarm Clock disabled after initialization failed. " +
                    "Check game and mod compatibility: " + exception);
            }
        }

        private static void Guard(string patch, Action action)
        {
            try { action(); }
            catch (Exception exception) { Log.LogError($"Alarm Clock {patch} failed: {exception}"); }
        }

        [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.OnLoad))]
        private static class AttachPatch
        {
            private static void Postfix(ShipItem __instance) => Guard("attach", () =>
            {
                if (__instance is ShipItemClock && __instance.GetComponent<ChronometerAlarm>() == null)
                    __instance.gameObject.AddComponent<ChronometerAlarm>();
            });
        }

        [HarmonyPatch(typeof(ShipItemClock), nameof(ShipItemClock.OnAltActivate), new Type[0])]
        private static class AltActivatePatch
        {
            private static bool Prefix(ShipItemClock __instance)
            {
                bool consumed = false;
                Guard("alt action", () =>
                {
                    var alarm = __instance.GetComponent<ChronometerAlarm>();
                    consumed = alarm != null && alarm.OnAltActivate();
                });
                return !consumed;
            }
        }

        // With empty hands the native raycast only targets nailed crates, bottles and beds,
        // so a nailed chronometer could not be looked at, set or stopped. Target it the
        // same way those items are, after the raycast has stored this frame's hit.
        private static class PointerAccess
        {
            public static readonly AccessTools.FieldRef<GoPointer, RaycastHit> Hit =
                AccessTools.FieldRefAccess<GoPointer, RaycastHit>("hit");
            public static readonly AccessTools.FieldRef<GoPointer, GoPointerButton> PointedAt =
                AccessTools.FieldRefAccess<GoPointer, GoPointerButton>("pointedAtButton");
            public static readonly AccessTools.FieldRef<GoPointer, float> LookDistance =
                AccessTools.FieldRefAccess<GoPointer, float>("currentLookDistance");
        }

        [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
        private static class NailedLookPatch
        {
            private static void Postfix(GoPointer __instance) => Guard("nailed look", () =>
            {
                if (__instance.type == GoPointer.PointerType.crosshairMouse && GameState.inCursorMenu) return;
                if (PointerAccess.PointedAt(__instance) != null || __instance.GetHeldItem() != null) return;
                if (GameState.sleeping || GameState.inBed != null || BoatCamera.on) return;
                var hit = PointerAccess.Hit(__instance);
                var collider = hit.collider;
                if (collider == null) return;
                if (collider.CompareTag("ItemSubcollider")) collider = collider.transform.parent.GetComponent<Collider>();
                var clock = collider != null ? collider.GetComponent<ShipItemClock>() : null;
                if (clock == null || !clock.nailed || clock.unclickable) return;
                PointerAccess.PointedAt(__instance) = clock;
                clock.Look(__instance);
                PointerAccess.LookDistance(__instance) = hit.distance;
            });
        }

        // Taking the wheel here, before GoPointer reads it, keeps native scroll
        // rotation and other held-item scroll handlers from also acting on it.
        [HarmonyPatch(typeof(GameInput), nameof(GameInput.GetScrollAxis))]
        private static class ScrollPatch
        {
            private static void Postfix(ref float __result)
            {
                if (ChronometerAlarm.EditingNow) __result = 0f;
            }
        }

        [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.UpdateLookText))]
        private static class LookTextPatch
        {
            private static void Postfix(ShipItem __instance)
            {
                if (!(__instance is ShipItemClock)) return;
                Guard("look text", () => __instance.GetComponent<ChronometerAlarm>()?.ApplyLookText());
            }
        }

        // A ringing chronometer shows "stop alarm" beside the native R icon, the same
        // way the game shows "pick up" beside the left-click icon. The private members
        // are resolved on first use, so a game change is caught by Guard instead of
        // failing the plugin before Awake.
        private static class LookUiAccess
        {
            public static readonly AccessTools.FieldRef<LookUI, TextMesh> ControlsText =
                AccessTools.FieldRefAccess<LookUI, TextMesh>("controlsText");
            // The line under the look text that shows the item's tooltip (its description).
            public static readonly AccessTools.FieldRef<LookUI, TextMesh> TooltipText =
                AccessTools.FieldRefAccess<LookUI, TextMesh>("hintText");
            public static readonly AccessTools.FieldRef<LookUI, GoPointer> Pointer =
                AccessTools.FieldRefAccess<LookUI, GoPointer>("pointer");
            public static readonly Action<LookUI> ShowRicon =
                AccessTools.MethodDelegate<Action<LookUI>>(AccessTools.Method(typeof(LookUI), "ShowRicon"));
            public static readonly Action<LookUI> HideIcons =
                AccessTools.MethodDelegate<Action<LookUI>>(AccessTools.Method(typeof(LookUI), "HideIcons"));
        }

        private const string StopPrompt = "stop alarm";

        private static bool Ringing(Component item)
        {
            var alarm = item != null ? item.GetComponent<ChronometerAlarm>() : null;
            return alarm != null && alarm.Ringer.IsRinging;
        }

        [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
        private static class LookPromptPatch
        {
            private static void Postfix(LookUI __instance, GoPointerButton button) => Guard("look prompt", () =>
            {
                if (!(button is ShipItemClock clock)) return;
                // Written directly every frame, so it appears with the modifier and native
                // code puts the chronometer's own tooltip back once it is released.
                string tooltip = clock.GetComponent<ChronometerAlarm>()?.Tooltip();
                if (tooltip != null) LookUiAccess.TooltipText(__instance).text = tooltip;
                if (!Settings.controlsTextEnabled) return;
                var text = LookUiAccess.ControlsText(__instance);
                // R reaches the looked-at clock only with empty hands. Otherwise it goes to the held item.
                var pointer = LookUiAccess.Pointer(__instance);
                bool emptyHands = pointer != null && pointer.GetHeldItem() == null;
                if (Ringing(button) && emptyHands)
                {
                    // A nailed clock gets no native prompt, so its first line would be stale.
                    string pickUp = clock.nailed ? "" : text.text.Split('\n')[0];
                    LookUiAccess.ShowRicon(__instance);
                    text.text = pickUp + "\n" + StopPrompt;
                }
                else if ((clock.nailed && emptyHands) || text.text.EndsWith("\n" + StopPrompt))
                {
                    // Native code leaves the previous object's text for a nailed clock,
                    // and our own prompt once the alarm stops.
                    LookUiAccess.HideIcons(__instance);
                    text.text = "";
                }
            });
        }

        [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowHoldText))]
        private static class HoldPromptPatch
        {
            private static void Postfix(LookUI __instance, PickupableItem item) => Guard("hold prompt", () =>
            {
                if (!Settings.controlsTextEnabled || !Ringing(item)) return;
                LookUiAccess.ShowRicon(__instance);
                LookUiAccess.ControlsText(__instance).text = StopPrompt;
            });
        }

        [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.SaveModData))]
        private static class SavePatch
        {
            private static void Postfix() => Guard("save", () =>
            {
                if (!Store.Writable) return;
                if (Store.Count > 0) GameState.modData[AlarmStore.Key] = Store.Serialize();
                else GameState.modData.Remove(AlarmStore.Key);
            });
        }

        [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadGame))]
        private static class LoadGamePatch
        {
            private static void Prefix() => Guard("load reset", () =>
            {
                ChronometerAlarm.StopAll();
                Store.Reset();
                // A save without mod data keeps the previous session's dictionary.
                GameState.modData.Remove(AlarmStore.Key);
            });
        }

        [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadModData))]
        private static class LoadModDataPatch
        {
            private static void Postfix() => Guard("load", () =>
            {
                GameState.modData.TryGetValue(AlarmStore.Key, out string json);
                if (!Store.Load(json))
                    Log.LogWarning("Saved alarms could not be read and were left unchanged in the save: " + Store.Problem);
            });
        }

        // GameState.modData is not cleared for a new game, so earlier alarms must not carry over.
        [HarmonyPatch(typeof(StartMenu), "StartNewGame")]
        private static class NewGamePatch
        {
            private static void Prefix() => Guard("new game reset", () =>
            {
                ChronometerAlarm.StopAll();
                Store.Reset();
                GameState.modData.Remove(AlarmStore.Key);
            });
        }
    }
}
