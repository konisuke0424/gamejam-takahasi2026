using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace KaiForest
{
    /// <summary>
    /// Mouse and touch as the game reads them. On a phone the browser may also turn a tap into mouse
    /// clicks, so while a finger is on the screen (and a moment after) the mouse is ignored: one tap
    /// never counts twice (e.g. start the game, then skip the diary page).
    /// </summary>
    public static class GameInput
    {
        /// <summary>Seconds after the last touch during which the mouse is still ignored.</summary>
        const float MouseQuietAfterTouch = 0.6f;
        static float lastTouch = -10f;
        static int refreshedFrame = -1;
        static bool touchUsed;

        // Domain reload is off in Play mode: the clock restarts but statics don't.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lastTouch = -10f;
            refreshedFrame = -1;
            touchUsed = false;
        }

        /// <summary>
        /// Played by touch: a phone, or the screen has been touched at all (an iPad's browser passes
        /// for a desktop one). Then the cursor is never captured and there is no Esc to point at.
        /// </summary>
        public static bool TouchUsed
        {
            get
            {
                Refresh();
                return touchUsed || Application.isMobilePlatform;
            }
        }

        /// <summary>A finger is on the screen, or just left it.</summary>
        public static bool TouchActive
        {
            get
            {
                Refresh();
                return Time.unscaledTime - lastTouch < MouseQuietAfterTouch;
            }
        }

        /// <summary>The mouse, unless AutoTest has it or the screen is being touched.</summary>
        public static Mouse Mouse => GameManager.IgnoreMouse || TouchActive ? null : Mouse.current;

        /// <summary>The touch points, or none (no touchscreen, or AutoTest has the input).</summary>
        public static ReadOnlyArray<TouchControl> Touches
        {
            get
            {
                var ts = Touchscreen.current;
                return ts == null || GameManager.IgnoreMouse ? default : ts.touches;
            }
        }

        /// <summary>A finger came down this frame; pos is where.</summary>
        public static bool TapStarted(out Vector2 pos)
        {
            foreach (var t in Touches)
                if (t.press.wasPressedThisFrame)
                {
                    pos = t.position.ReadValue();
                    return true;
                }
            pos = default;
            return false;
        }

        static void Refresh()
        {
            if (refreshedFrame == Time.frameCount) return;
            refreshedFrame = Time.frameCount;
            foreach (var t in Touches)
                if (t.press.isPressed || t.press.wasReleasedThisFrame)
                {
                    lastTouch = Time.unscaledTime;
                    touchUsed = true;
                    return;
                }
        }
    }
}
