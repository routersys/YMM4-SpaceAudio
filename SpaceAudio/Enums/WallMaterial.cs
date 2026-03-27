using SpaceAudio.Localization;
using System.ComponentModel.DataAnnotations;

namespace SpaceAudio.Enums;

public enum WallMaterial
{
    [Display(Name = nameof(Texts.MaterialConcrete), ResourceType = typeof(Texts))]
    Concrete,

    [Display(Name = nameof(Texts.MaterialWood), ResourceType = typeof(Texts))]
    Wood,

    [Display(Name = nameof(Texts.MaterialGlass), ResourceType = typeof(Texts))]
    Glass,

    [Display(Name = nameof(Texts.MaterialCarpet), ResourceType = typeof(Texts))]
    Carpet,

    [Display(Name = nameof(Texts.MaterialAcousticPanel), ResourceType = typeof(Texts))]
    AcousticPanel,

    [Display(Name = nameof(Texts.MaterialBrick), ResourceType = typeof(Texts))]
    Brick,

    [Display(Name = nameof(Texts.MaterialDrywall), ResourceType = typeof(Texts))]
    Drywall,

    [Display(Name = nameof(Texts.MaterialTile), ResourceType = typeof(Texts))]
    Tile
}
