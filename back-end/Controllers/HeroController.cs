using Microsoft.AspNetCore.Mvc;
using tour_of_heroes_api.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Sas;
using System.Text.Json;

namespace tour_of_heroes_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HeroController(IHeroRepository heroRepository,IConfiguration configuration) : ControllerBase
    {
        private readonly IHeroRepository _heroRepository = heroRepository;
        private readonly IConfiguration _configuration = configuration;

        // GET: api/Hero
        [HttpGet]
        public ActionResult<IEnumerable<Hero>> GetHeroes()
        {
            var heroes = _heroRepository.GetAll();
            return Ok(heroes);
        }

        // GET: api/Hero/5
        [HttpGet("{id}")]
        public ActionResult<Hero> GetHero(int id)
        {
            var hero = _heroRepository.GetById(id);

            if (hero == null)
            {
                return NotFound();
            }

            return Ok(hero);
        }

        // PUT: api/Hero/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        // [HttpPut("{id}")]
        // public ActionResult PutHero(int id, Hero hero)
        // {

        //     var heroToUpdate = _heroRepository.GetById(id);

        //     if (heroToUpdate == null)
        //     {
        //         return NotFound();
        //     }

        //     heroToUpdate.Name = hero.Name;
        //     heroToUpdate.AlterEgo = hero.AlterEgo;
        //     heroToUpdate.Description = hero.Description;

        //     _heroRepository.Update(heroToUpdate);

        //     return NoContent();

        // }
              // PUT: api/Hero/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutHero(int id, Hero hero)
        {
            if (id != hero.Id)
            {
                return BadRequest();
            }

            var oldAlterEgo = _heroRepository.GetById(id).AlterEgo;

            try
            {
                _heroRepository.Update(hero);

                /*********** Background processs (We have to rename the image) *************/
                if (hero.AlterEgo != oldAlterEgo)
                {
                    // Get the connection string from app settings
                    string connectionString = _configuration.GetConnectionString("AzureStorage");

                    // Instantiate a QueueClient which will be used to create and manipulate the queue
                    var queueClient = new QueueClient(connectionString, "alteregos");

                    // Create a queue
                    await queueClient.CreateIfNotExistsAsync();

                    // Create a dynamic object to hold the message
                    var message = new
                    {
                        oldName = oldAlterEgo,
                        newName = hero.AlterEgo
                    };

                    // Send the message
                    await queueClient.SendMessageAsync(JsonSerializer.Serialize(message).ToString());

                }
                /*********** End Background processs *************/
            }
            catch (Exception ex)
            {

                return NotFound(ex.Message);              
                
            }

            return NoContent();
        }

        // POST: api/Hero
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public ActionResult<Hero> PostHero(Hero hero)
        {

            _heroRepository.Add(hero);

            return Ok(hero);

        }

        // DELETE: api/Hero/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHero(int id)
        {
            // Buscar al héroe en la base de datos
            var hero = _heroRepository.GetById(id);

            if (hero == null)
            {
                return NotFound();
            }

            // Obtener los nombres de las imágenes
            var heroImageName = $"{hero.Name.ToLower().Replace(' ', '-')}.jpeg";
            var alterEgoImageName = $"{hero.AlterEgo.ToLower().Replace(' ', '-')}.png";

            try
            {
                // Obtener la cadena de conexión de Azure Storage desde la configuración
                string connectionString = _configuration.GetConnectionString("AzureStorage");

                // Crear un cliente de la cola para pics-to-delete
                var queueClient = new QueueClient(connectionString, "pics-to-delete");

                // Crear la cola si no existe
                await queueClient.CreateIfNotExistsAsync();

                // Construir el mensaje JSON con los nombres de las imágenes a eliminar
                var message = new
                {
                    heroImageName = heroImageName,
                    alterEgoImageName = alterEgoImageName
                };

                // Serializar el mensaje a JSON y enviarlo a la cola
                await queueClient.SendMessageAsync(JsonSerializer.Serialize(message));
            }
            catch (Exception ex)
            {
                return BadRequest($"Error sending delete message to queue: {ex.Message}");
            }

            // Eliminar el héroe de la base de datos
            _heroRepository.Delete(id);

            return NoContent();
        }




        // GET: api/hero/alteregopic/sas
        [HttpGet("alteregopic/sas/{imgName}")]
        public ActionResult GetAlterEgoPicSas(string imgName)
        {
            //Get image from Azure Storage
            string connectionString = _configuration.GetConnectionString("AzureStorage");

            // Create a BlobServiceClient object which will be used to create a container client
            var blobServiceClient = new BlobServiceClient(connectionString);

            //Get container client
            var containerClient = blobServiceClient.GetBlobContainerClient("alteregos");

            //Get blob client
            var blobClient = containerClient.GetBlobClient(imgName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "alteregos",
                BlobName = imgName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(3)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

            Uri sasUri = blobClient.GenerateSasUri(sasBuilder);

            Console.WriteLine($"SAS Uri for blob is: {sasUri}");

            //return image
            return Ok($"{blobServiceClient.Uri}{sasUri.Query}");
        }    
    }
}