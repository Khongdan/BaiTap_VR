using System;
using System.Collections.Generic;
using System.IO;
using BTTH_06;
using EcoPark;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BT_VR.EditorTools
{
    [InitializeOnLoad]
    public static class BuildEcoParkScene
    {
        private const string ScenePath = "Assets/Scenes/KhungCanhTuongTac.unity";
        private const string MatDir = "Assets/Materials/EcoPark";
        private const string AnimDir = "Assets/Animations/EcoPark";
        private const string TexDir = "Assets/Textures/EcoPark";
        private const string AudioDir = "Assets/Audio";
        private const string InterceptorPrefabPath = "Assets/Police Car & Helicopter/Prefabs/Interceptor.prefab";
        private const string HelicopterPrefabPath = "Assets/Police Car & Helicopter/Prefabs/Helicopter.prefab";
        private const string PendingFlagPath = "Library/RebuildEcoPark_Once.flag";

        private const float TerrainSize = 260f;
        private const float TerrainMaxHeight = 42f;
        private const float WaterLevel = 2.6f;

        static BuildEcoParkScene()
        {
            if (File.Exists(PendingFlagPath))
            {
                EditorApplication.delayCall += () =>
                {
                    if (!File.Exists(PendingFlagPath)) return;
                    File.Delete(PendingFlagPath);
                    BuildAll();
                };
            }
        }

        [MenuItem("Tools/HUBT/Build Scene KhungCanhTuongTac (Cong Vien Dao Xanh)")]
        public static void BuildAll()
        {
            EnsureDirectories();
            BuildBTVRScene.UpgradeImportedAssetMaterialsToURP();

            GenerateParkWavesAudio();
            GenerateEcoParkTextures();
            AssetDatabase.Refresh();

            ConfigureRepeatTexture(TexDir + "/tex_sand.png", false);
            ConfigureRepeatTexture(TexDir + "/tex_sand_nrm.png", true);
            ConfigureRepeatTexture(TexDir + "/tex_park_grass.png", false);
            ConfigureRepeatTexture(TexDir + "/tex_park_grass_nrm.png", true);
            ConfigureRepeatTexture(TexDir + "/tex_stone_path.png", false);
            ConfigureRepeatTexture(TexDir + "/tex_stone_path_nrm.png", true);
            ConfigureRepeatTexture(TexDir + "/tex_coastal_rock.png", false);
            ConfigureRepeatTexture(TexDir + "/tex_coastal_rock_nrm.png", true);

            var mats = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Anh sang & Skybox bau troi nang dep
            SetupLightingAndSkybox();

            // 2. Main Camera (khong hien khung GUI, co che do bam xe + Cinematic Orbit + bay theo truc thang)
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.25f;
            cam.farClipPlane = 600f;
            camObj.AddComponent<AudioListener>();
            EcoParkCameraController camCtrl = camObj.AddComponent<EcoParkCameraController>();

            // 3. Am thanh song nuoc vo bo & gio cong vien
            GameObject audioObj = new GameObject("Ambient_ParkWavesAudio");
            AudioSource waveAudio = audioObj.AddComponent<AudioSource>();
            waveAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/park_waves_wind.wav");
            waveAudio.loop = true;
            waveAudio.playOnAwake = true;
            waveAudio.volume = 0.30f;
            waveAudio.spatialBlend = 0f;

            // 4. Terrain 2 dao cong vien xanh + Rung cay trong bang Terrain Paint Trees
            Terrain parkTerrain = BuildIslandParkTerrain(mats);

            // 5. Mat ho / bien xanh trong lap lanh bao quanh dao
            BuildWaterLake(mats);

            // 6. Dia hinh Polybrush (2 go hoa & co chia luoi min san sang chinh sua bang Polybrush)
            BuildPolybrushFlowerMounds(parkTerrain, mats);

            // 7. 4 Cong trinh 3D tu thiet ke bang ProBuilder:
            //    Cau vom bac qua ho, Coi xay gio (co Animation), Hai dang ven ho, San do truc thang Helipad
            BuildProBuilderBridge(mats);
            BuildProBuilderWindmill(parkTerrain, mats);
            BuildProBuilderLighthouse(parkTerrain, mats);
            BuildProBuilderHelipad(parkTerrain, mats);

            // 8. Trang tri hoa, nam, da, bui cay ven ho (Asset Store)
            BuildParkFlowersAndRocks(parkTerrain);

            // 9. Xe o to Asset Store (Interceptor) dieu khien chay qua cau & vong quanh 2 dao
            Transform playerCar = BuildDrivableParkCar(parkTerrain);

            // 10. Truc thang Asset Store Animation bay vong quanh cong vien
            Transform heli = BuildOrbitingHelicopter();

            camCtrl.carTarget = playerCar;
            camCtrl.helicopterTarget = heli;
            camCtrl.currentMode = EcoParkCameraController.CameraMode.FollowCar;
            camCtrl.orbitCenter = new Vector3(0f, 5.5f, 0f);
            camCtrl.orbitRadius = 56f;
            camCtrl.orbitHeight = 24f;

            camObj.transform.position = playerCar.position - playerCar.forward * 9.5f + Vector3.up * 4.5f;
            camObj.transform.LookAt(playerCar.position + playerCar.forward * 12f + Vector3.up * 1.6f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildEcoParkScene] Da tao thanh cong Scene KhungCanhTuongTac tai: " + ScenePath);
        }

        public static void BuildAndCaptureVerify()
        {
            BuildAll();

            string artDir = @"C:\Users\KhongDan\.gemini\antigravity\brain\3f2f60a5-e517-4406-a760-808345ede584";
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject camObj = new GameObject("TempEcoCam");
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.25f;
            cam.farClipPlane = 600f;

            // Shot 1: Toan canh 2 dao cong vien, cau vom bac qua ho xanh, coi xay gio, hai dang, truc thang
            cam.transform.position = new Vector3(-2f, 24f, -54f);
            cam.transform.LookAt(new Vector3(2f, 5.5f, 4f));
            RenderToPng(cam, Path.Combine(artDir, "verify_ecopark_overview.png"), 1280, 720, true);

            // Shot 2: Goc lai xe sau Interceptor huong thang qua cau ProBuilder sang dao Hai dang
            GameObject car = GameObject.Find("PlayerCar_Park");
            if (car != null)
            {
                cam.transform.position = car.transform.position - car.transform.forward * 9.5f + Vector3.up * 4.2f;
                cam.transform.LookAt(car.transform.position + car.transform.forward * 24f + Vector3.up * 2.2f);
                RenderToPng(cam, Path.Combine(artDir, "verify_ecopark_car_bridge.png"), 1280, 720, false);
            }

            UnityEngine.Object.DestroyImmediate(camObj);
        }

        private static float SmoothRange(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static void RenderToPng(Camera cam, string path, int w, int h, bool warmup)
        {
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            if (warmup) cam.Render();
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = prev;
            UnityEngine.Object.DestroyImmediate(rt);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void EnsureDirectories()
        {
            string[] dirs = { "Assets/Scenes", MatDir, AnimDir, TexDir, AudioDir };
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            }
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

        // ==================== LIGHTING & SKYBOX ====================
        private static void SetupLightingAndSkybox()
        {
            GameObject lightObj = new GameObject("Directional Light");
            lightObj.transform.rotation = Quaternion.Euler(38f, -42f, 0f);
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.48f;
            dirLight.color = new Color(1f, 0.97f, 0.88f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.78f;

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/VRTemplateAssets/Materials/Skybox/Hub Skybox Blue 2.mat");
            if (skybox != null) RenderSettings.skybox = skybox;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.54f, 0.76f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.58f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.32f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.96f);
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 420f;
        }

        // ==================== AUDIO: LAKE WAVES + BREEZE ====================
        private static void GenerateParkWavesAudio()
        {
            string path = AudioDir + "/park_waves_wind.wav";
            if (File.Exists(path)) return;

            int sr = 44100;
            float dur = 6.0f;
            int count = Mathf.RoundToInt(sr * dur);
            System.Random rng = new System.Random(909);
            float lp1 = 0f, lp2 = 0f;

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                int dataSize = count * 2;
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sr);
                bw.Write(sr * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataSize);

                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / sr;
                    float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                    // Nhip song nuoc vo bo em diu 2 lan / 6s
                    float wave = 0.35f + 0.65f * Mathf.Pow(0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / 3.0f), 1.6f);
                    float cutoff = Mathf.Lerp(0.018f, 0.085f, wave);
                    lp1 += cutoff * (white - lp1);
                    lp2 += cutoff * (lp1 - lp2);
                    float s = Mathf.Clamp(lp2 * 2.2f * wave, -1f, 1f);
                    bw.Write((short)(s * 32767f));
                }
            }
        }

        // ==================== TEXTURES & MATERIALS ====================
        private static void GenerateEcoParkTextures()
        {
            CreateAlbedoAndNormal(TexDir + "/tex_sand.png", TexDir + "/tex_sand_nrm.png", 512, 101, 1.2f,
                (n1, n2, n3, r) => Color.Lerp(new Color(0.86f, 0.78f, 0.58f), new Color(0.75f, 0.67f, 0.48f), Mathf.Clamp01(n1 * 0.6f + n2 * 0.4f)));

            CreateAlbedoAndNormal(TexDir + "/tex_park_grass.png", TexDir + "/tex_park_grass_nrm.png", 512, 202, 2.2f,
                (n1, n2, n3, r) => Color.Lerp(new Color(0.24f, 0.52f, 0.19f), new Color(0.42f, 0.68f, 0.24f), Mathf.Clamp01(n1 * 0.5f + n2 * 0.3f + n3 * 0.2f)));

            CreateAlbedoAndNormal(TexDir + "/tex_stone_path.png", TexDir + "/tex_stone_path_nrm.png", 512, 303, 2.0f,
                (n1, n2, n3, r) => Color.Lerp(new Color(0.72f, 0.70f, 0.65f), new Color(0.58f, 0.55f, 0.50f), Mathf.Clamp01(r * 0.45f + n2 * 0.35f + n3 * 0.2f)));

            CreateAlbedoAndNormal(TexDir + "/tex_coastal_rock.png", TexDir + "/tex_coastal_rock_nrm.png", 512, 404, 4.5f,
                (n1, n2, n3, r) => Color.Lerp(new Color(0.26f, 0.27f, 0.26f), new Color(0.52f, 0.50f, 0.46f), Mathf.Clamp01(r * 0.55f + n1 * 0.45f)));
        }

        private static void CreateAlbedoAndNormal(string albedoPath, string normalPath, int size, int seed, float bump,
            Func<float, float, float, float, Color> colorFn)
        {
            Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float[,] hBuf = new float[size, size];
            float ox = seed * 13.7f, oy = seed * 29.1f;

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float n1 = TilePerlin(u, v, 6f, ox, oy);
                    float n2 = TilePerlin(u, v, 16f, ox + 30f, oy + 15f);
                    float n3 = TilePerlin(u, v, 40f, ox + 80f, oy + 60f);
                    float ridge = 1f - Mathf.Abs(n2 * 2f - 1f);
                    hBuf[y, x] = n1 * 0.35f + ridge * ridge * 0.45f + n3 * 0.2f;
                    albedo.SetPixel(x, y, colorFn(n1, n2, n3, ridge * ridge));
                }
            }
            albedo.Apply();
            File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(albedo);

            Texture2D nrm = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                int ym = (y - 1 + size) % size, yp = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size, xp = (x + 1) % size;
                    Vector3 n = new Vector3(-(hBuf[y, xp] - hBuf[y, xm]) * bump, -(hBuf[yp, x] - hBuf[ym, x]) * bump, 1f).normalized;
                    nrm.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            }
            nrm.Apply();
            File.WriteAllBytes(normalPath, nrm.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(nrm);
        }

        private static float TilePerlin(float u, float v, float scale, float ox, float oy)
        {
            float a = Mathf.PerlinNoise(u * scale + ox, v * scale + oy);
            float b = Mathf.PerlinNoise((u - 1f) * scale + ox, v * scale + oy);
            float c = Mathf.PerlinNoise(u * scale + ox, (v - 1f) * scale + oy);
            float d = Mathf.PerlinNoise((u - 1f) * scale + ox, (v - 1f) * scale + oy);
            float tx = u * u * (3f - 2f * u), ty = v * v * (3f - 2f * v);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static void ConfigureRepeatTexture(string assetPath, bool isNormalMap)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !isNormalMap;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            var dict = new Dictionary<string, Material>();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            dict["wood"] = SaveMat("Eco_Wood", urpLit, new Color(0.56f, 0.36f, 0.19f), 0f, 0.25f);
            dict["wood_dark"] = SaveMat("Eco_WoodDark", urpLit, new Color(0.32f, 0.20f, 0.11f), 0f, 0.22f);
            dict["stone"] = SaveMat("Eco_Stone", urpLit, new Color(0.68f, 0.67f, 0.64f), 0.05f, 0.24f);
            dict["deck"] = SaveMat("Eco_BridgeDeck", urpLit, new Color(0.42f, 0.39f, 0.36f), 0.05f, 0.28f);
            dict["white"] = SaveMat("Eco_White", urpLit, new Color(0.95f, 0.95f, 0.96f), 0.05f, 0.35f);
            dict["red"] = SaveMat("Eco_Red", urpLit, new Color(0.82f, 0.18f, 0.16f), 0.10f, 0.40f);
            dict["helipad_dark"] = SaveMat("Eco_Helipad", urpLit, new Color(0.24f, 0.26f, 0.30f), 0.05f, 0.30f);
            dict["yellow_mark"] = SaveMat("Eco_YellowMark", urpLit, new Color(0.98f, 0.82f, 0.16f), 0f, 0.35f);

            // Den hai dang phat sang
            Material beacon = SaveMat("Eco_BeaconGlow", urpLit, new Color(1f, 0.94f, 0.62f), 0f, 0.9f);
            beacon.EnableKeyword("_EMISSION");
            beacon.SetColor("_EmissionColor", new Color(3.2f, 2.6f, 1.0f));
            EditorUtility.SetDirty(beacon);
            dict["beacon"] = beacon;

            // Mat nuoc ho trong xanh lap lanh URP
            Material water = SaveMat("Eco_WaterLake", urpLit, new Color(0.08f, 0.56f, 0.76f, 0.78f), 0.15f, 0.94f);
            water.SetFloat("_Surface", 1f);
            water.SetFloat("_Blend", 0f);
            water.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            water.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            water.SetFloat("_ZWrite", 0f);
            water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            water.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(water);
            dict["water"] = water;

            // Doi hoa Polybrush (URP Lit + grass texture)
            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/tex_park_grass.png");
            Material moundMat = SaveMat("Eco_PolybrushMound", urpLit, new Color(0.90f, 0.98f, 0.80f), 0f, 0.14f);
            if (grassTex != null && moundMat.HasProperty("_BaseMap"))
            {
                moundMat.SetTexture("_BaseMap", grassTex);
                moundMat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            }
            EditorUtility.SetDirty(moundMat);
            dict["polybrush_mound"] = moundMat;

            return dict;
        }

        private static Material SaveMat(string name, Shader shader, Color color, float metallic, float smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ==================== TWIN ISLAND ECO-PARK TERRAIN + NATIVE PAINT TREES ====================
        private static Terrain BuildIslandParkTerrain(Dictionary<string, Material> mats)
        {
            string tdPath = MatDir + "/EcoPark_TerrainData.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath) != null)
                AssetDatabase.DeleteAsset(tdPath);

            TerrainData td = new TerrainData();
            td.heightmapResolution = 513;
            td.alphamapResolution = 512;
            td.baseMapResolution = 512;
            td.size = new Vector3(TerrainSize, TerrainMaxHeight, TerrainSize);

            AssetDatabase.CreateAsset(td, tdPath);

            TerrainLayer[] layers =
            {
                CreateTerrainLayer("Layer_SandBeach", TexDir + "/tex_sand.png", TexDir + "/tex_sand_nrm.png", new Vector2(18f, 18f), 0.7f),
                CreateTerrainLayer("Layer_LushGrass", TexDir + "/tex_park_grass.png", TexDir + "/tex_park_grass_nrm.png", new Vector2(16f, 16f), 0.9f),
                CreateTerrainLayer("Layer_ParkPath", TexDir + "/tex_stone_path.png", TexDir + "/tex_stone_path_nrm.png", new Vector2(12f, 12f), 1.0f),
                CreateTerrainLayer("Layer_CliffRock", TexDir + "/tex_coastal_rock.png", TexDir + "/tex_coastal_rock_nrm.png", new Vector2(22f, 22f), 1.4f)
            };
            td.terrainLayers = layers;

            int hmRes = td.heightmapResolution;
            float[,] heights = new float[hmRes, hmRes];

            for (int iz = 0; iz < hmRes; iz++)
            {
                float nz = (float)iz / (hmRes - 1);
                float wz = (nz - 0.5f) * TerrainSize;

                for (int ix = 0; ix < hmRes; ix++)
                {
                    float nx = (float)ix / (hmRes - 1);
                    float wx = (nx - 0.5f) * TerrainSize;

                    // Hai hon dao xanh: Dao Tay (-46, 0) & Dao Dong (+46, 0) ngan cach boi eo ho trung tam (X = -11..+11)
                    float nShore = (Mathf.PerlinNoise(wx * 0.03f + 10f, wz * 0.03f + 10f) - 0.5f) * 14f;
                    float distWest = Vector2.Distance(new Vector2(wx, wz), new Vector2(-46f, 0f)) + nShore;
                    float distEast = Vector2.Distance(new Vector2(wx, wz), new Vector2( 46f, 0f)) + nShore;

                    float islandWest = 1f - SmoothRange(34f, 60f, distWest);
                    float islandEast = 1f - SmoothRange(34f, 60f, distEast);
                    float landMask = Mathf.Max(islandWest, islandEast);

                    // Kenh nuoc xanh o giua X = -12..+12 de cau ProBuilder bac ngang
                    float channelCut = 1f - SmoothRange(7f, 16f, Mathf.Abs(wx));
                    landMask *= (1f - channelCut * 0.95f);

                    // Nen dat dao phang thoang o cao do 4.5m
                    float h = 0.2f + landMask * 4.3f;

                    // Cac ngon doi xanh nhap nho o ria ngoai 2 dao tao chieu sau
                    float hillNoise = Mathf.PerlinNoise(wx * 0.035f + 50f, wz * 0.035f + 25f);
                    float westHill = Mathf.Exp(-Mathf.Pow((wx + 66f) / 24f, 2f) - Mathf.Pow((wz - 14f) / 26f, 2f)) * (15f + hillNoise * 10f);
                    float eastHill = Mathf.Exp(-Mathf.Pow((wx - 68f) / 25f, 2f) - Mathf.Pow((wz - 12f) / 28f, 2f)) * (17f + hillNoise * 11f);
                    float southKnoll = Mathf.Exp(-Mathf.Pow((wx + 54f) / 20f, 2f) - Mathf.Pow((wz + 28f) / 18f, 2f)) * (9f + hillNoise * 6f);
                    h += (westHill + eastHill + southKnoll) * landMask;

                    // San phang nhe cac khu vuc cong trinh & duong noi cau (Z = 0, X = -42..-15 & +15..+42)
                    float bridgeApproachW = (1f - SmoothRange(6f, 13f, Mathf.Abs(wz))) * (1f - SmoothRange(13f, 20f, Mathf.Abs(wx + 26f)));
                    float bridgeApproachE = (1f - SmoothRange(6f, 13f, Mathf.Abs(wz))) * (1f - SmoothRange(13f, 20f, Mathf.Abs(wx - 26f)));
                    float helipadFlat = 1f - SmoothRange(8f, 14f, Vector2.Distance(new Vector2(wx, wz), new Vector2(32f, 22f)));
                    float lighthouseFlat = 1f - SmoothRange(6f, 12f, Vector2.Distance(new Vector2(wx, wz), new Vector2(34f, -22f)));
                    float windmillFlat = 1f - SmoothRange(6f, 12f, Vector2.Distance(new Vector2(wx, wz), new Vector2(-34f, 22f)));

                    float flatMask = Mathf.Max(bridgeApproachW, bridgeApproachE, helipadFlat, lighthouseFlat);
                    if (flatMask > 0.01f) h = Mathf.Lerp(h, 4.5f, flatMask);
                    if (windmillFlat > 0.01f) h = Mathf.Lerp(h, 5.6f, windmillFlat);

                    heights[iz, ix] = Mathf.Clamp01(h / TerrainMaxHeight);
                }
            }
            td.SetHeights(0, 0, heights);

            // Splatmap 4 lop: Cat vang ven bo, Co xanh cong vien, Duong dao, Da doi
            int amRes = td.alphamapResolution;
            float[,,] alpha = new float[amRes, amRes, 4];

            for (int iz = 0; iz < amRes; iz++)
            {
                float nz = (float)iz / (amRes - 1);
                float wz = (nz - 0.5f) * TerrainSize;

                for (int ix = 0; ix < amRes; ix++)
                {
                    float nx = (float)ix / (amRes - 1);
                    float wx = (nx - 0.5f) * TerrainSize;

                    float worldY = td.GetInterpolatedHeight(nx, nz);
                    float slope = td.GetSteepness(nx, nz);
                    float n = Mathf.PerlinNoise(wx * 0.09f + 7f, wz * 0.09f + 11f);

                    float wSand = 1f - SmoothRange(3.1f + (n - 0.5f) * 0.7f, 4.3f, worldY);
                    float wRock = SmoothRange(20f, 34f, slope);

                    // Duong dao noi cau qua 2 dao + vong tron nho quanh dao tay & dao dong
                    float onBridgeRoad = (1f - SmoothRange(3.2f, 4.8f, Mathf.Abs(wz))) * SmoothRange(13f, 16f, Mathf.Abs(wx)) * (1f - SmoothRange(42f, 48f, Mathf.Abs(wx)));
                    float westRing = 1f - SmoothRange(2.4f, 4.2f, Mathf.Abs(Vector2.Distance(new Vector2(wx, wz), new Vector2(-36f, 0f)) - 18f));
                    float eastRing = 1f - SmoothRange(2.4f, 4.2f, Mathf.Abs(Vector2.Distance(new Vector2(wx, wz), new Vector2( 36f, 0f)) - 18f));
                    float wPath = Mathf.Clamp01(Mathf.Max(onBridgeRoad, westRing, eastRing)) * SmoothRange(3.8f, 4.3f, worldY) * (1f - wRock);

                    float wGrass = Mathf.Clamp01(1f - wSand - wRock - wPath);
                    float sum = wSand + wGrass + wPath + wRock;
                    if (sum <= 0.0001f) { wGrass = 1f; sum = 1f; }

                    alpha[iz, ix, 0] = wSand / sum;
                    alpha[iz, ix, 1] = wGrass / sum;
                    alpha[iz, ix, 2] = wPath / sum;
                    alpha[iz, ix, 3] = wRock / sum;
                }
            }
            td.SetAlphamaps(0, 0, alpha);

            // Trong rung cay bang Paint Trees cua Terrain (ket hop 2 bo Asset Store)
            PopulateParkTerrainTrees(td);

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            GameObject terrainObj = Terrain.CreateTerrainGameObject(td);
            terrainObj.name = "ParkTerrain";
            terrainObj.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
            Terrain terrain = terrainObj.GetComponent<Terrain>();
            if (GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial != null)
                terrain.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;

            terrain.heightmapPixelError = 3f;
            terrain.treeDistance = 500f;
            terrain.treeBillboardDistance = 240f;
            terrain.treeMaximumFullLODCount = 400;
            return terrain;
        }

        private static void PopulateParkTerrainTrees(TerrainData td)
        {
            string[] protoPaths =
            {
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Oak/Oak.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Birch/Birch.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Spruce/Spruce_008.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Ash/Ash.prefab",
                "Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Prefabs/Tree_free_01_Prefab.prefab",
                "Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Prefabs/Tree_free_02_Prefab.prefab",
                "Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Prefabs/Tree_free_03_Prefab.prefab"
            };

            List<TreePrototype> protos = new List<TreePrototype>();
            foreach (string p in protoPaths)
            {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (pf != null) protos.Add(new TreePrototype { prefab = pf, bendFactor = 0.3f });
            }
            td.treePrototypes = protos.ToArray();
            if (protos.Count == 0) return;

            List<TreeInstance> instances = new List<TreeInstance>();
            System.Random rng = new System.Random(202611);

            for (int attempt = 0; attempt < 1200 && instances.Count < 230; attempt++)
            {
                float wx = Mathf.Lerp(-95f, 95f, (float)rng.NextDouble());
                float wz = Mathf.Lerp(-65f, 65f, (float)rng.NextDouble());

                // Giu thong thoang khu vuc cau, duong chinh, coi xay gio, hai dang, helipad & go hoa Polybrush
                if (Mathf.Abs(wz) < 8.5f && Mathf.Abs(wx) < 46f) continue;
                if (Vector2.Distance(new Vector2(wx, wz), new Vector2(-34f,  22f)) < 11f) continue; // Windmill
                if (Vector2.Distance(new Vector2(wx, wz), new Vector2( 34f, -22f)) < 11f) continue; // Lighthouse
                if (Vector2.Distance(new Vector2(wx, wz), new Vector2( 32f,  22f)) < 13f) continue; // Helipad
                if (Vector2.Distance(new Vector2(wx, wz), new Vector2(-23f, -15f)) <  9f) continue; // Polybrush 1
                if (Vector2.Distance(new Vector2(wx, wz), new Vector2( 23f,  13f)) <  9f) continue; // Polybrush 2
                // Giu trong 2 duong vong quanh dao de xe chay thoai mai
                if (Mathf.Abs(Vector2.Distance(new Vector2(wx, wz), new Vector2(-36f, 0f)) - 18f) < 4.5f) continue;
                if (Mathf.Abs(Vector2.Distance(new Vector2(wx, wz), new Vector2( 36f, 0f)) - 18f) < 4.5f) continue;

                float nx = (wx + TerrainSize * 0.5f) / TerrainSize;
                float nz = (wz + TerrainSize * 0.5f) / TerrainSize;
                if (nx < 0.04f || nx > 0.96f || nz < 0.04f || nz > 0.96f) continue;

                float worldY = td.GetInterpolatedHeight(nx, nz);
                float slope = td.GetSteepness(nx, nz);
                if (worldY < 4.1f || slope > 28f) continue;

                // Tranh cay moc de sat nhau qua
                bool tooClose = false;
                for (int j = 0; j < instances.Count; j++)
                {
                    float ex = (instances[j].position.x - 0.5f) * TerrainSize;
                    float ez = (instances[j].position.z - 0.5f) * TerrainSize;
                    if ((ex - wx) * (ex - wx) + (ez - wz) * (ez - wz) < 22f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                int protoIdx = rng.Next(protos.Count);
                float hScale = Mathf.Lerp(0.75f, 1.30f, (float)rng.NextDouble());
                byte tint = (byte)rng.Next(220, 255);

                instances.Add(new TreeInstance
                {
                    prototypeIndex = protoIdx,
                    position = new Vector3(nx, worldY / TerrainMaxHeight, nz),
                    widthScale = hScale * Mathf.Lerp(0.92f, 1.12f, (float)rng.NextDouble()),
                    heightScale = hScale,
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2.0f,
                    color = new Color32(tint, 255, tint, 255),
                    lightmapColor = Color.white
                });
            }

            td.SetTreeInstances(instances.ToArray(), true);
            Debug.Log($"[BuildEcoParkScene] Da trong {instances.Count} cay bang Terrain Paint Trees!");
        }

        private static TerrainLayer CreateTerrainLayer(string name, string texPath, string nrmPath, Vector2 tileSize, float normalScale)
        {
            string path = $"{MatDir}/{name}.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
            layer.normalScale = normalScale;
            layer.tileSize = tileSize;
            layer.smoothness = 0.08f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ==================== WATER LAKE ====================
        private static void BuildWaterLake(Dictionary<string, Material> mats)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water_Lake";
            water.transform.position = new Vector3(0f, WaterLevel, 0f);
            water.transform.localScale = new Vector3(45f, 1f, 45f);
            water.GetComponent<MeshRenderer>().sharedMaterial = mats["water"];
            UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
        }

        // ==================== POLYBRUSH FLOWER MOUNDS ====================
        private static void BuildPolybrushFlowerMounds(Terrain terrain, Dictionary<string, Material> mats)
        {
            CreatePolybrushMound("Polybrush_FlowerMound_1", terrain, new Vector3(-23f, 0f, -15f), 11f, 11f, 1.5f, mats["polybrush_mound"]);
            CreatePolybrushMound("Polybrush_FlowerMound_2", terrain, new Vector3( 23f, 0f,  13f), 11f, 11f, 1.6f, mats["polybrush_mound"]);
        }

        private static void CreatePolybrushMound(string name, Terrain terrain, Vector3 pos, float w, float d, float maxH, Material mat)
        {
            float baseY = terrain.SampleHeight(pos) + terrain.transform.position.y - 0.2f;
            ProBuilderMesh pb = ShapeGenerator.GeneratePlane(PivotLocation.Center, w, d, 14, 14, Axis.Up);
            pb.gameObject.name = name;
            pb.transform.position = new Vector3(pos.x, baseY, pos.z);

            var verts = new List<Vector3>(pb.positions);
            Color[] cols = new Color[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                float nx = v.x / (w * 0.5f), nz = v.z / (d * 0.5f);
                float h = Mathf.Exp(-(nx * nx + nz * nz) * 2.3f) * maxH;
                verts[i] = new Vector3(v.x, v.y + h, v.z);
                cols[i] = Color.Lerp(new Color(0.45f, 0.65f, 0.24f), new Color(0.75f, 0.85f, 0.30f), Mathf.Clamp01(h / maxH));
            }
            pb.positions = verts;
            pb.colors = cols;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
            pb.ToMesh();
            pb.Refresh();
            pb.gameObject.AddComponent<MeshCollider>();
        }

        // ==================== PROBUILDER 3D MODELS (4 LANDMARKS) ====================
        private static GameObject CreatePBCube(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, Vector3? euler = null)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localPosition = localPos;
            if (euler.HasValue) pb.transform.localEulerAngles = euler.Value;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
            pb.ToMesh();
            pb.Refresh();
            pb.gameObject.AddComponent<BoxCollider>();
            return pb.gameObject;
        }

        private static void BuildProBuilderBridge(Dictionary<string, Material> mats)
        {
            // Cau vom bac qua eo ho (tu X=-18 den X=+18 tai Z=0) de xe chay qua lai giua 2 dao
            GameObject bridge = new GameObject("ProBuilder_ArchBridge");
            bridge.transform.position = Vector3.zero;

            // Mat cau chinh phang tai Y = 5.45m, rong 7.6m
            CreatePBCube("Deck_Center", bridge.transform, new Vector3(0f, 5.45f, 0f), new Vector3(18.5f, 0.42f, 7.6f), mats["deck"]);
            // 2 doc len/xuong muot noi tu bo dao (Y=4.5m) len mat cau (Y=5.45m)
            CreatePBCube("Ramp_West", bridge.transform, new Vector3(-13.4f, 4.92f, 0f), new Vector3(9.4f, 0.42f, 7.6f), mats["deck"], new Vector3(0f, 0f, 6.8f));
            CreatePBCube("Ramp_East", bridge.transform, new Vector3( 13.4f, 4.92f, 0f), new Vector3(9.4f, 0.42f, 7.6f), mats["deck"], new Vector3(0f, 0f, -6.8f));

            // 6 tru cau da cam xuong long ho
            float[] px = { -8.2f, 0f, 8.2f };
            foreach (float x in px)
            {
                CreatePBCube($"Pillar_N_{x}", bridge.transform, new Vector3(x, 2.7f,  3.2f), new Vector3(1.1f, 5.4f, 1.1f), mats["stone"]);
                CreatePBCube($"Pillar_S_{x}", bridge.transform, new Vector3(x, 2.7f, -3.2f), new Vector3(1.1f, 5.4f, 1.1f), mats["stone"]);
            }

            // Lan can go & khung vom cong tren cau
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 3.65f;
                CreatePBCube($"Rail_Top_{side}", bridge.transform, new Vector3(0f, 6.45f, z), new Vector3(18.6f, 0.16f, 0.18f), mats["wood"]);
                CreatePBCube($"Rail_Mid_{side}", bridge.transform, new Vector3(0f, 6.02f, z), new Vector3(18.6f, 0.14f, 0.16f), mats["wood"]);
                for (float x = -8.5f; x <= 8.6f; x += 2.83f)
                    CreatePBCube($"Post_{side}_{x:0}", bridge.transform, new Vector3(x, 6.1f, z), new Vector3(0.22f, 1.15f, 0.22f), mats["wood_dark"]);

                // Thanh vom trang tri noi bat
                CreatePBCube($"Arch_L_{side}", bridge.transform, new Vector3(-4.5f, 7.8f, z), new Vector3(9.5f, 0.35f, 0.28f), mats["red"], new Vector3(0f, 0f, 22f));
                CreatePBCube($"Arch_R_{side}", bridge.transform, new Vector3( 4.5f, 7.8f, z), new Vector3(9.5f, 0.35f, 0.28f), mats["red"], new Vector3(0f, 0f, -22f));
            }
            // Thanh giang tren dinh vom
            CreatePBCube("Arch_TopBeam", bridge.transform, new Vector3(0f, 9.55f, 0f), new Vector3(0.45f, 0.35f, 7.6f), mats["red"]);
        }

        private static void BuildProBuilderWindmill(Terrain terrain, Dictionary<string, Material> mats)
        {
            Vector3 pos = new Vector3(-34f, 0f, 22f);
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;

            GameObject wm = new GameObject("ProBuilder_Windmill");
            wm.transform.position = pos;
            wm.transform.rotation = Quaternion.Euler(0f, 135f, 0f);

            // Than coi xay gio nhieu tang (ProBuilder)
            CreatePBCube("Base_Stone", wm.transform, new Vector3(0f, 1.5f, 0f), new Vector3(4.6f, 3.0f, 4.6f), mats["stone"]);
            CreatePBCube("Body_Mid", wm.transform, new Vector3(0f, 4.7f, 0f), new Vector3(3.8f, 3.6f, 3.8f), mats["white"]);
            CreatePBCube("Body_Top", wm.transform, new Vector3(0f, 7.8f, 0f), new Vector3(3.1f, 2.8f, 3.1f), mats["white"]);
            CreatePBCube("Roof_Cap1", wm.transform, new Vector3(0f, 9.5f, 0f), new Vector3(3.8f, 0.7f, 3.8f), mats["red"]);
            CreatePBCube("Roof_Cap2", wm.transform, new Vector3(0f, 10.2f, 0f), new Vector3(2.2f, 0.8f, 2.2f), mats["red"]);
            CreatePBCube("Door", wm.transform, new Vector3(0f, 1.1f, 2.32f), new Vector3(1.1f, 2.0f, 0.14f), mats["wood_dark"]);

            // Truc & 4 canh quat go (Animation quay tron lien tuc!)
            GameObject blades = new GameObject("Windmill_Blades");
            blades.transform.SetParent(wm.transform, false);
            blades.transform.localPosition = new Vector3(0f, 8.2f, 1.85f);

            CreatePBCube("Hub", blades.transform, Vector3.zero, new Vector3(0.65f, 0.65f, 0.8f), mats["wood_dark"]);
            CreatePBCube("Blade_Up", blades.transform, new Vector3(0f, 2.8f, 0.2f), new Vector3(0.85f, 5.2f, 0.12f), mats["wood"]);
            CreatePBCube("Blade_Down", blades.transform, new Vector3(0f, -2.8f, 0.2f), new Vector3(0.85f, 5.2f, 0.12f), mats["wood"]);
            CreatePBCube("Blade_Left", blades.transform, new Vector3(-2.8f, 0f, 0.2f), new Vector3(5.2f, 0.85f, 0.12f), mats["wood"]);
            CreatePBCube("Blade_Right", blades.transform, new Vector3(2.8f, 0f, 0.2f), new Vector3(5.2f, 0.85f, 0.12f), mats["wood"]);

            // Animation Clip quay canh quat 360 do
            string clipPath = AnimDir + "/EcoPark_Windmill_Spin.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AnimationCurve spinZ = MakeLinearCurve(new[] { 0f, 6f }, i => i == 0 ? 0f : -360f);
            clip.SetCurve("Windmill_Blades", typeof(Transform), "localEulerAnglesRaw.z", spinZ);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null) AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);

            string ctrlPath = AnimDir + "/EcoPark_Windmill.controller";
            AnimatorController ctrl = CreateSimpleController(ctrlPath, AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath));
            Animator anim = wm.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
        }

        private static void BuildProBuilderLighthouse(Terrain terrain, Dictionary<string, Material> mats)
        {
            Vector3 pos = new Vector3(34f, 0f, -22f);
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;

            GameObject lh = new GameObject("ProBuilder_Lighthouse");
            lh.transform.position = pos;

            // Thap hai dang soc do-trang (ProBuilder)
            CreatePBCube("LH_Base", lh.transform, new Vector3(0f, 1.0f, 0f), new Vector3(4.6f, 2.0f, 4.6f), mats["stone"]);
            CreatePBCube("LH_Tier1", lh.transform, new Vector3(0f, 3.2f, 0f), new Vector3(3.6f, 2.6f, 3.6f), mats["white"]);
            CreatePBCube("LH_Tier2", lh.transform, new Vector3(0f, 5.7f, 0f), new Vector3(3.2f, 2.4f, 3.2f), mats["red"]);
            CreatePBCube("LH_Tier3", lh.transform, new Vector3(0f, 8.1f, 0f), new Vector3(2.8f, 2.4f, 2.8f), mats["white"]);
            CreatePBCube("LH_Tier4", lh.transform, new Vector3(0f, 10.3f, 0f), new Vector3(2.4f, 2.0f, 2.4f), mats["red"]);
            CreatePBCube("LH_Balcony", lh.transform, new Vector3(0f, 11.5f, 0f), new Vector3(3.4f, 0.35f, 3.4f), mats["stone"]);
            CreatePBCube("LH_Beacon", lh.transform, new Vector3(0f, 12.6f, 0f), new Vector3(1.8f, 1.8f, 1.8f), mats["beacon"]);
            CreatePBCube("LH_Roof", lh.transform, new Vector3(0f, 13.9f, 0f), new Vector3(2.6f, 0.8f, 2.6f), mats["red"]);

            GameObject lightObj = new GameObject("Lighthouse_BeaconLight");
            lightObj.transform.SetParent(lh.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 12.8f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.90f, 0.55f);
            l.intensity = 24f;
            l.range = 32f;
        }

        private static void BuildProBuilderHelipad(Terrain terrain, Dictionary<string, Material> mats)
        {
            Vector3 pos = new Vector3(32f, 0f, 22f);
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;

            GameObject pad = new GameObject("ProBuilder_HelipadDeck");
            pad.transform.position = pos;

            CreatePBCube("Pad_Base", pad.transform, new Vector3(0f, 0.25f, 0f), new Vector3(11f, 0.5f, 11f), mats["helipad_dark"]);
            // Ky hieu chu H mau vang noi bat tren san do
            CreatePBCube("H_Left", pad.transform, new Vector3(-1.8f, 0.53f, 0f), new Vector3(0.65f, 0.06f, 5.2f), mats["yellow_mark"]);
            CreatePBCube("H_Right", pad.transform, new Vector3( 1.8f, 0.53f, 0f), new Vector3(0.65f, 0.06f, 5.2f), mats["yellow_mark"]);
            CreatePBCube("H_Mid", pad.transform, new Vector3(0f, 0.53f, 0f), new Vector3(3.6f, 0.06f, 0.65f), mats["yellow_mark"]);
        }

        // ==================== FLOWERS, MUSHROOMS & COASTAL ROCKS ====================
        private static void BuildParkFlowersAndRocks(Terrain terrain)
        {
            GameObject root = new GameObject("Park_FlowersRocks");
            string[] flowerPaths =
            {
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Foilage/Flowers/GroundFlower.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Foilage/Flowers/GroundFlower_001.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Foilage/Mushrooms/Mushroom.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Foilage/Mushrooms/Mushroom_001.prefab"
            };
            string[] rockPaths =
            {
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Rocks/Rock.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Rocks/Rock_004.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Rocks/Rock_005.prefab"
            };

            // Dat hoa & nam tren 2 go Polybrush + ven duong
            Vector3[] flowerCoords =
            {
                new(-23f, 0f, -15f), new(-21f, 0f, -13.5f), new(-25f, 0f, -16f), new(-22.5f, 0f, -17f),
                new( 23f, 0f,  13f), new( 21f, 0f,  11.5f), new( 25f, 0f,  14f), new( 22.5f, 0f,  15f),
                new(-19f, 0f,   7f), new(-25f, 0f,   9f),   new( 19f, 0f,  -8f), new( 25f, 0f, -10f)
            };
            for (int i = 0; i < flowerCoords.Length; i++)
            {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(flowerPaths[i % flowerPaths.Length]);
                if (pf == null) continue;
                Vector3 p = flowerCoords[i];
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y + (i < 8 ? 0.9f : 0f);
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
                inst.transform.position = p;
                inst.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                inst.transform.localScale = Vector3.one * 1.4f;
            }

            // Dat da tang ven bo ho
            Vector3[] rockCoords =
            {
                new(-16f, 0f, -10f), new(-15f, 0f, 11f), new(16f, 0f, -11f), new(15f, 0f, 10f),
                new(-18f, 0f, -24f), new( 19f, 0f, -25f), new(22f, 0f, 28f), new(-24f, 0f, 26f)
            };
            for (int i = 0; i < rockCoords.Length; i++)
            {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(rockPaths[i % rockPaths.Length]);
                if (pf == null) continue;
                Vector3 p = rockCoords[i];
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.1f;
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
                inst.transform.position = p;
                inst.transform.rotation = Quaternion.Euler(0f, i * 63f, 0f);
                inst.transform.localScale = Vector3.one * 1.25f;
            }
        }

        // ==================== DRIVABLE ASSET STORE CAR ====================
        private static Transform BuildDrivableParkCar(Terrain terrain)
        {
            Vector3 startPos = new Vector3(-26f, 0f, 0f);
            startPos.y = terrain.SampleHeight(startPos) + terrain.transform.position.y + 0.04f;

            GameObject car = new GameObject("PlayerCar_Park");
            car.transform.position = startPos;
            // Huong sang phai (+X) nhin thang qua cau ProBuilder bac ngang ho nuoc xanh!
            car.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InterceptorPrefabPath);
            GameObject visual = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, car.transform)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Interceptor_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            foreach (var mc in visual.GetComponentsInChildren<MeshCollider>()) UnityEngine.Object.DestroyImmediate(mc);
            foreach (var a in visual.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(a);

            List<Transform> wheelList = new List<Transform>();
            foreach (Transform t in visual.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("Wheel_")) wheelList.Add(t);
            }

            BoxCollider col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.78f, 0.15f);
            col.size = new Vector3(2.05f, 1.5f, 4.95f);

            Light spotL = CreateSpot("Headlight_L", car.transform, new Vector3(-0.68f, 0.72f, 2.55f));
            Light spotR = CreateSpot("Headlight_R", car.transform, new Vector3( 0.68f, 0.72f, 2.55f));

            AudioSource engineAudio = car.AddComponent<AudioSource>();
            engineAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/car_engine.wav");
            engineAudio.loop = true;
            engineAudio.playOnAwake = true;
            engineAudio.volume = 0.22f;

            AudioSource sirenAudio = car.AddComponent<AudioSource>();
            sirenAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/police_siren.wav");
            sirenAudio.loop = true;
            sirenAudio.playOnAwake = false;
            sirenAudio.volume = 0.25f;

            EcoParkCarController ctrl = car.AddComponent<EcoParkCarController>();
            ctrl.wheels = wheelList.ToArray();
            ctrl.leftHeadlight = spotL;
            ctrl.rightHeadlight = spotR;
            ctrl.engineAudio = engineAudio;
            ctrl.sirenAudio = sirenAudio;
            ctrl.minDriveY = WaterLevel + 0.35f;

            return car.transform;
        }

        private static Light CreateSpot(string name, Transform parent, Vector3 localPos)
        {
            GameObject o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.transform.localPosition = localPos;
            o.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Light l = o.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.96f, 0.86f);
            l.intensity = 26f;
            l.range = 28f;
            l.spotAngle = 55f;
            l.shadows = LightShadows.None;
            return l;
        }

        // ==================== ANIMATED HELICOPTER TOUR ====================
        private static Transform BuildOrbitingHelicopter()
        {
            GameObject heli = new GameObject("Helicopter_Tour");
            heli.transform.position = new Vector3(32f, 22f, 22f);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HelicopterPrefabPath);
            GameObject visual = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, heli.transform)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Helicopter_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.82f;

            foreach (var a in visual.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(a);

            HelicopterRotorSpinner spinner = heli.AddComponent<HelicopterRotorSpinner>();
            foreach (Transform t in visual.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Main_Rotor") spinner.mainRotor = t;
                else if (t.name == "Tail_Rotor") spinner.tailRotor = t;
            }

            // Bay vong tron em ai quanh toan bo 2 dao cong vien
            string clipPath = AnimDir + "/EcoPark_Helicopter_Orbit.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            int steps = 16;
            float dur = 24f;
            float radiusX = 46f, radiusZ = 34f;
            float[] times = new float[steps + 1];
            Vector3[] pts = new Vector3[steps + 1];
            float[] yaws = new float[steps + 1];

            for (int i = 0; i <= steps; i++)
            {
                float frac = (float)i / steps;
                float ang = frac * Mathf.PI * 2f;
                times[i] = frac * dur;
                pts[i] = new Vector3(Mathf.Cos(ang) * radiusX, 22f + Mathf.Sin(ang * 2f) * 2.2f, Mathf.Sin(ang) * radiusZ);
                // Huong tiep tuyen theo chieu bay
                Vector3 tangent = new Vector3(-Mathf.Sin(ang) * radiusX, 0f, Mathf.Cos(ang) * radiusZ).normalized;
                float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
                if (i > 0)
                {
                    while (yaw - yaws[i - 1] > 180f) yaw -= 360f;
                    while (yaw - yaws[i - 1] < -180f) yaw += 360f;
                }
                yaws[i] = yaw;
            }

            clip.SetCurve("", typeof(Transform), "localPosition.x", MakeLinearCurve(times, i => pts[i].x));
            clip.SetCurve("", typeof(Transform), "localPosition.y", MakeLinearCurve(times, i => pts[i].y));
            clip.SetCurve("", typeof(Transform), "localPosition.z", MakeLinearCurve(times, i => pts[i].z));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", MakeLinearCurve(times, i => 5f));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", MakeLinearCurve(times, i => yaws[i]));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", MakeLinearCurve(times, i => -8f));

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null) AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);

            AnimatorController ctrl = CreateSimpleController(AnimDir + "/EcoPark_Helicopter.controller", AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath));
            Animator anim = heli.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;

            return heli.transform;
        }

        private static AnimationCurve MakeLinearCurve(float[] times, Func<int, float> valFn)
        {
            AnimationCurve c = new AnimationCurve();
            for (int i = 0; i < times.Length; i++) c.AddKey(new Keyframe(times[i], valFn(i)));
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            }
            return c;
        }

        private static AnimatorController CreateSimpleController(string path, AnimationClip clip)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorState state = controller.layers[0].stateMachine.AddState(clip.name);
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }
    }
}
