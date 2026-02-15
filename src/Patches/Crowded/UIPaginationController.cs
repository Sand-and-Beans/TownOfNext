using UnityEngine;
using TMPro;

namespace TONX.Patches.Crowded;

    public abstract class UIPaginationController : MonoBehaviour
    {
        protected int _currentPage;
        private const int DEFAULT_ITEMS_PER_PAGE = 15;
        
        public virtual int ItemsPerPage => DEFAULT_ITEMS_PER_PAGE;
        
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = Mathf.Clamp(value, 0, MaxPageCount - 1);
                RefreshPageContent();
            }
        }
        
        public abstract int MaxPageCount { get; }
        
        protected abstract void RefreshPageContent();
        
        protected virtual void Initialize()
        {
            RefreshPageContent();
        }
        
        protected virtual void ProcessNavigationInput()
        {
            if (DestroyableSingleton<HudManager>.Instance?.Chat?.IsOpenOrOpening ?? false)
                return;
                
            float scrollDelta = Input.mouseScrollDelta.y;
            
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || scrollDelta > 0)
            {
                NavigatePage(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow) || scrollDelta < 0)
            {
                NavigatePage(1);
            }
        }
        
        protected void NavigatePage(int direction)
        {
            CurrentPage += Math.Sign(direction);
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
            var indicator = Instantiate(
                DestroyableSingleton<HudManager>.Instance.KillButton.cooldownTimerText,
                parent
            );
            
            indicator.name = "UI_PageIndicator";
            indicator.enableWordWrapping = false;
            indicator.gameObject.SetActive(true);
            indicator.transform.localPosition = localPosition;
            indicator.transform.localScale *= scale;
            
            return indicator;
        }
        
        protected void UpdatePageIndicator(TextMeshPro indicator)
        {
            if (indicator != null)
            {
                indicator.text = $"({CurrentPage + 1}/{MaxPageCount})";
            }
        }
    }