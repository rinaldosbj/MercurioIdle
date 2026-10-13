Shader "MercurioIdle/FocusSun"
{
 Properties { _SunTint("Sun Tint",Color)=(1,0.89,0.74,1) _Brightness("Brightness",Float)=1.2 }
 SubShader {
 Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
 Pass {
 Cull Back ZWrite On
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _SunTint;float _Brightness;
 struct A { float3 positionOS:POSITION;float3 normalOS:NORMAL; };
 struct V { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 normalOS:TEXCOORD2; };
 V Vert(A a) { V o;o.positionWS=TransformObjectToWorld(a.positionOS);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.normalOS=a.normalOS;return o; }
 half4 Frag(V i):SV_Target {
 float mu=saturate(dot(normalize(i.normalWS),normalize(GetCameraPositionWS()-i.positionWS)));
 float limb=0.4+0.6*mu;
 float granulation=0.98+0.02*sin(i.normalOS.x*173)*sin(i.normalOS.y*157)*sin(i.normalOS.z*149);
 return half4(_SunTint.rgb*_Brightness*limb*granulation,1);
 }
 ENDHLSL
 }
 }
}
