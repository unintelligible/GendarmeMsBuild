using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace GendarmeMsBuild
{
    public class Gendarme : Task
    {
        private string _gendarmeExeFilename = "C:\\program Files\\gendarme\\gendarme.exe";
        public string GendarmeExeFilename
        {
            get { return _gendarmeExeFilename; }
            set { _gendarmeExeFilename = value; }
        }

        [Required]
        public ITaskItem[] Assemblies { get; set; }

        public string GendarmeConfigFilename { get; set; }
        public string Ruleset { get; set; }
        public string GendarmeIgnoreFilename { get; set; }
        public string Severity { get; set; }
        public string Confidence { get; set; }
        public int Limit { get; set; }
        public string OutputXmlPath { get; set; }

        public override bool Execute()
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
            if (Assemblies.Length == 0)
            {
                Log.LogError("No .dll files found to run Gendarme against in " + Assemblies);
                return false;
            }

            var thisOutputFile = OutputXmlPath;
            var isUsingTempFile = false;
            if (string.IsNullOrEmpty(thisOutputFile))
            {
                thisOutputFile = Path.GetTempFileName();
                isUsingTempFile = true;
            }
            Log.LogMessage("output file: " + thisOutputFile);
            try
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
                {
                    sb.Append(" --ignore \"").Append(GendarmeIgnoreFilename).Append('"');
                }
                sb.Append(" --quiet");
                sb.Append(" --xml \"").Append(thisOutputFile).Append('"');
                foreach (var assembly in Assemblies)
                    sb.Append(" \"").Append(assembly.ItemSpec).Append('"');

                Log.LogMessage("Command line arguments: " + sb.ToString());
                var processInfo = new ProcessStartInfo(_gendarmeExeFilename, sb.ToString()) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true};
                var sw = new Stopwatch();
                sw.Start();
                var proc = Process.Start(processInfo);
                var stdErr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                var exitCode = proc.ExitCode;
                sw.Stop();
                Log.LogMessage("Finished running Gendarme in " + sw.ElapsedMilliseconds + "ms");
                if (exitCode != 0)
                {
                    if(stdErr.Length > 0)
                        Log.LogError(stdErr);
                    else
                    {
                        Log.LogError(ParseGendarmeOutputXml(thisOutputFile));
                    }
                    // Log.LogError("stdOut: " + stdOut);
                    return false;
                }
                return true;
            }
            finally
            {
                if(isUsingTempFile)
                try { File.Delete(thisOutputFile); }
                catch { /* do nothing */}
            }
        }

        class GendarmeOutput
        {
            public string Name;
            public string Problem;
            public string Solution;
            public int ViolationCount;
            public IEnumerable<string> ViolationTargets;
        }

        private static string ParseGendarmeOutputXml(string outputFile)
        {
            var xdoc = XDocument.Load(outputFile);
            var q = from ruleN in xdoc.Root.Elements("results").First().Elements("rule")
                    select new GendarmeOutput
                    {
                        Name = ruleN.Attribute("Uri").Value.Substring(ruleN.Attribute("Uri").Value.LastIndexOf('/') + 1).Replace('#', '.'),
                        Problem = ruleN.Elements("problem").First().Value,
                        Solution = ruleN.Elements("solution").First().Value,
                        ViolationCount = ruleN.Descendants("defect").Count(),
                        ViolationTargets = ruleN.Descendants("defect").Select(t => t.Attribute("Location").Value)
                    };
            var sb = new StringBuilder();
            sb.Append("Found ").Append(q.Aggregate(0, (acc, go) => acc + go.ViolationCount)).AppendLine(" Gendarme violations");
            sb.AppendLine();
            foreach (var n in q)
            {
                sb.Append("Rule: ").AppendLine(n.Name);
                sb.Append("Problem: ").AppendLine(n.Problem);
                sb.Append("Solution: ").AppendLine(n.Solution);
                sb.AppendLine("Error locations: ");
                foreach (var t in n.ViolationTargets)
                    sb.Append("  * ").AppendLine(t);
            }
            return sb.ToString();
        }

    }
}
    