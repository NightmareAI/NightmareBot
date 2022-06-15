using NightmareBot.Common.RunPod;

namespace NightmareBot.Tests
{
    public class RunPodApiIntegrationTests
    {
        private RunPodApiClient _podApiClient;

        [SetUp]
        public void Setup()
        {
            _podApiClient = new RunPodApiClient(Environment.GetEnvironmentVariable("NIGHTMAREBOT_RUNPOD_KEY"));
        }

        [Test]
        public async Task TestGetPods()
        {            
            var pods = await _podApiClient.GetPodsAsync();
            Assert.IsNotNull(pods);
        }
        [Test]
        public async Task TestGetCloud()
        {
            var cloud = await _podApiClient.GetCloudAsync(new GetCloudInput());
            Assert.IsNotNull(cloud);
        }

        [Test]
        public async Task TestGetPodsWithClouds()
        {
            var pods = await _podApiClient.GetPodsWithClouds();
            Assert.IsNotNull(pods);
        }
    }
}