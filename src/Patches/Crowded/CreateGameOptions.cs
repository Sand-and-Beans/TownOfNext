using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;

namespace TONX.Patches.Crowded;

internal static class LobbyOptimizer
{
    private static CreateOptionsPicker _optionsPickerInstance;
    
    public const int EXPANDED_MAX_PLAYERS = 127;
    public const int STANDARD_MAX_PLAYERS = 15;
    public const int EXPANDED_MAX_IMPOSTORS = 63;
    public const int STANDARD_MAX_IMPOSTORS = 3;
    
    private static bool IsExtendedMode => !ServerAddManager.;
    public static int CurrentMaxPlayers => IsExtendedMode ? EXPANDED_MAX_PLAYERS : STANDARD_MAX_PLAYERS;
    public static int CurrentMaxImpostors => IsExtendedMode ? EXPANDED_MAX_IMPOSTORS : STANDARD_MAX_IMPOSTORS;

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.Awake))]
    private static class OptionsPickerInitializer
    {
        private static void Prefix(CreateOptionsPicker __instance)
        {
            _optionsPickerInstance = __instance;
        }
        
        private static void Postfix(CreateOptionsPicker __instance)
        {
            if (__instance.mode != SettingsMode.Host) return;
            
            InitializePlayerCountControls(__instance);
            InitializeImpostorCountControls(__instance);
        }
        
        private static void InitializePlayerCountControls(CreateOptionsPicker picker)
        {
            var playerButtons = picker.MaxPlayerButtons;
            if (playerButtons.Count < 2) return;
            
            ConfigureRangeAdjustmentButton(playerButtons[0], -10, picker);
            ConfigureRangeAdjustmentButton(playerButtons[1](@ref), 10, picker);
            
            for (int i = 1; i < playerButtons.Count - 1; i++)
            {
                ConfigurePlayerCountButton(playerButtons[i], picker);
            }
        }
        
        private static void ConfigureRangeAdjustmentButton(SpriteRenderer button, int adjustment, CreateOptionsPicker picker)
        {
            var textComponent = button.GetComponentInChildren<TextMeshPro>();
            textComponent.text = adjustment > 0 ? "+" : "-";
            button.enabled = false;
            
            var buttonComponent = button.GetComponent<PassiveButton>();
            buttonComponent.OnClick.RemoveAllListeners();
            buttonComponent.OnClick.AddListener((Action)( =>
            {
                UpdateAllPlayerButtons(picker, adjustment);
                picker.UpdateMaxPlayersButtons(picker.GetTargetOptions());
            });
        }
        
        private static void UpdateAllPlayerButtons(CreateOptionsPicker picker, int adjustment)
        {
            for (int i = 1; i < picker.MaxPlayerButtons.Count - 1; i++)
            {
                var button = picker.MaxPlayerButtons[i];
                var textDisplay = button.GetComponentInChildren<TextMeshPro>();
                int currentValue = int.Parse(textDisplay.text);
                int baseValue = int.Parse(button.name);
                
                int newValue = adjustment > 0 
                    ? Math.Min(currentValue + 10, CurrentMaxPlayers - 14 + baseValue)
                    : Math.Max(currentValue - 10, baseValue - 2);
                    
                textDisplay.text = newValue.ToString();
            }
        }
        
        private static void ConfigurePlayerCountButton(SpriteRenderer button, CreateOptionsPicker picker)
        {
            var buttonComponent = button.GetComponent<PassiveButton>();
            var textDisplay = button.GetComponentInChildren<TextMeshPro>();
            
            buttonComponent.OnClick.RemoveAllListeners();
            buttonComponent.OnClick.AddListener(() =>
            {
                int selectedPlayers = int.Parse(textDisplay.text);
                UpdateGameSettingsForPlayerCount(picker, selectedPlayers);
            });
        }
        
        private static void UpdateGameSettingsForPlayerCount(CreateOptionsPicker picker, int playerCount)
        {
            var options = picker.GetTargetOptions();
            int maxImpostors = Math.Min(options.NumImpostors, playerCount / 2);
            
            options.SetInt(Int32OptionNames.NumImpostors, maxImpostors);
            picker.ImpostorButtons[1].TextMesh.text = maxImpostors.ToString();
            picker.SetMaxPlayersButtons(playerCount);
        }
        
        private static void InitializeImpostorCountControls(CreateOptionsPicker picker)
        {
            var impostorButtons = picker.ImpostorButtons;
            if (impostorButtons.Count < 3) return;
            
            var centerButton = impostorButtons[1];
            var centerText = centerButton.TextMesh;
            
            ConfigureIncrementButton(impostorButtons[0], -1, centerText, picker);
            ConfigureIncrementButton(impostorButtons[2], 1, centerText, picker);
            
            CleanupButtonComponents(centerButton);
        }
        
        private static void ConfigureIncrementButton(OptionsMenuButton button, int increment, TextMeshPro valueDisplay, CreateOptionsPicker picker)
        {
            button.SpriteRenderer.enabled = false;
            button.TextMesh.text = increment > 0 ? "+" : "-";
            
            var buttonComponent = button.PassiveButton;
            buttonComponent.OnClick.RemoveAllListeners();
            buttonComponent.OnClick.AddListener(() =>
            {
                int currentValue = int.Parse(valueDisplay.text);
                int newValue = Math.Clamp(currentValue + increment, 1, picker.GetTargetOptions().MaxPlayers / 2);
                
                picker.SetImpostorButtons(newValue);
                valueDisplay.text = newValue.ToString();
            });
        }
        
        private static void CleanupButtonComponents(OptionsMenuButton button)
        {
            var highlight = button.transform.FindChild("ConsoleHighlight");
            if (highlight != null) UnityEngine.Object.Destroy(highlight.gameObject);
            
            UnityEngine.Object.Destroy(button.PassiveButton);
            UnityEngine.Object.Destroy(button.BoxCollider);
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.Refresh))]
    private static class OptionsDisplayUpdater
    {
        private static bool Prefix(CreateOptionsPicker __instance)
        {
            var currentOptions = __instance.GetTargetOptions();
            
            __instance.UpdateImpostorsButtons(currentOptions.NumImpostors);
            __instance.UpdateMaxPlayersButtons(currentOptions);
            __instance.UpdateLanguageButton((uint)currentOptions.Keywords);
            
            if (__instance.MapMenu != null)
            {
                __instance.MapMenu.UpdateMapButtons((int)currentOptions.MapId);
            }
            
            var gameModeText = GameOptionsManager.Instance.CurrentGameOptions.GameMode;
            var modeName = GameModesHelpers.ModeToName.GetValueOrDefault(gameModeText, "Unknown");
            __instance.GameModeText.text = DestroyableSingleton<TranslationController>.Instance.GetString(modeName);
            
            return false;
        }
    }

    [HarmonyPatch(typeof(ServerManager), nameof(ServerManager.SetRegion))]
    private static class ServerRegionValidator
    {
        private static void Postfix()
        {
            if (!GameStates.IsVanillaServer) return;
            
            ValidateVanillaServerSettings();
            RefreshOptionsDisplay();
        }
        
        private static void ValidateVanillaServerSettings()
        {
            var hostOptions = GameOptionsManager.Instance.GameHostOptions;
            if (hostOptions == null) return;
            
            if (hostOptions.MaxPlayers > STANDARD_MAX_PLAYERS)
            {
                hostOptions.SetInt(Int32OptionNames.MaxPlayers, STANDARD_MAX_PLAYERS);
            }
            
            if (hostOptions.NumImpostors > STANDARD_MAX_IMPOSTORS)
            {
                hostOptions.SetInt(Int32OptionNames.NumImpostors, STANDARD_MAX_IMPOSTORS);
            }
        }
        
        private static void RefreshOptionsDisplay()
        {
            if (_optionsPickerInstance == null) return;
            
            for (int i = 1; i < _optionsPickerInstance.MaxPlayerButtons.Count - 1; i++)
            {
                var button = _optionsPickerInstance.MaxPlayerButtons[i];
                var textDisplay = button.GetComponentInChildren<TextMeshPro>();
                int baseValue = int.Parse(button.name);
                
                int adjustedValue = Math.Min(
                    int.Parse(textDisplay.text) + 10,
                    CurrentMaxPlayers - 14 + baseValue
                );
                
                textDisplay.text = adjustedValue.ToString();
            }
            
            _optionsPickerInstance.UpdateMaxPlayersButtons(_optionsPickerInstance.GetTargetOptions());
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.UpdateMaxPlayersButtons))]
    private static class PlayerCountButtonUpdater
    {
        private static bool Prefix(CreateOptionsPicker __instance, IGameOptions options)
        {
            if (__instance.mode != SettingsMode.Host) return true;
            
            if (__instance.CrewArea != null)
            {
                __instance.CrewArea.SetCrewSize(options.MaxPlayers, options.NumImpostors);
            }
            
            string selectedValue = options.MaxPlayers.ToString();
            for (int i = 1; i < __instance.MaxPlayerButtons.Count - 1; i++)
            {
                var button = __instance.MaxPlayerButtons[i];
                var buttonText = button.GetComponentInChildren<TextMeshPro>().text;
                button.enabled = buttonText == selectedValue;
            }
            
            return false;
        }
    }

    [HarmonyPatch(typeof(NormalGameOptionsV10), nameof(NormalGameOptionsV10.AreInvalid))]
    private static class GameOptionsValidator
    {
        private static bool Prefix(NormalGameOptionsV10 __instance, ref bool __result)
        {
            __result = __instance.NumImpostors < 0 
                || __instance.KillDistance < 0 
                || __instance.KillCooldown < 0 
                || __instance.PlayerSpeedMod <= 0;
            
            if (GameStates.IsVanillaServer && __instance.MaxPlayers > STANDARD_MAX_PLAYERS)
            {
                __result = true;
            }
            
            return false;
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.SetImpostorButtons))]
    private static class ImpostorCountSetter
    {
        private static bool Prefix(CreateOptionsPicker __instance, int impostorCount)
        {
            if (__instance.mode != SettingsMode.Host) return true;
            
            var currentOptions = __instance.GetTargetOptions();
            currentOptions.SetInt(Int32OptionNames.NumImpostors, impostorCount);
            
            __instance.SetTargetOptions(currentOptions);
            __instance.UpdateImpostorsButtons(impostorCount);
            
            return false;
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.SetMaxPlayersButtons))]
    private static class PlayerCountSetter
    {
        private static bool Prefix(CreateOptionsPicker __instance, int playerCount)
        {
            if (DestroyableSingleton<FindAGameManager>.InstanceExists || __instance.mode != SettingsMode.Host)
            {
                return true;
            }
            
            var currentOptions = __instance.GetTargetOptions();
            currentOptions.SetInt(Int32OptionNames.MaxPlayers, playerCount);
            
            __instance.SetTargetOptions(currentOptions);
            __instance.UpdateMaxPlayersButtons(currentOptions);
            
            return false;
        }
    }

    [HarmonyPatch(typeof(SecurityLogger), nameof(SecurityLogger.Awake))]
    private static class SecurityLoggerExpander
    {
        private static void Postfix(ref SecurityLogger __instance)
        {
            __instance.Timers = new Il2CppStructArray<float>(EXPANDED_MAX_PLAYERS);
        }
    }

    [HarmonyPatch(typeof(PlayerTab), nameof(PlayerTab.UpdateAvailableColors))]
    private static class ColorSelectionHandler
    {
        private static bool Prefix(PlayerTab __instance)
        {
            if (GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers <= STANDARD_MAX_PLAYERS)
            {
                return true;
            }
            
            __instance.AvailableColors.Clear();
            var localPlayer = PlayerControl.LocalPlayer;
            int currentColorId = localPlayer?.CurrentOutfit.ColorId ?? -1;
            
            for (int i = 0; i < Palette.PlayerColors.Count; i++)
            {
                if (currentColorId != i)
                {
                    __instance.AvailableColors.Add(i);
                }
            }
            
            return false;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPatch(typeof(ShapeshifterMinigame), nameof(ShapeshifterMinigame.Begin))]
    [HarmonyPatch(typeof(VitalsMinigame), nameof(VitalsMinigame.Begin))]
    private static class UIPaginationInitializer
    {
        private static void Postfix(MonoBehaviour instance)
        {
            switch (instance)
            {
                case MeetingHud meetingHud:
                    meetingHud.gameObject.AddComponent<MeetingPagination>().Initialize(meetingHud);
                    break;
                    
                case ShapeshifterMinigame shapeshifter:
                    shapeshifter.gameObject.AddComponent<ShapeshifterPagination>().Initialize(shapeshifter);
                    break;
                    
                case VitalsMinigame vitals:
                    vitals.gameObject.AddComponent<VitalsPagination>().Initialize(vitals);
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(PSManager), nameof(PSManager.CreateGame))]
    [HarmonyPatch(typeof(CreateGameOptions), nameof(CreateGameOptions.ContinueStart))]
    private static class GameSessionValidator
    {
        private static void Prefix()
        {
            if (!GameStates.IsVanillaServer || GameStates.IsLocalGame) return;
            
            var hostOptions = GameOptionsManager.Instance.GameHostOptions;
            if (hostOptions == null) return;
            
            if (hostOptions.MaxPlayers > STANDARD_MAX_PLAYERS)
            {
                hostOptions.SetInt(Int32OptionNames.MaxPlayers, STANDARD_MAX_PLAYERS);
            }
            
            if (hostOptions.NumImpostors > STANDARD_MAX_IMPOSTORS)
            {
                hostOptions.SetInt(Int32OptionNames.NumImpostors, STANDARD_MAX_IMPOSTORS);
            }
        }
    }
}

public abstract class PaginatedUI : MonoBehaviour
{
    protected const int DEFAULT_ITEMS_PER_PAGE = 15;
    protected const string PAGE_INDICATOR_NAME = "UI_PageIndicator";
    
    protected int _currentPage;
    
    protected virtual int ItemsPerPage => DEFAULT_ITEMS_PER_PAGE;
    protected abstract int TotalPages { get; }
    protected abstract int ItemCount { get; }
    
    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            _currentPage = Math.Clamp(value, 0, TotalPages - 1);
            UpdatePageDisplay();
        }
    }
    
    protected abstract void UpdatePageDisplay();
    
    protected virtual void Update()
    {
        if (HudManager.Instance?.Chat?.IsOpenOrOpening == true) return;
        
        float scrollInput = Input.mouseScrollDelta.y;
        bool navigateUp = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || scrollInput > 0;
        bool navigateDown = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow) || scrollInput < 0;
        
        if (navigateUp) NavigatePage(-1);
        else if (navigateDown) NavigatePage(1);
    }
    
    protected void NavigatePage(int direction)
    {
        int newPage = _currentPage + direction;
        
        if (newPage < 0) newPage = TotalPages - 1;
        else if (newPage >= TotalPages) newPage = 0;
        
        CurrentPage = newPage;
    }
    
    protected TextMeshPro CreatePageIndicator(Transform parent, Vector3 position, float scale = 0.5f)
    {
        var indicator = Instantiate(HudManager.Instance.KillButton.cooldownTimerText, parent);
        indicator.name = PAGE_INDICATOR_NAME;
        indicator.enableWordWrapping = false;
        indicator.transform.localPosition = position;
        indicator.transform.localScale = Vector3.one * scale;
        
        return indicator;
    }
}

