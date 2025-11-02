using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace CrowbaneArena
{
    public static class ZoneManager
    {
        // Track players currently in arena by SteamID
        private static readonly HashSet<ulong> PlayersInArena = new();

        // Track player entities by SteamID
        private static readonly Dictionary<ulong, Entity> PlayerEntities = new();
        private static readonly Dictionary<Entity, float3> LastPlayerPositions = new();
        public static float3 ArenaCenter { get; set; } = new float3(0, 0, 0);
        public static float ArenaRadius { get; set; } = 50f;
        public static float3 EntryPoint { get; set; } = new float3(0, 0, 0);
        public static float EntryRadius { get; set; } = 10f;
        public static float3 ExitPoint { get; set; } = new float3(0, 0, 0);
        public static float ExitRadius { get; set; } = 10f;
        public static float3 SpawnPoint { get; set; }   = new float3(0, 0, 0);

        // Helper property to get EntityManager
        private static EntityManager EM => VRisingCore.EntityManager;

        /// <summary>
        /// Gets the user entity from a character entity
        /// </summary>
        public static Entity GetUserFromCharacter(Entity characterEntity)
        {
            try
            {
                if (characterEntity == Entity.Null || !EM.HasComponent<PlayerCharacter>(characterEntity))
                    return Entity.Null;

                var playerCharacter = EM.GetComponentData<PlayerCharacter>(characterEntity);
                return playerCharacter.UserEntity;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error in GetUserFromCharacter: {ex.Message}");
                return Entity.Null;
            }
        }

        public static void SetArenaZone(float3 center, float radius)
        {
                ArenaCenter = center;
                ArenaRadius = radius;
                Plugin.Logger?.LogInfo($"Arena zone set: Center {center}, Radius {radius}");

                // Auto-set spawn point to arena center as requested
                SpawnPoint = ArenaCenter;
                Plugin.Logger?.LogInfo($"Spawn point automatically set to match arena center: {SpawnPoint}");
        }

        public static void SetEntryPoint(float3 point, float radius)
        {
            EntryPoint = point;
            EntryRadius = radius;
            Plugin.Logger?.LogInfo($"Entry point set: {point}, Radius {radius}");
        }

        public static void SetExitPoint(float3 point, float radius)
        {
            ExitPoint = point;
            ExitRadius = radius;
            Plugin.Logger?.LogInfo($"Exit point set: {point}, Radius {radius}");
        }

        public static void SetSpawnPoint(float3 point)
        {
            SpawnPoint = point;
            Plugin.Logger?.LogInfo($"Spawn point set: {point}");
        }

        public static void CheckPlayerZones(Entity playerEntity, float3 position)
        {
            if (!IsValidEntity(playerEntity))
                return;

            LastPlayerPositions[playerEntity] = position;

            // Disabled automatic entry/exit - only manual commands work
            // bool inEntry = IsInZone(position, EntryPoint, EntryRadius);
            // bool inExit = IsInZone(position, ExitPoint, ExitRadius);

            // bool wasInArena = IsPlayerInArena(playerEntity);

            // Auto entry/exit disabled - use .arena enter/.arena exit commands only
        }

        private static bool IsInZone(float3 position, float3 center, float radius)
        {
            return math.distance(position, center) <= radius;
        }

        private static bool IsValidEntity(Entity entity)
        {
            if (entity.Equals(Entity.Null))
            {
                Plugin.Logger?.LogWarning("Entity is null");
                return false;
            }

            try
            {
                var em = VRisingCore.EntityManager;

                // First check if entity exists
                if (!em.Exists(entity))
                {
                    Plugin.Logger?.LogWarning($"Entity {entity} does not exist");
                    return false;
                }

                // Removed debug logging to prevent spam

                // For arena operations, we need entities that have a Translation component at minimum
                if (!em.HasComponent<Translation>(entity))
                {
                    Plugin.Logger?.LogWarning($"No Translation component found on entity {entity}");
                    return false;
                }

                // If we have a PlayerCharacter, do additional validation
                if (em.HasComponent<PlayerCharacter>(entity))
                {
                    var playerCharacter = em.GetComponentData<PlayerCharacter>(entity);
                    var userEntity = playerCharacter.UserEntity;

                    if (userEntity.Equals(Entity.Null) || !em.Exists(userEntity))
                    {
                        Plugin.Logger?.LogWarning($"Invalid or non-existent user entity for player {entity}");
                        return false;
                    }

                    if (!em.HasComponent<User>(userEntity))
                    {
                        Plugin.Logger?.LogWarning($"User component missing for user entity {userEntity}");
                        return false;
                    }

                    var userData = em.GetComponentData<User>(userEntity);
                    if (!userData.IsConnected)
                    {
                        Plugin.Logger?.LogWarning($"User {userEntity} is not connected");
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnterArena(Entity playerEntity)
        {
            try
            {
                if (playerEntity == Entity.Null)
                {
                    Plugin.Logger?.LogWarning("Attempted to enter arena with null entity");
                    return;
                }

                // Get user entity and Steam ID
                var userEntity = GetUserFromCharacter(playerEntity);
                if (userEntity == Entity.Null)
                {
                    Plugin.Logger?.LogWarning($"Could not find user entity for player entity {playerEntity}");
                    return;
                }

                var user = EM.GetComponentData<User>(userEntity);
                var steamId = user.PlatformId;
                var playerName = user.CharacterName.ToString();

                Plugin.Logger?.LogInfo(
                    $"Attempting to enter arena for {playerName} (SteamID: {steamId}, Entity: {playerEntity})");

                if (PlayersInArena.Contains(steamId))
                {
                    Plugin.Logger?.LogInfo($"Player {playerName} is already in the arena");
                    return;
                }

                // Add to tracking collections
                PlayersInArena.Add(steamId);
                PlayerEntities[steamId] = playerEntity;

                // Apply arena features
                Plugin.Logger?.LogInfo($"Teleporting {playerName} to spawn...");
                TeleportToSpawn(playerEntity);

                Plugin.Logger?.LogInfo($"Unlocking VBloods for {playerName}...");
                VBloodManager.UnlockAllVBloods(playerEntity);

                // ABILITY GRANTING REMOVED AS REQUESTED

                Plugin.Logger?.LogInfo($"Player {playerName} (SteamID: {steamId}) successfully entered arena");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error in EnterArena: {ex.Message}");
                Plugin.Logger?.LogError(ex.StackTrace);
                throw;
            }
        }

        public static void TeleportToSpawn(Entity playerEntity)
        {
            if (!IsValidEntity(playerEntity))
                return;

            // Use the properly detected server EntityManager (ICB.core pattern)
            var em = VRisingCore.EntityManager;

            if (em.HasComponent<Translation>(playerEntity))
            {
                var translation = em.GetComponentData<Translation>(playerEntity);
                translation.Value = SpawnPoint;
                em.SetComponentData(playerEntity, translation);
                Plugin.Logger?.LogInfo($"Teleported {playerEntity} to spawn point: {SpawnPoint}");
            }
        }

        /// <summary>
        /// Manually adds a player to the arena, teleporting them to the spawn point
        /// and granting them arena abilities and VBlood unlocks.
        /// </summary>
        /// <param name="playerEntity">The entity of the player to add to the arena</param>
        public static void ManualEnterArena(Entity playerEntity)
        {
            ulong steamId = 0;
            string playerName = "Unknown";

            try
            {
                Plugin.Logger?.LogInfo($"[ManualEnterArena] Starting arena entry for entity: {playerEntity}");

                if (playerEntity == Entity.Null)
                {
                    Plugin.Logger?.LogWarning("[ManualEnterArena] Attempted to enter arena with null entity");
                    return;
                }

                // Verify entity is valid and has required components
                if (!IsValidEntity(playerEntity))
                {
                    Plugin.Logger?.LogError($"[ManualEnterArena] Invalid player entity: {playerEntity}");
                    return;
                }

                // Get user entity and Steam ID with additional validation
                var userEntity = GetUserFromCharacter(playerEntity);
                if (userEntity == Entity.Null)
                {
                    Plugin.Logger?.LogError(
                        $"[ManualEnterArena] Could not find user entity for player entity {playerEntity}");
                    return;
                }

                // Get user data with error handling
                if (!EM.HasComponent<User>(userEntity))
                {
                    Plugin.Logger?.LogError($"[ManualEnterArena] User entity {userEntity} is missing User component");
                    return;
                }

                var user = EM.GetComponentData<User>(userEntity);
                steamId = user.PlatformId;
                playerName = user.CharacterName.ToString();

                Plugin.Logger?.LogInfo(
                    $"[ManualEnterArena] Processing arena entry for {playerName} (SteamID: {steamId}, Entity: {playerEntity}, UserEntity: {userEntity})");

                // Check if already in arena
                if (PlayersInArena.Contains(steamId))
                {
                    Plugin.Logger?.LogWarning($"[ManualEnterArena] Player {playerName} is already in the arena");
                    return;
                }

                // Add to tracking collections first
                PlayersInArena.Add(steamId);
                PlayerEntities[steamId] = playerEntity;

                Plugin.Logger?.LogInfo($"[ManualEnterArena] Added {playerName} to arena tracking");

                // 1. First teleport the player
                try
                {
                    Plugin.Logger?.LogInfo($"[ManualEnterArena] Attempting to teleport {playerName} to spawn...");
                    TeleportToSpawn(playerEntity);
                    Plugin.Logger?.LogInfo($"[ManualEnterArena] Successfully teleported {playerName} to spawn");
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[ManualEnterArena] Error during teleport for {playerName}: {ex.Message}");
                    Plugin.Logger?.LogError(ex.StackTrace);
                    // Continue with other operations even if teleport fails
                }

                // 2. Unlock VBloods
                try
                {
                    Plugin.Logger?.LogInfo($"[ManualEnterArena] Unlocking VBloods for {playerName}...");
                    VBloodManager.UnlockAllVBloods(playerEntity);
                    Plugin.Logger?.LogInfo($"[ManualEnterArena] Successfully unlocked VBloods for {playerName}");
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError(
                        $"[ManualEnterArena] Error unlocking VBloods for {playerName}: {ex.Message}");
                    Plugin.Logger?.LogError(ex.StackTrace);
                    // Continue with other operations even if VBlood unlock fails
                }

                // ABILITY GRANTING REMOVED AS REQUESTED

                Plugin.Logger?.LogInfo(
                    $"[ManualEnterArena] Player {playerName} (SteamID: {steamId}) successfully processed arena entry");

                // 4. Spawn visual effects
                try
                {
                    Plugin.Logger?.LogInfo($"[ManualEnterArena] Spawning arena entry effects for {playerName}...");
                    SpawnArenaEffects(playerEntity);
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError(
                        $"[ManualEnterArena] Error spawning effects for {playerName}: {ex.Message}");
                    // Non-critical, just log the error
                }

                Plugin.Logger?.LogInfo($"[ManualEnterArena] Completed arena entry for {playerName}");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError(
                    $"[ManualEnterArena] CRITICAL ERROR for {playerName} (SteamID: {steamId}): {ex.Message}");
                Plugin.Logger?.LogError($"[ManualEnterArena] Stack Trace: {ex.StackTrace}");

                // If we have the steamId, try to clean up to prevent stuck states
                if (steamId != 0)
                {
                    PlayersInArena.Remove(steamId);
                    PlayerEntities.Remove(steamId);
                    Plugin.Logger?.LogWarning(
                        $"[ManualEnterArena] Cleaned up arena state for {playerName} after error");
                }

                // Re-throw to allow upper layers to handle the error
                throw new Exception($"Failed to process arena entry for {playerName}: {ex.Message}", ex);
            }
        }

        private static void SpawnArenaEffects(Entity playerEntity)
        {
            if (!IsValidEntity(playerEntity))
                return;

            // Use the properly detected server EntityManager (ICB.core pattern)
            var em = VRisingCore.EntityManager;
            var translation = em.GetComponentData<Translation>(playerEntity);
            var pos = translation.Value;
            // var effectPrefab = new PrefabGUID(-1905691330); // Bat effect

            // TODO: Implement arena entry effect spawning
            Plugin.Logger?.LogInfo($"Arena entry effect would spawn for {playerEntity} at {pos}");
        }

        private static void ExitArena(Entity playerEntity)
        {
            try
            {
                if (playerEntity == Entity.Null)
                {
                    Plugin.Logger?.LogWarning("Attempted to exit arena with null entity");
                    return;
                }

                // Get user entity and Steam ID
                var userEntity = GetUserFromCharacter(playerEntity);
                if (userEntity == Entity.Null)
                {
                    Plugin.Logger?.LogWarning($"Could not find user entity for player entity {playerEntity}");
                    return;
                }

                var user = EM.GetComponentData<User>(userEntity);
                var steamId = user.PlatformId;
                var playerName = user.CharacterName.ToString();

                if (PlayersInArena.Remove(steamId))
                {
                    PlayerEntities.Remove(steamId);
                    Plugin.Logger?.LogInfo($"Player {playerName} (SteamID: {steamId}) auto-exited arena");
                }
                else
                {
                    Plugin.Logger?.LogInfo(
                        $"Player {playerName} (SteamID: {steamId}) was not in arena but exit was requested");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error in ExitArena: {ex.Message}");
                Plugin.Logger?.LogError(ex.StackTrace);
            }
        }

        /// <summary>
        /// Manually removes a player from the arena, revoking their arena status.
        /// </summary>
        /// <param name="playerEntity">The entity of the player to remove from the arena</param>
        public static void ManualExitArena(Entity playerEntity)
        {
            try
            {
                if (playerEntity == Entity.Null)
                {
                    Plugin.Logger?.LogWarning("Attempted to exit arena with null entity");
                    return;
                }

                // Get user entity and Steam ID
                var userEntity = GetUserFromCharacter(playerEntity);
                if (userEntity == Entity.Null)
                {
                    Plugin.Logger?.LogWarning($"Could not find user entity for player entity {playerEntity}");
                    return;
                }

                var user = EM.GetComponentData<User>(userEntity);
                var steamId = user.PlatformId;
                var playerName = user.CharacterName.ToString();

                if (PlayersInArena.Remove(steamId))
                {
                    PlayerEntities.Remove(steamId);
                    Plugin.Logger?.LogInfo($"Player {playerName} (SteamID: {steamId}) manually exited arena");
                }
                else
                {
                    Plugin.Logger?.LogInfo(
                        $"Player {playerName} (SteamID: {steamId}) was not in arena but exit was requested");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error in ManualExitArena: {ex.Message}");
                Plugin.Logger?.LogError(ex.StackTrace);
            }
        }

        /// <summary>
        /// Checks if a player is currently in the arena.
        /// </summary>
        /// <param name="playerEntity">The entity of the player to check</param>
        /// <returns>True if the player is in the arena, false otherwise</returns>
        public static bool IsPlayerInArena(Entity playerEntity)
        {
            try
            {
                if (playerEntity == Entity.Null) return false;

                // Get the user entity
                var userEntity = GetUserFromCharacter(playerEntity);
                if (userEntity == Entity.Null) return false;

                // Get the Steam ID
                var user = EM.GetComponentData<User>(userEntity);
                return PlayersInArena.Contains(user.PlatformId);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error in IsPlayerInArena: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the territory index of a player in the arena grid.
        /// </summary>
        /// <param name="playerEntity">The entity of the player</param>
        /// <returns>The territory index, or -1 if not in a valid territory</returns>
        public static int GetPlayerTerritoryIndex(Entity playerEntity)
        {
            if (!IsValidEntity(playerEntity))
                return -1;

            if (LastPlayerPositions.TryGetValue(playerEntity, out var position))
            {
                return ArenaTerritory.GetArenaGridIndex(position);
            }

            return -1;
        }
    }
}
