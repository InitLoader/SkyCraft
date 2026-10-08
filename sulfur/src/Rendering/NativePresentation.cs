using HarmonyLib;
using PerfectRandom.Sulfur.Gameplay;
using PerfectRandom.Sulfur.Core.Units;
using SulfurCraft.Game;

namespace SulfurCraft.Rendering
{
    [HarmonyPatch(typeof(PlayerHUD), nameof(PlayerHUD.ManualUpdate))]
    internal static class NativePresentation
    {
        public static PlayerBridge Player;

        private static void Postfix(PlayerHUD __instance)
        {
            if (Player != null && Player.Active && __instance.player == Player.Player && __instance.canvasGroup != null)
                __instance.canvasGroup.alpha = 0;
        }
    }

    [HarmonyPatch(typeof(Player), "OnBeginCameraRendering")]
    internal static class NativeCameraPresentation
    {
        private static void Postfix(UnityEngine.Camera camera) => NativePresentation.Player?.PrepareCamera(camera);
    }
}
