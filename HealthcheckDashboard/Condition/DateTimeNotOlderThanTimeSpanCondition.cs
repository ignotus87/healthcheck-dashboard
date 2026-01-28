using System;

namespace HealthcheckDashboard.ConditionNS
{
    public class DateTimeNotOlderThanTimeSpanCondition : ICondition<DateTime>
    {
        public TimeSpan NotOlderThanTimespan { get; }
        public DateTime ActualValue => DateTime.Now.AddTicks(-NotOlderThanTimespan.Ticks);
        public WarnWhen WarnWhen { get; }

        public DateTimeNotOlderThanTimeSpanCondition(TimeSpan notOlderThanTimespan, WarnWhen warnWhen = WarnWhen.becomesFalse)
        {
            NotOlderThanTimespan = notOlderThanTimespan;
            WarnWhen = warnWhen;
        }

        public bool EvaluateCondition(DateTime parameter)
        {
            return parameter >= ActualValue;
        }

        public override string ToString()
        {
            return nameof(DateTimeNotOlderThanTimeSpanCondition)
                + ": " + NotOlderThanTimespan.ToString()
                + " which is: " + ActualValue
                + " (WarnWhen: " + WarnWhen + ")";
        }
    }
}
