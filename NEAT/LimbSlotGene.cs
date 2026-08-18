using System;

namespace EvoCreatures.NEAT
{
    // Purely a morphology flag. The sensor/effector NodeGenes for this slot
    // always exist in the genome (see NodeGene.OwningSlot) - this gene only
    // controls whether CreatureBuilder physically grows the limb. Toggling
    // this does NOT require touching the NEAT node/connection representation
    // at all, which is the whole point of pre-allocating slots.
    [Serializable]
    public class LimbSlotGene
    {
        public int SlotIndex;
        public bool Active;

        // Which physical limb variant to build if active (e.g. 0 = leg, 1 = fin).
        // Kept generic so CreatureTemplate can interpret it however it wants.
        public int LimbVariant;

        // Number of articulated pieces in this limb chain. A value of two is
        // an upper/lower limb (upper arm + forearm, or thigh + shin).
        public int SegmentCount;

        public LimbSlotGene(int slotIndex, bool active, int limbVariant = 0, int segmentCount = 2)
        {
            SlotIndex = slotIndex;
            Active = active;
            LimbVariant = limbVariant;
            SegmentCount = Math.Max(1, segmentCount);
        }

        public LimbSlotGene Clone() => new LimbSlotGene(SlotIndex, Active, LimbVariant, SegmentCount);
    }
}
