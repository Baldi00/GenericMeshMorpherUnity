using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshChanger : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> objects;
    [SerializeField]
    private float animationDuration = 1;

    private int currentObjectIndex = 0;
    private float animationTimer;

    private ComputeBuffer differenceVectorsFromPrevToNextBuffer;
    private ComputeBuffer differenceVectorsFromNextToPrevBuffer;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
            NextMesh();
    }

    private void NextMesh()
    {
        GameObject previousObject = objects[currentObjectIndex];
        currentObjectIndex = (currentObjectIndex + 1) % objects.Count;
        GameObject nextObject = objects[currentObjectIndex];

        Mesh previousMesh = previousObject.GetComponent<MeshFilter>().mesh;
        Mesh nextMesh = nextObject.GetComponent<MeshFilter>().mesh;
        Material previousObjectMaterial = previousObject.GetComponent<MeshRenderer>().material;
        Material nextObjectMaterial = nextObject.GetComponent<MeshRenderer>().material;

        Matrix4x4 previousLocalToWorld = previousObject.transform.localToWorldMatrix;
        Matrix4x4 nextLocalToWorld = nextObject.transform.localToWorldMatrix;

        differenceVectorsFromPrevToNextBuffer = new ComputeBuffer(previousMesh.vertexCount, sizeof(float) * 3);
        differenceVectorsFromPrevToNextBuffer.SetData(
            FindNearestVertices(previousMesh, nextMesh, ref previousLocalToWorld, ref nextLocalToWorld));
        previousObjectMaterial.SetBuffer("_DistancesFromOtherObjectVertices", differenceVectorsFromPrevToNextBuffer);

        differenceVectorsFromNextToPrevBuffer = new ComputeBuffer(nextMesh.vertexCount, sizeof(float) * 3);
        differenceVectorsFromNextToPrevBuffer.SetData(
            FindNearestVertices(nextMesh, previousMesh, ref nextLocalToWorld, ref previousLocalToWorld));
        nextObjectMaterial.SetBuffer("_DistancesFromOtherObjectVertices", differenceVectorsFromNextToPrevBuffer);

        StartCoroutine(AnimateMeshChange(previousObject, nextObject, previousObjectMaterial, nextObjectMaterial));
    }

    /// <summary>
    /// For every vertex in previous mesh finds the difference vectors to the nearest vertex in next mesh.
    /// Transformation matrices are needed because the distances and difference vectors are computed in world space
    /// </summary>
    /// <param name="previousMesh">The starting mesh of the animation</param>
    /// <param name="nextMesh">The end mesh of the animation</param>
    /// <param name="previousLocalToWorld">Transformation matrix from local to world for previous mesh</param>
    /// <param name="nextLocalToWorld">Transformation matrix from local to world for next mesh</param>
    /// <returns>The difference vectors to the nearest next mesh vertex for each previous mesh vertex</returns>
    private Vector3[] FindNearestVertices(Mesh previousMesh, Mesh nextMesh, ref Matrix4x4 previousLocalToWorld, ref Matrix4x4 nextLocalToWorld)
    {
        Vector3[] differencesVectorsFromPrevToNext = new Vector3[previousMesh.vertexCount];

        for (int i = 0; i < previousMesh.vertexCount; i++)
        {
            Vector3 currentPrevWorldVertex = previousLocalToWorld.MultiplyPoint3x4(previousMesh.vertices[i]);

            Vector3 currentNextWorldVertex = nextLocalToWorld.MultiplyPoint3x4(nextMesh.vertices[0]);
            Vector3 differenceVector = currentNextWorldVertex - currentPrevWorldVertex;
            float sqrDistance = differenceVector.sqrMagnitude;

            for (int j = 1; j < nextMesh.vertexCount; j++)
            {
                currentNextWorldVertex = nextLocalToWorld.MultiplyPoint3x4(nextMesh.vertices[j]);
                float currentSqrDistance = (currentNextWorldVertex - currentPrevWorldVertex).sqrMagnitude;
                if (currentSqrDistance < sqrDistance)
                {
                    differenceVector = currentNextWorldVertex - currentPrevWorldVertex;
                    sqrDistance = currentSqrDistance;
                }
            }

            differencesVectorsFromPrevToNext[i] = differenceVector;
        }

        return differencesVectorsFromPrevToNext;
    }

    private IEnumerator AnimateMeshChange(GameObject previousGO, GameObject nextGameObject, Material previousMat, Material nextMat)
    {
        nextGameObject.SetActive(true);

        previousMat.SetFloat("_MeshChangerSlider", 0);
        nextMat.SetFloat("_MeshChangerSlider", 1);
        previousMat.SetFloat("_Opacity", 1);
        nextMat.SetFloat("_Opacity", 0);

        animationTimer = -Time.deltaTime;
        while(animationTimer < animationDuration)
        {
            animationTimer += Time.deltaTime;
            previousMat.SetFloat("_MeshChangerSlider", animationTimer / animationDuration);
            nextMat.SetFloat("_MeshChangerSlider", (animationDuration - animationTimer) / animationDuration);
            previousMat.SetFloat("_Opacity", (animationDuration - animationTimer) / animationDuration);
            nextMat.SetFloat("_Opacity", animationTimer / animationDuration);
            yield return null;
        }

        previousMat.SetFloat("_MeshChangerSlider", 1);
        nextMat.SetFloat("_MeshChangerSlider", 0);
        previousMat.SetFloat("_Opacity", 0);
        nextMat.SetFloat("_Opacity", 1);

        previousGO.SetActive(false);
        nextGameObject.SetActive(true);

        differenceVectorsFromPrevToNextBuffer.Dispose();
        differenceVectorsFromNextToPrevBuffer.Dispose();
    }
}
