using System;

namespace HealthcheckDashboard.ConditionNS
{
    // Non-generic base condition so the runtime can hold any condition instance
    public interface ICondition
    {
        // Evaluate with a runtime object (implementations should cast to their expected type)
        bool EvaluateCondition(object parameter);
        bool WarnWhen { get; }
    }

    // Generic convenience interface for typed conditions
    public interface ICondition<T> : ICondition where T : struct
    {
        bool EvaluateCondition(T parameter);
    }
}
