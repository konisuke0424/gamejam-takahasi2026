using System.IO;
using UnityEditor;
using UnityEngine;

namespace KaiForest.EditorTools
{
    /// <summary>Import settings for generated audio and textures, applied by folder.</summary>
    public class KaiForestImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Game/Audio")) return;
            var imp = (AudioImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            bool bed = file.StartsWith("amb_");
            var s = imp.defaultSampleSettings;
            s.loadType = bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = bed ? 0.45f : 0.6f;
            imp.defaultSampleSettings = s;
            imp.forceToMono = !bed;
            imp.loadInBackground = bed;
        }

        void OnPreprocessTexture()
        {
            var t = (TextureImporter)assetImporter;
            if (assetPath.StartsWith("Assets/Game/UI"))
            {
                t.textureType = TextureImporterType.Default;
                t.mipmapEnabled = false;
                t.alphaIsTransparency = true;
                t.npotScale = TextureImporterNPOTScale.None;
                t.wrapMode = TextureWrapMode.Clamp;
                t.textureCompression = TextureImporterCompression.Uncompressed;
            }
            else if (assetPath.StartsWith("Assets/Game/Textures"))
            {
                t.mipmapEnabled = true;
                t.wrapMode = TextureWrapMode.Repeat;
                t.anisoLevel = 4;
                t.maxTextureSize = 512;
            }
        }
    }
}
