using System;
using System.Collections.Generic;
using EvoCreatures.NEAT;

namespace EvoCreatures.Creature
{
    // Produces genomes whose node-id layout matches CreatureTemplate exactly,
    // so CreatureBuilder/CreatureController can always find "sensor node for
    // slot 3" etc regardless of which genome instance they're reading.
    public static class GenomeFactory
    {
        public class NodeLayout
        {
            public List<int> CoreSensorIds = new();
            public List<int> BiasId_AsList = new(); // single bias node, kept as list for convenience
            public List<List<List<int>>> SegmentSensorIds = new();   // [slot][segment][sensorIndex]
            public List<List<List<int>>> SegmentEffectorIds = new(); // [slot][segment][effectorIndex]
        }

        public static (Genome genome, NodeLayout layout, InnovationTracker tracker) CreateInitial(
            CreatureTemplate template, Random rng, int seed = 0)
        {
            var genome = new Genome();
            var layout = new NodeLayout();
            int nextId = 0;

            var bias = new NodeGene(nextId++, NodeType.Bias);
            genome.Nodes.Add(bias);
            layout.BiasId_AsList.Add(bias.Id);

            for (int i = 0; i < template.CoreSensorCount; i++)
            {
                var n = new NodeGene(nextId++, NodeType.Input);
                genome.Nodes.Add(n);
                layout.CoreSensorIds.Add(n.Id);
            }

            for (int slot = 0; slot < template.SlotCount; slot++)
            {
                var slotSensorIds = new List<List<int>>();
                var slotEffectorIds = new List<List<int>>();
                for (int segment = 0; segment < template.SegmentsPerLimb; segment++)
                {
                    var sensorIds = new List<int>();
                    for (int s = 0; s < template.SensorsPerSegment; s++)
                    {
                        var n = new NodeGene(nextId++, NodeType.Input, owningSlot: slot);
                        genome.Nodes.Add(n);
                        sensorIds.Add(n.Id);
                    }
                    slotSensorIds.Add(sensorIds);

                    var effectorIds = new List<int>();
                    for (int e = 0; e < template.EffectorsPerSegment; e++)
                    {
                        var n = new NodeGene(nextId++, NodeType.Output, owningSlot: slot);
                        genome.Nodes.Add(n);
                        effectorIds.Add(n.Id);
                    }
                    slotEffectorIds.Add(effectorIds);
                }
                layout.SegmentSensorIds.Add(slotSensorIds);
                layout.SegmentEffectorIds.Add(slotEffectorIds);
            }

            var tracker = new InnovationTracker(startingNodeId: nextId);

            // Minimal starting topology: connect every input/bias directly to
            // every effector output, small random weights. rtNEAT will grow
            // hidden structure from here via MutateAddNode/MutateAddConnection.
            var inputIds = new List<int>(layout.BiasId_AsList);
            inputIds.AddRange(layout.CoreSensorIds);
            foreach (var slot in layout.SegmentSensorIds)
                foreach (var segment in slot) inputIds.AddRange(segment);

            var outputIds = new List<int>();
            foreach (var slot in layout.SegmentEffectorIds)
                foreach (var segment in slot) outputIds.AddRange(segment);

            foreach (var inId in inputIds)
            {
                foreach (var outId in outputIds)
                {
                    int innov = tracker.GetOrCreateConnectionInnovation(inId, outId);
                    float weight = (float)(rng.NextDouble() * 0.4 - 0.2);
                    genome.Connections.Add(new ConnectionGene(innov, inId, outId, weight));
                }
            }

            for (int slot = 0; slot < template.SlotCount; slot++)
            {
                // Start with a random subset of slots active so the initial
                // population isn't uniform - gives selection something to work with immediately.
                bool active = rng.NextDouble() < 0.5;
                genome.LimbSlots.Add(new LimbSlotGene(slot, active, segmentCount: template.SegmentsPerLimb));
            }

            return (genome, layout, tracker);
        }
    }
}
