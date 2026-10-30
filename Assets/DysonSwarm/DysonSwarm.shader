Shader "MercurioIdle/DysonSwarm"
{
    Properties {
        _PanelColor ("Panel Color", Color) = (0.3,0.65,1,1)
        [HideInInspector] _DysonZTest ("Diagnostic Z Test", Float) = 4
        [HideInInspector] _DysonSurfaceCull ("Surface Envelope Cull", Float) = 0
        [HideInInspector] _DysonZWrite ("Diagnostic Z Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "DysonForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            ZTest [_DysonZTest]
            ZWrite [_DysonZWrite]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_local _ DYSON_MEASURE_OVERDRAW
            #pragma require randomwrite : DYSON_MEASURE_OVERDRAW
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma multi_compile_instancing
            float3 _SunPosition, _TargetPosition, _RingNormal;
            float4 _RingData, _TorusData, _LatticeData, _PanelColor, _Emission;
            float _Phase, _PanelSize, _Metallic, _Smoothness, _DebugReflection, _ReflectionTolerance, _ValidateSharedOcclusion;
            #define DYSON_OVERDRAW_LAYER 0
            #include "DysonOverdraw.hlsl"
            #include "DysonLighting.hlsl"
            struct Panel { float4 position; float4 tangent; float4 bitangent; float4 normal; };
            StructuredBuffer<Panel> _Panels;
            StructuredBuffer<uint> _NearIds;
            struct Attributes { float3 positionOS : POSITION; uint instanceID : SV_InstanceID; };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                nointerpolation float error : TEXCOORD2;
                nointerpolation float variation : TEXCOORD3;
            };
            float3 SafeNormal(float3 value, float3 fallback) {
                float l = dot(value,value);
                return l > 1e-12 ? value * rsqrt(max(l,1e-12)) : fallback;
            }
            void Basis(float3 n, out float3 t, out float3 b) {
                t = normalize(cross(abs(n.y) < 0.99 ? float3(0,1,0) : float3(1,0,0), n));
                b = cross(n,t);
            }
            Varyings Vert(Attributes input)
            {
                uint id = _NearIds[input.instanceID];
                Panel panel = _Panels[id];
                Varyings output;
                output.positionWS = panel.position.xyz + _PanelSize * (input.positionOS.x*panel.tangent.xyz + input.positionOS.y*panel.bitangent.xyz + input.positionOS.z*panel.normal.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = panel.normal.xyz;
                output.error = panel.position.w;
                output.variation = 0.85 + 0.15 * frac(id * 0.61803398875);
                return output;
            }
            DYSON_OVERDRAW_EARLY
            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                if(_ValidateSharedOcclusion<0.5 || input.error<0) {
                    DysonCountFragment(input.positionCS.xy,0); DysonCountFragment(input.positionCS.xy,1);
                }
                if(_ValidateSharedOcclusion>0.5 && input.error<0) return half4(1,0,1,1);
                if (_DebugReflection > 0.5)
                    return half4(input.error <= _ReflectionTolerance ? float3(0,1,0) : float3(1,0,0),1);
                return half4(DysonSolarLighting(input.positionWS,normalize(input.normalWS),input.variation),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DysonPoints"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZTest [_DysonZTest]
            ZWrite [_DysonZWrite]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_local _ DYSON_MEASURE_OVERDRAW
            #pragma require randomwrite : DYSON_MEASURE_OVERDRAW
            #pragma vertex VertPoint
            #pragma fragment FragPoint
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            StructuredBuffer<float4> _Points;
            float4 _PanelColor, _Emission;
            float3 _SunPosition, _TargetPosition;
            float _Metallic, _Smoothness, _ValidateSharedOcclusion;
            #define DYSON_OVERDRAW_LAYER 1
            #include "DysonOverdraw.hlsl"
            #include "DysonLighting.hlsl"
            struct Attributes { float3 positionOS : POSITION; uint instanceID : SV_InstanceID; };
            struct Varyings { float4 positionCS : SV_POSITION; nointerpolation float3 color : TEXCOORD0; nointerpolation float hidden : TEXCOORD1; };
            Varyings VertPoint(Attributes input) {
                float4 pointSample = _Points[input.instanceID];
                Varyings output;
                output.positionCS = TransformWorldToHClip(pointSample.xyz);
                output.positionCS.xy += input.positionOS.xy * abs(pointSample.w) * output.positionCS.w / _ScaledScreenParams.xy;
                float variation = 0.85 + 0.15 * frac(dot(pointSample.xyz, float3(0.618,0.317,0.137)));
                output.hidden=pointSample.w<0 ? 1 : 0;
                output.color = _ValidateSharedOcclusion>0.5 && output.hidden>0.5 ? float3(1,0,1)
                    : DysonSolarLighting(pointSample.xyz,DysonMirrorNormal(pointSample.xyz),variation);
                return output;
            }
            DYSON_OVERDRAW_EARLY
            half4 FragPoint(Varyings input) : SV_Target {
                if(_ValidateSharedOcclusion<0.5 || input.hidden>0.5) {
                    DysonCountFragment(input.positionCS.xy,0); DysonCountFragment(input.positionCS.xy,1);
                }
                return half4(input.color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DysonContinuousSurface"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_DysonSurfaceCull]
            ZTest [_DysonZTest]
            ZWrite [_DysonZWrite]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_local _ DYSON_MEASURE_OVERDRAW
            #pragma require randomwrite : DYSON_MEASURE_OVERDRAW
            #pragma multi_compile_local _ DYSON_SHARED_SURFACE_DEPTH
            #pragma vertex VertSurface
            #pragma fragment FragSurface
            #define DYSON_OVERDRAW_LAYER 2
            #include "DysonSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DysonSurfaceEdgeFade"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_DysonSurfaceCull]
            ZTest [_DysonZTest]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_local _ DYSON_MEASURE_OVERDRAW
            #pragma require randomwrite : DYSON_MEASURE_OVERDRAW
            #pragma multi_compile_local _ DYSON_SHARED_SURFACE_DEPTH
            #pragma vertex VertSurface
            #pragma fragment FragSurface
            #define DYSON_SURFACE_FADE 1
            #define DYSON_OVERDRAW_LAYER 3
            #include "DysonSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DysonSharedSurfaceDepth"
            Cull [_DysonSurfaceCull]
            ZTest LEqual
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_local _ DYSON_MEASURE_OVERDRAW
            #pragma require randomwrite : DYSON_MEASURE_OVERDRAW
            #pragma vertex VertSurface
            #pragma fragment FragSurface
            #define DYSON_SURFACE_DEPTH_ONLY 1
            #define DYSON_OVERDRAW_LAYER 4
            #include "DysonSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DysonSharedSurfaceResolve"
            Cull Off
            ZTest [_DysonZTest]
            ZWrite [_DysonZWrite]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile_local _ DYSON_MEASURE_OVERDRAW
            #pragma require randomwrite : DYSON_MEASURE_OVERDRAW
            #pragma vertex VertResolve
            #pragma fragment FragResolve
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float3 _SunPosition, _TargetPosition;
            float4 _PanelColor, _Emission;
            float _Metallic, _Smoothness;
            #define DYSON_OVERDRAW_LAYER 2
            #include "DysonOverdraw.hlsl"
            #include "DysonLighting.hlsl"
            Texture2D<float4> _DysonSharedSurfaceDistance;
            struct ResolveVaryings { float4 positionCS : SV_POSITION; };
            ResolveVaryings VertResolve(uint id : SV_VertexID) {
                ResolveVaryings output; output.positionCS=GetFullScreenTriangleVertexPosition(id); return output;
            }
            struct ResolveOutput { half4 color : SV_Target; float depth : SV_Depth; };
            ResolveOutput FragResolve(ResolveVaryings input) {
                DysonCountFragment(input.positionCS.xy,0);
                float4 hit=_DysonSharedSurfaceDistance.Load(int3((uint2)input.positionCS.xy,0));
                clip(hit.w);
                DysonCountFragment(input.positionCS.xy,1);
                ResolveOutput output;
                output.color=half4(DysonSolarLighting(hit.xyz,DysonMirrorNormal(hit.xyz),0.925),1);
                float4 positionCS=TransformWorldToHClip(hit.xyz);
                output.depth=positionCS.z/positionCS.w;
                return output;
            }
            ENDHLSL
        }
    }
}
