using Components.Instances.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public struct Boid
{
    public float2 position;
    public float2 velocity;
    public int amountNeighbours;
    public Boid(float2 position, float2 velocity)
    {
        amountNeighbours = 0;
        this.position = position;
        this.velocity = velocity;
    }
}

[BurstCompile]
struct AddBoids : IJob
{
    private int amountBoidsToAdd;
    private NativeArray<Boid> boidsData;
    private uint randomSeed;

    public AddBoids(int amountBoidsToAdd, NativeArray<Boid> boidsData, uint randomSeed)
    {
        this.amountBoidsToAdd = amountBoidsToAdd;
        this.boidsData = boidsData;
        this.randomSeed = randomSeed;
    }

    public void Execute()
    {
        Random rng = new Random(randomSeed);
        for (int i = 0; i < amountBoidsToAdd; i++)
        {
            float2 randomPos = new float2(rng.NextFloat(-3.0f, 3.0f), rng.NextFloat(-3.0f, 3.0f));
            float2 randomVel = math.normalize(new float2(rng.NextFloat(-1.0f, 1.0f), rng.NextFloat(-1.0f, 1.0f)));
            boidsData[i] = new Boid(randomPos, randomVel);
        }
    }
}

[BurstCompile]
struct UpdateBoids : IJobFor
{
    [ReadOnly]
    private readonly NativeQuadTree quadtree;
    [NativeDisableParallelForRestriction, ReadOnly]
    private NativeArray<Boid> boidsDataRead;
    [NativeDisableParallelForRestriction, WriteOnly]
    private NativeArray<Boid> boidsDataWrite;

    public float alignmentRadiusSq;
    public float cohesionRadiusSq;
    public float separationRadiusfSq;

    public float alignmentWeight;
    public float cohesionWeight;
    public float separationWeight;

    public float maxSpeed;
    public float maxForce;
    private float deltaTime;

    private float maxDistance;

    public UpdateBoids(NativeQuadTree quadtree, NativeArray<Boid> boidsDataRead, NativeArray<Boid> boidsDataWrite, float alignmentRadiusSq, float cohesionRadiusSq, float separationRadiusfSq, float alignmentWeight, float cohesionWeight, float separationWeight, float maxSpeed, float maxForce, float deltaTime)
    {
        this.quadtree = quadtree;
        this.boidsDataRead = boidsDataRead;
        this.boidsDataWrite = boidsDataWrite;
        this.alignmentRadiusSq = alignmentRadiusSq;
        this.cohesionRadiusSq = cohesionRadiusSq;
        this.separationRadiusfSq = separationRadiusfSq;
        this.alignmentWeight = alignmentWeight;
        this.cohesionWeight = cohesionWeight;
        this.separationWeight = separationWeight;
        this.maxSpeed = maxSpeed;
        this.maxForce = maxForce;
        this.deltaTime = deltaTime;
        
        maxDistance = math.max(math.max(math.sqrt(alignmentRadiusSq), math.sqrt(cohesionRadiusSq)), math.sqrt(separationRadiusfSq));
    }

