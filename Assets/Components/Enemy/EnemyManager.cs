using System;
using Managers;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using EventType = Managers.EventType;
using Random = UnityEngine.Random;

[BurstCompile]
public class EnemyManager : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private float speedVariance;
    [SerializeField] private int startAmountEnemies = 100;
    [SerializeField] private int maxAmountEnemies = 10000;
    [SerializeField] private int maxQuadTreeDepth = 12;
    [SerializeField] private int maxObjectsPerCell = 5;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    
    private RenderParams renderParams;
    private NativeArray<Matrix4x4> matrices;
    private NativeArray<EnemyTransform> enemyTransforms;
    private NativeArray<Enemy> enemies;
    private int currentIndex;
    private int amountOfEnemies;
    
    private ComputeBuffer enemyDataBuffer;
    private ComputeBuffer enemyMatrixBuffer;
    private GraphicsBuffer commandBuf;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    private NativeQuadTree quadTree;

    private void OnDisable()
    {
        EventSystem<float3>.Unsubscribe(EventType.PLAYER_SHOOT, ZapShot);

        jobHandle.Complete();
        enemyDataBuffer?.Dispose();
        enemyMatrixBuffer?.Dispose();
        commandBuf?.Dispose();
        matrices.Dispose();
        enemies.Dispose();
        quadTree.Dispose();
    }

    private void OnEnable()
    {
        EventSystem<float3>.Subscribe(EventType.PLAYER_SHOOT, ZapShot);
        
        matrices = new NativeArray<Matrix4x4>(maxAmountEnemies, Allocator.Persistent);
        enemies = new NativeArray<Enemy>(maxAmountEnemies, Allocator.Persistent);
        renderParams = new RenderParams(material);
        enemyDataBuffer = new ComputeBuffer(maxAmountEnemies, sizeof(float) * 2);
        enemyMatrixBuffer = new ComputeBuffer(maxAmountEnemies, sizeof(float) * 16);
        commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        quadTree = new NativeQuadTree(maxAmountEnemies, maxQuadTreeDepth, maxObjectsPerCell, new float2(10, 10), matrices);
        
        for (int i = 0; i < startAmountEnemies; i++)
        {
            Vector3 pos = GetRandomPosition(4, (float)i / startAmountEnemies * (Mathf.PI * 2));
            // Vector3 pos = GetRandomPosition(5, Random.Range(0, Mathf.PI * 2));
            AddEnemy(pos);
        }
        
        commandData[0].indexCountPerInstance = mesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)amountOfEnemies;
        commandBuf.SetData(commandData);
        
        enemyDataBuffer.SetData(enemies);
        enemyMatrixBuffer.SetData(matrices);
        material.SetBuffer("enemyDataBuffer", enemyDataBuffer);
        material.SetBuffer("enemyMatrixBuffer", enemyMatrixBuffer);
    }

    // private void OnDrawGizmos()
    // {
    //     if(!Application.isPlaying)
    //         return;
    //
    //     foreach (uint cellId in quadTree.amountObjectsInCell.GetKeyArray(Allocator.Temp))
    //     {
    //         float4 bounds = quadTree.GetCellBounds(cellId);
    //         Gizmos.DrawWireCube(new Vector3(bounds.x, bounds.y, -2), new Vector3(bounds.z, bounds.w, 0.1f));
    //     }
    // }

    [BurstCompile]
    struct InsertPointsJob : IJob
    {
        private NativeQuadTree quadtree;
        [NativeDisableParallelForRestriction]
        private readonly NativeArray<Matrix4x4> matrices;
        private int until;
        
        public InsertPointsJob(NativeQuadTree quadtree, NativeArray<Matrix4x4> matrices, int until)
        {
            this.quadtree = quadtree;
            this.matrices = matrices;
            this.until = until;
        }

        public void Execute()
        {
            for (int i = 0; i < until; i++)
            {
                quadtree.Insert(i, new float2(matrices[i].m03, matrices[i].m13));
            }
        }
    }

    private JobHandle jobHandle;
    private void Update()
    {
        jobHandle.Complete();
        
        MoveEnemy(amountOfEnemies, ref matrices, ref enemies);
        enemyDataBuffer.SetData(enemies);
        enemyMatrixBuffer.SetData(matrices);
        material.SetBuffer("enemyDataBuffer", enemyDataBuffer);
        material.SetBuffer("enemyMatrixBuffer", enemyMatrixBuffer);
        
        Graphics.RenderMeshIndirect(renderParams, mesh, commandBuf);
        
        quadTree.Clear();
        InsertPointsJob insertPointsJob = new InsertPointsJob(quadTree, matrices, amountOfEnemies);
        jobHandle = insertPointsJob.Schedule(jobHandle);
        // for (int i = 0; i < amountOfEnemies; i++)
        // {
        //     quadTree.Insert(i, new float2(matrices[i].m03, matrices[i].m13));
        // }
    }

    [BurstCompile]
    private static void MoveEnemy(int amountOfEnemies, ref NativeArray<Matrix4x4> matrices, ref NativeArray<Enemy> enemies)
    {
        for (int i = 0; i < amountOfEnemies; i++)
        {
            Vector3 dirToCenter = -matrices[i].GetPosition().normalized * (enemies[i].speed * Time.deltaTime);
            dirToCenter.z = 0;
            Matrix4x4 matrix = matrices[i];
            matrix.m03 += dirToCenter.x;
            matrix.m13 += dirToCenter.y;
            matrices[i] = matrix;
        }
    }

    private void ZapShot(float3 pos)
    {
        pos.z = -1;
        ZapShotBurst(ref pos, 1, ref amountOfEnemies, ref currentIndex, ref matrices, ref enemies);
    }

    [BurstCompile]
    private static void ZapShotBurst(ref float3 pos, float zapRadius, ref int amountOfEnemies, ref int currentIndex, ref NativeArray<Matrix4x4> matrices, ref NativeArray<Enemy> enemies)
    {
        for (int i = 0; i < amountOfEnemies; i++)
        {
            if (math.distance(matrices[i].GetPosition(), pos) < zapRadius)
            {
                Enemy enemy = enemies[i];
                enemy.health -= 0.01f;
                if (enemy.health <= 0)
                {
                    // RemoveEnemy(i, ref amountOfEnemies, ref currentIndex, ref matrices, ref enemies);
                    currentIndex--;
                    amountOfEnemies--;
                    matrices[i] = matrices[currentIndex];
                    enemies[i] = enemies[currentIndex];
                    
                    Matrix4x4 matrix = matrices[currentIndex];
                    matrix.m03 = 9999;
                    matrices[currentIndex] = matrix;
                    
                    Enemy enemyDead = enemies[currentIndex];
                    enemyDead.speed = 0;
                    enemyDead.health = 0;
                    enemies[currentIndex] = enemyDead;
                    continue;
                }
                enemies[i] = enemy;
            }
        }
    }

    [BurstCompile]
    private static void RemoveEnemy(int index, ref int amountOfEnemies, ref int currentIndex, ref NativeArray<Matrix4x4> matrices, ref NativeArray<Enemy> enemies)
    {
        currentIndex--;
        amountOfEnemies--;
        matrices[index] = matrices[currentIndex];
        enemies[index] = enemies[currentIndex];
        
        Matrix4x4 matrix = matrices[currentIndex];
        matrix.m03 = 9999;
        matrices[currentIndex] = matrix;
    }

    private void AddEnemy(Vector3 position)
    {
        float angle = Vector3.Angle(-position.normalized, Vector3.up);
        angle = NormalizeAngle360(angle);
        Vector3 rotation = new Vector3(0, 0, angle);
        Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.Euler(rotation), Vector3.one * 0.1f);
        matrix.m23 = -1;
        matrices[currentIndex] = matrix;
        enemies[currentIndex] = (new Enemy(1, Random.Range(speed - speed * speedVariance, speed + speed * speedVariance)));

        currentIndex++;
        currentIndex %= maxAmountEnemies;
        amountOfEnemies++;
    }

    private Vector3 GetRandomPosition(float radius, float angle)
    {
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
    }
    
    [BurstCompile]
    private static float NormalizeAngle360(float angle)
    {
        // Shift negative angles to positive range
        if (angle < 0)
        {
            angle += 360;
        }
    
        // Ensure angle stays within 0-360 range
        angle %= 360;
    
        // Handle potential floating-point precision issues
        if (angle >= 360)
        {
            angle -= 360;
        }
    
        return angle;
    }
}

public struct EnemyTransform
{
    public float2 position;
    public float4 quaternion;
}

public struct Enemy
{
    public float health;
    public float speed;
    public Enemy(float health, float speed)
    {
        this.health = health;
        this.speed = speed;
    }
}