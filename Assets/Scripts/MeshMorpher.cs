using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshMorpher : MonoBehaviour
{
    public enum Mode
    {
        GPU,
        CPU_VERY_SLOW,
    }

    [SerializeField]
    private Mode executionMode;
    [SerializeField]
    private float animationDuration = 1;
    [SerializeField]
    private List<GameObject> objects;
    [SerializeField]
    private ComputeShader findNearestVerticesCompute;

    private int currentObjectIndex = 0;
    private float animationTimer;

    private GameObject previousObject;
    private GameObject nextObject;

    private ComputeBuffer differenceVectorsFromPrevToNextBuffer;
    private ComputeBuffer differenceVectorsFromNextToPrevBuffer;

    void Awake()
    {
        animationTimer = animationDuration;
    }

    void Update()
    {
        if (IsAnimatingMeshes())
            return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            PreviousMesh();
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            NextMesh();
    }

    /// <summary>
    /// Passes to previous mesh
    /// </summary>
    private void PreviousMesh()
    {
        previousObject = objects[currentObjectIndex];
        currentObjectIndex = (currentObjectIndex - 1 + objects.Count) % objects.Count;
        nextObject = objects[currentObjectIndex];

        SetupAndStartMeshAnimation();
    }

    /// <summary>
    /// Passes to next mesh
    /// </summary>
    private void NextMesh()
    {
        previousObject = objects[currentObjectIndex];
        currentObjectIndex = (currentObjectIndex + 1) % objects.Count;
        nextObject = objects[currentObjectIndex];

        SetupAndStartMeshAnimation();
    }

    /// <summary>
    /// Finds the nearest vertices from previous to next mesh, sets up materials parameters and starts the animation
    /// </summary>
    private void SetupAndStartMeshAnimation()
    {
        Mesh previousMesh = previousObject.GetComponent<MeshFilter>().mesh;
        Mesh nextMesh = nextObject.GetComponent<MeshFilter>().mesh;
        Material previousObjectMaterial = previousObject.GetComponent<MeshRenderer>().material;
        Material nextObjectMaterial = nextObject.GetComponent<MeshRenderer>().material;

        Matrix4x4 previousLocalToWorld = previousObject.transform.localToWorldMatrix;
        Matrix4x4 nextLocalToWorld = nextObject.transform.localToWorldMatrix;

        FindNearestVertices(previousMesh, nextMesh, ref previousLocalToWorld, ref nextLocalToWorld);

        previousObjectMaterial.SetBuffer("_DistancesFromOtherObjectVertices", differenceVectorsFromPrevToNextBuffer);
        nextObjectMaterial.SetBuffer("_DistancesFromOtherObjectVertices", differenceVectorsFromNextToPrevBuffer);

        StartCoroutine(
            AnimateMeshChangeCoroutine(previousObject, nextObject, previousObjectMaterial, nextObjectMaterial));
    }

    /// <summary>
    /// For every vertex in previous mesh finds the difference vectors to the nearest vertex in next mesh.
    /// Transformation matrices are needed because the distances and difference vectors are computed in world space.
    /// Makes computations on CPU or GPU depending on the given settings
    /// </summary>
    /// <param name="previousMesh">The starting mesh of the animation</param>
    /// <param name="nextMesh">The end mesh of the animation</param>
    /// <param name="previousLocalToWorld">Transformation matrix from local to world for previous mesh</param>
    /// <param name="nextLocalToWorld">Transformation matrix from local to world for next mesh</param>
    private void FindNearestVertices(Mesh previousMesh, Mesh nextMesh,
        ref Matrix4x4 previousLocalToWorld, ref Matrix4x4 nextLocalToWorld)
    {
        differenceVectorsFromPrevToNextBuffer = new ComputeBuffer(previousMesh.vertexCount, sizeof(float) * 3);
        differenceVectorsFromNextToPrevBuffer = new ComputeBuffer(nextMesh.vertexCount, sizeof(float) * 3);

        if (executionMode == Mode.CPU_VERY_SLOW)
        {
            differenceVectorsFromPrevToNextBuffer.SetData(
                FindNearestVerticesCpu(previousMesh, nextMesh, ref previousLocalToWorld, ref nextLocalToWorld));
            differenceVectorsFromNextToPrevBuffer.SetData(
                FindNearestVerticesCpu(nextMesh, previousMesh, ref nextLocalToWorld, ref previousLocalToWorld));
        }
        else if (executionMode == Mode.GPU)
        {
            FindNearestVerticesGpu(previousMesh, nextMesh,
                ref previousLocalToWorld, ref nextLocalToWorld, differenceVectorsFromPrevToNextBuffer);
            FindNearestVerticesGpu(nextMesh, previousMesh,
                ref nextLocalToWorld, ref previousLocalToWorld, differenceVectorsFromNextToPrevBuffer);
        }
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
    private Vector3[] FindNearestVerticesCpu(Mesh previousMesh, Mesh nextMesh,
        ref Matrix4x4 previousLocalToWorld, ref Matrix4x4 nextLocalToWorld)
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

    /// <summary>
    /// For every vertex in previous mesh finds the difference vectors to the nearest vertex in next mesh using a compute shader.
    /// Transformation matrices are needed because the distances and difference vectors are computed in world space
    /// </summary>
    /// <param name="previousMesh">The starting mesh of the animation</param>
    /// <param name="nextMesh">The end mesh of the animation</param>
    /// <param name="previousLocalToWorld">Transformation matrix from local to world for previous mesh</param>
    /// <param name="nextLocalToWorld">Transformation matrix from local to world for next mesh</param>
    /// <param name="differenceFromPrevToNext">The difference vectors to the nearest next mesh vertex for each previous mesh vertex</param>
    private void FindNearestVerticesGpu(Mesh previousMesh, Mesh nextMesh, ref Matrix4x4 previousLocalToWorld,
        ref Matrix4x4 nextLocalToWorld, ComputeBuffer differenceFromPrevToNext)
    {
        ComputeBuffer previousVerticesBuffer = new ComputeBuffer(previousMesh.vertexCount, sizeof(float) * 3);
        ComputeBuffer nextVerticesBuffer = new ComputeBuffer(nextMesh.vertexCount, sizeof(float) * 3);
        previousVerticesBuffer.SetData(previousMesh.vertices);
        nextVerticesBuffer.SetData(nextMesh.vertices);

        findNearestVerticesCompute.SetBuffer(0, "_PreviousMeshVertices", previousVerticesBuffer);
        findNearestVerticesCompute.SetBuffer(0, "_NextMeshVertices", nextVerticesBuffer);
        findNearestVerticesCompute.SetMatrix("_PreviousLocalToWorldMatrix", previousLocalToWorld);
        findNearestVerticesCompute.SetMatrix("_NextLocalToWorldMatrix", nextLocalToWorld);
        findNearestVerticesCompute.SetInt("_NextMeshVerticesCount", nextMesh.vertexCount);
        findNearestVerticesCompute.SetBuffer(0, "_DifferenceFromPrevToNext", differenceFromPrevToNext);

        findNearestVerticesCompute.Dispatch(0, Mathf.CeilToInt(previousMesh.vertexCount / 64f), 1, 1);

        previousVerticesBuffer.Dispose();
        nextVerticesBuffer.Dispose();
    }

    /// <summary>
    /// Animates the previous mesh and next mesh materials
    /// </summary>
    /// <param name="previousGameObject">The game object holding the previous mesh</param>
    /// <param name="nextGameObject">The game object holding the next mesh</param>
    /// <param name="previousMaterial">The previous mesh material</param>
    /// <param name="nextMaterial">The next mesh material</param>
    private IEnumerator AnimateMeshChangeCoroutine(GameObject previousGameObject, GameObject nextGameObject,
        Material previousMaterial, Material nextMaterial)
    {
        nextGameObject.SetActive(true);

        previousMaterial.SetFloat("_MeshMorpherSlider", 0);
        nextMaterial.SetFloat("_MeshMorpherSlider", 1);
        previousMaterial.SetFloat("_Opacity", 1);
        nextMaterial.SetFloat("_Opacity", 0);

        animationTimer = -Time.deltaTime;
        while (animationTimer < animationDuration)
        {
            animationTimer += Time.deltaTime;
            previousMaterial.SetFloat("_MeshMorpherSlider", animationTimer / animationDuration);
            nextMaterial.SetFloat("_MeshMorpherSlider", (animationDuration - animationTimer) / animationDuration);
            previousMaterial.SetFloat("_Opacity", (animationDuration - animationTimer) / animationDuration);
            nextMaterial.SetFloat("_Opacity", animationTimer / animationDuration);
            yield return null;
        }

        previousMaterial.SetFloat("_MeshMorpherSlider", 1);
        nextMaterial.SetFloat("_MeshMorpherSlider", 0);
        previousMaterial.SetFloat("_Opacity", 0);
        nextMaterial.SetFloat("_Opacity", 1);

        previousGameObject.SetActive(false);
        nextGameObject.SetActive(true);

        differenceVectorsFromPrevToNextBuffer.Dispose();
        differenceVectorsFromNextToPrevBuffer.Dispose();
    }

    /// <summary>
    /// Checks if a mesh animation is occurring
    /// </summary>
    /// <returns>True if a mesh animation is occurring, false otherwise</returns>
    private bool IsAnimatingMeshes()
    {
        return animationTimer < animationDuration;
    }
}
