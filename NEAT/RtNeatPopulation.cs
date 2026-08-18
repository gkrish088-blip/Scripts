using System;
using System.Collections.Generic;
using System.Linq;

namespace EvoCreatures.NEAT
{
    // Core rtNEAT loop: instead of generational batches, periodically pick the
    // worst eligible genome and replace it with a child of fitter parents.
    // "Eligible" = has lived at least MinAgeBeforeReplacement sim-seconds,
    // which is the innovation-protection mechanism - a genome whose limb slot
    // just activated gets time to adapt before it can be judged/culled.
    public class RtNeatPopulation
    {
        public List<Genome> Genomes = new();
        public List<Species> AllSpecies = new();

        public float SpeciesDistanceThreshold = 3.0f;
        public float MinAgeBeforeReplacement = 15f; // seconds
        public float WeightMutationChance = 0.8f;
        public float AddConnectionChance = 0.08f;
        public float AddNodeChance = 0.03f;
        public float LimbSlotToggleChance = 0.05f;

        private readonly InnovationTracker _tracker;
        private readonly Random _rng;
        private int _nextGenomeId;

        // (slotIndex, newGenome) - slotIndex is stable so the caller knows exactly
        // which physical creature instance to rebuild, without searching.
        public event Action<int, Genome> OnGenomeReplaced;

        public RtNeatPopulation(InnovationTracker tracker, int seed = 0)
        {
            _tracker = tracker;
            _rng = new Random(seed);
        }

        public void AddInitial(Genome g)
        {
            g.Id = _nextGenomeId++;
            Genomes.Add(g);
        }

        public void Speciate()
        {
            foreach (var s in AllSpecies) s.Members.Clear();

            foreach (var genome in Genomes)
            {
                Species match = null;
                foreach (var s in AllSpecies)
                {
                    if (Genome.Distance(genome, s.Representative) < SpeciesDistanceThreshold)
                    {
                        match = s;
                        break;
                    }
                }

                if (match == null)
                {
                    match = new Species { Id = AllSpecies.Count, Representative = genome };
                    AllSpecies.Add(match);
                }

                genome.SpeciesId = match.Id;
                match.Members.Add(genome);
            }

            AllSpecies.RemoveAll(s => s.Members.Count == 0);
        }

        // Call this on a fixed interval (e.g. every few seconds of sim time),
        // not every physics frame - rtNEAT replaces one individual at a time.
        public void Tick(float currentSimTime)
        {
            if (Genomes.Count < 4) return; // need enough individuals for meaningful selection

            Speciate();

            int worstIndex = -1;
            float worstFitness = float.MaxValue;
            for (int i = 0; i < Genomes.Count; i++)
            {
                if (currentSimTime - Genomes[i].BirthTime < MinAgeBeforeReplacement) continue;
                if (Genomes[i].Fitness < worstFitness)
                {
                    worstFitness = Genomes[i].Fitness;
                    worstIndex = i;
                }
            }
            if (worstIndex == -1) return;

            var parentA = TournamentSelect();
            var parentB = TournamentSelect();
            var (fitter, other) = parentA.Fitness >= parentB.Fitness ? (parentA, parentB) : (parentB, parentA);

            var child = Genome.Crossover(fitter, other, _rng);
            child.Id = _nextGenomeId++;
            child.BirthTime = currentSimTime;
            child.Fitness = 0f;

            if (_rng.NextDouble() < WeightMutationChance) child.MutateWeights(_rng);
            if (_rng.NextDouble() < AddConnectionChance) child.MutateAddConnection(_rng, _tracker);
            if (_rng.NextDouble() < AddNodeChance) child.MutateAddNode(_rng, _tracker);
            child.MutateLimbSlots(_rng, LimbSlotToggleChance);

            // Replace in place (not remove+add) so slot index stays stable for the caller.
            Genomes[worstIndex] = child;

            OnGenomeReplaced?.Invoke(worstIndex, child);
        }

        private Genome TournamentSelect(int tournamentSize = 3)
        {
            Genome best = null;
            for (int i = 0; i < tournamentSize; i++)
            {
                var candidate = Genomes[_rng.Next(Genomes.Count)];
                if (best == null || candidate.Fitness > best.Fitness)
                    best = candidate;
            }
            return best;
        }
    }
}
