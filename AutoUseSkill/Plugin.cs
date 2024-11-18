using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Configuration;
using UnityEngine;

namespace AutoUseSkill;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class AutoUseSkill : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private bool ModEnabled = true;
    private bool AutoCastOutOfCombat;
    private bool ShowGUI;
    private static ConfigEntry<KeyboardShortcut> EnableKey;
    private static ConfigEntry<KeyboardShortcut> OpenMenuKey;
    private static ConfigEntry<float> ButtonHeight;
    private Rect WindowRect;
    private bool Auto_Attack;
    private bool[] AutoUseDict = new bool[4];
    private double lastSearchTime = 0d;
    private static ConfigEntry<double> SearchInterval;

    private static Hero Player => ManagerBase<ControlManager>.instance?.controllingEntity as Hero;
    private static ControlManager controlManager => ManagerBase<ControlManager>.instance;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        EnableKey = Config.Bind("Keys", "EnableKey", new KeyboardShortcut(KeyCode.O, Array.Empty<KeyCode>()), "启用\nEnable");
        OpenMenuKey = Config.Bind("Keys", "OpenMenuKey", new KeyboardShortcut(KeyCode.I, Array.Empty<KeyCode>()), "打开菜单\nOpen menu");
        ButtonHeight = Config.Bind("GUI", "ButtonHeight", 0.035f, "按钮高度, 屏幕高度的比例\nButton height, ratio of screen height");
        SearchInterval = Config.Bind("SearchInterval", "SearchInterval", 0.01d, "技能释放检测间隔, 单位为秒\nSkill cast detection interval, in seconds");
        WindowRect = new Rect(Screen.width * 0.4f, Screen.height * 0.4f, Screen.width * 0.25f, Screen.height * ButtonHeight.Value * 4f);
    }

    private void Update()
    {
        if (OpenMenuKey.Value.IsDown())
            ShowGUI = !ShowGUI;
        if (EnableKey.Value.IsDown())
            ModEnabled = !ModEnabled;
        if (!ModEnabled || !Player || !AutoCastOutOfCombat && !Player.isInCombat) return;
        if (Time.time - lastSearchTime < SearchInterval.Value) return;
        lastSearchTime = Time.time;
        TryAutoCastSkill();
        TryAutoAttack();
    }

    private void TryAutoCastSkill()
    {
        for (var i = 0; i < 3; i++)
        {
            if (!AutoUseDict[i]) continue;
            Player.Ability.abilities.TryGetValue(i, out var abilityTrigger);
            if (abilityTrigger is not SkillTrigger skill) continue;
            if (!skill.CanBeCast()) continue;
            var shouldCast = true;
            switch (skill.currentConfig.castMethod.type)
            {
                case CastMethodType.Cone:
                case CastMethodType.Arrow:
                case CastMethodType.Target:
                    if (!controlManager.targetEnemy || !skill.currentConfig.CheckRange(Player, controlManager.targetEnemy)) shouldCast = false;
                    break;
                case CastMethodType.None:
                case CastMethodType.Point:
                default:
                    break;
            }

            if (shouldCast) controlManager.CastAbilityAuto(skill);
        }
    }

    private void TryAutoAttack()
    {
        if (!Auto_Attack) return;
        var attackAbility = Player.Ability.attackAbility as AttackTrigger;
        if (!attackAbility || attackAbility.IsNullOrInactive()) return;
        if (!controlManager.targetEnemy || !attackAbility.currentConfig.CheckRange(Player, controlManager.targetEnemy)) return;
        Player.Control.CmdAttack(controlManager.targetEnemy, false);
    }

    private void OnGUI()
    {
        if (!ShowGUI) return;
        WindowRect = GUILayout.Window(0, WindowRect, MenuGui, "Auto Use Skill", "box");
    }

    private void MenuGui(int id)
    {
        GUI.DragWindow(new Rect(0, 0, 1000, Screen.height * ButtonHeight.Value));
        var option = GUILayout.Height(Screen.height * ButtonHeight.Value);
        GUILayout.BeginVertical();
        GUILayout.Label("", option);
        GUILayout.BeginHorizontal();
        ModEnabled = GUILayout.Toggle(ModEnabled, "Enable", option);
        AutoCastOutOfCombat = GUILayout.Toggle(AutoCastOutOfCombat, "Auto Cast Out Of Combat", option);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Auto_Attack = GUILayout.Toggle(Auto_Attack, "Attack", option);
        AutoUseDict[0] = GUILayout.Toggle(AutoUseDict[0], "Q", option);
        AutoUseDict[1] = GUILayout.Toggle(AutoUseDict[1], "W", option);
        AutoUseDict[2] = GUILayout.Toggle(AutoUseDict[2], "E", option);
        AutoUseDict[3] = GUILayout.Toggle(AutoUseDict[3], "R", option);
        GUILayout.EndHorizontal();
        GUILayout.Label("检测间隔/Detection Interval: " + SearchInterval.Value.ToString("0.00") + "s", option);
        SearchInterval.Value = Math.Round(GUILayout.HorizontalSlider((float)SearchInterval.Value, 0.01f, 0.5f, option), 2);
        GUILayout.EndVertical();
    }
}