public class MeetingPagination : PaginatedUI
{
    private MeetingHud _meetingHud;
    private int? _cachedPageCount;
    
    protected override int ItemCount => _meetingHud.playerStates.Count;
    protected override int TotalPages => _cachedPageCount ??= CalculateTotalPages();
    
    public void Initialize(MeetingHud meetingHud)
    {
        _meetingHud = meetingHud;
        UpdatePageDisplay();
    }
    
    private int CalculateTotalPages()
    {
        return Math.Max(0, (ItemCount - 1) / ItemsPerPage);
    }
    
    protected override void UpdatePageDisplay()
    {
        if (_meetingHud == null) return;
        
        UpdatePlayerPositions();
        UpdateTimerDisplay();
    }
    
    private void UpdatePlayerPositions()
    {
        var activePlayers = _meetingHud.playerStates
            .OrderBy(p => p.AmDead)
            .ToList();
        
        for (int i = 0; i < activePlayers.Count; i++)
        {
            var playerArea = activePlayers[i];
            bool shouldDisplay = i >= CurrentPage * ItemsPerPage && i < (CurrentPage + 1) * ItemsPerPage;
            
            playerArea.gameObject.SetActive(shouldDisplay);
            
            if (shouldDisplay)
            {
                int relativeIndex = i % ItemsPerPage;
                Vector3 newPosition = CalculatePlayerPosition(relativeIndex);
                playerArea.transform.localPosition = newPosition;
            }
        }
    }
    
