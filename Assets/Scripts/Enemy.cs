using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints = new();
    [SerializeField] private float sightRange = 12f;
    [SerializeField] private float fieldOfViewAngle = 60f;
    [SerializeField] private float hearingRange = 5f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform noiseSource;

    private NavMeshAgent agent;
    private BehaviourTree tree;
    private Transform player;
    private Vector3 lastKnownPosition;
    private bool playerSeen;
    private bool isAttacking;
    private bool isSearching;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Initialize the behavior tree
        tree = new BehaviourTree("Enemy");

        // Create the enemy's logic using a PrioritySelector
        PrioritySelector actions = new PrioritySelector("Enemy Logic");

        // Sequence for attacking the player
        Sequence attackPlayerSeq = new Sequence("AttackPlayer", 300);
        attackPlayerSeq.AddChild(new Leaf("isPlayerInAttackRange?", new Condition(IsPlayerInAttackRange)));
        attackPlayerSeq.AddChild(new Leaf("AttackPlayer", new ActionStrategy(AttackPlayer)));
        // attackPlayerSeq.AddChild(new Leaf("PlayerOutOfAttackRange?", new Condition(PlayerOutOfAttackRange))); // Reset attack condition

        // Sequence for chasing the player
        Sequence chasePlayerSeq = new Sequence("ChasePlayer", 200);
        chasePlayerSeq.AddChild(new Leaf("isPlayerInSight?", new Condition(LookForPlayer)));
        chasePlayerSeq.AddChild(new Leaf("ChasePlayer", new ChaseTarget(transform, agent,() => player)));
        //chasePlayerSeq.AddChild(new Leaf("PlayerOutOfSight?", new Condition(PlayerOutOfSight))); // Reset chase condition

        // Sequence for searching the last known position
        Sequence searchPlayerSeq = new Sequence("SearchPlayer", 100);
        searchPlayerSeq.AddChild(new Leaf("isPlayerHeard?", new Condition(HearPlayer)));
        searchPlayerSeq.AddChild(new Leaf("SearchLastKnownPosition", new ActionStrategy(SearchLastKnownPosition)));
        //searchPlayerSeq.AddChild(new Leaf("NoMoreNoise?", new Condition(NoMoreNoise))); // Reset hearing condition

        // Add patrol behavior as a fallback
        actions.AddChild(new Leaf("Patrol", new PatrolStrategy(transform, agent, waypoints)));

        // Add all sequences to the priority selector
        actions.AddChild(attackPlayerSeq);
        actions.AddChild(chasePlayerSeq);
        actions.AddChild(searchPlayerSeq);

        // Add the selector to the tree
        tree.AddChild(actions);
    }

    private void Update()
    {
        tree.Process();
    }

    // Look for the player using raycast and field of view
    private bool LookForPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sightRange, playerLayer);
        foreach (Collider hitCollider in hits)
        {
            Transform potentialPlayer = hitCollider.transform;
            Vector3 directionToPlayer = potentialPlayer.position - transform.position;
            float distanceToPlayer = directionToPlayer.magnitude;

            // Check if the player is within the field of view
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            if (angleToPlayer < fieldOfViewAngle / 2f)
            {
                // Perform a raycast to ensure there are no obstacles blocking the view
                if (Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit raycastHit, sightRange))
                {
                    if (raycastHit.transform == potentialPlayer)
                    {
                        player = potentialPlayer; // Set the player reference dynamically
                        lastKnownPosition = player.position; // Update the last known position
                        playerSeen = true; // Mark the player as seen
                        return true; // Player is in sight
                    }
                }
            }
        }
        playerSeen = false; // Reset if player not in sight
        return false; // Player is not in sight
    }

    // New condition: Check if the player is out of sight
    private bool PlayerOutOfSight()
    {
        // Consider the player out of sight if LookForPlayer returns false
        return !LookForPlayer();
    }

    // Check if the player is within attack range
    private bool IsPlayerInAttackRange()
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= attackRange;
    }

    // New condition: Check if the player is out of attack range
    private bool PlayerOutOfAttackRange()
    {
        // Consider the player out of attack range if IsPlayerInAttackRange returns false
        return !IsPlayerInAttackRange();
    }

    // Attack the player (simplified as a log statement for now)
    private void AttackPlayer()
    {
        if (!isAttacking)
        {
            isAttacking = true; // Mark as attacking
           
            // After attacking, reset the state
            Invoke(nameof(ResetAttack), 1f); // Example delay to reset, adjust as needed
        }
    }

    // Reset the attack state
    private void ResetAttack()
    {
        isAttacking = false;
    }

    // Hear the player (using noise source as the player's position or noise location)
    private bool HearPlayer()
    {
        if (noiseSource == null) return false;
        float distanceToNoise = Vector3.Distance(transform.position, noiseSource.position);
        if (distanceToNoise <= hearingRange)
        {
            lastKnownPosition = noiseSource.position; // Update the last known position to the noise source
            return true;
        }
        return false;
    }

    // New condition: Check if there is no more noise to investigate
    private bool NoMoreNoise()
    {
        // If there's no noise source or the enemy has already investigated it, return true
        if (noiseSource == null) return true;

        // If the noise source is outside the hearing range, consider it "no more noise"
        return Vector3.Distance(transform.position, noiseSource.position) > hearingRange;
    }

    // Search the last known position of the player
    private void SearchLastKnownPosition()
    {
        if (!isSearching)
        {
            isSearching = true;
            if (lastKnownPosition == Vector3.zero) return; // No known position to search
            agent.SetDestination(lastKnownPosition);
            if (Vector3.Distance(transform.position, lastKnownPosition) < 1f)
            {
                // After searching, reset the state
                Invoke(nameof(ResetSearch), 2f); // Example delay to reset, adjust as needed
            }
        }
    }

    // Reset the search state
    private void ResetSearch()
    {
        isSearching = false;
    }
}
