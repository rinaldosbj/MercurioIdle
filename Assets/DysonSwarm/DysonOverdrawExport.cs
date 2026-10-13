using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

// Opt-in diagnostic output. Never executes in ordinary scene rendering.
public static class DysonOverdrawExport {
    [Serializable] private class Metadata {
        public int width,height,channels=10,renderFrame,stage;
        public double represented;
        public bool focus;
        public string mode,origin="top-left",layout="pixel-major: near, points, opaque surface, edge surface, shared depth preparation; then the same five after clip";
    }
    public static void Save(string folder,int stage,bool raw,double represented,bool focus,int frame,uint[] data,int width,int height) {
        if(data==null || data.Length!=checked(width*height*10)) throw new InvalidOperationException("Overdraw GPU readback failed.");
        string mode=raw ? "coverage" : "depth";
        string path=Path.Combine(folder,"DysonOverdraw_"+stage+"_"+mode);
        using(var file=File.Create(path+".bin.gz"))
        using(var compressed=new GZipStream(file,System.IO.Compression.CompressionLevel.Fastest))
        using(var writer=new BinaryWriter(compressed)) foreach(uint value in data) writer.Write(value);
        File.WriteAllText(path+".json",JsonUtility.ToJson(new Metadata {
            width=width,height=height,renderFrame=frame,stage=stage,represented=represented,focus=focus,mode=mode
        },true));
        Debug.Log("OVERDRAW_SAVED stage="+stage+" mode="+mode+" frame="+frame+" pixels="+(width*height));
    }
}
