
namespace MuMessage
{
    public interface IMessageEngine
    {
        static abstract MessageEngine Default { get; }

        void Register<TMessage>(object recipient, Action<TMessage> action) where TMessage : class;
        void Send<TMessage>(TMessage message) where TMessage : class;
    }
}