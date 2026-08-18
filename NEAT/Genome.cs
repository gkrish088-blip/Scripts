using System;
using System.Collections.Generic;
using System.Linq;

namespace EvoCreatures.NEAT
{
    [Serializable]
    public class Genome
    {
        public int Id;
        public List<NodeGene> Nodes = new();
        public List<ConnectionGene> Connections = new();
        public List<LimbSlotGene> LimbSlots = new();

        public float Fitness;
        public int SpeciesId = -1;
        public float BirthTime; // sim time this genome entered the population (rtNEAT eligibility)

        public Genome Clone()
        {
            var g = new Genome
            {
                Id = Id,
                Fitness = Fitness,
                SpeciesId = SpeciesId,
                BirthTime = BirthTime
            };
            g.Nodes = Nodes.Select(n => n.Clone()).ToList();
            g.Connections = Connections.Select(c => c.Clone()).ToList();
            g.LimbSlots = LimbSlots.Select(s => s.Clone()).ToList();
            return g;
        }

        public NodeGene GetNode(int id) => Nodes.First(n => n.Id == id);

        // ---------------- Mutation operators ----------------

        public void MutateWeights(Random rng, float perturbChance = 0.9f, float perturbStdDev = 0.5f)
        {
            foreach (var c in Connections)
            {
                if (rng.NextDouble() < perturbChance)
                    c.Weight += (float)(NextGaussian(rng) * perturbStdDev);
                else
                    c.Weight = (float)(rng.NextDouble() * 4.0 - 2.0); // full reassign
            }
        }

        public void MutateAddConnection(Random rng, InnovationTracker tracker, int maxAttempts = 20)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                var a = Nodes[rng.Next(Nodes.Count)];
                var b = Nodes[rng.Next(Nodes.Count)];

                if (a.Id == b.Id) continue;
                if (b.Type == NodeType.Input || b.Type == NodeType.Bias) continue; // can't feed into input/bias
                if (Connections.Any(c => c.InNodeId == a.Id && c.OutNodeId == b.Id)) continue;

                int innov = tracker.GetOrCreateConnectionInnovation(a.Id, b.Id);
                float weight = (float)(rng.NextDouble() * 4.0 - 2.0);
                Connections.Add(new ConnectionGene(innov, a.Id, b.Id, weight));
                return;
            }
        }

        public void MutateAddNode(Random rng, InnovationTracker tracker)
        {
            var enabled = Connections.Where(c => c.Enabled).ToList();
            if (enabled.Count == 0) return;

            var toSplit = enabled[rng.Next(enabled.Count)];
            toSplit.Enabled = false;

            int newNodeId = tracker.GetOrCreateNodeForSplit(toSplit.InnovationId);
            // Inherit slot ownership from whichever endpoint is slot-owned, so a
            // split inside a limb's subnetwork stays attributed to that limb.
            int owningSlot = GetNode(toSplit.InNodeId).OwningSlot != -1
                ? GetNode(toSplit.InNodeId).OwningSlot
                : GetNode(toSplit.OutNodeId).OwningSlot;

            Nodes.Add(new NodeGene(newNodeId, NodeType.Hidden, owningSlot));

            int innovIn = tracker.GetOrCreateConnectionInnovation(toSplit.InNodeId, newNodeId);
            int innovOut = tracker.GetOrCreateConnectionInnovation(newNodeId, toSplit.OutNodeId);

            Connections.Add(new ConnectionGene(innovIn, toSplit.InNodeId, newNodeId, 1.0f));
            Connections.Add(new ConnectionGene(innovOut, newNodeId, toSplit.OutNodeId, toSplit.Weight));
        }

        // The "morphological mutation" - flips whether a limb slot is physically
        // grown. Deliberately kept separate from the NN mutation operators above:
        // this never touches Nodes/Connections, so it can't desync innovation
        // tracking. CreatureBuilder just needs to be told to rebuild afterward.
        public void MutateLimbSlots(Random rng, float toggleChance = 0.05f)
        {
            foreach (var slot in LimbSlots)
            {
                if (rng.NextDouble() < toggleChance)
                    slot.Active = !slot.Active;
            }
        }

        // ---------------- Crossover ----------------

        // Standard NEAT crossover: matching genes inherited randomly from either
        // parent, disjoint/excess genes inherited from the fitter parent.
        public static Genome Crossover(Genome fitter, Genome other, Random rng)
        {
            var child = new Genome();
            child.Nodes = fitter.Nodes.Select(n => n.Clone()).ToList();

            var otherByInnov = other.Connections.ToDictionary(c => c.InnovationId);

            foreach (var geneA in fitter.Connections)
            {
                ConnectionGene chosen;
                if (otherByInnov.TryGetValue(geneA.InnovationId, out var geneB))
                    chosen = rng.NextDouble() < 0.5 ? geneA.Clone() : geneB.Clone();
                else
                    chosen = geneA.Clone(); // disjoint/excess from fitter parent

                child.Connections.Add(chosen);
            }

            // Limb slots: inherit from fitter parent, independently coin-flip
            // per slot with the other parent (slots aren't part of NEAT
            // innovation tracking, so no disjoint/excess logic needed here).
            child.LimbSlots = fitter.LimbSlots.Select(s => s.Clone()).ToList();
            for (int i = 0; i < child.LimbSlots.Count && i < other.LimbSlots.Count; i++)
            {
                if (rng.NextDouble() < 0.5)
                {
                    child.LimbSlots[i].Active = other.LimbSlots[i].Active;
                    child.LimbSlots[i].SegmentCount = other.LimbSlots[i].SegmentCount;
                }
            }

            return child;
        }

        // Rough topological distance for speciation - counts disjoint/excess
        // connection genes plus average weight difference on matching genes.
        public static float Distance(Genome a, Genome b, float c1 = 1.0f, float c2 = 1.0f, float c3 = 0.4f)
        {
            var aByInnov = a.Connections.ToDictionary(c => c.InnovationId);
            var bByInnov = b.Connections.ToDictionary(c => c.InnovationId);

            int matching = 0, disjointOrExcess = 0;
            float weightDiffSum = 0;

            foreach (var innov in aByInnov.Keys.Union(bByInnov.Keys))
            {
                bool inA = aByInnov.ContainsKey(innov);
                bool inB = bByInnov.ContainsKey(innov);
                if (inA && inB)
                {
                    matching++;
                    weightDiffSum += Math.Abs(aByInnov[innov].Weight - bByInnov[innov].Weight);
                }
                else
                {
                    disjointOrExcess++;
                }
            }

            int n = Math.Max(a.Connections.Count, b.Connections.Count);
            n = n < 20 ? 1 : n; // normalization only matters for larger genomes

            float avgWeightDiff = matching > 0 ? weightDiffSum / matching : 0;
            return (c1 * disjointOrExcess) / n + c3 * avgWeightDiff;
        }

        private static double NextGaussian(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}
