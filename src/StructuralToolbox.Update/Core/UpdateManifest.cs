using System;
using System.Collections.Generic;

namespace StructuralToolbox.Update
{
    public sealed class UpdateManifest
    {
        public const string ProductName = "FourJTooLs.StructuralToolbox";

        public UpdateManifest()
        {
            Product = ProductName;
            Channel = "stable";
            SizeBytes = -1L;
            Notes = new List<string>();
            PackageUrls = new List<Uri>();
        }

        public string Product { get; set; }
        public string Channel { get; set; }
        public Version Version { get; set; }
        public Uri PackageUrl { get; set; }
        public IList<Uri> PackageUrls { get; private set; }
        public string Sha256 { get; set; }
        public long SizeBytes { get; set; }
        public string ReleaseDate { get; set; }
        public IList<string> Notes { get; set; }

        public bool IsNewerThan(Version currentVersion)
        {
            return VersionComparer.IsNewerThan(Version, currentVersion);
        }
    }
}
