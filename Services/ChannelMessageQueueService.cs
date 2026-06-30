using EvolutionBot.Api.DTOs;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading;

namespace EvolutionBot.Api.Services
{
    public class ChannelMessageQueueService : IMessageQueueService
    {
        private readonly Channel<QueuedMessage> _queue;

        public ChannelMessageQueueService()
        {
            _queue = Channel.CreateUnbounded<QueuedMessage>();
        }

        public void EnqueueMessage(QueuedMessage message)
        {
            _queue.Writer.TryWrite(message);
        }

        public async Task<QueuedMessage> DequeueMessageAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
