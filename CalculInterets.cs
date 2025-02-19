using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
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
        public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "calcul-interets")] HttpRequestData req,
        FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CalculInterets");
            logger.LogInformation("Azure Function déclenchée.");

            //var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.WriteString($"Requête reçue : TEST");

            return response;
        }
    }
}
