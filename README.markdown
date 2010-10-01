#Gendarme MsBuild Task
This is a simple MsBuild wrapper for [Gendarme](http://www.mono-project.com/Gendarme).

##Prerequisites
[Download and install Gendarme](http://www.mono-project.com/Gendarme#Download)

##Usage
###MSBuild
To call from an MsBuild project, the syntax is straightforward:

    <Project DefaultTargets="Test" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
        <UsingTask AssemblyFile="**Path\To\GendarmeMsBuild.dll**" TaskName="GendarmeMsBuild.Gendarme" />
        <Target Name="Test">
            <Gendarme
                OutputXmlFilename="$(MSBuildProjectDirectory)\\bin\\Debug\\test-output.xml"
                Assemblies="**Path\To\My\*.dll**"
                />
        </Target>
    </Project>

###Visual Studio
You can also integrate with Visual Studio, so that Gendarme is run as part of building your project. Simply add the following to the bottom of your .csproj file (just before the </Project> tag):

    <UsingTask AssemblyFile="$(SolutionDir)..\GendarmeMsBuild\bin\debug\GendarmeMsBuild.dll" TaskName="GendarmeMsBuild.Gendarme"/>
    <Target Name="AfterBuild">
        <Gendarme Assemblies="$(TargetPath)" IntegrateWithVisualStudio="True"/>
    </Target>
##Options
The following properties are supported on the task:

<table>
<tr>
<td>GendarmeExeFilename<td>
<td>The path to Gendarme.exe. Defaults to C:\program Files\gendarme\gendarme.exe (or C:\program files (x86)\gendarme\gendarme.exe on 64bit systems) if no value is supplied.</td>
</tr>
<tr>
<td>Assemblies<td>
<td>The assemblies to inspect. Multiple files and masks ('?', '*') are supported. Required.</td>
</tr>
<tr>
<td>GendarmeConfigFilename<td>
<td>The path to the Gendarme config file. Maps to --config [filename] (optional)</td>
</tr>
<tr>
<td>Ruleset<td>
<td>The name of the ruleset to be used. Maps to --ruleset [set] (optional)</td>
</tr>
<tr>
<td>GendarmeIgnoreFilename<td>
<td>The path to the Gendarme ignore file. Maps to --ignore [filename] (optional)</td>
</tr>
<tr>
<td>Severity<td>
<td>The inspection severity. Maps to --severity [all | audit[+] | low[+|-] | medium[+|-] | high[+|-] | critical[-]] (optional)</td>
</tr>
<tr>
<td>Confidence<td>
<td>The confidence level defects are filtered by. Maps to --confidence [all | low[+] | normal[+|-] | high[+|-] | total[-]] (optional)</td>
</tr>
<tr>
<td>Limit<td>
<td>Limit the amount of defects found. Maps to --limit [value] (optional)</td>
</tr>
<tr>
<td>OutputXmlFilename<td>
<td>The path to save Gendarme's output XML (optional)</td>
</tr>
<tr>
<td>Quiet<td>
<td>Output minimal info. Maps to --quiet. Also causes the MSBuild task to output no info (optional). Ignored when Visual Studio integration is enabled</td>
</tr>
<tr>
<td>Verbose<td>
<td>Output verbose info. Maps to --verbose (optional). Ignored when Visual Studio integration is enabled</td>
</tr>
<tr>
<td>DefectsCauseFailure<td>
<td>Whether or not to fail the build if defects are found. Defaults to false. Useful when only the output XML is required. Ignored when Visual Studio integration is enabled</td>
</tr>
<tr>
<td>IntegrateWithVisualStudio<td>
<td>Whether or not to format the output in a format Visual Studio can understand. Defaults to false (optional)</td>
</tr>
</table>

##Thanks
The [Gendarme team](http://github.com/mono/mono-tools/blob/master/gendarme/AUTHORS) for building such a useful and flexible tool.
