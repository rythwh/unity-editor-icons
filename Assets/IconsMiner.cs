// Author of the original script: https://github.com/halak

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

public static class IconsMiner
{
	private static readonly StringBuilder iconDescriptionBuilder = new();

	[MenuItem("Unity Editor Icons/Generate README.md %g", priority = -1000)]
	private static void GenerateREADME()
	{
		Material guidMaterial = new(Shader.Find("Unlit/Texture"));
		string guidMaterialId = Path.Combine("Assets", "GuidMaterial.mat");
		const string readme = "README.md";

		AssetDatabase.CreateAsset(guidMaterial, guidMaterialId);
		EditorUtility.DisplayProgressBar("Generate README.md", "Generating...", 0);

		try {
			AssetBundle editorAssetBundle = GetEditorAssetBundle();
			string iconsPath = GetIconsPath();
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
			readmeBuilder.AppendLine($"| Icon | Name | File ID |");
			readmeBuilder.AppendLine($"|------|------|---------|");

			string[] assetNames = EnumerateIcons(editorAssetBundle, iconsPath).OrderBy(n => n).ToArray();
			string iconsDirectoryPath = Path.Combine("img");
			string descriptionsDirectoryPath = Path.Combine("meta");

			if (!Directory.Exists(iconsDirectoryPath)) {
				Directory.CreateDirectory(iconsDirectoryPath);
			}
			if (!Directory.Exists(descriptionsDirectoryPath)) {
				Directory.CreateDirectory(descriptionsDirectoryPath);
			}

			List<string> assetNamesFiltered = assetNames
				.GroupBy(name => {
					int dot = name.LastIndexOf('.');
					string stem = dot >= 0 ? name.Substring(0, dot) : name;
					string ext = dot >= 0 ? name.Substring(dot) : string.Empty;
					string baseStem = stem.EndsWith("@2x", StringComparison.OrdinalIgnoreCase) ? stem.Substring(0, stem.Length - 3) : stem;
					return baseStem + ext; // key: base name with original extension
				}, StringComparer.OrdinalIgnoreCase)
				.Select(group => group.FirstOrDefault(IsRetina) ?? group.First())
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			for (int i = 0; i < assetNamesFiltered.Count; i++) {
				try {
					string assetName = assetNamesFiltered[i];
					Texture2D icon = editorAssetBundle.LoadAsset<Texture2D>(assetName);

					if (!icon && icon.isReadable) {
						continue;
					}

					EditorUtility.DisplayProgressBar($"Generate {readme}", $"Generating... ({i + 1}/{assetNamesFiltered.Count})", (float)i / assetNamesFiltered.Count);

					Texture2D readableTexture = new(icon.width, icon.height, icon.format, icon.mipmapCount > 1);
					Graphics.CopyTexture(icon, readableTexture);

					string iconPath = Path.Combine(iconsDirectoryPath, $"{icon.name}.png");

					SaveTextureAsPng(readableTexture, iconPath);

					guidMaterial.mainTexture = icon;
					AssetDatabase.SaveAssets();

					string fileId = GetFileId(guidMaterialId);
					iconPath = iconPath.Replace(" ", "%20").Replace('\\', '/');
					string descriptionFilePath = WriteIconDescriptionFile(Path.Combine(descriptionsDirectoryPath, $"{icon.name}.md"), iconPath, icon, fileId);

					const int maxSize = 64;
					float largest = Mathf.Max(icon.width, icon.height);
					float scale = (Mathf.Min(largest, maxSize)) / largest;

					int targetWidth = Mathf.Max(1, Mathf.RoundToInt(icon.width * scale));
					int targetHeight = Mathf.Max(1, Mathf.RoundToInt(icon.height * scale));

					readmeBuilder.AppendLine($"| [<img src=\"{iconPath}\" width={targetWidth} height={targetHeight} title=\"{icon.name}\">]({descriptionFilePath}) | `{icon.name}` | `{fileId}` |");
				} catch (Exception exception) {
					Debug.LogException(exception);
				}
			}

			readmeBuilder.AppendLine("\n\n\n*Original script author [@halak](https://github.com/halak)*");
			File.WriteAllText(readme, readmeBuilder.ToString());

			Debug.Log($"'{readme}' is generated.");
		} finally {
			EditorUtility.ClearProgressBar();
			AssetDatabase.DeleteAsset(guidMaterialId);
		}
	}

