using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace Microsoft.BotBuilderSamples.RootBot.Cards
{
    public static class AdaptiveCardFactory
    {
        // Load attachment from embedded resource.
        public static Attachment CreateAdaptiveCardAttachment(System.Type assemblyType, string cardFileName)
        {
            var cardResourcePath = assemblyType.Assembly.GetManifestResourceNames().First(name => name.EndsWith(cardFileName));

            using (var stream = assemblyType.Assembly.GetManifestResourceStream(cardResourcePath))
            {
                using (var reader = new StreamReader(stream))
                {
                    var adaptiveCard = reader.ReadToEnd();
                    return new Attachment()
                    {
                        ContentType = "application/vnd.microsoft.card.adaptive",
                        Content = JsonConvert.DeserializeObject(adaptiveCard, new JsonSerializerSettings { MaxDepth = null }),
                    };
                }
            }
        }

    }
}
