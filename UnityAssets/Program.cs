using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetStudio;
using CommandLine;
using CommandLine.Text;
using GlobExpressions;


namespace UnityAssets
{
	class Program
	{
		private const int EXIT_SUCCESS = 0;
		private const int EXIT_ERROR = 1;

		class Options
		{
			[Option('r', "rootDir", Required = true, HelpText = "Game root directory. E.g.\n-r D:/Games/Plarium/PlariumPlay/StandAloneApps/raid")]
			public string RootDir { get; set; }

			[Option('a', "assetDir", Min = 1, Separator = ',', HelpText = "Match one or more asset directories. Separated by comma. Supports globs: E.g.\n-a resources,*/Raid_Data/StreamingAssets/AssetBundles")]
			public IEnumerable<string> AssetDir { get; set; }

			[Option('b', "bundleFiles", Min = 1, Separator = ',', HelpText = "Match one or more asset bundles to match. Separated by comma. Supports globs: E.g.\n-b *UIShared*,SkillIcons*")]
			public IEnumerable<string> Bundles { get; set; }

			[Option('o', "outputDir", Required = true, HelpText = "Location to write files to. E.g.\n-r D:/data")]
			public string OutputDir { get; set; }

		}

		static int RunAndReturnExitCode(Options options)
		{
			AssetsManager assetsManager = new AssetsManager();

			string dstFolder = options.OutputDir;
			string rootFolder = options.RootDir;
			string[] assetDirGlobs = options.AssetDir.ToArray();
			string[] assetGlobs = options.Bundles.ToArray();

			string[] assetFiles = FindFiles(rootFolder, assetDirGlobs, assetGlobs);

			if (assetFiles.Count() == 0)
			{
				Console.WriteLine("Your query returned no files. Booho!");
				return EXIT_ERROR;
			}

			assetsManager.LoadFiles(assetFiles);
			if (assetsManager.assetsFileList.Count == 0)
			{
				Console.WriteLine("No assets found. Booho!");
				return EXIT_ERROR;
			}

			var assets= BuildAssetData(assetsManager);

			foreach (AssetItem asset in assets)
			{
				//Console.WriteLine("Reading {0}", asset.Text);
				switch (asset.Type)
				{
					case ClassIDType.Sprite:
						if (!TryExportFile(dstFolder, asset, ".png", out var exportFullPath))
							continue;
						var stream = ((Sprite)asset.Asset).GetImage(ImageFormat.Png);
						if (stream != null)
						{
							using (stream)
							{
								Console.WriteLine("Saving {0}", asset.Text);
								File.WriteAllBytes(exportFullPath, stream.ToArray());
							}
						}
						break;
					default:
						break;
				}

			}

			return EXIT_SUCCESS;
		}

		static int Main(string[] args)
		{
			return Parser.Default.ParseArguments<Options>(args)
				.MapResult(RunAndReturnExitCode, _ => 1);
		}

		private static string[] FindFiles(string rootDir, string[] assetDirGlobs, string[] assetGlobs)
		{
			List<string> matchingDirectories = new List<string>();
			foreach (string assetDirGlob in assetDirGlobs)
			{
				matchingDirectories = matchingDirectories.Concat(Glob.Directories(rootDir, assetDirGlob)).ToList();
			}

			List<string> bundleDirs = new List<string>();
			foreach (string matchingDirectory in matchingDirectories)
			{
				string path = Path.Combine(rootDir, matchingDirectory);
				foreach (string assetGlob in assetGlobs)
				{
					var dirs = Glob.Directories(path, assetGlob);
					bundleDirs.AddRange(dirs.ToList().ConvertAll(dir => Path.Combine(path, dir)));
				}
			}

			List<string> bundleFiles = new List<string>();
			foreach (string bundleDir in bundleDirs)
			{
				var dirs = Glob.Files(bundleDir, "**/__data");
				dirs = dirs.Concat(Glob.Files(bundleDir, "**/*.unity3d"));
				bundleFiles.AddRange(dirs.ToList().ConvertAll(dir => Path.Combine(bundleDir, dir)));
			}

			return bundleFiles.ToArray();
		}

