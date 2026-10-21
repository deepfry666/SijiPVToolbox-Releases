using System;
using System.Collections.Generic;

namespace StructuralToolbox.Update
{
    public static class UpdateSourceResolver
    {
        // Public GitHub release assets only; never place repository credentials here.
        public const string DefaultManifestUrl =
            "https://github.com/deepfry666/SijiPVToolbox-Releases/releases/latest/download/stable.json";

        public static string Resolve()
        {
            if (String.IsNullOrWhiteSpace(DefaultManifestUrl))
                return String.Empty;

            Uri uri;
            if (!Uri.TryCreate(DefaultManifestUrl, UriKind.Absolute, out uri))
                return String.Empty;
            return uri.Scheme == "https" ? uri.AbsoluteUri : String.Empty;
        }

        public static IList<string> ResolveAll()
        {
            List<string> result = new List<string>();
            AddHttps(result, DefaultManifestUrl);
            return result;
        }

        private static void AddHttps(IList<string> result, string value)
        {
            Uri uri;
            if (String.IsNullOrWhiteSpace(value) ||
                !Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                !String.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                return;
            foreach (string existing in result)
                if (String.Equals(existing, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)) return;
            result.Add(uri.AbsoluteUri);
        }
    }
}
