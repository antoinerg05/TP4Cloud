using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace TP4Cloud
{
    public static class CalculInterets
    {
        [Function("CalculInterets")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "calcul-interets")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CalculInterets");
            logger.LogInformation("Azure Function déclenchée pour le calcul des intérêts.");

            try
            {
                // Lire et désérialiser le corps de la requête
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var interets = JsonSerializer.Deserialize<List<Interet>>(requestBody);

                if (interets == null || interets.Count == 0)
                {
                    return CreateErrorResponse(req, "Données invalides ou manquantes.");
                }

                // Calculer les intérêts et sauvegarder en base de données
                await SauvegarderInterets(interets, logger);

                // Réponse HTTP
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync($"Succès : {interets.Count} enregistrements traités.");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"Erreur lors du traitement de la requête : {ex.Message}");
                return CreateErrorResponse(req, "Erreur interne.");
            }
        }

        private static double CalculerInteret(double solde, double taux, DateTime dateDebut, DateTime dateFin)
        {
            var jours = (dateFin - dateDebut).TotalDays;
            return (solde * taux * jours) / 365;
        }

        private static async Task SauvegarderInterets(List<Interet> interets, ILogger logger)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureSQLConnectionString");
            connectionString = "Server=tcp:tp4db.database.windows.net,1433;Initial Catalog=tp4-sql;Persist Security Info=False;User ID=admintp4;Password=TGL2025@;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString)) 
            {
                await connection.OpenAsync();
                foreach (var interet in interets)
                {
                    interet.MontantInteret = CalculerInteret(interet.Solde, interet.Taux, interet.DateDebut, interet.DateFin);

                    string query = @"
                        INSERT INTO Interets (CompteID, Solde, DateDebut, DateFin, Taux, MontantInteret) 
                        VALUES (@CompteID, @Solde, @DateDebut, @DateFin, @Taux, @MontantInteret)";

                    using (var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CompteID", interet.CompteID);
                        command.Parameters.AddWithValue("@Solde", interet.Solde);
                        command.Parameters.AddWithValue("@DateDebut", interet.DateDebut);
                        command.Parameters.AddWithValue("@DateFin", interet.DateFin);
                        command.Parameters.AddWithValue("@Taux", interet.Taux);
                        command.Parameters.AddWithValue("@MontantInteret", interet.MontantInteret);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            logger.LogInformation($"{interets.Count} intérêts calculés et enregistrés en base.");
        }

        private static HttpResponseData CreateErrorResponse(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "application/json");
            response.WriteStringAsync(JsonSerializer.Serialize(new { error = message })).Wait();
            return response;
        }
    }

    public class Interet
    {
        public int CompteID { get; set; }
        public double Solde { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public double Taux { get; set; }
        public double MontantInteret { get; set; }
    }
}
