using System;

namespace EvoCreatures.NEAT
{
    [Serializable]
    public class ConnectionGene
    {
        public int InnovationId;
        public int InNodeId;
        public int OutNodeId;
        public float Weight;
        public bool Enabled = true;

        public ConnectionGene(int innovationId, int inNodeId, int outNodeId, float weight, bool enabled = true)
        {
            InnovationId = innovationId;
            InNodeId = inNodeId;
            OutNodeId = outNodeId;
            Weight = weight;
            Enabled = enabled;
        }

        public ConnectionGene Clone() => new ConnectionGene(InnovationId, InNodeId, OutNodeId, Weight, Enabled);
    }
}
