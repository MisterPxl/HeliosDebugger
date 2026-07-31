namespace HeliosDebugger
{
    public static class HeliosBootstrapPolicy
    {
        public static bool CanRun(
            HeliosDebuggerSettings settings,
            bool isEditor,
            bool isDebugBuild)
        {
            if (settings == null)
                return false;
            if (isEditor)
                return true;
            if (!isDebugBuild && !settings.AllowInReleaseBuild)
                return false;
            if (settings.DevelopmentBuildOnly && !isDebugBuild)
                return false;
            return true;
        }
    }
}
