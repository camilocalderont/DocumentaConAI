using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Net.Http.Headers;
using OpenAI;

namespace DocumentaConAI.Ai
{

    public class OpenAiApi
    {
        private static readonly HttpClient client = new HttpClient();
        private string ApiKey = "";
        private string Engine;
        private double Temperature;
        private double TopP;
        private double FrequencyPenalty;

        public OpenAiApi(string engine = "text-davinci-003", double temperature = 0.5, double topP = 0.5, double frequencyPenalty = 0.5)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            Engine = engine;
            Temperature = temperature;
            TopP = topP;
            FrequencyPenalty = frequencyPenalty;
        }

        public async Task<string> GenerateTextAsync(string prompt, int maxTokens)
        {
            var requestBody = new
            {
                prompt,
                max_tokens = maxTokens,
                engine = Engine,
                temperature = Temperature,
                top_p = TopP,
                frequency_penalty = FrequencyPenalty
            };

            var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync($"https://api.openai.com/v1/engines/{Engine}/completions", content);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            dynamic jsonData = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
            string generatedText = jsonData.choices[0].text;
            return generatedText;
        }

    }

}
