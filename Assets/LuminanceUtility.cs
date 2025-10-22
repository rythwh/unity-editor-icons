using UnityEngine;

namespace UnityEditorIcons
{
	public static class LuminanceUtility
	{
		public static bool IsIconPredominantlyLight(
			Texture2D sourceTexture,
			float minAlphaToCount = 0.1f,
			int sampleStride = 1
		)
		{
			bool useLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;

			float coverage;
			Color avgIconLin = ComputeAverageIconColorLinear(sourceTexture, minAlphaToCount, sampleStride, useLinear, out coverage);
			if (coverage < 1e-5f) {
				return false; // arbitrary: treat "no pixels" as not light
			}

			float luminance = RelativeLuminanceFromLinearRGB(avgIconLin);
			return luminance >= 0.5f;
		}

		private static Color ComputeAverageIconColorLinear(
			Texture2D src,
			float minAlphaToCount,
			int sampleStride,
			bool useLinear,
			out float coverage01
		)
		{
			Color32[] px = src.GetPixels32();
			int width = src.width;

			float totalWeight = 0f;
			float sumR = 0f, sumG = 0f, sumB = 0f;

			int stride = Mathf.Max(1, sampleStride);
			float minA = Mathf.Clamp01(minAlphaToCount);

			for (int y = 0; y < src.height; y += stride) {
				for (int x = 0; x < width; x += stride) {
					int idx = y * width + x;
					Color32 c = px[idx];

					float a = c.a / 255f;
					if (a < minA) {
						continue;
					}

					float r = c.r / 255f;
					float g = c.g / 255f;
					float b = c.b / 255f;

					if (useLinear) {
						r = Mathf.GammaToLinearSpace(r);
						g = Mathf.GammaToLinearSpace(g);
						b = Mathf.GammaToLinearSpace(b);
					}

					// Weight by alpha so softer pixels contribute less.
					float w = a;
					sumR += r * w;
					sumG += g * w;
					sumB += b * w;
					totalWeight += w;
				}
			}

			coverage01 = totalWeight / (src.width * src.height / (float)(stride * stride));
			if (totalWeight <= 0f) {
				return new Color(0f, 0f, 0f, 1f);
			}

			return new Color(sumR / totalWeight, sumG / totalWeight, sumB / totalWeight, 1f);
		}

		private static float RelativeLuminanceFromLinearRGB(Color linear)
		{
			// WCAG: Y = 0.2126 R + 0.7152 G + 0.0722 B  (linear)
			return 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;
		}
	}
}
