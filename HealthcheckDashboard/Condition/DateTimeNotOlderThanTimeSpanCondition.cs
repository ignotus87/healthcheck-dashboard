using System;

namespace HealthcheckDashboard.ConditionNS
{
    public class DateTimeNotOlderThanTimeSpanCondition : ICondition<DateTime>
    {
        public TimeSpan NotOlderThanTimespan { get; }
        public DateTime ShouldNotBeOlderThanDate => DateTime.Now.AddTicks(-NotOlderThanTimespan.Ticks);
        public bool WarnWhen { get; }
        public int HowOldInMinutes;
        public bool EvaluationResult;

        public DateTimeNotOlderThanTimeSpanCondition(TimeSpan notOlderThanTimespan, bool warnWhen)
        {
            NotOlderThanTimespan = notOlderThanTimespan;
            WarnWhen = warnWhen;
        }

        public bool EvaluateCondition(DateTime fileLastModifiedDate)
        {
            HowOldInMinutes = (int)(DateTime.Now - fileLastModifiedDate).TotalMinutes;
            return EvaluationResult = fileLastModifiedDate >= ShouldNotBeOlderThanDate;
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
            if (EvaluationResult)
            {
                return "File is fresh.";
            }
            else
            {
                return $"File is stale! Last modified {HowOldInMinutes} minutes ago.";
            }
        }
    }
}