	private static string WriteIconDescriptionFile(string path, string pathToIcon, Texture2D icon, string fileId)
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
		iconDescriptionBuilder.AppendLine("```");
		iconDescriptionBuilder.AppendLine(fileId);
		iconDescriptionBuilder.AppendLine("```");

		File.WriteAllText(path, iconDescriptionBuilder.ToString());

		iconDescriptionBuilder.Clear();

		return path.Replace(" ", "%20").Replace('\\', '/');
	}

	private static IEnumerable<string> EnumerateIcons(AssetBundle editorAssetBundle, string iconsPath)
	{
		foreach (string assetName in editorAssetBundle.GetAllAssetNames()) {
			if (assetName.StartsWith(iconsPath, StringComparison.OrdinalIgnoreCase) &&
				(assetName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
					assetName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))) {
				yield return assetName;
			}
		}
	}

	private static string GetFileId(string proxyAssetPath)
	{
		string serializedAsset = File.ReadAllText(proxyAssetPath);
		int index = serializedAsset.IndexOf("_MainTex:", StringComparison.Ordinal);

		if (index == -1) {
			return string.Empty;
		}

		const string fileId = "fileID:";
		int startIndex = serializedAsset.IndexOf(fileId, index, StringComparison.Ordinal) + fileId.Length;
		int endIndex = serializedAsset.IndexOf(',', startIndex);
		return serializedAsset.Substring(startIndex, endIndex - startIndex).Trim();
	}

	private static AssetBundle GetEditorAssetBundle()
	{
		return (AssetBundle)typeof(EditorGUIUtility)
			.GetMethod("GetEditorAssetBundle", BindingFlags.NonPublic | BindingFlags.Static)
			?.Invoke(null, new object[] { });
	}

	private static string GetIconsPath()
	{
		return UnityEditor.Experimental.EditorResources.iconsPath;
	}

	private static bool IsCompressed(Texture2D texture)
	{
		// GraphicsFormat is the reliable way to ask Unity if the underlying format is compressed.
		return GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat);
	}

	private static Texture2D ToReadableUncompressedCopy(Texture source)
	{
		int width = source.width;
		int height = source.height;

		// Pick an sRGB/Linear flag that matches the project so colors don't shift.
		bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

		RenderTexture rt = RenderTexture.GetTemporary(
			width,
			height,
			0,
			RenderTextureFormat.ARGB32,
			linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB
		);

		try {
			// Blit handles Texture2D, RenderTexture, etc.
			Graphics.Blit(source, rt);

			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = rt;

			Texture2D copy = new(width, height, TextureFormat.RGBA32, false, linear);
			copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
			copy.Apply(false, false);

			RenderTexture.active = previous;
			return copy;
		} finally {
			RenderTexture.ReleaseTemporary(rt);
		}
	}

	private static void SaveTextureAsPng(Texture texture, string absolutePath)
	{
		// If it's already a Texture2D and not compressed, we can try the fast path.
		Texture2D texture2D = texture as Texture2D;

		if (texture2D != null && texture2D.isReadable && !IsCompressed(texture2D)) {
			byte[] png = texture2D.EncodeToPNG();
			File.WriteAllBytes(absolutePath, png);
			return;
		}

		// Otherwise, make a safe, uncompressed copy via RT readback.
		Texture2D copy = ToReadableUncompressedCopy(texture);
		byte[] safePng = copy.EncodeToPNG();
		File.WriteAllBytes(absolutePath, safePng);

		// You can destroy the copy if you’re creating many to avoid leaking.
		Object.DestroyImmediate(copy);
	}

	private static bool IsRetina(string name)
	{
		int dot = name.LastIndexOf('.');
		string stem = dot >= 0 ? name.Substring(0, dot) : name;
		return stem.EndsWith("@2x", StringComparison.OrdinalIgnoreCase);
	}
}
