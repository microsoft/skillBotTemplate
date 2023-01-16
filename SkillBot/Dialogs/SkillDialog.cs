// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.Bot.Samples.SkillBot.Dialogs
{
    public class SkillDialog : CancelAndHelpDialog
    {
        private const string SkillPropertyStepMsgText = "<Replace this with your skill step prompt>";

        public SkillDialog(IBotTelemetryClient botTelemetryClient)
            : base(nameof(SkillDialog))
        {
            // Set the telemetry client for this and all child dialogs
            this.TelemetryClient = botTelemetryClient;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new DateResolverDialog());
            AddDialog(
                new WaterfallDialog(
                    nameof(WaterfallDialog),
                    new WaterfallStep[] { SkillStringPropertyStepAsync, SkillDatePropertyStepAsync, ConfirmStepAsync, FinalStepAsync }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private static bool IsAmbiguous(string timex)
        {
            var timexProperty = new TimexProperty(timex);
            return !timexProperty.Types.Contains(Constants.TimexTypes.Definite);
        }

        private async Task<DialogTurnResult> SkillStringPropertyStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var skillDetails = (SkillDetails)stepContext.Options;

            if (skillDetails.Property == null)
            {
                var promptMessage = MessageFactory.Text(SkillPropertyStepMsgText, SkillPropertyStepMsgText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }

            return await stepContext.NextAsync(skillDetails.Property, cancellationToken);
        }


        private async Task<DialogTurnResult> SkillDatePropertyStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var skillDetails = (SkillDetails)stepContext.Options;

            skillDetails.DateProperty = (string)stepContext.Result;

            if (skillDetails.DateProperty == null || IsAmbiguous(skillDetails.DateProperty))
            {
                return await stepContext.BeginDialogAsync(nameof(DateResolverDialog), skillDetails.DateProperty, cancellationToken);
            }

            return await stepContext.NextAsync(skillDetails.DateProperty, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var skillDetails = (SkillDetails)stepContext.Options;

            skillDetails.DateProperty = (string)stepContext.Result;

            var messageText = $"Please confirm your choice. Is this correct?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);

            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                var skillDetails = (SkillDetails)stepContext.Options;

                // Send the booking details to the Telemetry
                TelemetryClient.TrackEvent("Booking", new Dictionary<string, string>
                {
                    { "conversationId", stepContext.Context.Activity.Conversation.Id },
                    { "property", skillDetails.Property },
                    { "dateProperty", skillDetails.DateProperty },
                });


                return await stepContext.EndDialogAsync(skillDetails, cancellationToken);
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}
