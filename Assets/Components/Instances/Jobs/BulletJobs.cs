using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Components.Instances.Jobs
{
    public struct Bullet
    {
        public float damage;
        public float speed;
        public int penetrations;
    
        public float2 position;
        public float2 dir;

        public Bullet(float damage, float speed, int penetrations, float2 position, float2 dir)
        {
            this.damage = damage;
            this.speed = speed;
            this.penetrations = penetrations;
            this.position = position;
            this.dir = dir;
        }
    }

    [BurstCompile]
    struct UpdateBullets : IJob
    {
        private NativeArray<int> amountBullets;
        private float deltaTime;
        private NativeQuadTree quadtree;
        private NativeArray<Bullet> bulletsData;
        private NativeList<int> toBeRemovedBullets;
        private NativeList<int> toBeRemoved;
        private NativeList<int> quadtreeResults;
        private NativeArray<Enemy> enemyData;

        public UpdateBullets(NativeArray<int> amountBullets, NativeArray<Bullet> bulletsData, NativeArray<Enemy> enemyData, NativeList<int> toBeRemovedBullets, NativeList<int> toBeRemovedEnemy, NativeQuadTree quadtree, NativeList<int> quadtreeResults, float deltaTime)
        {
            this.amountBullets = amountBullets;
            this.deltaTime = deltaTime;
            this.bulletsData = bulletsData;
            this.toBeRemovedBullets = toBeRemovedBullets;
            this.toBeRemoved = toBeRemovedEnemy;
            this.quadtree = quadtree;
            this.enemyData = enemyData;
            this.quadtreeResults = quadtreeResults;
        }

        public void Execute()
        {
            for (int i = 0; i < amountBullets[1]; i++)
            {
                Bullet bulletData = bulletsData[i];
                if (!IsPointInsideBounds(new float4(0, 0, 11, 19), bulletData.position))
                {
                    toBeRemovedBullets.Add(i);
                    continue;
                }
                bulletData = Move(bulletData);
                bulletData = CheckCollision(bulletData, i);
                bulletsData[i] = bulletData;
            }
        }

        private Bullet Move(Bullet bulletData)
        {
            float2 dir = bulletData.dir * deltaTime * bulletData.speed;
            bulletData.position.x += dir.x;
            bulletData.position.y += dir.y;

            return bulletData;
        }

        private Bullet CheckCollision(Bullet bulletData, int index)
        {
            quadtreeResults.Clear();
            quadtree.Query(quadtreeResults, new float4(bulletData.position, new float2(0.1f, 0.1f)));
            for (int i = 0; i < quadtreeResults.Length; i++)
            {
                Enemy enemy = enemyData[quadtreeResults[i]];
                if (math.distance(enemy.position, bulletData.position) < 0.1f)
                {
                    if (bulletData.penetrations-- <= 0)
                    {
                        toBeRemovedBullets.Add(index);
                    }
                    
                    enemy.health -= bulletData.damage;
                    if (enemy.health <= 0)
                    {
                        toBeRemoved.Add(quadtreeResults[i]);
                    }
                    
                    bulletData.damage -= 0.1f;
                    enemyData[quadtreeResults[i]] = enemy;
                }
            }
            return bulletData;
        }
        
        private bool IsPointInsideBounds(float4 bounds, float2 point)
        {
            float2 halfSize = new float2(bounds.z * 0.5f, bounds.w * 0.5f);
            return math.all(point >= bounds.xy - halfSize & point <= bounds.xy + halfSize);
        }
    }

    [BurstCompile]
    struct RemoveBullets : IJob
    {
        private NativeArray<int> currentBulletIndex;
        private NativeArray<Bullet> bulletData;
        private NativeList<int> toBeRemoved;
        private int maxAmountBullets;

        public RemoveBullets(NativeArray<int> currentBulletIndex, NativeArray<Bullet> bulletData, NativeList<int> toBeRemoved, int maxAmountBullets)
        {
            this.currentBulletIndex = currentBulletIndex;
            this.bulletData = bulletData;
            this.toBeRemoved = toBeRemoved;
            this.maxAmountBullets = maxAmountBullets;
        }

        public void Execute()
        {
            for (int i = 0; i < toBeRemoved.Length; i++)
            {
                int index = toBeRemoved[i];
                // Bullet bullet = bulletData[index];
                // bullet.position = new float2(float.MaxValue, float.MaxValue);
                bulletData[index] = bulletData[currentBulletIndex[0]];
                bulletData[currentBulletIndex[0]] = new Bullet();
                currentBulletIndex[0]--;
                currentBulletIndex[0] += math.min(currentBulletIndex[0], 0) * (-maxAmountBullets + 1);
                currentBulletIndex[1]--;
            }
            toBeRemoved.Clear();
        }
    }

    //Could be parrallelized
    [BurstCompile]
    struct AddBullets : IJob
    {
        private NativeArray<int> currentEnemyIndex;
        private float2 direction;
        private NativeArray<Bullet> bulletsData;
        private float speed;
        private float damage;
        private int penetrations;
        private int maxAmountBullets;
        private float2 spawnPos;

        public AddBullets(NativeArray<int> currentEnemyIndex, float2 direction, float2 spawnPos, float speed, float damage, int penetrations, NativeArray<Bullet> bulletsData, int maxAmountBullets)
        {
            this.currentEnemyIndex = currentEnemyIndex;
            this.direction = direction;
            this.spawnPos = spawnPos;
            this.speed = speed;
            this.damage = damage;
            this.penetrations = penetrations;
            this.bulletsData = bulletsData;
            this.maxAmountBullets = maxAmountBullets;
        }

        public void Execute()
        {
            Random rng = new Random((uint)currentEnemyIndex[0] + 10);
            for (int i = 0; i < 5; i++)
            {
                float angle = GetAngleBetweenVectors(direction, new float2(0, 1));
                float cross = direction.x * 1.0f - direction.y * 0.0f; // Cross product with (0, 1)
                if (cross < 0) angle = -angle;
                angle += rng.NextFloat(-10, 10);
                float angleInRadians = math.radians(angle);
                float2 newDirection = new float2(math.sin(angleInRadians), math.cos(angleInRadians));
                
                bulletsData[currentEnemyIndex[0]] = new Bullet(damage, speed + rng.NextFloat(-speed * 0.3f, speed * 0.3f), penetrations, spawnPos, newDirection);

                currentEnemyIndex[0]++;
                currentEnemyIndex[0] %= maxAmountBullets;
                currentEnemyIndex[1]++;
                currentEnemyIndex[1] = math.min(currentEnemyIndex[1], maxAmountBullets);
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