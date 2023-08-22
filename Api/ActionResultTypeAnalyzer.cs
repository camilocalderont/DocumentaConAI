using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocumentaConAI.Api
{
    public static class ActionResultTypeAnalyzer
    {
        private static readonly List<string> ActionResultTypes = new List<string>
        {
            "ActionResult",
            "Task<ActionResult",
            "IActionResult",
            "ObjectResult",
            "PartialViewResult",
            "ViewResult",
            "OkObjectResult",
            "BadRequestObjectResult",
            "NotFoundObjectResult",
            "ContentResult",
            "Task"
        };

        private static readonly string ActionResultPattern = @"^(?<actionResultType>" + string.Join("|", ActionResultTypes) + @")\<(?<genericType>[^\>]+)\>$";

        public static bool IsActionResultType(string typeDescription)
        {
            return Regex.IsMatch(typeDescription, ActionResultPattern);
        }

        public static string ExtractActionResultGenericType(string typeDescription)
        {
            var match = Regex.Match(typeDescription, ActionResultPattern);
            if (match.Success)
            {
                return match.Groups["genericType"].Value;
            }
            throw new ArgumentException("The provided type description is not a recognized ActionResult type.");
        }

    }
}
