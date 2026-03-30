using MuLog;
using Serilog.Events; 
using System.Threading.Channels;

namespace MuMessage
{
    public sealed class MessageEngine
    {
        private readonly Lock _syncLock = new();
        private readonly Dictionary<Type, List<Subscription>> _subscriptions = [];
        private readonly Channel<object> _messageQueue;
        private readonly ILogManager? _log;
        private const string LogZone = "MESSENGER";

        public static MessageEngine Default { get; private set; } = new MessageEngine();

        // Constructor for Ninject (Dependency Injection)
        public MessageEngine(ILogManager logManager) : this()
        {
            _log = logManager;
            _log.EnableZone(LogZone);
            _log.Write(LogEventLevel.Information, "MessageEngine initialized with logging support.", LogZone);
        }

        private MessageEngine()
        {
            _messageQueue = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                SingleReader = true,
                AllowSynchronousContinuations = false
            });

            _ = Task.Run(ProcessMessagesInternalAsync);
        }

        public void Register<TMessage>(object recipient, Action<TMessage> action) where TMessage : class
        {
            ArgumentNullException.ThrowIfNull(recipient);
            ArgumentNullException.ThrowIfNull(action);

            lock (_syncLock)
            {
                var type = typeof(TMessage);
                if (!_subscriptions.TryGetValue(type, out var list))
                {
                    list = [];
                    _subscriptions[type] = list;
                }

                list.RemoveAll(s => !s.Recipient.IsAlive);
                list.Add(new Subscription(new WeakReference(recipient), (obj) => action((TMessage)obj)));

                _log?.Write(LogEventLevel.Debug, $"Recipient '{recipient.GetType().Name}' registered for message type '{type.Name}'.", LogZone);
            }
        }

        public void Send<TMessage>(TMessage message) where TMessage : class
        {
            ArgumentNullException.ThrowIfNull(message);

            if (_messageQueue.Writer.TryWrite(message))
            {
                _log?.Write(LogEventLevel.Debug, $"Message '{message.GetType().Name}' successfully enqueued.", LogZone);
            }
            else
            {
                _log?.Write(LogEventLevel.Error, $"Failed to enqueue message '{message.GetType().Name}'.", LogZone);
            }
        }

        private async Task ProcessMessagesInternalAsync()
        {
            await foreach (var message in _messageQueue.Reader.ReadAllAsync())
            {
                DeliverMessage(message);
            }
        }

        private void DeliverMessage(object message)
        {
            var messageType = message.GetType();
            List<Subscription> targets = [];

            lock (_syncLock)
            {
                if (_subscriptions.TryGetValue(messageType, out var list))
                {
                    targets = list.Where(s => s.Recipient.IsAlive).ToList();
                    if (targets.Count != list.Count) _subscriptions[messageType] = targets;
                }
            }

            foreach (var sub in targets)
            {
                try
                {
                    if (sub.Recipient.IsAlive)
                    {
                        sub.ActionWrapper(message);
                    }
                }
                catch (Exception ex)
                {
                    _log?.WriteError(ex, $"An error occurred while dispatching message '{messageType.Name}' to a subscriber.", LogZone);
                }
            }
        }

        private record Subscription(WeakReference Recipient, Action<object> ActionWrapper);

        public static void SetupDefault(MessageEngine engine) => Default = engine;
    }
}