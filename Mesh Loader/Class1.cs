using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MeshLoader
{
    public static class MeshLoaderUtility
    {
        public static Mesh GetCustomMesh(string base64)
        {
            try
            {
                string obj = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var (vertices, uvs, faces) = ParseOBJ(obj);
                Mesh mesh = new Mesh
                {
                    vertices = vertices,
                    uv = uvs,
                    triangles = faces
                };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading mesh: {ex.Message}");
                return null;
            }
        }

        public static Material GetCustomTexture(string base64)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(imageBytes))
                {
                    texture.name = "CustomTexture";
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    Material material;
                    if (shader != null)
                    {
                        material = new Material(shader);
                        material.SetTexture("_BaseMap", texture);
                        material.SetColor("_BaseColor", Color.white);
                    }
                    else
                    {
                        Debug.LogWarning("URP/Lit shader not found, falling back to Standard");
                        shader = Shader.Find("Standard");
                        material = new Material(shader);
                        material.SetTexture("_MainTex", texture);
                        material.SetColor("_Color", Color.white);
                    }
                    return material;
                }
                else
                {
                    Debug.LogError("Failed to load texture from base64");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading texture: {ex.Message}");
                return null;
            }
        }

        private static (Vector3[], Vector2[], int[]) ParseOBJ(string obj)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var faces = new List<List<(int vertexIndex, int uvIndex)>>();

            foreach (var line in obj.Split('\n'))
            {
                var elements = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (elements.Length == 0) continue;

                switch (elements[0])
                {
                    case "v":
                        vertices.Add(new Vector3(
                            float.Parse(elements[1]),
                            float.Parse(elements[2]),
                            -float.Parse(elements[3]))); // Negate Z as per previous fix
                        break;
                    case "vt":
                        uvs.Add(new Vector2(
                            float.Parse(elements[1]),
                            float.Parse(elements[2])));
                        break;
                    case "f":
                        var face = new List<(int, int)>();
                        for (int i = 1; i < elements.Length; i++)
                        {
                            var indices = elements[i].Split('/');
                            int vertexIndex = int.Parse(indices[0]) - 1;
                            int uvIndex = -1;
                            if (indices.Length > 1 && !string.IsNullOrEmpty(indices[1]))
                            {
                                uvIndex = int.Parse(indices[1]) - 1;
                            }
                            face.Add((vertexIndex, uvIndex));
                        }
                        faces.Add(face);
                        break;
                }
            }

            var uniqueVertices = new Dictionary<string, int>();
            var finalVertices = new List<Vector3>();
            var finalUVs = new List<Vector2>();
            var triangles = new List<int>();

            foreach (var face in faces)
            {
                if (face.Count < 3) continue;
                for (int i = 0; i < face.Count - 2; i++)
                {
                    var v0 = face[0];
                    var v1 = face[i + 1];
                    var v2 = face[i + 2];

                    string key0 = v0.vertexIndex + "_" + v0.uvIndex;
                    string key1 = v1.vertexIndex + "_" + v1.uvIndex;
                    string key2 = v2.vertexIndex + "_" + v2.uvIndex;

                    if (!uniqueVertices.ContainsKey(key0))
                    {
                        uniqueVertices[key0] = finalVertices.Count;
                        finalVertices.Add(vertices[v0.vertexIndex]);
                        finalUVs.Add(v0.uvIndex == -1 ? Vector2.zero : uvs[v0.uvIndex]);
                    }
                    if (!uniqueVertices.ContainsKey(key1))
                    {
                        uniqueVertices[key1] = finalVertices.Count;
                        finalVertices.Add(vertices[v1.vertexIndex]);
                        finalUVs.Add(v1.uvIndex == -1 ? Vector2.zero : uvs[v1.uvIndex]);
                    }
                    if (!uniqueVertices.ContainsKey(key2))
                    {
                        uniqueVertices[key2] = finalVertices.Count;
                        finalVertices.Add(vertices[v2.vertexIndex]);
                        finalUVs.Add(v2.uvIndex == -1 ? Vector2.zero : uvs[v2.uvIndex]);
                    }

                    // Reverse winding order as per previous fix for normals
                    triangles.Add(uniqueVertices[key0]);
                    triangles.Add(uniqueVertices[key2]);
                    triangles.Add(uniqueVertices[key1]);
                }
            }

            return (finalVertices.ToArray(), finalUVs.ToArray(), triangles.ToArray());
        }
    }
}