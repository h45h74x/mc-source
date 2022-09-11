﻿using System;
using System.Collections.Generic;
using System.Linq;
using fNbt;
using McSource.Extensions;
using McSource.Logging;
using McSource.Models.Nbt.BlockEntities;
using McSource.Models.Nbt.Blocks;
using McSource.Models.Nbt.Blocks.Abstract;
using McSource.Models.Nbt.Enums;
using McSource.Models.Nbt.Structs;
using McSource.Models.Vmf;

namespace McSource.Models.Nbt.Schematic
{
  /// <summary>
  /// Deserializes Schematics from .schem NBT files, according to the <a href="https://github.com/SpongePowered/Schematic-Specification/blob/master/versions/schematic-2.md#paletteObject">Sponge SpongeSchematic Specification</a>
  /// </summary>
  public class SpongeSchematic : Schematic<NbtCompound>
  {
    private Dictionary<int, string> LoadPalette(NbtCompound rootTag)
    {
      return rootTag
        .Get<NbtCompound>("Palette")!
        .Tags
        .Cast<NbtInt>()
        .ToDictionary(tag => tag.Value, tag => tag.Name!);
    }

    private ICollection<BlockEntity> LoadBlockEntities(NbtCompound rootTag)
    {
      return rootTag
        .Get<NbtList>("BlockEntities")!
        .Select(tag => BlockEntity.FromTag((NbtCompound) tag))
        .ToArray();
    }

    private byte[] LoadBlockData(NbtCompound rootTag)
    {
      return rootTag.Get<NbtByteArray>("BlockData")!.Value;
    }


    public static ISchematic? FromTag(NbtCompound rootTag, Config.Config config)
    {
      try
      {
        return new SpongeSchematic(config).Load(rootTag);
      }
      catch (NullReferenceException e)
      {
        Log.Error($"Could not read SpongeSchematic from {nameof(NbtCompound)}", e);
        return default;
      }
    }

    public SpongeSchematic(Config.Config config) : base(config)
    {
    }


    public override Map ToModel()
    {
      var map = new Map();
      map.World = new World(map);

      var solids = new List<Solid?>
      {
        // Skybox: South
        new SkyboxBlock(this, new Coordinates(0, 0, -1), new Dimensions3D(Dimensions.DX, Dimensions.DY, 1)).ToModel(map),
        // Skybox: North
        new SkyboxBlock(this, new Coordinates(0, 0, Dimensions.DZ), new Dimensions3D(Dimensions.DX, Dimensions.DY, 1)).ToModel(map),

        // Skybox: West
        new SkyboxBlock(this, new Coordinates(-1, 0, 0), new Dimensions3D(1, Dimensions.DY, Dimensions.DZ)).ToModel(map),
        // Skybox: East
        new SkyboxBlock(this, new Coordinates(Dimensions.DX, 0, 0), new Dimensions3D(1, Dimensions.DY, Dimensions.DZ)).ToModel(map),

        // Skybox: Top
        new SkyboxBlock(this, new Coordinates(0, Dimensions.DY, 0), new Dimensions3D(Dimensions.DX, 1, Dimensions.DZ)).ToModel(map), // Top
        // Skybox: Bottom
        new SkyboxBlock(this, new Coordinates(0, -1, 0), new Dimensions3D(Dimensions.DX, 1, Dimensions.DZ)).ToModel(map),
      };

      foreach (var block in Blocks)
      {
        block.Prepare();
      }

      // todo inefficient
      
      for (short z = 0; z < Dimensions.DZ; z++)
      for (short x = 0; x < Dimensions.DX; x++)
      for (short y = 0; y < Dimensions.DY; y++)
      {
        if (TryGet(x, y, z, out var block) && block is {CanDraw: true, BlockGroup: null})
        {
            // Y
          
            var ty = y;
            var yBlocks = new List<Block>();
            while (TryGet(x, ++ty, z, out var nextBlock) && nextBlock is {CanDraw: true, BlockGroup: null} && block.Equals(nextBlock))
            {
              yBlocks.Add(nextBlock);
            }

            if (yBlocks.Any())
            {
              var blockGroup = new BlockGroup(McDirection3D.Top, block, yBlocks.ToArray());
              Log.Info($"+Group: {blockGroup}");
              solids.Add(blockGroup.ToModel(map));
              continue;
            }

            // X
            
            var tx = x;
            var xBlocks = new List<Block>();
            while (TryGet(++tx, y, z, out var nextBlock) && nextBlock is {CanDraw: true, BlockGroup: null} && block.Equals(nextBlock))
            {
              xBlocks.Add(nextBlock);
            }

            if (xBlocks.Any())
            {
              var group = new BlockGroup(McDirection3D.East, block, xBlocks.ToArray());
              Log.Info($"+Group: {@group}");
              solids.Add(@group.ToModel(map));
              continue;
            }

            // Z
            
            var tz = z;
            var zBlocks = new List<Block>();
            while (TryGet(x, y, ++tz, out var nextBlock) && nextBlock is {CanDraw: true, BlockGroup: null} && block.Equals(nextBlock))
            {
              zBlocks.Add(nextBlock);
            }

            if (zBlocks.Any())
            {
              var group = new BlockGroup(McDirection3D.North, block, zBlocks.ToArray());
              Log.Info($"+Group: {@group}");
              solids.Add(@group.ToModel(map));
              continue;
            }

        }
      }

      var t1 = solids.Count;
      Log.Info($"Grouped solids: {solids.Count}");

      foreach (var block in Blocks)
      {
        if (block is {CanDraw: true, BlockGroup: null})
        {
          // Log.Info($"+Single: {block}");
          solids.Add(block.ToModel(map));
        }
      }

      Log.Info($"Single Solids: {solids.Count - t1}");

      map.World = new World(map)
      {
        Solids = solids
      };
      Log.Info($"Total Solids: {solids.Count}");
      return map;
    }

