#ifndef DYSON_SURFACE_COVERAGE_INCLUDED
#define DYSON_SURFACE_COVERAGE_INCLUDED

// Shared by the proxy and collector culling: first boundary along the SAME camera ray.
void DysonConsiderBoundary(float t, float face, float3 origin, float3 ray, float3 axis,
    float innerRadius, float outerRadius, float latitude, inout float nearest,
    inout float3 normal, inout float nearestFace)
{
    if(t<=1e-5 || t>=nearest) return;
    float3 p=origin+t*ray;
    float radius=length(p);
    float y=dot(p,axis);
    if(face<1.5) {
        if(abs(y)>latitude*radius+1e-4) return;
        normal=p/max(radius,1e-6)*(face<0.5 ? 1 : -1);
    } else {
        if(radius<innerRadius-1e-4 || radius>outerRadius+1e-4) return;
        nearestFace=y>=0 ? 2 : 3;
        float3 n=axis-(y/max(radius*radius,1e-8))*p;
        normal=normalize(n)*(y>=0 ? 1 : -1);
    }
    nearest=t;
    if(face<1.5) nearestFace=face;
}

bool DysonSurfaceHit(float3 camera, float3 ray, float3 sun, float3 axis,
    float radius, float halfDepth, float latitude, out float distance,
    out float3 normal, out float face)
{
    float3 origin=camera-sun;
    float innerRadius=max(1e-4,radius-halfDepth), outerRadius=radius+halfDepth;
    distance=1e30; normal=axis; face=-1;
    float b=dot(origin,ray), c=dot(origin,origin);
    float discriminant=b*b-c+outerRadius*outerRadius;
    if(discriminant>=0) {
        float root=sqrt(discriminant);
        DysonConsiderBoundary(-b-root,0,origin,ray,axis,innerRadius,outerRadius,latitude,distance,normal,face);
        DysonConsiderBoundary(-b+root,0,origin,ray,axis,innerRadius,outerRadius,latitude,distance,normal,face);
    }
    discriminant=b*b-c+innerRadius*innerRadius;
    if(discriminant>=0) {
        float root=sqrt(discriminant);
        DysonConsiderBoundary(-b-root,1,origin,ray,axis,innerRadius,outerRadius,latitude,distance,normal,face);
        DysonConsiderBoundary(-b+root,1,origin,ray,axis,innerRadius,outerRadius,latitude,distance,normal,face);
    }
    if(latitude<0.99999) {
        float y=dot(origin,axis), dy=dot(ray,axis), l2=latitude*latitude;
        float a=dy*dy-l2, qb=2*(y*dy-l2*b), qc=y*y-l2*c;
        if(abs(a)<1e-6) {
            if(abs(qb)>1e-6)
                DysonConsiderBoundary(-qc/qb,2,origin,ray,axis,innerRadius,outerRadius,latitude,distance,normal,face);
        } else {
            float disc=qb*qb-4*a*qc;
            if(disc>=0) {
                float root=sqrt(disc);
                DysonConsiderBoundary((-qb-root)/(2*a),2,origin,ray,axis,innerRadius,outerRadius,latitude,distance,normal,face);
                DysonConsiderBoundary((-qb+root)/(2*a),2,origin,ray,axis,innerRadius,outerRadius,latitude,distance,normal,face);
            }
        }
    }
    return face>=0;
}

float DysonDensityEdgeCoverage(float3 position, float3 sun, float3 axis, float2 coreSigma)
{
    if(coreSigma.x>=0.99999) return 1;
    float3 local=position-sun;
    float latitude=abs(dot(local,axis))/max(length(local),1e-6);
    float width=min(coreSigma.x,max(1e-6,coreSigma.y)*0.5);
    return smoothstep(0,width,coreSigma.x-latitude);
}

float DysonBoundaryCoverage(float3 position, float3 ray, float3 sun, float3 axis,
    float radius, float halfDepth, float latitude, float2 fade)
{
    if(fade.y<=0) return 1;
    float3 local=position-sun;
    float lengthLocal=max(length(local),1e-6);
    float3 radial=local/lengthLocal;
    float height=dot(local,axis);
    float radialSign=lengthLocal>=radius ? 1 : -1;
    float3 radialNormal=radialSign*radial;
    float3 normal=radialNormal;
    if(latitude<0.99999) {
        float heightSign=height>=0 ? 1 : -1;
        float3 sideGradient=heightSign*axis-latitude*radial;
        float3 sideNormal=sideGradient*rsqrt(max(dot(sideGradient,sideGradient),1e-8));
        float radialDistance=abs(lengthLocal-radius)-halfDepth;
        float sideDistance=(abs(height)-latitude*lengthLocal)/sqrt(max(1-latitude*latitude,1e-8));
        float blendWidth=max(1e-4,min(halfDepth,radius*latitude)*0.35);
        // The first face switches at the corner, but the coverage normal must not.
        // Blend the gradients in world units, independent of face IDs or tessellation.
        float sideWeight=smoothstep(-blendWidth,blendWidth,sideDistance-radialDistance);
        normal=lerp(radialNormal,sideNormal,sideWeight);
        normal*=rsqrt(max(dot(normal,normal),1e-8));
    }
    return smoothstep(fade.x,fade.y,abs(dot(normal,ray)));
}
#endif
