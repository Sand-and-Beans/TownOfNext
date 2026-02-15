using UnityEngine;
using TMPro;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TONX.Patches.Crowded;

[HarmonyPatch]
internal static class MinigamePaginationPatches
{
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    private static void AddMeetingPagination(MeetingHud __instance)
    {
        if (__instance == null) return;
        if (GameOptionsManager.Instance?.CurrentGameOptions?.MaxPlayers <= GameOptionsExtension.VANILLA_MAX_PLAYERS) return;
            
        var existing = __instance.gameObject.GetComponent<MeetingUIPaginator>();
        if (existing != null) GameObject.Destroy(existing);
            
        var paginator = __instance.gameObject.AddComponent<MeetingUIPaginator>();
        paginator.MeetingHud = __instance;
    }
        
    [HarmonyPatch(typeof(ShapeshifterMinigame), nameof(ShapeshifterMinigame.Begin))]
    [HarmonyPostfix]
    private static void AddShapeshifterPagination(ShapeshifterMinigame __instance)
    {
        if (__instance == null) return;
        if (GameOptionsManager.Instance?.CurrentGameOptions?.MaxPlayers <= GameOptionsExtension.VANILLA_MAX_PLAYERS) return;
            
        var existing = __instance.gameObject.GetComponent<ShapeshifterUIPaginator>();
        if (existing != null) GameObject.Destroy(existing);
            
        var paginator = __instance.gameObject.AddComponent<ShapeshifterUIPaginator>();
        paginator.Minigame = __instance;
    }
        
    [HarmonyPatch(typeof(VitalsMinigame), nameof(VitalsMinigame.Begin))]
    [HarmonyPostfix]
    private static void AddVitalsPagination(VitalsMinigame __instance)
    {
        if (__instance == null) return;
        if (GameOptionsManager.Instance?.CurrentGameOptions?.MaxPlayers <= GameOptionsExtension.VANILLA_MAX_PLAYERS) return;
            
        var existing = __instance.gameObject.GetComponent<VitalsUIPaginator>();
        if (existing != null) GameObject.Destroy(existing);
            
        var paginator = __instance.gameObject.AddComponent<VitalsUIPaginator>();
        paginator.Minigame = __instance;
    }

    [HarmonyPatch(typeof(SecurityLogger), nameof(SecurityLogger.Awake))]
    [HarmonyPostfix]
    private static void ExtendTimerArray(ref SecurityLogger __instance)
    {
        if (__instance != null)
        {
            __instance.Timers = new Il2CppStructArray<float>(GameOptionsExtension.EXTENDED_MAX_PLAYERS);
        }
    }
}

public class MeetingUIPaginator : UIPaginationController
{
    public MeetingHud MeetingHud { get; set; }
    private int _cachedMaxPage = -1;
        
    private IEnumerable<PlayerVoteArea> ActivePlayers => 
        MeetingHud?.playerStates?
            .OrderBy(p => p.AmDead)
            .ToList() ?? Enumerable.Empty<PlayerVoteArea>();
        
    public override int MaxPageCount
    {
        get
        {
            if (_cachedMaxPage < 0)
            {
                int playerCount = ActivePlayers.Count();
                _cachedMaxPage = Mathf.Max(0, (playerCount - 1) / ItemsPerPage);
            }
            return _cachedMaxPage;
        }
    }
    
    protected override void Initialize()
    {
        base.Initialize();
    }
    
    protected override void RefreshPageContent()
    {
        if (MeetingHud == null) return;
            
        int startIndex = CurrentPage * ItemsPerPage;
        var players = ActivePlayers.ToArray();
            
        for (int i = 0; i < players.Length; i++)
        {
            var playerArea = players[i];
            if (playerArea == null) continue;
            
            bool shouldShow = i >= startIndex && i < startIndex + ItemsPerPage;
            playerArea.gameObject.SetActive(shouldShow);
                
            if (shouldShow)
            {
                int pageIndex = i - startIndex;
                int row = pageIndex / 3;
                int col = pageIndex % 3;
                    
                Vector3 newPosition = MeetingHud.VoteOrigin + 
                                      new Vector3(
                                          MeetingHud.VoteButtonOffsets.x * col,
                                          MeetingHud.VoteButtonOffsets.y * row,
                                          playerArea.transform.localPosition.z
                                      );
                playerArea.transform.localPosition = newPosition;
            }
        }
        UpdateMeetingTimerDisplay();
    }
        
