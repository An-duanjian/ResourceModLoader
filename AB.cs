using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PVRTexLib;
using AssetsTools.NET.Texture;
using System.IO;
using static System.Net.Mime.MediaTypeNames;
using ResourceModLoader;
using AssetsTools.NET.Texture.TextureDecoders.CrnUnity;
using System.Security.Cryptography;

namespace ModLoader
{
    class AB
    {
        private static int PID = 172001;
        public static string createImageAbSingle(string path)
        {
            int pid = ++PID;
            string dirName = Path.GetDirectoryName(path);
            string bundleName = Path.GetFileNameWithoutExtension(path) + ".bundle";
            if (!Path.Exists(Path.Combine(dirName, "_generated")))
                Directory.CreateDirectory(Path.Combine(dirName, "_generated"));
            string pathAb = Path.Combine(dirName,"_generated",bundleName);
            AssetsManager manager = new AssetsManager();
            BundleFileInstance bundleInst = manager.LoadBundleFile(new MemoryStream(Resource1._ref),"ref.bundle");
            AssetsFileInstance assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, 0);
            AssetsFile assetsFile = assetsInst.file;

            foreach (var type in Enum.GetValues(typeof(AssetClassID)))
                if((AssetClassID)type != AssetClassID.AssetBundle)
                    assetsFile.GetAssetsOfType((int)type).ForEach(asset => { asset.SetRemoved(); });
            var abFileInfo = assetsFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var abFileField = manager.GetBaseField(assetsInst, abFileInfo);
            abFileField["m_Name"].AsString = bundleName;
            abFileField["m_AssetBundleName"].AsString = bundleName;
            abFileField["m_Container.Array"].Children[0]["second"]["asset"]["m_PathID"].AsLong = pid;
            abFileField["m_PreloadTable.Array"].Children[0]["m_PathID"].AsLong = pid;

            abFileInfo.SetNewData(abFileField);
            var baseField = manager.CreateValueBaseField(assetsInst, (int)AssetClassID.Texture2D);


            
            var encoded = Encode(path);
            int width = encoded.Item1;
            int height = encoded.Item2;

            baseField["m_Name"].AsString = Path.GetFileNameWithoutExtension(path);
            AssetTypeValueField m_StreamData = baseField["m_StreamData"];
            m_StreamData["offset"].AsInt = 0;
            m_StreamData["size"].AsInt = 0;
            m_StreamData["path"].AsString = "";

            baseField["m_Width"].AsInt = width;
            baseField["m_Height"].AsInt = height;


            baseField["m_TextureFormat"].AsInt = (int)TextureFormat.ARGB32;
            baseField["m_TextureDimension"].AsInt = 2;
            baseField["m_ImageCount"].AsInt = 1;
            baseField["m_MipCount"].AsInt = 1;
            baseField["m_ForcedFallbackFormat"].AsInt = 4;
            baseField["m_CompleteImageSize"].AsInt = encoded.Item3.Length;


            AssetTypeValueField image_data = baseField["image data"];
            image_data.Value.ValueType = AssetValueType.ByteArray;
            image_data.TemplateField.ValueType = AssetValueType.ByteArray;
            image_data.AsByteArray = encoded.Item3;
            var newInfo = AssetFileInfo.Create(assetsFile, pid, (int)AssetClassID.Texture2D);
            newInfo.SetNewData(baseField);

            assetsFile.Metadata.AddAssetInfo(newInfo);

            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].Name = "CAB-" + bundleName;
            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assetsFile);
            while(bundleInst.file.BlockAndDirInfo.DirectoryInfos.Count > 1)
                bundleInst.file.BlockAndDirInfo.DirectoryInfos.RemoveAt(1);

            if (Path.Exists(pathAb))
                File.Delete(pathAb);
            using FileStream fs = File.OpenWrite(pathAb);
            AssetsFileWriter bundleWriter = new AssetsFileWriter(fs);
            bundleInst.file.Write(bundleWriter);
            return pathAb;
        }

        private static Tuple<int,int, byte[]> Encode(string path)
        {
            using PVRTexture texture = new PVRTexture(path);
            // Check that PVRTexLib loaded the file successfully
            if (texture.GetTextureDataSize() == 0)
            {
                return null;
            }
            texture.Flip(PVRTexLibAxis.Y);

            // Decompress texture to the standard RGBA8888 format.
            ulong RGBA8888 = PVRDefine.PVRTGENPIXELID4('a', 'r', 'g', 'b', 8, 8, 8, 8);

            if (!texture.Transcode(RGBA8888, PVRTexLibVariableType.UnsignedByteNorm, PVRTexLibColourSpace.BT2020))
            {
                return null;
            }
            unsafe
            {
                byte* result = (byte*)texture.GetTextureDataPointer();
                byte[] resultArr = new byte[texture.GetTextureDataSize()];
                fixed (byte* ptr = resultArr)
                {
                    Buffer.MemoryCopy(result, ptr, texture.GetTextureDataSize(), texture.GetTextureDataSize());
                }
                return new Tuple<int,int,byte[]>((int)texture.GetTextureWidth(), (int)texture.GetTextureHeight(), resultArr);
            }
        }

    }
}
