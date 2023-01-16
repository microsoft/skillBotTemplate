// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace Microsoft.Bot.Samples.SkillBot.Dialogs
{
    public class SkillDetails
    {
        /// <summary>
        /// TODO: Enter the properties of your skill object
        /// </summary>
        /// Example:
        [JsonProperty("property")]
        public string Property { get; set; }

        [JsonProperty("date")]
        public string DateProperty { get; set; }

    }
}
