# SkillBot

Bot Framework v4 Skills with Dialogs sample.

This bot has been created using the [Bot Framework](https://dev.botframework.com); it shows how to use a skill dialog from a root bot.

## Prerequisites

- [.NET Framework SDK](https://dotnet.microsoft.com/download) version 6.0

  ```bash
  # determine dotnet version
  dotnet --version
  ```

## Key concepts in this sample

The solution uses dialogs, within both a parent bot (`RootBot`) and a skill bot (`RootBot`).
It demonstrates how to post activities from the parent bot to the skill bot and return the skill responses to the user.

- `RootBot`: this project shows how to consume a skill bot using a `SkillDialog`. It includes:
  - A [root dialog](RootBot/Dialogs/MainDialog.cs) that can call different actions on a skill using a `SkillDialog`:
    - To send events activities.
    - To send message activities.
    - To cancel a `SkillDialog` using `CancelAllDialogsAsync` that automatically sends an `EndOfConversation` activity to remotely let a skill know that it needs to end a conversation.
  - A sample [AdapterWithErrorHandler](RootBot/AdapterWithErrorHandler.cs) adapter that shows how to handle errors, terminate skills and send traces back to the emulator to help debugging the bot.
  - A [SkillsConfiguration](RootBot/SkillsConfiguration.cs) class that can load skill definitions from the appsettings.json file.
  - A [startup](RootBot/Startup.cs) class that shows how to register the different root bot components for dependency injection.
  - A [BotController](RootBot/Controllers/BotController.cs) that handles skill responses.

- `SkillBot`: this project shows a modified CoreBot that acts as a skill. It receives event and message activities from the parent bot and executes the requested tasks. This project includes:
  - An [ActivityRouterDialog](SkillBot/Dialogs/ActivityRouterDialog.cs) that handles Event and Message activities coming from a parent and performs different tasks.
    - Event activities are routed to specific dialogs using the parameters provided in the `Values` property of the activity.
    - Message activities are sent to CLU if configured and trigger the desired tasks if the intent is recognized.
  - A sample [ActivityHandler](SkillBot/Bots/SkillBot.cs) that uses the `RunAsync` method on `ActivityRouterDialog`.
    

## To try this sample

- Clone the repository.

  ```bash
  git clone https://github.com/dannygar/skillsBot.git
  ```

- Clone the template appsettings.json files in both project directories [RootBot/appsettings.template.json](RootBot/appsettings.template.json) and [RootBot/appsettings.template.json](RootBot/appsettings.template.json) into corresponding `appsettings.json` files
- Create a bot registration in the azure portal for the `RootBot` and update [RootBot/appsettings.json](RootBot/appsettings.json) with the AppId and password.
- Create a bot registration in the azure portal for the `SkillBot` and update [SkillBot/appsettings.json](SkillBot/appsettings.json) with the AppId and password. 
- Update the BotFrameworkSkills section in [RootBot/appsettings.json](RootBot/appsettings.json) with the AppId for the skill you created in the previous step.
- (Optional) Configure the CLUEndpoint, CLUAPIKey and CLUProjectName section in the [RootBot/appsettings.json](RootBot/appsettings.json) if you want to run message activities through LUIS.
- Open the `PersonalAssistantBot.sln` solution and configure it to [start debugging with multiple processes](https://docs.microsoft.com/en-us/visualstudio/debugger/debug-multiple-processes?view=vs-2019#start-debugging-with-multiple-processes).

## Testing the bot using Bot Framework Emulator

[Bot Framework Emulator](https://github.com/microsoft/botframework-emulator) is a desktop application that allows bot developers to test and debug their bots on localhost or running remotely through a tunnel.

- Install the Bot Framework Emulator version 4.8.0 or greater from [here](https://github.com/Microsoft/BotFramework-Emulator/releases)

### Connect to the bot using Bot Framework Emulator

- Launch Bot Framework Emulator
- File -> Open Bot
- Enter a Bot URL of `http://localhost:3978/api/messages`, the `MicrosoftAppId` and `MicrosoftAppPassword` for the `RootBot`

## Deploy the bots to Azure

To learn more about deploying a bot to Azure, see [Deploy your bot to Azure](https://aka.ms/azuredeployment) for a complete list of deployment instructions.
