using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public struct NativeQuadTree
{
    private readonly int maxDepth;
    private readonly int objectsPerNode;
    private readonly float2 boundsSize;
    
    private NativeParallelHashMap<uint, int> amountObjectsInCell; //hoeveel objecten je in een cell hebt
    private NativeParallelMultiHashMap<uint, int> objects; //de indexen die wijzen naar de objecten in een cell
    private readonly NativeArray<Matrix4x4> enemyTransforms;

    public NativeQuadTree(int maxObjects, int maxDepth, int objectsPerNode, float2 boundsSize, NativeArray<Matrix4x4> enemyTransforms)
    {
        this.maxDepth = maxDepth;
        this.objectsPerNode = objectsPerNode;
        this.boundsSize = boundsSize;
        
        amountObjectsInCell = new NativeParallelHashMap<uint, int>(maxObjects, Allocator.Persistent);
        objects = new NativeParallelMultiHashMap<uint, int>(maxObjects / objectsPerNode, Allocator.Persistent);
        this.enemyTransforms = enemyTransforms;
    }

    public void Dispose()
    {
        amountObjectsInCell.Dispose();
        objects.Dispose();
    }

    public bool Insert(int objectIndex, float2 position)
    {
        int depth = 0;

        for (uint i = 0; i < 4; i++)
        {
            uint localIndex = GetChildIndex(0, i, depth);
            if (!amountObjectsInCell.TryGetValue(localIndex, out int amount))
            {
                amountObjectsInCell.Add(localIndex, 0);
            }
            
            float4 bounds = GetCellBounds(localIndex, depth);
            if (IsPointInsideBounds(bounds, position))
            {
                if (amount >= objectsPerNode)
                {
                    bool child1 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 0, depth + 1), out _);
                    bool child2 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 1, depth + 1), out _);
                    bool child3 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 2, depth + 1), out _);
                    bool child4 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 3, depth + 1), out _);
                    if (child1 &&
                        child2 &&
                        child3 &&
                        child4)
                    {
                        Subdivide(localIndex, depth);
                    }
                    return InsertNext(localIndex, depth, objectIndex, position);
                }
                
                amountObjectsInCell[localIndex]++;
                objects.Add(localIndex, objectIndex);
                return true;
            }
        }
        depth++;

        return false;
    }

    private bool InsertNext(uint parentIndex, int depth, int objectIndex, float2 position)
    {
        depth++;
        for (uint i = 0; i < 4; i++)
        {
            uint localIndex = GetChildIndex(parentIndex, i, depth);
            if (!amountObjectsInCell.TryGetValue(localIndex, out int amount))
            {
                amountObjectsInCell.Add(localIndex, 0);
            }
            
            float4 bounds = GetCellBounds(localIndex, depth);
            if (IsPointInsideBounds(bounds, position))
            {
                if (amount >= objectsPerNode)
                {
                    bool child1 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 0, depth + 1), out _);
                    bool child2 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 1, depth + 1), out _);
                    bool child3 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 2, depth + 1), out _);
                    bool child4 = !amountObjectsInCell.TryGetValue(GetChildIndex(localIndex, 3, depth + 1), out _);
                    if (child1 &&
                        child2 &&
                        child3 &&
                        child4)
                    {
                        Subdivide(localIndex, depth);
                    }
                    return InsertNext(localIndex, depth, objectIndex, position);
                }
                
                amountObjectsInCell[localIndex]++;
                objects.Add(localIndex, objectIndex);
                return true;
            }
        }
        
        return false;
    }

    private void Subdivide(uint cellIndex, int depth)
    {
        depth++;
        
        int objectCount = 0;
        NativeArray<int> objectsInCell = new NativeArray<int>(objectsPerNode, Allocator.Temp);
        foreach (var tempObj in objects.GetValuesForKey(cellIndex))
            objectsInCell[objectCount++] = tempObj;

        objects.Remove(cellIndex);

        for (uint i = 0; i < 4; i++)
        {
            uint localIndex = GetChildIndex(cellIndex, i, depth);
            float4 bounds = GetCellBounds(localIndex, depth);
            for (int j = 0; j < objectCount; j++)
            {
                if (!IsPointInsideBounds(bounds, new float2(enemyTransforms[objectsInCell[j]].m03, enemyTransforms[objectsInCell[j]].m13)))
                    continue;
                
                if(!amountObjectsInCell.TryGetValue(localIndex, out _))
                    amountObjectsInCell.Add(localIndex, 0);
                
                amountObjectsInCell[localIndex]++;
                objects.Add(localIndex, objectsInCell[j]);
            }
        }
    }

    private float4 GetCellBounds(uint cellId, int depth)
    {
        float2 center = float2.zero;
        for (int i = depth; i >= 0 ; i--)
        {
            uint localCell = cellId >> i * 3;
            int iFlipped = depth - i;
            center.x += -boundsSize.x / math.pow(2, 2 + iFlipped) + boundsSize.x / math.pow(2, 1 + iFlipped) * (int)(localCell & 1);
            center.y += -boundsSize.y / math.pow(2, 2 + iFlipped) + boundsSize.y / math.pow(2, 1 + iFlipped) * (int)((localCell & 2) >> 1);
        }
        return new float4(center, boundsSize.x / math.pow(2, 1 + depth), boundsSize.y / math.pow(2, 1 + depth));
    }

    private uint GetChildIndex(uint parentIndex, uint childIndex, int depth)
    {
        return (8 | childIndex) | parentIndex << 3;
    }
    
    private static bool IsPointInsideBounds(float4 bounds, float2 point)
    {
        // Extract bounds information
        float2 halfSize = new float2(bounds.z * 0.5f, bounds.w * 0.5f);

        // Check if the point is within bounds
        return point.x >= bounds.x - halfSize.x && point.x <= bounds.x + halfSize.x && point.y >= bounds.y - halfSize.y && point.y <= bounds.y + halfSize.y;
    }
}