				public static List<AssetItem> BuildAssetData(AssetsManager assetsManager)
		{
			var assetItems = new List<AssetItem>();
			var containers = new List<(PPtr<AssetStudio.Object>, string)>();
			int i = 0;
			foreach (var assetsFile in assetsManager.assetsFileList)
			{
				foreach (var asset in assetsFile.Objects)
				{
					var assetItem = new AssetItem(asset);
					assetItems.Add(assetItem);
					assetItem.UniqueID = " #" + i;
					switch (asset)
					{
						case GameObject m_GameObject:
							assetItem.Text = m_GameObject.m_Name;
							break;
						case Texture2D m_Texture2D:
							if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
								assetItem.FullSize = asset.byteSize + m_Texture2D.m_StreamData.size;
							assetItem.Text = m_Texture2D.m_Name;
							break;
						case AudioClip m_AudioClip:
							if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
								assetItem.FullSize = asset.byteSize + m_AudioClip.m_Size;
							assetItem.Text = m_AudioClip.m_Name;
							break;
						case VideoClip m_VideoClip:
							if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
								assetItem.FullSize = asset.byteSize + (long)m_VideoClip.m_ExternalResources.m_Size;
							assetItem.Text = m_VideoClip.m_Name;
							break;
						case Shader m_Shader:
							assetItem.Text = m_Shader.m_ParsedForm?.m_Name ?? m_Shader.m_Name;
							break;
						case Mesh _:
						case TextAsset _:
						case AnimationClip _:
						case Font _:
						case MovieTexture _:
						case Sprite _:
							assetItem.Text = ((NamedObject)asset).m_Name;
							break;
						case MonoBehaviour m_MonoBehaviour:
							if (m_MonoBehaviour.m_Name == "" && m_MonoBehaviour.m_Script.TryGet(out var m_Script))
							{
								assetItem.Text = m_Script.m_ClassName;
							}
							else
							{
								assetItem.Text = m_MonoBehaviour.m_Name;
							}
							break;
						case AssetBundle m_AssetBundle:
							foreach (var m_Container in m_AssetBundle.m_Container)
							{
								var preloadIndex = m_Container.Value.preloadIndex;
								var preloadSize = m_Container.Value.preloadSize;
								var preloadEnd = preloadIndex + preloadSize;
								for (int k = preloadIndex; k < preloadEnd; k++)
								{
									containers.Add((m_AssetBundle.m_PreloadTable[k], m_Container.Key));
								}
							}
							assetItem.Text = m_AssetBundle.m_Name;
							break;
						case ResourceManager m_ResourceManager:
							foreach (var m_Container in m_ResourceManager.m_Container)
							{
								containers.Add((m_Container.Value, m_Container.Key));
							}
							break;
						case NamedObject m_NamedObject:
							assetItem.Text = m_NamedObject.m_Name;
							break;
					}
					if (assetItem.Text == "")
					{
						assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
					}
				}
			}
			return assetItems;
		}

		public static string FixFileName(string str)
		{
			if (str.Length >= 260) return Path.GetRandomFileName();
			return Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
		}

		private static bool TryExportFile(string dir, AssetItem item, string extension, out string fullPath)
		{
			var fileName = FixFileName(item.Text);
			fullPath = Path.Combine(dir, fileName + extension);
			if (!File.Exists(fullPath))
			{
				Directory.CreateDirectory(dir);
				return true;
			}
			fullPath = Path.Combine(dir, fileName + item.UniqueID + extension);
			if (!File.Exists(fullPath))
			{
				Directory.CreateDirectory(dir);
				return true;
			}
			return false;
		}
	}
}
