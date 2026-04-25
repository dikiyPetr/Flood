using System.Collections.Generic;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;

namespace Editor
{
    public sealed class FloorTestsResult
    {
        public bool IsFinished;
        public int Total;
        public int Passed;
        public int Failed;
        public List<string> FailureMessages = new List<string>();
    }

    public sealed class FloorTestsCallback : ICallbacks
    {
        public FloorTestsResult Result { get; } = new FloorTestsResult();

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor results)
        {
            Result.IsFinished = true;
            Result.Total = results.PassCount + results.FailCount;
            Result.Passed = results.PassCount;
            Result.Failed = results.FailCount;
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren)
            {
                return;
            }

            if (result.TestStatus == TestStatus.Failed || result.TestStatus == TestStatus.Inconclusive)
            {
                Result.FailureMessages.Add($"{result.FullName}: {result.Message}");
            }
        }
    }

    public static class FloorTestsRunner
    {
        public static FloorTestsCallback LastCallback { get; private set; }

        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "Floor.Tests" }
            };
            LastCallback = new FloorTestsCallback();
            api.RegisterCallbacks(LastCallback);
            api.Execute(new ExecutionSettings(filter));
        }
    }
}
