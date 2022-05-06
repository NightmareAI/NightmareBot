using System.Text.Json.Serialization;

namespace NightmareBot.Models;

public class SwinIRInput : IGeneratorInput
{
  [JsonPropertyName("images")]
  public string[] images { get; set; }

}