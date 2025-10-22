// Author of the original script: https://github.com/halak

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace UnityEditorIcons
{
	public static class IconsMiner
	{
		public static IEnumerable<string> EnumerateIcons(AssetBundle editorAssetBundle, string iconsPath)
		{
			foreach (string assetName in editorAssetBundle.GetAllAssetNames()) {
				if (assetName.StartsWith(iconsPath, StringComparison.OrdinalIgnoreCase) &&
					(assetName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
						assetName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))) {
					yield return assetName;
				}
			}
		}

		public static AssetBundle GetEditorAssetBundle()
		{
			return (AssetBundle)typeof(EditorGUIUtility)
				.GetMethod("GetEditorAssetBundle", BindingFlags.NonPublic | BindingFlags.Static)
				?.Invoke(null, new object[] { });
		}

		public static string GetIconsPath()
		{
			return UnityEditor.Experimental.EditorResources.iconsPath;
		}
	}
}
