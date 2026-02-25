#pragma vertex Vertex_main_instancing
#pragma fragment Fragment_main
#pragma multi_compile_instancing

// Per vertex is faster than per pixel, and almost indistinguishable for our purpose
#define USE_SH_PER_VERTEX

// This is the URP pass so set this define to activate OvrUnityGlobalIllumination headers
#define USING_URP
#pragma shader_feature UNITY_PIPELINE_URP      // Works before Unity 2021
#define UNITY_PIPELINE_URP      // Works after Unity 2021

// URP includes
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#include "../../../../ShaderUtils/OvrUnityLightsURP.hlsl"
#include "../../../../ShaderUtils/OvrUnityGlobalIlluminationURP.hlsl"

#include_with_pragmas "LibraryCommon.hlsl"
