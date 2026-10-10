using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class PvtVisualAssetReferencesTests
    {
        [Test]
        public void CheckedInCc0TextureSetsAreOneKilopixelAndImportable()
        {
            foreach (string name in new[] {"Stone","Brick","Wood","Plaster","Roof","Road","Ground"})
            {
                string root="Assets/Game/Art/PolyHaven/Textures/"+name+"/"+name+"_";
                Check(root+"Albedo_1k.jpg");
                Check(root+"Normal_1k.png");
                Check(root+"MetallicSmoothness_1k.png");
            }
            foreach (string path in new[] {
                "Assets/Game/Art/PolyHaven/Models/Pine/Pine_BarkAlbedo_1k.jpg",
                "Assets/Game/Art/PolyHaven/Models/Pine/Pine_TwigBase_1k.png",
                "Assets/Game/Art/PolyHaven/Models/Shrub/Shrub_Base_1k.png",
                "Assets/Game/Art/PolyHaven/Models/Door/Door_Albedo_1k.jpg",
                "Assets/Game/Art/PolyHaven/Models/Gate/Gate_Albedo_1k.jpg",
                "Assets/Game/Art/PolyHaven/Models/Statue/Statue_Albedo_1k.jpg" })
                Check(path);
        }

        [Test]
        public void DerivedCc0MeshesImportAsModelsWithoutSourceHighPolyTree()
        {
            foreach (string name in new[] {"Pine_LOD0","Pine_LOD1","Pine_LOD2",
                "Shrub_LOD0","Shrub_LOD1","Shrub_LOD2","CastleDoor",
                "IronGate","GothicStatue"})
            {
                string path="Assets/Game/Art/PolyHaven/Models/LOD/"+name+".fbx";
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path),Is.Not.Null,path);
            }
        }

        private static void Check(string path)
        {
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture,Is.Not.Null,path);
            Assert.That(texture.width,Is.EqualTo(1024),path+" width");
            Assert.That(texture.height,Is.EqualTo(1024),path+" height");
        }
    }
}
