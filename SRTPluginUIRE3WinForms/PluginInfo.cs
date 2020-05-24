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

        public int VersionMajor => assemblyVersion.Major;

        public int VersionMinor => assemblyVersion.Minor;

        public int VersionBuild => assemblyVersion.Build;

        public int VersionRevision => assemblyVersion.Revision;

        private Version assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    }
}
