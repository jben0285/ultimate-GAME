using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class VertexWobble : MonoBehaviour
{
    TMP_Text textMesh;
    Mesh mesh;
    Vector3[] vertices;
    Color32[] colors;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        textMesh = GetComponent<TMP_Text>();
    }

    // Update is called once per frame
    void Update()
    {
        textMesh.ForceMeshUpdate();

        TMP_TextInfo textInfo = textMesh.textInfo;
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            mesh = textInfo.meshInfo[i].mesh;
            vertices = textInfo.meshInfo[i].vertices;
            colors = textInfo.meshInfo[i].colors32;

            for (int j = 0; j < textInfo.characterCount; j++)
            {
                if (!textInfo.characterInfo[j].isVisible) continue;

                int vertexIndex = textInfo.characterInfo[j].vertexIndex;

                Vector3 offset = (Vector3)Wobble(Time.time + j);

                // Apply wobble
                vertices[vertexIndex] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;

                // Calculate a black-white pulsing value between 0 and 1
                float t = Mathf.PingPong(Time.time + vertices[vertexIndex].x * 0.005f, 1f);
                Color32 bwColor = Color.Lerp(Color.black, Color.white, t);

                // Apply to all 4 vertices of the character
                colors[vertexIndex] = bwColor;
                colors[vertexIndex + 1] = bwColor;
                colors[vertexIndex + 2] = bwColor;
                colors[vertexIndex + 3] = bwColor;
            }

            mesh.vertices = vertices;
            mesh.colors32 = colors;
            textMesh.UpdateGeometry(mesh, i);
        }
    }

    Vector2 Wobble(float time)
    {
        return new Vector2(Mathf.Sin(time * 6.3f) * 1f, Mathf.Cos(time * 6.3f) * 1f);
    }
}