    private Vector3 CalculatePlayerPosition(int index)
    {
        int row = index / 3;
        int column = index % 3;
        
        return _meetingHud.VoteOrigin + new Vector3(
            _meetingHud.VoteButtonOffsets.x * column,
            _meetingHud.VoteButtonOffsets.y * row,
            0
        );
    }
    
    private void UpdateTimerDisplay()
    {
        if (_meetingHud.TimerText.text.Contains($" ({CurrentPage + 1}/{TotalPages + 1})"))
            return;
            
        _meetingHud.TimerText.text += $" ({CurrentPage + 1}/{TotalPages + 1})";
    }
}

public class ShapeshifterPagination : PaginatedUI
{
    private ShapeshifterMinigame _minigame;
    private TextMeshPro _pageIndicator;
    
    protected override int ItemCount => _minigame.potentialVictims.Count;
    protected override int TotalPages => Math.Max(0, (ItemCount - 1) / ItemsPerPage);
    
    public void Initialize(ShapeshifterMinigame minigame)
    {
        _minigame = minigame;
        _pageIndicator = CreatePageIndicator(
            minigame.transform,
            new Vector3(4.1f, -2.36f, -1f)
        );
        
        UpdatePageDisplay();
    }
    
    protected override void UpdatePageDisplay()
    {
        if (_minigame == null || _pageIndicator == null) return;
        
        _pageIndicator.text = $"({CurrentPage + 1}/{TotalPages + 1})";
        
        var panels = _minigame.potentialVictims.ToArray();
        for (int i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            bool shouldDisplay = i >= CurrentPage * ItemsPerPage && i < (CurrentPage + 1) * ItemsPerPage;
            
            panel.gameObject.SetActive(shouldDisplay);
            
            if (shouldDisplay)
            {
                int relativeIndex = i % ItemsPerPage;
                panel.transform.localPosition = CalculatePanelPosition(relativeIndex);
            }
        }
    }
    
