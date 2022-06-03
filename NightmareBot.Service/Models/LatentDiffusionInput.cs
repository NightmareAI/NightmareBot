using Discord.Commands;

namespace NightmareBot.Models;

[NamedArgumentType]
public class LatentDiffusionInput : IGeneratorInput
{
  public string prompt { get; set; }
  public int ddim_steps { get; set; } = 80;
  public float ddim_eta { get; set; } = 0.0f;
  public int n_iter { get; set; } = 1;
  public int n_samples {get ; set; } = 3;
  public int height { get; set; } = 384;
  public int width { get; set; } = 256;
  public float scale { get; set; } = 10.0f;
  public bool plms { get; set; } = true;
}