using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DysonSwarmRenderer))]
public sealed class DysonSwarmRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var renderer = (DysonSwarmRenderer)target;
        EditorGUI.BeginChangeCheck();
        int count = EditorGUILayout.IntField(new GUIContent("Maximum Ring Count", "Quantidade de anéis principais. Camadas e planos pertencem a cada anel e não são anéis adicionais."),renderer.RingCount);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(renderer,"Change Dyson ring count");
            renderer.SetRingCount(count);
            EditorUtility.SetDirty(renderer);
            serializedObject.Update();
        }
        DrawPropertiesExcluding(serializedObject,"rings","m_Script");
        var uniform = serializedObject.FindProperty("useUniformRingSpacing").boolValue;
        var automaticNormals = serializedObject.FindProperty("autoDistributeRingNormals").boolValue;
        var rings = serializedObject.FindProperty("rings");
        for(int i=0;i<rings.arraySize;i++) {
            var ring = rings.GetArrayElementAtIndex(i);
            ring.isExpanded = EditorGUILayout.Foldout(ring.isExpanded,"Ring "+i,true);
            if(!ring.isExpanded) continue;
            EditorGUI.indentLevel++;
            using(new EditorGUI.DisabledScope(uniform)) EditorGUILayout.PropertyField(ring.FindPropertyRelative("radius"));
            using(new EditorGUI.DisabledScope(automaticNormals)) EditorGUILayout.PropertyField(ring.FindPropertyRelative("ringNormal"));
            EditorGUILayout.PropertyField(ring.FindPropertyRelative("phaseOffset"));
            if (serializedObject.FindProperty("usePerRingOverrides").boolValue)
                foreach(var field in new[]{"angularThickness","planeCount","slotsPerPlane","orbitalSpeed"})
                    EditorGUILayout.PropertyField(ring.FindPropertyRelative(field));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.LabelField("Espelhos reais", renderer.MirrorCount.ToString("N0"));
        EditorGUILayout.LabelField("Populacao visual mapeada", renderer.VisualMirrorCount.ToString("N0"));
        EditorGUILayout.LabelField("Aneis ativos", renderer.ActiveRingCount.ToString());
        for (int i = 0; i < renderer.ActiveRingCount; i++) {
            renderer.GetActiveRingLayout(i, out int planes, out int slots, out float radial, out float vertical);
            EditorGUILayout.LabelField("Anel " + i + " ativo", planes + " planos / " + slots + " slots; sigma R=" + radial.ToString("G3") + "; H=" + vertical.ToString("G3"));
            renderer.GetSurfaceState(i,out float core,out double saturation,out int individuals);
            EditorGUILayout.LabelField("Faixa / bordas",core.ToString("G3")+" graus; "+individuals.ToString("N0")+" orbitais reservados");
            EditorGUILayout.LabelField("Saturacao: populacao total",(saturation*renderer.ActiveRingCount).ToString("G6"));
        }
        EditorGUILayout.LabelField("Coletores individuais reservados",renderer.RenderedCollectorCount.ToString("N0"));
        if(serializedObject.ApplyModifiedProperties() || GUI.changed) {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
