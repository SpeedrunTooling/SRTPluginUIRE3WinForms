using SRTPluginBase;
using System;

namespace SRTPluginUIRE3WinForms
{
    internal class PluginInfo : IPluginInfo
    {
        public string Name => "WinForms UI";

        public string Description => "A WinForms-based User Interface for displaying game memory values.";

        public string Author => "Squirrelies";

        public Uri MoreInfoURL => new Uri("https://github.com/Squirrelies");

        public int VersionMajor => assemblyVersion.Major;

        public int VersionMinor => assemblyVersion.Minor;

        public int VersionBuild => assemblyVersion.Build;

        public int VersionRevision => assemblyVersion.Revision;

        private Version assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    }
}
