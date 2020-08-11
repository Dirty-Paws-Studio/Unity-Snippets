using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Script.Extensions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
static class PaletteMeshVariantEditor
{
    private const int defaultWindowWidth = 400;
    private const int defaultWindowHalfWidthAdj = defaultWindowWidth / 2 - 5;
    private const int defaultWindowHeight = 320;

    [MenuItem("Assets/Create/Palette Mesh Variant")]
    static void PaletteMeshVariant()
    {
        ShowWindow();
    }

    private static void ShowWindow()
    {
        var window = EditorWindow.GetWindow(typeof(PaletteMeshVariantEditorWindow));
        window.titleContent.text = "Palette Mesh Variant Editor";
        window.minSize = new Vector2(defaultWindowWidth, defaultWindowHeight);
    }

    public class PaletteMeshVariantEditorWindow : EditorWindow
    {
        private static Material palette;
        private static bool paletteReadable;
        private static bool paletteError;

        private static GameObject sourceMeshGo;
        private static Mesh mesh;

        private static List<List<ColorDef>> colorList;

        private static Object selection;

        void Awake()
        {
            selection = Selection.activeObject;
        }

        void OnGUI()
        {
            // Mesh and Palette
            DrawSourcesSelection();

            EditorGUILayout.Space();

            // Color List (or infos)
            DrawColorSelection();

            EditorGUILayout.Space();

            // Button
            GUI.enabled = mesh != null && palette != null;

            List<ColorDef> selectedColors = null;
            if (GUI.enabled)
            {
                selectedColors = colorList.SelectMany(row => row).Where(cl => cl.Selected).ToList();
                GUI.enabled = selectedColors.Count > 0;
            }

            if (GUILayout.Button("Create Variants"))
            {
                CreateVariants(selectedColors);
                Close();
            }

            // Restore state
            GUI.enabled = true;
        }

        private void DrawSourcesSelection()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Sources", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            // Mesh selection
            var selectedSourceMeshGo = ObjectField("Mesh", sourceMeshGo);

            // If the selection has changed
            if (selectedSourceMeshGo != sourceMeshGo)
            {
                // Extract mesh if possible
                if (selectedSourceMeshGo)
                {
                    var meshFilter = selectedSourceMeshGo.GetComponent<MeshFilter>();
                    mesh = meshFilter ? meshFilter.sharedMesh : null;
                }
                else
                {
                    mesh = null;
                }

                // Extract palette if it has not been already set
                if (palette == null && mesh != null)
                {
                    var renderer = selectedSourceMeshGo.GetComponent<MeshRenderer>();
                    var matOnSourceMeshGo = renderer ? renderer.sharedMaterial : null;
                    palette = ProcessInputPalette(matOnSourceMeshGo);
                }
            }

            sourceMeshGo = selectedSourceMeshGo;

            // Palette selection
            var selectedPalette = ObjectField("Palette", palette);
            // If selection has changed, extract colors
            if (selectedPalette != palette)
            {
                palette = ProcessInputPalette(selectedPalette);
                paletteError = selectedPalette && !palette;
            }

            EditorGUILayout.EndHorizontal();

