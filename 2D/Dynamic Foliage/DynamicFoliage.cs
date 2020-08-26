using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     This script for dynamic foliage sets the StartTime and Direction properties on a material based on any
///     DynamicFoliage shader (lit or unlit) when anything enters the trigger and thus triggering a wiggle "animation".
///
///     The material is reset to it's original variant after the wiggle finished so that it can be batched again, which
///     is not possible wiggle, as the StartTime property will be unique.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DynamicFoliage : MonoBehaviour
{
    [Tooltip(
        "Setting to Multiple allows to affect many (child) renderers using a single parent script + collider. Increases performance if a single entity consists of multiple sprites.")]
    [SerializeField]
    private DynamicFoliageMode mode = DynamicFoliageMode.Single;

    [Tooltip("References to (child) renderers. Must only be filled if Mode = Multiple.")] [SerializeField]
    private Renderer[] targets;

    // Stores original materials of renderers
    private Dictionary<int, Material> _originalMaterials;
    private float _triggerOutsideTargetTime;
    private bool _triggeredOutside = true;

    private static readonly int StartTimeId = Shader.PropertyToID("Vector1_46A70D4A");
    private static readonly int WiggleDurationId = Shader.PropertyToID("Vector1_3D5AF48A");
    private static readonly int WiggleDirectionId = Shader.PropertyToID("Vector1_CAE6B7FE");

    private void Start()
    {
        // Init dictionary
        _originalMaterials = new Dictionary<int, Material>();

        // If mode = single, set targets to only include own renderer.
        if (mode != DynamicFoliageMode.Single)
            return;

        var rdr = GetComponent<Renderer>();
        targets = rdr ? new[] {rdr} : new Renderer[0];
    }

    private void Update()
    {
        // After the wiggle animation has finished, reset the materials
        if (!_triggeredOutside && Time.time > _triggerOutsideTargetTime)
        {
            _triggeredOutside = true;

            foreach (var target in targets)
            {
                if (!target || !_originalMaterials.TryGetValue(target.GetInstanceID(), out var originalMat) ||
                    !originalMat)
                    continue;

                // Variant 1: Reset Material
                target.material = originalMat;

                // Variant 2: Material Block (does not work with SRP)
                //SetMaterialValues(target, DefaultStartTime, DefaultWiggleDirection);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_triggeredOutside)
            return;

        // Place TRIGGER CONDITIONS here

        _triggeredOutside = false;

        // Set Direction and StartTime properties on all target renderers
        var maxWiggleDuration = 0f;
        var direction = Math.Sign(transform.position.x - other.transform.position.x);

        foreach (var target in targets)
        {
            if (!target)
                continue;

            if (!_originalMaterials.ContainsKey(target.GetInstanceID()))
            {
                _originalMaterials[target.GetInstanceID()] = target.sharedMaterial;
            }

            SetMaterialValues(target, Time.time, direction);

            maxWiggleDuration = Math.Max(maxWiggleDuration, target.material.GetFloat(WiggleDurationId));
        }

        // Materials will be reset after the last wiggle animation has finished.
        _triggerOutsideTargetTime = Time.time + maxWiggleDuration;
    }

    private void SetMaterialValues(Renderer target, float startTime, float direction)
    {
        // Variant 2: Property Blocks (does not work with SRP)
        // var block = new MaterialPropertyBlock();
        // target.GetPropertyBlock(block);
        // block.SetFloat(StartTimeId, startTime);
        // block.SetFloat(WiggleDirectionId, direction);
        // target.SetPropertyBlock(block);

        var mat = target.material;
        mat.SetFloat(StartTimeId, startTime);
        mat.SetFloat(WiggleDirectionId, direction);
    }

    private enum DynamicFoliageMode
    {
        Single,
        Multiple
    }
}