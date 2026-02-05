using System;

using System;

namespace HealthcheckDashboard.ConditionNS
{
    // Evaluates a DateTime (SQL query result). Returns true when the supplied DateTime is older than the configured timespan.
    public class SqlQueryIntResultIsGreaterThanCondition : ICondition<int>
    {
        public int ValueInCondition { get; set; } = 0;
        public bool WarnWhen { get; }
        public int ValueFromQuery;
        public bool EvaluationResult { get; private set; }

        public SqlQueryIntResultIsGreaterThanCondition(int valueInCondition, bool warnWhen)
        {
            ValueInCondition = valueInCondition;
            WarnWhen = warnWhen;
        }

        // Returns true when the value is greater than the configured limit (i.e. query has rows that need attention).
        public bool EvaluateCondition(int queryResult)
        {
            ValueFromQuery = queryResult;
            return EvaluationResult = ValueFromQuery > ValueInCondition;
        }

        // explicit non-generic implementation forwards to typed method
        bool ICondition.EvaluateCondition(object parameter)
        {
            if (parameter is int number)
                return EvaluateCondition(number);
            throw new ArgumentException($"Expected parameter of type int");
        }

        public override string ToString()
        {
            if (EvaluationResult)
            {
                return $"SQL query has data: value: {ValueFromQuery} is greater than {ValueInCondition}.";
            }
            else
            {
                return $"SQL query does not contain data: value: {ValueFromQuery} is LTE {ValueInCondition}.";
            }
        }
    }
}