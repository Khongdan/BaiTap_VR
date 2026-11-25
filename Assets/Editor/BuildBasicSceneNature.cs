using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BT_VR.EditorTools
{
    /// <summary>
    /// Dung Scene BasicScene.unity theo de tai:
    /// "Xay dung canh quan thien nhien 3D" (Skybox Procedural + Terrain 500x500 + >=3 ngon nui
    /// + Thung lung + Dong song uon luon + Ho nuoc + Texture Co/Dat/Da + Trong cay Asset Store quanh song & ho).
    /// Khong tao mo hinh 3D tu ve hay script runtime moi; chi tao Texture cho Terrain Layer & dung Asset Store san co.
    /// </summary>
    public static class BuildBasicSceneNature
    {
        private const string ScenePath = "Assets/Scenes/BasicScene.unity";
        private const string MatDir = "Assets/Materials/BasicScene";
        private const string TexDir = "Assets/Textures/BasicScene";

        // Thong so Terrain tham khao dung bang huong dan (Width=500, Length=500, Height=100, PixelError=5, BaseMap=1024)
        private const float TerrainWidth = 500f;
        private const float TerrainLength = 500f;
        private const float TerrainHeight = 100f;
        private const float WaterLevel = 14.2f;

        [MenuItem("Tools/HUBT/Build BasicScene (Canh Quan Thien Nhien 3D)")]
        public static void BuildAll()
        {
            EnsureDirectories();
            BuildBTVRScene.UpgradeImportedAssetMaterialsToURP();

            // 1. Chi tao moi 3 bo Texture Co, Dat, Da (+ Normal Map) cho Paint Texture cua Terrain
            GenerateNatureTextures();
            AssetDatabase.Refresh();

            ConfigureRepeatTexture(TexDir + "/Grass_Albedo.png", false);
            ConfigureRepeatTexture(TexDir + "/Grass_Normal.png", true);
            ConfigureRepeatTexture(TexDir + "/Dirt_Albedo.png", false);
            ConfigureRepeatTexture(TexDir + "/Dirt_Normal.png", true);
            ConfigureRepeatTexture(TexDir + "/Rock_Albedo.png", false);
            ConfigureRepeatTexture(TexDir + "/Rock_Normal.png", true);

            // 2. Tao Skybox_BauTroi (Shader: Skybox/Procedural theo dung huong dan) va Material mat nuoc
            Material skyboxMat = CreateProceduralSkyboxMaterial();
            Material waterMat = CreateWaterMaterial();

            // 3. Tao moi BasicScene.unity gon sach (4 doi tuong: Main Camera, Directional Light, Terrain, Water_RiverAndLake)
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 4. Directional Light & Skybox Environment
            Light sunLight = SetupLightingAndSkybox(skyboxMat);

            // 5. Terrain 500x500x100: >=3 ngon nui, thung lung, dong song uon luon, ho nuoc, Texture Co/Dat/Da & Cay Asset Store
            Terrain terrain = BuildNatureTerrain();

            // 6. Mat nuoc cho song uon luon & ho nuoc (Plane nam dung muc WaterLevel)
            BuildWaterPlane(waterMat);

            // 7. Bo tri Main Camera quan sat toan canh dep nhat trong Game View
            SetupOverviewCamera(terrain, sunLight);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildBasicSceneNature] Da hoan thanh Canh quan thien nhien 3D trong: " + ScenePath);
        }

        public static void BuildAndCaptureGameView()
        {
            BuildAll();

            string artDir = @"C:\Users\KhongDan\.gemini\antigravity\brain\3f2f60a5-e517-4406-a760-808345ede584";
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                RenderToPng(mainCam, Path.Combine(artDir, "verify_basicscene_gameview.png"), 1280, 720, true);
            }

            // Chup them 1 goc tu tren cao de kiem tra ro duong cong dong song + ho nuoc + 4 ngon nui
            GameObject topCamObj = new GameObject("TempTopCam");
            Camera topCam = topCamObj.AddComponent<Camera>();
            topCam.fieldOfView = 55f;
            topCam.nearClipPlane = 0.3f;
            topCam.farClipPlane = 1200f;
            topCam.transform.position = new Vector3(135f, 118f, 75f);
            topCam.transform.LookAt(new Vector3(255f, 16f, 275f));
            RenderToPng(topCam, Path.Combine(artDir, "verify_basicscene_aerial.png"), 1280, 720, false);
            UnityEngine.Object.DestroyImmediate(topCamObj);
        }

        private static void EnsureDirectories()
        {
            string[] dirs = { "Assets/Scenes", MatDir, TexDir };
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

        // ==================== SKYBOX PROCEDURAL & DIRECTIONAL LIGHT ====================
        private static Material CreateProceduralSkyboxMaterial()
        {
            string path = MatDir + "/Skybox_BauTroi.mat";
            Shader skyShader = Shader.Find("Skybox/Procedural");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(skyShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = skyShader;
            }

            mat.SetFloat("_SunDisk", 2f); // High Quality Sun
            mat.SetFloat("_SunSize", 0.045f);
            mat.SetFloat("_SunSizeConvergence", 5.5f);
            mat.SetFloat("_AtmosphereThickness", 0.85f);
            // Trong Unity Skybox/Procedural, SkyTint = (0.5, 0.5, 0.5) cho bau troi xanh tu nhien chuan nhat
            mat.SetColor("_SkyTint", new Color(0.50f, 0.50f, 0.50f, 1f));
            mat.SetColor("_GroundColor", new Color(0.36f, 0.43f, 0.48f, 1f));
            mat.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateWaterMaterial()
        {
            string path = MatDir + "/Water_Nature.mat";
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = urpLit;
            }

            mat.SetColor("_BaseColor", new Color(0.08f, 0.46f, 0.66f, 0.82f));
            mat.SetFloat("_Metallic", 0.15f);
            mat.SetFloat("_Smoothness", 0.94f);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Light SetupLightingAndSkybox(Material skyboxMat)
        {
            GameObject lightObj = new GameObject("Directional Light");
            // Anh sang nang chieu xien tao chieu sau cho nui, thung lung, mat ho & bong cay
            lightObj.transform.rotation = Quaternion.Euler(26f, -34f, 0f);
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 1.45f;
            dirLight.color = new Color(1f, 0.95f, 0.85f);
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.82f;

            RenderSettings.skybox = skyboxMat;
            RenderSettings.sun = dirLight;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.83f, 0.92f);
            RenderSettings.fogStartDistance = 260f;
            RenderSettings.fogEndDistance = 780f;

            return dirLight;
        }

        // ==================== TEXTURES: CO, DAT, DA (+ NORMAL MAPS) ====================
        private static void GenerateNatureTextures()
        {
            // 1. Grass (Co xanh muot tu nhien)
            CreateAlbedoAndNormal(TexDir + "/Grass_Albedo.png", TexDir + "/Grass_Normal.png", 512, 11, 2.0f,
                (n1, n2, n3, ridge) =>
                {
                    Color c1 = new Color(0.24f, 0.48f, 0.17f);
                    Color c2 = new Color(0.42f, 0.62f, 0.22f);
                    return Color.Lerp(c1, c2, Mathf.Clamp01(n1 * 0.5f + n2 * 0.3f + n3 * 0.2f));
                });

            // 2. Dirt (Dat nau / bo song & bo ho)
            CreateAlbedoAndNormal(TexDir + "/Dirt_Albedo.png", TexDir + "/Dirt_Normal.png", 512, 27, 2.4f,
                (n1, n2, n3, ridge) =>
                {
                    Color c1 = new Color(0.46f, 0.35f, 0.23f);
                    Color c2 = new Color(0.66f, 0.54f, 0.38f);
                    return Color.Lerp(c1, c2, Mathf.Clamp01(n1 * 0.55f + n2 * 0.28f + n3 * 0.17f));
                });

            // 3. Rock (Da nui xam nau go ghe)
            CreateAlbedoAndNormal(TexDir + "/Rock_Albedo.png", TexDir + "/Rock_Normal.png", 512, 43, 5.0f,
                (n1, n2, n3, ridge) =>
                {
                    Color dark = new Color(0.22f, 0.21f, 0.20f);
                    Color mid = new Color(0.44f, 0.42f, 0.39f);
                    Color light = new Color(0.68f, 0.66f, 0.62f);
                    float v = Mathf.Clamp01(ridge * 0.52f + n1 * 0.30f + n3 * 0.18f);
                    return v < 0.5f ? Color.Lerp(dark, mid, v * 2f) : Color.Lerp(mid, light, (v - 0.5f) * 2f);
                });
        }

        private static void CreateAlbedoAndNormal(string albedoPath, string normalPath, int size, int seed, float bumpStrength,
            Func<float, float, float, float, Color> colorFn)
        {
            Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float[,] hBuf = new float[size, size];
            float ox = seed * 15.1f, oy = seed * 27.3f;

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float n1 = TilePerlin(u, v, 6f, ox, oy);
                    float n2 = TilePerlin(u, v, 16f, ox + 35f, oy + 20f);
                    float n3 = TilePerlin(u, v, 40f, ox + 85f, oy + 65f);
                    float ridge = 1f - Mathf.Abs(n2 * 2f - 1f);
                    ridge *= ridge;

                    hBuf[y, x] = n1 * 0.35f + ridge * 0.45f + n3 * 0.20f;
                    albedo.SetPixel(x, y, colorFn(n1, n2, n3, ridge));
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
                    Vector3 n = new Vector3(
                        -(hBuf[y, xp] - hBuf[y, xm]) * bumpStrength,
                        -(hBuf[yp, x] - hBuf[ym, x]) * bumpStrength,
                        1f).normalized;
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

        // ==================== TERRAIN GEOGRAPHY HELPERS ====================
        private static float SmoothRange(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float RidgedNoise(float x, float z)
        {
            float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x, z) * 2f - 1f);
            return n * n;
        }

        private static float SmoothMax(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(a, b, h) + k * h * (1f - h) * 0.35f;
        }

        /// <summary>
        /// Tam dong song uon luon hinh chu S chay tu khe nui (Z=450) do xuong ho nuoc (Z=185).
        /// </summary>
        private static float RiverCenterX(float z)
        {
            return 250f + Mathf.Sin((z - 180f) * 0.029f) * 42f + Mathf.Cos((z - 180f) * 0.013f) * 14f;
        }

        /// <summary>
        /// Khoang cach chuan hoa den tam ho nuoc (<= 1.0 la nam trong long ho).
        /// </summary>
        private static float LakeNormalizedDist(float x, float z)
        {
            float cx = 248f, cz = 168f;
            float n = (Mathf.PerlinNoise(x * 0.025f + 4f, z * 0.025f + 9f) - 0.5f) * 0.24f;
            float dx = (x - cx) / 56f;
            float dz = (z - cz) / 42f;
            return Mathf.Sqrt(dx * dx + dz * dz) + n;
        }

        private static float ErodedMountain(float x, float z, float px, float pz, float radius, float height, float r1, float r2)
        {
            float dx = x - px;
            float dz = z - pz;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist >= radius * 1.4f) return 0f;

            float angle = Mathf.Atan2(dz, dx);
            float ribs = 0.5f + 0.5f * Mathf.Cos(angle * 5f + r1 * 2.4f);
            float effR = radius * (0.85f + 0.25f * ribs);
            float t = Mathf.Clamp01(1f - dist / effR);
            return height * Mathf.Pow(t, 1.5f) * (0.85f + 0.15f * r1 + 0.08f * r2);
        }

        // ==================== BUILD NATURE TERRAIN (500x500x100) ====================
        private static Terrain BuildNatureTerrain()
        {
            string tdPath = MatDir + "/BasicScene_TerrainData.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath) != null)
                AssetDatabase.DeleteAsset(tdPath);

            TerrainData td = new TerrainData();
            td.heightmapResolution = 513;
            td.alphamapResolution = 512;
            td.baseMapResolution = 1024; // Dung bang thong so trong file (Base Map Resolution = 1024)
            td.size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength);

            AssetDatabase.CreateAsset(td, tdPath);

            // 1. 3 Terrain Layers: Co (Grass), Dat (Dirt), Da (Rock)
            TerrainLayer[] layers =
            {
                CreateTerrainLayer("Layer_Grass", TexDir + "/Grass_Albedo.png", TexDir + "/Grass_Normal.png", new Vector2(22f, 22f), 0.9f),
                CreateTerrainLayer("Layer_Dirt", TexDir + "/Dirt_Albedo.png", TexDir + "/Dirt_Normal.png", new Vector2(18f, 18f), 1.0f),
                CreateTerrainLayer("Layer_Rock", TexDir + "/Rock_Albedo.png", TexDir + "/Rock_Normal.png", new Vector2(28f, 28f), 1.6f)
            };
            td.terrainLayers = layers;

            // 2. Dieu khac Heightmap: Thung lung o giua + >= 3 ngon nui bao quanh + Dong song uon luon + Ho nuoc
            int hmRes = td.heightmapResolution;
            float[,] heights = new float[hmRes, hmRes];

            for (int iz = 0; iz < hmRes; iz++)
            {
                float nz = (float)iz / (hmRes - 1);
                float wz = nz * TerrainLength;
                float rcx = RiverCenterX(wz);

                for (int ix = 0; ix < hmRes; ix++)
                {
                    float nx = (float)ix / (hmRes - 1);
                    float wx = nx * TerrainWidth;

                    float r1 = RidgedNoise(wx * 0.011f + 8f, wz * 0.011f + 5f);
                    float r2 = RidgedNoise(wx * 0.026f + 22f, wz * 0.026f + 19f);
                    float soft = Mathf.PerlinNoise(wx * 0.014f + 50f, wz * 0.014f + 30f);

                    // Nen thung lung rong uon luon nhe o giua (X gan 250) va cao dan len 2 ben thanh long chao thung lung
                    float distCenter = Mathf.Abs(wx - 250f);
                    float valleyBowl = SmoothRange(75f, 220f, distCenter) * (16f + r1 * 22f);
                    float baseH = 18.2f + (soft - 0.5f) * 3.5f + valleyBowl;

                    // 4 NGON NUI HUNG VI (>= 3 ngon nui theo de bai):
                    // Nui 1 (Tay Bac - cao nhat)
                    float m1 = ErodedMountain(wx, wz, 130f, 390f, 115f, 84f, r1, r2);
                    // Nui 2 (Trung Tam phia sau - hung vi ngay cuoi thung lung)
                    float m2 = ErodedMountain(wx, wz, 265f, 435f, 105f, 76f, r1, r2);
                    // Nui 3 (Dong Bac - doi nui da rong)
                    float m3 = ErodedMountain(wx, wz, 395f, 375f, 118f, 80f, r1, r2);
                    // Nui 4 & 5 (Hai suon nui trai/phai om lay thung lung)
                    float m4 = ErodedMountain(wx, wz, 72f, 235f, 95f, 56f, r1, r2);
                    float m5 = ErodedMountain(wx, wz, 432f, 240f, 98f, 60f, r1, r2);

                    // Doi nho phia nam lam diem quan sat cho Camera toan canh
                    float southHill = Mathf.Exp(-Mathf.Pow((wx - 245f) / 85f, 2f) - Mathf.Pow((wz - 40f) / 48f, 2f)) * 18f;

                    float h = baseH + southHill;
                    h = SmoothMax(h, m1, 8f);
                    h = SmoothMax(h, m2, 8f);
                    h = SmoothMax(h, m3, 8f);
                    h = SmoothMax(h, m4, 8f);
                    h = SmoothMax(h, m5, 8f);

                    // KHAC LONG DONG SONG UON LUON (Z = 175 -> 435) xuong duoi WaterLevel (14.2m)
                    if (wz >= 155f && wz <= 445f)
                    {
                        float riverDist = Mathf.Abs(wx - rcx);
                        float riverWidth = Mathf.Lerp(17f, 11f, SmoothRange(175f, 420f, wz));
                        float riverEndFade = 1f - SmoothRange(395f, 445f, wz);
                        float riverCarve = (1f - SmoothRange(riverWidth * 0.55f, riverWidth * 1.45f, riverDist)) * riverEndFade;
                        if (riverCarve > 0.001f)
                        {
                            float riverBedY = 9.8f + SmoothRange(360f, 430f, wz) * 3.5f;
                            h = Mathf.Lerp(h, riverBedY, riverCarve);
                        }
                    }

                    // KHAC LONG HO NUOC RONG (tam X=248, Z=168) xuong duoi WaterLevel (14.2m)
                    float lakeD = LakeNormalizedDist(wx, wz);
                    float lakeCarve = 1f - SmoothRange(0.68f, 1.25f, lakeD);
                    if (lakeCarve > 0.001f)
                    {
                        float lakeBedY = 8.2f;
                        h = Mathf.Lerp(h, lakeBedY, lakeCarve);
                    }

                    heights[iz, ix] = Mathf.Clamp01(h / TerrainHeight);
                }
            }
            td.SetHeights(0, 0, heights);

            // 3. To Texture Co (0), Dat (1), Da (2)
            int amRes = td.alphamapResolution;
            float[,,] alpha = new float[amRes, amRes, 3];

            for (int iz = 0; iz < amRes; iz++)
            {
                float nz = (float)iz / (amRes - 1);
                float wz = nz * TerrainLength;
                float rcx = RiverCenterX(wz);

                for (int ix = 0; ix < amRes; ix++)
                {
                    float nx = (float)ix / (amRes - 1);
                    float wx = nx * TerrainWidth;

                    float worldY = td.GetInterpolatedHeight(nx, nz);
                    float slope = td.GetSteepness(nx, nz);
                    float n = Mathf.PerlinNoise(wx * 0.045f + 12f, wz * 0.045f + 17f);

                    // Da (Rock) tren cac ngon nui cao & suon doc
                    float wRockSlope = SmoothRange(19f, 32f, slope);
                    float wRockHeight = SmoothRange(34f + (n - 0.5f) * 8f, 48f, worldY);
                    float wRock = Mathf.Clamp01(Mathf.Max(wRockSlope, wRockHeight));

                    // Dat (Dirt) doc bo ho, long ho, bo song uon luon & cac mang dat tu nhien
                    float wShoreDirt = 1f - SmoothRange(WaterLevel + 0.8f, WaterLevel + 3.4f + (n - 0.5f) * 1.4f, worldY);
                    float riverEdgeDirt = 0f;
                    if (wz >= 165f && wz <= 425f)
                    {
                        float rDist = Mathf.Abs(wx - rcx);
                        riverEdgeDirt = (1f - SmoothRange(11f, 22f, rDist)) * (1f - SmoothRange(380f, 425f, wz));
                    }
                    float dirtPatch = SmoothRange(0.68f, 0.85f, n) * 0.35f;
                    float wDirt = Mathf.Clamp01(Mathf.Max(wShoreDirt, riverEdgeDirt * 0.75f, dirtPatch)) * (1f - wRock * 0.7f);

                    // Co (Grass) bao phu thung lung va chan doi
                    float wGrass = Mathf.Clamp01(1f - wRock - wDirt);

                    float sum = wGrass + wDirt + wRock;
                    if (sum <= 0.0001f) { wGrass = 1f; sum = 1f; }

                    alpha[iz, ix, 0] = wGrass / sum;
                    alpha[iz, ix, 1] = wDirt / sum;
                    alpha[iz, ix, 2] = wRock / sum;
                }
            }
            td.SetAlphamaps(0, 0, alpha);

            // 4. Trong cay Asset Store xung quanh Ho nuoc va doc 2 ben Dong song uon luon bang Terrain Paint Trees
            PlantAssetStoreTreesAroundLakeAndRiver(td);

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            GameObject terrainObj = Terrain.CreateTerrainGameObject(td);
            terrainObj.name = "Terrain";
            terrainObj.transform.position = Vector3.zero;
            Terrain terrain = terrainObj.GetComponent<Terrain>();
            if (GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial != null)
                terrain.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;

            // Thong so hien thi dung bang trong file (Pixel Error = 5)
            terrain.heightmapPixelError = 5f;
            terrain.treeDistance = 650f;
            terrain.treeBillboardDistance = 320f;
            terrain.treeMaximumFullLODCount = 500;
            terrain.treeCrossFadeLength = 25f;
            return terrain;
        }

        private static void PlantAssetStoreTreesAroundLakeAndRiver(TerrainData td)
        {
            // Chi su dung cac prefab cay Asset Store da tai san
            string[] treePaths =
            {
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Oak/Oak.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Ash/Ash.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Birch/Birch.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Poplar/Poplar.prefab",
                "Assets/Happy Little Trees - Free nature pack by Nebula/Prefabs/Trees/Spruce/Spruce_008.prefab",
                "Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Prefabs/Tree_free_01_Prefab.prefab",
                "Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Prefabs/Tree_free_02_Prefab.prefab",
                "Assets/BEDRILL/Lowpoly_Trees_Cartoon_Free/Prefabs/Tree_free_03_Prefab.prefab"
            };

            List<TreePrototype> protos = new List<TreePrototype>();
            foreach (string p in treePaths)
            {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (pf != null) protos.Add(new TreePrototype { prefab = pf, bendFactor = 0.25f });
            }
            td.treePrototypes = protos.ToArray();
            if (protos.Count == 0) return;

            List<TreeInstance> instances = new List<TreeInstance>();
            System.Random rng = new System.Random(20261125);

            // A. Trong cay thanh dai tu nhien bao quanh HO NUOC
            for (int i = 0; i < 160; i++)
            {
                float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                // Giu thoang nhe mat truoc ho (huong Camera Z < 115) de khong che mat ho
                float distFactor = Mathf.Lerp(1.06f, 1.65f, (float)rng.NextDouble());
                float wx = 248f + Mathf.Cos(ang) * 56f * distFactor;
                float wz = 168f + Mathf.Sin(ang) * 42f * distFactor;
                if (wz < 118f && Mathf.Abs(wx - 248f) < 34f) continue;

                TryAddTree(td, instances, protos.Count, wx, wz, rng, 28f);
            }

            // B. Trong cay doc 2 ben bo DONG SONG UON LUON (Z = 200..410)
            for (float z = 200f; z <= 408f; z += 4.5f)
            {
                float rcx = RiverCenterX(z);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int row = 0; row < 2; row++)
                    {
                        float off = (row == 0 ? 18f : 31f) + (float)(rng.NextDouble() * 12.0);
                        float wx = rcx + side * off;
                        float wz = z + (float)(rng.NextDouble() * 4.5 - 2.25);
                        TryAddTree(td, instances, protos.Count, wx, wz, rng, 34f);
                    }
                }
            }

            // C. Them mot so cum cay rai rac o trien doi thung lung cho canh quan phong phu tu nhien
            for (int i = 0; i < 140; i++)
            {
                float wx = Mathf.Lerp(115f, 385f, (float)rng.NextDouble());
                float wz = Mathf.Lerp(130f, 360f, (float)rng.NextDouble());
                if (wz < 125f && Mathf.Abs(wx - 245f) < 48f) continue;
                TryAddTree(td, instances, protos.Count, wx, wz, rng, 40f);
            }

            td.SetTreeInstances(instances.ToArray(), true);
            Debug.Log($"[BuildBasicSceneNature] Da trong {instances.Count} cay Asset Store xung quanh ho va song!");
        }

        private static void TryAddTree(TerrainData td, List<TreeInstance> list, int protoCount, float wx, float wz, System.Random rng, float minDistSq)
        {
            float nx = wx / TerrainWidth;
            float nz = wz / TerrainLength;
            if (nx < 0.03f || nx > 0.97f || nz < 0.03f || nz > 0.97f) return;

            float worldY = td.GetInterpolatedHeight(nx, nz);
            float slope = td.GetSteepness(nx, nz);

            // Tuyet doi khong trong cay duoi nuoc (WaterLevel = 14.2) hoac tren vach da qua doc
            if (worldY < WaterLevel + 0.95f || worldY > 38f || slope > 26f) return;

            for (int j = 0; j < list.Count; j++)
            {
                float ex = list[j].position.x * TerrainWidth;
                float ez = list[j].position.z * TerrainLength;
                if ((ex - wx) * (ex - wx) + (ez - wz) * (ez - wz) < minDistSq) return;
            }

            int pIdx = rng.Next(protoCount);
            float hScale = Mathf.Lerp(0.85f, 1.45f, (float)rng.NextDouble());
            byte tint = (byte)rng.Next(220, 255);

            list.Add(new TreeInstance
            {
                prototypeIndex = pIdx,
                position = new Vector3(nx, worldY / TerrainHeight, nz),
                widthScale = hScale * Mathf.Lerp(0.92f, 1.12f, (float)rng.NextDouble()),
                heightScale = hScale,
                rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                color = new Color32(tint, 255, tint, 255),
                lightmapColor = Color.white
            });
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
            layer.smoothness = 0.07f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ==================== WATER PLANE (RIVER & LAKE) ====================
        private static void BuildWaterPlane(Material waterMat)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water_RiverAndLake";
            water.transform.position = new Vector3(250f, WaterLevel, 280f);
            water.transform.localScale = new Vector3(36f, 1f, 36f);
            water.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
            UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
        }

        // ==================== PANORAMIC MAIN CAMERA ====================
        private static void SetupOverviewCamera(Terrain terrain, Light sunLight)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            camObj.AddComponent<AudioListener>();

            // Bo tri Camera o goc cao phia Tay Nam nhin ro ca Ho nuoc, Dong song uon luon chu S, Thung lung & 3+ Ngon nui
            Vector3 camPos = new Vector3(198f, 70f, 64f);
            float groundHere = terrain.SampleHeight(camPos);
            camPos.y = Mathf.Max(camPos.y, groundHere + 24f);
            camObj.transform.position = camPos;
            camObj.transform.LookAt(new Vector3(256f, 18f, 255f));
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
    }
}
