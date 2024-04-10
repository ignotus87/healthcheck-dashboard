using System;

namespace HealthcheckDashboard.ConditionNS
{
    public class DateTimeNotOlderThanTimeSpanCondition : ICondition<DateTime>
    {
        public TimeSpan NotOlderThanTimespan { get; }
        public DateTime ActualValue => DateTime.Now.AddTicks(-NotOlderThanTimespan.Ticks);

        public DateTimeNotOlderThanTimeSpanCondition(TimeSpan notOlderThanTimespan)
        {
            NotOlderThanTimespan = notOlderThanTimespan;
        }

        public bool EvaluateCondition(DateTime parameter)
        {
            return parameter >= ActualValue;
        }

        public override string ToString()
        {
            return nameof(DateTimeNotOlderThanTimeSpanCondition) + ": " + NotOlderThanTimespan.ToString() + " which is: " + ActualValue;
        }
    }
}
