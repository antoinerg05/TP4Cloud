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
                var interets = JsonSerializer.Deserialize<List<Interet>>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (interets == null || interets.Count == 0)
                {
                    logger.LogError("Erreur : JSON vide ou mal formé.");
                    return CreateErrorResponse(req, "Données invalides ou manquantes. Assurez-vous que le JSON est bien formé et qu'il s'agit d'un tableau.");
                }

                logger.LogInformation($"Requête reçue avec {interets.Count} enregistrements à traiter.");

                // Calculer les intérêts et sauvegarder en base de données
                await SauvegarderInterets(interets, logger);

                // Réponse HTTP
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync($"Succès : {interets.Count} enregistrements traités.");
                return response;
            }
            catch (JsonException jsonEx)
            {
                logger.LogError($"Erreur de parsing JSON : {jsonEx.Message}");
                return CreateErrorResponse(req, "Erreur de format JSON. Assurez-vous que votre requête est bien un tableau JSON.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Erreur lors du traitement de la requête : {ex.Message}");
                return CreateErrorResponse(req, "Erreur interne.");
            }
        }

        private static double CalculerInteret(double solde, double taux, DateTime dateDebut, DateTime dateFin)
        {
            if (solde <= 0 || taux <= 0 || dateDebut >= dateFin)
                return 0;

            var jours = (dateFin - dateDebut).TotalDays;
            return (solde * taux * jours) / 365;
        }

        private static async Task SauvegarderInterets(List<Interet> interets, ILogger logger)
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureSQLConnectionString", EnvironmentVariableTarget.Process);
            connectionString = "Server=tcp:tp4db.database.windows.net,1433;Initial Catalog=tp4-sql;Persist Security Info=False;User ID=admintp4;Password=TGL2025@;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

            if (string.IsNullOrEmpty(connectionString))
            {
                logger.LogError("La chaîne de connexion à la base de données est introuvable.");
                throw new Exception("Erreur de configuration : AzureSQLConnectionString est manquant.");
            }

            logger.LogInformation("Connexion à la base de données Azure SQL...");

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    logger.LogInformation("Connexion réussie.");

                    using (var transaction = connection.BeginTransaction()) // Ajout d'une transaction
                    {
                        try
                        {
                            foreach (var interet in interets)
                            {
                                interet.MontantInteret = CalculerInteret(interet.Solde, interet.Taux, interet.DateDebut, interet.DateFin);

                                if (interet.MontantInteret <= 0)
                                {
                                    logger.LogWarning($"Données invalides détectées pour CompteID: {interet.CompteID}, calcul ignoré.");
                                    continue;
                                }

                                string query = @"
                                    INSERT INTO Interets (CompteID, Solde, DateDebut, DateFin, Taux, MontantInteret) 
                                    VALUES (@CompteID, @Solde, @DateDebut, @DateFin, @Taux, @MontantInteret)";

                                using (var command = new SqlCommand(query, connection, transaction))
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

                            transaction.Commit(); // Valider les modifications
                            logger.LogInformation($"{interets.Count} intérêts calculés et enregistrés en base.");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback(); // Annuler la transaction en cas d'échec
                            logger.LogError($"Erreur lors de l'insertion en base : {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Erreur lors de la connexion à la base de données : {ex.Message}");
                throw;
            }
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
