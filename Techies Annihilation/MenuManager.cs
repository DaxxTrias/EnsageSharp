﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using Ensage.Common.Menu;
using SharpDX;
using Techies_Annihilation.BombFolder;
using Techies_Annihilation.Features;

namespace Techies_Annihilation
{
    internal class MenuManager
    {
        public static readonly Menu Menu = new Menu("Techies Annihilation", "Techies Annihilation", true,
            "npc_dota_hero_techies", true);
        public static bool DebugInGame => Menu.Item("Dev.Text.enable").GetValue<bool>();
        public static bool DebugInConsole => Menu.Item("Dev.Text2.enable").GetValue<bool>();
        public static float GetUpdateSpeed => Menu.Item("Performance.tickRate").GetValue<Slider>().Value;

        public static Vector2 GetExtraPosForTopPanel
            =>
                new Vector2(Menu.Item("TopPanel.Extra.X").GetValue<Slider>().Value,
                    Menu.Item("TopPanel.Extra.Y").GetValue<Slider>().Value);

        public static Vector2 GetTopPanelExtraSize
            => new Vector2(Menu.Item("TopPanel.Size").GetValue<Slider>().Value);

        public static bool CheckForAegis => Menu.Item("Settings.Aegis.Enable").GetValue<bool>();

        public static bool IsEnableForceStaff => GetBool("Settings.ForceStaff.Enable");
        public static bool LandMineIndicatorEnable => GetBool("Drawing.LandMineStatus.Enable");
        public static double GetLandMineIndicatorDigSize => GetSlider("Drawing.LandMineStatus.Dig.Size")/100f;
        public static float GetLandMineBarSize => GetSlider("Drawing.LandMineStatus.Bar.Size");
        public static bool LandMinesDrawDigs => GetBool("Drawing.LandMineStatus.Digs.Enable");
        public static double GetBombDelay => GetSlider("Settings.Delay")/1000f;

        public static bool IsEnableDelayBlow => GetBool("Settings.Delay.Enable");
        public static bool IsStackerEnabled => GetBool("Drawing.Stacker.Enable");
        public static bool IsSuperDetonate => GetBool("Settings.SuperDetonate.Enable");
        public static bool IsAutoDetonation => GetBool("Settings.AutoDetonation.Enable");
        public static bool IsCameraMovingEnable => GetBool("Settings.MoveCamera.Enable");
        public static int CameraMovingType => GetStringList("Settings.MoveCamera.Type");
        public static bool IsEnable => GetBool("Enable");

