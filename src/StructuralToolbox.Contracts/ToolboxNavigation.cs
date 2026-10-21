using System;

namespace StructuralToolbox.Contracts
{
    public static class ToolboxNavigation
    {
        private static Action<string> _showModule;

        public static void Configure(Action<string> showModule)
        {
            _showModule = showModule;
        }

        public static void Reset()
        {
            _showModule = null;
        }

        public static bool ShowModule(string moduleId)
        {
            Action<string> handler = _showModule;
            if (handler == null || String.IsNullOrWhiteSpace(moduleId))
                return false;
            handler(moduleId);
            return true;
        }
    }
}
