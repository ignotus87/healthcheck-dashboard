using System;

namespace HealthcheckDashboard.ConditionNS
{
    public class IntIsGreaterThanCondition : ICondition<int>
    {
        public int ValueInCondition { get; set; } = 0;
        public bool WarnWhen { get; }
        public int ValueReceived;
        public bool EvaluationResult { get; private set; }

        public IntIsGreaterThanCondition(int valueInCondition, bool warnWhen)
        {
            ValueInCondition = valueInCondition;
            WarnWhen = warnWhen;
        }

        // Returns true when the value is greater than the configured limit (i.e. query has rows that need attention).
        public bool EvaluateCondition(int queryResult)
        {
            ValueReceived = queryResult;
            return EvaluationResult = ValueReceived > ValueInCondition;
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
                return $"ValueReceived ({ValueReceived}) is greater than {ValueInCondition}.";
            }
            else
            {
                return $"ValueReceived ({ValueReceived}) is LTE {ValueInCondition}.";
            }
        }
    }
}