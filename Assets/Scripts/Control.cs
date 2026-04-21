using System.Collections.Generic;
using UnityEngine;

public class Control : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private Player playerPrefab;
    [SerializeField] private int playerCount = 2;

    [SerializeField] private Bot botPrefab;
    [SerializeField] private int botCount = 10;
    [SerializeField] private float moveSpeed = 4f;

    [Header("Spawn Height")]
    [SerializeField] private float spawnY = 0f;

    [Header("Spawn Clearance")]
    [SerializeField] private Vector3 spawnHalfExtents = new Vector3(0.45f, 0.45f, 0.45f);
    [SerializeField] private int spawnClearanceAttempts = 30;

    private void Start()
    {
        Player[] spawnedPlayers = SpawnPlayers();
        SpawnBots();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayers(spawnedPlayers);
        }
    }

    private Player[] SpawnPlayers()
    {
        if (playerPrefab == null) return new Player[0];

        Player[] players = new Player[playerCount];

        for (int i = 0; i < playerCount; i++)
        {
            Player spawnedPlayer = Instantiate(
                playerPrefab,
                GetRandomSpawnPosition(),
                Quaternion.identity);
            Player.ControlScheme scheme = GetControlScheme(i);
            spawnedPlayer.Initialize(i, scheme, moveSpeed);
            players[i] = spawnedPlayer;
        }

        return players;
    }

    private Player.ControlScheme GetControlScheme(int playerIndex)
    {
        var cfg = GameConfig.Instance;
        if (cfg != null
            && playerIndex >= 0 && playerIndex < cfg.PlayerSchemeAssigned.Length
            && cfg.PlayerSchemeAssigned[playerIndex])
        {
            return cfg.PlayerSchemes[playerIndex];
        }

        ControlMode mode = cfg?.Mode ?? ControlMode.Keyboard;
        switch (mode)
        {
            case ControlMode.Phone:
                return Player.ControlScheme.Phone;
            case ControlMode.ESP32:
                return Player.ControlScheme.ESP32;
            default: // Keyboard
                return playerIndex == 0 ? Player.ControlScheme.WASD : Player.ControlScheme.ArrowKeys;
        }
    }

    private void SpawnBots()
    {
        if (botPrefab == null) return;

        List<Vector3> placed = new List<Vector3>(botCount);
        for (int i = 0; i < botCount; i++)
        {
            Vector3 pos = GetSpreadSpawnPosition(placed);
            Bot spawnedBot = Instantiate(botPrefab, pos, Quaternion.identity);
            spawnedBot.Initialize(moveSpeed);
            placed.Add(pos);
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector3 pos = ScreenBoundsUtility.GetRandomPointInsideVisibleWorld(spawnY);
        for (int i = 0; i < spawnClearanceAttempts && !IsSpawnPositionClear(pos); i++)
        {
            pos = ScreenBoundsUtility.GetRandomPointInsideVisibleWorld(spawnY);
        }
        return pos;
    }

    /// <summary>
    /// Best-candidate sampling: of several random points, pick the one whose
    /// nearest already-placed neighbor is farthest away. Spreads bots evenly.
    /// Prefers positions clear of obstacles; falls back to any candidate if none clear.
    /// </summary>
    private Vector3 GetSpreadSpawnPosition(List<Vector3> existing)
    {
        const int candidatesPerPick = 8;
        Vector3 best = GetRandomSpawnPosition();
        float bestScore = MinSqDistanceToExisting(best, existing);
        bool bestClear = IsSpawnPositionClear(best);

        for (int i = 1; i < candidatesPerPick; i++)
        {
            Vector3 candidate = GetRandomSpawnPosition();
            float score = MinSqDistanceToExisting(candidate, existing);
            bool candidateClear = IsSpawnPositionClear(candidate);

            // Clear candidates always beat blocked ones; otherwise compare by spread.
            if ((candidateClear && !bestClear) ||
                (candidateClear == bestClear && score > bestScore))
            {
                best = candidate;
                bestScore = score;
                bestClear = candidateClear;
            }
        }
        return best;
    }

    private bool IsSpawnPositionClear(Vector3 pos)
    {
        Collider[] hits = Physics.OverlapBox(pos, spawnHalfExtents, Quaternion.identity);
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;
            if (hit.GetComponentInParent<Player>() != null) continue;
            if (hit.GetComponentInParent<Bot>() != null) continue;
            return false;
        }
        return true;
    }

    private static float MinSqDistanceToExisting(Vector3 point, List<Vector3> existing)
    {
        if (existing.Count == 0) return float.PositiveInfinity;
        float min = float.PositiveInfinity;
        for (int i = 0; i < existing.Count; i++)
        {
            float d = (point - existing[i]).sqrMagnitude;
            if (d < min) min = d;
        }
        return min;
    }
}
