using UnityEditor;
using UnityEngine;

namespace KaiForest.EditorTools
{
    /// <summary>
    /// Import settings for the Tripo-made monster in Resources/Monster100 (written by
    /// Tools/blender/convert_tripo_monster.py): generic rig, looping "idle" / "run" clips named without
    /// the FBX take prefix, and the normal map marked as one. The model's material is the URP Lit
    /// asset Monster100_Mat (made here once, then free to tweak in the Inspector); the FBX's own
    /// materials are remapped to it because the ones generated from the FBX came out untextured.
    /// Re-running the converter re-imports with the same settings.
    /// The material check also runs every time the editor loads scripts: an import that happens
    /// while this script is still compiling is handled by the previous version, which may not know it.
    /// </summary>
    [InitializeOnLoad]
    public class MonsterModelPostprocessor : AssetPostprocessor
    {
        static MonsterModelPostprocessor() => EditorApplication.delayCall += EnsureMaterial;

        const string Folder = "Assets/Game/Resources/Monster100/";
        const string ModelPath = Folder + "Monster100.fbx";
        const string MaterialPath = Folder + "Monster100_Mat.mat";
        const string BaseMap = Folder + "Monster100_basecolor.jpg";
        const string NormalMap = Folder + "Monster100_normal.jpg";

        public override uint GetVersion() => 1;

        bool Ours => assetPath.StartsWith(Folder);

        void OnPreprocessModel()
        {
            if (!Ours) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.importAnimation = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessAnimation()
        {
            if (!Ours) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = c.name.Substring(c.name.LastIndexOf('|') + 1); // "Armature|Armature|run" -> "run"
                c.loopTime = true;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!Ours) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            if (assetPath == NormalMap) ti.textureType = TextureImporterType.NormalMap;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (path == ModelPath || path == BaseMap || path == NormalMap)
                {
                    EditorApplication.delayCall += EnsureMaterial;
                    return;
                }
            }
        }

        static void EnsureMaterial()
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter mi)) return;
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMap);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMap);
            if (baseTex == null) return;

            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.2f);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Cull", 0f); // both faces: the rags are single sheets in places
                m.doubleSidedGI = true;
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            bool dirty = false;
            if (m.GetTexture("_BaseMap") == null)
            {
                m.SetTexture("_BaseMap", baseTex);
                dirty = true;
            }
            if (normalTex != null && m.GetTexture("_BumpMap") == null)
            {
                m.SetTexture("_BumpMap", normalTex);
                m.EnableKeyword("_NORMALMAP");
                dirty = true;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(m);
                AssetDatabase.SaveAssets();
            }

            // Point every material slot of the FBX at it (once; remapped slots are no longer sub-assets).
            // Not during Play: MonsterBody sets the material at runtime anyway, and this runs again on the next load.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool remapped = false;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (!(sub is Material embedded)) continue;
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), m);
                remapped = true;
            }
            if (remapped) mi.SaveAndReimport();
        }
    }
}
