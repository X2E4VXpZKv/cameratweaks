using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection.Emit;
using UnityEngine;

namespace cameratweaks;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("DSPGAME.exe")]
public class Plugin : BaseUnityPlugin {
    public static new ManualLogSource Logger;
        
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        harmony.PatchAll(typeof(AccelPatches));
        harmony.PatchAll(typeof(SmoothingPatches));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }




    // [HarmonyPostfix, HarmonyPatch(typeof(SailPoser), "Init")]
    // static void DisableFov(SailPoser __instance) {
    //    __instance.disableFov = true;
    // }

    static void LogInstructions(IEnumerable<CodeInstruction> instructions) {
        foreach (CodeInstruction instruction in instructions) {
            Logger.LogWarning($"{instruction.opcode} {instruction.operand}");
        }
    }
}

public class AccelPatches {

    [HarmonyTranspiler, HarmonyPatch(typeof(SailPoser), "Calculate")]
    static IEnumerable<CodeInstruction> RemoveAccelFromSail(IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldc_R4, 0.2f),
            new CodeMatch(OpCodes.Bge_Un)
        )
        .Repeat( matcher => {
            matcher
                .Advance(-1)
                .RemoveInstructions(2)
                .SetOpcodeAndAdvance(OpCodes.Br_S);
            }
        );
        return matcher.InstructionEnumeration();
    }
}

public class SmoothingPatches {

    [HarmonyTranspiler, HarmonyPatch(typeof(RTSPoser), "Calculate")]
    static IEnumerable<CodeInstruction> RemoveSmoothingFromRTS(IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Lerp), "TweenAngle"))
        );
        if (matcher.IsValid) {
            matcher
                .Advance(-8)
                .RemoveInstructions(2)
                .Advance(2)
                .RemoveInstructions(5);
        }

        matcher.End();
        matcher.MatchBack(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Lerp), "Tween", [typeof(float), typeof(float), typeof(float)]))
        );

        if (matcher.IsValid) {
            matcher
                .Advance(-8)
                .RemoveInstructions(2)
                .Advance(2)
                .RemoveInstructions(5);
        }

        return matcher.InstructionEnumeration();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(PlanetPoser), "Calculate")]
    static IEnumerable<CodeInstruction> RemoveSmoothingFromPlanet(IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Lerp), "Tween", [typeof(Quaternion), typeof(Quaternion), typeof(float)]))
        );
        if (matcher.IsValid) {
            matcher
                .Advance(-8)
                .RemoveInstructions(2)
                .Advance(2)
                .RemoveInstructions(5);
        }

        return matcher.InstructionEnumeration();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(UIStarmap), "_OnUpdate")]
    static IEnumerable<CodeInstruction> RemoveSmoothingFromStarmap(IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Lerp), "Tween", [typeof(Quaternion), typeof(Quaternion), typeof(float)]))
        );
        if (matcher.IsValid) {
            matcher
                .Advance(-6)
                .RemoveInstructions(2)
                .Advance(2)
                .RemoveInstructions(3);
        }

        return matcher.InstructionEnumeration();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(GraticulePoser), "Calculate")]
    static IEnumerable<CodeInstruction> RemoveSmoothingFromBlueprint(IEnumerable<CodeInstruction> instructions) {
       var matcher = new CodeMatcher(instructions);

       for (var i = 2; i > 0; i--) {
           matcher.MatchForward(false,
               new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Lerp), "Tween", [typeof(float), typeof(float), typeof(float)]))
           );
           if (matcher.IsValid) {
               matcher
                   .Advance(-8)
                   .RemoveInstructions(2)
                   .Advance(2)
                   .RemoveInstructions(5);
           }
       }

       return matcher.InstructionEnumeration();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(MilkyWayCamera), "CameraControl")]
    static IEnumerable<CodeInstruction> RemoveSmoothingFromMilkyway(IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Lerp), "TweenAngle"))
        ).Repeat( matcher => {
            matcher
                .Advance(-6)
                .RemoveInstructions(2)
                .Advance(2)
                .RemoveInstructions(3);
            }
        );

        return matcher.InstructionEnumeration();
    }


    [HarmonyTranspiler, HarmonyPatch(typeof(SailPoser), "Calculate")]
    static IEnumerable<CodeInstruction> RemoveSmoothingFromSail(IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Lerp), "Tween", [typeof(Quaternion), typeof(Quaternion), typeof(float)]))
        );

        if (matcher.IsValid) {
            matcher
                .Advance(-6)
                .RemoveInstructions(2)
                .Advance(2)
                .RemoveInstructions(3);
        }

        return matcher.InstructionEnumeration();
    }
}
