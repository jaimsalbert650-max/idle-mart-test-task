using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace IdleMart.EditorTools
{
    /// <summary>
    /// Writes a short summary of every Test Runner run to Logs/test-summary.txt.
    /// Re-registers after each domain reload, so PlayMode runs are captured too (handy for CI or automation).
    /// </summary>
    [InitializeOnLoad]
    public static class TestResultsLogger
    {
        private const string OutputPath = "Logs/test-summary.txt";

        static TestResultsLogger()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        }

        [MenuItem("Idle Mart/Run PlayMode Tests", priority = 30)]
        public static void RunPlayMode() =>
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode }));

        private sealed class Callbacks : ICallbacks
        {
            private readonly StringBuilder _failures = new StringBuilder();

            public void RunStarted(ITestAdaptor testsToRun) => _failures.Clear();

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText(OutputPath,
                    $"{result.Test.Name}: {result.ResultState} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}\n{_failures}");
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                    _failures.AppendLine($"FAIL {result.FullName}: {result.Message}");
            }
        }
    }
}
