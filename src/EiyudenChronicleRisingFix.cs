using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace EiyudenChronicleRisingFix
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class ECRFix : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        public static ConfigEntry<bool> CustomResolution;
        public static ConfigEntry<float> DesiredResolutionX;
        public static ConfigEntry<float> DesiredResolutionY;
        public static ConfigEntry<bool> Fullscreen;
        public static ConfigEntry<bool> Letterboxing;
        public static ConfigEntry<bool> SkipIntroLogos;

        private void Awake()
        {
            Log = Logger;

            // Plugin startup logic
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            CustomResolution = Config.Bind("Set Custom Resolution",
                                "CustomResolution",
                                true,
                                "Enable the usage of a custom resolution.");

            DesiredResolutionX = Config.Bind("Set Custom Resolution",
                                "ResolutionWidth",
                                (float)Display.main.systemWidth, // Set default to display width so we don't leave an unsupported resolution as default
                                "Set desired resolution width.");

            DesiredResolutionY = Config.Bind("Set Custom Resolution",
                                "ResolutionHeight",
                                (float)Display.main.systemHeight, // Set default to display height so we don't leave an unsupported resolution as default
                                "Set desired resolution height.");

            Fullscreen = Config.Bind("Set Custom Resolution",
                                "Fullscreen",
                                 true,
                                "Set to true for fullscreen or false for windowed.");

            Letterboxing = Config.Bind("Ultrawide Fixes",
                               "DisableCutsceneLetterboxing",
                                true,
                               "Set to true for no letterboxing during cutscenes.");

            SkipIntroLogos = Config.Bind("General",
                                "SkipIntroLogos",
                                true,
                                "Set to true to skip the intro logos.");

            if (CustomResolution.Value)
            {
                Harmony.CreateAndPatchAll(typeof(UltrawidePatches));
            }

            if (Letterboxing.Value)
            {
                Harmony.CreateAndPatchAll(typeof(LetterboxPatch));
            }

            if (SkipIntroLogos.Value)
            {
                Harmony.CreateAndPatchAll(typeof(SkipIntroPatch));
            }
        }
    }

    [HarmonyPatch]
    public class UltrawidePatches
    {
        public static float DefaultAspectRatio = (float)1920 / 1080;
        public static float NewAspectRatio = ECRFix.DesiredResolutionX.Value / ECRFix.DesiredResolutionY.Value;
        public static float AspectMultiplier = NewAspectRatio / DefaultAspectRatio;

        // Set custom resolution
        [HarmonyPatch(typeof(GraphicSettings), "ApplyModifications")]
        [HarmonyPostfix]
        public static void FixRes()
        {
            Screen.SetResolution((int)ECRFix.DesiredResolutionX.Value, (int)ECRFix.DesiredResolutionY.Value, (bool)ECRFix.Fullscreen.Value);
            ECRFix.Log.LogInfo($"Changed resolution to {(int)ECRFix.DesiredResolutionX.Value} x {(int)ECRFix.DesiredResolutionY.Value}. Fullscreen = {(bool)ECRFix.Fullscreen.Value} ");
        }

        // Set screen match mode when object has canvasscaler enabled
        [HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
        [HarmonyPostfix]
        public static void SetScreenMatchMode(CanvasScaler __instance)
        {
            __instance.m_ScreenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        // Set SysBase screen width
        // Can't access it traditionally as it is a static constructor
        [HarmonyPatch(typeof(SysBase), "Start")]
        [HarmonyPostfix]
        public static void SetScreenWdith(SysBase __instance)
        {
            SysBase.SCR_W = (int)(AspectMultiplier * 1920);
        }

        // Fix horizontal position of many UI elements.
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(EventIcon), "DrawPos")]
        [HarmonyPatch(typeof(GateIcon), "DrawPos")]
        [HarmonyPatch(typeof(IconMat), "DrawPos")]
        [HarmonyPatch(typeof(IconQst), "DrawPos")]
        [HarmonyPatch(typeof(TalkIcon), "DrawPos")]
        [HarmonyPatch(typeof(TalkPlay), "_view_pos", new Type[] { typeof(Vector3), typeof(Vector3) })]
        public static IEnumerable<CodeInstruction> FixDrawPos(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                      .MatchForward(true,
                                    new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && ((float)i.operand == 1920))
                                    )
                      .SetOperandAndAdvance(1920 * AspectMultiplier)
                      .InstructionEnumeration();
        }
    }

    public class LetterboxPatch
    {
        // Disable cutscene letterboxing
        [HarmonyPatch(typeof(TalkPlay), "mask_in")]
        [HarmonyPostfix]
        public static void FixLetterboxing(TalkPlay __instance)
        {
            __instance.m_mask._disp_off();
            __instance.m_mask._disp_enable(false);
            ECRFix.Log.LogInfo($"Disabled cutscene letterboxing.");
        }
    }

    public class SkipIntroPatch
    {
        // Skip intro logos
        [HarmonyPatch(typeof(MenuLogo), "step_start")]
        [HarmonyPrefix]
        public static void SkipLogos(MenuLogo __instance)
        {
            __instance.m_logo._off(4);
            __instance.m_logo._off(0);
            __instance.m_drv = 0;
            __instance._step0(0, false);
            ECRFix.Log.LogInfo($"Intro logos skipped.");
            return;
        }
    }
}
