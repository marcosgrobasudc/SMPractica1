using System.Collections.Generic;
using UnityEngine;

public class GuardCoordinator : MonoBehaviour
{
    private static GuardCoordinator instance;
    private List<GuardScript> guards = new List<GuardScript>();
    private GuardScript coordinator;
    private Dictionary<GuardScript, (float playerDist, float treasureDist, float exitDist)> bids = new Dictionary<GuardScript, (float, float, float)>();
    private Vector3 lastKnownPlayerPosition;
    private Transform treasureLocation;
    private Transform exitLocation;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static GuardCoordinator Instance
    {
        get { return instance; }
    }

    public void RegisterGuard(GuardScript guard)
    {
        if (!guards.Contains(guard))
        {
            guards.Add(guard);
        }
    }

    public List<GuardScript> Guards
    {
        get { return guards; }
    }

    public void StartAuction(GuardScript caller, Vector3 playerPosition, Transform treasureLoc, Transform exitLoc)
    {
        if (guards.Count == 0) return;

        coordinator = caller;
        lastKnownPlayerPosition = playerPosition;
        treasureLocation = treasureLoc;
        exitLocation = exitLoc;
        bids.Clear();

        // Enviar mensaje ACL a los demás guardias para solicitar ofertas
        foreach (GuardScript guard in guards)
        {
            if (guard != coordinator)
            {
                guard.SendACLMessage(this.gameObject, "CALL_FOR_PROPOSAL", "Offer your distance", "auction");
            }
        }
    }

    public void ReceiveACLMessage(ACLMessage message)
    {
        if (message.Performative == "PROPOSE")
        {
            ProcessBid(message);
        }
    }

    private void ProcessBid(ACLMessage message)
    {
        GuardScript sender = message.Sender.GetComponent<GuardScript>();
        string[] values = message.Content.Split(',');
        float playerDist = float.Parse(values[0]);
        float treasureDist = float.Parse(values[1]);
        float exitDist = float.Parse(values[2]);

        bids[sender] = (playerDist, treasureDist, exitDist);

        if (bids.Count == guards.Count - 1)
        {
            AssignRoles();
        }
    }

    private void AssignRoles()
    {
        List<GuardScript> availableGuards = new List<GuardScript>(bids.Keys);
        GuardScript closestToPlayer = GetClosestGuard(availableGuards, g => bids[g].playerDist);
        GuardScript closestToTreasure = GetClosestGuard(availableGuards, g => bids[g].treasureDist);
        GuardScript closestToExit = GetClosestGuard(availableGuards, g => bids[g].exitDist);

        if (closestToPlayer != null)
        {
            closestToPlayer.SendACLMessage(coordinator.gameObject, "ACCEPT_PROPOSAL", "ChasePlayer", "auction");
            availableGuards.Remove(closestToPlayer);
        }
        if (closestToTreasure != null && closestToTreasure != closestToPlayer)
        {
            closestToTreasure.SendACLMessage(coordinator.gameObject, "ACCEPT_PROPOSAL", "CheckTreasure", "auction");
            availableGuards.Remove(closestToTreasure);
        }
        if (closestToExit != null && closestToExit != closestToPlayer && closestToExit != closestToTreasure)
        {
            closestToExit.SendACLMessage(coordinator.gameObject, "ACCEPT_PROPOSAL", "GuardExit", "auction");
        }
    }

    private GuardScript GetClosestGuard(List<GuardScript> guards, System.Func<GuardScript, float> distanceFunc)
    {
        GuardScript bestGuard = null;
        float minDist = float.MaxValue;

        foreach (GuardScript guard in guards)
        {
            float dist = distanceFunc(guard);
            if (dist < minDist)
            {
                minDist = dist;
                bestGuard = guard;
            }
        }
        return bestGuard;
    }
}
