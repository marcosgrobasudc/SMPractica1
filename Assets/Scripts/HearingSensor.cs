using UnityEngine;

public class HearingSensor : MonoBehaviour
{
    public float hearingRange = 5f;

    private Transform player;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    public bool CanHearPlayer()
    {
        return Vector3.Distance(transform.position, player.position) < hearingRange;
    }
}