using UnityEngine;
using EvoCreatures.NEAT;
using EvoCreatures.Environment;

namespace EvoCreatures.Creature
{
    [RequireComponent(typeof(CreatureBuilder))]
    public class CreatureController : MonoBehaviour
    {
        public FoodManager Food;
        public WaterVolume Water; // real submersion, driven by the same body BuoyancyController uses

        private Genome _genome;
        private GenomeFactory.NodeLayout _layout;
        private NeuralNetwork _brain;
        private CreatureBuilder _builder;

        public float AccumulatedFitness { get; private set; }

        public void Initialize(Genome genome, GenomeFactory.NodeLayout layout, CreatureBuilder builder)
        {
            _genome = genome;
            _layout = layout;
            _builder = builder;
            _brain = new NeuralNetwork(genome);
            AccumulatedFitness = 0f;
        }

        private void FixedUpdate()
        {
            if (_brain == null || _builder.Torso == null) return;

            GatherSensors();
            _brain.Step();
            ApplyEffectors();
            AccumulateFitness();
        }

        private void GatherSensors()
        {
            var torsoTf = _builder.Torso.transform;

            // Core sensors: torso pitch, torso roll, dir-to-food (x,z), submersion depth.
            var euler = torsoTf.rotation.eulerAngles;
            float pitch = Mathf.DeltaAngle(0, euler.x) / 180f;
            float roll = Mathf.DeltaAngle(0, euler.z) / 180f;

            Vector3 toFood = Food != null ? Food.GetNearestFoodDirection(torsoTf.position) : Vector3.zero;
            float submersion = Water != null ? Water.GetSubmersionFraction(torsoTf.position.y, 0.3f) : 0f;

            var core = _layout.CoreSensorIds;
            if (core.Count > 0) _brain.SetInput(core[0], pitch);
            if (core.Count > 1) _brain.SetInput(core[1], roll);
            if (core.Count > 2) _brain.SetInput(core[2], toFood.x);
            if (core.Count > 3) _brain.SetInput(core[3], toFood.z);
            if (core.Count > 4) _brain.SetInput(core[4], submersion);

            // Per-segment sensors: each upper/lower joint is independently
            // observable. A dormant limb (or absent segment) reports zero.
            for (int slot = 0; slot < _layout.SegmentSensorIds.Count; slot++)
            {
                for (int segment = 0; segment < _layout.SegmentSensorIds[slot].Count; segment++)
                {
                    var joint = _builder.GetSegmentJoint(slot, segment);
                    var sensorIds = _layout.SegmentSensorIds[slot][segment];
                    float angle = 0f, angularVel = 0f;
                    if (joint != null)
                    {
                        angle = joint.jointPosition.dofCount > 0 ? joint.jointPosition[0] : 0f;
                        angularVel = joint.jointVelocity.dofCount > 0 ? joint.jointVelocity[0] : 0f;
                    }

                    if (sensorIds.Count > 0) _brain.SetInput(sensorIds[0], angle);
                    if (sensorIds.Count > 1) _brain.SetInput(sensorIds[1], angularVel);
                }
            }
        }

        private void ApplyEffectors()
        {
            for (int slot = 0; slot < _layout.SegmentEffectorIds.Count; slot++)
            {
                for (int segment = 0; segment < _layout.SegmentEffectorIds[slot].Count; segment++)
                {
                    var joint = _builder.GetSegmentJoint(slot, segment);
                    if (joint == null) continue;

                    var effectorIds = _layout.SegmentEffectorIds[slot][segment];
                    if (effectorIds.Count == 0) continue;

                    float output = _brain.GetOutput(effectorIds[0]);
                    var drive = joint.xDrive;
                    drive.target = output * 60f;
                    joint.xDrive = drive;
                }
            }
        }

        private void AccumulateFitness()
        {
            if (Food == null) return;
            AccumulatedFitness += Food.ConsumeNearbyFood(_builder.Torso.transform.position);
        }

        public Genome GetGenome() => _genome;
    }
}
