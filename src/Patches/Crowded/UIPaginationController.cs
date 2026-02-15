using UnityEngine;
using TMPro;
using System;

namespace TONX.Patches.Crowded;

public abstract class UIPaginationController : MonoBehaviour
{
    protected int _currentPage;
    protected const int DEFAULT_ITEMS_PER_PAGE = 15;
    
    public virtual int ItemsPerPage => DEFAULT_ITEMS_PER_PAGE;
    
    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            int newPage = Mathf.Clamp(value, 0, Mathf.Max(0, MaxPageCount - 1));
            if (_currentPage != newPage)
            {
                _currentPage = newPage;
                RefreshPageContent();
            }
        }
    }
    
    public abstract int MaxPageCount { get; }
    
    protected abstract void RefreshPageContent();
    
    protected virtual void Initialize()
    {
        RefreshPageContent();
    }
    
    public void Cycle(bool increment)
    {
        int change = increment ? 1 : -1;
        if (MaxPageCount <= 0) return;
        
        int newPage = _currentPage + change;
        if (newPage < 0) newPage = MaxPageCount - 1;
        else if (newPage >= MaxPageCount) newPage = 0;
        
        CurrentPage = newPage;
    }
    
    protected virtual void ProcessNavigationInput()
    {
        if (DestroyableSingleton<HudManager>.Instance?.Chat?.IsOpenOrOpening ?? false)
            return;
                
        float scrollDelta = Input.mouseScrollDelta.y;
        
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || scrollDelta > 0)
        {
            Cycle(false);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow) || scrollDelta < 0)
        {
            Cycle(true);
        }
    }
    
    protected virtual void Awake()
    {
        Initialize();
    }
    
    protected virtual void Update()
    {
        ProcessNavigationInput();
    }
    
    protected TextMeshPro CreatePageIndicator(Transform parent, Vector3 localPosition, float scale = 0.5f)
    {
        if (!DestroyableSingleton<HudManager>.InstanceExists) return null;
        
        var sourceText = DestroyableSingleton<HudManager>.Instance.KillButton?.cooldownTimerText;
        if (sourceText == null) return null;
        
        var indicator = Instantiate(sourceText, parent);
        indicator.name = "UI_PageIndicator";
        indicator.enableWordWrapping = false;
        indicator.gameObject.SetActive(true);
        indicator.transform.localPosition = localPosition;
        indicator.transform.localScale = Vector3.one * scale;
        
        return indicator;
    }
    
    protected void UpdatePageIndicator(TextMeshPro indicator)
    {
        if (indicator != null)
        {
            indicator.text = $"({CurrentPage + 1}/{MaxPageCount + 1})";
        }
    }
}