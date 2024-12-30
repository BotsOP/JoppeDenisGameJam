#ifndef SHADER_GRAPH_SUPPORT_H
#define SHADER_GRAPH_SUPPORT_H

struct Enemy
{
    float health;
    float speed;
};

StructuredBuffer<Enemy> enemyDataBuffer;
StructuredBuffer<float4x4> enemyMatrixBuffer;

inline void SetUnityMatrices(uint instanceID, inout float4x4 objectToWorld, inout float4x4 worldToObject)
{
// #if UNITY_ANY_INSTANCING_ENABLED
    objectToWorld = enemyMatrixBuffer[instanceID];
    worldToObject = enemyMatrixBuffer[instanceID];
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