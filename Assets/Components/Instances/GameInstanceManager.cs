using System;
using Components.Instances.Jobs;
using Managers;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using EventType = Managers.EventType;
using Random = UnityEngine.Random;

[BurstCompile]
public class GameInstanceManager : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private float speed;
    [SerializeField] private float speedVariance;
    [SerializeField] private int startAmountEnemies = 100;
    [SerializeField] private int maxAmountEnemies = 10000;
    [SerializeField] private int maxQuadTreeDepth = 12;
    [SerializeField] private int maxObjectsPerCell = 5;
    [SerializeField] private float2 shootSize;
    [SerializeField] private float shootDamage;
    [SerializeField] private Camera cam;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    [Header("Bullet")]
    [SerializeField] private int maxAmountBullets = 10000;
    [SerializeField] private float bulletSpeed = 1;
    [SerializeField] private float bulletDamage = 1;
    [SerializeField] private int bulletPenetration = 1;
    [SerializeField] private Material bulletMaterial;
    
    private RenderParams renderParams;
    private NativeArray<Enemy> enemies;
    private NativeArray<Bullet> bullets;
    private NativeList<int> toBeRemoved;
    private NativeList<int> toBeRemovedBullets;
    private NativeArray<int> currentEnemyIndex;
    private NativeArray<int> currentBulletIndex;
    private NativeList<int> quadtreeResults;
    private float2 hitPos;
    private JobHandle jobHandle;
    
    private ComputeBuffer enemyDataBuffer;
    private ComputeBuffer bulletDataBuffer;
    private GraphicsBuffer commandBuf;
    private GraphicsBuffer commandBufBullets;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] commandDataBullets;

    private NativeQuadTree quadTree;

    private void OnDisable()
    {
        // EventSystem<float3>.Unsubscribe(EventType.PLAYER_SHOOT, ZapShot);
        
        jobHandle.Complete();
        enemyDataBuffer?.Dispose();
        commandBuf?.Dispose();
        enemies.Dispose();
        quadTree.Dispose();
        toBeRemoved.Dispose();
        currentEnemyIndex.Dispose();
        bullets.Dispose();
        toBeRemovedBullets.Dispose();
        quadtreeResults.Dispose();
        currentBulletIndex.Dispose();
    }

    private void OnEnable()
    {
        // EventSystem<float3>.Subscribe(EventType.PLAYER_SHOOT, ZapShot);
        
        enemies = new NativeArray<Enemy>(maxAmountEnemies, Allocator.Persistent);
        bullets = new NativeArray<Bullet>(maxAmountBullets, Allocator.Persistent);
        renderParams = new RenderParams(material);
        enemyDataBuffer = new ComputeBuffer(maxAmountEnemies, sizeof(float) * 5);
        bulletDataBuffer = new ComputeBuffer(maxAmountBullets, sizeof(float) * 7);
        commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandBufBullets = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        commandDataBullets = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        quadTree = new NativeQuadTree(maxAmountEnemies, maxQuadTreeDepth, maxObjectsPerCell, new float2(10, 10), enemies);
        toBeRemoved = new NativeList<int>(Allocator.Persistent);
        toBeRemovedBullets = new NativeList<int>(Allocator.Persistent);
        currentEnemyIndex = new NativeArray<int>(2, Allocator.Persistent);
        currentBulletIndex = new NativeArray<int>(2, Allocator.Persistent);
        quadtreeResults = new NativeList<int>(Allocator.Persistent);
        
        AddEnemies addEnemies = new AddEnemies(currentEnemyIndex, startAmountEnemies, maxAmountEnemies, speed, speedVariance, enemies, (uint)Random.Range(0, int.MaxValue));
        addEnemies.Run();
        
        commandData[0].indexCountPerInstance = mesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)startAmountEnemies;
        commandBuf.SetData(commandData);
        
        commandDataBullets[0].indexCountPerInstance = mesh.GetIndexCount(0);
        commandDataBullets[0].instanceCount = (uint)startAmountEnemies;
        commandBufBullets.SetData(commandDataBullets);
        
        enemyDataBuffer.SetData(enemies);
        material.SetBuffer("enemyDataBuffer", enemyDataBuffer);
    }
    
    private void Update()
    {
        jobHandle.Complete();

        enemyDataBuffer.SetData(enemies);
        material.SetBuffer("enemyDataBuffer", enemyDataBuffer);
        renderParams.material = material;
        commandData[0].instanceCount = (uint)currentEnemyIndex[1];
        commandBuf.SetData(commandData);
        Graphics.RenderMeshIndirect(renderParams, mesh, commandBuf);
        
        bulletDataBuffer.SetData(bullets);
        bulletMaterial.SetBuffer("bulletDataBuffer", bulletDataBuffer);
        renderParams.material = bulletMaterial;
        commandDataBullets[0].instanceCount = (uint)currentBulletIndex[1];
        commandBufBullets.SetData(commandDataBullets);
        Graphics.RenderMeshIndirect(renderParams, mesh, commandBufBullets);
        
        quadTree.Clear();
        AddEnemies addEnemies = new AddEnemies(currentEnemyIndex, maxAmountEnemies - currentEnemyIndex[1], maxAmountEnemies, speed, speedVariance, enemies, (uint)Random.Range(0, int.MaxValue));
        InsertPointsJob insertPointsJob = new InsertPointsJob(quadTree, enemies, currentEnemyIndex[1]);
        UpdateEnemies updateEnemies = new UpdateEnemies(enemies, toBeRemoved, currentEnemyIndex[1], Time.deltaTime);
        RemoveEnemies removeEnemies = new RemoveEnemies(currentEnemyIndex, enemies, toBeRemoved, maxAmountEnemies);
        
        JobHandle jobHandle2 = addEnemies.Schedule();
        JobHandle jobHandle3 = insertPointsJob.Schedule(jobHandle2);
        JobHandle jobHandle4 = updateEnemies.Schedule(jobHandle3);

        JobHandle jobHandle5 = new JobHandle();
        if (Input.GetMouseButton(0))
        {
            RaycastHit hit;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        
            if (Physics.Raycast(ray, out hit))
            {
                float2 dir = math.normalize(new float2(hit.point.x, hit.point.y));
                AddBullets addBullets = new AddBullets(currentBulletIndex, dir, float2.zero, bulletSpeed, bulletDamage, bulletPenetration, bullets, maxAmountBullets);
                jobHandle5 = addBullets.Schedule();
            }
        }
        UpdateBullets updateBullets = new UpdateBullets(currentBulletIndex, bullets, enemies, toBeRemovedBullets, toBeRemoved, quadTree, quadtreeResults, Time.deltaTime);
        JobHandle jobHandle6 = updateBullets.Schedule(JobHandle.CombineDependencies(jobHandle5, jobHandle4));

        RemoveBullets removeBullets = new RemoveBullets(currentBulletIndex, bullets, toBeRemovedBullets, maxAmountBullets);
        
        jobHandle = JobHandle.CombineDependencies(removeEnemies.Schedule(jobHandle6), removeBullets.Schedule(jobHandle6));
    }
}

