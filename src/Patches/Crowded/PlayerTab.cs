namespace TONX.Patches.Crowded;

public class PlayerTab
{
    [HarmonyPatch(typeof(global::PlayerTab))]
    internal static class PlayerTabPatches
    {
        [HarmonyPatch(nameof(global::PlayerTab.Update))]
        [HarmonyPostfix]
        private static void FixColorSelection(global::PlayerTab __instance)
        {
            if (GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers > GameOptionsExtension.VANILLA_MAX_PLAYERS)
            {
                __instance.currentColorIsEquipped = false;
            }
        }
        
        [HarmonyPatch(nameof(global::PlayerTab.UpdateAvailableColors))]
        [HarmonyPrefix]
        private static bool OverrideColorUpdate(global::PlayerTab __instance)
        {
            if (GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers <= GameOptionsExtension.VANILLA_MAX_PLAYERS)
                return true;
            
            __instance.AvailableColors.Clear();
            var localPlayerColor = PlayerControl.LocalPlayer?.CurrentOutfit.ColorId ?? -1;
            
            for (int i = 0; i < Palette.PlayerColors.Count; i++)
            {
                if (localPlayerColor != i)
                {
                    __instance.AvailableColors.Add(i);
                }
            }
            
            return false;
        }
    }
}