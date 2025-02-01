using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using TONX.Modules.OptionItems;
using TONX.Modules.OptionItems.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using static TONX.Translator;
using Object = UnityEngine.Object;

namespace TONX
{
    [HarmonyPatch(typeof(GameSettingMenu))]
    public static class GameSettingMenuPatch
    {
        private static List<GameOptionsMenu> tonxSettingsTab = new List<GameOptionsMenu>();
        private static List<PassiveButton> tonxSettingsButton = new List<PassiveButton>();
        public static List<string> TONXMenuName = new List<string>();
        public static List<CategoryHeaderMasked> CategoryHeaders = new List<CategoryHeaderMasked>();
        // 左侧按钮坐标
        private static Vector3 buttonPosition = new(-2.55f, -0.1f, 0f);
        // 本体按钮大小
        private static Vector3 buttonSize = new(0.45f, 0.6f, 1f);

        [HarmonyPatch(nameof(GameSettingMenu.Start)), HarmonyPostfix]
        public static void StartPostfix(GameSettingMenu __instance)
        {
            // 重置列表
            tonxSettingsTab = new List<GameOptionsMenu>();
            tonxSettingsButton = new List<PassiveButton>();
            TONXMenuName = new List<string>();
            CategoryHeaders = new List<CategoryHeaderMasked>();

            __instance.GamePresetsButton.transform.parent.localPosition = buttonPosition;
            __instance.GamePresetsButton.transform.parent.localScale = buttonSize;

            // TONX設定ボタン
            foreach (var tab in Enum.GetValues(typeof(TabGroup)))
            {
                Vector3 offset_left = new (0f, 0.64f * ((int)tab + 3) - 0.64f, 0f);
                Vector3 offset_right = new (-3f, 0.64f * ((int)tab - 2) - 0.64f, 0f);
                var SettingsTab = Object.Instantiate(__instance.GameSettingsTab, __instance.GameSettingsTab.transform.parent);
                SettingsTab.name = tab.ToString() + " TAB";
                TONXMenuName.Add(SettingsTab.name);
                var vanillaOptions = SettingsTab.GetComponentsInChildren<OptionBehaviour>();
                foreach (var vanillaOption in vanillaOptions)
                {
                    Object.Destroy(vanillaOption.gameObject);
                }

                var SettingsButton = Object.Instantiate(__instance.GameSettingsButton, __instance.GameSettingsButton.transform.parent);
                SettingsButton.name = tab.ToString() + " BUTTON";
                SettingsButton.transform.localPosition -= ((int)tab < 2) ? offset_left : offset_right;
                SettingsButton.buttonText.DestroyTranslator();
                SettingsButton.buttonText.text = GetString($"TabGroup.{tab}");
                var activeSprite = SettingsButton.activeSprites.GetComponent<SpriteRenderer>();
                var selectedSprite = SettingsButton.selectedSprites.GetComponent<SpriteRenderer>();
                Color32 buttonColor = tab switch
                {
                    TabGroup.SystemSettings => Main.UnityModColor,
                    TabGroup.GameSettings => new Color32(89, 239, 131, 255),
                    TabGroup.ImpostorRoles => Utils.GetCustomRoleTypeColor(Roles.Core.CustomRoleTypes.Impostor),
                    TabGroup.CrewmateRoles => Utils.GetCustomRoleTypeColor(Roles.Core.CustomRoleTypes.Crewmate),
                    TabGroup.NeutralRoles => Utils.GetCustomRoleTypeColor(Roles.Core.CustomRoleTypes.Neutral),
                    TabGroup.Addons => Utils.GetCustomRoleTypeColor(Roles.Core.CustomRoleTypes.Addon),
                    TabGroup.OtherRoles => new Color32(118, 184, 224, 255),
                    _ => Color.white,
                };
                activeSprite.color = selectedSprite.color = buttonColor;
                SettingsButton.OnClick.AddListener((Action)(() =>
                {
                    __instance.ChangeTab((int)tab+3, false);  // バニラタブを閉じる
                    SettingsTab.gameObject.SetActive(true);
                    __instance.MenuDescriptionText.text = GetString($"MenuDescriptionText.{tab}");
                    SettingsButton.SelectButton(true);
                }));

                // 各設定スイッチを作成
                var template = __instance.GameSettingsTab.stringOptionOrigin;
                var scOptions = new Il2CppSystem.Collections.Generic.List<OptionBehaviour>();
                foreach (var option in OptionItem.AllOptions)
                {
                    if (option.Tab != (TabGroup)tab) continue;
                    if (option.OptionBehaviour == null)
                    {
                        if (option.IsText) 
                        {
                            CategoryHeaders.Add(CreateCategoryHeader(__instance, SettingsTab, option));
                            continue;
                        }
                        var stringOption = Object.Instantiate(template, SettingsTab.settingsContainer);
                        scOptions.Add(stringOption);
                        stringOption.SetClickMask(__instance.GameSettingsButton.ClickMask);
                        stringOption.SetUpFromData(stringOption.data, GameOptionsMenu.MASK_LAYER);
                        stringOption.OnValueChanged = new Action<OptionBehaviour>((o) => { });
                        stringOption.TitleText.text = option.Name;
                        stringOption.Value = stringOption.oldValue = option.CurrentValue;
                        stringOption.ValueText.text = option.GetString();
                        stringOption.name = option.Name;

                        // タイトルの枠をデカくする
                        var indent = 0f;  // 親オプションがある場合枠の左を削ってインデントに見せる
                        var parent = option.Parent;
                        while (parent != null)
                        {
                            indent += 0.15f;
                            parent = parent.Parent;
                        }
                        stringOption.LabelBackground.size += new Vector2(2f - indent * 2, 0f);
                        stringOption.LabelBackground.transform.localPosition += new Vector3(-1f + indent, 0f, 0f);
                        stringOption.TitleText.rectTransform.sizeDelta += new Vector2(2f - indent * 2, 0f);
                        stringOption.TitleText.transform.localPosition += new Vector3(-1f + indent, 0f, 0f);

                        option.OptionBehaviour = stringOption;
                    }
                    option.OptionBehaviour.gameObject.SetActive(true);
                }
                SettingsTab.Children = scOptions;
                SettingsTab.gameObject.SetActive(false);

                // 存储模组设置按钮
                tonxSettingsTab.Add(SettingsTab);
                tonxSettingsButton.Add(SettingsButton);
            }
        }
        private const float JumpButtonSpacing = 0.6f;
        // ジャンプしたカテゴリヘッダのScrollerとの相対Y座標がこの値になる
        private const float CategoryJumpY = 2f;
        private static CategoryHeaderMasked CreateCategoryHeader(GameSettingMenu __instance, GameOptionsMenu tonxTab, OptionItem option)
        {
            var categoryHeader = Object.Instantiate(__instance.GameSettingsTab.categoryHeaderOrigin, Vector3.zero, Quaternion.identity, tonxTab.settingsContainer);
            categoryHeader.name = option.Name;
            categoryHeader.Title.text = option.GetName();
            var maskLayer = GameOptionsMenu.MASK_LAYER;
            categoryHeader.Background.material.SetInt(PlayerMaterial.MaskLayer, maskLayer);
            if (categoryHeader.Divider != null)
            {
                categoryHeader.Divider.material.SetInt(PlayerMaterial.MaskLayer, maskLayer);
            }
            categoryHeader.Title.fontMaterial.SetFloat("_StencilComp", 3f);
            categoryHeader.Title.fontMaterial.SetFloat("_Stencil", (float)maskLayer);
            categoryHeader.transform.localScale = Vector3.one * GameOptionsMenu.HEADER_SCALE;
            return categoryHeader;
        }

