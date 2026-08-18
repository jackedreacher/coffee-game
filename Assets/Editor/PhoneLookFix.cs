#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Fixes the two reasons the phone build does not look like the editor.
//
// Both were found from one screenshot: a wall of "Cannot create required
// material because shader is null" and a kitchen with the colour washed out of
// it. They are not the same bug but they are close relatives, and neither one
// can be seen from inside the editor -- which is exactly why they were shipped.
//
// 1. THE SHADER. Outlines/Scripts/RendererFeatures/EdgeDetection.cs reaches for
//    its shader with Shader.Find at runtime. Shader.Find only ever returns
//    shaders that made it into the build, and the build keeps a shader for one
//    of three reasons: a material references it, it sits in a Resources folder,
//    or it is in Always Included Shaders. Hidden/Edge Detection is none of
//    those -- nothing in the project has a material on it -- so it is stripped,
//    Find answers null, and the renderer feature retries once per camera per
//    frame forever. That is the wall of red.
//
// 2. THE COLOUR. The editor draws with the PC quality level and the phone draws
//    with Mobile, and they are two different render pipeline assets. The Mobile
//    one has Fast sRGB/Linear Conversions switched on, which is an approximation
//    of the conversion between colour spaces -- cheap, and visibly lighter. On
//    top of that the outline pass is the thing that puts the dark edges on
//    everything, and by (1) it is not running at all on the phone, so the whole
//    image loses its contrast twice over
public static class PhoneLookFix
{
    private const string edgeShaderName = "Hidden/Edge Detection";

    private const string edgeShaderPath = "Assets/Outlines/ShaderGraphs/EdgeDetection.shader";

    private const string mobileAssetPath = "Assets/Settings/Mobile_RPAsset.asset";

    [MenuItem("Cooked Fast/APK/3 - Telefon Gorunumunu Duzelt", priority = 302)]
    public static void Fix()
    {
        StringBuilder report = new StringBuilder();

        report.Append(IncludeShader());
        report.AppendLine();
        report.Append(FixColour());

        AssetDatabase.SaveAssets();

        report.AppendLine();
        report.AppendLine("Simdi APK'yi yeniden derle -- ikisi de derleme ayari,");
        report.AppendLine("Play modunda fark gorunmez.");
        report.AppendLine();
        report.AppendLine("  Cooked Fast > APK > 2 - Derle");

        Debug.Log("[Telefon]\n" + report);
        EditorUtility.DisplayDialog("Telefon Gorunumu", report.ToString(), "Tamam");
    }

    // ---- 1: the stripped shader ---------------------------------------------

    private static string IncludeShader()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("1) SHADER HATASI");

        Shader shader = Shader.Find(edgeShaderName);

        // Falls back to the file, because Shader.Find inside the editor answers
        // from everything on disk -- so a null here means the shader is not in
        // the project at all rather than merely stripped from a build
        if (shader == null)
            shader = AssetDatabase.LoadAssetAtPath<Shader>(edgeShaderPath);

        if (shader == null)
        {
            report.AppendLine("  " + edgeShaderName + " PROJEDE YOK.");
            report.AppendLine("  Beklenen yer: " + edgeShaderPath);
            report.AppendLine("  Outlines paketi eksik olabilir.");

            return report.ToString();
        }

        SerializedObject graphics = Settings("ProjectSettings/GraphicsSettings.asset");

        if (graphics == null)
        {
            report.AppendLine("  GraphicsSettings okunamadi.");
            return report.ToString();
        }

        SerializedProperty list = graphics.FindProperty("m_AlwaysIncludedShaders");

        if (list == null)
        {
            report.AppendLine("  Always Included Shaders listesi bulunamadi.");
            return report.ToString();
        }

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue != shader)
                continue;

            report.AppendLine("  " + edgeShaderName + " zaten listede.");
            report.AppendLine("  Hata devam ediyorsa APK eski -- yeniden derle.");

            return report.ToString();
        }

        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;

        graphics.ApplyModifiedProperties();

        report.AppendLine("  " + edgeShaderName + " -> Always Included Shaders");
        report.AppendLine("  Artik derlemeden atilmayacak, Shader.Find bulacak.");
        report.AppendLine("  Kirmizi yazi akini bitecek ve konturlar geri gelecek.");

        return report.ToString();
    }

    // ---- 2: the washed out colour -------------------------------------------

    private static string FixColour()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("2) SOLGUN RENKLER");

        RenderPipelineAsset mobile =
            AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(mobileAssetPath);

        if (mobile == null)
        {
            report.AppendLine("  " + mobileAssetPath + " bulunamadi.");
            return report.ToString();
        }

        report.AppendLine("  Editor PC kalitesinde, telefon Mobile kalitesinde --");
        report.AppendLine("  iki ayri URP asset'i. Fark buradan geliyor.");

        SerializedObject so = new SerializedObject(mobile);

        SerializedProperty fast = so.FindProperty("m_UseFastSRGBLinearConversion");

        if (fast == null)
        {
            report.AppendLine("  Fast sRGB alani bulunamadi, atlandi.");
            return report.ToString();
        }

        if (!fast.boolValue)
        {
            report.AppendLine("  Fast sRGB/Linear Conversions zaten kapali.");
            report.AppendLine("  Renk farki kalan tek sebepten geliyor: konturlar.");
            report.AppendLine("  Yukaridaki shader duzelmesi onu da cozer.");

            return report.ToString();
        }

        fast.boolValue = false;

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(mobile);

        report.AppendLine("  Fast sRGB/Linear Conversions: ACIK -> KAPALI");
        report.AppendLine("  Bu ayar renk uzayi donusumunu yaklasik hesapliyordu.");
        report.AppendLine("  Ucuz ama gozle gorulur derecede acik. Kapatildi.");
        report.AppendLine();
        report.AppendLine("  Geri almak istersen: " + mobileAssetPath);
        report.AppendLine("  > Quality > Use Fast sRGB/Linear Conversions");

        return report.ToString();
    }

    // Project settings are ordinary serialized assets on disk; they are just not
    // in the AssetDatabase, so they are opened by path rather than looked up
    private static SerializedObject Settings(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        return assets == null || assets.Length <= 0 || assets[0] == null
            ? null
            : new SerializedObject(assets[0]);
    }
}
#endif
