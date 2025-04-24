using System.Collections.Generic;
using UnityEngine;

public class GuardCoordinator : MonoBehaviour
{
    private static GuardCoordinator _instance;
    private List<GuardScript> _guards = new List<GuardScript>();
    private GuardScript coordinator;
    private Dictionary<GuardScript, (float playerDist, float treasureDist, float exitDist)> bids = new Dictionary<GuardScript, (float, float, float)>();
    private Vector3 lastKnownPlayerPosition;
    private Transform treasureLocation;
    private Transform exitLocation;
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static GuardCoordinator Instance => _instance;
    public List<GuardScript> Guards => _guards;

    public void RegisterGuard(GuardScript newGuard)
    {
        if (!_guards.Contains(newGuard))
        {
            _guards.Add(newGuard);
        }
    }

    public void StartAuction(GuardScript caller, Vector3 playerPosition, Transform treasureLoc, Transform exitLoc)
    {
        if (Guards.Count == 0) return;

        SetCurrentCoordinator(caller); // Usar el método nuevo
        lastKnownPlayerPosition = playerPosition;
        treasureLocation = treasureLoc;
        exitLocation = exitLoc;
        bids.Clear();

        // Hacemos que el coordinador también persiga
        CurrentCoordinator.AssignRole("chase");
        CurrentCoordinator.SetTarget(playerPosition);

        // Subasta: Perdimos distancias a cada guardia
        string payload = $"{playerPosition.x},{playerPosition.y},{playerPosition.z}";
        
        foreach (GuardScript guard in Guards)
        {
            if (guard != CurrentCoordinator)
            {
                guard.SendACLMessage(
                    receiver: this.gameObject,
                    performative: "CALL_FOR_PROPOSAL",
                    content: payload,
                    protocol: "auction"
                );
            }
        }
    }

    public void ReceiveACLMessage(ACLMessage message)
    {
        if (message.Performative == "PROPOSE")
        {
            // Extraemos guardia y distancias
            GuardScript sender = message.Sender.GetComponent<GuardScript>();
            var vals = message.Content.Split(',');

            float pDist = float.Parse(vals[0]);
            float tDist = float.Parse(vals[1]);
            float eDist = float.Parse(vals[2]);

            bids[sender] = (pDist, tDist, eDist);

            // Si ya tenemos todos los bid, asignamos roles
            if (bids.Count == Guards.Count - 1)
            {
                AssignRoles();
            }
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

        if (bids.Count == Guards.Count - 1)
        {
            AssignRoles();
        }
    }

    private void AssignRoles()
    {
        var competitors = new List<GuardScript>(bids.Keys);
        
        // Buscamos el mejor perseguidor
        var bestChase = GetClosestGuard(competitors, g => bids[g].playerDist);
        bestChase?.SendACLMessage(
            receiver: this.gameObject,
            performative: "ASSIGN_ROLE",
            content: "chase",
            protocol: "auction"
        );
        competitors.Remove(bestChase);
        

        // Buscamos el mejor guardián del tesoro
        var bestGuard = GetClosestGuard(competitors, g => bids[g].treasureDist);
        bestGuard?.SendACLMessage(
            receiver: this.gameObject,
            performative: "ASSIGN_ROLE",
            content: "treasure",
            protocol: "auction"
        );
        competitors.Remove(bestGuard);

        // El resto que se ponga a patrullar vagos
        foreach (var g in competitors)
        {
            g.SendACLMessage(
                receiver: this.gameObject,
                performative: "ASSIGN_ROLE",
                content: "patrol",
                protocol: "auction"
            );
        }
    }

    public GuardScript CurrentCoordinator { get; private set; }

    public bool HasActiveCoordinator()
    {
        return CurrentCoordinator != null;
    }

    public void SetCurrentCoordinator(GuardScript coordinator)
    {
        this.CurrentCoordinator = coordinator;
        this.coordinator = coordinator; // Mantener compatibilidad con tu variable existente
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
