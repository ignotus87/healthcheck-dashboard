using System;

namespace HealthcheckDashboard.ConditionNS
{
    public class DateTimeNotOlderThanTimeSpanCondition : ICondition<DateTime>
    {
        public TimeSpan NotOlderThanTimespan { get; }
        public DateTime ActualValue => DateTime.Now.AddTicks(-NotOlderThanTimespan.Ticks);
        public bool WarnWhen { get; }

        public DateTimeNotOlderThanTimeSpanCondition(TimeSpan notOlderThanTimespan, bool warnWhen)
        {
            NotOlderThanTimespan = notOlderThanTimespan;
            WarnWhen = warnWhen;
        }

        public bool EvaluateCondition(DateTime parameter)
        {
            return parameter >= ActualValue;
        }

        // explicit non-generic implementation forwards to typed method
        bool ICondition.EvaluateCondition(object parameter)
        {
            if (parameter is DateTime dt)
                return EvaluateCondition(dt);
            throw new ArgumentException($"Expected parameter of type {nameof(DateTime)}");
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
