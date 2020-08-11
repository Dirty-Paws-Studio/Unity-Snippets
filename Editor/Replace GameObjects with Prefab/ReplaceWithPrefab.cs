using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
// Loosely based on the script by Jason Weimann, https://unity3d.college/2017/09/07/replace-gameobjects-or-prefabs-with-another-prefab/
public class ReplaceWithPrefab : EditorWindow
{
    private const int defaultWindowWidth = 200;
    private const int defaultWindowHeight = 150;

    private static GameObject prefab;

    [MenuItem("Tools/Replace With Prefab")]
    static void CreateReplaceWithPrefab()
    {
        var window = EditorWindow.GetWindow(typeof(ReplaceWithPrefab));
        window.titleContent.text = "Palette Mesh Variant Editor";
        window.minSize = new Vector2(defaultWindowWidth, defaultWindowHeight);
    }

    private void OnGUI()
    {
        prefab = (GameObject) EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
        var selection = Selection.gameObjects;

        if (selection.Length > 0)
        {
            EditorGUILayout.LabelField(selection.Length+ " GameObject" + (selection.Length > 0 ? "s " : " ") + "selected.");
        }
        else
        {
            EditorGUILayout.HelpBox("No GameObjects selected.", MessageType.Info);
        }


        GUI.enabled = prefab && selection.Length > 0;
        if (GUILayout.Button("Replace"))
        {
            var assetType = PrefabUtility.GetPrefabAssetType(prefab);
            var isPrefab = assetType != PrefabAssetType.NotAPrefab;
            
            foreach (var selected in selection)
            {
                
                GameObject newObject;

                if (isPrefab)
                {
                    newObject = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
                }
                else
                {
                    newObject = Instantiate(prefab);
                    newObject.name = prefab.name;
                }

                if (newObject == null)
                {
                    Debug.LogError("Error instantiating prefab");
                    break;
                }

                Undo.RegisterCreatedObjectUndo(newObject, "Replace With Prefabs");
                newObject.transform.parent = selected.transform.parent;
                newObject.transform.localPosition = selected.transform.localPosition;
                newObject.transform.localRotation = selected.transform.localRotation;
                newObject.transform.localScale = selected.transform.localScale;
                newObject.transform.SetSiblingIndex(selected.transform.GetSiblingIndex());
                Undo.DestroyObjectImmediate(selected);
            }
            
            Close();
        }
        
        GUI.enabled = true;
    }
}
# endif