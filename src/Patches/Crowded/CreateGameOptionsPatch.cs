using AmongUs.GameOptions;
using HarmonyLib;
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

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.Awake))]
    internal static class CreateOptionsPickerAwakePatches
    {
        private static void CacheOptionsPicker(CreateOptionsPicker __instance)
        {
            if (__instance == null) return;
            _cachedOptionsPicker = __instance;
        }
        
        [HarmonyPrefix]
        private static void Prefix(CreateOptionsPicker __instance) => CacheOptionsPicker(__instance);
        
        [HarmonyPostfix]
        private static void Postfix(CreateOptionsPicker __instance)
        {
            if (__instance.mode != SettingsMode.Host) return;
            TransformPlayerCountButtons(__instance);
            AdjustImpostorCountControls(__instance);
        }

        private static void TransformPlayerCountButtons(CreateOptionsPicker picker)
        {
            var buttons = picker?.MaxPlayerButtons;
            if (buttons == null || buttons.Count < 2) return;
            
            ReplaceEdgeButton(buttons[0], "-", () =>
            {
                for (int i = 1; i < buttons.Count - 1; i++)
                {
                    UpdateButtonValue(buttons[i], -10, i);
                }
                picker.UpdateMaxPlayersButtons(picker.GetTargetOptions());
            });

            ReplaceEdgeButton(buttons[buttons.Count - 1], "+", () =>
            {
                for (int i = 1; i < buttons.Count - 1; i++)
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
            var text = button?.GetComponentInChildren<TextMeshPro>();
            if (text != null && int.TryParse(text.text, out int current))
            {
                int minValue = buttonIndex - 2;
                int maxValue = GameOptionsExtension.EXTENDED_MAX_PLAYERS - 14 + buttonIndex;
                text.text = Mathf.Clamp(current + delta, minValue, maxValue).ToString();
            }
        }

        private static void ReplaceEdgeButton(SpriteRenderer button, string label, System.Action onClick)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<TextMeshPro>();
            if (text != null) text.text = label;
            button.enabled = false;

            var passiveButton = button.GetComponent<PassiveButton>();
            if (passiveButton != null)
            {
                passiveButton.OnClick.RemoveAllListeners();
                passiveButton.OnClick.AddListener(onClick);
            }
            GameObject.Destroy(button);
        }

        private static void RebindPlayerButton(SpriteRenderer button, CreateOptionsPicker picker)
        {
            if (button == null || picker == null) return;
            var passiveButton = button.GetComponent<PassiveButton>();
            var text = button.GetComponentInChildren<TextMeshPro>();

            if (passiveButton == null || text == null) return;

            passiveButton.OnClick.RemoveAllListeners();
            passiveButton.OnClick.AddListener((Action)(() =>
            {
                if (!int.TryParse(text.text, out int selectedPlayers)) return;
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
            var impostorButtons = picker?.ImpostorButtons;
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
            if (button == null || displayText == null || picker == null) return;
            button.SpriteRenderer.enabled = false;
            button.TextMesh.text = label;

            var passiveButton = button.PassiveButton;
            if (passiveButton == null) return;

            passiveButton.OnClick.RemoveAllListeners();
            passiveButton.OnClick.AddListener((Action)(() =>
            {
                if (!int.TryParse(displayText.text, out int current)) return;
                var options = picker.GetTargetOptions();
                int maxImpostors = GameOptionsExtension.CalculateMaxImpostors(options.MaxPlayers);
                int newValue = Mathf.Clamp(current + direction, 1, maxImpostors);

                picker.SetImpostorButtons(newValue);
                displayText.text = newValue.ToString();
            }));
        }

        private static void UpdateImpostorDisplay(CreateOptionsPicker picker, int impostorCount)
        {
            if (picker?.ImpostorButtons?.Count > 1)
            {
                var text = picker.ImpostorButtons[1].TextMesh;
                if (text != null) text.text = impostorCount.ToString();
            }
        }

        private static void RemoveUIElements(Transform parent, string childName)
        {
            var child = parent?.Find(childName);
            if (child != null) GameObject.Destroy(child.gameObject);
        }

        private static void RemoveComponent<T>(GameObject obj) where T : Component
        {
            var component = obj?.GetComponent<T>();
            if (component != null) GameObject.Destroy(component);
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.UpdateMaxPlayersButtons))]
    [HarmonyPrefix]
    private static bool OverrideMaxPlayersDisplay(CreateOptionsPicker __instance, IGameOptions opts)
    {
        if (__instance.mode != SettingsMode.Host) return true;

        string currentMax = opts?.MaxPlayers.ToString() ?? "10";
        var buttons = __instance.MaxPlayerButtons;

        for (int i = 1; i < buttons.Count - 1; i++)
        {
            var btn = buttons[i];
            var text = btn?.GetComponentInChildren<TextMeshPro>();
            btn.enabled = (text != null && text.text == currentMax);
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
        __result = __instance.NumImpostors < 1
                   || __instance.KillDistance < 0
                   || __instance.KillCooldown < 0f
                   || __instance.PlayerSpeedMod <= 0f;
        
        if (GameStates.IsVanillaServer && __instance.MaxPlayers > GameOptionsExtension.VANILLA_MAX_PLAYERS)
        {
            __result = true;
        }
        if (__instance.NumImpostors + 1 > __instance.MaxPlayers / 2)
        {
            __result = true;
        }

        return false;
    }

    [HarmonyPatch(typeof(ServerManager), nameof(ServerManager.SetRegion))]
    [HarmonyPostfix]
    private static void EnforceVanillaLimits(ServerManager __instance)
    {
        if (!GameStates.IsVanillaServer) return;

        var hostOptions = GameOptionsManager.Instance?.GameHostOptions;
        if (hostOptions != null)
        {
            hostOptions.SetInt(Int32OptionNames.MaxPlayers,
                Mathf.Min(hostOptions.MaxPlayers, GameOptionsExtension.VANILLA_MAX_PLAYERS));
            hostOptions.SetInt(Int32OptionNames.NumImpostors,
                Mathf.Min(hostOptions.NumImpostors, 3));
        }

        if (_cachedOptionsPicker != null)
        {
            var buttons = _cachedOptionsPicker.MaxPlayerButtons;
            for (int i = 1; i < buttons.Count - 1; i++)
            {
                var button = buttons[i];
                var text = button?.GetComponentInChildren<TextMeshPro>();
                if (text != null && int.TryParse(text.text, out int current))
                {
                    int maxAllowed = GameOptionsExtension.EXTENDED_MAX_PLAYERS - 14 + i;
                    text.text = Mathf.Min(current, maxAllowed).ToString(); // 移除错误的 +10
                }
            }
            _cachedOptionsPicker.UpdateMaxPlayersButtons(_cachedOptionsPicker.GetTargetOptions());
        }
    }
}