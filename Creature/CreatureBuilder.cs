using System.Collections.Generic;
using UnityEngine;
using EvoCreatures.NEAT;

namespace EvoCreatures.Creature
{
    // Builds/rebuilds the physical body for a genome. Only ACTIVE limb slots
    // get physical limbs - dormant slots exist in the genome's node list but
    // have no ArticulationBody, so they simply contribute no torque/no
    // physical presence until MutateLimbSlots flips them on.
    public class CreatureBuilder : MonoBehaviour
    {
        public CreatureTemplate Template;
        public GameObject TorsoPrefab;
        public GameObject LimbSegmentPrefab; // simple capsule w/ ArticulationBody, reused per limb

        // [slot][segment]. Empty lists represent dormant slots. Keeping this
        // hierarchy lets controllers address shoulder and elbow independently.
        private readonly List<List<ArticulationBody>> _slotSegmentJoints = new();

        public ArticulationBody Torso { get; private set; }
        public ArticulationBody GetSegmentJoint(int slotIndex, int segmentIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotSegmentJoints.Count) return null;
            var segments = _slotSegmentJoints[slotIndex];
            return segmentIndex >= 0 && segmentIndex < segments.Count ? segments[segmentIndex] : null;
        }

        public IEnumerable<ArticulationBody> ActiveJoints
        {
            get
            {
                foreach (var segments in _slotSegmentJoints)
                    foreach (var joint in segments)
                        if (joint != null) yield return joint;
            }
        }

        public void Build(Genome genome)
        {
            Clear();

            var torsoGO = Instantiate(TorsoPrefab, transform);
            // Prefabs retain the transform they had when they were authored.  A
            // creature body must instead be relative to its CreatureRoot,
            // otherwise every new torso starts at the prefab's old scene
            // position.
            torsoGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Torso = torsoGO.GetComponent<ArticulationBody>();
            if (Torso == null) Torso = torsoGO.AddComponent<ArticulationBody>();
            Torso.immovable = false;

            for (int i = 0; i < Template.SlotCount; i++)
                _slotSegmentJoints.Add(new List<ArticulationBody>());

            for (int i = 0; i < genome.LimbSlots.Count; i++)
            {
                var slotGene = genome.LimbSlots[i];
                if (!slotGene.Active) continue;
                int slotIndex = slotGene.SlotIndex;
                if (slotIndex < 0 || slotIndex >= Template.Slots.Count) continue;

                var def = Template.Slots[slotIndex];
                int segmentCount = Mathf.Clamp(slotGene.SegmentCount, 1, Template.SegmentsPerLimb);
                Transform parent = torsoGO.transform;

                for (int segment = 0; segment < segmentCount; segment++)
                {
                    var limbGO = Instantiate(LimbSegmentPrefab, parent);
                    if (segment == 0)
                    {
                        limbGO.transform.localPosition = def.AnchorLocalPosition;
                        limbGO.transform.localRotation = Quaternion.Euler(def.AnchorLocalEuler);
                    }
                    else
                    {
                        // Compensate for the upper segment's scale so every
                        // piece keeps the prefab's intended world dimensions.
                        limbGO.transform.localPosition = Template.ChildSegmentLocalOffset;
                        limbGO.transform.localRotation = Quaternion.identity;
                        limbGO.transform.localScale = Vector3.one;
                    }

                    var joint = limbGO.GetComponent<ArticulationBody>();
                    if (joint == null) joint = limbGO.AddComponent<ArticulationBody>();

                    Vector3 parentAnchor = segment == 0
                        ? def.AnchorLocalPosition
                        : Template.ChildSegmentLocalOffset;
                    Quaternion parentRotation = segment == 0
                        ? Quaternion.Euler(def.AnchorLocalEuler)
                        : Quaternion.identity;
                    ConfigureJoint(joint, parentAnchor, parentRotation);

                    _slotSegmentJoints[slotIndex].Add(joint);
                    parent = limbGO.transform;
                }
            }
        }

        private static void ConfigureJoint(ArticulationBody joint, Vector3 parentAnchor, Quaternion parentRotation)
        {
            joint.matchAnchors = false;
            joint.parentAnchorPosition = parentAnchor;
            joint.parentAnchorRotation = parentRotation;
            joint.anchorPosition = Vector3.zero;
            joint.anchorRotation = Quaternion.identity;
            joint.jointType = ArticulationJointType.RevoluteJoint;
            var drive = joint.xDrive;
            drive.stiffness = 2000f;
            drive.damping = 100f;
            drive.forceLimit = 100f;
            joint.xDrive = drive;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _slotSegmentJoints.Clear();
            Torso = null;
        }
    }
}
