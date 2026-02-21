using HarmonyLib;
using System;
using TMPro;
using UnityEngine;
using SaberFactory2.UI.Lib;

namespace SaberFactory2.Core
{
    [HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
    internal class TMProFixerUI
    {
        static void Postfix(TextMeshProUGUI __instance)
        {
            if (__instance == null) return;
            if (__instance.font == null || __instance.fontSharedMaterial == null)
            {
                UITemplateCache.AssignValidFont(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(TextMeshPro), "OnEnable")]
    internal class TMProFixer3D
    {
        static void Postfix(TextMeshPro __instance)
        {
            if (__instance == null) return;
            UITemplateCache.AssignValidFont(__instance);
        }
    }

    [HarmonyPatch(typeof(TextMeshPro), "OnPreRenderObject")]
    internal class TMProFixerPreRender
    {
        static bool Prefix(TextMeshPro __instance)
        {
            if (__instance == null) return false;

            if (__instance.font == null || __instance.fontSharedMaterial == null)
            {
                UITemplateCache.AssignValidFont(__instance);
            }

            if (__instance.font == null || __instance.fontSharedMaterial == null) return false;

            return true;
        }

        static Exception Finalizer(Exception __exception, TextMeshPro __instance)
        {
            if (__exception != null)
            {
                if (__instance != null)
                {
                    __instance.enabled = false;
                }
                return null;
            }
            return __exception;
        }
    }
}