using System.Collections.Generic;

namespace EvoCreatures.NEAT
{
    // One instance shared by the whole population for the whole run.
    // Ensures that "the same" structural mutation (same in/out node pair,
    // or same node split) gets the same innovation number wherever it
    // occurs, which is what makes crossover between differently-structured
    // genomes meaningful instead of noise.
    public class InnovationTracker
    {
        private int _nextNodeId;
        private int _nextInnovationId;

        // key: (inNode, outNode) -> innovation id, for this generation/run
        private readonly Dictionary<(int, int), int> _connectionInnovations = new();

        // key: innovation id of the connection that got split -> new node id
        private readonly Dictionary<int, int> _nodeSplitInnovations = new();

        public InnovationTracker(int startingNodeId)
        {
            _nextNodeId = startingNodeId;
            _nextInnovationId = 0;
        }

        public int GetOrCreateConnectionInnovation(int inNode, int outNode)
        {
            var key = (inNode, outNode);
            if (_connectionInnovations.TryGetValue(key, out var id))
                return id;

            id = _nextInnovationId++;
            _connectionInnovations[key] = id;
            return id;
        }

        // Returns the node id created when splitting connection `splitInnovationId`.
        // Reuses the same new node id if this exact connection has already been
        // split elsewhere in the population this run (standard NEAT behavior).
        public int GetOrCreateNodeForSplit(int splitInnovationId)
        {
            if (_nodeSplitInnovations.TryGetValue(splitInnovationId, out var nodeId))
                return nodeId;

            nodeId = _nextNodeId++;
            _nodeSplitInnovations[splitInnovationId] = nodeId;
            return nodeId;
        }

        public int PeekNextNodeId => _nextNodeId;
    }
}
