using System;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills
{
    public enum EffectState
    {
        Created,
        Acting,
        Finishing,
        Finished
    }

    public class Effect
    {
        private object _lock = new object();
        private int _count;

        public uint Index { get; set; }
        public Skill Skill { get; set; }
        public EffectTemplate Template { get; set; }
        public Unit Caster { get; set; }
        public SkillCaster SkillCaster { get; set; }
        public BaseUnit Owner { get; set; }
        public EffectState State { get; set; }
        public bool InUse { get; set; }
        public int Duration { get; set; }
        public double Tick { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public Effect(BaseUnit owner, Unit caster, SkillCaster skillCaster, EffectTemplate template, Skill skill, DateTime time)
        {
            Owner = owner;
            Caster = caster;
            SkillCaster = skillCaster;
            Template = template;
            Skill = skill;
            StartTime = time;
            EndTime = DateTime.MinValue;
        }

        public void UpdateEffect()
        {
            Template.Start(Caster, Owner, this);
            if (Duration == 0)
                Duration = Template.GetDuration();
            if (StartTime == DateTime.MinValue)
            {
                StartTime = DateTime.Now;
                EndTime = StartTime.AddMilliseconds(Duration);
            }

            Tick = Template.GetTick();

            if (Tick > 0)
            {
                var time = GetTimeLeft();
                if (time > 0)
                    _count = (int) (time / Tick + 0.5f + 1);
                else
                    _count = -1;
                EffectTaskManager.Instance.AddDispelTask(this, Tick);
            }
            else
                EffectTaskManager.Instance.AddDispelTask(this, GetTimeLeft());
        }

        public void ScheduleEffect()
        {
            switch (State)
            {
                case EffectState.Created:
                {
                    State = EffectState.Acting;

                    Template.Start(Caster, Owner, this);

                    if (Duration == 0)
                        Duration = Template.GetDuration();
                    if (StartTime == DateTime.MinValue)
                    {
                        StartTime = DateTime.Now;
                        EndTime = StartTime.AddMilliseconds(Duration);
                    }

                    Tick = Template.GetTick();

                    if (Tick > 0)
                    {
                        var time = GetTimeLeft();
                        if (time > 0)
                            _count = (int) (time / Tick + 0.5f + 1);
                        else
                            _count = -1;
                        EffectTaskManager.Instance.AddDispelTask(this, Tick);
                    }
                    else
                        EffectTaskManager.Instance.AddDispelTask(this, GetTimeLeft());

                    return;
                }
                case EffectState.Acting:
                {
                    if (_count == -1)
                    {
                        if (Template.OnActionTime)
                        {
                            Template.TimeToTimeApply(Caster, Owner, this);
                            return;
                        }
                    }
                    else if (_count > 0)
                    {
                        _count--;
                        if (Template.OnActionTime && _count > 0)
                        {
                            Template.TimeToTimeApply(Caster, Owner, this);
                            return;
                        }
                    }

                    State = EffectState.Finishing;
                    break;
                }
            }

            if (State == EffectState.Finishing)
            {
                State = EffectState.Finished;
                InUse = false;
                StopEffectTask();
            }
        }

        public void Exit()
        {
            if (State == EffectState.Finished)
                return;
            if (State != EffectState.Created)
            {
                State = EffectState.Finishing;
                ScheduleEffect();
            }
            else
                State = EffectState.Finishing;
        }

        private void StopEffectTask()
        {
            lock (_lock)
            {
                Owner.Effects.RemoveEffect(this);
                Template.Dispel(Caster, Owner, this);
            }
        }

        public void SetInUse(bool inUse, bool update)
        {
            InUse = inUse;
            if (update)
                UpdateEffect();
            else if (inUse)
                ScheduleEffect();
            else if (State != EffectState.Finished)
                State = EffectState.Finishing;
        }

        public bool IsEnded()
        {
            return State == EffectState.Finished || State == EffectState.Finishing;
        }

        public double GetTimeLeft()
        {
            if (Duration == 0)
                return -1;
            var time = (long) (StartTime.AddMilliseconds(Duration) - DateTime.Now).TotalMilliseconds;
            return time > 0 ? time : 0;
        }

        public uint GetTimeElapsed()
        {
            var time = (uint) (DateTime.Now - StartTime).TotalMilliseconds;
            return time > 0 ? time : 0;
        }

        public void WriteData(PacketStream stream)
        {
            Template.WriteData(stream);
        }
    }
}