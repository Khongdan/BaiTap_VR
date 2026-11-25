using System;
using System.Collections.Generic;
using System.IO;
using BTTH_06;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BT_VR.EditorTools
{
    public static class BuildBTTH06Scene
    {
        private const string ScenePath = "Assets/Scenes/BTTH_06.unity";
        private const string MatDir = "Assets/Materials/BTTH_06";
        private const string AnimDir = "Assets/Animations/BTTH_06";
        private const string TexDir = "Assets/Textures/BTTH_06";
        private const string AudioDir = "Assets/Audio";
        private const string InterceptorPrefabPath = "Assets/Police Car & Helicopter/Prefabs/Interceptor.prefab";
        private const string HelicopterPrefabPath = "Assets/Police Car & Helicopter/Prefabs/Helicopter.prefab";

        private const float TerrainWidth = 340f;
        private const float TerrainLength = 340f;
        private const float TerrainMaxHeight = 86f;

        [MenuItem("Tools/HUBT/Build Scene BTTH_06 (Thung Lung Nui Tuyet)")]
        public static void BuildAll()
        {
            EnsureDirectories();
            BuildBTVRScene.UpgradeImportedAssetMaterialsToURP();

            GenerateWindAudio();
            GenerateTerrainTexturesAndNormals();
            AssetDatabase.Refresh();

            ConfigureRepeatTexture(TexDir + "/tex_dirt_road.png", false);
            ConfigureRepeatTexture(TexDir + "/tex_dirt_road_nrm.png", true);
            ConfigureRepeatTexture(TexDir + "/tex_valley_grass.png", false);
            ConfigureRepeatTexture(TexDir + "/tex_valley_grass_nrm.png", true);
            ConfigureRepeatTexture(TexDir + "/tex_mountain_rock.png", false);
            ConfigureRepeatTexture(TexDir + "/tex_mountain_rock_nrm.png", true);
            ConfigureRepeatTexture(TexDir + "/tex_peak_snow.png", false);
            ConfigureRepeatTexture(TexDir + "/tex_peak_snow_nrm.png", true);

            var mats = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Lighting & Skybox
            SetupLightingAndSkybox();

            // 2. Main Camera (khong co khung GUI)
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.25f;
            cam.farClipPlane = 700f;
            camObj.AddComponent<AudioListener>();
            BTTH06CameraController camCtrl = camObj.AddComponent<BTTH06CameraController>();

            // 3. Am thanh gio thung lung nui
            GameObject windObj = new GameObject("Ambient_WindAudio");
            AudioSource windAudio = windObj.AddComponent<AudioSource>();
            windAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/mountain_wind.wav");
            windAudio.loop = true;
            windAudio.playOnAwake = true;
            windAudio.volume = 0.28f;
            windAudio.spatialBlend = 0f;

            // 4. Terrain tu nhien (nui da 3 dinh tuyet + duong thung lung + RUNG CAY TRONG BANG TERRAIN PAINT TREES)
            Terrain valleyTerrain = BuildNaturalValleyTerrainWithTrees(mats);

            // 5. Dia hinh Polybrush (2 doi nho ven mep co chia luoi min san sang to/sua bang Polybrush)
            BuildPolybrushSideHills(valleyTerrain, mats);

            // 6. Mo hinh 3D tu thiet ke bang ProBuilder (Cong chao, Hang rao, Tram nghi chan)
            BuildProBuilderStructures(valleyTerrain, mats);

            // 7. Xe o to tu template Asset Store (Interceptor) dieu khien duoc chay trong map
            Transform playerCar = BuildDrivableValleyCar(valleyTerrain);

            // 8. Truc thang tuan tra tu Asset Store co Animation bay luon + quay canh quat
            Transform heli = BuildAnimatedHelicopter();

            camCtrl.carTarget = playerCar;
            camCtrl.helicopterTarget = heli;
            camCtrl.currentMode = BTTH06CameraController.CameraMode.FollowCar;
            camCtrl.scenicPosition = new Vector3(0f, 22f, 10f);
            camCtrl.scenicLookAt = new Vector3(0f, 18f, 225f);

            camObj.transform.position = playerCar.position - playerCar.forward * 9.5f + Vector3.up * 4.4f;
            camObj.transform.LookAt(playerCar.position + playerCar.forward * 14f + Vector3.up * 2.0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildBTTH06Scene] Da cap nhat Scene BTTH_06 (Nui tu nhien + Cay trong bang Terrain Paint Trees)!");
        }

        public static void BuildAndVerifyBTTH06Only()
        {
            BuildAll();

            string artDir = @"C:\Users\KhongDan\.gemini\antigravity\brain\3f2f60a5-e517-4406-a760-808345ede584";
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject camObj = new GameObject("TempVerifyCam");
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.25f;
            cam.farClipPlane = 700f;

            // Goc nhin toan canh thung lung giong buc anh mau
            cam.transform.position = new Vector3(0f, 23f, 8f);
            cam.transform.LookAt(new Vector3(0f, 16f, 220f));
            RenderToPng(cam, Path.Combine(artDir, "verify_btth06_natural_overview.png"), 1280, 720, true);

            // Goc nhin lai xe Interceptor tren duong thung lung
            GameObject carObj = GameObject.Find("PlayerCar_Valley");
            if (carObj != null)
            {
                cam.transform.position = carObj.transform.position - carObj.transform.forward * 9.5f + Vector3.up * 4.2f;
                cam.transform.LookAt(carObj.transform.position + carObj.transform.forward * 38f + Vector3.up * 4.5f);
                RenderToPng(cam, Path.Combine(artDir, "verify_btth06_natural_car.png"), 1280, 720, false);
            }
            UnityEngine.Object.DestroyImmediate(camObj);
        }

        private static float SmoothRange(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static void RenderToPng(Camera cam, string filePath, int w, int h, bool warmup)
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

            File.WriteAllBytes(filePath, tex.EncodeToPNG());
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
            // Nang xien tao bong cay do sang phai va noi bat khoi da nui giong anh mau
            lightObj.transform.rotation = Quaternion.Euler(32f, 56f, 0f);
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.55f;
            dirLight.color = new Color(1f, 0.96f, 0.89f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.85f;

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/VRTemplateAssets/Materials/Skybox/Hub Skybox Blue 2.mat");
            if (skybox != null) RenderSettings.skybox = skybox;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.54f, 0.70f, 0.90f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.50f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.20f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.83f, 0.91f);
            RenderSettings.fogStartDistance = 240f;
            RenderSettings.fogEndDistance = 620f;
        }

        // ==================== AUDIO ====================
        private static void GenerateWindAudio()
        {
            string windPath = AudioDir + "/mountain_wind.wav";
            if (File.Exists(windPath)) return;

            int sr = 44100;
            float dur = 5.0f;
            int count = Mathf.RoundToInt(sr * dur);
            System.Random rng = new System.Random(777);
            float lp1 = 0f, lp2 = 0f;

            using (FileStream fs = new FileStream(windPath, FileMode.Create, FileAccess.Write))
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
                    float gust = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / dur);
                    float cutoff = Mathf.Lerp(0.015f, 0.045f, gust);
                    lp1 += cutoff * (white - lp1);
                    lp2 += cutoff * (lp1 - lp2);
                    bw.Write((short)(Mathf.Clamp(lp2 * 2.4f * gust, -1f, 1f) * 32767f));
                }
            }
        }

        // ==================== PROCEDURAL PBR TERRAIN TEXTURES + NORMAL MAPS ====================
        private static void GenerateTerrainTexturesAndNormals()
        {
            CreateAlbedoAndNormal(TexDir + "/tex_dirt_road.png", TexDir + "/tex_dirt_road_nrm.png", 512, 11, 1.4f,
                (n1, n2, n3, ridge) =>
                {
                    Color c1 = new Color(0.76f, 0.68f, 0.53f);
                    Color c2 = new Color(0.61f, 0.54f, 0.40f);
                    return Color.Lerp(c1, c2, Mathf.Clamp01(n1 * 0.55f + n2 * 0.30f + n3 * 0.15f));
                });

            CreateAlbedoAndNormal(TexDir + "/tex_valley_grass.png", TexDir + "/tex_valley_grass_nrm.png", 512, 22, 2.0f,
                (n1, n2, n3, ridge) =>
                {
                    Color c1 = new Color(0.32f, 0.40f, 0.18f);
                    Color c2 = new Color(0.47f, 0.50f, 0.24f);
                    return Color.Lerp(c1, c2, Mathf.Clamp01(n1 * 0.45f + n2 * 0.35f + n3 * 0.20f));
                });

            // Da nui tuong phan sac net + Normal Map go ghe de bat sang giong vach nui da anh mau
            CreateAlbedoAndNormal(TexDir + "/tex_mountain_rock.png", TexDir + "/tex_mountain_rock_nrm.png", 512, 33, 5.2f,
                (n1, n2, n3, ridge) =>
                {
                    Color crevice = new Color(0.18f, 0.16f, 0.15f);
                    Color midRock = new Color(0.38f, 0.36f, 0.33f);
                    Color lightRock = new Color(0.64f, 0.61f, 0.57f);
                    float v = Mathf.Clamp01(ridge * 0.52f + n1 * 0.30f + n3 * 0.18f);
                    return v < 0.5f ? Color.Lerp(crevice, midRock, v * 2f) : Color.Lerp(midRock, lightRock, (v - 0.5f) * 2f);
                });

            CreateAlbedoAndNormal(TexDir + "/tex_peak_snow.png", TexDir + "/tex_peak_snow_nrm.png", 512, 44, 1.5f,
                (n1, n2, n3, ridge) =>
                {
                    Color shadowSnow = new Color(0.84f, 0.89f, 0.96f);
                    Color brightSnow = new Color(0.98f, 0.99f, 1.00f);
                    return Color.Lerp(shadowSnow, brightSnow, Mathf.Clamp01(n1 * 0.6f + n2 * 0.4f));
                });
        }

        private static void CreateAlbedoAndNormal(string albedoPath, string normalPath, int size, int seed, float bumpStrength,
            Func<float, float, float, float, Color> colorFn)
        {
            Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float[,] heightBuf = new float[size, size];
            float ox = seed * 17.3f, oy = seed * 31.7f;

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float n1 = TileablePerlin(u, v, 6f, ox, oy);
                    float n2 = TileablePerlin(u, v, 16f, ox + 40f, oy + 20f);
                    float n3 = TileablePerlin(u, v, 42f, ox + 90f, oy + 70f);
                    float ridge = 1f - Mathf.Abs(n2 * 2f - 1f);
                    ridge = ridge * ridge;

                    heightBuf[y, x] = n1 * 0.35f + ridge * 0.45f + n3 * 0.20f;
                    albedo.SetPixel(x, y, colorFn(n1, n2, n3, ridge));
                }
            }
            albedo.Apply();
            File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(albedo);

            // Sinh Normal Map tu heightBuf
            Texture2D nrm = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                int ym = (y - 1 + size) % size;
                int yp = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size;
                    int xp = (x + 1) % size;

                    float dx = (heightBuf[y, xp] - heightBuf[y, xm]) * bumpStrength;
                    float dy = (heightBuf[yp, x] - heightBuf[ym, x]) * bumpStrength;
                    Vector3 normal = new Vector3(-dx, -dy, 1f).normalized;
                    nrm.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f));
                }
            }
            nrm.Apply();
            File.WriteAllBytes(normalPath, nrm.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(nrm);
        }

        private static float TileablePerlin(float u, float v, float scale, float ox, float oy)
        {
            float a = Mathf.PerlinNoise(u * scale + ox, v * scale + oy);
            float b = Mathf.PerlinNoise((u - 1f) * scale + ox, v * scale + oy);
            float c = Mathf.PerlinNoise(u * scale + ox, (v - 1f) * scale + oy);
            float d = Mathf.PerlinNoise((u - 1f) * scale + ox, (v - 1f) * scale + oy);
            float tx = u * u * (3f - 2f * u);
            float ty = v * v * (3f - 2f * v);
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
            Shader urpTerrain = Shader.Find("Universal Render Pipeline/Terrain/Lit");

            dict["terrain_mat"] = SaveMat("BTTH06_TerrainURP", urpTerrain != null ? urpTerrain : urpLit, Color.white, 0f, 0.1f);
            dict["wood"] = SaveMat("BTTH06_Wood", urpLit, new Color(0.45f, 0.29f, 0.16f), 0f, 0.22f);
            dict["wood_dark"] = SaveMat("BTTH06_WoodDark", urpLit, new Color(0.28f, 0.18f, 0.10f), 0f, 0.20f);
            dict["stone"] = SaveMat("BTTH06_Stone", urpLit, new Color(0.48f, 0.47f, 0.45f), 0f, 0.18f);
            dict["roof"] = SaveMat("BTTH06_Roof", urpLit, new Color(0.58f, 0.24f, 0.16f), 0f, 0.28f);

            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/tex_valley_grass.png");
            Material polyMat = SaveMat("BTTH06_PolybrushHill", urpLit, new Color(0.85f, 0.90f, 0.72f), 0f, 0.12f);
            if (grassTex != null && polyMat.HasProperty("_BaseMap"))
            {
                polyMat.SetTexture("_BaseMap", grassTex);
                polyMat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            }
            EditorUtility.SetDirty(polyMat);
            dict["polybrush_hill"] = polyMat;

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

        // ==================== NATURAL TERRAIN MOUNTAINS + TERRAIN PAINT TREES ====================
        private static float RoadCenterX(float worldZ)
        {
            return Mathf.Sin(worldZ * 0.017f) * 3.2f;
        }

        private static Terrain BuildNaturalValleyTerrainWithTrees(Dictionary<string, Material> mats)
        {
            string tdPath = MatDir + "/BTTH06_TerrainData.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath) != null)
                AssetDatabase.DeleteAsset(tdPath);

            TerrainData td = new TerrainData();
            td.heightmapResolution = 513;
            td.alphamapResolution = 512;
            td.baseMapResolution = 512;
            td.size = new Vector3(TerrainWidth, TerrainMaxHeight, TerrainLength);

            // Luu TerrainData asset TRUOC khi SetAlphamaps/SetTreeInstances de Unity luu day du sub-assets
            AssetDatabase.CreateAsset(td, tdPath);

            // 1. Gan 4 TerrainLayer co ca Albedo + Normal Map
            TerrainLayer[] layers =
            {
                CreateTerrainLayer("Layer_DirtRoad", TexDir + "/tex_dirt_road.png", TexDir + "/tex_dirt_road_nrm.png", new Vector2(20f, 20f), 0.7f),
                CreateTerrainLayer("Layer_Grass", TexDir + "/tex_valley_grass.png", TexDir + "/tex_valley_grass_nrm.png", new Vector2(18f, 18f), 0.9f),
                CreateTerrainLayer("Layer_MountainRock", TexDir + "/tex_mountain_rock.png", TexDir + "/tex_mountain_rock_nrm.png", new Vector2(26f, 26f), 1.6f),
                CreateTerrainLayer("Layer_Snow", TexDir + "/tex_peak_snow.png", TexDir + "/tex_peak_snow_nrm.png", new Vector2(22f, 22f), 0.6f)
            };
            td.terrainLayers = layers;

            // 2. Dieu khac Heightmap nui da tu nhien bang Domain-Warped Ridged Multifractal fBM
            int hmRes = td.heightmapResolution;
            float[,] heights = new float[hmRes, hmRes];

            for (int iz = 0; iz < hmRes; iz++)
            {
                float nz = (float)iz / (hmRes - 1);
                float wz = nz * TerrainLength;
                float rcx = RoadCenterX(wz);

                for (int ix = 0; ix < hmRes; ix++)
                {
                    float nx = (float)ix / (hmRes - 1);
                    float wx = nx * TerrainWidth - TerrainWidth * 0.5f;

                    // Domain warping de chan nui uon luon tu nhien, khong thang dung nhu buc tuong
                    float warpX = (Mathf.PerlinNoise(wx * 0.012f + 11f, wz * 0.012f + 23f) - 0.5f) * 18f;
                    float warpZ = (Mathf.PerlinNoise(wx * 0.012f + 41f, wz * 0.012f + 7f) - 0.5f) * 18f;
                    float dwx = wx + warpX;
                    float dwz = wz + warpZ;

                    float distFromRoad = Mathf.Abs(wx - rcx);
                    float warpedDist = Mathf.Abs(wx - rcx + warpX * 0.55f);

                    // 4-octave ridged multifractal noise tao khe ranh va go da tu nhien
                    float r1 = RidgedNoise(dwx * 0.014f + 5f, dwz * 0.014f + 3f);
                    float r2 = RidgedNoise(dwx * 0.032f + 19f, dwz * 0.032f + 27f);
                    float r3 = RidgedNoise(dwx * 0.072f + 61f, dwz * 0.072f + 43f);
                    float r4 = Mathf.PerlinNoise(wx * 0.15f + 90f, wz * 0.15f + 80f);
                    float crags = r1 * 0.55f + r2 * 0.28f + r3 * 0.13f + r4 * 0.04f;

                    // Suon nui thoai thoai hai ben thung lung (bat dau tu 18m, cao dan tu nhien toi 115m)
                    float foothillSlope = SmoothRange(18f, 88f, warpedDist);
                    float mountainSide = Mathf.Pow(foothillSlope, 1.25f) * (10f + crags * 38f);
                    float outerRange = SmoothRange(75f, 155f, distFromRoad) * (12f + r1 * 18f);

                    // 3 NGON NUI TUYET HUNG VI O PHIA XA (bo cuc giong het anh mau, co go da sac net!)
                    // Dinh Trai: cao nhat & nhon sac, hoi gan hon
                    float peakLeft = ErodedAlpinePeak(wx, wz, -68f, 222f, 58f, 80f, r2, r3);
                    // Dinh Giua: dinh nhon nam dung diem tu con duong o xa
                    float peakCenter = ErodedAlpinePeak(wx, wz, 8f, 286f, 44f, 64f, r2, r3);
                    // Dinh Phai: nui vom rong phu tuyet ben phai
                    float peakRight = ErodedBroadMountain(wx, wz, 76f, 250f, 62f, 62f, r1, r2);

                    // Day nui chan phia sau thung lung
                    float backHills = SmoothRange(235f, 325f, wz) * (12f + crags * 26f);

                    float h = 1.8f + mountainSide + outerRange + backHills;
                    h = SmoothMax(h, peakLeft, 6f);
                    h = SmoothMax(h, peakCenter, 6f);
                    h = SmoothMax(h, peakRight, 6f);

                    // Long duong thung lung bang phang mem mai de xe chay em, chuyen tiep muot o cuoi thung lung
                    if (wz < 260f && distFromRoad < 19f)
                    {
                        float gentleRoll = Mathf.Sin(wz * 0.03f) * 0.45f + Mathf.Cos(wz * 0.013f) * 0.35f;
                        float sideBlend = SmoothRange(8.5f, 18.5f, distFromRoad);
                        float endBlend = SmoothRange(195f, 255f, wz);
                        float blend = Mathf.Clamp01(Mathf.Max(sideBlend, endBlend));
                        h = Mathf.Lerp(1.8f + gentleRoll, h, blend);
                    }

                    heights[iz, ix] = Mathf.Clamp01(h / TerrainMaxHeight);
                }
            }
            td.SetHeights(0, 0, heights);

            // 3. To mau 4 lop Splatmap tu nhien (Duong cat -> Co xanh ven duong -> Da nui xam go ghe -> Tuyet trang dinh nui)
            int amRes = td.alphamapResolution;
            float[,,] alpha = new float[amRes, amRes, 4];

            for (int iz = 0; iz < amRes; iz++)
            {
                float nz = (float)iz / (amRes - 1);
                float wz = nz * TerrainLength;
                float rcx = RoadCenterX(wz);

                for (int ix = 0; ix < amRes; ix++)
                {
                    float nx = (float)ix / (amRes - 1);
                    float wx = nx * TerrainWidth - TerrainWidth * 0.5f;

                    float distFromRoad = Mathf.Abs(wx - rcx);
                    float worldY = td.GetInterpolatedHeight(nx, nz);
                    float slope = td.GetSteepness(nx, nz);
                    float n1 = Mathf.PerlinNoise(wx * 0.055f + 9f, wz * 0.055f + 13f);
                    float n2 = Mathf.PerlinNoise(wx * 0.14f + 33f, wz * 0.14f + 47f);

                    float wRoad = 0f, wGrass = 0f, wRock = 0f, wSnow = 0f;

                    // Duong cat/dat rong giua thung lung (mep duong chuyen tiep mem mai tu nhien)
                    if (wz < 245f)
                    {
                        float roadEdge = 7.6f + (n1 - 0.5f) * 3.0f;
                        wRoad = 1f - SmoothRange(roadEdge - 2.5f, roadEdge + 4.0f, distFromRoad);
                        wRoad *= 1f - SmoothRange(220f, 245f, wz);
                    }

                    // Tham co xanh olive duoi chan hang cay & thung lung thap
                    if (distFromRoad > 5.0f && distFromRoad < 42f && worldY < 16f)
                    {
                        float gIn = SmoothRange(5.0f, 10.5f, distFromRoad);
                        float gOut = 1f - SmoothRange(18f, 40f, distFromRoad);
                        float gSlope = 1f - SmoothRange(14f, 28f, slope);
                        wGrass = gIn * gOut * gSlope * (0.75f + 0.25f * n2);
                    }

                    // Tuyet trang phu tren ca 3 dinh nui cao (ke ca dinh nhon ben trai & giua giong anh mau)
                    float snowLine = 38f + (n1 - 0.5f) * 8f + SmoothRange(46f, 68f, slope) * 10f;
                    wSnow = SmoothRange(snowLine, snowLine + 10f, worldY);
                    if (slope > 66f) wSnow *= 0.45f;

                    // Da nui xam nau bao phu toan bo suon nui & vach doc
                    wRock = Mathf.Clamp01(1f - wRoad - wGrass - wSnow);
                    if (slope > 20f && wSnow < 0.45f)
                        wRock = Mathf.Max(wRock, SmoothRange(18f, 34f, slope));

                    float sum = wRoad + wGrass + wRock + wSnow;
                    if (sum <= 0.0001f) { wRock = 1f; sum = 1f; }

                    alpha[iz, ix, 0] = wRoad / sum;
                    alpha[iz, ix, 1] = wGrass / sum;
                    alpha[iz, ix, 2] = wRock / sum;
                    alpha[iz, ix, 3] = wSnow / sum;
                }
            }
            td.SetAlphamaps(0, 0, alpha);

            // 4. TRONG TOAN BO RUNG CAY BANG CONG CU PAINT TREES CUA TERRAIN (TerrainData.treePrototypes + SetTreeInstances)
            PopulateNativeTerrainTrees(td);

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            GameObject terrainObj = Terrain.CreateTerrainGameObject(td);
            terrainObj.name = "ValleyTerrain";
            terrainObj.transform.position = new Vector3(-TerrainWidth * 0.5f, 0f, 0f);
            Terrain terrain = terrainObj.GetComponent<Terrain>();
            if (GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial != null)
                terrain.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
            else
                terrain.materialTemplate = mats["terrain_mat"];

            terrain.heightmapPixelError = 3f;
            terrain.treeDistance = 550f;
            terrain.treeBillboardDistance = 260f;
            terrain.treeMaximumFullLODCount = 450;
            terrain.treeCrossFadeLength = 25f;
            terrain.detailObjectDistance = 120f;
            return terrain;
        }

        private static void PopulateNativeTerrainTrees(TerrainData td)
        {
            // Dang ky 6 mau cay Asset Store vao danh sach Paint Trees cua Terrain
            string[] protoPaths =
            {
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Oak/Oak.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Ash/Ash.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Birch/Birch.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Poplar/Poplar.prefab",
                "Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Prefabs/Tree_free_01_Prefab.prefab",
                "Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Prefabs/Tree_free_02_Prefab.prefab"
            };

            List<TreePrototype> protos = new List<TreePrototype>();
            foreach (string p in protoPaths)
            {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (pf != null)
                {
                    protos.Add(new TreePrototype { prefab = pf, bendFactor = 0.25f });
                }
            }
            td.treePrototypes = protos.ToArray();
            if (protos.Count == 0) return;

            List<TreeInstance> instances = new List<TreeInstance>();
            System.Random rng = new System.Random(202606);

            float cabinX = RoadCenterX(76f) - 15.5f;
            float cabinZ = 76f;

            // Trong 2 dai rung cay tu nhien chay doc 2 ben duong thung lung (Z = 10..226)
            for (float z = 10f; z <= 226f; z += 5.2f)
            {
                float rcx = RoadCenterX(z);
                for (int side = -1; side <= 1; side += 2)
                {
                    // 3 lop cay tu mep duong lan nhe vao chan doi tao do day tu nhien nhu anh mau
                    int treesThisBand = 3;
                    for (int row = 0; row < treesThisBand; row++)
                    {
                        float minOff = row == 0 ? 10.2f : (row == 1 ? 15.8f : 22.0f);
                        float spread = row == 0 ? 3.2f : (row == 1 ? 5.0f : 7.5f);
                        float wx = rcx + side * (minOff + (float)rng.NextDouble() * spread);
                        float wz = z + (float)(rng.NextDouble() * 4.8 - 2.4);

                        if (wz < 6f || wz > TerrainLength - 20f) continue;

                        // Giu trong khu vuc Tram nghi chan & Cong chao
                        if (Mathf.Abs(wx - cabinX) < 6.5f && Mathf.Abs(wz - cabinZ) < 6.5f) continue;
                        if (Mathf.Abs(wz - 52f) < 3.5f && Mathf.Abs(wx - RoadCenterX(52f)) < 11f) continue;

                        float nx = (wx + TerrainWidth * 0.5f) / TerrainWidth;
                        float nz = wz / TerrainLength;
                        if (nx < 0.02f || nx > 0.98f || nz < 0.02f || nz > 0.98f) continue;

                        float worldY = td.GetInterpolatedHeight(nx, nz);
                        float slope = td.GetSteepness(nx, nz);

                        // Chi moc cay o chan thung lung va suon thoai (khong moc tren vach da qua dung)
                        if (worldY > 19f || slope > 27f) continue;

                        // Chon loai cay (uu tien tan rong Oak/Ash/BEDRILL sat duong giong anh mau)
                        int protoIdx;
                        double pick = rng.NextDouble();
                        if (pick < 0.34) protoIdx = 0;       // Oak (tan rong)
                        else if (pick < 0.56) protoIdx = 1;  // Ash
                        else if (pick < 0.72) protoIdx = 4 % protos.Count; // BEDRILL 1
                        else if (pick < 0.85) protoIdx = 5 % protos.Count; // BEDRILL 2
                        else if (pick < 0.93) protoIdx = 2;  // Birch
                        else protoIdx = 3;                   // Poplar

                        float hScale = Mathf.Lerp(0.72f, 1.32f, (float)rng.NextDouble());
                        float wScale = hScale * Mathf.Lerp(0.90f, 1.15f, (float)rng.NextDouble());
                        byte tint = (byte)rng.Next(215, 255);

                        TreeInstance ti = new TreeInstance
                        {
                            prototypeIndex = protoIdx,
                            position = new Vector3(nx, worldY / TerrainMaxHeight, nz),
                            widthScale = wScale,
                            heightScale = hScale,
                            rotation = (float)(rng.NextDouble() * Mathf.PI * 2.0),
                            color = new Color32(tint, 255, tint, 255),
                            lightmapColor = Color.white
                        };
                        instances.Add(ti);
                    }
                }
            }

            td.SetTreeInstances(instances.ToArray(), true);
            Debug.Log($"[BuildBTTH06Scene] Da trong {instances.Count} cay bang cong cu Paint Trees cua Terrain!");
        }

        private static float RidgedNoise(float x, float z)
        {
            float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x, z) * 2f - 1f);
            return n * n;
        }

        private static float ErodedAlpinePeak(float wx, float wz, float px, float pz, float radius, float height, float r2, float r3)
        {
            float dx = wx - px;
            float dz = wz - pz;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist >= radius * 1.45f) return 0f;

            // Tao 5 song nui toa ra tu dinh (aretes) giup ngon nui nhon sac tu nhien nhu nui Alps
            float angle = Mathf.Atan2(dz, dx);
            float ridges = 0.5f + 0.5f * Mathf.Cos(angle * 5f + r2 * 2.2f);
            float effectiveR = radius * (0.86f + 0.24f * ridges);
            float t = Mathf.Clamp01(1f - dist / effectiveR);
            float profile = Mathf.Pow(t, 1.55f);
            return height * profile * (0.86f + 0.14f * r2 + 0.06f * r3);
        }

        private static float ErodedBroadMountain(float wx, float wz, float px, float pz, float radius, float height, float r1, float r2)
        {
            float dx = (wx - px) / (radius * 1.25f);
            float dz = (wz - pz) / radius;
            float r2Dist = dx * dx + dz * dz;
            if (r2Dist > 2.2f) return 0f;
            return height * Mathf.Exp(-r2Dist * 1.45f) * (0.84f + 0.16f * r1 + 0.08f * r2);
        }

        private static float SmoothMax(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(a, b, h) + k * h * (1f - h) * 0.35f;
        }

        private static TerrainLayer CreateTerrainLayer(string name, string texPath, string nrmPath, Vector2 tileSize, float normalScale)
        {
            string path = $"{MatDir}/{name}.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
            layer.normalScale = normalScale;
            layer.tileSize = tileSize;
            layer.smoothness = 0.06f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ==================== POLYBRUSH SIDE HILLS ====================
        private static void BuildPolybrushSideHills(Terrain terrain, Dictionary<string, Material> mats)
        {
            CreatePolybrushHill("Polybrush_SideHill_L", terrain, new Vector3(-22f, 0f, 38f), 14f, 20f, 2.0f, mats["polybrush_hill"]);
            CreatePolybrushHill("Polybrush_SideHill_R", terrain, new Vector3( 22f, 0f, 64f), 14f, 20f, 2.2f, mats["polybrush_hill"]);
        }

        private static void CreatePolybrushHill(string name, Terrain terrain, Vector3 pos, float w, float d, float maxH, Material mat)
        {
            float baseY = terrain.SampleHeight(pos) + terrain.transform.position.y - 0.35f;
            ProBuilderMesh pb = ShapeGenerator.GeneratePlane(PivotLocation.Center, w, d, 14, 14, Axis.Up);
            pb.gameObject.name = name;
            pb.transform.position = new Vector3(pos.x, baseY, pos.z);

            var verts = new List<Vector3>(pb.positions);
            Color[] colors = new Color[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                float nx = v.x / (w * 0.5f);
                float nz = v.z / (d * 0.5f);
                float h = Mathf.Exp(-(nx * nx + nz * nz) * 2.4f) * maxH;
                verts[i] = new Vector3(v.x, v.y + h, v.z);
                colors[i] = Color.Lerp(new Color(0.55f, 0.48f, 0.34f), new Color(0.34f, 0.52f, 0.22f), Mathf.Clamp01(h / maxH));
            }
            pb.positions = verts;
            pb.colors = colors;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
            pb.ToMesh();
            pb.Refresh();
            pb.gameObject.AddComponent<MeshCollider>();
        }

        // ==================== PROBUILDER CUSTOM 3D MODELS ====================
        private static void BuildProBuilderStructures(Terrain terrain, Dictionary<string, Material> mats)
        {
            // 1. Cong chao go/da dau thung lung (dat o Z=52 de goc nhin toan canh thoang dep)
            float gateZ = 52f;
            float gateX = RoadCenterX(gateZ);
            float gateY = terrain.SampleHeight(new Vector3(gateX, 0f, gateZ)) + terrain.transform.position.y;
            GameObject gate = new GameObject("ProBuilder_MountainGate");
            gate.transform.position = new Vector3(gateX, gateY, gateZ);

            CreatePBCube("StoneBase_L", gate.transform, new Vector3(-8.2f, 0.7f, 0f), new Vector3(1.5f, 1.4f, 1.5f), mats["stone"]);
            CreatePBCube("StoneBase_R", gate.transform, new Vector3( 8.2f, 0.7f, 0f), new Vector3(1.5f, 1.4f, 1.5f), mats["stone"]);
            CreatePBCube("Pillar_L", gate.transform, new Vector3(-8.2f, 3.6f, 0f), new Vector3(0.7f, 5.2f, 0.7f), mats["wood_dark"]);
            CreatePBCube("Pillar_R", gate.transform, new Vector3( 8.2f, 3.6f, 0f), new Vector3(0.7f, 5.2f, 0.7f), mats["wood_dark"]);
            CreatePBCube("CrossBeam_Main", gate.transform, new Vector3(0f, 6.2f, 0f), new Vector3(18.4f, 0.85f, 0.95f), mats["wood"]);
            CreatePBCube("Gate_Roof", gate.transform, new Vector3(0f, 6.85f, 0f), new Vector3(19.2f, 0.45f, 1.65f), mats["roof"]);

            // 2. Hang rao go ven duong
            GameObject fence = new GameObject("ProBuilder_RoadFence");
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = 20f; z <= 48f; z += 4.5f)
                {
                    float fx = RoadCenterX(z) + side * 8.6f;
                    float fy = terrain.SampleHeight(new Vector3(fx, 0f, z)) + terrain.transform.position.y;
                    CreatePBCube($"Post_{(side < 0 ? "L" : "R")}_{z:0}", fence.transform, new Vector3(fx, fy + 0.65f, z), new Vector3(0.24f, 1.3f, 0.24f), mats["wood_dark"]);
                    if (z < 45f)
                    {
                        float nz = z + 4.5f;
                        float nfx = RoadCenterX(nz) + side * 8.6f;
                        float nfy = terrain.SampleHeight(new Vector3(nfx, 0f, nz)) + terrain.transform.position.y;
                        Vector3 mid = new Vector3((fx + nfx) * 0.5f, (fy + nfy) * 0.5f, (z + nz) * 0.5f);
                        CreatePBCube($"RailTop_{(side < 0 ? "L" : "R")}_{z:0}", fence.transform, mid + Vector3.up * 1.0f, new Vector3(0.14f, 0.14f, 4.6f), mats["wood"]);
                        CreatePBCube($"RailMid_{(side < 0 ? "L" : "R")}_{z:0}", fence.transform, mid + Vector3.up * 0.5f, new Vector3(0.14f, 0.14f, 4.6f), mats["wood"]);
                    }
                }
            }

            // 3. Tram nghi chan ven nui
            float cabZ = 76f;
            float cabX = RoadCenterX(cabZ) - 15.5f;
            float cabY = terrain.SampleHeight(new Vector3(cabX, 0f, cabZ)) + terrain.transform.position.y;
            GameObject cabin = new GameObject("ProBuilder_RestCabin");
            cabin.transform.position = new Vector3(cabX, cabY, cabZ);
            cabin.transform.rotation = Quaternion.Euler(0f, 18f, 0f);

            CreatePBCube("Cabin_Deck", cabin.transform, new Vector3(0f, 0.25f, 0f), new Vector3(5.6f, 0.5f, 4.8f), mats["stone"]);
            CreatePBCube("Cabin_WallBack", cabin.transform, new Vector3(-2.4f, 1.8f, 0f), new Vector3(0.28f, 3.0f, 4.4f), mats["wood"]);
            CreatePBCube("Cabin_PostFL", cabin.transform, new Vector3(2.4f, 1.8f, -2.0f), new Vector3(0.3f, 3.0f, 0.3f), mats["wood_dark"]);
            CreatePBCube("Cabin_PostFR", cabin.transform, new Vector3(2.4f, 1.8f,  2.0f), new Vector3(0.3f, 3.0f, 0.3f), mats["wood_dark"]);
            CreatePBCube("Cabin_RoofBase", cabin.transform, new Vector3(0f, 3.45f, 0f), new Vector3(6.4f, 0.38f, 5.6f), mats["roof"]);
            CreatePBCube("Cabin_RoofTop", cabin.transform, new Vector3(0f, 3.95f, 0f), new Vector3(4.2f, 0.55f, 3.8f), mats["roof"]);
            CreatePBCube("Cabin_Bench", cabin.transform, new Vector3(-1.2f, 0.85f, 0f), new Vector3(0.8f, 0.5f, 3.2f), mats["wood_dark"]);
        }

        private static GameObject CreatePBCube(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            ProBuilderMesh pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            pb.gameObject.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localPosition = localPos;
            pb.GetComponent<MeshRenderer>().sharedMaterial = mat;
            pb.ToMesh();
            pb.Refresh();
            pb.gameObject.AddComponent<BoxCollider>();
            return pb.gameObject;
        }

        // ==================== DRIVABLE ASSET STORE CAR (INTERCEPTOR) ====================
        private static Transform BuildDrivableValleyCar(Terrain terrain)
        {
            float startZ = 28f;
            float startX = RoadCenterX(startZ);
            float startY = terrain.SampleHeight(new Vector3(startX, 0f, startZ)) + terrain.transform.position.y + 0.04f;

            GameObject car = new GameObject("PlayerCar_Valley");
            car.transform.position = new Vector3(startX, startY, startZ);
            car.transform.rotation = Quaternion.identity;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InterceptorPrefabPath);
            GameObject visual = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, car.transform)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Interceptor_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            foreach (var mc in visual.GetComponentsInChildren<MeshCollider>())
                UnityEngine.Object.DestroyImmediate(mc);
            foreach (var a in visual.GetComponentsInChildren<Animator>())
                UnityEngine.Object.DestroyImmediate(a);

            List<Transform> wheelList = new List<Transform>();
            foreach (Transform t in visual.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("Wheel_")) wheelList.Add(t);
            }

            BoxCollider col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.78f, 0.15f);
            col.size = new Vector3(2.05f, 1.5f, 4.95f);

            Light spotL = CreateSpotLight("Headlight_L", car.transform, new Vector3(-0.68f, 0.72f, 2.55f));
            Light spotR = CreateSpotLight("Headlight_R", car.transform, new Vector3( 0.68f, 0.72f, 2.55f));

            AudioSource engineAudio = car.AddComponent<AudioSource>();
            engineAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/car_engine.wav");
            engineAudio.loop = true;
            engineAudio.playOnAwake = true;
            engineAudio.volume = 0.25f;

            AudioSource sirenAudio = car.AddComponent<AudioSource>();
            sirenAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/police_siren.wav");
            sirenAudio.loop = true;
            sirenAudio.playOnAwake = false;
            sirenAudio.volume = 0.28f;

            ValleyCarController ctrl = car.AddComponent<ValleyCarController>();
            ctrl.valleyTerrain = terrain;
            ctrl.wheels = wheelList.ToArray();
            ctrl.leftHeadlight = spotL;
            ctrl.rightHeadlight = spotR;
            ctrl.engineAudio = engineAudio;
            ctrl.sirenAudio = sirenAudio;
            ctrl.minX = -32f;
            ctrl.maxX = 32f;
            ctrl.minZ = 10f;
            ctrl.maxZ = 225f;

            return car.transform;
        }

        private static Light CreateSpotLight(string name, Transform parent, Vector3 localPos)
        {
            GameObject o = new GameObject(name);
            o.transform.SetParent(parent, false);
            o.transform.localPosition = localPos;
            o.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Light l = o.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.96f, 0.86f);
            l.intensity = 28f;
            l.range = 30f;
            l.spotAngle = 55f;
            l.shadows = LightShadows.None;
            return l;
        }

        // ==================== ANIMATED HELICOPTER PATROL ====================
        private static Transform BuildAnimatedHelicopter()
        {
            GameObject heli = new GameObject("Helicopter_Patrol");
            heli.transform.position = new Vector3(-12f, 28f, 80f);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HelicopterPrefabPath);
            GameObject visual = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, heli.transform)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Helicopter_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.85f;

            foreach (var a in visual.GetComponentsInChildren<Animator>())
                UnityEngine.Object.DestroyImmediate(a);

            HelicopterRotorSpinner spinner = heli.AddComponent<HelicopterRotorSpinner>();
            foreach (Transform t in visual.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Main_Rotor") spinner.mainRotor = t;
                else if (t.name == "Tail_Rotor") spinner.tailRotor = t;
            }

            string clipPath = AnimDir + "/BTTH06_Helicopter_Patrol.anim";
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            float[] times = { 0f, 8f, 10f, 18f, 20f };
            Vector3[] pts =
            {
                new(-12f, 28f,  75f),
                new( 14f, 32f, 195f),
                new( 14f, 32f, 195f),
                new(-12f, 28f,  75f),
                new(-12f, 28f,  75f)
            };
            float[] rotsY = { 8f, 8f, 188f, 188f, 368f };

            clip.SetCurve("", typeof(Transform), "localPosition.x", MakeCurve(times, i => pts[i].x));
            clip.SetCurve("", typeof(Transform), "localPosition.y", MakeCurve(times, i => pts[i].y));
            clip.SetCurve("", typeof(Transform), "localPosition.z", MakeCurve(times, i => pts[i].z));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", MakeCurve(times, i => 4f));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", MakeCurve(times, i => rotsY[i]));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", MakeCurve(times, i => 0f));

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
                AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);

            string ctrlPath = AnimDir + "/BTTH06_Helicopter.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
                AssetDatabase.DeleteAsset(ctrlPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            AnimatorState state = controller.layers[0].stateMachine.AddState("BTTH06_Helicopter_Patrol");
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);

            Animator anim = heli.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            return heli.transform;
        }

        private static AnimationCurve MakeCurve(float[] times, Func<int, float> valFn)
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
    }
}
