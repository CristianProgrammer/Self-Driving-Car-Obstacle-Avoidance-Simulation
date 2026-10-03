using System.Collections.Generic;
using UnityEngine;

public class Path : MonoBehaviour
{
    public Color lineColor = Color.green;

    private readonly List<Transform> nodes = new List<Transform>();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = lineColor;

        Transform[] pathTransforms = GetComponentsInChildren<Transform>();
        nodes.Clear();

        foreach (Transform pathTransform in pathTransforms)
        {
            if (pathTransform != transform)
            {
                nodes.Add(pathTransform);
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 currentNode = nodes[i].position;
            Vector3 previousNode = i > 0
                ? nodes[i - 1].position
                : nodes[nodes.Count - 1].position;

            Gizmos.DrawLine(previousNode, currentNode);
            Gizmos.DrawWireSphere(currentNode, 0.9f);
        }
    }
}
