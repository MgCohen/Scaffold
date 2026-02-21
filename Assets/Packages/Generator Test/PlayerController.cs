using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public float speed = 20;

    private NetworkTransform _transform;

    private void Start()
    {
        _transform = GetComponent<NetworkTransform>();
    }
    private void Update()
    {
        if (!IsOwner) return;
        var movement = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        _transform.transform.position += movement * speed * Time.deltaTime;
    }

}