using Discord.Commands;

namespace NightmareBot.Models;

[NamedArgumentType]
public class LatentDiffusionInput : IGeneratorInput
{
  public string prompt { get; set; }
  public int ddim_steps { get; set; } = 50;
  public float ddim_eta { get; set; } = 0.0f;
  public int n_iter { get; set; } = 1;
  public int n_samples {get ; set; } = 1;
  public int H { get; set; } = 256;
  public int W { get; set; } = 256;
  public float scale { get; set; } = 5.0f;
  public bool plms { get; set; } = false;
}