        // 初めてロール設定を表示したときに発生する例外(バニラバグ)の影響を回避するためPrefix
        [HarmonyPatch(nameof(GameSettingMenu.ChangeTab)), HarmonyPrefix]
        public static bool ChangeTabPrefix(GameSettingMenu __instance, int tabNum, bool previewOnly)
        {
            // 用于应对按下快捷键会触发预设按钮的bug
            if (previewOnly && tabNum == 0)
            {
                if (!__instance.GamePresetsButton.beingHeldDown) return false;
            }

            // 用于应对CloseOverlayMenu失败的bug
            if (!previewOnly)
            {
                foreach (var tab in tonxSettingsTab)
                {
                    if (tab)
                    {
                        tab.gameObject.SetActive(false);
                    }
                }
                foreach (var button in tonxSettingsButton)
                {
                    if (button)
                    {
                        button.SelectButton(false);
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Initialize))]
    public static class GameOptionsMenuInitializePatch
    {
        public static void Postfix(GameOptionsMenu __instance)
        {
            foreach (var ob in __instance.Children)
            {
                switch (ob.Title)
                {
                    case StringNames.GameShortTasks:
                    case StringNames.GameLongTasks:
                    case StringNames.GameCommonTasks:
                        ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 99);
                        break;
                    case StringNames.GameKillCooldown:
                        ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 180);
                        break;
                    case StringNames.GameNumImpostors:
                        if (DebugModeManager.IsDebugMode)
                        {
                            ob.Cast<NumberOption>().ValidRange.min = 0;
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Update))]
    public class GameOptionsMenuUpdatePatch
    {
        private static float _timer = 1f;

        public static void Postfix(GameOptionsMenu __instance)
        {
            if (!GameSettingMenuPatch.TONXMenuName.Contains(__instance.name)) return;

            foreach (var tab in Enum.GetValues(typeof(TabGroup)))
            {
                if (__instance.name != tab.ToString() + " TAB") continue;

                _timer += Time.deltaTime;
                if (_timer < 0.1f) return;
                _timer = 0f;

                var offset = 2.4f;
                var isOdd = true;
                var isFirst = true;

                foreach (var option in OptionItem.AllOptions)
                {
                    if ((TabGroup)tab != option.Tab) continue;
                    if (option.IsText)
                    {
                        if (isFirst)
                        {
                            offset += 0.3f;
                            isFirst = false;
                        }
                        foreach (var categoryHeader in GameSettingMenuPatch.CategoryHeaders)
                        {
                            if (option.Name == categoryHeader.name)
                            {
                                UpdateCategoryHeader(categoryHeader, ref offset);
                                continue;
                            }
                        }
                        continue;
                    }
                    if (isFirst) isFirst = false;
                    UpdateOption(ref isOdd, option, ref offset);
                }

                __instance.scrollBar.ContentYBounds.max = (-offset) - 1.5f;
            }
        }
        private static void UpdateCategoryHeader(CategoryHeaderMasked categoryHeader, ref float offset)
        {
            offset -= GameOptionsMenu.HEADER_HEIGHT;
            categoryHeader.transform.localPosition = new(GameOptionsMenu.HEADER_X, offset, -2f);
        }
        private static void UpdateOption(ref bool isOdd, OptionItem item, ref float offset)
        {
            if (item?.OptionBehaviour == null || item.OptionBehaviour.gameObject == null) return;

            var enabled = true;
            var parent = item.Parent;

            // 親オプションの値を見て表示するか決める
            enabled = AmongUsClient.Instance.AmHost && !item.IsHiddenOn(Options.CurrentGameMode);
            var stringOption = item.OptionBehaviour;
            while (parent != null && enabled)
            {
                enabled = parent.GetBool();
                parent = parent.Parent;
            }
            
            item.OptionBehaviour.gameObject.SetActive(enabled);

            if (enabled)
            {
                // 見やすさのため交互に色を変える  
                stringOption.LabelBackground.color = item is IRoleOptionItem roleOption ? roleOption.RoleColor : (isOdd ? Color.cyan : Color.white);

                offset -= GameOptionsMenu.SPACING_Y;
                if (item.IsHeader)
                {
                    // IsHeaderなら隙間を広くする
                    offset -= HeaderSpacingY;
                }
                item.OptionBehaviour.transform.localPosition = new Vector3(
                    GameOptionsMenu.START_POS_X,
                    offset,
                    -2f);

                stringOption.ValueText.text = item.GetString();

                isOdd = !isOdd;
            }
        }

        private const float HeaderSpacingY = 0.2f;
    }

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Initialize))]
    public class StringOptionInitializePatch
    {
        public static bool Prefix(StringOption __instance)
        {
            var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
            if (option == null) return true;

            __instance.OnValueChanged = new Action<OptionBehaviour>((o) => { });
            __instance.TitleText.text = option.GetName(option is RoleSpawnChanceOptionItem);
            __instance.Value = __instance.oldValue = option.CurrentValue;
            __instance.ValueText.text = option.GetString();

            return false;
        }
    }

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Increase))]
    public class StringOptionIncreasePatch
    {
        public static bool Prefix(StringOption __instance)
        {
            var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
            if (option == null) return true;

            option.SetValue(option.CurrentValue + (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : 1));
            return false;
        }
    }

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Decrease))]
    public class StringOptionDecreasePatch
    {
        public static bool Prefix(StringOption __instance)
        {
            var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
            if (option == null) return true;

            option.SetValue(option.CurrentValue - (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : 1));
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
    public class RpcSyncSettingsPatch
    {
        public static void Postfix()
        {
            OptionItem.SyncAllOptions();
        }
    }
    [HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.InitialSetup))]
    public static class RolesSettingsMenuPatch
    {
        public static void Postfix(RolesSettingsMenu __instance)
        {
            foreach (var ob in __instance.advancedSettingChildren)
            {
                switch (ob.Title)
                {
                    case StringNames.EngineerCooldown:
                        ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 180);
                        break;
                    case StringNames.ShapeshifterCooldown:
                        ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 180);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
