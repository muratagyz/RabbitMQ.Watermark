using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Watermark.UI.Services;
using System.Drawing;
using System.Text;
using System.Text.Json;

namespace RabbitMQ.Watermark.UI.BackgroundServices;

public class ImageWatermarkProcessBackgroundService : BackgroundService
{
    private readonly RabbitMQClientService _clientService;
    private readonly ILogger<ImageWatermarkProcessBackgroundService> _logger;
    private IModel _channel;
    public ImageWatermarkProcessBackgroundService(RabbitMQClientService clientService, ILogger<ImageWatermarkProcessBackgroundService> logger)
    {
        _clientService = clientService;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _channel = _clientService.Connect();

        _channel.BasicQos(0, 1, false);

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        _channel.BasicConsume(queue: RabbitMQClientService.QueueName, false, consumer);

        consumer.Received += ConsumerOnReceived;

        return Task.CompletedTask;
    }

    private Task ConsumerOnReceived(object sender, BasicDeliverEventArgs @event)
    {
        try
        {
            var productImageCreatedEvent =
                JsonSerializer.Deserialize<ProductImageCreatedEvent>(Encoding.UTF8.GetString(@event.Body.ToArray()));

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Images", productImageCreatedEvent.ImageName);

            using var img = Image.FromFile(path);

            using var graphic = Graphics.FromImage(img);

            var siteName = "www.muratagyuz.com";

            var font = new Font(FontFamily.GenericMonospace, 32, FontStyle.Bold, GraphicsUnit.Pixel);

            var textSize = graphic.MeasureString(siteName, font);

            var color = Color.FromArgb(128, 255, 255, 255);

            var brush = new SolidBrush(color);

            var position = new Point(img.Width - ((int)textSize.Width + 30), img.Height - ((int)textSize.Height + 30));

            graphic.DrawString(siteName, font, brush, position);

            img.Save("wwwroot\\Images\\watermarks\\" + productImageCreatedEvent.ImageName);

            img.Dispose();
            graphic.Dispose();

            _channel.BasicAck(@event.DeliveryTag, false);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        return base.StopAsync(cancellationToken);
    }
}