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
    public float test;
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
    [SerializeField] private int maxAmountEnemies = 10000;
    [SerializeField] private int maxQuadTreeDepth = 12;
    [SerializeField] private int maxObjectsPerCell = 5;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    private RenderParams renderParams;
    private NativeArray<Boid> boidsPing;
    private NativeArray<Boid> boidsPong;
    private JobHandle jobHandle;
    
    private ComputeBuffer boidsDataBuffer;
    private GraphicsBuffer commandBuf;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

    private NativeQuadTree quadTree;

    private void OnDisable()
    {
        jobHandle.Complete();
        boidsDataBuffer?.Dispose();
        commandBuf?.Dispose();
        boidsPing.Dispose();
        boidsPong.Dispose();
        quadTree.Dispose();
    }

    private void OnEnable()
    {
        boidsPing = new NativeArray<Boid>(maxAmountEnemies, Allocator.Persistent);
        boidsPong = new NativeArray<Boid>(maxAmountEnemies, Allocator.Persistent);
        renderParams = new RenderParams(material);
        boidsDataBuffer = new ComputeBuffer(maxAmountEnemies, sizeof(float) * 5);
        commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        quadTree = new NativeQuadTree(maxAmountEnemies, maxQuadTreeDepth, maxObjectsPerCell, new float2(17, 10), boidsPing);
        
        AddBoids addBoids = new AddBoids(maxAmountEnemies, boidsPing, (uint)Random.Range(0, 999));
        addBoids.Run();
        
        commandData[0].indexCountPerInstance = mesh.GetIndexCount(0);
        commandData[0].instanceCount = (uint)maxAmountEnemies;
        commandBuf.SetData(commandData);
        
        boidsDataBuffer.SetData(boidsPing);
        material.SetBuffer("BoidsDataBuffer", boidsDataBuffer);
    }
    
    private void Update()
    {
        jobHandle.Complete();

        NativeArray<Boid> currentBoidRead = Time.frameCount % 2 == 1 ? boidsPing : boidsPong;
        NativeArray<Boid> currentBoidWrite = Time.frameCount % 2 == 0 ? boidsPing : boidsPong;

        boidsDataBuffer.SetData(currentBoidRead);
        material.SetBuffer("BoidsDataBuffer", boidsDataBuffer);
        renderParams.material = material;
        Graphics.RenderMeshIndirect(renderParams, mesh, commandBuf);
        
        quadTree.Clear();
        quadTree.enemyTransforms = currentBoidRead;
        InsertBoidsQuadtree insertPointsJob = new InsertBoidsQuadtree(quadTree, currentBoidRead);
        // UpdateBoidsSequential updateBoids = new UpdateBoidsSequential(quadTree, currentBoidRead, currentBoidWrite, alignmentRadius, cohesionRadius, separationRadius, alignmentWeight, cohesionWeight, separationWeight, maxSpeed, maxForce, Time.deltaTime);
        UpdateBoids updateBoids = new UpdateBoids(quadTree, currentBoidRead, currentBoidWrite, alignmentRadius, cohesionRadius, separationRadius, alignmentWeight, cohesionWeight, separationWeight, maxSpeed, maxForce, Time.deltaTime);
        JobHandle jobHandle2 = insertPointsJob.Schedule();
        // jobHandle = updateBoids.Schedule(jobHandle2);
        jobHandle = updateBoids.ScheduleParallel(maxAmountEnemies, 64, jobHandle2);
    }
}

