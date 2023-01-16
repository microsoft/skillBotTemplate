using Azure;
using Azure.AI.Language.Conversations;
using Azure.AI.Language.Conversations.Authoring;
using Azure.Core;
using Microsoft.Bot.Builder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Bot.Samples.SkillBot.CLU
{
    public class TelemetryProperties
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Category { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float Confidence { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ResolutionKind { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Timex { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Begin { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string End { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; set; }
    }

    /// <remarks>
    /// See <see href="https://docs.microsoft.com/rest/api/language/conversation-analysis-runtime"/> for more information about models you can pass to this client.
    /// </remarks>
    /// <seealso href="https://docs.microsoft.com/rest/api/language/conversation-analysis-runtime"/>
    public class ConversationLanguageUnderstandingClient
    {
        private readonly Uri _endpoint;
        private readonly ConversationAnalysisClient _client;
        private readonly ConversationAuthoringClient _author;
        private readonly string _projectName;
        private readonly bool _verbose;
        //Create field for telemetry client. Use manual tracking to AppInsights until it is integrated to CLU
        private IBotTelemetryClient _adapterBotTelemetryClient;


        /// <summary>
        /// Gets the service endpoint for this client.
        /// </summary>
        public virtual Uri Endpoint => _endpoint;

        /// <summary> Initializes a new instance of ConversationAnalysisClient. </summary>
        /// <param name="endpoint"> Supported Cognitive Services endpoint (e.g., https://&lt;resource-name&gt;.cognitiveservices.azure.com). </param>
        /// <param name="credential"> A credential used to authenticate to an Azure Service. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="endpoint"/> or <paramref name="credential"/> is null. </exception>
        public ConversationLanguageUnderstandingClient(Uri endpoint, AzureKeyCredential credential, IBotTelemetryClient botTelemetryClient) 
            : this(endpoint, credential, new ConversationsClientOptions())
        {
        }

        /// <summary> Initializes a new instance of ConversationAnalysisClient. </summary>
        /// <param name="endpoint"> Supported Cognitive Services endpoint (e.g., https://&lt;resource-name&gt;.cognitiveservices.azure.com). </param>
        /// <param name="credential"> A credential used to authenticate to an Azure Service. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="endpoint"/> or <paramref name="credential"/> is null. </exception>
        public ConversationLanguageUnderstandingClient(Uri endpoint, AzureKeyCredential credential, string projectName, bool verbose, IBotTelemetryClient botTelemetryClient) 
            : this(endpoint, credential, botTelemetryClient)
        {
            _projectName = projectName;
            _verbose = verbose;
            _adapterBotTelemetryClient = botTelemetryClient;
        }

        /// <summary> Initializes a new instance of ConversationAnalysisClient. </summary>
        /// <param name="endpoint"> Supported Cognitive Services endpoint (e.g., https://&lt;resource-name&gt;.cognitiveservices.azure.com). </param>
        /// <param name="credential"> A credential used to authenticate to an Azure Service. </param>
        /// <param name="options"> The options for configuring the client. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="endpoint"/> or <paramref name="credential"/> is null. </exception>
        public ConversationLanguageUnderstandingClient(Uri endpoint, AzureKeyCredential credential, ConversationsClientOptions options)
        {
            options ??= new ConversationsClientOptions()
            {
            };

            _endpoint = endpoint;

            _client = new ConversationAnalysisClient(endpoint, credential, options);
            _author = new ConversationAuthoringClient(endpoint, credential);
        }


        //
        // Summary:
        //     Return results of the analysis (Suggested actions and intents).
        //
        // Parameters:
        //   turnContext:
        //     Context object containing information for a single turn of conversation with
        //     a user.
        //
        //   cancellationToken:
        //     A cancellation token that can be used by other objects or threads to receive
        //     notice of cancellation.
        //
        // Returns:
        //     The CLU results of the analysis of the current message text in the current turn's
        //     context activity.
        public virtual async Task<RecognizerResult> RecognizeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return await RecognizeInternalAsync(turnContext, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        //
        // Summary:
        //     Return results of the analysis (Suggested actions and intents).
        //
        // Parameters:
        //   turnContext:
        //     Context object containing information for a single turn of conversation with
        //     a user.
        //
        //   cancellationToken:
        //     A cancellation token that can be used by other objects or threads to receive
        //     notice of cancellation.
        //
        // Returns:
        //     The CLU results of the analysis of the current message text in the current turn's
        //     context activity.
        public virtual async Task<T> RecognizeAsync<T>(ITurnContext turnContext, CancellationToken cancellationToken) where T : IRecognizerConvert, new()
        {
            T result = new T();
            result.Convert(await RecognizeInternalAsync(turnContext, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
            return result;
        }


        //
        // Summary:
        //     Returns a RecognizerResult object.
        //
        // Parameters:
        //   turnContext:
        //     Dialog turn Context.
        //
        // Returns:
        //     RecognizerResult object.
        private async Task<RecognizerResult> RecognizeInternalAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var telemetryProperties = new Dictionary<string, string>();
            var result = new RecognizerResult();
            result.Text = turnContext.Activity.Text;

            var data = new
            {
                analysisInput = new
                {
                    conversationItem = new
                    {
                        id = "1",
                        text = result.Text,
                        modality = "text",
                        language = "en-us",
                        participantId = turnContext.Activity.CallerId,
                    }
                },
                parameters = new
                {
                    projectName = _projectName,
                    deploymentName = await GetProjectLatestDeploymentAsync(_projectName),
                    verbose = _verbose,

                    // Use Utf16CodeUnit for strings in .NET.
                    stringIndexType = "Utf16CodeUnit",
                },
                kind = "Conversation",
            };


            Response response = await _client.AnalyzeConversationAsync(RequestContent.Create(data));

            using JsonDocument analysisResult = JsonDocument.Parse(response.ContentStream);
            JsonElement conversationalTaskResult = analysisResult.RootElement;
            JsonElement conversationPrediction = conversationalTaskResult.GetProperty("result").GetProperty("prediction");

            result.Properties.Add("TopIntent", conversationPrediction.GetProperty("topIntent").GetString());
                
            // Add telemetry
            telemetryProperties.Add("TopIntent", conversationPrediction.GetProperty("topIntent").GetString());

            // Intents
            var intentProperties = new List<TelemetryProperties>();
            foreach (JsonElement intent in conversationPrediction.GetProperty("intents").EnumerateArray())
            {
                result.Intents.Add(intent.GetProperty("category").GetString(),
                    new IntentScore() { Score = intent.GetProperty("confidenceScore").GetSingle() });

                // Add telemetry
                intentProperties.Add(new TelemetryProperties
                { 
                    Category = intent.GetProperty("category").GetString(),
                    Confidence = intent.GetProperty("confidenceScore").GetSingle(),
                });
                telemetryProperties.Add(intent.GetProperty("category").GetString(), JsonConvert.SerializeObject(intentProperties));
            }

            // Entities
            var entityProperties = new List<TelemetryProperties>();
            var jEntities = new JArray();
            foreach (JsonElement entity in conversationPrediction.GetProperty("entities").EnumerateArray())
            {
                var jEntity = new JObject {
                    new JProperty("category", entity.GetProperty("category").GetString()),
                    new JProperty("text", entity.GetProperty("text").GetString()),
                    new JProperty("confidence", entity.GetProperty("confidenceScore").ToString())
                };

                // Add telemetry
                var entityTelemetry = new TelemetryProperties
                {
                    Category = entity.GetProperty("category").GetString(),
                    Confidence = entity.GetProperty("confidenceScore").GetSingle(),
                    Text = entity.GetProperty("text").GetString(),
                };

                if (entity.TryGetProperty("resolutions", out JsonElement resolutions))
                {
                    var jResolutions = new JArray();
                    foreach (JsonElement resolution in resolutions.EnumerateArray())
                    {
                        if (resolution.GetProperty("resolutionKind").GetString() == CLUResolutionKinds.DateTimeResolution)
                        {
                            var jResolution = new JObject {
                                new JProperty("resolutionKind", resolution.GetProperty("resolutionKind").GetString()),
                                new JProperty("timex", resolution.GetProperty("timex").GetString()),
                                new JProperty("value", resolution.GetProperty("value").GetString())
                            };
                            jResolutions.Add(jResolution);

                            // Add telemetry
                            entityTelemetry.ResolutionKind = resolution.GetProperty("resolutionKind").GetString();
                            entityTelemetry.Timex = resolution.GetProperty("timex").GetString();
                            entityTelemetry.Value = resolution.GetProperty("value").GetString();
                        }
                        else if (resolution.GetProperty("resolutionKind").GetString() == CLUResolutionKinds.TemporalSpanResolution)
                        {
                            var jResolution = new JObject {
                                new JProperty("resolutionKind", resolution.GetProperty("resolutionKind").GetString()),
                                new JProperty("timex", resolution.GetProperty("timex").GetString()),
                                new JProperty("begin", resolution.GetProperty("begin").GetString()),
                                new JProperty("end", resolution.GetProperty("end").GetString()),
                            };
                            jResolutions.Add(jResolution);

                            // Add telemetry
                            entityTelemetry.ResolutionKind = resolution.GetProperty("resolutionKind").GetString();
                            entityTelemetry.Timex = resolution.GetProperty("timex").GetString();
                            entityTelemetry.Begin = resolution.GetProperty("begin").GetString();
                            entityTelemetry.End = resolution.GetProperty("end").GetString();
                        }
                    }
                    if (jResolutions.Count > 0)
                    {
                        jEntity.Add(new JProperty("resolutions", jResolutions));
                    }
                }

                jEntities.Add(jEntity);

                // Add telemetry
                entityProperties.Add(entityTelemetry);
                telemetryProperties.Add(entity.GetProperty("category").GetString(), JsonConvert.SerializeObject(entityProperties));
            }

            result.Entities.Add("entity", jEntities);

            // Send the CLU results to AppInsight
            _adapterBotTelemetryClient.TrackEvent("CLU", telemetryProperties);

            return result;
        }


        private async Task<string> GetProjectLatestDeploymentAsync(string projectName)
        {
            var deploymentNames = new List<string>();
            var deploymentData = _author.GetDeploymentsAsync(projectName, null);
            await foreach (var deploymentInfo in deploymentData)
            {
                JsonElement deployment = JsonDocument.Parse(deploymentInfo.ToStream()).RootElement;
                deploymentNames.Add(deployment.GetProperty("deploymentName").ToString());
            }

            return deploymentNames.First<string>();
        }

    }
}
