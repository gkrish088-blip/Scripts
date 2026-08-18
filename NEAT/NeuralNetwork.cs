using System;
using System.Collections.Generic;
using System.Linq;

namespace EvoCreatures.NEAT
{
    // Built once from a Genome when a creature is instantiated. Because the
    // topology can contain cycles (recurrent connections are allowed - NEAT
    // doesn't forbid them), we evaluate by relaxing the whole graph a fixed
    // number of iterations per control step rather than a single topological
    // pass. This is the same practical shortcut rtNEAT-style continuous
    // controllers use: the network is a dynamical system running alongside
    // physics, not a one-shot function.
    public class NeuralNetwork
    {
        private readonly Dictionary<int, float> _values = new();
        private readonly List<(int inId, int outId, float weight)> _edges = new();
        private readonly HashSet<int> _inputIds = new();
        private readonly int _relaxIterations;

        public NeuralNetwork(Genome genome, int relaxIterations = 3)
        {
            _relaxIterations = relaxIterations;
            foreach (var n in genome.Nodes)
            {
                _values[n.Id] = n.Type == NodeType.Bias ? 1f : 0f;
                if (n.Type == NodeType.Input || n.Type == NodeType.Bias)
                    _inputIds.Add(n.Id);
            }
            foreach (var c in genome.Connections.Where(c => c.Enabled))
                _edges.Add((c.InNodeId, c.OutNodeId, c.Weight));
        }

        public void SetInput(int nodeId, float value) => _values[nodeId] = value;

        public float GetOutput(int nodeId) => _values.TryGetValue(nodeId, out var v) ? v : 0f;

        public void Step()
        {
            for (int iter = 0; iter < _relaxIterations; iter++)
            {
                var accum = new Dictionary<int, float>();
                foreach (var (inId, outId, weight) in _edges)
                {
                    if (!accum.ContainsKey(outId)) accum[outId] = 0f;
                    accum[outId] += _values[inId] * weight;
                }

                foreach (var kv in accum)
                {
                    if (_inputIds.Contains(kv.Key)) continue; // never overwrite inputs/bias
                    _values[kv.Key] = (float)Math.Tanh(kv.Value);
                }
            }
        }
    }
}
