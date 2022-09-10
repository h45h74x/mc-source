﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using McSource.Extensions;
using McSource.Logging;
using McSource.Models.Nbt.BlockEntities;
using McSource.Models.Nbt.Blocks.Abstract;
using McSource.Models.Nbt.Enums;
using McSource.Models.Nbt.Face;
using McSource.Models.Nbt.Schematic;
using McSource.Models.Nbt.Structs;
using McSource.Models.Vmf;
using VmfSharp;

namespace McSource.Models.Nbt.Blocks
{
  /// <summary>
  /// Default solid, non-translucent minecraft block without any special attributes
  /// </summary>
  public class SolidBlock : TexturedBlock<SolidFace>
  {
    public SolidBlock(ISchematic parent, [NotNull] BlockInfo info, Coordinates coordinates,
      [CanBeNull] BlockEntity? blockEntity = default) : base(parent, info, coordinates, blockEntity)
    {
    }

    protected override SolidFace GetFace(McDirection3D pos)
    {
      if (Config?.Texture.MaterialPath == null)
      {
        return new SolidFace(this, pos, Info.ToPath());
      }

      return new SolidFace(this, pos, $"{Info.Namespace}/{Config?.Texture.MaterialPath}");
    }
  }
}