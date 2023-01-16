using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Bot.Builder;
using JsonExtensionDataAttribute = Newtonsoft.Json.JsonExtensionDataAttribute;
using System.Text;
using Microsoft.Bot.Samples.SkillBot.CLU;

namespace Microsoft.Bot.Samples.SkillBot.CognitiveModels
{
    public class SkillModel: IRecognizerConvert
    {
        public enum Intent
        {
            FirstIntent,
            SecondIntent,
            Cancel,
            None
        };

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text;

        [JsonProperty("alteredText", NullValueHandling = NullValueHandling.Ignore)]
        public string AlteredText;

        [JsonProperty("intents", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<Intent, IntentScore> Intents;

        [JsonProperty("entities", NullValueHandling = NullValueHandling.Ignore)]
        public _Entities _EntitiesList;

        public List<Entity> Entities;

        [JsonProperty("TopIntent", NullValueHandling = NullValueHandling.Ignore)]
        public string TopIntentText;

        public class Entity
        {
            [JsonProperty("category", NullValueHandling = NullValueHandling.Ignore)]
            public string Category;

            [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
            public string Text;

            [JsonProperty("confidence", NullValueHandling = NullValueHandling.Ignore)]
            public string Confidence;

            [JsonProperty("resolutions", NullValueHandling = NullValueHandling.Ignore)]
            public List<_Resolution> Resolutions;
        }

        public class _Entities
        {
            [JsonProperty("entity", NullValueHandling = NullValueHandling.Ignore)]
            public List<Entity> Entity;
        }

        public class _Resolution
        {
            [JsonProperty("resolutionKind", NullValueHandling = NullValueHandling.Ignore)]
            public string ResolutionKind { get; set; }

            [JsonProperty("timex", NullValueHandling = NullValueHandling.Ignore)]
            public string Timex { get; set; }

            [JsonProperty("begin", NullValueHandling = NullValueHandling.Ignore)]
            public string Begin { get; set; }

            [JsonProperty("end", NullValueHandling = NullValueHandling.Ignore)]
            public string End { get; set; }

            [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
            public string Value { get; set; }
        }

        [JsonExtensionData(ReadData = true, WriteData = true)]
        public IDictionary<string, object> Properties {get; set; }

        public void Convert(dynamic result)
        {
            var app = JsonConvert.DeserializeObject<SkillModel>(JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            Text = app.Text;
            AlteredText = app.AlteredText;
            Intents = app.Intents;
            _EntitiesList = app._EntitiesList;
            Entities = ParseEntityDates(_EntitiesList.Entity);
            Properties = app.Properties;
            TopIntentText = app.TopIntentText;
        }


        public (Intent intent, double score) TopIntent()
        {
            Intent maxIntent = Intent.None;
            var max = 0.0;
            foreach (var entry in Intents)
            {
                if (entry.Value.Score > max)
                {
                    maxIntent = entry.Key;
                    max = entry.Value.Score.Value;
                }
            }
            return (maxIntent, max);
        }


        private List<Entity> ParseEntityDates(List<Entity> entities)
        {
            foreach (var entity in entities)
            {
                if (entity.Category == "Date" && entity.Resolutions != null)
                {
                    // Replace the textual expression of the date (range) with the actual resoluted dates
                    var sb = new StringBuilder();

                    foreach (var resolution in entity.Resolutions)
                    {
                        if (resolution.ResolutionKind == CLUResolutionKinds.DateTimeResolution)
                        {
                            entity.Text = resolution.Value;
                        }
                        else if (resolution.ResolutionKind == CLUResolutionKinds.TemporalSpanResolution)
                        {
                            entity.Text = $"from {resolution.Begin} to {resolution.End}";
                        }
                    }
                }
            }

            return entities;
        }
    }
}
