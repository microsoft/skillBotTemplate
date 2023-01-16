// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure;
using Azure.AI.Language.Conversations.Authoring;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Samples.SkillBot.CLU;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Bot.Samples.SkillBot.Dialogs
{
    public class CLURecognizer : IRecognizer
    {
        private readonly ConversationLanguageUnderstandingClient _recognizer;
        private readonly ConversationAuthoringClient _author;

        public CLURecognizer(IConfiguration configuration, IBotTelemetryClient botTelemetryClient)
        {
            var cluIsConfigured = !string.IsNullOrEmpty(configuration["CLUEndpoint"])
                && !string.IsNullOrEmpty(configuration["CLUAPIKey"])
                && !string.IsNullOrEmpty(configuration["CLUProjectName"]);

            if (cluIsConfigured)
            {
                var verbose = !string.IsNullOrEmpty(configuration["CLUVerbose"]) ? bool.Parse(configuration["CLUVerbose"]) : false;
                _recognizer = new ConversationLanguageUnderstandingClient(
                    new Uri(
                    configuration["CLUEndpoint"]),
                    new AzureKeyCredential(
                    configuration["CLUAPIKey"]),
                    configuration["CLUProjectName"],
                    verbose,
                    botTelemetryClient);

                _author = new ConversationAuthoringClient(new Uri(
                    configuration["CLUEndpoint"]), new AzureKeyCredential(
                    configuration["CLUAPIKey"]));

            }
        }

        // Returns true if CLU is configured in the appsettings.json and initialized.
        public virtual bool IsConfigured => _recognizer != null;

        public virtual async Task<RecognizerResult> RecognizeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
            => await _recognizer.RecognizeAsync(turnContext, cancellationToken);

        public virtual async Task<T> RecognizeAsync<T>(ITurnContext turnContext, CancellationToken cancellationToken)
            where T : IRecognizerConvert, new()
            => await _recognizer.RecognizeAsync<T>(turnContext, cancellationToken);

        public async Task<Dictionary<string, List<string>>> GetProjectsAsync()
        {
            var projects = new Dictionary<string, List<string>>();

            var collection = _author.GetProjectsAsync();

            await foreach (var data in collection)
            {
                JsonElement result = JsonDocument.Parse(data.ToStream()).RootElement;
                var projectName = result.GetProperty("projectName").ToString();
                var deploymentData = _author.GetDeploymentsAsync(projectName);
                var deploymentNames = new List<string>();
                await foreach (var deploymentInfo in deploymentData)
                {
                    JsonElement deployment = JsonDocument.Parse(deploymentInfo.ToStream()).RootElement;
                    deploymentNames.Add(deployment.GetProperty("deploymentName").ToString());
                }

                projects.Add(projectName, deploymentNames);
            }

            return projects;
        }



        public static IntentScore GetMaxScore(IEnumerable<IntentScore> values)
        {
            var scores = new List<double>();
            foreach (var value in values)
            {
                if (value.Score != null)
                    scores.Add((double)value.Score);
            }

            return values.FirstOrDefault(x => x.Score == scores.Max());
        }

    }
}
