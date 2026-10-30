            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float3 _SunPosition, _TargetPosition, _RingNormal;
            float4 _RingData, _TorusData, _SurfaceData, _PanelColor, _Emission;
            float4 _SurfaceGrazing;
            float _Metallic, _Smoothness, _SurfaceOccupancy;
            #include "DysonOverdraw.hlsl"
            #include "DysonLighting.hlsl"
            #include "DysonSurfaceCoverage.hlsl"
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float radius : TEXCOORD1; nointerpolation float face : TEXCOORD2; };
            Varyings VertSurface(Attributes input) {
                float3 t=normalize(cross(abs(_RingNormal.y)<0.99 ? float3(0,1,0):float3(1,0,0),_RingNormal));
                float3 b=cross(_RingNormal,t);
                float angle=input.positionOS.x*6.28318530718;
                // A conservative sphere only launches rays. The band boundary is analytic,
                // so triangle edges and joins between shell faces cannot cut holes into it.
                float latitudeSin=sin((2*input.positionOS.y-1)*1.57079632679);
                float radius=(_RingData.x+2*_TorusData.x)*1.002;
                float s,c;sincos(angle,s,c);
                float3 direction=latitudeSin*_RingNormal+sqrt(max(0,1-latitudeSin*latitudeSin))*(c*t+s*b);
                Varyings output;
                output.positionWS=_SunPosition+radius*direction;
                output.radius=radius;output.face=input.positionOS.z;
                output.positionCS=TransformWorldToHClip(output.positionWS);
                return output;
            }
            #if defined(DYSON_SHARED_SURFACE_DEPTH) && !defined(DYSON_SURFACE_DEPTH_ONLY)
            Texture2D<float4> _DysonSharedSurfaceDistance;
#endif
            struct SurfaceOutput { float4 color : SV_Target; float depth : SV_Depth; };
            SurfaceOutput FragSurface(Varyings input) {
                DysonCountFragment(input.positionCS.xy,0);
                float3 camera=GetCameraPositionWS();
                float3 toSurface=input.positionWS-camera;
                float3 ray=DysonNormalize(toSurface,float3(0,0,1));
                // Exactly one envelope fragment per ray, including cameras inside it.
                float outerRadius=(_RingData.x+2*_TorusData.x)*1.002;
                bool outside=length(camera-_SunPosition)>outerRadius;
                if(outside && dot(input.positionWS-_SunPosition,ray)>0) discard;
                float distance,face;float3 normal;
                if(!DysonSurfaceHit(camera,ray,_SunPosition,_RingNormal,_RingData.x,
                    2*_TorusData.x,_SurfaceData.x,distance,normal,face)) discard;
                float3 position=camera+distance*ray;
                float coverage=DysonBoundaryCoverage(position,ray,_SunPosition,_RingNormal,
                    _RingData.x,2*_TorusData.x,_SurfaceData.x,_SurfaceGrazing.xy);
                if(_SurfaceData.w<0) coverage*=DysonDensityEdgeCoverage(position,_SunPosition,_RingNormal,_SurfaceData.xy);
#if defined(DYSON_SURFACE_FADE)
                clip(0.999899-coverage);
                clip(coverage-0.00001);
#else
                clip(coverage-0.9999);
                coverage=1;
#endif
#if defined(DYSON_SHARED_SURFACE_DEPTH) && !defined(DYSON_SURFACE_DEPTH_ONLY)
                float closestDistance=_DysonSharedSurfaceDistance.Load(int3((uint2)input.positionCS.xy,0)).w;
                // Retain self and numerical ties; only discard confidently hidden hits.
                if(closestDistance>=0) clip(closestDistance+max(0.0001,distance*0.00001)-distance);
#endif
                DysonCountFragment(input.positionCS.xy,1);
                SurfaceOutput output;
                #if defined(DYSON_SURFACE_DEPTH_ONLY)
                output.color=float4(position,distance);
#else
                output.color=half4(DysonSolarLighting(position,DysonMirrorNormal(position),0.925),coverage);
#endif
                float4 clipPosition=TransformWorldToHClip(position);
                output.depth=clipPosition.z/clipPosition.w;
                return output;
            }
