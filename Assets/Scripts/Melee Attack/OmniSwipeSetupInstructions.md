# Omnidirectional Swipe Setup

The code expects the melee weapon to use a **single** swipe animation that can be rotated to any direction.  Follow these steps in Unity to replace the old directional swipe animations.

1. **Create the animation**
   - Make a new animation clip called `OmniMeleeSwipe` based on the old forward swipe.
   - Ensure the animation faces **right** at 0° rotation and includes the capsule collider enabling/disabling and the white flash/hit effects.
   - If pogoing is required, add the existing `PerformPogo` animation event at the correct frame.

2. **Animator controller**
   - On the melee weapon's animator, remove the old directional swipe states or leave them unused.
   - Add a trigger parameter named `OmniMeleeSwipe`.
   - Create a state that plays the new `OmniMeleeSwipe` clip and transition to it from *Any State* when the trigger fires.  The state should automatically exit back to idle when the clip finishes.

3. **Pivot / rotation**
   - The melee weapon GameObject that holds the animator will be rotated by script.  Place the pivot so that rotating around Z points the swipe in the desired direction.
   - The swipe should scale/position correctly when the object rotates (no directional offsets).

4. **Testing**
   - Verify that swinging with no directional input aims forward based on the player's facing.
   - Moving the stick in any direction (except down while grounded) should rotate the swipe to match the stick direction.
   - Downward attacks while in the air still trigger pogo and other effects.

With the above steps complete the new omnidirectional system will function with the updated scripts.
