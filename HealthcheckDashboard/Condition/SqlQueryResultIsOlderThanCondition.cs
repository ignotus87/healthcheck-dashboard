using System;

using System;

namespace HealthcheckDashboard.ConditionNS
{
    // Evaluates a DateTime (SQL query result). Returns true when the supplied DateTime is older than the configured timespan.
    public class SqlQueryResultIsOlderThanCondition : ICondition<DateTime>
    {
        public TimeSpan LimitTimespan { get; }
        public DateTime OlderThanDate => DateTime.Now.AddTicks(-LimitTimespan.Ticks);
        public bool WarnWhen { get; }
        public int HowOldInMinutes;
        public bool EvaluationResult { get; private set; }

        public SqlQueryResultIsOlderThanCondition(TimeSpan limitTimespan, bool warnWhen)
        {
            LimitTimespan = limitTimespan;
            WarnWhen = warnWhen;
        }

        // Returns true when the value is older than the configured limit (i.e. stale).
        public bool EvaluateCondition(DateTime queryResult)
        {
            HowOldInMinutes = (int)(DateTime.Now - queryResult).TotalMinutes;
            return EvaluationResult = queryResult < OlderThanDate;
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
                return $"SQL result is stale. Last value {HowOldInMinutes} minutes old (threshold = {LimitTimespan}).";
            }
            else
            {
                return $"SQL result is fresh (last value {HowOldInMinutes} minutes ago).";
            }
        }
    }
}