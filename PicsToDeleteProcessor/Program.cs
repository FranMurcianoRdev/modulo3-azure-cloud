//Ejercicio 2
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetEnv;

namespace PicsToDeleteProcessor
{
    class Program
    {
        private const string QueueName = "pics-to-delete";
        private const string HeroBlobContainerName = "heroes"; 
        private const string AlterEgoBlobContainerName = "alteregos"; 

        static async Task Main(string[] args)
        {
            // Obtener la cadena de conexión desde el entorno o configuración
            Env.Load();
            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            // Crear un cliente de la cola
            var queueClient = new QueueClient(connectionString, QueueName);
            await queueClient.CreateIfNotExistsAsync();

            // Crear un cliente de blobs para los contenedores
            var blobServiceClient = new BlobServiceClient(connectionString);
            var heroContainerClient = blobServiceClient.GetBlobContainerClient(HeroBlobContainerName);
            var alterEgoContainerClient = blobServiceClient.GetBlobContainerClient(AlterEgoBlobContainerName);
            await heroContainerClient.CreateIfNotExistsAsync();
            await alterEgoContainerClient.CreateIfNotExistsAsync();

            while (true)
            {
                // Obtener los mensajes de la cola
                QueueMessage[] messages = await queueClient.ReceiveMessagesAsync(maxMessages: 10, visibilityTimeout: TimeSpan.FromSeconds(30));

                foreach (var message in messages)
                {
                    try
                    {
                        // Procesar el mensaje
                        var deleteInfo = JsonSerializer.Deserialize<DeleteInfo>(message.MessageText);

                        if (deleteInfo != null)
                        {
                            // Eliminar los blobs de ambos contenedores
                            await DeleteBlobIfExists(heroContainerClient, deleteInfo.heroImageName);
                            await DeleteBlobIfExists(alterEgoContainerClient, deleteInfo.alterEgoImageName);
                        }

                        // Eliminar el mensaje de la cola
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing message: {ex.Message}");
                    }
                }

                await Task.Delay(1000);
            }
        }

        private static async Task DeleteBlobIfExists(BlobContainerClient containerClient, string blobName)
        {
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
            Console.WriteLine($"Deleted blob: {blobName}");
        }

        private class DeleteInfo
        {
            public required string heroImageName { get; set; }
            public required string alterEgoImageName { get; set; } 
        }
    }
}
