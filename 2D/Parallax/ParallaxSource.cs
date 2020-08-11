using UnityEditor;
using UnityEngine;

/// <summary>
///     A parallax camera movement script, based and expanded on the great solution by David Dion-Paquet.
///     (https://www.gamasutra.com/blogs/DavidDionPaquet/20140601/218766/Creating_a_parallax_system_in_Unity3D_is_harder_than_it_seems.php)
/// </summary>
[ExecuteAlways]
public class ParallaxSource : MonoBehaviour
{
    // Parallax in edit mode
    [Tooltip("Enables parallax in edit mode, so that elements can be placed according to the camera's position.")]
    [SerializeField]
    private bool parallaxInEditMode = false;

    private Vector3? _storedPosition;

    public bool ParallaxInEditMode => parallaxInEditMode;

    // Editor scene view movement
    [Space(10)]
    [Tooltip("Enables movement in scene view (without selecting the object) using Keypad:0 + Arrow Keys.")]
    [SerializeField]
    private bool sceneMovement = true;

    [Min(0)] [Tooltip("Step size for movement in scene view")] [SerializeField]
    private float sceneMovementStepSize = 0.5f;

    private bool _movementKeyDown;
    private Vector2 _movementDirection;


    private void Awake()
    {
        RegisterMovementCallback();
        // If no position has been stored yet, use the current position
        if (_storedPosition == null)
        {
            _storedPosition = transform.position;
        }
    }

    private void OnDestroy()
    {
        UnregisterMovementCallback();
    }

    private void SavePosition()
    {
        _storedPosition = transform.position;
    }

    private void RestorePosition()
    {
        transform.position = _storedPosition ?? Vector3.zero;
    }

    private void RegisterMovementCallback()
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui += DuringSceneGuiCallback;
        _movementKeyDown = false;
        _movementDirection = Vector2.zero;
#endif
    }

    private void UnregisterMovementCallback()
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui -= DuringSceneGuiCallback;
        _movementKeyDown = false;
        _movementDirection = Vector2.zero;
#endif
    }

#if UNITY_EDITOR
    private void DuringSceneGuiCallback(SceneView sceneView)
    {
        var evt = Event.current;

        if (!evt.isKey || !sceneMovement)
        {
            return;
        }

        // Input system does not work outside game view, so we have to track keys over the course of multiple calls
        var anyRelevantKeyAction = false;
        var dir = evt.type == EventType.KeyDown ? 1 : 0;
        switch (evt.keyCode)
        {
            case KeyCode.Keypad0:
                _movementKeyDown = dir == 1;
                anyRelevantKeyAction = true;
                break;
            case KeyCode.LeftArrow:
                _movementDirection = new Vector2(-dir, _movementDirection.y);
                anyRelevantKeyAction = true;
                break;
            case KeyCode.RightArrow:
                _movementDirection = new Vector2(dir, _movementDirection.y);
                anyRelevantKeyAction = true;
                break;
            case KeyCode.UpArrow:
                _movementDirection = new Vector2(_movementDirection.x, dir);
                anyRelevantKeyAction = true;
                break;
            case KeyCode.DownArrow:
                _movementDirection = new Vector2(_movementDirection.x, -dir);
                anyRelevantKeyAction = true;
                break;
        }

        // If Keypad:0 or any arrow key was pressed and movement is enabled, move transform and consume the event
        if (anyRelevantKeyAction && _movementKeyDown)
        {
            // Time.deltaTime is not exact in this case, but it works well enough!
            transform.position += (Vector3) _movementDirection * sceneMovementStepSize;
            evt.Use();
        }
    }
#endif

#if UNITY_EDITOR
    /// <summary>
    ///     Custom Editor for the Parallax Source Script.
    ///     Adds a readonly field for the stored position and two buttons for saving and restoring the position.
    /// </summary>
    [CustomEditor(typeof(ParallaxSource))]
    public class ParallaxSourceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var script = target as ParallaxSource;

            if (script == null)
            {
                return;
            }

            GUILayout.Space(10);

            GUI.enabled = false;
            EditorGUILayout.Vector3Field("Stored Position", script._storedPosition ?? Vector3.zero);
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Position"))
            {
                // Enable undo/redo
                Undo.RecordObject(script, "Save Camera Position");

                script.SavePosition();

                // Set script dirty, so that saving is encouraged.
                EditorUtility.SetDirty(script);
            }

            if (GUILayout.Button("Restore Position"))
            {
                // Enable undo/redo
                Undo.RecordObject(script.transform, "Restore Camera Position");

                script.RestorePosition();

                // Set transfirm dirty, so that saving is encouraged.
                EditorUtility.SetDirty(script.transform);
            }

            GUILayout.EndHorizontal();
        }
    }
#endif
}