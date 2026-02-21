using System.IO;
using IPA.Utilities;
using SaberFactory2.Helpers;

namespace SaberFactory2
{
    public class PluginDirectories
    {
        private const string CustomSabersDirName = "CustomSabers";
        public DirectoryInfo CustomSaberDir;
        public DirectoryInfo PresetDir;
        public DirectoryInfo SaberFactoryDir;
        public DirectoryInfo Cache;
        public PluginDirectories()
        {
            var baseDir = new DirectoryInfo(UnityGame.InstallPath);
            var userDataDir = new DirectoryInfo(UnityGame.UserDataPath);
            SaberFactoryDir = userDataDir.CreateSubdirectory("Saber Factory 2");
            PresetDir = SaberFactoryDir.CreateSubdirectory("Presets");
            Cache = SaberFactoryDir.CreateSubdirectory("Cache");
            CustomSaberDir = baseDir.GetDirectory(CustomSabersDirName);
            if (!CustomSaberDir.Exists)
            {
                CustomSaberDir = SaberFactoryDir.GetDirectory(CustomSabersDirName);
                PathTools.RelativeExtension = Path.Combine(userDataDir.Name, SaberFactoryDir.Name) + Path.DirectorySeparatorChar;
            }
            if (!CustomSaberDir.Exists)
            {
                CustomSaberDir = userDataDir.GetDirectory(CustomSabersDirName);
                PathTools.RelativeExtension = userDataDir.Name + Path.DirectorySeparatorChar;
            }
            if (!CustomSaberDir.Exists)
            {
                CustomSaberDir = baseDir.CreateSubdirectory(CustomSabersDirName);
                PathTools.RelativeExtension = null;
            }
        }
    }
}