using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace UnityEditorIcons
{
	public static class TextureUtility
	{
		public static (Texture2D, Texture2D) GetTexturePair(AssetBundle editorAssetBundle, List<string> group)
		{
			return (
				editorAssetBundle.LoadAsset<Texture2D>(group.FirstOrDefault(IsRetina) ?? group.First()),
				group.Count > 1 ? editorAssetBundle.LoadAsset<Texture2D>(group.Skip(1).FirstOrDefault()) : null
			);
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

		public static void SaveTextureAsPng(Texture texture, string absolutePath)
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
			string stem = dot >= 0 ? name[..dot] : name;
			return stem.EndsWith("@2x", StringComparison.OrdinalIgnoreCase);
		}

		// Composites src over a solid background. Outputs RGB24 (opaque).
		public static Texture2D CompositeOnBackground(Texture2D src, Color bg)
		{
			int width = src.width;
			int height = src.height;

			// Work in linear if project is Linear. This avoids fringe artifacts.
			bool useLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;

			Color32[] srcPixels = src.GetPixels32();
			Color32[] outPixels = new Color32[srcPixels.Length];

			// Prepare background in correct space.
			float bgR = useLinear ? Mathf.GammaToLinearSpace(bg.r) : bg.r;
			float bgG = useLinear ? Mathf.GammaToLinearSpace(bg.g) : bg.g;
			float bgB = useLinear ? Mathf.GammaToLinearSpace(bg.b) : bg.b;

			for (int i = 0; i < srcPixels.Length; i++) {
				Color32 s = srcPixels[i];

				float a = s.a / 255f;

				// Convert source to the chosen working space.
				float sr = s.r / 255f;
				float sg = s.g / 255f;
				float sb = s.b / 255f;

				if (useLinear) {
					sr = Mathf.GammaToLinearSpace(sr);
					sg = Mathf.GammaToLinearSpace(sg);
					sb = Mathf.GammaToLinearSpace(sb);
				}

				// Porter-Duff "over": out = src * a + bg * (1 - a)
				float or = sr * a + bgR * (1f - a);
				float og = sg * a + bgG * (1f - a);
				float ob = sb * a + bgB * (1f - a);

				if (useLinear) {
					or = Mathf.LinearToGammaSpace(or);
					og = Mathf.LinearToGammaSpace(og);
					ob = Mathf.LinearToGammaSpace(ob);
				}

				byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(or * 255f), 0, 255);
				byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(og * 255f), 0, 255);
				byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(ob * 255f), 0, 255);

				outPixels[i] = new Color32(r, g, b, 255);
			}

			Texture2D result = new(width, height, TextureFormat.RGB24, false);
			result.SetPixels32(outPixels);
			result.Apply(false, false);
			return result;
		}
	}
}
