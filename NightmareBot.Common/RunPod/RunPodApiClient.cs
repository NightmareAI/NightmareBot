using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace NightmareBot.Common.RunPod
{

    public class RunPodApiClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly GraphQLHttpClient _apiClient;
        public RunPodApiClient(string apiKey, string baseUrl = "https://api.runpod.io/graphql")
        {            
            _baseUrl = baseUrl;
            _apiKey = apiKey; 
            _apiClient = new GraphQLHttpClient(_baseUrl + $"?api_key={_apiKey}", new NewtonsoftJsonSerializer());
            _apiClient.Options.UseWebSocketForQueriesAndMutations = false;                      
        }

        public async Task<Pod[]?> GetPodsAsync()
        {
            var getPodsRequest = new GraphQLRequest
            {
                Query = @"
		        query myPods {
			        myself {
			          pods {
				        id
				        containerDiskInGb
				        costPerHr
				        desiredStatus
				        dockerArgs
				        dockerId
				        env
				        gpuCount
				        imageName
				        lastStatusChange
				        machineId
				        memoryInGb
				        name
				        podType
				        port
				        ports
				        uptimeSeconds
				        vcpuCount
				        volumeInGb
				        volumeMountPath
				        machine {
				          gpuDisplayName
				        }
			          }
			        }
		          }"
            };
            var result = await _apiClient.SendQueryAsync<GetPodsContainer>(getPodsRequest);

            if (result.Errors != null && result.Errors.Any())
            {
                var output = new StringBuilder();
                foreach (var error in result.Errors)
                    output.AppendLine(error.Message);
                throw new Exception(output.ToString());
            }

            return result?.Data?.Myself?.Pods;
        }

        public async Task<string> StartSpotPod(string id, float bid)
        {
            var startPodRequest = new GraphQLRequest
            {
                Query = @"
		        mutation Mutation($podId: String!, $bidPerGpu: Float!, $gpuCount: Int!) {
			        podBidResume(input: {podId: $podId, bidPerGpu: $bidPerGpu, gpuCount: $gpuCount}) {
			          id
			          costPerHr
			          desiredStatus
			          lastStatusChange
			        }
		        }",
                Variables = new { podId = id, bidPerGpu = bid, gpuCount = 1 }
            };

            var result = await _apiClient.SendQueryAsync<dynamic>(startPodRequest);

            if (result.Errors != null && result.Errors.Any())
            {
                var output = new StringBuilder();
                foreach (var error in result.Errors)
                    output.AppendLine(error.Message);
                throw new Exception(output.ToString());
            }

            return result.AsGraphQLHttpResponse().StatusCode.ToString();

        }

        public async Task<string> StopPod(string podId)
        {            
            var stopPodRequest = new GraphQLRequest
            {
                Query = @"
		        mutation stopPod($podId: String!) {
		          podStop(input: {podId:  $podId}) {
			        id
			        desiredStatus
			        lastStatusChange
		          }
		        }",
                Variables = new { podId = podId }                
            };

            var result = await _apiClient.SendQueryAsync<dynamic>(stopPodRequest);

            if (result.Errors != null && result.Errors.Any())
            {
                var output = new StringBuilder();
                foreach (var error in result.Errors)
                    output.AppendLine(error.Message);
                throw new Exception(output.ToString());
            }

            return result.AsGraphQLHttpResponse().StatusCode.ToString();
        }

        public async Task<Cloud[]?> GetCloudAsync(GetCloudInput input)
        {
            var getCloudRequest = new GraphQLRequest
            {
                Query = @"
                query LowestPrice($input: GpuLowestPriceInput!) {
			        gpuTypes {
			          lowestPrice(input: $input) {
				        gpuName
				        gpuTypeId
				        minimumBidPrice
				        uninterruptablePrice
				        minMemory
				        minVcpu
			          }
			        }
		        }",
                Variables = new { input = input }
            };

            var result = await _apiClient.SendQueryAsync<GetCloudResponse>(getCloudRequest);

            if (result.Errors != null && result.Errors.Any())
            {
                var output = new StringBuilder();
                foreach (var error in result.Errors)
                    output.AppendLine(error.Message);
                throw new Exception(output.ToString());
            }

            return result.Data?.GpuTypes?.Select(g => g.LowestPrice)?.ToArray();
        }

        public async Task<Dictionary<Pod, Cloud>> GetPodsWithClouds()
        {
            Dictionary<Pod, Cloud> cloudsByPod = new Dictionary<Pod, Cloud>();
            var pods = await this.GetPodsAsync();
            if (pods != null)
            {
                var clouds = await this.GetCloudAsync(new GetCloudInput() {  SecureCloud = true });
                
                foreach (Pod pod in pods)
                {
                    var cloudItem = clouds?.FirstOrDefault(c => c.GpuName == pod.Machine?.GpuDisplayName);
                    if (cloudItem != null)
                    {
                        cloudsByPod.Add(pod, cloudItem);
                    }
                }                
            }
            return cloudsByPod;
            
        }
    }
}
