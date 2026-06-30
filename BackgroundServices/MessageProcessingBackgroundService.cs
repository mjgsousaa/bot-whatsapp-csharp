using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EvolutionBot.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EvolutionBot.Api.BackgroundServices
{
    public class MessageProcessingBackgroundService : BackgroundService
    {
        private readonly ILogger<MessageProcessingBackgroundService> _logger;
        private readonly IMessageQueueService _messageQueueService;
        private readonly IServiceProvider _serviceProvider; // To resolve scoped services

        public MessageProcessingBackgroundService(
            ILogger<MessageProcessingBackgroundService> logger,
            IMessageQueueService messageQueueService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _messageQueueService = messageQueueService;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviço de processamento de mensagens em segundo plano iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var queuedMessage = await _messageQueueService.DequeueMessageAsync(stoppingToken);
                    _logger.LogInformation("Processando mensagem da fila para {Number}", queuedMessage.Number);

                    // Use a new scope for each message to ensure services are correctly scoped
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var evolutionApiService = scope.ServiceProvider.GetRequiredService<EvolutionApiService>();
                        var success = await evolutionApiService.SendMessage(queuedMessage.InstanceId, new DTOs.EvolutionApi.SendMessageRequest
                        {
                            Number = queuedMessage.Number,
                            TextMessage = queuedMessage.TextMessage
                        });

                        if (success)
                        {
                            _logger.LogInformation("Mensagem enviada com sucesso para {Number}", queuedMessage.Number);
                        }
                        else
                        {
                            _logger.LogError("Falha ao enviar mensagem para {Number}", queuedMessage.Number);
                            // Optionally re-enqueue or log for manual review
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // When the stopping token is canceled, exit the loop
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar mensagem da fila.");
                }

                // Add a small delay to prevent tight looping if the queue is empty or errors occur frequently
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

            _logger.LogInformation("Serviço de processamento de mensagens em segundo plano parado.");
        }
    }
}
