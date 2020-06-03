using SRTPluginBase;
using System;

namespace SRTPluginUIRE3WinForms
{
    internal class PluginInfo : IPluginInfo
    {
        public string Name => "WinForms UI (Resident Evil 3 (2020))";

        public string Description => "A WinForms-based User Interface for displaying Resident Evil 3 (2020) game memory values.";

        public string Author => "Squirrelies";

        public Uri MoreInfoURL => new Uri("https://github.com/Squirrelies/SRTPluginUIRE3WinForms");

        public int VersionMajor => assemblyFileVersion.ProductMajorPart;

        public int VersionMinor => assemblyFileVersion.ProductMinorPart;

        public int VersionBuild => assemblyFileVersion.ProductBuildPart;

        public int VersionRevision => assemblyFileVersion.ProductPrivatePart;

        private System.Diagnostics.FileVersionInfo assemblyFileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
    }
}
