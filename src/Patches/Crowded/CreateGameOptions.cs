using AmongUs.GameOptions;
using TMPro;
using UnityEngine;

namespace TONX.Patches.Crowded;

internal static class GameOptionsExtension
{
    public const int VANILLA_MAX_PLAYERS = 15;
    public const int EXTENDED_MAX_PLAYERS = 127;
        
    public static int GetExtendedMaxPlayers(bool isVanillaServer)
    {
        return isVanillaServer ? VANILLA_MAX_PLAYERS : EXTENDED_MAX_PLAYERS;
    }
        
    public static int CalculateMaxImpostors(int playerCount)
    {
        return Mathf.Clamp(playerCount / 2, 1, playerCount - 1);
    }
}

[HarmonyPatch]
internal static class LobbyOptionsPatches
{
    private static CreateOptionsPicker _cachedOptionsPicker;

    [HarmonyPatch(typeof(CreateOptionsPicker), "Awake")]
    [HarmonyPrefix]
    private static void CacheOptionsPicker(CreateOptionsPicker __instance)
    {
        _cachedOptionsPicker = __instance;
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), "Awake")]
    [HarmonyPostfix]
    private static void ModifyPlayerCountUI(CreateOptionsPicker __instance)
    {
        if (__instance.mode != null) return;

        TransformPlayerCountButtons(__instance);
        AdjustImpostorCountControls(__instance);
    }

    private static void TransformPlayerCountButtons(CreateOptionsPicker picker)
    {
        var buttons = picker.MaxPlayerButtons;
        if (buttons == null || buttons.Count < 2) return;

        ReplaceEdgeButton(buttons[0], "-", (index, text) =>
        {
            for (int i = 1; i < 11; i++)
            {
                UpdateButtonValue(buttons[i], -10, i);
            }

            picker.UpdateMaxPlayersButtons(picker.GetTargetOptions());
        });

        ReplaceEdgeButton(buttons[buttons.Count - 1], "+", (index, text) =>
        {
            for (int i = 1; i < 11; i++)
            {
                UpdateButtonValue(buttons[i], 10, i);
            }

            picker.UpdateMaxPlayersButtons(picker.GetTargetOptions());
        });

        for (int i = 1; i < buttons.Count - 1; i++)
        {
            RebindPlayerButton(buttons[i], picker);
        }
    }

    private static void UpdateButtonValue(SpriteRenderer button, int delta, int buttonIndex)
    {
        var text = button.GetComponentInChildren<TextMeshPro>();
        if (text != null)
        {
            int current = int.Parse(text.text);
            int minValue = buttonIndex - 2;
            int maxValue = GameOptionsExtension.EXTENDED_MAX_PLAYERS - 14 + buttonIndex;
            text.text = Mathf.Clamp(current + delta, minValue, maxValue).ToString();
        }
    }

    private static void ReplaceEdgeButton(SpriteRenderer button, string label, Action<int, TextMeshPro> onClick)
    {
        button.GetComponentInChildren<TextMeshPro>().text = label;
        button.enabled = false;

        var passiveButton = button.GetComponent<PassiveButton>();
        passiveButton.OnClick.RemoveAllListeners();
        passiveButton.OnClick.AddListener((Action)(() => onClick?.Invoke(0, null)));

        GameObject.Destroy(button);
    }

    private static void RebindPlayerButton(SpriteRenderer button, CreateOptionsPicker picker)
    {
        var passiveButton = button.GetComponent<PassiveButton>();
        var text = button.GetComponentInChildren<TextMeshPro>();

        passiveButton.OnClick.RemoveAllListeners();
        passiveButton.OnClick.AddListener((Action)(() =>
        {
            int selectedPlayers = int.Parse(text.text);
            var options = picker.GetTargetOptions();

            int maxImpostors = GameOptionsExtension.CalculateMaxImpostors(selectedPlayers);
            int currentImpostors = Mathf.Min(options.NumImpostors, maxImpostors);

            options.SetInt(Int32OptionNames.NumImpostors, currentImpostors);
            UpdateImpostorDisplay(picker, currentImpostors);

            picker.SetMaxPlayersButtons(selectedPlayers);
        }));
    }

    private static void AdjustImpostorCountControls(CreateOptionsPicker picker)
    {
        var impostorButtons = picker.ImpostorButtons;
        if (impostorButtons == null || impostorButtons.Count < 3) return;

        var middleButton = impostorButtons[1];
        middleButton.SpriteRenderer.enabled = false;
        RemoveUIElements(middleButton.transform, "ConsoleHighlight");
        RemoveComponent<PassiveButton>(middleButton.gameObject);
        RemoveComponent<BoxCollider>(middleButton.gameObject);

        var displayText = middleButton.TextMesh;
        displayText.text = picker.GetTargetOptions().NumImpostors.ToString();

        var leftButton = impostorButtons[0];
        SetupImpostorAdjustButton(leftButton, "-", displayText, picker, -1);

        var rightButton = impostorButtons[2];
        SetupImpostorAdjustButton(rightButton, "+", displayText, picker, 1);
    }

    private static void SetupImpostorAdjustButton(ImpostorsOptionButton button, string label,
        TextMeshPro displayText, CreateOptionsPicker picker, int direction)
    {
        button.SpriteRenderer.enabled = false;
        button.TextMesh.text = label;

        var passiveButton = button.PassiveButton;
        passiveButton.OnClick.RemoveAllListeners();
        passiveButton.OnClick.AddListener((Action)(() =>
        {
            int current = int.Parse(displayText.text);
            int maxImpostors = picker.GetTargetOptions().MaxPlayers / 2;
            int newValue = Mathf.Clamp(current + direction, 1, maxImpostors);

            picker.SetImpostorButtons(newValue);
            displayText.text = newValue.ToString();
        }));
    }

    private static void UpdateImpostorDisplay(CreateOptionsPicker picker, int impostorCount)
    {
        if (picker.ImpostorButtons.Count > 1)
        {
            picker.ImpostorButtons[1].TextMesh.text = impostorCount.ToString();
        }
    }

    private static void RemoveUIElements(Transform parent, string childName)
    {
        var child = parent.Find(childName);
        if (child != null) GameObject.Destroy(child.gameObject);
    }

    private static void RemoveComponent<T>(GameObject obj) where T : Component
    {
        var component = obj.GetComponent<T>();
        if (component != null) GameObject.Destroy(component);
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), "UpdateMaxPlayersButtons")]
    [HarmonyPrefix]
    private static bool OverrideMaxPlayersDisplay(CreateOptionsPicker __instance, IGameOptions opts)
    {
        if (__instance.mode != null) return true;

        string currentMax = opts.MaxPlayers.ToString();
        var buttons = __instance.MaxPlayerButtons;

        for (int i = 1; i < buttons.Count - 1; i++)
        {
            buttons[i].enabled = buttons[i].GetComponentInChildren<TextMeshPro>().text == currentMax;
        }

        if (__instance.CrewArea != null)
        {
            __instance.CrewArea.SetCrewSize(opts.MaxPlayers, opts.NumImpostors);
        }

        return false;
    }

    [HarmonyPatch(typeof(NormalGameOptionsV07), nameof(NormalGameOptionsV07.AreInvalid))]
    [HarmonyPrefix]
    private static bool ValidateExtendedOptions(NormalGameOptionsV07 __instance, ref bool __result)
    {
        __result = __instance.NumImpostors < 0
                   || __instance.KillDistance < 0
                   || __instance.KillCooldown < 0f
                   || __instance.PlayerSpeedMod <= 0f;

        if (GameStates.IsVanillaServer && __instance.MaxPlayers > GameOptionsExtension.VANILLA_MAX_PLAYERS)
        {
            __result = true;
        }

        return false;
    }

    [HarmonyPatch(typeof(ServerManager), "SetRegion")]
    [HarmonyPostfix]
    private static void EnforceVanillaLimits(ServerManager __instance)
    {
        if (!GameStates.IsVanillaServer) return;

        var hostOptions = GameOptionsManager.Instance.GameHostOptions;
        if (hostOptions != null)
        {
            hostOptions.SetInt(Int32OptionNames.MaxPlayers,
                Mathf.Min(hostOptions.MaxPlayers, GameOptionsExtension.VANILLA_MAX_PLAYERS));
            hostOptions.SetInt(Int32OptionNames.NumImpostors,
                Mathf.Min(hostOptions.NumImpostors, 3));
        }

        if (_cachedOptionsPicker != null)
        {
            for (int i = 1; i < 11; i++)
            {
                var button = _cachedOptionsPicker.MaxPlayerButtons[i];
                var text = button.GetComponentInChildren<TextMeshPro>();
                if (text != null)
                {
                    int current = int.Parse(text.text);
                    int maxAllowed = GameOptionsExtension.EXTENDED_MAX_PLAYERS - 14 + i;
                    text.text = Mathf.Min(current + 10, maxAllowed).ToString();
                }
            }

            _cachedOptionsPicker.UpdateMaxPlayersButtons(_cachedOptionsPicker.GetTargetOptions());
        }
    }
}
    

    