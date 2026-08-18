using System.Collections.Generic;
using UnityEngine;

namespace EvoCreatures.Environment
{
    public class FoodManager : MonoBehaviour
    {
        public float WaterLevelY = 0f;
        public Vector2 LandXRange = new(-20f, 20f);
        public Vector2 LandZRange = new(-20f, -5f);   // land region
        public Vector2 WaterXRange = new(-20f, 20f);
        public Vector2 WaterZRange = new(5f, 20f);    // water region

        public int MaxFoodItems = 60;
        public float ConsumeRadius = 1.2f;
        public float FitnessPerFood = 1f;

        // Shift schedule: at Time=0, all food spawns on land. At Time=ShiftDuration
        // and beyond, all food spawns in water. Linear interpolation between.
        public float ShiftDuration = 300f; // seconds, tune for POC run length

        private readonly List<Vector3> _food = new();
        private float _simTime;

        public float CurrentWaterFraction => Mathf.Clamp01(_simTime / ShiftDuration);

        private void FixedUpdate()
        {
            _simTime += Time.fixedDeltaTime;

            while (_food.Count < MaxFoodItems)
                _food.Add(SpawnPoint());
        }

        private Vector3 SpawnPoint()
        {
            bool inWater = Random.value < CurrentWaterFraction;
            var xr = inWater ? WaterXRange : LandXRange;
            var zr = inWater ? WaterZRange : LandZRange;
            float y = inWater ? WaterLevelY - 0.2f : WaterLevelY + 0.2f;
            return new Vector3(Random.Range(xr.x, xr.y), y, Random.Range(zr.x, zr.y));
        }

        public Vector3 GetNearestFoodDirection(Vector3 fromPosition)
        {
            if (_food.Count == 0) return Vector3.zero;

            Vector3 nearest = _food[0];
            float bestDist = Vector3.Distance(fromPosition, nearest);
            foreach (var f in _food)
            {
                float d = Vector3.Distance(fromPosition, f);
                if (d < bestDist) { bestDist = d; nearest = f; }
            }

            return (nearest - fromPosition).normalized;
        }

        // Returns fitness delta for any food consumed near `position` this call.
        public float ConsumeNearbyFood(Vector3 position)
        {
            float gained = 0f;
            for (int i = _food.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(position, _food[i]) <= ConsumeRadius)
                {
                    _food.RemoveAt(i);
                    gained += FitnessPerFood;
                }
            }
            return gained;
        }
    }
}
