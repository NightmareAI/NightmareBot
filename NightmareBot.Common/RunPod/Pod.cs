using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareBot.Common.RunPod
{
    public record GetPodsContainer
    {
        public GetPodsResponse? Myself { get; set; }
    }
    public record GetPodsResponse
    {
        public Pod[]? Pods { get; set; }
    }

    public record Pod
    {
        public string Id { get; set; } = string.Empty;
        public int? ContainerDiskInGb { get; set; }
        public string? DesiredStatus { get; set; }
        public string? DockerArgs { get; set; }
        public string[]? Env { get; set; }
        public int? GpuCount { get; set; }
        public string? ImageName { get; set; }
        public int? MemoryInGb { get; set; }
        public string? Name { get; set; }
        public string? PodType { get; set; }
        public string? Ports { get; set; }
        public int? VcpuCount { get; set; }
        public int? VolumeInGb { get; set; }
        public string? VolumeMountPath { get; set; }        
        public Machine? Machine { get; set; }
    }
}
