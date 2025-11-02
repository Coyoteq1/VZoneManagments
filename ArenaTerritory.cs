using Unity.Mathematics;
using Unity.Entities;
using System.Collections.Generic;

namespace CrowbaneArena
{
    public static class ArenaTerritory
    {
        public static readonly float3 ArenaGridCenter = new float3(-1000, 0, 500);
        public static readonly float ArenaGridRadius = 300f;
        public static readonly int ArenaGridIndex = 500;
        public static readonly int ArenaRegionType = 5; // Custom arena region
        
        private static HashSet<int2> ArenaBlocks = new HashSet<int2>();
        private static bool IsInitialized = false;

        public static void InitializeArenaGrid()
        {
            if (IsInitialized) return;
            
            // Generate grid blocks for arena territory
            var centerBlock = ConvertPosToBlockCoord(ArenaGridCenter);
            int blockRadius = (int)(ArenaGridRadius / 10f); // 10 units per block
            
            for (int x = -blockRadius; x <= blockRadius; x++)
            {
                for (int z = -blockRadius; z <= blockRadius; z++)
                {
                    var blockCoord = new int2(centerBlock.x + x, centerBlock.y + z);
                    var blockWorldPos = ConvertBlockCoordToPos(blockCoord);
                    
                    if (math.distance(blockWorldPos, ArenaGridCenter) <= ArenaGridRadius)
                    {
                        ArenaBlocks.Add(blockCoord);
                    }
                }
            }
            
            IsInitialized = true;
            Plugin.Logger?.LogInfo($"Arena territory initialized with {ArenaBlocks.Count} blocks at grid index {ArenaGridIndex}");
        }

        public static bool IsInArenaTerritory(float3 position)
        {
            var blockCoord = ConvertPosToBlockCoord(position);
            return ArenaBlocks.Contains(blockCoord);
        }

        public static int GetArenaRegion(float3 position)
        {
            return IsInArenaTerritory(position) ? ArenaRegionType : 0;
        }

        public static int GetArenaGridIndex(float3 position)
        {
            return IsInArenaTerritory(position) ? ArenaGridIndex : -1;
        }

        private static int2 ConvertPosToBlockCoord(float3 position)
        {
            return new int2(
                (int)math.floor(position.x / 10f),
                (int)math.floor(position.z / 10f)
            );
        }

        private static float3 ConvertBlockCoordToPos(int2 blockCoord)
        {
            return new float3(blockCoord.x * 10f + 5f, 0, blockCoord.y * 10f + 5f);
        }

        public static List<int2> GetArenaBlocks()
        {
            return new List<int2>(ArenaBlocks);
        }
    }
}
