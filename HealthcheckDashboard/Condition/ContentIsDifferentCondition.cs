using System;
using System.IO;
using System.Text.Json;

namespace HealthcheckDashboard.ConditionNS
{
    // Compares string results between subsequent runs and reports true when the result changed
    public class ContentIsDifferentCondition : ICondition
    {
        private string _lastValue = null;
        public string ContentFilePath { get; }
        public bool WarnWhen { get; }
        public bool EvaluationResult { get; private set; }
        public int FirstDiffAt { get; private set; }

        public ContentIsDifferentCondition(string contentFilePath, bool warnWhen)
        {
            ContentFilePath = contentFilePath;
            WarnWhen = warnWhen;
            FirstDiffAt = -1;
        }

        // Returns true when the incoming value differs from the previously saved value.
        // If `parameter` is JSON it will be formatted (pretty-printed) before comparison.
        public bool EvaluateCondition(string freshContent)
        {
            // Format JSON in parameter if possible
            var formattedFreshContent = FormatJsonIfPossible(freshContent);

            // Read saved content from file (may be null)
            var valueFromFile = ReadSavedContent();

            if (valueFromFile is null)
            {
                File.WriteAllText(ContentFilePath, formattedFreshContent ?? string.Empty, System.Text.Encoding.UTF8);
                return EvaluationResult = false;
            }

            // If file content is JSON too, normalize it the same way (optional; keeps comparisons consistent)
            var formattedFileValue = FormatJsonIfPossible(valueFromFile);

            // Compare normalized values
            if (formattedFileValue == null && formattedFreshContent == null)
            {
                EvaluationResult = false;
                FirstDiffAt = -1;
            }
            else if (formattedFileValue == null || formattedFreshContent == null)
            {
                EvaluationResult = true;
                FirstDiffAt = 0;
            }
            else
            {
                if (string.Equals(formattedFileValue, formattedFreshContent, StringComparison.Ordinal))
                {
                    EvaluationResult = false;
                    FirstDiffAt = -1;
                }
                else
                {
                    EvaluationResult = true;
                    FirstDiffAt = IndexOfFirstDifference(formattedFileValue, formattedFreshContent);
                }
            }

            _lastValue = formattedFreshContent;

            if (EvaluationResult)
            {
                File.WriteAllText(ContentFilePath + "_new", formattedFreshContent ?? string.Empty, System.Text.Encoding.UTF8);
            }

            return EvaluationResult;
        }

        private string ReadSavedContent()
        {
            if (string.IsNullOrEmpty(ContentFilePath) || !File.Exists(ContentFilePath))
                return null;

            try
            {
                return File.ReadAllText(ContentFilePath);
            }
            catch
            {
                // Treat read errors as missing content
                return null;
            }
        }

        private static string FormatJsonIfPossible(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var trimmed = text.TrimStart();
            if (!(trimmed.StartsWith("{") || trimmed.StartsWith("[")))
                return text;

            try
            {
                using var doc = JsonDocument.Parse(text);
                var options = new JsonSerializerOptions { WriteIndented = true };
                return JsonSerializer.Serialize(doc.RootElement, options);
            }
            catch
            {
                // Not valid JSON or serialization failed -> return original
                return text;
            }
        }

        private static int IndexOfFirstDifference(string a, string b)
        {
            var min = Math.Min(a.Length, b.Length);
            for (int i = 0; i < min; i++)
            {
                if (a[i] != b[i])
                    return i;
            }

            return a.Length != b.Length ? min : -1;
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
                + ": Content is " + (EvaluationResult ? "different from" : "same as") + " saved value"
                + (EvaluationResult ? " (FirstDiffAt=" + FirstDiffAt + ")" : "");
        }
    }
}