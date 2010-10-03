using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace GendarmeMsBuild
{
    public class Gendarme : Task
    {
        private string _gendarmeExeFilename = Path.Combine(Path.Combine(ProgramFilesx86(), "Gendarme"), "gendarme.exe");
        private static readonly Regex LineRegex = new Regex("\\(≈\\d+\\)");

        #region Task Properties
        /// <summary>
        /// The path to Gendarme.exe. Defaults to C:\program Files\gendarme\gendarme.exe (or C:\program files (x86)\gendarme\gendarme.exe on 64bit systems) if no value is supplied.
        /// </summary>
        public string GendarmeExeFilename
        {
            get { return _gendarmeExeFilename; }
            set { _gendarmeExeFilename = value; }
        }

        /// <summary>
        /// The assemblies to inspect. Multiple files and masks ('?', '*') are supported. Required.
        /// </summary>
        [Required]
        public ITaskItem[] Assemblies { get; set; }

        /// <summary>
        /// The path to the Gendarme config file. Maps to --config [filename] (optional)
        /// </summary>
        public string GendarmeConfigFilename { get; set; }
        /// <summary>
        /// The name of the ruleset to be used. Maps to --ruleset [set] (optional)
        /// </summary>
        public string Ruleset { get; set; }
        /// <summary>
        /// The path to the Gendarme ignore file. Maps to --ignore [filename] (optional)
        /// </summary>
        public string GendarmeIgnoreFilename { get; set; }
        /// <summary>
        /// The inspection severity. Maps to --severity [all | audit[+] | low[+|-] | medium[+|-] | high[+|-] | critical[-]] (optional)
        /// </summary>
        public string Severity { get; set; }
        /// <summary>
        /// The confidence level defects are filtered by. Maps to --confidence [all | low[+] | normal[+|-] | high[+|-] | total[-]] (optional)
        /// </summary>
        public string Confidence { get; set; }
        /// <summary>
        /// Limit the amount of defects found. Maps to --limit [value] (optional)
        /// </summary>
        public int? Limit { get; set; }
        /// <summary>
        /// The path to save Gendarme's output XML (optional)
        /// </summary>
        public string OutputXmlFilename { get; set; }
        /// <summary>
        /// Output minimal info. Maps to --quiet. Also causes the MSBuild task to output no info (optional). Ignored when Visual Studio integration is enabled.
        /// </summary>
        public bool? Quiet { get; set; }
        /// <summary>
        /// Output verbose info. Maps to --verbose (optional). Ignored when Visual Studio integration is enabled.
        /// </summary>
        public bool? Verbose { get; set; }
        /// <summary>
        /// Whether or not to fail the build if defects are found. Defaults to false. Useful when only the 
        /// output XML is required. Ignored when Visual Studio integration is enabled.
        /// </summary>
        [Obsolete("use WarningsAsErrors instead")]
        public bool DefectsCauseFailure
        {
            get { return WarningsAsErrors; }
            set { WarningsAsErrors = value; }
        }

        /// <summary>
        /// Whether to consider defects (which are normally treated as warnings) as errors. When set to true, 
        /// the build will fail if any defects are found. Optional.
        /// </summary>
        public bool WarningsAsErrors { get; set; }

        /// <summary>
        /// Whether or not to format the output in a format Visual Studio can understand. Defaults to false (optional)
        /// </summary>
        public bool IntegrateWithVisualStudio { get; set; }
        #endregion

        /// <summary>
        /// Execute the MSBuild task
        /// </summary>
        /// <returns>True if no defects are found, false otherwise.</returns>
        public override bool Execute()
        {
            if (!VerifyProperties()) return false;

            var thisOutputFile = OutputXmlFilename;
            var isUsingTempFile = false;
            if (string.IsNullOrEmpty(thisOutputFile))
            {
                thisOutputFile = Path.GetTempFileName();
                isUsingTempFile = true;
            }
            else
            {
                if(File.Exists(thisOutputFile))
                    try
                    {
                        File.Delete(thisOutputFile);
                    } catch(Exception e)
                    {
                        Log.LogErrorFromException(new Exception("Couldn't recreate output file " + thisOutputFile, e));
                        return false;
                    }
            }
            MaybeLogMessage("output file: " + thisOutputFile);

            try
            {
                var commandLineArguments = BuildCommandLineArguments(thisOutputFile);

                MaybeLogMessage("GendarmeMsBuild - command line arguments to Gendarme: " + commandLineArguments);
                var processInfo = new ProcessStartInfo(_gendarmeExeFilename, commandLineArguments) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
                var sw = new Stopwatch();
                sw.Start();
                var proc = Process.Start(processInfo);
                var stdErr = proc.StandardError.ReadToEnd();
                var stdOut = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                var exitCode = proc.ExitCode;
                sw.Stop();
                MaybeLogMessage("GendarmeMsBuild - finished running Gendarme in " + sw.ElapsedMilliseconds + "ms");
                if (exitCode != 0)
                {
                    if (stdErr.Length > 0)
                    {
                        // problem with the call out to Gendarme
                        Log.LogError(stdErr);
                        return false;
                    }
                    else
                    {
                        if (!IntegrateWithVisualStudio && !string.IsNullOrEmpty(stdOut))
                            Log.LogMessage(stdOut);

                        CreateVisualStudioOutput(thisOutputFile);
                        return !WarningsAsErrors;
                    }
                }
                if (!IntegrateWithVisualStudio && !string.IsNullOrEmpty(stdOut))
                    Log.LogMessage(stdOut);
                return true;
            }
            finally
            {
                if (isUsingTempFile)
                    try { File.Delete(thisOutputFile); }
                    catch { /* do nothing */}
            }
        }

        #region helper methods/classes
        private string BuildCommandLineArguments(string thisOutputFile)
        {
            var sb = new StringBuilder();
            if (GendarmeConfigFilename != null)
                sb.Append(" --config ").Append('"').Append(GendarmeConfigFilename).Append('"');
            if (Ruleset != null)
                sb.Append(" --set ").Append('"').Append(Ruleset).Append('"');
            if (Severity != null)
                sb.Append(" --severity ").Append('"').Append(Severity).Append('"');
            if (Confidence != null)
                sb.Append(" --confidence ").Append('"').Append(Confidence).Append('"');
            if (GendarmeIgnoreFilename != null)
                sb.Append(" --ignore \"").Append(GendarmeIgnoreFilename).Append('"');
            if (Limit.HasValue)
                sb.Append(" --limit ").Append(Limit.Value.ToString());
            if (Quiet.HasValue && Quiet.Value)
                sb.Append(" --quiet");
            else if (Verbose.HasValue && Verbose.Value)
                sb.Append(" --verbose");
            sb.Append(" --xml \"").Append(thisOutputFile).Append('"');
            foreach (var assembly in Assemblies)
                sb.Append(" \"").Append(assembly.ItemSpec).Append('"');
            return sb.ToString();
        }

        private bool VerifyProperties()
        {
            if (!File.Exists(GendarmeExeFilename))
            {
                Log.LogError("Couldn't find gendarme.exe at " + GendarmeExeFilename);
                return false;
            }
            if (!string.IsNullOrEmpty(GendarmeIgnoreFilename) && !File.Exists(GendarmeIgnoreFilename))
            {
                Log.LogError("Couldn't find the Gendarme ignore file at " + GendarmeExeFilename);
                return false;
            }
            if (Assemblies.Length == 0 || !Assemblies.Any(ti => ti.ItemSpec != null && ti.ItemSpec.ToLower().EndsWith(".dll")))
            {
                Log.LogError("No .dll files found to run Gendarme against in " + Assemblies);
                return false;
            }
            return true;
        }

        class GendarmeDefect
        {
            public string RuleName;
            public string Problem;
            public string Solution;
            public string Source;
            public string Target;
        }

        private void CreateVisualStudioOutput(string outputFile)
        {
            var xdoc = XDocument.Load(outputFile);
            var q = from defect in xdoc.Root.Descendants("defect")
                    let rule = defect.Parent.Parent
                    select new GendarmeDefect
                       {
                           RuleName = rule.Attribute("Uri").Value.Substring(rule.Attribute("Uri").Value.LastIndexOf('/') + 1).Replace('#', '.'),
                           Problem = rule.Element("problem").Value,
                           Solution = rule.Element("solution").Value,
                           Source = LineRegex.IsMatch(defect.Attribute("Source").Value) ? defect.Attribute("Source").Value : null,
                           Target = rule.Element("target").Attribute("Name").Value
                       };
            foreach (var defect in q)
            {
                if (defect.Source != null)
                {
                    var match = LineRegex.Match(defect.Source);
                    var lineNumber = int.Parse(match.Value.Substring(2, match.Value.Length - 3));
                    var sourceFile = defect.Source.Substring(0, defect.Source.IndexOf(match.Value));
                    LogDefect("[gendarme]", defect.RuleName, null, sourceFile, lineNumber, 1, 0, 0, defect.RuleName + ": " + defect.Problem);
                }
                else
                {
                    LogDefect("[gendarme]", defect.RuleName, null, null, 0, 0, 0, 0, defect.RuleName + ": " + defect.Target + ": " + defect.Problem);
                }
            }
        }

        /// <summary>
        /// Log a message to MSBuild if Visual Studio integration isn't enabled, and the Quiet option isn't set.
        /// </summary>
        /// <param name="message"></param>
        private void MaybeLogMessage(string message)
        {
            if (!IntegrateWithVisualStudio && (!Quiet.HasValue || !Quiet.Value))
                Log.LogMessage(message);
        }

        private void LogDefect(string subcategory, string errorCode, string helpKeyword, string file, int lineNumber,
                             int columnNumber, int endLineNumber, int endColumnNumber, string message, params string[] messageArgs)
        {
            if (WarningsAsErrors)
                Log.LogError(subcategory, errorCode, helpKeyword, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, messageArgs);
            else
                Log.LogWarning(subcategory, errorCode, helpKeyword, file, lineNumber, columnNumber, endLineNumber, endColumnNumber, message, messageArgs);
        }

        private void LogDefect(string message)
        {
            if (WarningsAsErrors)
                Log.LogError(message);
            else
                Log.LogWarning(message);
        }

        static string ProgramFilesx86()
        {
            if (8 == IntPtr.Size
                || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
            {
                return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            }

            return Environment.GetEnvironmentVariable("ProgramFiles");
        }
        #endregion
    }
}
    