    public void Execute(int index)
    {
        NativeList<int> quadtreeResults = new NativeList<int>(Allocator.Temp);
        Boid boid = boidsDataRead[index];
        
        if (!IsPointInsideBounds(new float4(0, 0, 16, 9), boid.position))
        {
            boid.velocity = (float2.zero - boid.position);
        }
        
        quadtree.Query(quadtreeResults, new float4(boid.position, maxDistance * 1.1f, maxDistance * 1.1f));

        boid.amountNeighbours = 0;
        if (quadtreeResults.Length > 0)
        {
            float2 alignment = float2.zero;
            int alignmentCount = 0;
            float2 cohesion = float2.zero;
            int cohesionCount = 0;
            float2 seperation = float2.zero;
            int seperationCount = 0;
        
            for (int i = 0; i < quadtreeResults.Length; i++)
            {
                Boid otherBoid = boidsDataRead[quadtreeResults[i]];
                float distanceSq = math.distancesq(otherBoid.position, boid.position);
            
                if(distanceSq == 0)
                    continue;
            
                if (distanceSq < alignmentRadiusSq)
                {
                    alignment += otherBoid.velocity;
                    alignmentCount++;
                }
                if (distanceSq < cohesionRadiusSq)
                {
                    cohesion += otherBoid.position;
                    cohesionCount++;
                }
                if (distanceSq < separationRadiusfSq)
                {
                    float2 away = math.normalize(boid.position - otherBoid.position) / math.sqrt(distanceSq);
                    seperation += away;
                    seperationCount++;
                }
            }
        
            if(alignmentCount > 0)
                alignment /= alignmentCount;
            if(cohesionCount > 0)
                cohesion /= cohesionCount;
            if(seperationCount > 0)
                seperation /= seperationCount;
            
            alignment *= alignmentWeight;
            cohesion = (cohesion - boid.position) * cohesionWeight;
            seperation *= separationWeight;
        
            float2 newVel = alignment + cohesion + seperation;
            newVel = ClampMagnitude(newVel, maxForce);
            
            boid.velocity += newVel;
            boid.velocity = ClampMagnitude(boid.velocity, maxSpeed);
            boid.amountNeighbours = seperationCount;
        }
        
        boid.position += boid.velocity * deltaTime;
        
        boidsDataWrite[index] = boid;
    }
    
    private float2 ClampMagnitude(float2 vector, float maxMagnitude)
    {
        float magnitudeSq = math.lengthsq(vector);
        if (magnitudeSq > maxMagnitude * maxMagnitude)
        {
            return math.normalize(vector) * maxMagnitude;
        }
        return vector;
    }
    
    private bool IsPointInsideBounds(float4 bounds, float2 point)
    {
        float2 halfSize = new float2(bounds.z * 0.5f, bounds.w * 0.5f);
        return math.all(point >= bounds.xy - halfSize & point <= bounds.xy + halfSize);
    }
}

[BurstCompile]
struct InsertBoidsQuadtree : IJob
{
    private NativeQuadTree quadtree;
    private readonly NativeArray<Boid> boids;
    
    public InsertBoidsQuadtree(NativeQuadTree quadtree, NativeArray<Boid> boids)
    {
        this.quadtree = quadtree;
        this.boids = boids;
    }

    public void Execute()
    {
        for (int i = 0; i < boids.Length; i++)
        {
            quadtree.Insert(i, boids[i].position);
        }
    }
}

[BurstCompile]
struct UpdateBoidsSequential : IJob
{
    [ReadOnly]
    private readonly NativeQuadTree quadtree;
    [NativeDisableParallelForRestriction, ReadOnly]
    private NativeArray<Boid> boidsDataRead;
    [NativeDisableParallelForRestriction, WriteOnly]
    private NativeArray<Boid> boidsDataWrite;

    public float alignmentRadiusSq;
    public float cohesionRadiusSq;
    public float separationRadiusfSq;

    public float alignmentWeight;
    public float cohesionWeight;
    public float separationWeight;

    public float maxSpeed;
    public float maxForce;
    private float deltaTime;

    public UpdateBoidsSequential(NativeQuadTree quadtree, NativeArray<Boid> boidsDataRead, NativeArray<Boid> boidsDataWrite, float alignmentRadiusSq, float cohesionRadiusSq, float separationRadiusfSq, float alignmentWeight, float cohesionWeight, float separationWeight, float maxSpeed, float maxForce, float deltaTime)
    {
        this.quadtree = quadtree;
        this.boidsDataRead = boidsDataRead;
        this.boidsDataWrite = boidsDataWrite;
        this.alignmentRadiusSq = alignmentRadiusSq;
        this.cohesionRadiusSq = cohesionRadiusSq;
        this.separationRadiusfSq = separationRadiusfSq;
        this.alignmentWeight = alignmentWeight;
        this.cohesionWeight = cohesionWeight;
        this.separationWeight = separationWeight;
        this.maxSpeed = maxSpeed;
        this.maxForce = maxForce;
        this.deltaTime = deltaTime;
    }

