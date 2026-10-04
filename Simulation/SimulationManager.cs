using System;
using System.Collections.Generic;
using UnityEngine;
using EvoCreatures.NEAT;
using EvoCreatures.Creature;
using EvoCreatures.Environment;

namespace EvoCreatures.Simulation
{
    // Top-level wiring for the POC. Isolation strategy: every creature instance
    // is on the same Unity physics-layer as itself only (see note below) so
    // creatures never collide with each other while still colliding with
    // ground/food - avoids the complexity of multiple PhysicsScenes for now.
    // Swap to Physics.CreatePhysicsScene per-creature later if true isolation
    // (e.g. independent gravity/timestep) is ever needed; the plan (per
    // earlier discussion) is to relax this once locomotion is stable and let
    // creatures share one scene and learn collision avoidance.
    public class SimulationManager : MonoBehaviour
    {
        public CreatureTemplate Template;
        public FoodManager Food;
        public WaterVolume Water;
        public GameObject CreatureRootPrefab; // has CreatureBuilder + CreatureController attached
        public int PopulationSize = 20;
        public Vector2 SpawnAreaSize = new(30f, 30f);

        public float FastForwardTimeScale = 8f;
        [Min(0f)] public float FastForwardDurationRealtime = 30f;
        [Min(0.01f)] public float FastForwardRtNeatTickInterval = 5f;
        [Min(0.01f)] public float NormalRtNeatTickInterval = 30f;

        private RtNeatPopulation _population;
        private InnovationTracker _tracker;
        private GenomeFactory.NodeLayout _layout;
        private readonly List<CreatureController> _instances = new();
        private readonly List<CreatureBuilder> _builders = new();

        private float _simTime;
        private float _timeSinceLastTick;
        private float _fastForwardEndsAtRealtime;
        private bool _fastForwardActive;

        private float CurrentRtNeatTickInterval => _fastForwardActive
            ? FastForwardRtNeatTickInterval
            : NormalRtNeatTickInterval;

        private void Start()
        {
            // The inspector currently stores the FoodManager prefab asset here.
            // Instantiate it so FixedUpdate can populate the live simulation
            // and render the food items.
            if (Food != null && !Food.gameObject.scene.IsValid())
            {
                Food = Instantiate(Food);
                Food.name = "FoodManager_Runtime";
                Food.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            _fastForwardActive = FastForwardDurationRealtime > 0f && FastForwardTimeScale > 1f;
            Time.timeScale = _fastForwardActive ? FastForwardTimeScale : 1f;
            _fastForwardEndsAtRealtime = Time.unscaledTime + FastForwardDurationRealtime;

            var rng = new System.Random(12345);
            var (firstGenome, layout, tracker) = GenomeFactory.CreateInitial(Template, rng);
            _layout = layout;
            _tracker = tracker;
            _population = new RtNeatPopulation(_tracker, seed: 999);
            _population.OnGenomeReplaced += HandleGenomeReplaced;

            _population.AddInitial(firstGenome);
            for (int i = 1; i < PopulationSize; i++)
            {
                var (g, _, _) = GenomeFactory.CreateInitial(Template, rng);
                _population.AddInitial(g);
            }

            for (int i = 0; i < PopulationSize; i++)
                SpawnInstance(i, _population.Genomes[i]);
        }

        private void Update()
        {
            // Use unscaled time so this is 30 seconds on the player's clock,
            // rather than 30 simulated seconds (which would only be 3.75
            // seconds at an 8x time scale).
            if (_fastForwardActive && Time.unscaledTime >= _fastForwardEndsAtRealtime)
                EndFastForward();
        }

        private void EndFastForward()
        {
            _fastForwardActive = false;
            Time.timeScale = 1f;

            // Do not immediately do an expensive replacement based on the
            // previous fast-forward interval. Start the slower steady-state
            // evolution cadence fresh.
            _timeSinceLastTick = 0f;
            Debug.Log($"Evolution warm-up finished. Simulation restored to 1x; replacements now run every {NormalRtNeatTickInterval:0.##} simulation seconds.");
        }

        private void SpawnInstance(int index, Genome genome)
        {
            var pos = new Vector3(
                UnityEngine.Random.Range(-SpawnAreaSize.x, SpawnAreaSize.x),
                2f,
                UnityEngine.Random.Range(-SpawnAreaSize.y, SpawnAreaSize.y));

            var go = Instantiate(CreatureRootPrefab, pos, Quaternion.identity, transform);
            go.name = $"Creature_{index}";

            var builder = go.GetComponent<CreatureBuilder>();
            var controller = go.GetComponent<CreatureController>();
            var buoyancy = go.GetComponent<BuoyancyController>();
            builder.Template = Template;
            controller.Food = Food;
            controller.Water = Water;
            if (buoyancy != null) buoyancy.Water = Water;

            builder.Build(genome);

            // The torso and limbs are created by Build, so this must happen
            // afterwards. Applying it before Build left every generated body
            // part on the Default layer, allowing different creatures to
            // collide and force their articulated parts apart.
            // Requires Creature_0... layers to be pre-created; otherwise it
            // safely falls back to the default layer.
            int layer = LayerMask.NameToLayer($"Creature_{index}");
            if (layer != -1) SetLayerRecursive(go, layer);

            controller.Initialize(genome, _layout, builder);

            while (_instances.Count <= index) { _instances.Add(null); _builders.Add(null); }
            _instances[index] = controller;
            _builders[index] = builder;
        }

        private void HandleGenomeReplaced(int slotIndex, Genome newGenome)
        {
            var builder = _builders[slotIndex];
            var controller = _instances[slotIndex];

            builder.Build(newGenome);

            // Replacement creates fresh child objects, so give those objects
            // the same isolation layer as their CreatureRoot as well.
            SetLayerRecursive(builder.gameObject, builder.gameObject.layer);
            controller.Initialize(newGenome, _layout, builder);
        }

        private void FixedUpdate()
        {
            _simTime += Time.fixedDeltaTime;
            _timeSinceLastTick += Time.fixedDeltaTime;

            // Feed accumulated fitness back into the genome each step so rtNEAT
            // sees up-to-date scores when it picks the next worst-performer.
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] == null) continue;
                _instances[i].GetGenome().Fitness = _instances[i].AccumulatedFitness;
            }

            if (_timeSinceLastTick >= CurrentRtNeatTickInterval)
            {
                _timeSinceLastTick = 0f;
                _population.Tick(_simTime);
            }
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
