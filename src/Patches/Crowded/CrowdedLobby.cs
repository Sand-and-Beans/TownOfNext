using AmongUs.GameOptions;
using UnityEngine;

namespace TONX.Patches.Crowded;

[HarmonyPatch]
internal static class LobbyCreationPatches
{
    [HarmonyPatch(typeof(PSManager), nameof(PSManager.CreateGame))]
    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.ContinueStart))]
    [HarmonyPrefix]
    private static void ValidateBeforeHost()
    {
            
        if (GameStates.IsVanillaServer && !GameStates.IsLocalGame)
        {
            var hostOptions = GameOptionsManager.Instance.GameHostOptions;
            if (hostOptions != null)
            {
                hostOptions.SetInt(Int32OptionNames.MaxPlayers, 
                    Mathf.Min(hostOptions.MaxPlayers, 15));
                hostOptions.SetInt(Int32OptionNames.NumImpostors, 
                    Mathf.Min(hostOptions.NumImpostors, 3));
            }
        }
    }
}