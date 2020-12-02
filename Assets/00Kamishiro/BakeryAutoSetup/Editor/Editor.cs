/*
 * Copyright (c) 2020 AoiKamishiro
 * 
 * This code is provided under the MIT license.
 * 
 */

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Kamishiro.UnityEditor.BakeryAutoSetup
{
    public class Editor : EditorWindow
    {
        SceneAsset modScene = null;
        int pointSamples = 8;
        int directSamples = 16;
        Vector2Int meshSamples = new Vector2Int(16, 256);
        float w1;
        PointLightMode mode = PointLightMode.Cookie;
        enum PointLightMode
        {
            Cookie,
            Cone
        }


        [MenuItem("Tools/Kamishiro/BakeryAutoSetup", priority = 150)]
        private static void OnEnable()
        {
            Editor window = GetWindow<Editor>("BakeryAutoSetup");
            window.minSize = new Vector2(320, 400);
        }
        private void OnGUI()
        {
            w1 = position.width / 5 * 3;
            EditorGUILayout.HelpBox("Light witch disabled in scene will be ignored.", MessageType.Info);
            UIHelper.ShurikenHeader("Settings");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target Scene", EditorStyles.boldLabel);
            modScene = (SceneAsset)EditorGUILayout.ObjectField(modScene, typeof(SceneAsset), false);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Samplese", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("DirectLight Shadow Samples", GUILayout.Width(w1));
                directSamples = EditorGUILayout.IntField(directSamples);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("PointLight Samples", GUILayout.Width(w1));
                pointSamples = EditorGUILayout.IntField(pointSamples);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("LightMesh Samples (Near/Far)", GUILayout.Width(w1));
                meshSamples[0] = EditorGUILayout.IntField(meshSamples[0]);
                meshSamples[1] = EditorGUILayout.IntField(meshSamples[1]);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PointLight Mode", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("PointLight Mode", GUILayout.Width(w1));
                mode = (PointLightMode)EditorGUILayout.EnumPopup(mode);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (GUILayout.Button("Start Process"))
            {
                SetupScenes();
            }

            UIHelper.ShurikenHeader("About");
            Version.DisplayVersion();
        }

        private void SetupScenes()
        {
            if (modScene == null)
            {
                Debug.LogWarning("[BakeryAutoSetup] SceneAsset is null.");
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(modScene));
            if (scene == null)
            {
                Debug.LogWarning("[BakeryAutoSetup] Scene cannot load.");
                return;
            }

            Light[] pointLights = new Light[] { };
            Light[] areaLights = new Light[] { };
            Light[] directionalLights = new Light[] { };
            BakerySkyLight[] skyLights = new BakerySkyLight[] { };
            GameObject[] rootObjects = scene.GetRootGameObjects();
            Transform[] transforms = new Transform[] { };

            foreach (GameObject gameObject in rootObjects)
            {
                transforms = transforms.Concat(new Transform[] { }).Concat(gameObject.GetComponentsInChildren<Transform>()).ToArray();
            }

            foreach (Transform transform in transforms)
            {
                Light light = transform.GetComponent<Light>();
                if (light != null && light.enabled && light.lightmapBakeType == LightmapBakeType.Baked && light.gameObject.activeInHierarchy)
                {
                    if (light.type == LightType.Point || light.type == LightType.Spot)
                    {
                        pointLights = pointLights.Concat(new Light[] { light }).ToArray();
                    }
                    if (light.type == LightType.Area)
                    {
                        areaLights = areaLights.Concat(new Light[] { light }).ToArray();
                    }
                    if (light.type == LightType.Directional)
                    {
                        directionalLights = directionalLights.Concat(new Light[] { light }).ToArray();
                    }
                }
                BakerySkyLight bakerySkyLight = transform.GetComponent<BakerySkyLight>();
                if (bakerySkyLight != null)
                {
                    skyLights = skyLights.Concat(new BakerySkyLight[] { bakerySkyLight }).ToArray();
                }
            }
            foreach (Light pointLight in pointLights)
            {
                SetupPointLight(pointLight);
                pointLight.enabled = false;
                pointLight.tag = "EditorOnly";
            }
            foreach (Light areaLight in areaLights)
            {
                SetupAreaLight(areaLight);
                areaLight.enabled = false;
                areaLight.tag = "EditorOnly";
            }
            foreach (Light directionalLight in directionalLights)
            {
                SetupDirectionalLight(directionalLight);
                directionalLight.enabled = false;
                directionalLight.tag = "EditorOnly";
            }
            if (skyLights.Length == 0)
            {
                SetupSkyKight();
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }
        private void SetupPointLight(Light light)
        {
            BakeryPointLight bakeryPointLight = light.GetComponent<BakeryPointLight>();
            if (bakeryPointLight == null) { bakeryPointLight = light.gameObject.AddComponent<BakeryPointLight>(); }
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                bakeryPointLight.color = light.color;
                bakeryPointLight.intensity = light.intensity;
            }
            else if (!GraphicsSettings.lightsUseLinearIntensity)
            {
                float lightR, lightG, lightB, lightInt;
                GetLinearLightParameters(light, out lightR, out lightG, out lightB, out lightInt);
                bakeryPointLight.color = new Color(lightR, lightG, lightB);
                bakeryPointLight.intensity = lightInt;
            }
            else
            {
                bakeryPointLight.color = light.color;
                bakeryPointLight.intensity = light.intensity;
            }
            bakeryPointLight.shadowSpread = 0.05f;
            bakeryPointLight.realisticFalloff = false;
            bakeryPointLight.falloffMinRadius = 1f;
            bakeryPointLight.cutoff = light.range;
            bakeryPointLight.samples = pointSamples;
            bakeryPointLight.projMode = light.type == LightType.Point ? BakeryPointLight.ftLightProjectionMode.Omni : mode == PointLightMode.Cone ? BakeryPointLight.ftLightProjectionMode.Cone : BakeryPointLight.ftLightProjectionMode.Cookie;
            bakeryPointLight.cookie = light.type == LightType.Point ? null : AssetDatabase.LoadAssetAtPath(ftLightmaps.GetRuntimePath() + "ftUnitySpotTexture.bmp", typeof(Texture2D)) as Texture2D;
            bakeryPointLight.angle = light.spotAngle;
            bakeryPointLight.bitmask = 1;
            bakeryPointLight.indirectIntensity = light.bounceIntensity;
        }
        private void SetupAreaLight(Light light)
        {
            BakeryLightMesh bakeryLightMesh = light.GetComponent<BakeryLightMesh>();
            if (bakeryLightMesh == null) { bakeryLightMesh = light.gameObject.AddComponent<BakeryLightMesh>(); }
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                bakeryLightMesh.color = light.color;
                bakeryLightMesh.intensity = light.intensity;
            }
            else if (!GraphicsSettings.lightsUseLinearIntensity)
            {
                float lightR, lightG, lightB, lightInt;
                GetLinearLightParameters(light, out lightR, out lightG, out lightB, out lightInt);
                bakeryLightMesh.color = new Color(lightR, lightG, lightB);
                bakeryLightMesh.intensity = lightInt;
            }
            else
            {
                bakeryLightMesh.color = light.color;
                bakeryLightMesh.intensity = light.intensity;
            }
            bakeryLightMesh.samples =meshSamples[1] ;
            bakeryLightMesh.samples2 = meshSamples[0];
            bakeryLightMesh.cutoff = light.range * 1.5f;
            bakeryLightMesh.selfShadow = false;
            bakeryLightMesh.bitmask=1;
            bakeryLightMesh.indirectIntensity = light.bounceIntensity;
        }
        private void SetupDirectionalLight(Light light)
        {
            BakeryDirectLight bakeryDirectLight = light.GetComponent<BakeryDirectLight>();
            if (bakeryDirectLight == null) { bakeryDirectLight = light.gameObject.AddComponent<BakeryDirectLight>(); }
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                bakeryDirectLight.color = light.color;
                bakeryDirectLight.intensity = light.intensity;
            }
            else if (!GraphicsSettings.lightsUseLinearIntensity)
            {
                float lightR, lightG, lightB, lightInt;
                GetLinearLightParameters(light, out lightR, out lightG, out lightB, out lightInt);
                bakeryDirectLight.color = new Color(lightR, lightG, lightB);
                bakeryDirectLight.intensity = lightInt;
            }
            else
            {
                bakeryDirectLight.color = light.color;
                bakeryDirectLight.intensity = light.intensity;
            }
            bakeryDirectLight.indirectIntensity = light.bounceIntensity;
            bakeryDirectLight.shadowSpread = 0.01f;
            bakeryDirectLight.samples = directSamples;
            bakeryDirectLight.bitmask = 1;
        }
        private void SetupSkyKight()
        {
            Material skyMat = RenderSettings.skybox;

            GameObject skyKight = new GameObject();
            BakerySkyLight bakerySkyLight = skyKight.AddComponent<BakerySkyLight>();
            skyKight.name = "Skylight";

            if (skyMat.HasProperty("_Tex") && skyMat.HasProperty("_Exposure") && skyMat.HasProperty("_Tint"))
            {
                bakerySkyLight.cubemap = skyMat.GetTexture("_Tex") as Cubemap;
                float exposure = skyMat.GetFloat("_Exposure");
                bool exposureSRGB = skyMat.shader.name == "Skybox/Cubemap";
                if (exposureSRGB)
                {
                    exposure = Mathf.Pow(exposure, 2.2f); // can't detect [Gamma] keyword...
                    exposure *= PlayerSettings.colorSpace == ColorSpace.Linear ? 4.59f : 2; // weird unity constant
                }
                bakerySkyLight.intensity = exposure;
                bakerySkyLight.color = skyMat.GetColor("_Tint");

                float matAngle = 0;
                if (skyMat.HasProperty("_Rotation")) matAngle = skyMat.GetFloat("_Rotation");
                var matQuat = Quaternion.Euler(0, matAngle, 0);
                bakerySkyLight.transform.rotation = matQuat;
            }
            skyKight.tag = "EditorOnly";
        }
        private void GetLinearLightParameters(Light light, out float lightR, out float lightG, out float lightB, out float lightInt)
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                lightInt = light.intensity;
                lightR = light.color.r;
                lightG = light.color.g;
                lightB = light.color.b;
                return;
            }

            if (!GraphicsSettings.lightsUseLinearIntensity)
            {
                lightR = Mathf.Pow(light.color.r * light.intensity, 2.2f);
                lightG = Mathf.Pow(light.color.g * light.intensity, 2.2f);
                lightB = Mathf.Pow(light.color.b * light.intensity, 2.2f);
                lightInt = Mathf.Max(Mathf.Max(lightR, lightG), lightB);
                lightR /= lightInt;
                lightG /= lightInt;
                lightB /= lightInt;
            }
            else
            {
                lightInt = light.intensity;
                lightR = light.color.linear.r;
                lightG = light.color.linear.g;
                lightB = light.color.linear.b;
            }
        }
    }
}