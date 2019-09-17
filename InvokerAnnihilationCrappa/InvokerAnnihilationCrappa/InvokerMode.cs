using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.SDK.Helpers;
using Ensage.SDK.Orbwalker.Modes;
using Ensage.SDK.Service;
using log4net;
using PlaySharp.Toolkit.Logging;
using SharpDX;
using AbilityId = Ensage.AbilityId;

namespace InvokerAnnihilationCrappa
{
    public class InvokerMode : KeyPressOrbwalkingModeAsync
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Hero _target;

        public InvokerMode(Key key, IServiceContext context,
            Invoker me) : base(context, key)
        {
            Me = me;
        }

        /*public InvokerMode(Key key, Lazy<IOrbwalkerManager> orbwalkerManager, Lazy<IInputManager> input,
            Invoker me) : base(orbwalkerManager.Value, input.Value, key)
        {
            Me = me;
        }*/

        public Invoker Me { get; set; }

        public void Load()
        {
            UpdateManager.Subscribe(TargetUpdater, 25);
            UpdateManager.Subscribe(IndicatorUpdater);
            Drawing.OnDraw += args =>
            {
                if (Me.Config.DrawMinDistanceInOrbwalk && !Orbwalker.OrbwalkingPoint.IsZero && CanExecute)
                {
                    var pos = Drawing.WorldToScreen(Orbwalker.OrbwalkingPoint);
                    Drawing.DrawCircle(pos, 15, 15, Color.Aqua);
                }
            };
        }


        public static Vector3 Direction(double angle, float length = 1f)
        {
            var rotation = angle;
            return new Vector3((float) Math.Cos(rotation) * length, (float) Math.Sin(rotation) * length, 0);
        }

        private void IndicatorUpdater()
        {
            if (_target != null)
                Me.ParticleManager.Value.DrawDangerLine(Owner, "Invo_line", _target.Position);
        }

        public void Unload()
        {
            UpdateManager.Unsubscribe(TargetUpdater);
        }
        private void TargetUpdater()
        {
            if (!CanExecute && _target != null)
            {
                if (!Me.TargetManager.Value.IsActive)
                {
                    Me.TargetManager.Value.Activate();
                    Log.Info("re Enable _targetManager");
                }
                Cancel();
                _target = null;
                Orbwalker.OrbwalkingPoint = Vector3.Zero;
                Me.ParticleManager.Value.Remove("Invo_target");
                Me.ParticleManager.Value.Remove("Invo_line");
            }
            else if (CanExecute && _target != null)
            {
                if (Me.Config.AutoIceWall)
                {
                    var currentCombo = Me.Config.ComboPanel.Combos.First(x => x.Id == Me.SelectedCombo);
                    if (currentCombo.AbilityInfos[currentCombo.CurrentAbility].Ability.Id == AbilityId.invoker_ice_wall)
                        return;
                }
                if (Me.BlockerSleeper.Sleeping)
                    return;
                //TODO: attack here
                var distance = Owner.Distance2D(_target);
                if (distance < Me.Config.MinDisInOrbwalk)
                {
                    //var myPos = Owner.Position;

                    /*var pos = (targetPos - Game.MousePosition).Normalized();
                    pos *= Me.Config.MinDisInOrbwalk;
                    pos = targetPos - pos;
                    Orbwalker.OrbwalkingPoint = pos;*/
                    var mousePos = Game.MousePosition;
                    var targetPos = new Vector3(_target.Position.X, _target.Position.Y, mousePos.Z);
                    var ownerPosExtral = new Vector3(Owner.Position.X, Owner.Position.Y, mousePos.Z);
                    var ownerDis = Math.Min(Owner.Distance2D(mousePos), 300);
                    var ownerPos = ownerPosExtral.Extend(mousePos, ownerDis);
                    var pos = targetPos.Extend(ownerPos, Me.Config.MinDisInOrbwalk);

                    Orbwalker.OrbwalkingPoint = pos;
                    if (Orbwalker.CanAttack(_target) && !_target.IsAttackImmune() && !_target.IsInvul())
                    {
                        Orbwalker.OrbwalkTo(_target);
                    }
                    else
                    {
                        Orbwalker.OrbwalkTo(null);
                    }
                   
                }
                else
                {
                    Orbwalker.OrbwalkingPoint = Vector3.Zero;
                    if (_target.IsAttackImmune() || _target.IsInvul())
                    {
                        Orbwalker.Move(Game.MousePosition);
                    }
                    else //if (!Me.Config.ExtraDelayAfterSpells)
                    {
                        Orbwalker.OrbwalkTo(_target);
                    }
                }
            }
        }
        public void UpdateConfig(Config config)
        {
            Me.Config = config;
        }

