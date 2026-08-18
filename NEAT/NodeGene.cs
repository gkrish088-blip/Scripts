using System;

namespace EvoCreatures.NEAT
{
    public enum NodeType { Bias, Input, Hidden, Output }

    // A single neuron in the genome. Nodes for limb sensors/effectors always
    // exist in the genome (fixed I/O) even if the physical limb is dormant -
    // this is what lets us avoid the "growing I/O" problem entirely.
    [Serializable]
    public class NodeGene
    {
        public int Id;
        public NodeType Type;

        // -1 = core body node (always active). >=0 = belongs to limb slot N
        // and is only physically meaningful once that slot is active.
        public int OwningSlot = -1;

        public NodeGene(int id, NodeType type, int owningSlot = -1)
        {
            Id = id;
            Type = type;
            OwningSlot = owningSlot;
        }

        public NodeGene Clone() => new NodeGene(Id, Type, OwningSlot);
    }
}
