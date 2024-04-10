using System;

namespace HealthcheckDashboard.ConditionNS
{
    public interface ICondition<T> where T : struct
    {
        bool EvaluateCondition(T parameter);
    }
}
