using EvolutionBot.Api.DTOs;
using System.Threading.Tasks;

namespace EvolutionBot.Api.Services
{
    public interface IMessageQueueService
    {
        void EnqueueMessage(QueuedMessage message);
        Task<QueuedMessage> DequeueMessageAsync(CancellationToken cancellationToken);
    }
}
