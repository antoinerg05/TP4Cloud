using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Functions.Worker;

namespace BanqueTardi.Functions
{
    public static class CalculInteretsFunction
    {
        [FunctionName("CalculInterets")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Demande de calcul d’intérêts reçue.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<Interet> interets = JsonConvert.DeserializeObject<List<Interet>>(requestBody);

            foreach (var interet in interets)
            {
                // Calcul du montant d'intérêt
                var nbJours = (interet.DateFin - interet.DateDebut).TotalDays;
                interet.MontantInteret = interet.Solde * interet.Taux * (nbJours / 365);

                // Insertion dans la base de données
                await InsererInteret(interet, log);
            }

            return new OkObjectResult("Calcul et insertion réalisés avec succès.");
        }

        private static async Task InsererInteret(Interet interet, ILogger log)
        {
            // Récupération de la chaîne de connexion dans les paramètres d'application
            string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"INSERT INTO Interets 
                                (CompteID, Solde, DateDebut, DateFin, Taux, MontantInteret)
                                VALUES (@CompteID, @Solde, @DateDebut, @DateFin, @Taux, @MontantInteret)";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CompteID", interet.CompteID);
                    command.Parameters.AddWithValue("@Solde", interet.Solde);
                    command.Parameters.AddWithValue("@DateDebut", interet.DateDebut);
                    command.Parameters.AddWithValue("@DateFin", interet.DateFin);
                    command.Parameters.AddWithValue("@Taux", interet.Taux);
                    command.Parameters.AddWithValue("@MontantInteret", interet.MontantInteret);

                    connection.Open();
                    await command.ExecuteNonQueryAsync();
                    log.LogInformation($"CompteID {interet.CompteID} inséré avec MontantInteret = {interet.MontantInteret}");
                }
            }
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
