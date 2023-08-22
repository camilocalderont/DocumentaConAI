using System;
using System.Collections.Generic;
using System.Linq;

namespace DocumentaConAI.Ai
{
    public class TextSplitter
    {
        private const int MaxTokens = 2070;
        // Estimamos que 1 token es aproximadamente 2 caracteres, pero este es un valor muy aproximado.
        private const int EstimatedCharsPerToken = 2;
        private const int EstimatedMaxChars = MaxTokens * EstimatedCharsPerToken;

        public List<string> SplitText(string text)
        {
            var chunks = new List<string>();
            var words = text.Split(' ');

            string chunk = "";
            foreach (var word in words)
            {
                if (chunk.Length + word.Length > EstimatedMaxChars)
                {
                    chunks.Add(chunk);
                    chunk = word;
                }
                else
                {
                    chunk += ' ' + word;
                }
            }

            // Añadir el último chunk
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(chunk);
            }

            return chunks;
        }
    }

}
