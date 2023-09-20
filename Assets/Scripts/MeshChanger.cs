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

        differenceVectorsFromPrevToNextBuffer = new ComputeBuffer(previousMesh.vertexCount, sizeof(float) * 3);
        differenceVectorsFromPrevToNextBuffer.SetData(differencesVectorsFromPrevToNext);
        previousObjectMaterial.SetBuffer("_DistancesFromOtherObjectVertices", differenceVectorsFromPrevToNextBuffer);

        Vector3[] differencesVectorsFromNextToPrev = new Vector3[nextMesh.vertexCount];

        for (int i = 0; i < nextMesh.vertexCount; i++)
        {
            Vector3 currentNextWorldVertex = nextLocalToWorld.MultiplyPoint3x4(nextMesh.vertices[i]);

            Vector3 currentPrevWorldVertex = previousLocalToWorld.MultiplyPoint3x4(nextMesh.vertices[0]);
            Vector3 differenceVector = currentPrevWorldVertex - currentNextWorldVertex;
            float sqrDistance = differenceVector.sqrMagnitude;

            for (int j = 1; j < previousMesh.vertexCount; j++)
            {
                currentPrevWorldVertex = previousLocalToWorld.MultiplyPoint3x4(previousMesh.vertices[j]);
                float currentSqrDistance = (currentPrevWorldVertex - currentNextWorldVertex).sqrMagnitude;
                if (currentSqrDistance < sqrDistance)
                {
                    differenceVector = currentPrevWorldVertex - currentNextWorldVertex;
                    sqrDistance = currentSqrDistance;
                }
            }

            differencesVectorsFromNextToPrev[i] = differenceVector;
        }

        differenceVectorsFromNextToPrevBuffer = new ComputeBuffer(nextMesh.vertexCount, sizeof(float) * 3);
        differenceVectorsFromNextToPrevBuffer.SetData(differencesVectorsFromNextToPrev);
        nextObjectMaterial.SetBuffer("_DistancesFromOtherObjectVertices", differenceVectorsFromNextToPrevBuffer);

        StartCoroutine(AnimateMeshChange(previousObject, nextObject, previousObjectMaterial, nextObjectMaterial));
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