        public static void Init()
        {
            Menu.AddItem(new MenuItem("Enable", "Enable").SetValue(true));
            var settings = new Menu("Settings", "Settings");
            var delay = new Menu("Delay", "Delay");
            settings.AddItem(new MenuItem("Settings.Aegis.Enable", "Detonate in aegis").SetValue(true));
            settings.AddItem(new MenuItem("Settings.AutoDetonation.Enable", "Auto Detonation").SetValue(true));
            settings.AddItem(new MenuItem("Settings.ForceStaff.Enable", "Enable ForceStaff").SetValue(true));
            settings.AddItem(new MenuItem("Settings.SuperDetonate.Enable", "Detonate all in once").SetValue(false));
            settings.AddItem(new MenuItem("Settings.MoveCamera.Enable", "Move camera on mines").SetValue(false));
            settings.AddItem(
                new MenuItem("Settings.MoveCamera.Type", "Camera type").SetValue(new StringList("By console command",
                    "By mouse")));
            delay.AddItem(new MenuItem("Settings.Delay.Enable", "Enable").SetValue(false));
            delay.AddItem(new MenuItem("Settings.Delay", "Delay bomb activation").SetValue(new Slider(150, 1, 500)));
            var draw = new Menu("Drawing", "Drawing");
            var stacker = new Menu("Stacker", "Stacker");
            stacker.AddItem(new MenuItem("Drawing.Stacker.Enable", "Enable").SetValue(true));
            var landMineIndicator = new Menu("Indicator", "Indicator");
            landMineIndicator.AddItem(new MenuItem("Drawing.LandMineStatus.Enable", "Enable LandMine Indicator").SetValue(true));
            landMineIndicator.AddItem(new MenuItem("Drawing.LandMineStatus.Digs.Enable", "Draw [%]").SetValue(true));
            landMineIndicator.AddItem(
                new MenuItem("Drawing.LandMineStatus.Bar.Size", "Indicator size").SetValue(new Slider(13, 5, 30)));
            landMineIndicator.AddItem(new MenuItem("Drawing.LandMineStatus.Dig.Size", "Text size").SetValue(new Slider(100,50,150)));
            var range = new Menu("Range", "Range");
            var landMineRange =
                range.AddItem(new MenuItem("Drawing.Range.LandMine", "Range for LandMine").SetValue(true));

            var staticTrapRange =
                range.AddItem(new MenuItem("Drawing.Range.StaticTrap", "Range for StaticTrap").SetValue(true));

            var remotaMineRange =
                range.AddItem(new MenuItem("Drawing.Range.RemoteMine", "Range for RemoteMine").SetValue(true));

            var topPanel = new Menu("TopPanel", "TopPanel");
            topPanel.AddItem(
                new MenuItem("TopPanel.Extra.X", "Extra Position X").SetValue(new Slider(0, -150, 150)));
            topPanel.AddItem(
                new MenuItem("TopPanel.Extra.Y", "Extra Position Y").SetValue(new Slider(0, -150, 150)));
            topPanel.AddItem(
                new MenuItem("TopPanel.Size", "Size").SetValue(new Slider(0, -50, 50)));
            var perfomance = new Menu("Performance", "Performance");
            perfomance.AddItem(
                new MenuItem("Performance.tickRate", "Damage Update Rate (Drawing)").SetValue(new Slider(500, 50, 1000))).SetTooltip("in ms");
            var devolper = new Menu("Developer", "Developer");
            devolper.AddItem(new MenuItem("Dev.Text.enable", "Debug messages ingame").SetValue(false));
            devolper.AddItem(new MenuItem("Dev.Text2.enable", "Debug messages in console").SetValue(false));
            Menu.AddSubMenu(settings);
            settings.AddSubMenu(delay);
            settings.AddSubMenu(draw);
            settings.AddSubMenu(perfomance);
            draw.AddSubMenu(topPanel);
            draw.AddSubMenu(landMineIndicator);
            draw.AddSubMenu(stacker);
            draw.AddSubMenu(range);
            
            Menu.AddSubMenu(devolper);
            Menu.AddToMainMenu();

            landMineRange.ValueChanged += RangeOnChange;
            staticTrapRange.ValueChanged += RangeOnChange;
            remotaMineRange.ValueChanged += RangeOnChange;
        }

        public static bool DrawRangeForLandMine => GetBool("Drawing.Range.LandMine");
        public static bool DrawRangeForStaticTrap => GetBool("Drawing.Range.StaticTrap");
        public static bool DrawRangeForRemoteMine => GetBool("Drawing.Range.RemoteMine");

        private static void RangeOnChange(object sender, OnValueChangeEventArgs onValueChangeEventArgs)
        {
            var menu = sender as MenuItem;
            if (menu==null)
                return;
            if (onValueChangeEventArgs.GetNewValue<bool>())
                return;
            List<BombManager> list = new List<BombManager>();
            switch (menu.Name)
            {
                case "Drawing.Range.LandMine":
                    list = Core.Bombs.Where(x => !x.IsRemoteMine && x.Bomb.MaximumHealth == 1).ToList();
                    break;
                case "Drawing.Range.StaticTrap":
                    list = Core.Bombs.Where(x => !x.IsRemoteMine && x.Bomb.MaximumHealth == 100).ToList();
                    break;
                case "Drawing.Range.RemoteMine":
                    list = Core.Bombs.Where(x => x.IsRemoteMine).ToList();
                    break;
                default:
                    break;
            }
            foreach (var bombManager in list)
            {
                bombManager.RangEffect?.Dispose();
            }
        }

        private static float GetSlider(string item)
        {
            return Menu.Item(item).GetValue<Slider>().Value;
        }
        private static bool GetKey(string item)
        {
            return Menu.Item(item).GetValue<KeyBind>().Active;
        }
        private static bool GetBool(string item)
        {
            return Menu.Item(item).GetValue<bool>();
        }
        private static int GetStringList(string item)
        {
            return Menu.Item(item).GetValue<StringList>().SelectedIndex;
        }
    }
}