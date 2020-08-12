using System.Linq;
using UnityEngine;

/// <summary>
///     A parallax layer script, based and expanded on the great solution by David Dion-Paquet.
///     (https://www.gamasutra.com/blogs/DavidDionPaquet/20140601/218766/Creating_a_parallax_system_in_Unity3D_is_harder_than_it_seems.php)
/// </summary>
[ExecuteAlways]
public class ParallaxLayer : MonoBehaviour
{
    // Inspector Properties
    [Tooltip("Camera, on whose movement the parallax movement is based. Defaults to Camera.main.")] [SerializeField]
    private Camera parallaxCamera;

    [Space(10)]
    [Tooltip(
        "Horizontal Speed. Higher values result in slower relative movement. 0 ... no parallax, 1 ... moves with camera.")]
    [SerializeField]
    private float speedX = 0;

    [Tooltip(
        "Vertical Speed. Higher values result in slower relative movement. 0 ... no parallax, 1 ... moves with camera.")]
    [SerializeField]
    private float speedY = 0;

    [Tooltip(
        "If enabled, the parallax movement is inverse to camera movement, resulting in faster relative movement. Typically used for foreground elements.")]
    [SerializeField]
    private bool moveInOppositeDirection = false;

    [Space(10)]
    [Tooltip(
        "Enables horizontal infinite scrolling. This script must be placed on parent object with >= 3 children, aligned next to each other.")]
    [SerializeField]
    private bool infiniteScroll = false;

    // Parallax Movement
    private ParallaxSource _parallaxSource;
    private Vector3 _previousSourcePosition;
    private bool _previousParallaxInEditMode;

    private bool ParallaxInEditMode => _parallaxSource != null && _parallaxSource.ParallaxInEditMode;

    // Infinite Scrolling
    private Transform[] _scrollingElements;
    private int _leftIndex;
    private int _rightIndex;
    private float _scrollSize;

    // Functions
    private void OnEnable()
    {
        Setup();
    }

    private void OnValidate()
    {
        Setup();
    }

    private void LateUpdate()
    {
        if (!parallaxCamera)
        {
            return;
        }

        // Set previous position if movement in edit mode has just been activated on the camera
        var parallaxInEditMode = ParallaxInEditMode;
        if (!_previousParallaxInEditMode && parallaxInEditMode)
        {
            _previousSourcePosition = parallaxCamera.transform.position;
        }

        _previousParallaxInEditMode = parallaxInEditMode;

        // If we are in edit mode and movement in edit mode is disabled, return
        if (!Application.isPlaying && !parallaxInEditMode)
        {
            return;
        }

        // Movement: Get delta position, scale it with direction and speed, apply
        var position = parallaxCamera.transform.position;
        var distance = position - _previousSourcePosition;
        _previousSourcePosition = position;

        // Perform both scrolling types
        PerformParallaxScroll(distance);
        PerformInfiniteScroll(position, distance);
    }

    private void PerformParallaxScroll(Vector3 distance)
    {
        var direction = moveInOppositeDirection ? -1f : 1f;
        var movement = Vector3.Scale(distance, new Vector3(speedX, speedY)) * direction;
        transform.position += movement;
    }

    private void PerformInfiniteScroll(Vector3 position, Vector3 distance)
    {
        // Infinite Scrolling.
        if (!infiniteScroll)
        {
            return;
        }

        // If child count changed, update (onValidate is not triggered if a child is added)
        // This check is only relevant when being in edit mode, so performance is not an issue.
        if (_scrollingElements.Length != transform.childCount)
        {
            SetupScrolling();
        }

        // Skip if scrollSize could not be determined
        if (_scrollSize < 0.05f)
        {
            return;
        }

        // Horizontal camera size
        var camXBounds = parallaxCamera.orthographicSize * parallaxCamera.aspect;

        // If left camera bounds are below left element, move rightmost element to the left
        if (distance.x < 0 && position.x - camXBounds < _scrollingElements[_leftIndex].transform.position.x)
        {
            ScrollLeft();
        }
        // and vice versa
        else if (distance.x > 0 && position.x + camXBounds > _scrollingElements[_rightIndex].transform.position.x)
        {
            ScrollRight();
        }
    }

    private void ScrollLeft()
    {
        // Right element is moved to the leftmost position
        _scrollingElements[_rightIndex].position += Vector3.left * (_scrollingElements.Length * _scrollSize);

        // Indices are updated (array positions are not changed!)
        _leftIndex = _rightIndex;
        _rightIndex--;

        if (_rightIndex < 0)
            _rightIndex = _scrollingElements.Length - 1;
    }

    private void ScrollRight()
    {
        _scrollingElements[_leftIndex].position += Vector3.right * (_scrollingElements.Length * _scrollSize);

        _rightIndex = _leftIndex;
        _leftIndex++;

        if (_leftIndex == _scrollingElements.Length)
            _leftIndex = 0;
    }


    private void Setup()
    {
        // Set camera as source if not set
        if (parallaxCamera == null && Camera.main != null)
        {
            parallaxCamera = Camera.main;
        }

        if (parallaxCamera == null)
        {
            return;
        }

        // Set up initial values
        _parallaxSource = parallaxCamera.gameObject.GetComponent<ParallaxSource>();
        _previousSourcePosition = parallaxCamera.transform.position;
        _previousParallaxInEditMode = ParallaxInEditMode;

        // Setup or cleanup scrolling
        if (infiniteScroll)
        {
            SetupScrolling();
        }
        else
        {
            CleanupScrolling();
        }
    }

    private void SetupScrolling()
    {
        // Get children and order them by position.x
        _scrollingElements = new Transform[transform.childCount];

        for (var i = 0; i < _scrollingElements.Length; i++)
        {
            _scrollingElements[i] = transform.GetChild(i);
        }

        _scrollingElements = _scrollingElements.OrderBy(e => e.transform.position.x).ToArray();

        if (_scrollingElements.Length < 3)
        {
            Debug.LogWarning(
                $"Incorrect number of child transforms for scrolling. Expected >= 3, got {_scrollingElements.Length}.");
        }

        // Init indices
        _leftIndex = 0;
        _rightIndex = _scrollingElements.Length - 1;

        // Scroll unit size is set to the distance between two elements
        if (_scrollingElements.Length > 1)
        {
            _scrollSize = Mathf.Abs(_scrollingElements[1].position.x - _scrollingElements[0].position.x);
        }
        else
        {
            _scrollSize = 0;
        }
    }

    private void CleanupScrolling()
    {
        _scrollingElements = null;
        _leftIndex = 0;
        _rightIndex = 0;
        _scrollSize = 0;
    }
}