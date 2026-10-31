using UnityEditor;
using UnityEngine;

namespace Flightline.EditorTools
{
    // Imported animation sheets (Resources/Sheets) are small pixel art: keep them sharp and uncompressed.
    class SheetImport : AssetPostprocessor
    {
        public override uint GetVersion() => 2; // bump when the settings below change, so existing sheets reimport

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Sheets/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = assetPath.EndsWith("arcbeam.png") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; // the beam tiles along the arc
            ti.isReadable = true; // crash debris is cut from the airframe's first frame at runtime
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
