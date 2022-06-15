using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareBot.Common.RunPod
{
    public record GetCloudResponse
    {
        public GpuTypeResponse[] GpuTypes { get; set; } = new GpuTypeResponse[0];
    }

    public record GpuTypeResponse
    {
        public Cloud LowestPrice { get; set; } = new Cloud();
    }

    public record Cloud
    {
        public string? GpuName { get; set; }
        public string? GpuTypeId { get; set; }
        public float? MinimumBidPrice { get; set; }
        public float? UninterruptablePrice { get; set; }
        public int? MinMemory { get; set; }
        public int? MinVcpu { get; set; }
    }

    public record GetCloudInput
    {
        public int GpuCount { get; set; } = 1;
        public int? MinMemoryInGb { get; set; }
        public int? MinVcpuCount { get; set; }
        public bool SecureCloud { get; set; } = false;
        public int? TotalDisk { get; set; }
    }
}
