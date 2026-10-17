using UnityEngine;

namespace KaiForest
{
    /// <summary>All materials the procedural world uses. Created and filled by KaiForestSetup.</summary>
    [CreateAssetMenu(menuName = "KaiForest/Material Library")]
    public class MaterialLibrary : ScriptableObject
    {
        public Material ground;
        public Material bark;
        public Material foliage;
        public Material bush;
        public Material stone;
        public Material moss;
        public Material torii;
        public Material wood;
        public Material straw;
        public Material paper;
        public Material cloth;
        public Material obi;
        public Material skin;
        public Material hair;
        public Material monsterBody;
        public Material monsterFace;
        public Material monsterEye;
        public Material lanternGlow;
        public Material lanternFrame;
    }
}
