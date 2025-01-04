using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Components.Instances.Jobs
{
    public struct Enemy
    {
        public float health;
        public float speed;
    
        public float2 position;
        public float angle;
        public Enemy(float health, float speed, float2 position, float angle)
        {
            this.health = health;
            this.speed = speed;
            this.position = position;
            this.angle = angle;
        }
    }

    [BurstCompile]
    struct InsertPointsJob : IJob
    {
        private NativeQuadTree quadtree;
        [NativeDisableParallelForRestriction]
        private readonly NativeArray<Enemy> enemyTransforms;
        private int until;
    
        public InsertPointsJob(NativeQuadTree quadtree, NativeArray<Enemy> enemyTransforms, int until)
        {
            this.quadtree = quadtree;
            this.enemyTransforms = enemyTransforms;
            this.until = until;
        }

        public void Execute()
        {
            for (int i = 0; i < until; i++)
            {
                quadtree.Insert(i, enemyTransforms[i].position);
            }
        }
    }

    [BurstCompile]
    struct UpdateEnemies : IJob
    {
        private int amountEnemies;
        private float deltaTime;
        private NativeArray<Enemy> enemiesData;
        private NativeList<int> toBeRemoved;

        public UpdateEnemies(NativeArray<Enemy> enemiesData, NativeList<int> toBeRemoved, int amountEnemies, float deltaTime)
        {
            this.enemiesData = enemiesData;
            this.toBeRemoved = toBeRemoved;
            this.amountEnemies = amountEnemies;
            this.deltaTime = deltaTime;
        }

        public void Execute()
        {
            for (int i = 0; i < amountEnemies; i++)
            {
                Move(i);
            }
        }

        private void Move(int index)
        {
            Enemy enemyTransform = enemiesData[index];
            float2 dirToCenter = -math.normalize(enemyTransform.position) * (enemiesData[index].speed * deltaTime);
            enemyTransform.position.x += dirToCenter.x;
            enemyTransform.position.y += dirToCenter.y;

            if (IsPointInsideBounds(new float4(0, 0, 1, 1), enemyTransform.position))
            {
                toBeRemoved.Add(index);
            }
            enemiesData[index] = enemyTransform;
        }
    
        private bool IsPointInsideBounds(float4 bounds, float2 point)
        {
            float2 halfSize = new float2(bounds.z * 0.5f, bounds.w * 0.5f);
            return math.all(point >= bounds.xy - halfSize & point <= bounds.xy + halfSize);
        }
    }

    [BurstCompile]
    struct RemoveEnemies : IJob
    {
        private NativeArray<int> currentEnemyIndex;
        private NativeArray<Enemy> enemiesData;
        private NativeList<int> toBeRemoved;
        private int maxAmountEnemies;

        public RemoveEnemies(NativeArray<int> currentEnemyIndex, NativeArray<Enemy> enemiesData, NativeList<int> toBeRemoved, int maxAmountEnemies)
        {
            this.currentEnemyIndex = currentEnemyIndex;
            this.enemiesData = enemiesData;
            this.toBeRemoved = toBeRemoved;
            this.maxAmountEnemies = maxAmountEnemies;
        }

        public void Execute()
        {
            for (int i = 0; i < toBeRemoved.Length; i++)
            {
                int index = toBeRemoved[i];
                enemiesData[index] = enemiesData[currentEnemyIndex[0]];
                enemiesData[currentEnemyIndex[0]] = new Enemy();
                currentEnemyIndex[0]--;
                currentEnemyIndex[0] += math.min(currentEnemyIndex[0], 0) * (maxAmountEnemies - 1) * -1;
                currentEnemyIndex[1]--;
            }
            toBeRemoved.Clear();
        }
    }

    //Could be parrallelized
    [BurstCompile]
    struct AddEnemies : IJob
    {
        private NativeArray<int> currentEnemyIndex;

        private int amountEnemiesToAdd;
        private int maxAmountEnemies;
        private float speed;
        private float speedVariance;
        private NativeArray<Enemy> enemiesData;
        private uint randomSeed;

        public AddEnemies(NativeArray<int> currentEnemyIndex, int amountEnemiesToAdd, int maxAmountEnemies, float speed, float speedVariance, NativeArray<Enemy> enemiesData, uint randomSeed)
        {
            this.currentEnemyIndex = currentEnemyIndex;
            this.amountEnemiesToAdd = amountEnemiesToAdd;
            this.maxAmountEnemies = maxAmountEnemies;
            this.speed = speed;
            this.speedVariance = speedVariance;
            this.enemiesData = enemiesData;
            this.randomSeed = randomSeed;
        }

        public void Execute()
        {
            Random rng = new Random(randomSeed);
            for (int i = 0; i < amountEnemiesToAdd; i++)
            {
                float angle = rng.NextFloat(0, math.PI * 2);
                float2 position = new float2(math.cos(angle), math.sin(angle)) * 4.9f;
                angle = GetAngleBetweenVectors(-math.normalize(position), new float2(0, 1));
                angle = math.degrees(angle);
                enemiesData[currentEnemyIndex[0]] = new Enemy(1, rng.NextFloat(speed - speed * speedVariance, speed + speed * speedVariance), position, angle);

                currentEnemyIndex[0]++;
                currentEnemyIndex[0] %= maxAmountEnemies;
                currentEnemyIndex[1]++;
                currentEnemyIndex[1] = math.min(maxAmountEnemies, maxAmountEnemies);
            }
        }
    
        private float GetAngleBetweenVectors(float2 vectorA, float2 vectorB)
        {
            // Calculate dot product and lengths
            float dotProduct = math.dot(vectorA, vectorB);
            float magnitudeA = math.length(vectorA);
            float magnitudeB = math.length(vectorB);

            // Avoid division by zero
            if (magnitudeA == 0f || magnitudeB == 0f)
                return 0f;

            // Calculate the angle in radians
            float angleRadians = math.acos(math.clamp(dotProduct / (magnitudeA * magnitudeB), -1f, 1f));

            // Convert to degrees if needed
            return math.degrees(angleRadians);
        }
    }
}