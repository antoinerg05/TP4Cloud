using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TP4Cloud
{
    public class CalculInterets
    {
        private readonly ILogger<CalculInterets> _logger;

        public CalculInterets(ILogger<CalculInterets> logger)
        {
            _logger = logger;
        }

        [Function("CalculInterets")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}
