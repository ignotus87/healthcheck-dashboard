using System;
using System.IO;
using System.Text.Json;

namespace HealthcheckDashboard.ConditionNS
{
    // Compares string results between subsequent runs and reports true when the result changed
    public class StringNotNullCondition : ICondition
    {
        private string _lastValue = null;
        public string Value { get; private set; }
        public bool WarnWhen { get; }
        public bool EvaluationResult { get; private set; }

        public StringNotNullCondition(bool warnWhen)
        {
            WarnWhen = warnWhen;
        }

        // Returns true when the incoming value differs from the previously saved value.
        // If `parameter` is JSON it will be formatted (pretty-printed) before comparison.
        public bool EvaluateCondition(string freshContent)
        {
            Value = freshContent;
            EvaluationResult = freshContent != null;

            _lastValue = freshContent;

            return EvaluationResult;
        }

        bool ICondition.EvaluateCondition(object parameter)
        {
            if (parameter == null)
            {
                return EvaluateCondition(null);
            }
            else if (parameter is string s0)
            {
                return EvaluateCondition(s0);
            }
            throw new ArgumentException($"Expected parameter of type {nameof(String)}");
        }

        public override string ToString()
        {
            if (EvaluationResult)
            {
                return "String is not null: " + Value;
            }
            else
            {
                return "String is null";
            }
        }
    }
}