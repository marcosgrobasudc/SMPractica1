using UnityEngine;
using System.Collections.Generic;

public class AuctionManager
{
    public void StartAuction(

        Vector3 playerPosition,
        List<GuardScript> guards,
        Vector3 treasurePosition,
        Vector3 exitPosition)

    {
        // 1. Filtrar guardias disponibles (excluyendo coordinadores)
        var availableGuards = guards.FindAll(g => !g.IsCoordinator && g.IsAvailableForAssignment());

        // 2. Asignar perseguidor adicional (más cercano al jugador)
        var closestToPlayer = FindClosestGuard(availableGuards, playerPosition);
        if (closestToPlayer != null)
        {
            closestToPlayer.AssignRole("chase");
            closestToPlayer.SetTarget(playerPosition);
            availableGuards.Remove(closestToPlayer);
        }

        // 3. Asignar guardián del tesoro (más cercano al tesoro)
        var closestToTreasure = FindClosestGuard(availableGuards, treasurePosition);
        if (closestToTreasure != null)
        {
            closestToTreasure.AssignRole("treasure");
            closestToTreasure.SetTarget(treasurePosition);
        }
    }

    private GuardScript FindClosestGuard(List<GuardScript> guards, Vector3 targetPos)
    {
        GuardScript closestGuard = null;
        float minDistance = Mathf.Infinity;

        foreach (var guard in guards)
        {
            float dist = Vector3.Distance(guard.transform.position, targetPos);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestGuard = guard;
            }
        }
        return closestGuard;
    }
}