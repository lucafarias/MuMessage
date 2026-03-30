using MuLog;
using MuMessage;
using Ninject;
using Ninject.Modules;

public class MuModule : NinjectModule
{
    public override void Load()
    {
        // Lega ILogManager alla tua implementazione (es. MyLogManager)
        // Supponiamo che il tuo LogManager sia già inizializzato altrove o lo inizializzi qui
        Bind<ILogManager>().To<LogManager>().InSingletonScope();

        // Lega MessageEngine in Singleton Scope
        Bind<MessageEngine>().ToSelf().InSingletonScope();

        // Opzionale: Sincronizza il Singleton statico con quello di Ninject
        var engine = Kernel.Get<MessageEngine>();
        MessageEngine.SetupDefault(engine);
    }
}