    private void UpdateMeetingTimerDisplay()
    {
        if (MeetingHud?.TimerText == null) return;
        if (MeetingHud.state == MeetingHud.VoteStates.Proceeding ||
            MeetingHud.state == MeetingHud.VoteStates.Results)
        {
            string pageInfo = $" ({CurrentPage + 1}/{MaxPageCount + 1})";
            if (!MeetingHud.TimerText.text.Contains(pageInfo))
            {
                MeetingHud.TimerText.text += pageInfo;
            }
        }
    }
        
    protected override void Awake()
    {
        _cachedMaxPage = -1;
        base.Awake();
    }
}

public class ShapeshifterUIPaginator : UIPaginationController
{
    public ShapeshifterMinigame Minigame { get; set; }
    private TextMeshPro _pageIndicator;
        
    private IEnumerable<ShapeshifterPanel> AvailableTargets => 
        Minigame?.potentialVictims?.ToArray() ?? Enumerable.Empty<ShapeshifterPanel>();
        
    public override int MaxPageCount => 
        Mathf.Max(0, (AvailableTargets.Count() - 1) / ItemsPerPage);
        
    protected override void Initialize()
    {
        if (Minigame != null && DestroyableSingleton<HudManager>.InstanceExists)
        {
            var sourceText = DestroyableSingleton<HudManager>.Instance.KillButton.cooldownTimerText;
            if (sourceText != null)
            {
                _pageIndicator = Instantiate(sourceText, Minigame.transform);
                _pageIndicator.name = "UI_PageIndicator";
                _pageIndicator.enableWordWrapping = false;
                _pageIndicator.gameObject.SetActive(true);
                _pageIndicator.transform.localPosition = new Vector3(4.1f, -2.36f, -1f);
                _pageIndicator.transform.localScale *= 0.5f;
            }
        }
        base.Initialize();
    }
        
    protected override void RefreshPageContent()
    {
        UpdatePageIndicator(_pageIndicator);
            
        int startIndex = CurrentPage * ItemsPerPage;
        var targets = AvailableTargets.ToArray();
            
        for (int i = 0; i < targets.Length; i++)
        {
            var panel = targets[i];
            if (panel == null) continue;
            
            bool shouldShow = i >= startIndex && i < startIndex + ItemsPerPage;
            panel.gameObject.SetActive(shouldShow);
                
            if (shouldShow && Minigame != null)
            {
                int pageIndex = i - startIndex;
                int row = pageIndex / 3;
                int col = pageIndex % 3;
                    
                panel.transform.localPosition = new Vector3(
                    Minigame.XStart + Minigame.XOffset * col,
                    Minigame.YStart + Minigame.YOffset * row,
                    panel.transform.localPosition.z
                );
            }
        }
    }
}

public class VitalsUIPaginator : UIPaginationController
{
    public VitalsMinigame Minigame { get; set; }
    private TextMeshPro _pageIndicator;
        
    private IEnumerable<VitalsPanel> VitalsPanels => 
        Minigame?.vitals?.ToArray() ?? Enumerable.Empty<VitalsPanel>();
        
    public override int MaxPageCount => 
        Mathf.Max(0, (VitalsPanels.Count() - 1) / ItemsPerPage);
        
    protected override void Initialize()
    {
        if (Minigame != null && DestroyableSingleton<HudManager>.InstanceExists)
        {
            var sourceText = DestroyableSingleton<HudManager>.Instance.KillButton.cooldownTimerText;
            if (sourceText != null)
            {
                _pageIndicator = Instantiate(sourceText, Minigame.transform);
                _pageIndicator.name = "UI_PageIndicator";
                _pageIndicator.enableWordWrapping = false;
                _pageIndicator.gameObject.SetActive(true);
                _pageIndicator.transform.localPosition = new Vector3(2.7f, -2f, -1f);
                _pageIndicator.transform.localScale *= 0.5f;
            }
        }
        base.Initialize();
    }
        
    protected override void RefreshPageContent()
    {
        if (PlayerControl.LocalPlayer != null && 
            PlayerTask.PlayerHasTaskOfType<HudOverrideTask>(PlayerControl.LocalPlayer))
        {
            return;
        }
            
        UpdatePageIndicator(_pageIndicator);
            
        int startIndex = CurrentPage * ItemsPerPage;
        var panels = VitalsPanels.ToArray();
            
        for (int i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            if (panel == null) continue;
            
            bool shouldShow = i >= startIndex && i < startIndex + ItemsPerPage;
            panel.gameObject.SetActive(shouldShow);
                
            if (shouldShow && Minigame != null)
            {
                int pageIndex = i - startIndex;
                int row = pageIndex / 3;
                int col = pageIndex % 3;
                    
                panel.transform.localPosition = new Vector3(
                    Minigame.XStart + Minigame.XOffset * col,
                    Minigame.YStart + Minigame.YOffset * row,
                    panel.transform.localPosition.z
                );
            }
        }
    }
}