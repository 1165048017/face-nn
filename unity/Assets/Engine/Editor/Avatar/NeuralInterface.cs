﻿using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XEngine.Editor
{
    public class NeuralData
    {
        public float[] boneArgs;
        public Action<string> callback;
        public RoleShape shape;
        public string name;
    }

    public class NeuralInterface
    {
        static RenderTexture rt;
        static Camera camera;
        static string export;
        static string model;
        const int CNT = 95;
        static Connect connect;
        static string EXPORT
        {
            get
            {
                if (string.IsNullOrEmpty(export))
                {
                    export = Application.dataPath;
                    int i = export.IndexOf("unity/Assets");
                    export = export.Substring(0, i) + "export/";
                }
                return export;
            }
        }

        static string MODEL
        {
            get
            {
                if (string.IsNullOrEmpty(model))
                {
                    model = Application.dataPath;
                    int idx = model.IndexOf("/Assets");
                    model = model.Substring(0, idx);
                    model = model + "/models/";
                }
                return model;
            }
        }


        [MenuItem("Tools/SelectModel")]
        public static void Model2Image()
        {
            XEditorUtil.SetupEnv();
            string file = UnityEditor.EditorUtility.OpenFilePanel("Select model file", MODEL, "bytes");
            FileInfo info = new FileInfo(file);
            ProcessFile(info, true);
            MoveDestDir("model_*", "regular/");
            EditorUtility.Open(EXPORT + "regular/");
        }

        [MenuItem("Tools/SelectPicture")]
        public static void Picture2Model()
        {
            XEditorUtil.SetupEnv();
            string picture = UnityEditor.EditorUtility.OpenFilePanel("Select model file", EXPORT, "jpg");
            int idx = picture.LastIndexOf('/') + 1;
            string descript = picture.Substring(0,idx) + "db_description";
            if (!string.IsNullOrEmpty(descript))
            {
                string key = picture.Substring(idx).Replace(".jpg", "");
                FileInfo info = new FileInfo(descript);
                FileStream fs = new FileStream(descript, FileMode.Open, FileAccess.Read);
                BinaryReader reader = new BinaryReader(fs);
                float[] args = new float[CNT];
                while (true)
                {
                    string name = reader.ReadString();
                    for (int i = 0; i < CNT; i++) args[i] = reader.ReadSingle();
                    if (name == key)
                    {
                        int shape = int.Parse(name[name.Length - 1].ToString());
                        NeuralData data = new NeuralData
                        {
                            callback = Capture,
                            boneArgs = args,
                            shape = (RoleShape)shape,
                            name = name
                        };
                        NeuralInput(data, true);
                        break;
                    }
                }
                reader.Close();
                fs.Close();
            }
        }


        [MenuItem("Tools/BatchExportModels")]
        public static void BatchModels()
        {
            XEditorUtil.SetupEnv();
            DirectoryInfo dir = new DirectoryInfo(MODEL);
            var files = dir.GetFiles("*.bytes");
            for (int i = 0; i < files.Length; i++)
            {
                ProcessFile(files[i], true);
            }
            MoveDestDir("model_*", "regular/");
            EditorUtility.Open(EXPORT + "regular/");
        }

        
        [MenuItem("Tools/GenerateDatabase")]
        private static void GenerateDatabase2()
        {
            int datacount = 2000;
            RandomExportModels((int)(datacount * 0.8), "trainset", true, true);
            RandomExportModels((int)(datacount * 0.2), "testset", false, true);
            EditorUtility.Open(EXPORT);
        }

        [MenuItem("Tools/GenerateFaceDatabase")]
        private static void GenerateDatabase()
        {
            int datacount = 2000;
            RandomExportModels((int)(datacount * 0.8), "trainset", true, false);
            RandomExportModels((int)(datacount * 0.2), "testset", false, false);
            EditorUtility.Open(EXPORT);
        }

        private static void RandomExportModels(int expc, string prefix, bool noise, bool complate)
        {
            XEditorUtil.SetupEnv();
            float[] args = new float[CNT];

            FileStream fs = new FileStream(EXPORT + "db_description", FileMode.OpenOrCreate, FileAccess.Write);
            BinaryWriter bw = new BinaryWriter(fs);
            for (int j = 0; j < expc; j++)
            {
                int shape = UnityEngine.Random.Range(3, 5);
                string name = string.Format("db_{0:0000}_{1}", j, shape);
                bw.Write(name);
                for (int i = 0; i < CNT; i++)
                {
                    args[i] = UnityEngine.Random.Range(0.0f, 1.0f);
                    bw.Write(noise ? AddNoise(args[i], i) : args[i]);
                }
                NeuralData data = new NeuralData
                {
                    callback = Capture,
                    boneArgs = args,
                    shape = (RoleShape)shape,
                    name = name
                };
                UnityEditor.EditorUtility.DisplayProgressBar(prefix, string.Format("is generating {0}/{1}", j, expc), (float)j / expc);
                NeuralInput(data, complate);
            }
            UnityEditor.EditorUtility.DisplayProgressBar(prefix, "post processing, wait for a moment", 1);
            bw.Close();
            fs.Close();
            MoveDestDir("db_*", prefix + "/");
            UnityEditor.EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// tip: noise only for train set, not for test set
        /// </summary>
        private static float AddNoise(float arg, int indx)
        {
            int rnd = UnityEngine.Random.Range(0, CNT);
            if (indx == rnd)
            {
                rnd = UnityEngine.Random.Range(-10, 10);
                return ((arg * 80) + 10 + rnd) / 100.0f;
            }
            return arg;
        }


        private static void MoveDestDir(string pattern, string sub)
        {
            try
            {
                var path = EXPORT + sub;
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                Directory.CreateDirectory(path);
                DirectoryInfo dir = new DirectoryInfo(EXPORT);
                var files = dir.GetFiles(pattern);
                for (int i = 0; i < files.Length; i++)
                {
                    files[i].MoveTo(path + files[i].Name);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message + "\n" + e.StackTrace);
                UnityEditor.EditorUtility.ClearProgressBar();
            }
        }



        private static void ProcessFile(FileInfo info, bool complate)
        {
            if (info != null)
            {
                string file = info.FullName;
                FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                float[] args = new float[CNT];
                BinaryReader br = new BinaryReader(fs);
                RoleShape shape = (RoleShape)br.ReadInt32();
                for (int i = 0; i < CNT; i++)
                {
                    args[i] = br.ReadSingle();
                }
                NeuralData data = new NeuralData
                {
                    callback = Capture,
                    boneArgs = args,
                    shape = shape,
                    name = "model_" + info.Name.Replace(".bytes", "")
                };
                NeuralInput(data, complate);
                br.Close();
                fs.Close();
            }
        }


        private static void NeuralInput(NeuralData data, bool complate)
        {
            var prev = ScriptableObject.CreateInstance<FashionPreview>();
            prev.NeuralProcess(data, complate);
            FashionPreview.preview = prev;
        }


        [MenuItem("Tools/Connect", priority = 2)]
        private static void Connect()
        {
            if (connect == null)
            {
                connect = new Connect();
            }
            else
            {
                connect.Quit();
            }
            connect.Initial(5010, 5011);
            Send();
        }

        private static void Send()
        {
            if (connect != null)
            {
                connect.Send("hello world");
            }
            else
            {
                Debug.LogError("connect not initial");
            }
        }

        [MenuItem("Tools/Close", priority = 2)]
        private static void Quit()
        {
            if (FashionPreview.preview != null)
            {
                ScriptableObject.DestroyImmediate(FashionPreview.preview);
            }
            if (connect != null)
            {
                connect.Quit();
            }
        }


        private static void Capture(string name)
        {
            if (camera == null)
                camera = GameObject.FindObjectOfType<Camera>();
            if (rt == null)
            {
                string path = "Assets/Engine/Editor/EditorResources/CameraOuput.renderTexture";
                rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            }
            rt.Release();
            camera.targetTexture = rt;
            camera.Render();
            camera.Render();
            SaveRenderTex(rt, name);
            Clear();
        }


        private static void Clear()
        {
            camera.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();
        }


        private static void SaveRenderTex(RenderTexture rt, string name)
        {
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            byte[] bytes = tex.EncodeToJPG();
            if (bytes != null && bytes.Length > 0)
            {
                try
                {
                    if (!Directory.Exists(EXPORT))
                    {
                        Directory.CreateDirectory(EXPORT);
                    }
                    File.WriteAllBytes(EXPORT + name + ".jpg", bytes);
                }
                catch (IOException ex)
                {
                    Debug.Log("转换图片失败" + ex.Message);
                }
            }
        }

    }

}