    private Vector3 CalculatePanelPosition(int index)
    {
        int row = index / 3;
        int column = index % 3;
        
        return new Vector3(
            _minigame.XStart + _minigame.XOffset * column,
            _minigame.YStart + _minigame.YOffset * row,
            0
        );
    }
}

public class VitalsPagination : PaginatedUI
{
    private VitalsMinigame _minigame;
    private TextMeshPro _pageIndicator;
    
    protected override int ItemCount => _minigame.vitals.Count;
    protected override int TotalPages => Math.Max(0, (ItemCount - 1) / ItemsPerPage);
    
    public void Initialize(VitalsMinigame minigame)
    {
        _minigame = minigame;
        _pageIndicator = CreatePageIndicator(
            minigame.transform,
            new Vector3(2.7f, -2f, -1f)
        );
        
        UpdatePageDisplay();
    }
    
    protected override void UpdatePageDisplay()
    {
        if (_minigame == null || _pageIndicator == null) return;
        if (PlayerTask.PlayerHasTaskOfType<HudOverrideTask>(PlayerControl.LocalPlayer)) return;
        
        _pageIndicator.text = $"({CurrentPage + 1}/{TotalPages + 1})";
        
        var panels = _minigame.vitals.ToArray();
        for (int i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            bool shouldDisplay = i >= CurrentPage * ItemsPerPage && i < (CurrentPage + 1) * ItemsPerPage;
            
            panel.gameObject.SetActive(shouldDisplay);
            
            if (shouldDisplay)
            {
                int relativeIndex = i % ItemsPerPage;
                panel.transform.localPosition = CalculatePanelPosition(relativeIndex);
            }
        }
    }
    
    private Vector3 CalculatePanelPosition(int index)
    {
        int row = index / 3;
        int column = index % 3;
        
        return new Vector3(
            _minigame.XStart + _minigame.XOffset * column,
            _minigame.YStart + _minigame.YOffset * row,
            0
        );
    }
}