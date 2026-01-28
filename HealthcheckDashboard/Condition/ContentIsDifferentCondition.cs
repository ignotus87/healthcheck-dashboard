using System;

namespace HealthcheckDashboard.ConditionNS
{
    // Compares string results between subsequent runs and reports true when the result changed
    public class ContentIsDifferentCondition : ICondition
    {
        private string _lastValue = null;
        public string ContentFilePath { get; }
        public WarnWhen WarnWhen { get; }

        public ContentIsDifferentCondition(string contentFilePath, WarnWhen warnWhen = WarnWhen.changes)
        {
            ContentFilePath = contentFilePath;
            WarnWhen = warnWhen;
        }

        // Returns true when the incoming value differs from the previously seen value.
        // The first invocation (when _lastValue == null) returns false.
        public bool EvaluateCondition(string parameter)
        {
            var changed = _lastValue != null && _lastValue != parameter;
            _lastValue = parameter;
            return changed;
        }

        bool ICondition.EvaluateCondition(object parameter)
        {
            if (parameter is string s)
                return EvaluateCondition(s);
            throw new ArgumentException($"Expected parameter of type {nameof(String)}");
        }

        public override string ToString()
        {
            return nameof(ContentIsDifferentCondition)
                + ": ContentFilePath=" + (ContentFilePath ?? "<null>")
                + " (WarnWhen: " + WarnWhen + ")";
        }
    }
}