    public void Execute()
    {
        for (int index = 0; index < boidsDataRead.Length; index++)
        {
            NativeList<int> quadtreeResults = new NativeList<int>(Allocator.Temp);
        Boid boid = boidsDataRead[index];
        
        int test = 3;
        // if (boid.position.x > test)
        //     boid.velocity.x = -maxForce * 2;
        // if (boid.position.x < -test)
        //     boid.velocity.x = maxForce * 2;
        //
        // if (boid.position.y > test)
        //     boid.velocity.y = -maxForce * 2;
        // if (boid.position.y < -test)
        //     boid.velocity.y = maxForce * 2;
        if (!IsPointInsideBounds(new float4(0, 0, 16, 9), boid.position))
        {
            boid.velocity = (float2.zero - boid.position);
        }
        // if (!IsPointInsideBounds(new float4(0, 0, 16, 9), boid.position))
        // {
        //     if (boid.position.x > 16 / 2f)
        //         boid.position.x -= 15.99f;
        //     if (boid.position.x < 16 / -2f)
        //         boid.position.x += 15.99f;
        //     
        //     if (boid.position.y > 9 / 2f)
        //         boid.position.y -= 8.99f;
        //     if (boid.position.y < 9 / -2f)
        //         boid.position.y += 8.99f;
        // }
        
        quadtree.Query(quadtreeResults, new float4(boid.position, 0.5f, 0.5f));

        boid.amountNeighbours = 0;
        if (quadtreeResults.Length > 0)
        {
            float2 alignment = float2.zero;
            int alignmentCount = 0;
            float2 cohesion = float2.zero;
            int cohesionCount = 0;
            float2 seperation = float2.zero;
            int seperationCount = 0;
        
            for (int i = 0; i < quadtreeResults.Length; i++)
            {
                Boid otherBoid = boidsDataRead[quadtreeResults[i]];
                float distanceSq = math.distancesq(otherBoid.position, boid.position);
            
                if(distanceSq == 0)
                    continue;
            
                if (distanceSq < alignmentRadiusSq)
                {
                    alignment += otherBoid.velocity;
                    alignmentCount++;
                }
                if (distanceSq < cohesionRadiusSq)
                {
                    cohesion += otherBoid.position;
                    cohesionCount++;
                }
                if (distanceSq < separationRadiusfSq)
                {
                    float2 away = math.normalize(boid.position - otherBoid.position) / math.sqrt(distanceSq);
                    seperation += away;
                    seperationCount++;
                }
            }
        
            if(alignmentCount > 0)
                alignment /= alignmentCount;
            if(cohesionCount > 0)
                cohesion /= cohesionCount;
            if(seperationCount > 0)
                seperation /= seperationCount;
            
            alignment *= alignmentWeight;
            cohesion = (cohesion - boid.position) * cohesionWeight;
            seperation *= separationWeight;
        
            float2 newVel = alignment + cohesion + seperation;
            newVel = ClampMagnitude(newVel, maxForce);
            
            boid.velocity += newVel;
            boid.velocity = ClampMagnitude(boid.velocity, maxSpeed);
            boid.amountNeighbours = seperationCount;
        }
        
        boid.position += boid.velocity * deltaTime;
        
        boidsDataWrite[index] = boid;
        }
    }
    
    private float2 ClampMagnitude(float2 vector, float maxMagnitude)
    {
        float magnitudeSq = math.lengthsq(vector);
        if (magnitudeSq > maxMagnitude * maxMagnitude)
        {
            return math.normalize(vector) * maxMagnitude;
        }
        return vector;
    }
    
    private bool IsPointInsideBounds(float4 bounds, float2 point)
    {
        float2 halfSize = new float2(bounds.z * 0.5f, bounds.w * 0.5f);
        return math.all(point >= bounds.xy - halfSize & point <= bounds.xy + halfSize);
    }
}
