# Generic Mesh Morpher for Unity
Simple (nearest vertex) and generic (from-to meshes with any vertex count) mesh morpher for Unity
## What it does
For both previous and next mesh, for each vertex finds the nearest vertex on the other mesh, then lerps between the two.<br>
The previous mesh deforms becoming the next while the next starts deformed and returns to its original shape.<br><br>
The heavy computation of finding nearest vertices ($O(n^2)$ complexity where $n$ is vertexCount) can be done on CPU or GPU (via compute shader):
- CPU: Quite ok for really low poly meshes (both meshes have few vertices), REALLY slow otherwise, almost useless in any situation
- GPU: Way faster, better in any situation
## Purpose of the project
Excercise on ShaderLab/HLSL shaders and compute shaders in Unity
## Video demonstration


https://github.com/Baldi00/GenericMeshMorpherUnity/assets/46602744/8c67f97c-f82b-438e-bb4b-83888f7dc123

