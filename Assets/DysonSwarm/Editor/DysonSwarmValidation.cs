using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DysonSwarmValidation
{
    [InitializeOnLoadMethod]
    private static void RunRequestedContinuityValidation()
    {
        const string request = "Library/DysonSwarm/GrowthValidation.request";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () => {
            if (!File.Exists(request)) return;
            File.Delete(request);
            try {
                ValidateGrowthContinuity();
                File.WriteAllText("Library/DysonSwarm/BirthValidation.txt", "PASSED: mapping, persistent GPU trajectories, cached/uncached parity, post-saturation birth CDF boundary support and render shader compilation.");
            } catch (Exception exception) {
                File.WriteAllText("Library/DysonSwarm/BirthValidation.txt", exception.ToString());
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/Dyson Swarm/Validate mirror mapping")]
    public static void ValidateMirrorMapping()
    {
        foreach (var configuration in new[] {
            new[] { 1000d, 30000000d, 42100000000000d },
            new[] { 1d, 2d, 3d }, new[] { 1d, 3d, 3d },
            new[] { 1d, 100d, 101d }, new[] { 1000d, 1001d, 1e100 }
        }) {
            double limit = configuration[0], visualMax = configuration[1], realMax = configuration[2];
            if (DysonSwarmRenderer.MapRealToVisualMirrors(limit, limit, visualMax, realMax) != limit ||
                DysonSwarmRenderer.MapRealToVisualMirrors(realMax, limit, visualMax, realMax) != visualMax)
                throw new Exception("Mirror mapping endpoint mismatch.");
            double previousSlope = 1;
            for (int i = 0; i < 100; i++) {
                double delta = Math.Exp(Math.Log(realMax - limit) + (i / 99d - 1) * Math.Log(1e10));
                double x = limit + delta, step = Math.Max(1e-4, Math.Abs(x) * 1e-6);
                double left = Math.Max(limit, x - step), right = Math.Min(realMax, x + step);
                if (right <= left) continue;
                double slope = (DysonSwarmRenderer.MapRealToVisualMirrors(right, limit, visualMax, realMax)
                    - DysonSwarmRenderer.MapRealToVisualMirrors(left, limit, visualMax, realMax)) / (right - left);
                if (double.IsNaN(slope) || slope < -1e-10 || slope > 1 + 1e-8 || slope > previousSlope + 1e-7)
                    throw new Exception($"Mirror mapping slope must decrease within [0,1]: {slope}.");
                previousSlope = slope;
            }
        }
        double joinSlope = (DysonSwarmRenderer.MapRealToVisualMirrors(1000.001) - 1000) / 0.001;
        double firstIncrement = DysonSwarmRenderer.MapRealToVisualMirrors(1001) - 1000;
        if (Math.Abs(joinSlope - 1) > 1e-6 || firstIncrement <= 0 || firstIncrement > 1 + 1e-8)
            throw new Exception("Linear/logarithmic derivative discontinuity.");
        Debug.Log($"DYSON_MAPPING_PASSED: endpoints, C1 join, nonincreasing derivatives in [0,1]; join slope={joinSlope}, 1000 -> 1001 adds {firstIncrement} visual mirrors.");
    }

    [MenuItem("Tools/Dyson Swarm/Validate growth continuity")]
    public static void ValidateGrowthContinuity()
    {
        ValidateMirrorMapping();
        int[] previous = new int[5];
        foreach (double count in new double[] { 0, 0.5, 999, 1000, 1001, 3599, 3600, 6799, 6800, 99999, 100000, 100001, 200001, 400001, 1000000 }) {
            int total = 0;
            for (int i = 0; i < 5; i++) {
                int current = DysonSwarmRenderer.StableRingPopulation(count, 100000, i, 5);
                if (current < previous[i]) throw new Exception("Growth removed existing ring IDs.");
                previous[i] = current; total += current;
            }
            if (total != Math.Ceiling(count)) throw new Exception("Growth population conservation failed.");
        }
        foreach (int slots in new[] { 1, 2, 80, 100, 512 }) {
            int stride = DysonSwarmRenderer.ProgressiveSlotStride(slots);
            var seen = new bool[slots];
            for (int i = 0; i < slots; i++) {
                int slot = i * stride % slots;
                if (seen[slot]) throw new Exception("Progressive slots duplicate mirrors.");
                seen[slot] = true;
            }
        }

        var obj = new GameObject("Continuity validation");
        var cameraObject = new GameObject("Continuity camera");
        var material = new Material(Shader.Find("MercurioIdle/DysonSwarm"));
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        try {
            var swarm = obj.AddComponent<DysonSwarmRenderer>();
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            var type = typeof(DysonSwarmRenderer);
            Action<string, object> set = (name, value) => type.GetField(name, flags).SetValue(swarm, value);
            Func<string, object> get = name => type.GetField(name, flags).GetValue(swarm);
            set("panelMaterial", material); set("autoDistributeRingNormals", true);
            // Exercise ring births, width limiting and core saturation at affordable populations.
            set("linearMirrorLimit", 1000000d); set("mirrorsPerAdditionalRing", 1200d);
            set("widthLimitStart", 3600d); set("maximumCloudWidth", 2f);
            set("physicalMirrorRadius", 0.07f);
            var shader = Resources.Load<ComputeShader>("DysonSwarm");
            Vector4[][] baseline = null;
            GraphicsBuffer firstBuffer = null, firstOrbitBuffer = null;
            foreach (int count in new[] { 999, 1000, 1001, 1201, 2401, 3599, 3600, 3601, 4801, 6799, 6800, 10000 }) {
                set("mirrorCount", (double)count);
                type.GetMethod("OnValidate", flags).Invoke(swarm, null);
                if (!(bool)type.GetMethod("PrepareSwarm", flags).Invoke(swarm, new object[] { camera, 12.5d }))
                    throw new Exception("Continuity renderer preparation failed.");
                var panels = (GraphicsBuffer[])get("panels");
                var orbits = (GraphicsBuffer[])get("orbitParameters");
                if (count == 999) { firstBuffer = panels[0]; firstOrbitBuffer = orbits[0]; }
                if (count == 1000 && (panels[0] != firstBuffer || orbits[0] != firstOrbitBuffer))
                    throw new Exception("999 -> 1000 reallocated the swarm.");
                var properties = (MaterialPropertyBlock[])get("ringProperties");
                var counts = (uint[])get("cachedCounts");
                var snapshots = new Vector4[panels.Length][];
                for (int ring = 0; ring < panels.Length; ring++) {
                    var props = properties[ring];
                    foreach (string name in new[] { "_SunPosition", "_TargetPosition", "_RingNormal", "_RingData", "_TorusData", "_LatticeData", "_Distribution", "_SurfaceData", "_SurfaceGrazing", "_BirthDistribution" })
                        shader.SetVector(name, props.GetVector(name));
                    Vector3 normal = props.GetVector("_RingNormal");
                    Vector3 tangent = Vector3.Cross(Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right, normal).normalized;
                    shader.SetVector("_CentralTangent", tangent); shader.SetVector("_CentralBitangent", Vector3.Cross(normal, tangent));
                    shader.SetFloat("_Phase", props.GetFloat("_Phase"));
                    shader.SetFloat("_UseSharedOcclusion", 0);
                    shader.SetTexture(0, "_CollectorOcclusionTiles", Texture2D.whiteTexture);
                    shader.SetVector("_FrustumPadding", Vector4.zero); shader.SetVector("_Lod", Vector4.zero);
                    shader.SetBuffer(3, "_Orbits", orbits[ring]); shader.SetInt("_OrbitCount", orbits[ring].count);
                    var birthCdf = ((GraphicsBuffer[])get("birthOrbitCdfs"))[ring];
                    shader.SetBuffer(0, "_BirthOrbitCdf", birthCdf); shader.SetBuffer(3, "_BirthOrbitCdf", birthCdf);
                    shader.SetInt("_DispatchWidth", ((orbits[ring].count + 63) / 64) * 64);
                    shader.Dispatch(3, (orbits[ring].count + 63) / 64, 1, 1);
                    var orbitSamples = new Vector4[orbits[ring].count * 3]; orbits[ring].GetData(orbitSamples);
                    int slots = (int)props.GetVector("_RingData").w;
                    for (int orbit = 0; orbit * slots < counts[ring]; orbit++) {
                        double core = DysonSwarmRenderer.ResolveSurfaceCore((double)orbit * slots + 1,
                            props.GetVector("_RingData").x, props.GetVector("_TorusData").y, 0.07, 0.8);
                        if (core <= 0 || orbitSamples[orbit * 3 + 2].w > 0.5f) continue;
                        float a = Vector3.Dot(normal, (Vector3)orbitSamples[orbit * 3]);
                        float b = Vector3.Dot(normal, (Vector3)orbitSamples[orbit * 3 + 1]);
                        if (Math.Sqrt(a * a + b * b) < core - 0.0001)
                            throw new Exception("A post-saturation orbit cannot reach its birth surface boundary.");
                    }
                    shader.SetBuffer(0, "_Orbits", orbits[ring]); shader.SetBuffer(0, "_Panels", panels[ring]);
                    shader.SetBuffer(0, "_NearIds", ((GraphicsBuffer[])get("nearIds"))[ring]);
                    shader.SetBuffer(0, "_Points", ((GraphicsBuffer[])get("pointPositions"))[ring]);
                    var counters = ((GraphicsBuffer[])get("counters"))[ring];
                    shader.SetFloat("_BackMaskCompute",0); shader.SetBuffer(0,"_BackMask",counters); shader.SetBuffer(0, "_Counters", counters); shader.SetBuffer(1, "_Counters", counters);
                    // Expose hidden IDs too, so their trajectories can be compared through saturation.
                    shader.SetVector("_SurfaceData", Vector4.zero);
                    shader.SetInt("_Count", (int)counts[ring]); shader.SetInt("_DispatchWidth", (((int)counts[ring] + 63) / 64) * 64);
                    Vector4[] cached = new Vector4[counts[ring] * 4];
                    for (int cache = 0; cache < 2; cache++) {
                        shader.SetFloat("_UseOrbitCache", cache);
                        shader.Dispatch(1, 1, 1, 1); shader.Dispatch(0, ((int)counts[ring] + 63) / 64, 1, 1);
                        var snapshot = new Vector4[cached.Length]; panels[ring].GetData(snapshot, 0, 0, snapshot.Length);
                        var visible = new uint[2]; counters.GetData(visible);
                        if (visible[0] != counts[ring]) throw new Exception("Continuity GPU lost mirrors.");
                        if (cache == 0) cached = snapshot;
                        else for (int j = 0; j < cached.Length; j++)
                            if (Vector4.Distance(cached[j], snapshot[j]) > 0.0001f) throw new Exception("Orbit cache changes mirror trajectories.");
                    }
                    snapshots[ring] = cached;
                    if (baseline != null && ring < baseline.Length)
                        for (int j = 0; j < baseline[ring].Length; j++)
                            if (Vector4.Distance(baseline[ring][j], cached[j]) > 0.0001f)
                                throw new Exception($"Growth moved an existing mirror: population={count}, ring={ring}, vector={j}.");
                }
                baseline = snapshots;
            }
            shader.SetFloat("_UseOrbitCache", 0);
            shader.SetVector("_LatticeData", Vector4.zero);
            shader.SetVector("_BirthDistribution", Vector4.zero);
            for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
            if (ShaderUtil.ShaderHasError(material.shader)) throw new Exception("Dyson render shader compilation failed.");
            Debug.Log("DYSON_CONTINUITY_PASSED: stable GPU IDs, cached/uncached parity, 999 -> 1000 buffer reuse, ring births, slot/plane/width thresholds and saturation.");
        } finally {
            UnityEngine.Object.DestroyImmediate(obj); UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    private static void ValidatePopulationGrowth()
    {
        if (DysonSwarmRenderer.ResolveGrowth(0,100,5) != 0 ||
            DysonSwarmRenderer.ResolveGrowth(0.5,100,5) != 1 ||
            DysonSwarmRenderer.ResolveGrowth(99.999,100,5) != 1 ||
            DysonSwarmRenderer.ResolveGrowth(100,100,5) != 2 ||
            DysonSwarmRenderer.ResolveGrowth(399.999,100,5) != 4 ||
            DysonSwarmRenderer.ResolveGrowth(400,100,5) != 5 ||
            DysonSwarmRenderer.ResolveGrowth(double.MaxValue,100,5) != 5 ||
            DysonSwarmRenderer.ResolveGrowth(double.NaN,100,5) != 0 ||
            DysonSwarmRenderer.ResolveGrowth(10,100,0) != 0)
            throw new Exception("Population growth boundary validation failed.");
        Debug.Log("Population growth: zero, fractional population, threshold boundaries and configured maxima passed.");
    }

    private static void ValidateGpuTransforms()
    {
        var shader = Resources.Load<ComputeShader>("DysonSwarm");
        if (shader == null) throw new Exception("Missing Dyson compute shader.");
        const int slots = 80, orbits = 150 * 3 * 3, count = slots * orbits;
        using (var panels = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 64))
        using (var near = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 4))
        using (var points = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 16))
        using (var counters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, 4))
        using (var nearArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, 4))
        using (var pointArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, 4)) {
            using var backNearArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, 4);
            using var backPointArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, 4);
            shader.SetFloat("_BackfaceSplit",0);
            shader.SetInt("_CollectorCapacity",count);
            shader.SetBuffer(2,"_BackNearArgs",backNearArgs); shader.SetBuffer(2,"_BackPointArgs",backPointArgs);
            using var birthCdf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 16);
            birthCdf.SetData(new Vector4[1]);
            shader.SetVector("_BirthDistribution", Vector4.zero);
            shader.SetBuffer(0,"_BirthOrbitCdf",birthCdf); shader.SetBuffer(3,"_BirthOrbitCdf",birthCdf);
            shader.SetBuffer(0,"_Panels",panels); shader.SetBuffer(0,"_NearIds",near);
            shader.SetBuffer(0,"_Points",points); shader.SetFloat("_BackMaskCompute",0); shader.SetBuffer(0,"_BackMask",counters); shader.SetBuffer(0,"_Counters",counters);
            shader.SetBuffer(1,"_Counters",counters); shader.SetBuffer(2,"_Counters",counters);
            shader.SetBuffer(2,"_NearArgs",nearArgs); shader.SetBuffer(2,"_PointArgs",pointArgs);
            shader.SetInt("_Count",count); shader.SetInt("_DispatchWidth",((count+63)/64)*64);
            shader.SetVector("_SunPosition",Vector3.zero); shader.SetVector("_TargetPosition",new Vector3(18,0,0));
            shader.SetVector("_RingNormal",new Vector3(1,1,1).normalized);
            shader.SetVector("_RingData",new Vector4(15,0.035f,150,slots));
            shader.SetVector("_TorusData",new Vector4(0.3f,0.3f + 15*Mathf.Sin(0.035f),2,1));
            shader.SetVector("_LatticeData",new Vector4(3,3,0,0));
            shader.SetVector("_SurfaceData",Vector4.zero);
            shader.SetVector("_Distribution",new Vector4(1,0.75f,1,12345));
            shader.SetVector("_CameraPosition",new Vector3(0,0,-30)); shader.SetVector("_CameraForward",Vector3.forward);
            shader.SetFloat("_Phase",0);shader.SetFloat("_CullMargin",0);
            var transforms = new Vector4[count * 4];
            var baseline = new Vector3[count];
            float maxError = 0, minRadiusGap = float.MaxValue, maxPlaneError = 0;
            for (int mode = 0; mode < 4; mode++) {
                shader.SetVector("_Lod",new Vector4(mode == 0 ? 0 : mode == 1 || mode == 4 ? 100 : mode == 2 ? 1 : 2,0.5f,mode == 3 ? 1 : 30,mode == 3 ? 1 : 0));
                nearArgs.SetData(new uint[] {18,0,0,0,0}); pointArgs.SetData(new uint[] {3,0,0,0,0});
                shader.Dispatch(1,1,1,1); shader.Dispatch(0,(count+63)/64,1,1); shader.Dispatch(2,1,1,1);
                var counts = new uint[2]; counters.GetData(counts);
                var na = new uint[5]; var pa = new uint[5]; nearArgs.GetData(na); pointArgs.GetData(pa);
                if (counts[0]+counts[1] != count || na[1] != counts[0] || pa[1] != counts[1])
                    throw new Exception($"LOD lost or duplicated collectors / indirect argument mismatch: mode={mode}, near={counts[0]}, points={counts[1]}, nearArgs={na[1]}, pointArgs={pa[1]}");
                if (mode == 0 && counts[0] != count || (mode == 1 || mode == 3) && counts[1] != count || mode == 2 && (counts[0] == 0 || counts[1] == 0))
                    throw new Exception("Incorrect near / far / mixed / orthographic LOD classification.");
                if (mode == 0) {
                    panels.GetData(transforms);
                    var radii = new float[orbits];
                    for (int i = 0; i < count; i++) {
                        var v = transforms[i*4]; var position = new Vector3(v.x,v.y,v.z);
                        if (float.IsNaN(v.x) || position.magnitude < 13.799f || position.magnitude > 16.201f) throw new Exception("Invalid GPU orbit.");
                        maxError = Mathf.Max(maxError,v.w); baseline[i] = position;
                    }
                    for (int o = 0; o < orbits; o++) {
                        radii[o] = baseline[o*slots].magnitude;
                        var a = baseline[o*slots]; var b = baseline[o*slots+slots/4];
                        var normal = Vector3.Cross(a,b).normalized;
                        for (int j = 0; j < slots; j++) {
                            var position = baseline[o*slots+j];
                            maxPlaneError = Mathf.Max(maxPlaneError, Mathf.Abs(Vector3.Dot(normal,position)));
                            var next = baseline[o*slots+(j+1)%slots];
                            if (Mathf.Abs(Vector3.Dot(position.normalized,next.normalized)-Mathf.Cos(2*Mathf.PI/slots)) > 0.00001f)
                                throw new Exception("Non-uniform collector spacing inside an orbital plane.");
                        }
                    }
                    double sumX=0,sumX2=0,sumZ2=0,sumZ4=0;
                    var axis=new Vector3(1,1,1).normalized;
                    foreach(var position in baseline) {
                        double z=Vector3.Dot(position,axis);
                        double x=Math.Sqrt(Math.Max(0,position.sqrMagnitude-z*z))-15;
                        sumX+=x;sumX2+=x*x;sumZ2+=z*z;sumZ4+=z*z*z*z;
                    }
                    double sx=Math.Sqrt(sumX2/count-Math.Pow(sumX/count,2));
                    double sz=Math.Sqrt(sumZ2/count);double kurtosis=sumZ4/count/Math.Pow(sumZ2/count,2);
                    if(sx<0.25||sx>0.35||sz<0.75||sz>0.9||kurtosis<2.4||kurtosis>3.6)
                        throw new Exception($"Non-Gaussian projected density: radial sigma={sx}, vertical sigma={sz}, vertical kurtosis={kurtosis}");
                    Debug.Log($"Projected GPU density: radial sigma={sx}, vertical sigma={sz}, vertical kurtosis={kurtosis} (normal: 3)");
                    Array.Sort(radii);
                    for (int i = 1; i < radii.Length; i++) minRadiusGap = Mathf.Min(minRadiusGap,radii[i]-radii[i-1]);
                    // Random floating-point radii may coincide at machine precision; no fixed shells.
                }
                if (mode == 1) {
                    var pv = new Vector4[count]; points.GetData(pv);
                    var positions = Array.ConvertAll(pv,v=>new Vector3(v.x,v.y,v.z));
                    Comparison<Vector3> compare = (a,b)=> { int c=a.x.CompareTo(b.x); return c!=0?c:a.y.CompareTo(b.y); };
                    Array.Sort(baseline,compare); Array.Sort(positions,compare);
                    for (int i = 0; i < count; i++) if (Vector3.Distance(baseline[i],positions[i]) > 0.0001f) throw new Exception("LOD changed orbital positions.");
                }
                Debug.Log($"GPU LOD mode {mode}: hexagons={counts[0]}, points={counts[1]}, total={count}");
            }
            shader.SetVector("_SurfaceData",new Vector4(0.2f,0.05f,1,1));
            shader.SetVectorArray("_SurfaceOrbitCdf",DysonSwarmRenderer.BuildSurfaceOrbitCdf(0.2,0.05,out double mass));
            shader.SetVector("_LatticeData",new Vector4(1,1,0.3f,0));
            shader.SetVector("_Lod",Vector4.zero);
            var positionsByFrame=new Vector3[3][];var visibleByFrame=new bool[3][];
            int radialInner=0,radialOuter=0,lateral=0;
            for(int frame=0;frame<3;frame++) {
                shader.SetFloat("_Phase",frame*0.37f);shader.Dispatch(1,1,1,1);shader.Dispatch(0,(count+63)/64,1,1);shader.Dispatch(2,1,1,1);
                var counts=new uint[2];counters.GetData(counts);
                var arguments=new uint[5];nearArgs.GetData(arguments);
                if(counts[0]<=0 || counts[0]>=count || counts[1]!=0 || arguments[1]!=counts[0]) throw new Exception("Dense-volume culling failed.");
                var ids=new uint[count];near.GetData(ids);panels.GetData(transforms);
                positionsByFrame[frame]=new Vector3[count];visibleByFrame[frame]=new bool[count];
                var axis=new Vector3(1,1,1).normalized;
                for(int j=0;j<counts[0];j++) {
                    int id=(int)ids[j];var v=transforms[id*4];var position=new Vector3(v.x,v.y,v.z);
                    float offset=position.magnitude-15;float latitude=Mathf.Abs(Vector3.Dot(position.normalized,axis));
                    if(visibleByFrame[frame][id] || float.IsNaN(offset) || Mathf.Abs(offset)>1.201f || latitude<0.19999f && Mathf.Abs(offset)<0.598f)
                        throw new Exception("Invalid outskirts or an individual inside the replaced dense volume.");
                    positionsByFrame[frame][id]=position;visibleByFrame[frame][id]=true;
                    if(frame==0 && latitude<0.2f) { if(offset<0)radialInner++;else radialOuter++; }
                    if(frame==0 && latitude>=0.2f && Mathf.Abs(offset)<0.598f)lateral++;
                }
            }
            shader.SetFloat("_Phase",0);shader.SetFloat("_CullMargin",0.15f);
            shader.Dispatch(1,1,1,1);shader.Dispatch(0,(count+63)/64,1,1);shader.Dispatch(2,1,1,1);
            var marginCounts=new uint[2];counters.GetData(marginCounts);
            int beforeMargin=0;foreach(bool visible in visibleByFrame[0]) if(visible)beforeMargin++;
            if(marginCounts[0]<=beforeMargin)throw new Exception("Conservative culling did not preserve border collectors.");
            Debug.Log($"Dense-volume border margin: {marginCounts[0]-beforeMargin} additional edge collectors retained.");
            shader.SetFloat("_CullMargin",0);
            int checkedOrbits=0;
            for(int id=0;id<count;id++) if(visibleByFrame[0][id] && visibleByFrame[1][id] && visibleByFrame[2][id]) {
                var a=positionsByFrame[0][id];var b=positionsByFrame[1][id];var c=positionsByFrame[2][id];
                float error=Mathf.Abs(Vector3.Dot(Vector3.Cross(a,b).normalized,c));
                if(error>0.001f || Mathf.Abs(a.magnitude-b.magnitude)>0.0001f || Mathf.Abs(Vector3.Dot(a.normalized,b.normalized)-Mathf.Cos(0.37f))>0.00001f)
                    throw new Exception("A continuous-regime orbit does not pass through the Sun or does not keep its radius.");
                checkedOrbits++;
            }
            if(checkedOrbits<100 || radialInner<100 || radialOuter<100 || lateral<100) throw new Exception("Missing 3D outskirts or insufficient orbital samples.");
            Debug.Log($"Continuous GPU: {checkedOrbits} persistent centered circular trajectories; inner radial={radialInner}, outer radial={radialOuter}, lateral={lateral}; dense-volume culling passed.");
            shader.SetInt("_Count",0);shader.Dispatch(1,1,1,1);shader.Dispatch(0,1,1,1);shader.Dispatch(2,1,1,1);
            if (maxError > 0.001f || maxPlaneError > 0.001f) throw new Exception("GPU reflection or Sun-centered plane error.");
            Debug.Log($"GPU validation: reflection error={maxError}, plane error={maxPlaneError}, minimum unique altitude gap={minRadiusGap}");
        }
    }
    [MenuItem("Tools/Dyson Swarm/Validate shader and reflection")]
    public static void Validate()
    {
        ValidatePopulationGrowth();
        double surfaceRadius=15,size=0.045,sigma=0.6,occupancy=0.8;
        double saturated=DysonSwarmRenderer.SurfacePopulation(surfaceRadius,sigma,0,size,occupancy);
        double closed=DysonSwarmRenderer.SurfacePopulation(surfaceRadius,sigma,1,size,occupancy);
        double previous=0;
        foreach(double population in new[]{saturated*0.5,saturated,saturated*2,saturated*5,closed,1e9,1e12}) {
            double core=DysonSwarmRenderer.ResolveSurfaceCore(population,surfaceRadius,sigma,size,occupancy);
            if(core<previous || core<0 || core>1) throw new Exception("Surface growth is not monotonic.");
            if(population>saturated && population<closed && Math.Abs(DysonSwarmRenderer.SurfacePopulation(surfaceRadius,sigma,core,size,occupancy)-population)>population*1e-8)
                throw new Exception("Surface population conservation failed.");
            previous=core;
        }
        Debug.Log("Continuous surface: population conservation, monotonic growth and full closure through 1e12 passed.");
        ValidateGpuTransforms();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/DysonSwarm/DysonSwarm.shader");
        if (shader == null) return;
        var material = new Material(shader);
        try {
            ShaderUtil.CompilePass(material, 0, true);
            ShaderUtil.CompilePass(material, 1, true);
            ShaderUtil.CompilePass(material, 2, true);
            float maxError = 0, maxPlaneError = 0, maxRadiusError = 0;
            int samples = 0;
            foreach (var ring in DysonSwarmRenderer.CreateDefaultRings())
            for (int mode = 0; mode < 2; mode++)
            for (int section = 0; section < (mode == 1 ? 9 : 1); section++) {
                float x = mode == 1 ? section % 3 - 1 : 0;
                float y = mode == 1 ? (section / 3) * 0.5f : 0;
                var disk = new Vector2(x*Mathf.Sqrt(1-0.5f*y*y),y*Mathf.Sqrt(1-0.5f*x*x));
                disk *= disk.magnitude; // Density Concentration = 2.
                float radius = ring.radius + 0.3f*disk.x;
                // Test oblique central axes as well as circular phase/radius constraints.
                var initial = (Quaternion.Euler(23,37,11)*ring.ringNormal).normalized;
                var centralT = Vector3.Cross(Mathf.Abs(initial.y)<0.99f ? Vector3.up : Vector3.right,initial).normalized;
                var centralB = Vector3.Cross(initial,centralT);
                float spread = ring.angularThickness*Mathf.Deg2Rad + Mathf.Asin(Mathf.Clamp01(0.3f/radius));
                float inclination = mode == 1 ? spread*disk.y : ring.angularThickness*Mathf.Deg2Rad;
                for (int p = 0; p < ring.planeCount; p++) {
                    float omega=2*Mathf.PI*p/ring.planeCount;
                    var n=(Mathf.Cos(inclination)*initial + Mathf.Sin(inclination)*(Mathf.Cos(omega)*centralT + Mathf.Sin(omega)*centralB)).normalized;
                    var t=(centralT-n*Vector3.Dot(centralT,n)).normalized;
                    var b=Vector3.Cross(n,t);
                    if(Mathf.Abs(Vector3.Dot(n,initial)-Mathf.Cos(inclination))>0.00001f)
                        throw new Exception("Plane normal escaped its ring's inclination cone.");
                    for (int s = 0; s < 64; s++)
                    for (int frame = 0; frame < 3; frame++) {
                        float theta = 2*Mathf.PI*s/64 + (p+section*0.5f)*Mathf.PI/ring.slotsPerPlane + ring.phaseOffset*Mathf.Deg2Rad + frame*1.7f;
                        var position = radius*(Mathf.Cos(theta)*t + Mathf.Sin(theta)*b);
                        maxPlaneError = Mathf.Max(maxPlaneError,Mathf.Abs(Vector3.Dot(position,n)));
                        maxRadiusError = Mathf.Max(maxRadiusError,Mathf.Abs(position.magnitude-radius));
                        var incoming = position.normalized;
                        var outgoing = (new Vector3(18,0,0)-position).normalized;
                        var difference = outgoing-incoming;
                        var normal = difference.sqrMagnitude > 1e-12f ? difference.normalized : Vector3.Cross(Mathf.Abs(incoming.y)<0.99f ? Vector3.up : Vector3.right,incoming).normalized;
                        maxError = Mathf.Max(maxError,(Vector3.Reflect(incoming,normal)-outgoing).magnitude);
                        samples++;
                    }
                }
            }
            bool errors = ShaderUtil.ShaderHasError(shader);
            string report = $"Shader supported: {shader.isSupported}\nShader errors: {errors}\nSamples: {samples}\nReflection error: {maxError}\nSun-centered orbital plane error: {maxPlaneError}\nCircular radius error: {maxRadiusError}\n";
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) report += $"{message.severity}: {message.message} ({message.file}:{message.line})\n";
            Directory.CreateDirectory("Library/DysonSwarm");
            File.WriteAllText("Library/DysonSwarm/Validation.txt",report);
            if (errors || maxError > 0.001f || maxPlaneError > 0.001f || maxRadiusError > 0.001f) Debug.LogError(report); else Debug.Log(report);
        } finally { UnityEngine.Object.DestroyImmediate(material); }
    }
}
