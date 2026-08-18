using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvoCreatures.Creature
{
    [Serializable]
    public struct SlotDefinition
    {
        public Vector3 AnchorLocalPosition; // where on the torso this limb attaches
        public Vector3 AnchorLocalEuler;
    }

    // The fixed body plan every genome in the population shares. This is what
    // makes the "pre-allocated slots" approach work: GenomeFactory and
    // CreatureBuilder both read this same template, so node ids / slot
    // indices always line up regardless of which limbs are active.
    [CreateAssetMenu(menuName = "EvoCreatures/CreatureTemplate")]
    public class CreatureTemplate : ScriptableObject
    {
        public int SlotCount = 6;
        public List<SlotDefinition> Slots = new();

        [Min(1)] public int SegmentsPerLimb = 2;

        // Offset of a child segment's centre in its parent's local space. The
        // default matches the current scaled capsule prefab: a capsule is 0.2
        // units tall in world space, so -2 local Y places the next capsule
        // directly below it through the parent's 0.1 Y scale.
        public Vector3 ChildSegmentLocalOffset = new(0f, -2f, 0f);

        // Core (always-on) sensors, independent of limbs:
        // e.g. torso pitch, torso roll, distance-to-food, direction-to-food (x,z), submersion depth
        public int CoreSensorCount = 5;

        // Per-segment sensors (e.g. joint angle, joint angular velocity) and
        // effectors (e.g. target joint torque/velocity).
        public int SensorsPerSegment = 2;
        public int EffectorsPerSegment = 1;

        private void OnValidate()
        {
            while (Slots.Count < SlotCount) Slots.Add(new SlotDefinition());
            while (Slots.Count > SlotCount) Slots.RemoveAt(Slots.Count - 1);
        }
    }
}
