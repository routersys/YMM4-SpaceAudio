using SpaceAudio.Localization;
using System.ComponentModel.DataAnnotations;

namespace SpaceAudio.Enums;

public enum ReverbQuality
{
    [Display(Name = nameof(Texts.QualityEconomy), ResourceType = typeof(Texts))]
    Economy,

    [Display(Name = nameof(Texts.QualityStandard), ResourceType = typeof(Texts))]
    Standard,

    [Display(Name = nameof(Texts.QualityHigh), ResourceType = typeof(Texts))]
    High
}
