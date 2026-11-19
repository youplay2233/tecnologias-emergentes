using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using GLTFast.Schema;
using UnityEngine;
using Material = UnityEngine.Material;

// glTFast crea sus propios materiales con shaders que en el Quest podrían no estar en la build.
// Este generador usa en su lugar el material Pieza_Base (Standard, incluido vía Resources) con el
// color, lo metálico y la rugosidad que trae cada pieza en el GLB.
public class MaterialesRobot : IMaterialGenerator
{
    public Material GetDefaultMaterial(bool pointsSupport = false)
    {
        return Nuevo(new Color(0.78f, 0.8f, 0.83f), 0.3f, 0.55f);
    }

    public Material GenerateMaterial(MaterialBase gltfMaterial, IGltfReadable gltf, bool pointsSupport = false)
    {
        PbrMetallicRoughnessBase pbr = gltfMaterial != null ? gltfMaterial.PbrMetallicRoughness : null;
        if (pbr == null) return GetDefaultMaterial(pointsSupport);
        Material m = Nuevo(pbr.BaseColor, pbr.metallicFactor, pbr.roughnessFactor);
        m.name = string.IsNullOrEmpty(gltfMaterial.name) ? "Pieza" : gltfMaterial.name;
        return m;
    }

    public void SetLogger(ICodeLogger logger) { }

    public static Material Nuevo(Color color, float metalico, float rugosidad)
    {
        Material plantilla = Holo.MatPiezaBase;
        Material m = plantilla != null ? new Material(plantilla) : new Material(Shader.Find("Standard"));
        m.color = color;
        m.SetFloat("_Metallic", Mathf.Clamp01(metalico));
        m.SetFloat("_Glossiness", Mathf.Clamp01(1f - rugosidad));
        return m;
    }
}
