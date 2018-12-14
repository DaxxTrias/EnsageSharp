using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.SDK.Helpers;
using Ensage.SDK.Menu;
using log4net;
using PlaySharp.Toolkit.Logging;

namespace InvokerAnnihilationCrappa.Features
{
    public class Prepare
    {
        private readonly Config _main;
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public Prepare(Config main)
        {
            _main = main;
            var panel = main.Factory.Menu("Prepare");
            Enable = panel.Item("Combo key with CTRL (need to hold)", true);
            CustomKey = panel.Item("Cusom key (need to hold)", new KeyBind('0'));
            Smart = panel.Item("Smart more", false);
            Smart.Item.SetTooltip("will prepare 1st & 3rd abilities instead of 1st & 2nd");

            if (Enable)
            {
                //UpdateManager.BeginInvoke(Callback);
                UpdateManager.Subscribe(Tost, 100);
                CustomKey.Item.ValueChanged += ItemOnValueChanged;
            }

            Enable.Item.ValueChanged += (sender, args) =>
            {
                if (args.GetNewValue<bool>())
                    UpdateManager.Subscribe(Tost, 100);
                else
                    UpdateManager.Unsubscribe(Tost);
                //UpdateManager.BeginInvoke(Callback);
            };
        }

        public MenuItem<bool> Smart { get; set; }

        private void Tost()
        {
            var inAction = _main.Invoker.Mode.CanExecute;
            if (inAction && Game.IsKeyDown(0x11))
            {
                Invoke2();
            }
        }
        private void CustomHotkeyLooper()
        {
            var inAction = _main.Invoker.Mode.CanExecute;
            if (!inAction)
            {
                try
                {
                    Invoke2();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                
            }
        }

        private void ItemOnValueChanged(object sender, OnValueChangeEventArgs args)
        {
            if (args.GetNewValue<KeyBind>().Active)
            {
                UpdateManager.Subscribe(CustomHotkeyLooper, 100);
                /*
                if (!Enable)
                    UpdateManager.BeginInvoke(Callback);*/
            }
            else
            {
                UpdateManager.Unsubscribe(CustomHotkeyLooper);
            }
        }

        public MenuItem<KeyBind> CustomKey { get; set; }

        private void Invoke2()
        {
            var me = _main.Invoker.Owner;
            if (!me.CanCast())
                return;
            var selectedComboId = _main.Invoker.SelectedCombo;
            var combo = _main.ComboPanel.Combos[selectedComboId];
            var abilities = combo.AbilityInfos;
            /*var one = abilities[0].Ability is Item ? abilities[1] : abilities[0];
            var two = abilities[0].Ability is Item ? abilities[2] : abilities[1];*/
            var one =
                abilities.First(x => !(x.Ability is Item) && _main.AbilitiesInCombo.Value.IsEnabled(x.Ability.Name));
            var two =
                abilities.First(
                    x =>
                        !(x.Ability is Item) && _main.AbilitiesInCombo.Value.IsEnabled(x.Ability.Name) && !one.Equals(x));
            var three =
                abilities.First(
                    x =>
                        !(x.Ability is Item) && _main.AbilitiesInCombo.Value.IsEnabled(x.Ability.Name) && !one.Equals(x) && !two.Equals(x));
            if (Smart && three != null)
                two = three;
            var empty1 = _main.Invoker.Owner.Spellbook.Spell4;
            var empty2 = _main.Invoker.Owner.Spellbook.Spell5;
            //Console.WriteLine($"One: {one.Name} Two: {two.Name} Three: [{three?.Name}]");
            //Console.WriteLine($"empty1: {empty1.Name} empty2: {empty2.Name}");
            var ability1Invoked = one.Ability.Equals(empty1) || one.Ability.Equals(empty2);
            var ability2Invoked = two.Ability.Equals(empty1) || two.Ability.Equals(empty2);
            if (ability1Invoked && ability2Invoked)
            {
                if (!Smart) return;
                if (one.Ability.Equals(empty1))
                {
                    _main.Invoker.Invoke(two);
                }
                else if (two.Ability.Equals(empty2))
                {
                    _main.Invoker.Invoke(one);
                }
                return;
            }
            if (ability1Invoked)
            {
                _main.Invoker.Invoke(one.Ability.Equals(empty2) ? one : two);
            }
            else if (ability2Invoked)
            {
                _main.Invoker.Invoke(two.Ability.Equals(empty2) ? two : one);
            }
            else
            {
                _main.Invoker.Invoke(one);
            }
        }

        private async Task Invoke()
        {
            var me = _main.Invoker.Owner;
            if (!me.CanCast())
                return;
            var selectedComboId = _main.Invoker.SelectedCombo;
            var combo = _main.ComboPanel.Combos[selectedComboId];
            var abilities = combo.AbilityInfos;
            /*var one = abilities[0].Ability is Item ? abilities[1] : abilities[0];
            var two = abilities[0].Ability is Item ? abilities[2] : abilities[1];*/
            var one =
                abilities.First(x => !(x.Ability is Item) && _main.AbilitiesInCombo.Value.IsEnabled(x.Ability.Name));
            var two =
                abilities.First(
                    x =>
                        !(x.Ability is Item) && _main.AbilitiesInCombo.Value.IsEnabled(x.Ability.Name) && !one.Equals(x));
            var three =
                abilities.First(
                    x =>
                        !(x.Ability is Item) && _main.AbilitiesInCombo.Value.IsEnabled(x.Ability.Name) && !one.Equals(x) && !two.Equals(x));
            if (Smart && three != null)
                two = three;
            var empty1 = _main.Invoker.Owner.Spellbook.Spell4;
            var empty2 = _main.Invoker.Owner.Spellbook.Spell5;
            var ability1Invoked = one.Ability.Equals(empty1) || one.Ability.Equals(empty2);
            var ability2Invoked = two.Ability.Equals(empty1) || two.Ability.Equals(empty2);
            if (ability1Invoked && ability2Invoked)
                return;
            if (ability1Invoked)
            {
                if (one.Ability.Equals(empty2))
                    await _main.Invoker.InvokeAsync(one);
                else
                    await _main.Invoker.InvokeAsync(two);
            }
            else if (ability2Invoked)
            {
                if (two.Ability.Equals(empty2))
                    await _main.Invoker.InvokeAsync(two);
                else
                    await _main.Invoker.InvokeAsync(one);
            }
            else
            {
                await _main.Invoker.InvokeAsync(one);
            }
        }

        public void OnDeactivate()
        {

        }

        public MenuItem<bool> Enable { get; set; }
    }
}