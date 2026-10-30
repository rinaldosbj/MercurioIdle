#ifndef DYSON_LIGHTING_INCLUDED
#define DYSON_LIGHTING_INCLUDED
float4 _SolarColor, _ShadowColor, _SolarReflection, _EclipseOccluder;

float3 DysonNormalize(float3 value, float3 fallback)
{
    float lengthSquared = dot(value,value);
    return lengthSquared > 1e-12 ? value * rsqrt(max(lengthSquared,1e-12)) : fallback;
}

float DysonVisibility(float3 start, float3 end, float4 sphere)
{
    if (sphere.w <= 0) return 1;
    float3 segment = end-start;
    float fraction = dot(sphere.xyz-start,segment) / max(dot(segment,segment),1e-8);
    if (fraction <= 0 || fraction >= 1) return 1;
    float distanceToRay = length(start + fraction * segment - sphere.xyz);
    float edge = max(0.0001, sphere.w * _SolarReflection.w);
    return smoothstep(sphere.w-edge,sphere.w+edge,distanceToRay);
}

float3 DysonMirrorNormal(float3 position)
{
    float3 incoming = DysonNormalize(position-_SunPosition,float3(0,1,0));
    float3 outgoing = DysonNormalize(_TargetPosition-position,incoming);
    float3 tangent = normalize(cross(abs(incoming.y)<0.99 ? float3(0,1,0) : float3(1,0,0),incoming));
    return DysonNormalize(outgoing-incoming,tangent);
}

float3 DysonSolarLighting(float3 position, float3 normal, float variation)
{
    float3 toSun = _SunPosition-position;
    float distanceToSun = max(length(toSun),0.0001);
    float3 light = toSun / distanceToSun;
    float3 view = DysonNormalize(GetCameraPositionWS()-position,normal);
    // The mirror coating is on its Sun-facing side. Its back stays dark.
    float incidence = saturate(dot(normal,light));
    float front = smoothstep(-0.025,0.025,dot(normal,view));
    float visibility = DysonVisibility(position,_SunPosition,_EclipseOccluder);
    float reflectedAlignment = saturate(dot(reflect(-light,normal),view));
    float angularWidth = asin(saturate(_SolarReflection.z / distanceToSun)) + _SolarReflection.y + (1-_Smoothness)*(1-_Smoothness)*0.25;
    float glint = smoothstep(cos(min(angularWidth,1.5)),1,reflectedAlignment);
    float fresnel = pow(1-saturate(abs(dot(normal,view))),5);
    float3 base = _PanelColor.rgb * variation;
    float illuminated = incidence * visibility * front;
    float3 diffuse = base * _SolarColor.rgb * illuminated * lerp(0.7,0.18,_Metallic);
    float3 reflection = lerp(0.04.xxx,lerp(base,1.0.xxx,0.75),_Metallic)
        * _SolarColor.rgb * glint * illuminated * _SolarReflection.x * (1+fresnel);
    // The path to the focus does not control incident sunlight or observer-facing illumination.
    float3 lit = diffuse+reflection;
    return lit + _ShadowColor.rgb * 0.12 + _Emission.rgb * lerp(0.05,1,visibility*front);
}
#endif
