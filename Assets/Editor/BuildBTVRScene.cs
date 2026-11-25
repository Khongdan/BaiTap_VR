using System;
using System.Collections.Generic;
using System.IO;
using BT_VR;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BT_VR.EditorTools
{
    public static class BuildBTVRScene
    {
        private const string ScenePath = "Assets/Scenes/BT_VR.unity";
        private const string MatDir = "Assets/Materials/BT_VR";
        private const string AnimDir = "Assets/Animations/BT_VR";
        private const string PrefabDir = "Assets/Prefabs/BT_VR";
        private const string AudioDir = "Assets/Audio";
        private const string TexDir = "Assets/Textures";
        private const string InterceptorPrefabPath = "Assets/Police Car & Helicopter/Prefabs/Interceptor.prefab";

        [MenuItem("Tools/HUBT/Build Scene BT_VR (Tuan 4, 5, 6)")]
        public static void BuildAll()
        {
            EnsureDirectories();
            EnsureTags(new[] { "Obstacle" });

            // Xoa bo cac tai nguyen dong vang cu (EnergyItem) neu con ton tai
            CleanupRemovedEnergyAssets();

            // Nang cap shader cho goi Asset Store "Police Car & Helicopter" va "BEDRILL" sang URP
            UpgradeImportedAssetMaterialsToURP();

            GenerateAudioFiles();
            GenerateTextures();
            AssetDatabase.Refresh();

            ConfigureTextureImport(TexDir + "/mat_duong.png", TextureWrapMode.Repeat);
            ConfigureTextureImport(TexDir + "/tranh_venice.png", TextureWrapMode.Clamp);

            var mats = CreateMaterials();

            // Tao Scene BT_VR moi - TAT CA doi tuong dat gon truc tiep trong BT_VR
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLightingAndSkybox();

            // 1. Main Camera (gan luon BT_VR_SceneController, khong co khung GUI)
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 300f;
            camObj.AddComponent<AudioListener>();
            BT_VR_SceneController sceneCtrl = camObj.AddComponent<BT_VR_SceneController>();

            // 2. Mat duong vong quanh ngoi nha
            BuildHouseLoopRoad(mats);

            // 3. Ngoi nha Office ProBuilder + Khung tranh + 2 cay bon san nha (Tuan 4)
            BuildProBuilderHouse(mats);

            // 4. Xe canh sat tu Asset Store Interceptor chay quanh nha (Tuan 4 & 5)
            EditablePoliceCarController policeCar = BuildPoliceCar(mats);

            // 5. Xe o to nguoi dieu khien tu Asset Store Interceptor (Tuan 6 - KHONG co an dong vang)
            Tuan6CarController playerCar = BuildPlayerCar(mats);

            // 6. 3 Vat can Barrier dat o lan ngoai quanh nha (khong co EnergyItem)
            BuildObstacles(mats);

            sceneCtrl.playerCar = playerCar;
            sceneCtrl.policeCar = policeCar;
            sceneCtrl.currentView = BT_VR_SceneController.ViewMode.FollowPlayerCar;

            camObj.transform.position = playerCar.transform.position - playerCar.transform.forward * 7.8f + Vector3.up * 4.2f;
            camObj.transform.LookAt(playerCar.transform.position + Vector3.up * 1.1f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildBTVRScene] Da cap nhat Scene BT_VR (dung xe Asset Store Interceptor, da xoa EnergyItem)!");
        }

        public static void UpgradeImportedAssetMaterialsToURP()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;

            UpgradeMaterial("Assets/Police Car & Helicopter/Models/Materials/car.mat",
                "Assets/Police Car & Helicopter/Textures/car.tif", urpLit, 0.25f, 0.65f, Color.white);
            UpgradeMaterial("Assets/Police Car & Helicopter/Models/Materials/helicopter.mat",
                "Assets/Police Car & Helicopter/Textures/helicopter.tif", urpLit, 0.25f, 0.55f, Color.white);
            UpgradeMaterial("Assets/Police Car & Helicopter/Models/Materials/plane.mat",
                null, urpLit, 0f, 0.2f, new Color(0.22f, 0.23f, 0.25f));
            UpgradeMaterial("Assets/Police Car & Helicopter/Models/Materials/unnamed.mat",
                null, urpLit, 0.1f, 0.4f, new Color(0.75f, 0.75f, 0.78f));

            // Nang cap BEDRILL Tree material sang URP
            Material bedrillMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Materials/Tree_Free_Material.mat");
            if (bedrillMat != null && bedrillMat.shader != urpLit)
            {
                Texture tex = bedrillMat.HasProperty("_MainTex") ? bedrillMat.GetTexture("_MainTex") : null;
                if (tex == null)
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Textures/Tree_Free_Texture.png");
                bedrillMat.shader = urpLit;
                if (tex != null && bedrillMat.HasProperty("_BaseMap")) bedrillMat.SetTexture("_BaseMap", tex);
                if (bedrillMat.HasProperty("_BaseColor")) bedrillMat.SetColor("_BaseColor", Color.white);
                if (bedrillMat.HasProperty("_Smoothness")) bedrillMat.SetFloat("_Smoothness", 0.12f);
                EditorUtility.SetDirty(bedrillMat);
            }
        }

        private static void UpgradeMaterial(string matPath, string texPath, Shader urpLit, float metallic, float smoothness, Color baseColor)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return;

            Texture tex = !string.IsNullOrEmpty(texPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) : null;
            if (tex == null && mat.HasProperty("_MainTex"))
                tex = mat.GetTexture("_MainTex");

            mat.shader = urpLit;
            if (tex != null && mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
        }

        private static void CleanupRemovedEnergyAssets()
        {
            string[] toRemove =
            {
                PrefabDir + "/EnergyItem_Tuan6.prefab",
                AnimDir + "/Tuan6_Energy_Float.anim",
                AnimDir + "/Tuan6_Energy.controller",
                AudioDir + "/energy_pickup.wav"
            };
            foreach (string p in toRemove)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) != null)
                    AssetDatabase.DeleteAsset(p);
            }
        }

        private static void EnsureDirectories()
        {
            string[] dirs = { "Assets/Scenes", MatDir, AnimDir, PrefabDir, AudioDir, TexDir };
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            }
        }

        private static void EnsureTags(string[] requiredTags)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            foreach (string tag in requiredTags)
            {
                bool found = false;
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) { found = true; break; }
                }
                if (!found)
                {
                    tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                    tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
                }
            }
            tagManager.ApplyModifiedProperties();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool exists = false;
            foreach (var s in scenes)
            {
                if (s.path == scenePath) { s.enabled = true; exists = true; break; }
            }
            if (!exists) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetupLightingAndSkybox()
        {
            GameObject lightObj = new GameObject("Directional Light");
            lightObj.transform.rotation = Quaternion.Euler(46f, -35f, 0f);
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.25f;
            dirLight.color = new Color(1f, 0.96f, 0.88f);
            dirLight.shadows = LightShadows.Soft;

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/VRTemplateAssets/Materials/Skybox/Hub Skybox Blue 2.mat");
            if (skybox != null) RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.72f, 0.90f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.50f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.28f, 0.22f);
        }

        private static void GenerateAudioFiles()
        {
            if (!File.Exists(AudioDir + "/police_siren.wav"))
            {
                WriteWavFile(AudioDir + "/police_siren.wav", 44100, 4.0f, (t, dur) =>
                {
                    float phase = 2f * Mathf.PI * (1000f * t - 380f * (2.0f / (2f * Mathf.PI)) * Mathf.Sin(2f * Mathf.PI * t / 2.0f));
                    float s = Mathf.Sin(phase) * 0.65f + Mathf.Sin(phase * 2f) * 0.2f + Mathf.Sin(phase * 3f) * 0.1f;
                    return Mathf.Clamp(s * 0.55f, -1f, 1f);
                });
            }
            if (!File.Exists(AudioDir + "/car_engine.wav"))
            {
                WriteWavFile(AudioDir + "/car_engine.wav", 44100, 3.0f, (t, dur) =>
                {
                    float f = 68f * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 3f * t));
                    float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * f * 3f * t) * 0.15f;
                    return Mathf.Clamp(s * 0.45f, -1f, 1f);
                });
            }
            if (!File.Exists(AudioDir + "/car_crash.wav"))
            {
                System.Random rng = new System.Random(42);
                WriteWavFile(AudioDir + "/car_crash.wav", 44100, 1.1f, (t, dur) =>
                {
                    float env = Mathf.Exp(-t * 5.2f);
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 35f, t / dur) * t);
                    return Mathf.Clamp((noise * 0.65f + thud * 0.6f) * env, -1f, 1f);
                });
            }
        }

        private static void WriteWavFile(string path, int sampleRate, float durationSeconds, Func<float, float, float> sampleFunc)
        {
            int sampleCount = Mathf.RoundToInt(sampleRate * durationSeconds);
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                int dataSize = sampleCount * 2;
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataSize);
                for (int i = 0; i < sampleCount; i++)
                {
                    float t = (float)i / sampleRate;
                    bw.Write((short)(Mathf.Clamp(sampleFunc(t, durationSeconds), -1f, 1f) * 32767f));
                }
            }
        }

        private static void GenerateTextures()
        {
            if (!File.Exists(TexDir + "/mat_duong.png"))
            {
                int w = 512, h = 512;
                Texture2D road = new Texture2D(w, h, TextureFormat.RGBA32, false);
                System.Random rng = new System.Random(101);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float n = (float)(rng.NextDouble() * 0.06 - 0.03);
                        Color c = new Color(0.20f + n, 0.21f + n, 0.23f + n, 1f);
                        if ((x >= 18 && x <= 28) || (x >= w - 28 && x <= w - 18)) c = new Color(0.88f, 0.88f, 0.86f, 1f);
                        if (x >= w / 2 - 8 && x <= w / 2 + 8 && (y % 128) >= 20 && (y % 128) <= 92) c = new Color(0.95f, 0.92f, 0.72f, 1f);
                        road.SetPixel(x, y, c);
                    }
                }
                road.Apply();
                File.WriteAllBytes(TexDir + "/mat_duong.png", road.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(road);
            }
        }

        private static void ConfigureTextureImport(string assetPath, TextureWrapMode wrapMode)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = wrapMode;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            var dict = new Dictionary<string, Material>();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Texture2D paintingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/tranh_venice.png");
            Texture2D carTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Police Car & Helicopter/Textures/car.tif");

            dict["mat_duong_nhua"] = SaveMat("mat_duong_nhua", urpLit, new Color(0.18f, 0.19f, 0.22f), 0f, 0.2f);
            dict["mat_vach_trang"] = SaveMat("mat_vach_trang", urpLit, new Color(0.95f, 0.94f, 0.88f), 0f, 0.3f);
            dict["mat_via_he"] = SaveMat("mat_via_he", urpLit, new Color(0.86f, 0.83f, 0.78f), 0f, 0.2f);
            dict["mat_nha_khung"] = SaveMat("mat_nha_khung", urpLit, new Color(0.14f, 0.15f, 0.17f), 0.35f, 0.45f);
            dict["mat_nha_san"] = SaveMat("mat_nha_san", urpLit, new Color(0.88f, 0.86f, 0.82f), 0f, 0.25f);
            dict["mat_dieu_hoa"] = SaveMat("mat_dieu_hoa", urpLit, new Color(0.92f, 0.92f, 0.94f), 0.1f, 0.4f);

            Material glass = SaveMat("mat_nha_kinh", urpLit, new Color(0.12f, 0.22f, 0.28f, 0.42f), 0.1f, 0.9f);
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(glass);
            dict["mat_nha_kinh"] = glass;

            dict["mat_tranh_khung"] = SaveMat("mat_tranh_khung", urpLit, new Color(0.10f, 0.09f, 0.09f), 0.2f, 0.55f);
            dict["mat_tranh_vien_trang"] = SaveMat("mat_tranh_vien_trang", urpLit, new Color(0.95f, 0.95f, 0.95f), 0f, 0.2f);
            dict["mat_tranh_anh"] = SaveMat("mat_tranh_anh", urpLit, Color.white, 0f, 0.3f, paintingTex, Vector2.one);

            dict["mat_than_cay"] = SaveMat("mat_than_cay", urpLit, new Color(0.36f, 0.22f, 0.12f), 0f, 0.15f);
            dict["mat_la_cay"] = SaveMat("mat_la_cay", urpLit, new Color(0.42f, 0.72f, 0.20f), 0f, 0.2f);
            dict["mat_o_dat"] = SaveMat("mat_o_dat", urpLit, new Color(0.92f, 0.92f, 0.92f), 0f, 0.25f);
            dict["mat_dat"] = SaveMat("mat_dat", urpLit, new Color(0.24f, 0.17f, 0.11f), 0f, 0.1f);

            // Material cho PlayerCar (dung texture that car.tif pha anh do the thao de phan biet voi xe canh sat)
            dict["mat_player_interceptor"] = SaveMat("mat_player_interceptor", urpLit, new Color(0.95f, 0.32f, 0.30f), 0.35f, 0.72f, carTex, Vector2.one);

            dict["mat_vat_can"] = SaveMat("mat_vat_can", urpLit, new Color(0.62f, 0.20f, 0.16f), 0.1f, 0.35f);
            dict["mat_canh_bao_vang"] = SaveEmissiveMat("mat_canh_bao_vang", urpLit, new Color(1f, 0.78f, 0.12f), new Color(1.1f, 0.75f, 0.1f));

            Material smokeMat = SaveMat("mat_khoi", urpParticles != null ? urpParticles : urpLit, new Color(0.32f, 0.32f, 0.34f, 0.72f), 0f, 0f);
            if (urpParticles != null)
            {
                smokeMat.SetFloat("_Surface", 1f);
                smokeMat.SetFloat("_Blend", 0f);
                smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                smokeMat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(smokeMat);
            dict["mat_khoi"] = smokeMat;

            return dict;
        }

        private static Material SaveMat(string name, Shader shader, Color color, float metallic, float smoothness, Texture mainTex = null, Vector2? tiling = null)
        {
            string path = $"{MatDir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mainTex != null && mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", mainTex);
                if (tiling.HasValue) mat.SetTextureScale("_BaseMap", tiling.Value);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SaveEmissiveMat(string name, Shader shader, Color baseColor, Color emissionColor)
        {
            Material mat = SaveMat(name, shader, baseColor, 0.1f, 0.85f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ==================== ROAD LOOP AROUND HOUSE ====================
        private static void BuildHouseLoopRoad(Dictionary<string, Material> mats)
        {
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "Road_Loop";
            road.transform.position = new Vector3(0f, -0.1f, 0f);
            road.transform.localScale = new Vector3(36f, 0.2f, 34f);
            road.GetComponent<MeshRenderer>().sharedMaterial = mats["mat_duong_nhua"];

            for (int x = -12; x <= 12; x += 3)
            {
                CreateDash($"Dash_S_{x}", road.transform, new Vector3(x / 36f, 0.53f, -11.5f / 34f), new Vector3(1.6f / 36f, 0.05f, 0.24f / 34f), mats["mat_vach_trang"]);
                CreateDash($"Dash_N_{x}", road.transform, new Vector3(x / 36f, 0.53f,  11.5f / 34f), new Vector3(1.6f / 36f, 0.05f, 0.24f / 34f), mats["mat_vach_trang"]);
            }
            for (int z = -9; z <= 9; z += 3)
            {
                CreateDash($"Dash_W_{z}", road.transform, new Vector3(-12.2f / 36f, 0.53f, z / 34f), new Vector3(0.24f / 36f, 0.05f, 1.6f / 34f), mats["mat_vach_trang"]);
                CreateDash($"Dash_E_{z}", road.transform, new Vector3( 12.2f / 36f, 0.53f, z / 34f), new Vector3(0.24f / 36f, 0.05f, 1.6f / 34f), mats["mat_vach_trang"]);
            }
        }

        private static void CreateDash(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = name;
            d.transform.SetParent(parent, false);
            d.transform.localPosition = localPos;
            d.transform.localScale = localScale;
            d.GetComponent<MeshRenderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
        }

        // ==================== PROBUILDER OFFICE HOUSE ====================
        private static GameObject CreatePBCube(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, Vector3? localEuler = null)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localPosition = localPos;
            if (localEuler.HasValue) pb.transform.localEulerAngles = localEuler.Value;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
            pb.ToMesh();
            pb.Refresh();
            pb.gameObject.AddComponent<BoxCollider>();
            return pb.gameObject;
        }

        private static void BuildProBuilderHouse(Dictionary<string, Material> mats)
        {
            GameObject house = new GameObject("OfficeHouse");

            CreatePBCube("Sidewalk_Base", house.transform, new Vector3(0f, 0.1f, 0f), new Vector3(13.5f, 0.2f, 10.5f), mats["mat_via_he"]);

            float floorW = 8.4f, floorD = 6.2f, floorH = 0.24f, storyH = 2.6f;

            CreatePBCube("Floor_1", house.transform, new Vector3(0f, 0.32f, 0f), new Vector3(floorW, floorH, floorD), mats["mat_nha_khung"]);
            CreatePBCube("Floor_1_Inner", house.transform, new Vector3(0f, 0.45f, 0f), new Vector3(floorW - 0.3f, 0.05f, floorD - 0.3f), mats["mat_nha_san"]);
            CreatePBCube("Floor_2", house.transform, new Vector3(0f, 0.32f + storyH, 0f), new Vector3(floorW, floorH, floorD), mats["mat_nha_khung"]);
            CreatePBCube("Floor_2_Inner", house.transform, new Vector3(0f, 0.45f + storyH, 0f), new Vector3(floorW - 0.3f, 0.05f, floorD - 0.3f), mats["mat_nha_san"]);
            CreatePBCube("Roof_Slab", house.transform, new Vector3(0f, 0.32f + storyH * 2f, 0f), new Vector3(floorW + 0.1f, 0.32f, floorD + 0.1f), mats["mat_nha_khung"]);
            CreatePBCube("Roof_Inner", house.transform, new Vector3(0f, 0.49f + storyH * 2f, 0f), new Vector3(floorW - 0.6f, 0.06f, floorD - 0.6f), mats["mat_nha_san"]);

            Vector2[] colPositions =
            {
                new(-floorW * 0.5f + 0.22f, -floorD * 0.5f + 0.22f),
                new( 0f,                    -floorD * 0.5f + 0.22f),
                new( floorW * 0.5f - 0.22f, -floorD * 0.5f + 0.22f),
                new(-floorW * 0.5f + 0.22f,  floorD * 0.5f - 0.22f),
                new( 0f,                     floorD * 0.5f - 0.22f),
                new( floorW * 0.5f - 0.22f,  floorD * 0.5f - 0.22f),
                new(-floorW * 0.5f + 0.22f,  0f),
                new( floorW * 0.5f - 0.22f,  0f)
            };
            for (int i = 0; i < colPositions.Length; i++)
            {
                float colH = (i == 1) ? storyH : storyH * 2f;
                float colY = (i == 1) ? (0.32f + storyH * 1.5f) : (0.32f + storyH);
                CreatePBCube($"Column_{i + 1}", house.transform, new Vector3(colPositions[i].x, colY, colPositions[i].y), new Vector3(0.32f, colH, 0.32f), mats["mat_nha_khung"]);
            }

            CreatePBCube("DoorFrame_Left", house.transform, new Vector3(-1.05f, 1.5f, -floorD * 0.5f + 0.18f), new Vector3(0.14f, 2.3f, 0.18f), mats["mat_nha_khung"]);
            CreatePBCube("DoorFrame_Right", house.transform, new Vector3(1.05f, 1.5f, -floorD * 0.5f + 0.18f), new Vector3(0.14f, 2.3f, 0.18f), mats["mat_nha_khung"]);
            CreatePBCube("DoorFrame_Center", house.transform, new Vector3(0f, 1.5f, -floorD * 0.5f + 0.18f), new Vector3(0.08f, 2.3f, 0.14f), mats["mat_nha_khung"]);

            CreatePBCube("Glass_Front", house.transform, new Vector3(0f, 0.32f + storyH, -floorD * 0.5f + 0.18f), new Vector3(floorW - 0.4f, storyH * 2f - 0.2f, 0.06f), mats["mat_nha_kinh"]);
            CreatePBCube("Glass_Back", house.transform, new Vector3(0f, 0.32f + storyH, floorD * 0.5f - 0.18f), new Vector3(floorW - 0.4f, storyH * 2f - 0.2f, 0.06f), mats["mat_nha_kinh"]);
            CreatePBCube("Glass_Left", house.transform, new Vector3(-floorW * 0.5f + 0.18f, 0.32f + storyH, 0f), new Vector3(0.06f, storyH * 2f - 0.2f, floorD - 0.4f), mats["mat_nha_kinh"]);
            CreatePBCube("Glass_Right", house.transform, new Vector3(floorW * 0.5f - 0.18f, 0.32f + storyH, 0f), new Vector3(0.06f, storyH * 2f - 0.2f, floorD - 0.4f), mats["mat_nha_kinh"]);

            float roofY = 0.52f + storyH * 2f;
            CreatePBCube("Roof_AC_Unit", house.transform, new Vector3(1.5f, roofY + 0.22f, 1.4f), new Vector3(1.1f, 0.44f, 0.75f), mats["mat_dieu_hoa"]);

            GameObject signObj = new GameObject("OFFICE_Sign");
            signObj.transform.SetParent(house.transform, false);
            signObj.transform.localPosition = new Vector3(0f, 3.05f, -floorD * 0.5f - 0.04f);
            TextMesh tm = signObj.AddComponent<TextMesh>();
            tm.text = "OFFICE";
            tm.fontSize = 64;
            tm.characterSize = 0.09f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = Color.white;

            GameObject framePrefab = BuildProBuilderPictureFramePrefab(mats);
            GameObject frame = (GameObject)PrefabUtility.InstantiatePrefab(framePrefab, house.transform);
            frame.name = "ProBuilder_PictureFrame";
            frame.transform.localPosition = new Vector3(-2.6f, 1.85f, -floorD * 0.5f - 0.02f);

            GameObject treePrefab = BuildProBuilderTreePrefab(mats);
            GameObject tL = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, house.transform);
            tL.name = "ProBuilder_Tree_Left";
            tL.transform.localPosition = new Vector3(-5.2f, 0.2f, -3.4f);

            GameObject tR = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, house.transform);
            tR.name = "ProBuilder_Tree_Right";
            tR.transform.localPosition = new Vector3(5.2f, 0.2f, -3.4f);
            tR.transform.localRotation = Quaternion.Euler(0f, 140f, 0f);
        }

        private static GameObject BuildProBuilderPictureFramePrefab(Dictionary<string, Material> mats)
        {
            GameObject root = new GameObject("ProBuilder_PictureFrame_Tuan4");
            float w = 1.6f, h = 1.15f, border = 0.10f, depth = 0.12f;
            CreatePBCube("Frame_Top", root.transform, new Vector3(0f, (h - border) * 0.5f, 0f), new Vector3(w, border, depth), mats["mat_tranh_khung"]);
            CreatePBCube("Frame_Bottom", root.transform, new Vector3(0f, -(h - border) * 0.5f, 0f), new Vector3(w, border, depth), mats["mat_tranh_khung"]);
            CreatePBCube("Frame_Left", root.transform, new Vector3(-(w - border) * 0.5f, 0f, 0f), new Vector3(border, h - border * 2f, depth), mats["mat_tranh_khung"]);
            CreatePBCube("Frame_Right", root.transform, new Vector3((w - border) * 0.5f, 0f, 0f), new Vector3(border, h - border * 2f, depth), mats["mat_tranh_khung"]);
            CreatePBCube("MatBoard_White", root.transform, new Vector3(0f, 0f, 0.02f), new Vector3(w - border * 1.8f, h - border * 1.8f, 0.04f), mats["mat_tranh_vien_trang"]);

            GameObject art = GameObject.CreatePrimitive(PrimitiveType.Quad);
            art.name = "Painting_Canvas";
            art.transform.SetParent(root.transform, false);
            art.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            art.transform.localScale = new Vector3(1.08f, 0.72f, 1f);
            art.GetComponent<MeshRenderer>().sharedMaterial = mats["mat_tranh_anh"];

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/ProBuilder_PictureFrame_Tuan4.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildProBuilderTreePrefab(Dictionary<string, Material> mats)
        {
            GameObject root = new GameObject("ProBuilder_Tree_Tuan4");
            float boxW = 1.6f, boxH = 0.24f, rim = 0.16f;
            CreatePBCube("Planter_N", root.transform, new Vector3(0f, boxH * 0.5f, (boxW - rim) * 0.5f), new Vector3(boxW, boxH, rim), mats["mat_o_dat"]);
            CreatePBCube("Planter_S", root.transform, new Vector3(0f, boxH * 0.5f, -(boxW - rim) * 0.5f), new Vector3(boxW, boxH, rim), mats["mat_o_dat"]);
            CreatePBCube("Planter_E", root.transform, new Vector3((boxW - rim) * 0.5f, boxH * 0.5f, 0f), new Vector3(rim, boxH, boxW - rim * 2f), mats["mat_o_dat"]);
            CreatePBCube("Planter_W", root.transform, new Vector3(-(boxW - rim) * 0.5f, boxH * 0.5f, 0f), new Vector3(rim, boxH, boxW - rim * 2f), mats["mat_o_dat"]);
            CreatePBCube("Planter_Soil", root.transform, new Vector3(0f, boxH * 0.35f, 0f), new Vector3(boxW - rim * 1.8f, boxH * 0.7f, boxW - rim * 1.8f), mats["mat_dat"]);

            CreatePBCube("Trunk_Base", root.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.32f, 1.3f, 0.32f), mats["mat_than_cay"]);
            CreatePBCube("Branch_Left", root.transform, new Vector3(-0.36f, 1.75f, 0.08f), new Vector3(0.18f, 1.05f, 0.18f), mats["mat_than_cay"], new Vector3(8f, 0f, 34f));
            CreatePBCube("Branch_Right", root.transform, new Vector3(0.38f, 1.82f, -0.08f), new Vector3(0.18f, 1.15f, 0.18f), mats["mat_than_cay"], new Vector3(-8f, 0f, -32f));

            CreatePBIco("Canopy_Center", root.transform, new Vector3(0f, 2.75f, 0.05f), 0.95f, mats["mat_la_cay"]);
            CreatePBIco("Canopy_Left", root.transform, new Vector3(-0.75f, 2.35f, 0.25f), 0.75f, mats["mat_la_cay"]);
            CreatePBIco("Canopy_Right", root.transform, new Vector3(0.78f, 2.42f, -0.2f), 0.78f, mats["mat_la_cay"]);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/ProBuilder_Tree_Tuan4.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreatePBIco(string name, Transform parent, Vector3 localPos, float radius, Material mat)
        {
            ProBuilderMesh ico = ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, radius, 1);
            ico.gameObject.name = name;
            ico.transform.SetParent(parent, false);
            ico.transform.localPosition = localPos;
            ico.GetComponent<MeshRenderer>().sharedMaterial = mat;
            ico.ToMesh();
            ico.Refresh();
        }

        // ==================== ASSET STORE INTERCEPTOR HELPER ====================
        private static GameObject InstantiateInterceptorVisual(Transform parent, Material overrideMat, out Transform[] wheels)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InterceptorPrefabPath);
            GameObject visual = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Interceptor_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // Xoa Animator va MeshCollider non-convex tren visual con (dung BoxCollider + Animator tren root)
            foreach (var mc in visual.GetComponentsInChildren<MeshCollider>())
                UnityEngine.Object.DestroyImmediate(mc);
            foreach (var a in visual.GetComponentsInChildren<Animator>())
                UnityEngine.Object.DestroyImmediate(a);

            if (overrideMat != null)
            {
                foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
                    mr.sharedMaterial = overrideMat;
            }

            List<Transform> wheelList = new List<Transform>();
            foreach (Transform t in visual.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("Wheel_"))
                    wheelList.Add(t);
            }
            wheels = wheelList.ToArray();
            return visual;
        }

        // ==================== POLICE CAR (TUAN 4 + TUAN 5 - ASSET STORE INTERCEPTOR) ====================
        private static EditablePoliceCarController BuildPoliceCar(Dictionary<string, Material> mats)
        {
            GameObject car = new GameObject("PoliceCar_Animation");
            car.transform.position = new Vector3(-9.5f, 0.02f, -8.5f);

            InstantiateInterceptorVisual(car.transform, null, out Transform[] wheels);

            BoxCollider col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.78f, 0.15f);
            col.size = new Vector3(2.05f, 1.5f, 4.95f);

            Rigidbody rb = car.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.useGravity = false;
            rb.isKinematic = true;

            AnimationClip clip = CreateLoopAnimationClip(AnimDir + "/PoliceCar_Loop.anim", 9.5f, 8.5f, 0.02f, 16f);
            AnimatorController controller = CreateSimpleController(AnimDir + "/PoliceCar_Loop.controller", clip);
            Animator anim = car.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.enabled = false;

            AudioSource audio = car.AddComponent<AudioSource>();
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/police_siren.wav");
            audio.playOnAwake = true;
            audio.loop = true;
            audio.volume = 0.25f;
            audio.spatialBlend = 0.85f;
            audio.minDistance = 4f;
            audio.maxDistance = 40f;

            // Den pha truoc & den nhay canh sat xanh-do tren noc Interceptor
            Light leftSpot = CreateChildLight("Headlight_Spot_L", car.transform, new Vector3(-0.68f, 0.72f, 2.55f), Quaternion.Euler(8f, 0f, 0f), LightType.Spot, new Color(1f, 0.96f, 0.84f), 28f, 24f, 52f);
            Light rightSpot = CreateChildLight("Headlight_Spot_R", car.transform, new Vector3(0.68f, 0.72f, 2.55f), Quaternion.Euler(8f, 0f, 0f), LightType.Spot, new Color(1f, 0.96f, 0.84f), 28f, 24f, 52f);
            Light redPoint = CreateChildLight("SirenLight_Red", car.transform, new Vector3(-0.42f, 1.82f, 0.12f), Quaternion.identity, LightType.Point, new Color(1f, 0.12f, 0.12f), 14f, 9f, 0f);
            Light bluePoint = CreateChildLight("SirenLight_Blue", car.transform, new Vector3(0.42f, 1.82f, 0.12f), Quaternion.identity, LightType.Point, new Color(0.12f, 0.48f, 1f), 14f, 9f, 0f);

            GameObject haloObj = new GameObject("Siren_HaloEffect");
            haloObj.transform.SetParent(car.transform, false);
            haloObj.transform.localPosition = new Vector3(0f, 1.74f, 0.12f);
            ParticleSystem ps = haloObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 0.15f;
            main.startSize = 1.4f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.2f, 0.2f, 0.45f), new Color(0.2f, 0.55f, 1f, 0.45f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.9f, 0.1f, 0.1f);
            haloObj.GetComponent<ParticleSystemRenderer>().sharedMaterial = mats["mat_khoi"];

            EditablePoliceCarController ctrl = car.AddComponent<EditablePoliceCarController>();
            ctrl.movementMode = EditablePoliceCarController.MovementMode.EditableWaypoints;
            ctrl.customWaypoints = new List<Vector3>
            {
                new(-9.5f, 0.02f, -8.5f),
                new(-9.5f, 0.02f,  8.5f),
                new( 9.5f, 0.02f,  8.5f),
                new( 9.5f, 0.02f, -8.5f)
            };
            ctrl.carAnimator = anim;
            ctrl.sirenAudio = audio;
            ctrl.redSirenLight = redPoint;
            ctrl.blueSirenLight = bluePoint;
            ctrl.leftHeadlight = leftSpot;
            ctrl.rightHeadlight = rightSpot;
            ctrl.haloEffectObject = haloObj;
            ctrl.wheels = wheels;

            PrefabUtility.SaveAsPrefabAssetAndConnect(car, PrefabDir + "/PoliceCar_Tuan5.prefab", InteractionMode.AutomatedAction);
            return ctrl;
        }

        // ==================== PLAYER DRIVABLE CAR (TUAN 6 - ASSET STORE INTERCEPTOR) ====================
        private static Tuan6CarController BuildPlayerCar(Dictionary<string, Material> mats)
        {
            GameObject car = new GameObject("PlayerCar");
            car.transform.position = new Vector3(-6f, 0.02f, -14.2f);
            car.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // Dung mau xe that Interceptor tu Asset Store (phoi mau do the thao de phan biet voi xe canh sat)
            InstantiateInterceptorVisual(car.transform, mats["mat_player_interceptor"], out Transform[] wheels);

            BoxCollider col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.78f, 0.15f);
            col.size = new Vector3(2.05f, 1.5f, 4.95f);

            Rigidbody rb = car.AddComponent<Rigidbody>();
            rb.mass = 1000f;
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            GameObject smokeObj = new GameObject("SmokeParticle");
            smokeObj.transform.SetParent(car.transform, false);
            smokeObj.transform.localPosition = new Vector3(0f, 1.05f, 2.0f);
            ParticleSystem smoke = smokeObj.AddComponent<ParticleSystem>();
            var smMain = smoke.main;
            smMain.startLifetime = 1.6f;
            smMain.startSpeed = 2.4f;
            smMain.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            smMain.startColor = new Color(0.28f, 0.28f, 0.30f, 0.85f);
            smMain.gravityModifier = -0.25f;
            var smEm = smoke.emission;
            smEm.rateOverTime = 24f;
            var smShape = smoke.shape;
            smShape.shapeType = ParticleSystemShapeType.Cone;
            smShape.angle = 22f;
            smShape.radius = 0.35f;
            smokeObj.GetComponent<ParticleSystemRenderer>().sharedMaterial = mats["mat_khoi"];
            smokeObj.SetActive(false);

            AudioSource engineAudio = car.AddComponent<AudioSource>();
            engineAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/car_engine.wav");
            engineAudio.loop = true;
            engineAudio.playOnAwake = false;
            engineAudio.volume = 0.35f;

            AudioSource sfxAudio = car.AddComponent<AudioSource>();
            sfxAudio.playOnAwake = false;

            Tuan6CarController ctrl = car.AddComponent<Tuan6CarController>();
            ctrl.baseSpeed = 10f;
            ctrl.turnSpeed = 95f;
            ctrl.roadLimitX = 16.5f;
            ctrl.roadLimitZ = 15.5f;
            ctrl.smokeParticle = smokeObj;
            ctrl.engineAudio = engineAudio;
            ctrl.sfxAudio = sfxAudio;
            ctrl.crashClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/car_crash.wav");
            ctrl.wheels = wheels;

            PrefabUtility.SaveAsPrefabAssetAndConnect(car, PrefabDir + "/RacingCar_Tuan6.prefab", InteractionMode.AutomatedAction);
            return ctrl;
        }

        // ==================== OBSTACLES ONLY (NO GOLD / ENERGY) ====================
        private static void BuildObstacles(Dictionary<string, Material> mats)
        {
            GameObject barrierPrefab = CreateBarrierPrefab(mats);
            Vector3[] barrierPositions =
            {
                new( 14.5f, 0.72f, -3.5f),
                new(  0.0f, 0.72f, 14.2f),
                new(-14.5f, 0.72f,  3.5f)
            };
            for (int i = 0; i < barrierPositions.Length; i++)
            {
                GameObject b = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab);
                b.name = $"Barrier_{i + 1}";
                b.transform.position = barrierPositions[i];
                if (i == 0 || i == 2) b.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
        }

        private static GameObject CreateBarrierPrefab(Dictionary<string, Material> mats)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Barrier_Tuan6";
            obj.tag = "Obstacle";
            obj.transform.localScale = new Vector3(1.8f, 1.4f, 1.1f);
            obj.GetComponent<MeshRenderer>().sharedMaterial = mats["mat_vat_can"];

            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Warning_Stripe";
            stripe.transform.SetParent(obj.transform, false);
            stripe.transform.localScale = new Vector3(1.02f, 0.22f, 1.02f);
            stripe.GetComponent<MeshRenderer>().sharedMaterial = mats["mat_canh_bao_vang"];
            UnityEngine.Object.DestroyImmediate(stripe.GetComponent<Collider>());

            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, PrefabDir + "/Barrier_Tuan6.prefab");
            UnityEngine.Object.DestroyImmediate(obj);
            return prefab;
        }

        // ==================== HELPERS ====================
        private static Light CreateChildLight(string name, Transform parent, Vector3 localPos, Quaternion localRot, LightType type, Color color, float intensity, float range, float spotAngle)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = localRot;
            Light l = obj.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            if (type == LightType.Spot) l.spotAngle = spotAngle;
            l.shadows = LightShadows.None;
            return l;
        }

        private static AnimationClip CreateLoopAnimationClip(string path, float rx, float rz, float y, float duration)
        {
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            float[] times = { 0f, duration * 0.22f, duration * 0.25f, duration * 0.47f, duration * 0.50f, duration * 0.72f, duration * 0.75f, duration * 0.97f, duration };
            Vector3[] pts =
            {
                new(-rx, y, -rz), new(-rx, y,  rz), new(-rx, y,  rz),
                new( rx, y,  rz), new( rx, y,  rz), new( rx, y, -rz),
                new( rx, y, -rz), new(-rx, y, -rz), new(-rx, y, -rz)
            };
            float[] rotsY = { 0f, 0f, 90f, 90f, 180f, 180f, 270f, 270f, 360f };

            clip.SetCurve("", typeof(Transform), "localPosition.x", MakeLinearCurve(times, i => pts[i].x));
            clip.SetCurve("", typeof(Transform), "localPosition.y", MakeLinearCurve(times, i => pts[i].y));
            clip.SetCurve("", typeof(Transform), "localPosition.z", MakeLinearCurve(times, i => pts[i].z));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", MakeLinearCurve(times, i => 0f));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", MakeLinearCurve(times, i => rotsY[i]));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", MakeLinearCurve(times, i => 0f));

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static AnimationCurve MakeLinearCurve(float[] times, Func<int, float> valueSelector)
        {
            AnimationCurve curve = new AnimationCurve();
            for (int i = 0; i < times.Length; i++) curve.AddKey(new Keyframe(times[i], valueSelector(i)));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        private static AnimatorController CreateSimpleController(string path, AnimationClip clip)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorState state = controller.layers[0].stateMachine.AddState(clip.name);
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }
    }
}
