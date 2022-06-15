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
        public RunPodApiClient(string apiKey, string baseUrl = "https://api.runpod.io/graphql")
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;            
        }

        public async Task<string> StopPod(string podId)
        {
            var graphQLClient = new GraphQLHttpClient(_baseUrl + $"?api_key={_apiKey}", new NewtonsoftJsonSerializer());
            graphQLClient.Options.UseWebSocketForQueriesAndMutations = false;           
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

            var result = await graphQLClient.SendQueryAsync<dynamic>(stopPodRequest);

            if (result.Errors != null && result.Errors.Any())
            {
                var output = new StringBuilder();
                foreach (var error in result.Errors)
                    output.AppendLine(error.Message);
                throw new Exception(output.ToString());
            }

            return result.AsGraphQLHttpResponse().StatusCode.ToString();
        }
    }
}
