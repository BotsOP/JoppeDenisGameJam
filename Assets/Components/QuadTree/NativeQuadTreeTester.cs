using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class NativeQuadTreeTester : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

[BurstCompile]
struct InsertPointsJob : IJob
{
    public NativeQuadTree quadtree;
    public NativeArray<float2> positions;
    public NativeArray<int> objectIndexes;
    public int until;
            
    public void Execute()
    {
        for (int i = 0; i < until; i++)
        {
            quadtree.Insert(objectIndexes[i], positions[i]);
        }
    }
}