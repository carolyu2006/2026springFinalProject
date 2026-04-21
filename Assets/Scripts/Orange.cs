using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
public class Orange : MonoBehaviour
{
    private Vector3 startPosition;
    private bool isHeld;
    private MeshRenderer[] renderers;
    private MeshCollider meshCollider;

    public bool IsHeld => isHeld;

    private void Start()
    {
        startPosition = transform.position;

        meshCollider = GetComponent<MeshCollider>();
        meshCollider.convex = true;
        meshCollider.isTrigger = true;

        renderers = GetComponentsInChildren<MeshRenderer>(true);
    }

    public void PickUp(Transform holder)
    {
        isHeld = true;
        transform.SetParent(holder);
        transform.localPosition = Vector3.zero;
        SetVisible(false);
    }

    public void Drop()
    {
        isHeld = false;
        transform.SetParent(null);
        transform.position = startPosition;
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (renderers != null)
        {
            foreach (MeshRenderer r in renderers)
                r.enabled = visible;
        }
        if (meshCollider != null)
            meshCollider.enabled = visible;
    }
}
