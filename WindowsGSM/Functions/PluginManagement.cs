﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsGSM.Functions
{
    public class PluginMetadata
    {
        public bool IsLoaded;
        public string GameImage, AuthorImage, FullName, FileName, Error;
        public Plugin Plugin;
        public Type Type;
    }

    class PluginManagement
    {
        public const string DefaultUserImage = "pack://application:,,,/Images/Plugins/User.png";
        public const string DefaultPluginImage = "pack://application:,,,/Images/WindowsGSM.png";

        public PluginManagement()
        {
            Directory.CreateDirectory(ServerPath.GetPlugins());
        }

        public async Task<List<PluginMetadata>> LoadPlugins(bool shouldAwait = true)
        {
            var plugins = new List<PluginMetadata>();
            foreach (var pluginFolder in Directory.GetDirectories(ServerPath.GetPlugins(), "*.cs", SearchOption.TopDirectoryOnly).ToList())
            {
                var pluginFile = Path.Combine(pluginFolder, Path.GetFileName(pluginFolder));
                if (File.Exists(pluginFile))
                {
                    PluginMetadata plugin = new PluginMetadata();
                    plugin.IsLoaded = false;
                    plugin.FileName = "NoValidPlugin";
                    plugin.FullName = "NoValidPlugin";
                    try
                    {
                        plugin = await LoadPlugin(pluginFile, shouldAwait);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("logs/pluginsImportError.log", $"error importing plugin {pluginFile}: {ex.Message}, {ex.StackTrace}");
                    }
                    if (plugin != null)
                    {
                        plugins.Add(plugin);
                    }
                }
            }
            
            return plugins;
        }

        public async Task<PluginMetadata> LoadPlugin(string path, bool shouldAwait = false)
        {
            var pluginMetadata = new PluginMetadata
            {
                FileName = Path.GetFileName(path)
            };

            var compiler = new RoslynCompiler($"WindowsGSM.Plugins.{Path.GetFileNameWithoutExtension(path)}", File.ReadAllText(path), new[] { typeof(Console), typeof(Console) }, pluginMetadata);
            var type = compiler.Compile();

            try
            {
                pluginMetadata.Type = shouldAwait ? await Task.Run(() => type.Assembly.GetType($"WindowsGSM.Plugins.{Path.GetFileNameWithoutExtension(path)}")) : type.Assembly.GetType($"WindowsGSM.Plugins.{Path.GetFileNameWithoutExtension(path)}");
                var plugin = GetPluginClass(pluginMetadata);
                pluginMetadata.FullName = $"{plugin.FullName} [{pluginMetadata.FileName}]";
                pluginMetadata.Plugin = plugin.Plugin;
                try
                {
                    string gameImage = ServerPath.GetPlugins(pluginMetadata.FileName, $"{Path.GetFileNameWithoutExtension(pluginMetadata.FileName)}.png");
                    ImageSource image = new BitmapImage(new Uri(gameImage));
                    pluginMetadata.GameImage = gameImage;
                }
                catch
                {
                    pluginMetadata.GameImage = DefaultPluginImage;
                }
                try
                {
                    string authorImage = ServerPath.GetPlugins(pluginMetadata.FileName, "author.png");
                    ImageSource image = new BitmapImage(new Uri(authorImage));
                    pluginMetadata.AuthorImage = authorImage;
                }
                catch
                {
                    pluginMetadata.AuthorImage = DefaultUserImage;
                }
                pluginMetadata.IsLoaded = true;
            }
            catch (Exception e)
            {
                pluginMetadata.Error = e.Message;
                Console.WriteLine(pluginMetadata.Error);
                File.AppendAllText("logs/pluginsImportError.log", $"error importing plugin {pluginMetadata.FileName}: {e.Message}, {e.StackTrace}");
                pluginMetadata.IsLoaded = false;
            }

            return pluginMetadata;
        }

        public static BitmapSource GetDefaultUserBitmapSource()
        {
            using (var stream = System.Windows.Application.GetResourceStream(new Uri(DefaultUserImage)).Stream)
            {
                return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
        }

        public static BitmapSource GetDefaultPluginBitmapSource()
        {
            using (var stream = System.Windows.Application.GetResourceStream(new Uri(DefaultPluginImage)).Stream)
            {
                return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
        }

        public static dynamic GetPluginClass(PluginMetadata plugin, ServerConfig serverConfig = null) => Activator.CreateInstance(plugin.Type, serverConfig);
    }
}