        public override async Task ExecuteAsync(CancellationToken token)
        {
            //Console.WriteLine($"On Execute");
            if (Me.Config.Prepare.Enable && Game.IsKeyDown(0x11))
                return;
            if (_target == null || !_target.IsValid || !_target.IsAlive)
            {
                if (!Me.TargetManager.Value.IsActive)
                    Me.TargetManager.Value.Activate();
                _target = Me.TargetManager.Value.Active.GetTargets().FirstOrDefault() as Hero;
                if (_target != null)
                {
                    Log.Info("target detected");
                    var currentCombo = Me.Config.ComboPanel.Combos.First(x => x.Id == Me.SelectedCombo);
                    currentCombo.CurrentAbility = 0;
                    if (Me.TargetManager.Value.IsActive)
                        Me.TargetManager.Value.Deactivate();
                }
                else
                {
                    Log.Info("Cant find target");
                }
            }
            
            if (_target != null)
            {
                var currentCombo = Me.Config.ComboPanel.Combos.First(x => x.Id == Me.SelectedCombo);
                if (currentCombo.CurrentAbility < currentCombo.AbilityCount)
                {
                    var currentAbility = currentCombo.AbilityInfos[currentCombo.CurrentAbility];
                    if (Me.Config.AbilitiesInCombo.Value.IsEnabled(currentAbility.Ability.Name) || currentAbility.Ability is Item)
                    {
                        //Log.Warn($"Ability State: [{currentAbility.Ability.Name}] {currentAbility.Ability.AbilityState}");
                        if (currentAbility.Ability.AbilityState == AbilityState.Ready)
                        {
                            if (!currentAbility.Ability.CanBeCasted())
                            {
                                await Me.InvokeAsync(currentAbility);
                            }
                            else if (currentAbility.Ability.CanHit(_target) ||
                                     currentAbility.Ability.Id == AbilityId.invoker_chaos_meteor &&
                                     _target.HasModifier("modifier_invoker_cold_snap")
                                     /*Ensage.SDK.Extensions.EntityExtensions.Distance2D(Me.Owner, _target) <=
                                     currentAbility.Ability.CastRange*/||
                                     currentAbility.Ability.Id == AbilityId.invoker_ice_wall ||
                                     currentAbility.Ability.Id == AbilityId.item_refresher)
                            {
                                var casted = await currentAbility.UseAbility(_target, token);
                                if (casted)
                                {
                                    if (_target == null)
                                    {
                                        Log.Debug("target == null -> exit");
                                        return;
                                    }
                                    double tempShit;
                                    try
                                    {
                                        tempShit = Owner.GetTurnTime(_target.NetworkPosition) * 1000 + Game.Ping + 25;
                                    }
                                    catch (Exception)
                                    {
                                        Log.Debug("shit happened");
                                        return;
                                    }
                                    var turnTime = Math.Max(Game.Ping + 50, tempShit);
                                    Log.Info($"using: [{currentCombo.CurrentAbility}] ({(int)turnTime} ms)" + currentAbility.Ability.Id);
                                    if (currentAbility.Ability.Id == AbilityId.item_refresher)
                                    {
                                        
                                        var ssInCombo = currentCombo.AbilityInfos.FirstOrDefault(x =>
                                            x.Ability.Id == AbilityId.invoker_sun_strike);
                                        if (ssInCombo != null && Me.Config.Cataclysm)
                                        {
                                            var index = currentCombo.AbilityInfos.FindIndex(x => x.Ability.Id == AbilityId.invoker_sun_strike);
                                            var after3 = Math.Min(index == -1 ? 3 : index + 1, currentCombo.AbilityInfos.Count);
                                            currentCombo.CurrentAbility = after3 - 1 ;
                                        }
                                        else
                                        {
                                            currentCombo.CurrentAbility -= 2;
                                        }
                                        //DecComboStage(currentCombo, 2);
                                    }
                                    else
                                    {
                                        IncComboStage(currentCombo);
                                    }

                                    if (Me.Config.ExtraDelayAfterSpells)
                                        await Task.Delay((int) turnTime, token);
                                    else
                                        await Task.Delay((int) Game.Ping, token);
                                }
                                else
                                {
                                    Log.Info($"not casted: [{currentCombo.CurrentAbility}]" +
                                             currentAbility.Ability.Name);
                                }
                            }
                            else
                            {
                                //Log.Error($"something is wrong {currentAbility.Name}");
                                if (Me.Config.SmartMove && !currentAbility.Ability.CanHit(_target))
                                {
                                    Owner.Move(_target.Position);
                                }
                            }
                        }
                        else
                        {
                            Log.Info($"skip ability cuz cd: [{currentCombo.CurrentAbility}]" +
                                     currentAbility.Ability.Name);
                            IncComboStage(currentCombo);
                        }
                    }
                    else
                    {
                        Log.Info($"skip ability cuz disabled: [{currentCombo.CurrentAbility}]" +
                                     currentAbility.Ability.Name);
                        IncComboStage(currentCombo);
                    }
                }
                else
                {
                    Log.Info("end of combo. Set current ability stage to 0");
                    currentCombo.CurrentAbility = 0;
                }
                if (_target == null)
                {
                    return;
                }
                if (_target.IsAttackImmune() || _target.IsInvul())
                {
                    Orbwalker.Move(Game.MousePosition);
                }
                else
                {
                    var distance = Owner.Distance2D(_target);
                    if (distance < Me.Config.MinDisInOrbwalk)
                    {
                        //var myPos = Owner.Position;
                        /*var pos = (targetPos - Game.MousePosition).Normalized();
                        pos *= Me.Config.MinDisInOrbwalk;
                        pos = targetPos - pos;
                        Orbwalker.OrbwalkingPoint = pos;
                        Orbwalker.OrbwalkTo(null);*/

                        var mousePos = Game.MousePosition;
                        var targetPos = new Vector3(_target.Position.X, _target.Position.Y, mousePos.Z);
                        var ownerPosExtral = new Vector3(Owner.Position.X, Owner.Position.Y, mousePos.Z);
                        var ownerDis = Math.Min(Owner.Distance2D(mousePos), 300);
                        var ownerPos = ownerPosExtral.Extend(mousePos, ownerDis);
                        var pos = targetPos.Extend(ownerPos, Me.Config.MinDisInOrbwalk);

                        Orbwalker.OrbwalkingPoint = pos;
                        Orbwalker.OrbwalkTo(null);
                    }
                    else
                    {
                        Orbwalker.OrbwalkingPoint = Vector3.Zero;
                        Orbwalker.OrbwalkTo(_target);

                    }
                    
                    var others =
                        EntityManager<Unit>.Entities.Where(
                            x =>
                                x.IsAlive && x.IsControllable && x.Team == Owner.Team && !x.Equals(Owner) &&
                                x.NetworkName == ClassId.CDOTA_BaseNPC_Invoker_Forged_Spirit.ToString());
                    foreach (var other in others)
                    {
                        if (other.IsAttacking())
                            continue;
                        other.Attack(_target);
                    }
                    if (currentCombo.CurrentAbility > 1)
                    {
                        if (Me.Shiva != null && Me.Config.ItemsInCombo.Value.IsEnabled(Me.Shiva.Ability.Name) &&
                            Me.Shiva.CanBeCasted && Me.Owner.Distance2D(_target) <= Me.Shiva.Radius)
                        {
                            Me.Shiva.UseAbility();
                        }
                        if (Me.Hex != null && Me.Config.ItemsInCombo.Value.IsEnabled(Me.Hex.Ability.Name) &&
                            Me.Hex.CanBeCasted && Me.Hex.CanHit(_target))
                        {
                            float duration;
                            if (!_target.IsStunned(out duration) || duration <= 0.15f)
                                Me.Hex.UseAbility(_target);
                        }
                        if (Me.Orchid != null && Me.Config.ItemsInCombo.Value.IsEnabled(Me.Orchid.Ability.Name) &&
                            Me.Orchid.CanBeCasted && Me.Orchid.CanHit(_target))
                        {
                            Me.Orchid.UseAbility(_target);
                        }
                        if (Me.Bloodthorn != null && Me.Config.ItemsInCombo.Value.IsEnabled(Me.Bloodthorn.Ability.Name) &&
                            Me.Bloodthorn.CanBeCasted && Me.Bloodthorn.CanHit(_target))
                        {
                            Me.Bloodthorn.UseAbility(_target);
                        }
                    }
                    else
                    {
                        if (Me.Blink != null && Me.Config.ItemsInCombo.Value.IsEnabled(Me.Blink.Ability.Name) &&
                            Me.Blink.CanBeCasted && Me.Blink.CanHit(_target))
                            Me.Blink.UseAbility(_target.Position);
                    }
                }
                try
                {
                    Me.ParticleManager.Value.DrawRange(_target, "Invo_target", 125, Color.YellowGreen);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            else
            {
                Orbwalker.Move(Game.MousePosition);
            }
        }

        private void IncComboStage(Combo currentCombo)
        {
            if (currentCombo.CurrentAbility + 1 >= currentCombo.AbilityCount)
                currentCombo.CurrentAbility = 0;
            else
                currentCombo.CurrentAbility++;
        }
        private void DecComboStage(Combo currentCombo, int power)
        {
            if (power > 1)
                DecComboStage(currentCombo, power - 1);
            if (currentCombo.CurrentAbility - 1 <= currentCombo.AbilityCount)
                currentCombo.CurrentAbility = currentCombo.AbilityCount;
            else
                currentCombo.CurrentAbility--;
        }
    }
}