    /// <summary>
    /// Ported from <a href="https://github.com/SpongePowered/Sponge/blob/aa2c8c53b4f9f40297e6a4ee281bee4f4ce7707b/src/main/java/org/spongepowered/common/data/persistence/SchematicTranslator.java#L147-L175">here</a>
    /// </summary>
    /// <param name="rootTag"></param>
    /// <exception cref="ArgumentException"></exception>
    public override ISchematic Load(NbtCompound rootTag)
    {
      Dimensions = new Dimensions3D
      {
        DY = rootTag.Get<NbtShort>("Height")!.Value,
        DX = rootTag.Get<NbtShort>("Width")!.Value,
        DZ = rootTag.Get<NbtShort>("Length")!.Value
      };
      Blocks = new Block[Dimensions.DX, Dimensions.DY, Dimensions.DZ];

      var palette = LoadPalette(rootTag);
      var blockEntities = LoadBlockEntities(rootTag);

      var i = 0;
      var index = 0;

      var blockData = LoadBlockData(rootTag);
      while (i < blockData.Length)
      {
        int value = 0, varintLength = 0;

        while (true)
        {
          value |= (blockData[i] & 127) << (varintLength++ * 7);

          if (varintLength > 5)
          {
            throw new ArgumentException("VarInt too big (probably corrupted data)");
          }

          if ((blockData[i++] & 128) != 128)
          {
            break;
          }
        }

        // index = (y * length + z) * width + x
        var coordinates = new Coordinates
        {
          Y = index / (Dimensions.DX * Dimensions.DZ),
          Z = Dimensions.DZ - 1 - ((index % (Dimensions.DX * Dimensions.DZ)) / Dimensions.DX),
          X = (index % (Dimensions.DX * Dimensions.DZ)) % Dimensions.DX,
        };
        var blockEntity = blockEntities.FirstOrDefault(be => coordinates == be.Coordinates);

        this.Add(Block.Create(this, BlockInfo.FromString(palette[value]), coordinates, blockEntity), coordinates);

        index++;
      }

      return this;
    }
  }
}