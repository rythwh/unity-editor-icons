using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityEditorIcons
{
	public static class ReadmeBuilder
	{
		private static readonly StringBuilder iconDescriptionBuilder = new();

		private static Color darkBackground;
		private static Color lightBackground;

		[MenuItem("Unity Editor Icons/Generate README.md %g", priority = -1000)]
		private static void GenerateREADME()
		{
			ColorUtility.TryParseHtmlString("#0d1117", out darkBackground);
			ColorUtility.TryParseHtmlString("#ffffff", out lightBackground);

			const string readme = "README.md";

			AssetBundle editorAssetBundle = IconsMiner.GetEditorAssetBundle();
			string iconsPath = IconsMiner.GetIconsPath();
			StringBuilder readmeBuilder = new();

			readmeBuilder.AppendLine("# Unity Editor Built-in Icons");
			readmeBuilder.AppendLine($"Unity version **{Application.unityVersion}**");
			readmeBuilder.AppendLine();
			readmeBuilder.AppendLine("Load icons using `EditorGUIUtility.IconContent(<ICON NAME>);`");
			readmeBuilder.AppendLine();
			readmeBuilder.AppendLine("### File ID");
			readmeBuilder.AppendLine("You can change script icon by file id");
			readmeBuilder.AppendLine("1. Open meta file (ex. `*.cs.meta`) in Text Editor");
			readmeBuilder.AppendLine("2. Modify the line `icon: {instanceID: 0}` to `icon: {fileID: <FILE ID>, guid: 0000000000000000d000000000000000, type: 0}`");
			readmeBuilder.AppendLine("3. Save and focus Unity Editor");
			readmeBuilder.AppendLine();
			readmeBuilder.AppendLine("All icons are clickable, you will be forwarded to description file.");
			readmeBuilder.AppendLine($"| Icon | Name |");
			readmeBuilder.AppendLine($"|------|------|");

			string[] assetNames = IconsMiner.EnumerateIcons(editorAssetBundle, iconsPath).OrderBy(n => n).ToArray();
			string iconsDirectoryPath = Path.Combine("img");
			string descriptionsDirectoryPath = Path.Combine("meta");

			if (!Directory.Exists(iconsDirectoryPath)) {
				Directory.CreateDirectory(iconsDirectoryPath);
			}
			if (!Directory.Exists(descriptionsDirectoryPath)) {
				Directory.CreateDirectory(descriptionsDirectoryPath);
			}

			// Save icon images (even ones which will be filtered-out)
			foreach (string assetName in assetNames) {

				Texture2D icon = editorAssetBundle.LoadAsset<Texture2D>(assetName);

				if (!icon && icon.isReadable) {
					continue;
				}

				string iconPath = Path.Combine(iconsDirectoryPath, $"{icon.name}.png");

				Texture2D readableTexture = new(icon.width, icon.height, icon.format, icon.mipmapCount > 1);
				Graphics.CopyTexture(icon, readableTexture);

				bool lightIcon = LuminanceUtility.IsIconPredominantlyLight(readableTexture);
				readableTexture = TextureUtility.CompositeOnBackground(readableTexture, lightIcon ? darkBackground : lightBackground);
				TextureUtility.SaveTextureAsPng(readableTexture, iconPath);
			}

			// Filter icons to only keep @2x (retina) icons - to have a shorter list
			List<(Texture2D defaultIcon, Texture2D smallIcon)> icons = assetNames
				.GroupBy(
					name => {
						int dot = name.LastIndexOf('.');
						string stem = dot >= 0 ? name[..dot] : name;
						string ext = dot >= 0 ? name[dot..] : string.Empty;
						string baseStem = stem.EndsWith("@2x", StringComparison.OrdinalIgnoreCase) ? stem[..^3] : stem;
						return baseStem + ext; // key: base name with original extension
					},
					StringComparer.OrdinalIgnoreCase)
				.Select(group => group.OrderByDescending(g => g))
				.Select(group => TextureUtility.GetTexturePair(editorAssetBundle, group.ToList()))
				.OrderBy(group => group.Item1.name)
				.ToList();

			foreach ((Texture2D defaultIcon, Texture2D smallIcon) in icons) {
				string iconPath = Path.Combine(iconsDirectoryPath, $"{defaultIcon.name}.png");
				iconPath = iconPath.Replace(" ", "%20").Replace('\\', '/');

				string descriptionFilePath = WriteIconDescriptionFile(Path.Combine(descriptionsDirectoryPath, $"{defaultIcon.name}.md"), iconPath, defaultIcon);

				const int maxSize = 64;
				float largest = Mathf.Max(defaultIcon.width, defaultIcon.height);
				float scale = Mathf.Min(largest, maxSize) / largest;
				int targetWidth = Mathf.Max(1, Mathf.RoundToInt(defaultIcon.width * scale));
				int targetHeight = Mathf.Max(1, Mathf.RoundToInt(defaultIcon.height * scale));

				string smallIconName = string.Empty;
				string smallIconOutput = string.Empty;
				if (smallIcon != null) {
					// smallIconName = icon.name.Replace("@2x", string.Empty);
					smallIconName = smallIcon.name;

					string smallIconPath = Path.Combine(iconsDirectoryPath, $"{smallIconName}.png");
					smallIconPath = smallIconPath.Replace(" ", "%20").Replace('\\', '/');

					string smallIconDescriptionFilePath = WriteIconDescriptionFile(Path.Combine(descriptionsDirectoryPath, $"{smallIconName}.md"), smallIconPath, smallIcon);

					smallIconOutput = $"[<img src=\"{smallIconPath}\" width={targetWidth / 2f} height={targetHeight / 2f} title=\"{smallIconName}\">]({smallIconDescriptionFilePath})";
				}

				string retinaIconOutput = $"[<img src=\"{iconPath}\" width={targetWidth} height={targetHeight} title=\"{defaultIcon.name}\">]({descriptionFilePath})";
				readmeBuilder.AppendLine($"| {retinaIconOutput}{(!string.IsNullOrWhiteSpace(smallIconOutput) ? $" {smallIconOutput}" : string.Empty)} | `{defaultIcon.name}`{(!string.IsNullOrWhiteSpace(smallIconName) ? $" `{smallIconName}`" : string.Empty)} |");
			}

			readmeBuilder.AppendLine("\n\n\n*Original script author [@halak](https://github.com/halak)*");
			File.WriteAllText(readme, readmeBuilder.ToString());

			Debug.Log($"'{readme}' is generated.");
		}

		private static string WriteIconDescriptionFile(string path, string pathToIcon, Texture2D icon)
		{
			iconDescriptionBuilder.AppendLine($"# {icon.name} `{icon.width}x{icon.height}`");
			iconDescriptionBuilder.AppendLine($"<img src=\"/{pathToIcon}\" width={Mathf.Min(icon.width, 512)} height={Mathf.Min(icon.height, 512)}>");
			iconDescriptionBuilder.AppendLine();
			iconDescriptionBuilder.AppendLine("``` CSharp");
			iconDescriptionBuilder.AppendLine($"EditorGUIUtility.IconContent(\"{icon.name}\")");
			iconDescriptionBuilder.AppendLine("```");
			iconDescriptionBuilder.AppendLine("```");
			iconDescriptionBuilder.AppendLine(icon.name);
			iconDescriptionBuilder.AppendLine("```");

			File.WriteAllText(path, iconDescriptionBuilder.ToString());

			iconDescriptionBuilder.Clear();

			return path.Replace(" ", "%20").Replace('\\', '/');
		}
	}
}
