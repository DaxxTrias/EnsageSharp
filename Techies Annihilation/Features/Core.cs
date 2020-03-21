using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Ensage;
using Ensage.Common;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Objects;
using Ensage.Common.Objects.UtilityObjects;
using Ensage.SDK.Helpers;
using Techies_Annihilation.BombFolder;
using Techies_Annihilation.Utils;
using AbilityId = Ensage.AbilityId;
using UnitExtensions = Ensage.SDK.Extensions.UnitExtensions;

namespace Techies_Annihilation.Features
{
    internal class Core
    {
        public static Hero Me;
        public static Team MeTeam;
        public static Team EnemyTeam;
        public static Ability LandMine;
        public static Ability RemoteMine;
        public static Ability Suicide;
        public static bool ExtraDamageFromSuicide=false;
        public static List<BombManager> Bombs = new List<BombManager>();
        public static List<BombManager> LandBombs = new List<BombManager>();
        public static List<BombManager> RamoteBombs = new List<BombManager>();

        public static float GetLandMineDamage => LandMine.GetAbilityData("damage", LandMine.Level);
        public static float GetRemoteMineDamage => RemoteMine.GetAbilityData("damage", RemoteMine.Level);
        public static float GetSuicideDamage => Suicide.GetAbilityData("damage", Suicide.Level);

        public static MultiSleeper HeroSleeper=new MultiSleeper();
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
        private enum MouseEvent
        {
            MouseeventfLeftdown = 0x02,
            MouseeventfLeftup = 0x04,
        }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);
        public static void OnUpdate(EventArgs args)
        {
            if (!MenuManager.IsEnable)
                return;
            if (!MenuManager.IsAutoDetonation)
                return;
            var spellAmp = 0;//UnitExtensions.GetSpellAmplification(Me);
            foreach (var hero in Heroes.GetByTeam(EnemyTeam))      
            {
                if (HeroSleeper.Sleeping(hero) || !hero.IsAlive || !hero.IsVisible || !hero.CanDie(MenuManager.CheckForAegis) || hero.IsMagicImmune())
                    continue;
                if (hero.HasModifiers(
                    new[]
                    {
                        "modifier_shredder_timber_chain", "modifier_storm_spirit_ball_lightning",
                        "modifier_item_aeon_disk_buff", "modifier_ember_spirit_sleight_of_fist_caster",
                        "modifier_ember_spirit_sleight_of_fist_caster_invulnerability"
                    },
                    false))
                    continue;
                var listForDetonation = new List<BombManager>();
                var heroHealth = hero.Health + hero.HealthRegeneration;
                var rainrop = hero.GetItemById(ItemId.item_infused_raindrop);
                if (rainrop != null && rainrop.CanBeCasted())
                {
                    var extraHealth = 90f;//rainrop.GetAbilityData("magic_damage_block");
                    heroHealth += extraHealth;
                }
                //Console.WriteLine($"[{hero.GetRealName()}] Total Life -> {heroHealth}");
                var reduction = RemoteMine.GetDamageReduction(hero);
                var refraction = hero.FindModifier("modifier_templar_assassin_refraction_absorb");
                var blockCount = refraction?.StackCount;
                var aeon = UnitExtensions.GetItemById(hero, AbilityId.item_aeon_disk);
                var breakHealthForAeon = hero.MaximumHealth * .8f;
                float treshold = 0;
                var heroid = hero.HeroId;
                if (heroid == HeroId.npc_dota_hero_medusa)
                {
                    var shield = hero.GetAbilityById(AbilityId.medusa_mana_shield);
                    if (shield.IsToggled)
                    {
                        treshold = shield.GetAbilityData("damage_per_mana");
                    }
                }
                var startManaCalc = hero.Mana;
                foreach (var element in Bombs)
                {
                    if (element.IsRemoteMine && element.Active)
                    {
                        if (element.CanHit(hero))
                        {
                            //Printer.Print($"BombDelay: {element.GetBombDelay(hero)} MaxDelay: {MenuManager.GetBombDelay}");
                            if (MenuManager.IsEnableDelayBlow &&
                                !(element.GetBombDelay(hero) >= MenuManager.GetBombDelay))
                            {
                                continue;
                            }
                            if (blockCount > 0)
                            {
                                blockCount--;
                            }
                            else
                            {
                                var damage = DamageHelpers.GetSpellDamage(element.Damage, spellAmp, reduction);
                                if (heroid == HeroId.npc_dota_hero_medusa)
                                    BombDamageManager.CalcDamageForDusa(ref damage, ref startManaCalc, treshold);
                                heroHealth -= damage;
                            }
                            listForDetonation.Add(element);
                            var aeuoByPass = aeon != null && aeon.CanBeCasted() && heroHealth < breakHealthForAeon;
                            
                            if (heroHealth <= 0 || aeuoByPass)
                            {
                                if (MenuManager.IsCameraMovingEnable)
                                {
                                    if (MenuManager.CameraMovingType==0)
                                    {
                                        var heroPos = hero.Position;
                                        var consolePosition = $"{heroPos.X} {heroPos.Y}";
                                        Game.ExecuteCommand($"dota_camera_set_lookatpos {consolePosition}");
                                    }
                                    else
                                    {
                                        var pos = hero.Position.WorldToMinimap();
                                        SetCursorPos((int) pos.X, (int) pos.Y);
                                        UpdateManager.BeginInvoke(() =>
                                        {
                                            mouse_event((int) MouseEvent.MouseeventfLeftdown, 0, 0, 0, 0);
                                            mouse_event((int) MouseEvent.MouseeventfLeftup, 0, 0, 0, 0);
                                        }, 100);
                                    }
                                }
                                HeroSleeper.Sleep(300 + listForDetonation.Count*30, hero);
                                if (MenuManager.IsSuperDetonate)
                                {
                                    foreach (var manager in Bombs.Where(x=> x.IsRemoteMine && x.Active && x.CanHit(hero)))
                                    {
                                        manager.Detonate();
                                    }
                                }
                                else
                                {
                                    foreach (var manager in listForDetonation)
                                    {
                                        manager.Detonate();
                                    }
                                }
                                
                                break;
                            }
                        }
                    }
                }
            }
        }

        public static void Init(Hero me)
        {
            Me = me;
            LandMine = me.GetAbilityById(AbilityId.techies_land_mines);
            RemoteMine = me.GetAbilityById(AbilityId.techies_remote_mines);
            Suicide = me.GetAbilityById(AbilityId.techies_suicide);
            MeTeam = me.Team;
            EnemyTeam = me.GetEnemyTeam();
        }
    }
}