using System;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(CoffeeGame.Presentation.Tests.PartyTestProgress))]
namespace CoffeeGame.Presentation.Tests
{
    // Optional command-line trace for diagnosing a stalled Unity batch test run.
    public sealed class PartyTestProgress : ITestRunCallback
    {
        private static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-party-test-progress") >= 0;
        public void RunStarted(ITest testsToRun) { if (Enabled) Debug.Log("PARTY TEST RUN " + testsToRun.FullName); }
        public void TestStarted(ITest test) { if (Enabled) Debug.Log("PARTY TEST START " + test.FullName); }
        public void TestFinished(ITestResult result) { if (Enabled) Debug.Log("PARTY TEST END " + result.FullName + " " + result.ResultState); }
        public void RunFinished(ITestResult result) { if (Enabled) Debug.Log("PARTY TEST COMPLETE " + result.ResultState); }
    }
}
