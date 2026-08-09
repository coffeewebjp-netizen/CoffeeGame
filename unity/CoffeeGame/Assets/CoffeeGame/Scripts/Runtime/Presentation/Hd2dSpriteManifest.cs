using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    /// <summary>
    /// JSON-backed description of an HD-2D character. The manifest deliberately
    /// contains resource paths rather than direct Unity asset references so that
    /// an art sheet can be replaced without rebuilding a prefab or Animator.
    /// </summary>
    [Serializable]
    public sealed class Hd2dSpriteManifest
    {
        public int version = 1;
        public string characterId;
        public bool directional = true;
        public string fallbackSpritePath;
        public float pixelsPerUnit = 256f;
        public float pivotX = 0.5f;
        public float pivotY = 0.05f;
        public float visualScale = 1f;
        public int sortingBase = 1000;
        public float depthSortingStepsPerUnit = 32f;
        public float shadowWidth = 0.72f;
        public float shadowDepth = 0.30f;
        public float shadowOpacity = 0.24f;
        public float shadowYOffset = -0.035f;
        public string[] requiredActions = Array.Empty<string>();
        public Hd2dSpriteClipDefinition[] clips = Array.Empty<Hd2dSpriteClipDefinition>();

        public void ApplySafeDefaults()
        {
            version = Mathf.Max(1, version);
            pixelsPerUnit = pixelsPerUnit > 1f ? pixelsPerUnit : 256f;
            pivotX = Mathf.Clamp01(pivotX);
            pivotY = Mathf.Clamp01(pivotY);
            visualScale = visualScale > 0.01f ? visualScale : 1f;
            depthSortingStepsPerUnit = depthSortingStepsPerUnit > 0f
                ? depthSortingStepsPerUnit
                : 32f;
            shadowWidth = Mathf.Max(0f, shadowWidth);
            shadowDepth = Mathf.Max(0f, shadowDepth);
            shadowOpacity = Mathf.Clamp01(shadowOpacity);
            clips ??= Array.Empty<Hd2dSpriteClipDefinition>();
            requiredActions ??= Array.Empty<string>();
        }

        /// <summary>
        /// Some Unity JsonUtility versions materialize an omitted nested class
        /// field as an empty object instead of null. Strip fields without either
        /// resource contract are therefore missing JSON placeholders, not
        /// authored strips, and must be normalized before validation/fallback.
        /// </summary>
        public void NormalizeJsonPlaceholders()
        {
            if (clips == null)
            {
                return;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i]?.NormalizeJsonPlaceholders();
            }
        }

        public bool IsUsable(out string reason)
        {
            if (version < 1)
            {
                reason = "version must be 1 or newer";
                return false;
            }

            if (clips == null || clips.Length == 0)
            {
                reason = "the clip list is empty";
                return false;
            }

            if (!IsFinite(pixelsPerUnit) || pixelsPerUnit <= 1f)
            {
                reason = "pixelsPerUnit must be greater than one";
                return false;
            }
            if (!IsFinite(pivotX) || !IsFinite(pivotY) ||
                pivotX < 0f || pivotX > 1f || pivotY < 0f || pivotY > 1f)
            {
                reason = "character pivot must be normalized between zero and one";
                return false;
            }
            if (!IsFinite(visualScale) || !IsFinite(depthSortingStepsPerUnit) ||
                visualScale <= 0.01f || depthSortingStepsPerUnit <= 0f)
            {
                reason = "visualScale and depthSortingStepsPerUnit must be positive";
                return false;
            }
            if (!IsFinite(shadowWidth) || !IsFinite(shadowDepth) ||
                !IsFinite(shadowOpacity) || !IsFinite(shadowYOffset) ||
                shadowWidth < 0f || shadowDepth < 0f || shadowOpacity < 0f || shadowOpacity > 1f)
            {
                reason = "shadow dimensions/opacity are outside their valid range";
                return false;
            }

            var actions = new HashSet<CharacterAction>();
            for (int i = 0; i < clips.Length; i++)
            {
                Hd2dSpriteClipDefinition clip = clips[i];
                if (clip == null)
                {
                    reason = $"clip {i} is null";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(clip.action) ||
                    !Enum.TryParse(clip.action, true, out CharacterAction action) ||
                    !Enum.IsDefined(typeof(CharacterAction), action))
                {
                    reason = $"clip {i} has an unknown action '{clip.action}'";
                    return false;
                }

                if (!actions.Add(action))
                {
                    reason = $"action '{action}' is declared more than once";
                    return false;
                }

                if (!IsFinite(clip.framesPerSecond) || clip.framesPerSecond <= 0f)
                {
                    reason = $"action '{action}' must have a positive framesPerSecond value";
                    return false;
                }

                bool hasStrip = false;
                if (!ValidateStrip(clip.all, action, "all", ref hasStrip, out reason) ||
                    !ValidateStrip(clip.down, action, "down", ref hasStrip, out reason) ||
                    !ValidateStrip(clip.side, action, "side", ref hasStrip, out reason) ||
                    !ValidateStrip(clip.up, action, "up", ref hasStrip, out reason))
                {
                    return false;
                }

                if (!hasStrip)
                {
                    reason = $"action '{action}' contains no sprite strip";
                    return false;
                }

                if (directional && clip.all == null &&
                    (clip.down == null || clip.side == null || clip.up == null))
                {
                    reason = $"directional action '{action}' must define all, or down/side/up strips";
                    return false;
                }
            }

            if (requiredActions != null)
            {
                var declaredRequirements = new HashSet<CharacterAction>();
                for (int i = 0; i < requiredActions.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(requiredActions[i]) ||
                        !Enum.TryParse(requiredActions[i], true, out CharacterAction required) ||
                        !Enum.IsDefined(typeof(CharacterAction), required))
                    {
                        reason = $"requiredActions contains unknown action '{requiredActions[i]}'";
                        return false;
                    }
                    if (!declaredRequirements.Add(required))
                    {
                        reason = $"required action '{required}' is declared more than once";
                        return false;
                    }
                    if (!actions.Contains(required))
                    {
                        reason = $"required action '{required}' has no clip";
                        return false;
                    }
                }
            }

            reason = string.Empty;
            return true;
        }

        private static bool ValidateStrip(
            Hd2dSpriteStripDefinition strip,
            CharacterAction action,
            string direction,
            ref bool hasStrip,
            out string reason)
        {
            reason = string.Empty;
            if (strip == null)
            {
                return true;
            }

            hasStrip = true;
            string label = $"action '{action}' {direction} strip";
            bool hasIndividualFrames = strip.resourcePaths != null && strip.resourcePaths.Length > 0;
            if (string.IsNullOrWhiteSpace(strip.resourcePath) && !hasIndividualFrames)
            {
                reason = $"{label} has neither resourcePath nor resourcePaths";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(strip.resourcePath) && hasIndividualFrames)
            {
                reason = $"{label} cannot define both resourcePath and resourcePaths";
                return false;
            }
            if (hasIndividualFrames)
            {
                for (int i = 0; i < strip.resourcePaths.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(strip.resourcePaths[i]))
                    {
                        reason = $"{label} resourcePaths contains an empty path at index {i}";
                        return false;
                    }
                }
            }
            if (!IsFinite(strip.pixelsPerUnit) ||
                strip.pixelsPerUnit < 0f ||
                (strip.pixelsPerUnit > 0f && strip.pixelsPerUnit <= 1f))
            {
                reason = $"{label} pixelsPerUnit must be zero or greater than one";
                return false;
            }
            if (!IsFinite(strip.pivotX) || !IsFinite(strip.pivotY) ||
                strip.pivotX > 1f || strip.pivotY > 1f)
            {
                reason = $"{label} pivot overrides must be negative or normalized between zero and one";
                return false;
            }

            if (hasIndividualFrames)
            {
                return true;
            }

            if (strip.columns <= 0 || strip.rows <= 0)
            {
                reason = $"{label} must have positive columns and rows";
                return false;
            }
            if (strip.rowFromTop < 0 || strip.rowFromTop >= strip.rows)
            {
                reason = $"{label} row {strip.rowFromTop} is outside its {strip.rows} rows";
                return false;
            }

            if (strip.frameColumns != null && strip.frameColumns.Length > 0)
            {
                for (int i = 0; i < strip.frameColumns.Length; i++)
                {
                    if (strip.frameColumns[i] < 0 || strip.frameColumns[i] >= strip.columns)
                    {
                        reason = $"{label} frame column {strip.frameColumns[i]} is outside its {strip.columns} columns";
                        return false;
                    }
                }
                return true;
            }

            if (strip.frameCount <= 0 || strip.firstColumn < 0 ||
                strip.firstColumn + strip.frameCount > strip.columns)
            {
                reason = $"{label} sequential frame range is outside its {strip.columns} columns";
                return false;
            }
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public sealed class Hd2dSpriteClipDefinition
    {
        public string action;
        public float framesPerSecond = 8f;
        public bool loop;
        public bool holdLastFrame = true;
        public Hd2dSpriteStripDefinition all;
        public Hd2dSpriteStripDefinition down;
        public Hd2dSpriteStripDefinition side;
        public Hd2dSpriteStripDefinition up;

        public void NormalizeJsonPlaceholders()
        {
            all = NormalizeStrip(all);
            down = NormalizeStrip(down);
            side = NormalizeStrip(side);
            up = NormalizeStrip(up);
        }

        private static Hd2dSpriteStripDefinition NormalizeStrip(
            Hd2dSpriteStripDefinition strip)
        {
            return strip != null && strip.HasResourceContract ? strip : null;
        }
    }

    [Serializable]
    public sealed class Hd2dSpriteStripDefinition
    {
        public string resourcePath;
        // Optional one-file-per-frame contract. When populated, each resource is
        // sliced as one full frame and the grid/range fields are ignored.
        public string[] resourcePaths;
        // Zero inherits the character-level value. This is useful when action,
        // locomotion and turnaround sheets were authored at different body scales.
        public float pixelsPerUnit;
        // Missing JsonUtility fields deserialize to zero on some Unity versions,
        // so usePivotOverride disambiguates an authored (0,0) from "inherit".
        // A negative component still inherits the character-level component.
        public bool usePivotOverride;
        public float pivotX = -1f;
        public float pivotY = -1f;
        // Set for an "all" strip that is still a horizontal side pose. A
        // direction-specific side strip is horizontal implicitly.
        public bool useHorizontalFacing;
        // The existing HD-2D convention is image-left. Individual strips can
        // override it when their source art is authored image-right.
        public bool authoredFacingRight;
        public int columns = 1;
        public int rows = 1;
        public int rowFromTop;
        public int firstColumn;
        public int frameCount = 1;
        public int[] frameColumns;

        public bool HasResourceContract =>
            !string.IsNullOrWhiteSpace(resourcePath) ||
            (resourcePaths != null && resourcePaths.Length > 0);

        public int ResolvedFrameCount => resourcePaths != null && resourcePaths.Length > 0
            ? resourcePaths.Length
            : frameColumns != null && frameColumns.Length > 0
                ? frameColumns.Length
                : Mathf.Max(1, frameCount);

        public int GetColumn(int frameIndex)
        {
            if (frameColumns != null && frameColumns.Length > 0)
            {
                return frameColumns[Mathf.Clamp(frameIndex, 0, frameColumns.Length - 1)];
            }

            return firstColumn + Mathf.Clamp(frameIndex, 0, Mathf.Max(1, frameCount) - 1);
        }
    }

    public static class Hd2dSpriteManifestLoader
    {
        public static bool TryLoad(string resourcePath, out Hd2dSpriteManifest manifest, out string error)
        {
            manifest = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                error = "manifest resource path is empty";
                return false;
            }

            TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath.Trim());
            if (jsonAsset == null)
            {
                error = $"Resources/{resourcePath}.json was not found";
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<Hd2dSpriteManifest>(jsonAsset.text);
            }
            catch (Exception exception)
            {
                error = $"manifest JSON could not be parsed: {exception.Message}";
                return false;
            }

            if (manifest == null)
            {
                error = "manifest JSON produced no data";
                return false;
            }

            manifest.NormalizeJsonPlaceholders();
            if (!manifest.IsUsable(out error))
            {
                manifest = null;
                return false;
            }

            // Validate authored/raw values before applying numeric defaults so
            // malformed manifests take the 3D fallback rather than being repaired.
            manifest.ApplySafeDefaults();

            return true;
        }
    }
}
