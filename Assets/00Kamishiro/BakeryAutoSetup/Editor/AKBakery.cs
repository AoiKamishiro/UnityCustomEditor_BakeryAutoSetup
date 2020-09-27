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

namespace Kamishiro
{
    public class AKBakery : EditorWindow
    {
        [MenuItem("Tools/Kamishiro/BakeryAutoSetup", priority = 150)]
        private static void OnEnable()
        {
            AKBakery window = GetWindow<AKBakery>("BakeryAutoSetup");
            window.minSize = new Vector2(320, 400);
            window.Show();
        }
        private void OnGUI()
        {
            if (GUILayout.Button("Press"))
            {
                SetupScenes();
            }
        }

        private void SetupScenes()
        {
            Light[] pointLights = new Light[] { };
            Light[] areaLights = new Light[] { };
            Light[] directionalLights = new Light[] { };
            BakerySkyLight[] skyLights = new BakerySkyLight[] { };

            Scene scene = SceneManager.GetActiveScene();
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
            //bakeryPointLight.color = light.color;
            //bakeryPointLight.intensity = light.intensity;
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
            bakeryPointLight.samples = 8;
            bakeryPointLight.projMode = light.type == LightType.Point ? BakeryPointLight.ftLightProjectionMode.Omni : BakeryPointLight.ftLightProjectionMode.Cookie;
            bakeryPointLight.cookie = light.type == LightType.Point ? null : AssetDatabase.LoadAssetAtPath(ftLightmaps.GetRuntimePath() + "ftUnitySpotTexture.bmp", typeof(Texture2D)) as Texture2D;
            bakeryPointLight.angle = light.spotAngle;
            bakeryPointLight.bitmask = 1;
            bakeryPointLight.indirectIntensity = light.bounceIntensity;
        }
        private void SetupAreaLight(Light light)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            MeshFilter meshFilter = light.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = light.GetComponent<MeshRenderer>();
            BakeryLightMesh bakeryLightMesh = light.GetComponent<BakeryLightMesh>();
            if (meshFilter == null) { meshFilter = light.gameObject.AddComponent<MeshFilter>(); }
            if (meshRenderer == null) { meshRenderer = light.gameObject.AddComponent<MeshRenderer>(); }
            if (bakeryLightMesh == null) { bakeryLightMesh = light.gameObject.AddComponent<BakeryLightMesh>(); }
            light.transform.localScale = Vector3.one;
            meshFilter.mesh = quad.GetComponent<MeshFilter>().mesh = quad.GetComponent<MeshFilter>().sharedMesh;
            meshRenderer.material = AssetDatabase.LoadAssetAtPath(ftLightmaps.GetRuntimePath() + "ftDefaultAreaLightMat.mat", typeof(Material)) as Material;

            float scale = light.transform.lossyScale.z;
            light.transform.localScale = new Vector3(light.areaSize.x / scale, light.areaSize.y / scale, 1);
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
            bakeryLightMesh.cutoff = light.range * 1.5f;
            bakeryLightMesh.selfShadow = false;
            bakeryLightMesh.indirectIntensity = light.bounceIntensity;

            light.tag = "EditorOnly";
            light.gameObject.isStatic = false;
            DestroyImmediate(quad);
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
            bakeryDirectLight.samples = 16;
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
        public string GetPath(Transform self)
        {
            string path = self.gameObject.name;

            Transform parent = self.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
        void GetLinearLightParameters(Light light, out float lightR, out float lightG, out float lightB, out float lightInt)
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