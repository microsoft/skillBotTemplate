// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.RootBot.Cards;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Microsoft.Bot.Samples.RootBot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private const string SkillBotId = "<Replace it with your Skill Bot Id>";
        private const string SkillAction1 = "<Replace it with your Skill's first action>";
        private const string SkillAction2 = "<Replace it with your Skill's second action>";
        private const string SkillActionMessage = "Message";

        public static readonly string ActiveSkillPropertyName = $"{typeof(MainDialog).FullName}.ActiveSkillProperty";
        private static readonly string SelectedSkillKey = $"{typeof(MainDialog).FullName}.SelectedSkillKey";
        private readonly IStatePropertyAccessor<BotFrameworkSkill> _activeSkillProperty;
        private readonly BotFrameworkAuthentication _auth;
        private readonly string _connectionName;
        private readonly SkillsConfiguration _skillsConfig;


        public MainDialog(
            BotFrameworkAuthentication auth, 
            ConversationState conversationState, 
            SkillConversationIdFactoryBase conversationIdFactory, 
            SkillsConfiguration skillsConfig, 
            IConfiguration configuration)
            : base(nameof(MainDialog))
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));

            var botId = configuration.GetSection(MicrosoftAppCredentials.MicrosoftAppIdKey)?.Value;
            if (string.IsNullOrWhiteSpace(botId))
            {
                throw new ArgumentException($"{MicrosoftAppCredentials.MicrosoftAppIdKey} is not set in configuration");
            }

            _connectionName = configuration.GetSection("ConnectionName")?.Value;
            if (string.IsNullOrWhiteSpace(_connectionName))
            {
                throw new ArgumentException("\"ConnectionName\" is not set in configuration");
            }

            _skillsConfig = skillsConfig;

            // Use helper method to add SkillDialog instances for the configured skills.
            CreateSkillDialogOptions(botId, conversationIdFactory, conversationState);

            AddDialog(new ChoicePrompt("ActionStepPrompt"));

            // Add ChoicePrompt to render skill actions.
            AddDialog(new ChoicePrompt("SkillActionPrompt", SkillActionPromptValidator));

            //AddDialog(new SsoSignInDialog(_connectionName));

            var waterfallSteps = new WaterfallStep[]
            {
                SelectSkillStepAsync,
                SelectSkillActionStepAsync,
                HandleActionStepAsync,
                PromptFinalStepAsync,
            };
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            // Create state property to track the active skill.
            _activeSkillProperty = conversationState.CreateProperty<BotFrameworkSkill>(ActiveSkillPropertyName);

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }


        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
        {
            // This is an example on how to cancel a SkillDialog that is currently in progress from the parent bot.
            var activeSkill = await _activeSkillProperty.GetAsync(innerDc.Context, () => null, cancellationToken);
            var activity = innerDc.Context.Activity;
            if (activeSkill != null && activity.Type == ActivityTypes.Message && activity.Text.Equals("abort", StringComparison.CurrentCultureIgnoreCase))
            {
                // Cancel all dialogs when the user says abort.
                // The SkillDialog automatically sends an EndOfConversation message to the skill to let the
                // skill know that it needs to end its current dialogs, too.
                await innerDc.CancelAllDialogsAsync(cancellationToken);
                return await innerDc.ReplaceDialogAsync(InitialDialogId, "Canceled! \n\n What skill would you like to call?", cancellationToken);
            }

            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }


        // This validator defaults to Message if the user doesn't select an existing option.
        private Task<bool> SkillActionPromptValidator(PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken)
        {
            if (!(promptContext.Recognized.Succeeded && promptContext.Recognized.Value.Score == 1))
            {
                // Assume the user wants to send a message if an item in the list is not selected.
                promptContext.Recognized.Value = new FoundChoice { Value = SkillActionMessage };
            }

            return Task.FromResult(true);
        }


        // Helper to create a SkillDialogOptions instance for the SSO skill.
        private void CreateSkillDialogOptions(string botId, SkillConversationIdFactoryBase conversationIdFactory, ConversationState conversationState)
        {
            foreach (var skillInfo in _skillsConfig.Skills.Values)
            {
                var skillDialogOptions = new SkillDialogOptions
                {
                    BotId = botId,
                    ConversationIdFactory = conversationIdFactory,
                    SkillClient = _auth.CreateBotFrameworkClient(),
                    SkillHostEndpoint = _skillsConfig.SkillHostEndpoint,
                    ConversationState = conversationState,
                    Skill = skillInfo,
                };

                AddDialog(new SkillDialog(skillDialogOptions, skillInfo.Id));
            }
        }


        private async Task<DialogTurnResult> SelectSkillStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var messageText = stepContext.Options?.ToString() ?? "What skill would you like to call?";
            var repromptMessageText = "That was not a valid choice, please select a valid skill.";
            var options = new PromptOptions
            {
                Prompt = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput),
                RetryPrompt = MessageFactory.Text(repromptMessageText, repromptMessageText, InputHints.ExpectingInput),
                Choices = _skillsConfig.Skills.Select(skill => new Choice(skill.Value.Id)).ToList()
            };

            // Prompt the user to select a skill.
            return await stepContext.PromptAsync("ActionStepPrompt", options, cancellationToken);
        }


        // Render a prompt to select the action for the skill.
        private async Task<DialogTurnResult> SelectSkillActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the skill info based on the selected skill.
            var selectedSkillId = ((FoundChoice)stepContext.Result).Value;
            var selectedSkill = _skillsConfig.Skills.FirstOrDefault(s => s.Value.Id == selectedSkillId).Value;

            if(selectedSkill == null)
            {
                return await stepContext.PromptAsync("SkillActionPrompt", null, cancellationToken);
            }

            // Remember the skill selected by the user.
            stepContext.Values[SelectedSkillKey] = selectedSkill;

            // Create the PromptOptions with the actions supported by the selected skill.
            var messageText = $"Select an action # to send to **{selectedSkill.Id}** or just type in a message and it will be forwarded to the skill";
            var options = new PromptOptions
            {
                Prompt = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput),
                Choices = GetSkillActions(selectedSkill)
            };

            // Prompt the user to select a skill action.
            return await stepContext.PromptAsync("SkillActionPrompt", options, cancellationToken);
        }

        // Helper method to create Choice elements for the actions supported by the skill.
        private IList<Choice> GetSkillActions(BotFrameworkSkill skill)
        {
            // Note: the bot would probably render this by reading the skill manifest.
            // We are just using hardcoded skill actions here for simplicity.

            var choices = new List<Choice>();
            switch (skill.Id)
            {
                case SkillBotId:
                    choices.Add(new Choice(SkillAction1));
                    choices.Add(new Choice(SkillAction2));
                    break;

            }

            return choices;
        }



        private async Task<DialogTurnResult> HandleActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var selectedSkill = (BotFrameworkSkill)stepContext.Values[SelectedSkillKey];

            var action = ((FoundChoice)stepContext.Result).Value;
            var userId = stepContext.Context.Activity?.From?.Id;
            var userTokenClient = stepContext.Context.TurnState.Get<UserTokenClient>();

            switch (action)
            {
                case SkillActionMessage:
                case SkillAction1:
                case SkillAction2:
                    return await CreateDialogSkillBotActivity(action, stepContext, cancellationToken);

                default:
                    // This should never be hit since the previous prompt validates the choice
                    throw new InvalidOperationException($"Unrecognized action: {action}");
            }
        }

        private async Task<DialogTurnResult> PromptFinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var activeSkill = await _activeSkillProperty.GetAsync(stepContext.Context, () => null, cancellationToken);

            // Check if the skill returned any results and display them.
            if (stepContext.Result != null)
            {
                var assemblyType = GetType();
                var card = AdaptiveCardFactory.CreateAdaptiveCardAttachment(GetType(), "FlightItineraryCard.json"); ;
                var activity = MessageFactory.Attachment(card);


                var message = $"Skill \"{activeSkill.Id}\" invocation complete.";
                // message += $" Result: {JsonConvert.SerializeObject(stepContext.Result)}";
                message += $" Result: {stepContext.Result}";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(message, message, inputHint: InputHints.IgnoringInput), cancellationToken: cancellationToken);
                await stepContext.Context.SendActivityAsync(activity, cancellationToken: cancellationToken);

            }

            // Clear the skill selected by the user.
            stepContext.Values[SelectedSkillKey] = null;

            // Clear active skill in state.
            await _activeSkillProperty.DeleteAsync(stepContext.Context, cancellationToken);

            // Restart the main dialog with a different message the second time around.
            return await stepContext.ReplaceDialogAsync(InitialDialogId, (activeSkill != null)? $"Done with \"{activeSkill.Id}\". \n\n What skill would you like to call?" : null, cancellationToken);
        }


        // Helper method to create the activity to be sent to the DialogSkillBot using selected type and values.
        private async Task<DialogTurnResult> CreateDialogSkillBotActivity(string selectedOption, WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var selectedSkill = (BotFrameworkSkill)stepContext.Values[SelectedSkillKey];

            var action = ((FoundChoice)stepContext.Result).Value;

            var beginSkillActivity = new Activity
            {
                Type = ActivityTypes.Event,
                Name = selectedOption,
                Value = (action == SkillAction2) ? JObject.Parse("{ \"latitude\": 47.614891, \"longitude\": -122.195801}") : null
            };

            // Just forward the message activity to the skill with whatever the user said. 
            if (selectedOption.Equals(SkillActionMessage, StringComparison.CurrentCultureIgnoreCase))
            {
                // Note message activities also support input parameters but we are not using them in this example.
                // Return a deep clone of the activity so we don't risk altering the original one 
                beginSkillActivity = ObjectPath.Clone(stepContext.Context.Activity);
            }

            // Save active skill in state (this is use in case of errors in the AdapterWithErrorHandler).
            await _activeSkillProperty.SetAsync(stepContext.Context, selectedSkill, cancellationToken);

            // We are manually creating the activity to send to the skill; ensure we add the ChannelData and Properties 
            // from the original activity so the skill gets them.
            // Note: this is not necessary if we are just forwarding the current activity from context. 
            beginSkillActivity.ChannelData = stepContext.Context.Activity.ChannelData;
            beginSkillActivity.Properties = stepContext.Context.Activity.Properties;

            // Start the skillDialog instance with the arguments. 
            return await stepContext.BeginDialogAsync(selectedSkill.Id, new BeginSkillDialogOptions { Activity = beginSkillActivity }, cancellationToken);
        }


        // Creates the prompt choices based on the current sign in status
        //private async Task<List<Choice>> GetPromptChoicesAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        //{
        //    var promptChoices = new List<Choice>();
        //    var userId = stepContext.Context.Activity?.From?.Id;
        //    var userTokenClient = stepContext.Context.TurnState.Get<UserTokenClient>();

        //    // Show different options if the user is signed in on the parent or not.
        //    var token = await userTokenClient.GetUserTokenAsync(userId, _connectionName, stepContext.Context.Activity?.ChannelId, null, cancellationToken);
        //    if (token == null)
        //    {
        //        // User is not signed in.
        //        promptChoices.Add(new Choice("Login"));

        //        // Token exchange will fail when the root is not logged on and the skill should 
        //        // show a regular OAuthPrompt
        //        //promptChoices.Add(new Choice("Call Skill (without SSO)"));
        //    }
        //    else
        //    {
        //        // User is signed in to the parent.
        //        promptChoices.Add(new Choice("Logout from the root bot"));
        //        promptChoices.Add(new Choice("Show token"));
        //    }

        //    promptChoices.AddRange(_skillsConfig.Skills.Select(skill => new Choice(skill.Value.Id)).ToList());

        //    return promptChoices;
        //}

    }
}
