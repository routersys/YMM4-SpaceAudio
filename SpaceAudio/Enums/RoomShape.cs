using SpaceAudio.Localization;
using System.ComponentModel.DataAnnotations;

namespace SpaceAudio.Enums;

public enum RoomShape
{
    [Display(Name = nameof(Texts.ShapeRectangular), ResourceType = typeof(Texts))]
    Rectangular,

    [Display(Name = nameof(Texts.ShapeLShaped), ResourceType = typeof(Texts))]
    LShaped,

    [Display(Name = nameof(Texts.ShapeCathedral), ResourceType = typeof(Texts))]
    Cathedral,

    [Display(Name = nameof(Texts.ShapeStudio), ResourceType = typeof(Texts))]
    Studio
}
