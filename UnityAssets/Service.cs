using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AssetStudio;
using GlobExpressions;
using Raid.Toolkit.Model;
using RGiesecke.DllExport;

namespace UnityAssets
{
	class Service
	{
		private static readonly string[] assetDirGlobs = new string[] {
			"resources",
			"*/Raid_Data/StreamingAssets/AssetBundles",
		};
		private static readonly string[] assetGlobs = new string[] {
			"HeroAvatars*",
		};
		private static readonly PlariumPlayAdapter.GameInfo GameInfo;
		private static bool IsValid;
		private static readonly Dictionary<string, Sprite> SpriteMap = new Dictionary<string, Sprite>();

		static Service()
		{
			PlariumPlayAdapter pp = new PlariumPlayAdapter();
			IsValid = pp.TryGetGameVersion(101, "raid", out GameInfo);
			if (!IsValid)
			{
				return;
			}

			Load();
		}

		private static void Load()
		{
			string[] assetFiles = FindFiles(GameInfo.InstallPath, assetDirGlobs, assetGlobs);
			if (assetFiles.Length == 0)
			{
				IsValid = false;
				return;
			}

			AssetsManager assetsManager = new AssetsManager();
			assetsManager.LoadFiles(assetFiles);

			var assets = BuildAssetData(assetsManager);
			var spriteAssets = assets.Where(asset => asset.Type == ClassIDType.Sprite);
			foreach (var spriteAsset in spriteAssets)
			{
				SpriteMap.Add(spriteAsset.Text, spriteAsset.Asset as Sprite);
			}
		}

		// function GetPngFromHero(ID: String; Buf: array of Byte) : SIZE: int64;
		[DllExport(CallingConvention = CallingConvention.StdCall)]
		public static long GetPngFromHero([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr byteBuf)
		{
			if (!SpriteMap.TryGetValue(id, out Sprite sprite))
			{
				return 0;
			}
			byte[] imgBuf = sprite.GetImage(ImageFormat.Png).ToArray();
			Marshal.Copy(imgBuf, 0, byteBuf, imgBuf.Length);
			return imgBuf.Length;
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

		private static bool TryExportFile(string dir, AssetItem item, string extension, bool keepFiles, out string fullPath)
		{
			var fileName = FixFileName(item.Text);
			fullPath = Path.Combine(dir, fileName + extension);
			if (!File.Exists(fullPath))
			{
				Directory.CreateDirectory(dir);
				return true;
			}
			if (!keepFiles)
			{
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
