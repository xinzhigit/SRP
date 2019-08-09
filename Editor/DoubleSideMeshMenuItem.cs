using UnityEditor;
using UnityEngine;
using System.IO;

public static class DoubleSideMeshMenuItem
{
    [MenuItem("Assets/Create/Double-Sided Mesh")]
    static void MakeDoubleSizeMeshAsset() {
        var sourceMesh = Selection.activeObject as Mesh;
        if(sourceMesh == null) {
            Debug.LogError("You must have a mesh asset selected.");
            return;
        }

        Mesh insideMesh = Object.Instantiate(sourceMesh);
        int[] triangles = insideMesh.triangles;
        System.Array.Reverse(triangles);
        insideMesh.triangles = triangles;

        Vector3[] normals = insideMesh.normals;
        for(int n = 0; n < normals.Length; ++n) {
            normals[n] = -normals[n];
        }
        insideMesh.normals = normals;

        var combineMesh = new Mesh();
        combineMesh.CombineMeshes(new CombineInstance[] { new CombineInstance { mesh = insideMesh }, new CombineInstance { mesh = sourceMesh } }, true, false, false);

        Object.DestroyImmediate(insideMesh);

        AssetDatabase.CreateAsset(combineMesh, Path.Combine("Assets", sourceMesh.name + "Double-Sided.asset"));
    }
}
