using System;

using System;

namespace HealthcheckDashboard.ConditionNS
{
    // Evaluates a DateTime (SQL query result). Returns true when the supplied DateTime is older than the configured timespan.
    public class SqlQueryDateTimeResultIsGreaterThanMillisCondition : ICondition<DateTime>
    {
        public TimeSpan LimitTimespan { get; }
        public DateTime CurrentUtcDate => DateTime.UtcNow;
        public bool WarnWhen { get; }
        public int DiffInMilliseconds;
        public bool EvaluationResult { get; private set; }

        public SqlQueryDateTimeResultIsGreaterThanMillisCondition(TimeSpan limitTimespan, bool warnWhen)
        {
            LimitTimespan = limitTimespan;
            WarnWhen = warnWhen;
        }

        // Returns true when the value differs greatly than the configured limit (i.e. stale).
        public bool EvaluateCondition(DateTime queryResult)
        {
            DiffInMilliseconds = (int)(DateTime.UtcNow - queryResult).TotalMilliseconds;
            return EvaluationResult = Math.Abs(DiffInMilliseconds) > LimitTimespan.TotalMilliseconds;
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
                return $"Time at SQL server differs by {TimeSpan.FromMilliseconds(DiffInMilliseconds)} (limit = {LimitTimespan}).";
            }
            else
            {
                return $"Time at SQL server is correct";
            }
        }
    }
}