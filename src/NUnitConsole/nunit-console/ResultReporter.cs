﻿// ***********************************************************************
// Copyright (c) 2014 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Globalization;
using System.Xml;

namespace NUnit.ConsoleRunner
{
    using Options;
    using Utilities;

    public class ResultReporter
    {
        private XmlNode _result;
        private string _overallResult;
        private ConsoleOptions _options;
        private ResultSummary _summary;

        private int _reportIndex = 0;

        public ResultReporter(XmlNode result, ConsoleOptions options)
        {
            _result = result;

            _overallResult = result.GetAttribute("result");
            if (_overallResult == "Skipped")
                _overallResult = "Warning";

            _options = options;
            _summary = new ResultSummary(result);
        }

        public ResultSummary Summary
        {
            get { return _summary; }
        }

        /// <summary>
        /// Reports the results to the console
        /// </summary>
        public void ReportResults()
        {
            Console.WriteLine();

            if (_options.StopOnError && _summary.FailureCount + _summary.ErrorCount > 0)
            {
                ColorConsole.WriteLine(ColorStyle.Failure, "Execution terminated after first error");
                Console.WriteLine();
            }

            WriteSummaryReport();

            WriteAssemblyErrorsAndWarnings();

            if (_overallResult == "Failed")
                WriteErrorsAndFailuresReport();

            if (_summary.SkipCount + _summary.IgnoreCount > 0)
                WriteNotRunReport();
        }

        #region Summary Report

        private void WriteSummaryReport()
        {
            ColorStyle overall = _overallResult == "Passed"
                ? ColorStyle.Pass
                : _overallResult == "Failed" 
                    ? ColorStyle.Failure
                    : _overallResult == "Warning"
                        ? ColorStyle.Warning
                        : ColorStyle.Output;
            
            ColorConsole.WriteLine(ColorStyle.SectionHeader, "Test Run Summary");
            ColorConsole.WriteLabel("    Overall result: ", _overallResult, overall, true);

            ColorConsole.WriteLabel("   Tests run: ", _summary.RunCount.ToString(CultureInfo.CurrentUICulture), false);
            ColorConsole.WriteLabel(", Passed: ", _summary.PassCount.ToString(CultureInfo.CurrentCulture), false);
            ColorConsole.WriteLabel(", Errors: ", _summary.ErrorCount.ToString(CultureInfo.CurrentUICulture), false);
            ColorConsole.WriteLabel(", Failures: ", _summary.FailureCount.ToString(CultureInfo.CurrentUICulture), false);
            ColorConsole.WriteLabel(", Inconclusive: ", _summary.InconclusiveCount.ToString(CultureInfo.CurrentUICulture), true);

            var notRunTotal = _summary.SkipCount + _summary.IgnoreCount + _summary.InvalidCount;
            ColorConsole.WriteLabel("     Not run: ", notRunTotal.ToString(CultureInfo.CurrentUICulture), false);
            ColorConsole.WriteLabel(", Invalid: ", _summary.InvalidCount.ToString(CultureInfo.CurrentUICulture), false);
            ColorConsole.WriteLabel(", Ignored: ", _summary.IgnoreCount.ToString(CultureInfo.CurrentUICulture), false);
            ColorConsole.WriteLabel(", Skipped: ", _summary.SkipCount.ToString(CultureInfo.CurrentUICulture), true);

            var duration = _result.GetAttribute("duration", 0.0);
            var startTime = _result.GetAttribute("start-time", DateTime.MinValue);
            var endTime = _result.GetAttribute("end-time", DateTime.MaxValue);

            ColorConsole.WriteLabel("  Start time: ", startTime.ToString("u"), true);
            ColorConsole.WriteLabel("    End time: ", endTime.ToString("u"), true);
            ColorConsole.WriteLabel("    Duration: ", string.Format("{0} seconds", duration.ToString("0.000")), true);
            Console.WriteLine();
        }

        #endregion

        #region Assembly Errors and Warnings

