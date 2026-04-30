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
        harmony.PatchAll(typeof(RightClickPatches));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }




    // [HarmonyPostfix, HarmonyPatch(typeof(SailPoser), "Init")]
    // static void DisableFov(SailPoser __instance) {
    //    __instance.disableFov = true;
    // }

    public static void LogInstructions(IEnumerable<CodeInstruction> instructions) {
        foreach (CodeInstruction instruction in instructions) {
            Logger.LogWarning($"{instruction.opcode} {instruction.operand}");
        }
    }
}

public class RightClickPatches {
    static Vector3 dragBeginMousePosition;

    static bool DragCanceledRtsCancel() {
        bool cameraConflict = !VFInput.override_keys[48].IsNull() && VFInput.override_keys[48].keyCode - 323 == 1;
        bool dragCanceledUp = VFInput.rtsCancel.onUp && (dragBeginMousePosition - Input.mousePosition).sqrMagnitude < 64f;

        return cameraConflict ? dragCanceledUp : VFInput.rtsCancel.onDown;
    }
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerController), "GameTick")]
    static void GameTick() {
        if (VFInput.rtsCancel.onDown) dragBeginMousePosition = Input.mousePosition;
    }
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlayerAction_Build), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_Click), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_Path), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_Addon), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_Inserter), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_Reform), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_Upgrade), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_Dismantle), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_BlueprintCopy), "EscLogic")]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), "EscLogic")]
    [HarmonyPatch(typeof(PlayerAction_Plant), "EscLogic")]
    [HarmonyPatch(typeof(PlayerAction_Combat), "GameTickShieldBurst")]
    static IEnumerable<CodeInstruction> OnDownPatch(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        matcher.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(VFInput), "get_rtsCancel")));
        matcher.SetOperandAndAdvance(SymbolExtensions.GetMethodInfo(() => DragCanceledRtsCancel()));
        matcher.RemoveInstruction();

        return matcher.InstructionEnumeration();
    }
    [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_BlueprintCopy), "Operating")]
    static IEnumerable<CodeInstruction> BlueprintPatch(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);

        // subtraction mode start
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(VFInput), "get_rtsCancel")),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(VFInput.InputValue), "onDown"))
        );
        matcher.SetOperandAndAdvance(SymbolExtensions.GetMethodInfo(() => DragCanceledRtsCancel()));
        matcher.RemoveInstruction();

        // addition mode cancel
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(VFInput), "get_rtsCancel")),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(VFInput.InputValue), "onUp"))
        );
        matcher.SetOperandAndAdvance(SymbolExtensions.GetMethodInfo(() => DragCanceledRtsCancel()));
        matcher.RemoveInstruction();

        // insert usemouseright so you cancel first selection without exiting
        matcher.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(BuildTool_BlueprintCopy), "DeterminePreviews")));
        matcher.Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VFInput), "UseMouseRight")));

        // subtraction mode cancel
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(VFInput), "get_rtsCancel")),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(VFInput.InputValue), "onDown"))
        );
        matcher.SetOperandAndAdvance(SymbolExtensions.GetMethodInfo(() => DragCanceledRtsCancel()));
        matcher.RemoveInstruction();
        
        return matcher.InstructionEnumeration();
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
