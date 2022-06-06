using Discord.Commands;

namespace NightmareBot.Models;

[NamedArgumentType]
[Obsolete("Model no longer implemented")]
public class DeepMusicInput : IGeneratorInput
{
  public string song {get; set;}
  public int resolution {get; set;} = 512;
  public int? duration {get; set;}
  public int pitch_sensitivity { get; set; } = 220;
  public float tempo_sensitivity {get; set;} = 0.25f;
  public float depth {get; set;} = 1.0f;
  public float jitter {get; set;} = 0.5f;
  public int frame_length {get; set;} = 512;
  public float truncation {get; set;} = 1.0f;
  public int smooth_factor {get; set;} = 20;
  public int batch_size {get; set;} = 30;
  public bool use_previous_classes {get; set;} = false;
  public bool use_previous_vectors {get; set;} = false;
  public string classes {get; set;}
  public int? num_classes {get; set;}
  public bool sort_classes_by_power {get; set;}
}