        private void WriteAssemblyErrorsAndWarnings()
        {
            foreach (XmlNode node in _result.SelectNodes("test-suite[@type='Assembly']"))
            {
                if (node.GetAttribute("runstate") == "NotRunnable")
                    WriteAssemblyMessage(ColorStyle.Error, node.SelectSingleNode("properties/property[@name='_SKIPREASON']").GetAttribute("value"));
                else if (node.GetAttribute("total") == "0" || node.GetAttribute("testcasecount") == "0")
                    WriteAssemblyMessage(ColorStyle.Warning, "Warning: No tests found in " + node.GetAttribute("name"));
            }
        }

        private void WriteAssemblyMessage(ColorStyle style, string message)
        {
            ColorConsole.WriteLine(style, message);
            Console.WriteLine();
        }

        #endregion

        #region Errors and Failures Report

        private void WriteErrorsAndFailuresReport()
        {
            _reportIndex = 0;
            ColorConsole.WriteLine(ColorStyle.SectionHeader, "Errors and Failures");
            WriteErrorsAndFailures(_result);
            Console.WriteLine();
        }

        private void WriteErrorsAndFailures(XmlNode result)
        {
            string resultState = result.GetAttribute("result");

            switch (result.Name)
            {
                case "test-case":
                    if (resultState == "Failed")
                    {
                        using (new ColorConsole(ColorStyle.Failure))
                            WriteSingleResult(result);
                    }
                    return;

                case "test-run":
                    foreach (XmlNode childResult in result.ChildNodes)
                        WriteErrorsAndFailures(childResult);
                    break;

                case "test-suite":
                    if (resultState == "Failed")
                    {
                        if (result.GetAttribute("type") == "Theory")
                        {
                            using (new ColorConsole(ColorStyle.Failure))
                                WriteSingleResult(result);
                        }
                        else
                        {
                            var site = result.GetAttribute("site");
                            if (site == "SetUp" || site == "TearDown")
                                using (new ColorConsole(ColorStyle.Failure))
                                    WriteSingleResult(result);
                            if (site == "SetUp") return;
                        }
                    }
                    
                    foreach (XmlNode childResult in result.ChildNodes)
                        WriteErrorsAndFailures(childResult);

                    break;
            }
        }

        #endregion

        #region Not Run Report

        public void WriteNotRunReport()
        {
            _reportIndex = 0;
            ColorConsole.WriteLine(ColorStyle.SectionHeader, "Tests Not Run");
            WriteNotRunResults(_result);
            Console.WriteLine();
        }

        private void WriteNotRunResults(XmlNode result)
        {
            switch (result.Name)
            {
                case "test-case":
                    string status = result.GetAttribute("result");

                    if (status == "Skipped")
                    {
                        string label = result.GetAttribute("label");

                        var colorStyle = label == "Ignored" 
                            ? ColorStyle.Warning 
                            : ColorStyle.Output;

                        using (new ColorConsole(colorStyle))
                            WriteSingleResult(result);
                    }

                    break;

                case "test-suite":
                case "test-run":
                    foreach (XmlNode childResult in result.ChildNodes)
                        WriteNotRunResults(childResult);

                    break;
            }
        }

        #endregion

        #region Helper Methods

        private void WriteSingleResult(XmlNode result)
        {
            string status = result.GetAttribute("label");
            if (status == null)
                status = result.GetAttribute("result");

            if (status == "Failed" || status == "Error")
            {
                var site = result.GetAttribute("site");
                if (site == "SetUp" || site == "TearDown")
                    status = site + " " + status;
            }

            string fullName = result.GetAttribute("fullname");

            Console.WriteLine("{0}) {1} : {2}", ++_reportIndex, status, fullName);

            XmlNode failureNode = result.SelectSingleNode("failure");
            if (failureNode != null)
            {
                XmlNode message = failureNode.SelectSingleNode("message");
                XmlNode stacktrace = failureNode.SelectSingleNode("stack-trace");

                if (message != null)
                    Console.WriteLine(message.InnerText);

                if (stacktrace != null)
                    Console.WriteLine(stacktrace.InnerText + Environment.NewLine);
            }

            XmlNode reasonNode = result.SelectSingleNode("reason");
            if (reasonNode != null)
            {
                XmlNode message = reasonNode.SelectSingleNode("message");

                if (message != null)
                    Console.WriteLine(message.InnerText);
            }
        }

        #endregion
    }
}
