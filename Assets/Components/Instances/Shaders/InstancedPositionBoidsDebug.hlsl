#ifndef SHADER_GRAPH_SUPPORT_H
#define SHADER_GRAPH_SUPPORT_H

#include "Matrix.hlsl"

StructuredBuffer<uint> debugDataBuffer;
StructuredBuffer<float2> precomputedBoundSizes;



int GetDepth(uint cellId)
{
    uint depthMasks[10] = {
        4,
        32,
        256,
        2048,
        16384,
        131072,
        1048576,
        8388608,
        67108864,
        536870912
    };
    
    for (int i = 9; i >= 0; i--)
    {
        if ((cellId & depthMasks[i]) > 0)
        {
            return i;
        }
    }
    return 10;
}

float4 GetCellBounds(uint cellId)
{
    float2 center = float2(0, 0);
    int depth = GetDepth(cellId);
    for (int i = depth; i >= 0 ; i--)
    {
        uint localCell = cellId >> i * 3;
        int iFlipped = depth - i;
        center.x += -precomputedBoundSizes[iFlipped + 1].x + precomputedBoundSizes[iFlipped].x * (int)(localCell & 1);
        center.y += -precomputedBoundSizes[iFlipped + 1].y + precomputedBoundSizes[iFlipped].y * (int)((localCell & 2) >> 1);
    }
    return float4(center, precomputedBoundSizes[depth].x, precomputedBoundSizes[depth].y);
}

inline void SetUnityMatrices(uint instanceID, inout float4x4 objectToWorld, inout float4x4 worldToObject)
{
// #if UNITY_ANY_INSTANCING_ENABLED
    float4x4 worldMatrix;
    float4 bounds = GetCellBounds(debugDataBuffer[instanceID]);
    worldMatrix = compose(float3(bounds.x, bounds.y, -2), float4(0, 0, 0, 1), float3(bounds.z, bounds.w, 1));
    // worldMatrix[0][3] = GetDepth(debugDataBuffer[instanceID]);
    
    // worldMatrix = compose(float3(bounds.x, bounds.y, -1), float4(0, 0, 0, 1), float3(bounds.z, bounds.w, 0.1));
    // if(instanceID < amountEnemies)
    // {
    //     worldMatrix = compose(float3(enemyDataBuffer[instanceID].position, -1), EulerToQuaternion(float3(0, 0, enemyDataBuffer[instanceID].angle)), float3(0.1, 0.1, 0.1));
    // }
    // else
    // {
    //     worldMatrix = compose(float3(enemyDataBuffer[instanceID].position, 1), EulerToQuaternion(float3(0, 0, enemyDataBuffer[instanceID].angle)), float3(0.1, 0.1, 0.1));
    // }

    objectToWorld = worldMatrix;
    
    float3x3 w2oRotation;
    w2oRotation[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
    w2oRotation[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
    w2oRotation[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;
    
    float det = dot(objectToWorld[0].xyz, w2oRotation[0]);
    w2oRotation = transpose(w2oRotation);
    w2oRotation *= rcp(det);
    float3 w2oPosition = mul(w2oRotation, -objectToWorld._14_24_34);
    
    worldToObject._11_21_31_41 = float4(w2oRotation._11_21_31, 0.0f);
    worldToObject._12_22_32_42 = float4(w2oRotation._12_22_32, 0.0f);
    worldToObject._13_23_33_43 = float4(w2oRotation._13_23_33, 0.0f);
    worldToObject._14_24_34_44 = float4(w2oPosition, 1.0f);
// #endif
}

void passthroughVec3_float(in float3 In, out float3 Out)
{
    Out = In;
}

void setup()
{
#if UNITY_ANY_INSTANCING_ENABLED
    SetUnityMatrices(unity_InstanceID, unity_ObjectToWorld, unity_WorldToObject);
#endif
}




#endif