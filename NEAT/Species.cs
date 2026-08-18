using System.Collections.Generic;
using System.Linq;

namespace EvoCreatures.NEAT
{
    public class Species
    {
        public int Id;
        public Genome Representative;
        public List<Genome> Members = new();

        public float AverageFitness => Members.Count > 0 ? Members.Average(m => m.Fitness) : 0f;
    }
}