            // Model Error Messages
            if (mesh == null && sourceMeshGo != null)
            {
                EditorGUILayout.Space();

                // sourceMeshGo has no direct mesh child
                var meshRenderersInChildren = sourceMeshGo.GetComponentsInChildren<MeshRenderer>();
                if (meshRenderersInChildren != null && meshRenderersInChildren.Length > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Selected asset includes multiple meshes. This is not supported by the Mesh Variant Editor.",
                        MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox("Selected asset does contain any mesh or it could not be found.",
                        MessageType.Error);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private Material ProcessInputPalette(Material tempPalette)
        {
            try
            {
                var paletteTexture = tempPalette ? tempPalette.mainTexture as Texture2D : null;
                if (!paletteTexture)
                {
                    return null;
                }

                Debug.Log("Processing Palette");
                palette = tempPalette;

                colorList = MatchGridColors(paletteTexture);
                paletteReadable = MakeReadable(paletteTexture);

                return tempPalette;
            }
            catch (Exception e)
            {
                // If any error occurs (there are lots and lots of reasons for it), log it and return null
                Debug.LogError(e);
                return null;
            }
        }

        private void DrawColorSelection()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Colors", EditorStyles.boldLabel);

            if (palette && (!paletteReadable || colorList.Count == 0))
            {
                EditorGUILayout.HelpBox("Palette texture is not readable.", MessageType.Error);
            }
            else if (paletteError)
            {
                EditorGUILayout.HelpBox("Palette texture could not be get from selected Material.", MessageType.Error);
            }
            else if (!palette)
            {
                GUILayout.Label("No Palette selected.");
            }
            else
            {
                // Iterate over the colors and create a colored Toggle for each
                foreach (var colorRow in colorList)
                {
                    EditorGUILayout.BeginHorizontal();

                    foreach (var colorDef in colorRow)
                    {
                        var style = new GUIStyle(GUI.skin.button);
                        var c = colorDef.Color;
                        var contrast = c.grayscale > 0.8 ? Color.black : Color.white;
                        style.normal.background = colorDef.Texture;
                        style.normal.textColor = colorDef.Selected ? contrast : c;
                        colorDef.Selected = GUILayout.Toggle(colorDef.Selected, "✔", style);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateVariants(List<ColorDef> selectedColors)
        {
            // Calculate palette size, as it is needed for aligning the uvs. Calculated here for caching
            var paletteSize = GetPaletteSize();

            foreach (var colorDef in selectedColors)
            {
                CreateVariant(colorDef, paletteSize);
            }
        }

        private void CreateVariant(ColorDef selectedColor, Vector2Int? paletteSize = null)
        {
            // Calculate palette size, as it is needed for aligning the uvs
            paletteSize = paletteSize ?? GetPaletteSize();
            // Create Variant
            CreateColoredAsset(selectedColor.PositionInPalette, paletteSize, true);
            // Reset selection
            selectedColor.Selected = false;
        }

        private void CreateColoredAsset(Vector2 uvOffset, Vector2Int? paletteSize = null,
            bool relativeToOrigin = false)
        {
            // We create two assets: The mesh and a prefab, as exporting a model file (obj, fbx, ...)
            // would require additional export packages.
            var sourcePath = AssetDatabase.GetAssetPath(sourceMeshGo.gameObject);
            var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
            var selectionPath = AssetDatabase.GetAssetPath(selection);
            var meshPath = $"{selectionPath}/{sourceFileName} - variant (mesh).asset";
            meshPath = AssetDatabase.GenerateUniqueAssetPath(meshPath);
            var prefabPath = $"{selectionPath}/{sourceFileName} - variant.prefab";
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

            // Copy mesh and change uvs on copy
            var copy = CopyMesh(mesh);
            copy.uv = copy.uv.Select(uv =>
            {
                // Normalize uvs by setting them into range x,y in [0, 1/paletteSize) and add offset.
                // This means that MULTI-COLOR uv setup are merged into one SINGLE COLOR cell!
                if (relativeToOrigin && paletteSize != null)
                {
                    var x = uv.x % (1f / paletteSize.Value.x) + uvOffset.x;
                    var y = uv.y % (1f / paletteSize.Value.y) + uvOffset.y;
                    return new Vector2(x, y);
                }
                else
                {
                    // Simple offset (not used anymore/currently)
                    return uv + uvOffset;
                }
            }).ToArray();

            // Create Prefab
            var goCopy = new GameObject();

            try
            {
                // Add mesh and palette material
                var mf = goCopy.AddComponent<MeshFilter>();
                mf.sharedMesh = copy;
                var mr = goCopy.AddComponent<MeshRenderer>();
                mr.sharedMaterial = palette;

                // Save Mesh
                AssetDatabase.CreateAsset(copy, meshPath);

                // Save prefab
                PrefabUtility.SaveAsPrefabAsset(goCopy, prefabPath);
            }
            finally
            {
                DestroyImmediate(goCopy);
            }
        }

        // Taken and adapted from https://answers.unity.com/questions/1424385/how-to-display-texture-field-with-label-on-top-lik.html
        private static T ObjectField<T>(string name, T texture) where T : Object
        {
            GUILayout.BeginVertical();
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = defaultWindowHalfWidthAdj;
            GUILayout.Label(name, style);
            var result =
                (T) EditorGUILayout.ObjectField(texture, typeof(T), false, GUILayout.Width(defaultWindowHalfWidthAdj),
                    GUILayout.Height(70));
            GUILayout.EndVertical();
            return result;
        }

        private Vector2Int GetPaletteSize()
        {
            if (colorList == null || colorList.Count == 0)
            {
                return Vector2Int.zero;
            }

            return new Vector2Int(colorList[0].Count, colorList.Count);
        }

        // Taken and adapted from the ClickToColor Source
        // Searches the texture for different colors
        // Sets up "grid" of colors
        private static List<List<ColorDef>> MatchGridColors(Texture2D _texture, Vector2[] uvs = null)
        {
            // I noticed that Unity did not reliably free the manually created textures. Better be safe than sorry here.
            if (colorList != null)
            {
                foreach (var row in colorList)
                {
                    foreach (var colorDef in row)
                    {
                        DestroyImmediate(colorDef.Texture);
                    }
                }

                colorList = null;
            }

            if (_texture == null)
            {
                return new List<List<ColorDef>>();
            }

            Vector2? lowestUv = uvs?.Length > 0 ? uvs.Min() : (Vector2?) null;

            int rows = 0, columns = 0;
            int tempRows = 1;
            int tempColumns = 1;
            Color tempColor1;
            Color tempColor2;

            var colorDefs = new List<ColorDef>();

            //Gets numbers of rows
            tempColor1 = _texture.GetPixel(0, 0);
            for (int i = 0; i < _texture.width; i++)
            {
                tempColor2 = _texture.GetPixel(i, 0);
                if (!CompareColors(tempColor1, tempColor2))
                {
                    tempRows++;
                    tempColor1 = tempColor2;
                }

                if (tempRows > 13)
                    break;
            }

            //gets number of columns
            tempColor1 = _texture.GetPixel(0, 0);
            for (int i = 0; i < _texture.height; i++)
            {
                tempColor2 = _texture.GetPixel(0, i);
                if (!CompareColors(tempColor1, tempColor2))
                {
                    tempColumns++;
                    tempColor1 = tempColor2;
                }

                if (tempColumns > 13)
                    break;
            }

            //Sets global variables to match findings
            rows = tempRows;
            columns = tempColumns;

            //steps through the grid to find the different colors and stores them on ColorList
            //Steps are based on row and columns numbers as well as the grid being uniform size
            int wFirstStep;
            int hFirstStep;
            int widthStep;
            int heightStep;
            widthStep = Mathf.RoundToInt(_texture.width / tempRows);
            wFirstStep = Mathf.RoundToInt(widthStep / 2);
            heightStep = Mathf.RoundToInt(_texture.height / tempColumns);
            hFirstStep = Mathf.RoundToInt(heightStep / 2);

            if (colorDefs != null)
                colorDefs.Clear();

            for (int j = 0; j < tempColumns; j++)
            {
                for (int i = 0; i < tempRows; i++)
                {
                    int xPos;
                    int yPos;
                    xPos = wFirstStep + widthStep * i;
                    yPos = _texture.height - (hFirstStep + heightStep * j);
                    Color tempColor;
                    tempColor = _texture.GetPixel(xPos, yPos);

                    var tex = new Texture2D(1, 1);
                    tex.SetPixel(0, 0, tempColor);
                    tex.Apply();

                    var colorState = new ColorDef
                    {
                        Color = tempColor,
                        Texture = tex,
                        PositionInPalette = new Vector2((float) i / tempColumns, (float) (tempRows - j - 1) / tempRows)
                    };

                    colorDefs.Add(colorState);
                }
            }

            return colorDefs.BreakIntoChunks(columns);
        }

        private static bool CompareColors(Color c1, Color c2)
        {
            var equal = c1.r == c2.r && c1.g == c2.g && c1.b == c2.b && c1.a == c2.a;
            return equal;
        }

        // Taken and adapted from the ClickToColor Source
        private static bool MakeReadable(Texture2D tex)
        {
            if (tex == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(tex);

            TextureImporter importer = (TextureImporter) AssetImporter.GetAtPath(path);
            //Save setting to reset after disable or unload of texture
            var isReadable = importer.isReadable;
            //txFormat = importer.textureFormat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            //importer.textureFormat = TextureImporterFormat.RGBA32;
            AssetDatabase.ImportAsset(path);

            return isReadable;
        }

        private static Mesh CopyMesh(Mesh source)
        {
            Mesh copy = new Mesh();
            copy.vertices = source.vertices.ToArray();
            copy.triangles = source.triangles.ToArray();
            copy.uv = source.uv.ToArray();
            copy.normals = source.normals.ToArray();
            copy.colors = source.colors.ToArray();
            copy.tangents = source.tangents.ToArray();
            return copy;
        }

        private class ColorDef
        {
            internal Color Color;
            internal bool Selected;
            internal Texture2D Texture;
            internal Vector2 PositionInPalette;
        }
    }
}
#endif