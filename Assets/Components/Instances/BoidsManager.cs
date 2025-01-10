using System;
using System.Collections.Generic;
using Components.Instances.Jobs;
using Managers;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using EventType = Managers.EventType;
using Random = UnityEngine.Random;

public class BoidsManager : MonoBehaviour
{
    [Header("Flocking Behavior")]
    public float alignmentRadius = 3f;
    public float cohesionRadius = 3f;
    public float separationRadius = 1.5f;

    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float separationWeight = 1.5f;

    public float maxSpeed = 5f;
    public float maxForce = 3f;
    
    [Header("Settings")]
    [SerializeField] private bool showDebug;
    [SerializeField] private int maxAmountEnemies = 10000;
    [SerializeField] private int maxQuadTreeDepth = 12;
    [SerializeField] private int maxObjectsPerCell = 5;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    [SerializeField] private Material materialDebug;

    private RenderParams renderParams;
    private RenderParams renderParamsDebug;
    private NativeArray<Boid> boidsPing;
    private NativeArray<Boid> boidsPong;
    private JobHandle jobHandle;
    
    private ComputeBuffer boidsDataBuffer;
    private ComputeBuffer quadtreeDataBuffer;
    private ComputeBuffer quadtreePrecomputedBoundsBuffer;
    private GraphicsBuffer commandBuf;
    private GraphicsBuffer commandBufDebug;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] commandDataDebug;

    private NativeQuadTree quadTree;
    private NativeArray<uint> quadtreeKeys;

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.3f);
    }

    private void OnDisable()
    {
        jobHandle.Complete();
        boidsDataBuffer?.Dispose();
        quadtreeDataBuffer?.Dispose();
        quadtreePrecomputedBoundsBuffer?.Dispose();
        commandBuf?.Dispose();
        commandBufDebug?.Dispose();
        boidsPing.Dispose();
        boidsPong.Dispose();
        quadTree.Dispose();
    }

    private void OnEnable()
    {
        boidsPing = new NativeArray<Boid>(maxAmountEnemies, Allocator.Persistent);
        boidsPong = new NativeArray<Boid>(maxAmountEnemies, Allocator.Persistent);
        renderParams = new RenderParams(material);
        renderParamsDebug = new RenderParams(materialDebug);
        boidsDataBuffer = new ComputeBuffer(maxAmountEnemies, sizeof(float) * 5);
        quadtreeDataBuffer = new ComputeBuffer(maxAmountEnemies, sizeof(uint));
        commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandBufDebug = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        commandDataDebug = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        quadTree = new NativeQuadTree(maxAmountEnemies, maxQuadTreeDepth, maxObjectsPerCell, new float2(17, 10), boidsPing);
        quadtreePrecomputedBoundsBuffer = new ComputeBuffer(quadTree.precomputedBoundSizes.Length, sizeof(float) * 2, ComputeBufferType.Structured);
        quadtreePrecomputedBoundsBuffer.SetData(quadTree.precomputedBoundSizes);
        
        AddBoids addBoids = new AddBoids(maxAmountEnemies, boidsPing, (uint)Random.Range(0, 999));
        addBoids.Run();
        
        commandData[0].indexCountPerInstance = mesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)maxAmountEnemies;
        commandBuf.SetData(commandData);
        
        commandDataDebug[0].indexCountPerInstance = mesh.GetIndexCount(0);
        commandDataDebug[0].instanceCount = (uint)(maxAmountEnemies / maxObjectsPerCell);
        commandBufDebug.SetData(commandDataDebug);
        
        boidsDataBuffer.SetData(boidsPing);
        material.SetBuffer("BoidsDataBuffer", boidsDataBuffer);
        materialDebug.SetBuffer("precomputedBoundSizes", quadtreePrecomputedBoundsBuffer);

        alignmentRadius = Mathf.Pow(alignmentRadius, 2);
        cohesionRadius = Mathf.Pow(cohesionRadius, 2);
        separationRadius = Mathf.Pow(separationRadius, 2);
    }
    
    private void Update()
    {
        jobHandle.Complete();

        NativeArray<Boid> currentBoidRead = Time.frameCount % 2 == 1 ? boidsPing : boidsPong;
        NativeArray<Boid> currentBoidWrite = Time.frameCount % 2 == 0 ? boidsPing : boidsPong;

        boidsDataBuffer.SetData(currentBoidRead);
        material.SetBuffer("BoidsDataBuffer", boidsDataBuffer);
        Graphics.RenderMeshIndirect(renderParams, mesh, commandBuf);

        if (showDebug)
        {
            quadtreeKeys = quadTree.amountObjectsInCell.GetKeyArray(Allocator.Temp);
            quadtreeDataBuffer.SetData(quadtreeKeys);
            materialDebug.SetBuffer("debugDataBuffer", quadtreeDataBuffer);
            materialDebug.SetBuffer("precomputedBoundSizes", quadtreePrecomputedBoundsBuffer);
            Graphics.RenderMeshIndirect(renderParamsDebug, mesh, commandBufDebug);
        }
        
        quadTree.Clear();
        quadTree.enemyTransforms = currentBoidRead;
        InsertBoidsQuadtree insertPointsJob = new InsertBoidsQuadtree(quadTree, currentBoidRead);
        
        UpdateBoids updateBoids = new UpdateBoids(quadTree, currentBoidRead, currentBoidWrite, alignmentRadius, cohesionRadius, separationRadius, alignmentWeight, cohesionWeight, separationWeight, maxSpeed, maxForce, Time.deltaTime);
        JobHandle jobHandle2 = insertPointsJob.Schedule();
        jobHandle = updateBoids.ScheduleParallel(maxAmountEnemies, 64, jobHandle2);
    }
}

