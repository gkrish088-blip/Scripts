using UnityEngine;
using EvoCreatures.Creature;

namespace EvoCreatures.Environment
{
    // Applies buoyancy + drag to every ArticulationBody part of a creature
    // (torso + active limbs) based on how submerged each one is. This is the
    // component that actually makes deep water behaviorally different from
    // land - without it, water is just a texture the food happens to spawn in.
    [RequireComponent(typeof(CreatureBuilder))]
    public class BuoyancyController : MonoBehaviour
    {
        public WaterVolume Water;

        public float PartRadius = 0.3f; // rough half-thickness of a limb/torso part
        public float BuoyancyCoefficient = 1.15f; // >1 so torso floats rather than hovering neutrally
        public float LinearDragCoefficient = 2.5f;
        public float AngularDragCoefficient = 0.5f;

        private CreatureBuilder _builder;

        private void Awake() => _builder = GetComponent<CreatureBuilder>();

        private void FixedUpdate()
        {
            if (Water == null || _builder == null || _builder.Torso == null) return;

            ApplyToBody(_builder.Torso);
            foreach (var joint in _builder.ActiveJoints)
                ApplyToBody(joint);
        }

        private void ApplyToBody(ArticulationBody body)
        {
            Vector3 pos = body.worldCenterOfMass;
            if (!Water.ContainsXZ(pos)) return;

            float submersion = Water.GetSubmersionFraction(pos.y, PartRadius);
            if (submersion <= 0f) return;

            float gravityMag = Mathf.Abs(Physics.gravity.y);
            Vector3 buoyantForce = Vector3.up * (BuoyancyCoefficient * submersion * body.mass * gravityMag);
            body.AddForce(buoyantForce, ForceMode.Force);

            // Opposes velocity, scaled by submersion - this is what turns
            // "flail limbs for free movement" into "push against resistance
            // to actually get anywhere," which is the whole point.
            Vector3 dragForce = -body.linearVelocity * (LinearDragCoefficient * submersion);
            body.AddForce(dragForce, ForceMode.Force);

            Vector3 dragTorque = -body.angularVelocity * (AngularDragCoefficient * submersion);
            body.AddTorque(dragTorque, ForceMode.Force);
        }
    }
}
